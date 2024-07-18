import os
import json
import socket
import asyncio
import sys
import clr
import logging
import traceback
import re

from python_executors import execute_python_command
from anthropic import AsyncAnthropic

def add_clr_reference(dll_name):
    try:
        dll_path = os.path.abspath(dll_name)
        if not os.path.exists(dll_path):
            raise FileNotFoundError(f"{dll_name} not found at {dll_path}")
        clr.AddReference(dll_path)
    except Exception as e:
        print(f"Error adding CLR reference: {e}")
        sys.exit(1)

add_clr_reference('LocalGPT_FileAccess.dll')

try:
    from LocalGPT_FileAccess import FileBrowser, ProgramLauncher, MemoryManager
    print("Successfully imported FileBrowser, ProgramLauncher, and MemoryManager from LocalGPT_FileAccess.dll")
except ImportError as e:
    print(f"Error importing from LocalGPT_FileAccess.dll: {e}")
    sys.exit(1)

file_browser = FileBrowser()
program_launcher = ProgramLauncher()
memory_manager = MemoryManager()

anthropic_api_key = os.getenv('ANTHROPIC_API_KEY')
client = AsyncAnthropic(api_key=anthropic_api_key)

def load_capabilities():
    with open('capabilities.json', 'r') as f:
        return json.load(f)

def load_purpose():
    with open('purpose.json', 'r') as f:
        return json.load(f)

capabilities = load_capabilities()
purpose = load_purpose()

class ChatSession:
    def __init__(self):
        self.capabilities = capabilities
        self.purpose = purpose
        self.system_prompt = self.create_system_prompt()
        self.messages = []
        try:
            self.chat_memory_file = memory_manager.CreateChatMemory()
        except Exception as e:
            logging.error(f"Failed to create chat memory: {e}")
            self.chat_memory_file = None

    def create_system_prompt(self):
        return f"""
        Capabilities:
        {json.dumps(self.capabilities, indent=2)}

        Purpose and Tone:
        {json.dumps(self.purpose, indent=2)}

        Please adhere to the above capabilities and purpose in all interactions.
        
        When suggesting actions that require system commands, you can naturally incorporate them into your responses. The following commands are available:
        - launch_program [program_name] [optional_arguments]
        - run_code_in_virtual_env [code]
        - scrape_website [url] [optional_subdomain]

        For example, you might say: "Certainly! I can open that file for you. Let me launch_program notepad example.txt"
        """

    def cleanup_messages(self):
        if len(self.messages) > 20:  # Adjust this number as needed
            self.messages = self.messages[-10:]  # Keep only the last 10 messages
        self.update_chat_memory()

    def update_chat_memory(self):
        if self.chat_memory_file:
            try:
                memory_content = json.dumps(self.messages)
                memory_manager.UpdateChatMemory(self.chat_memory_file, memory_content)
            except Exception as e:
                logging.error(f"Failed to update chat memory: {e}")

    def load_chat_memory(self):
        if self.chat_memory_file:
            try:
                memory_content = memory_manager.LoadChatMemory(self.chat_memory_file)
                if memory_content:
                    self.messages = json.loads(memory_content)
            except Exception as e:
                logging.error(f"Failed to load chat memory: {e}")

    async def process_message(self, user_message):
        self.cleanup_messages()
        
        self.messages.append({"role": "user", "content": user_message})
        
        logging.debug(f"Messages before processing: {json.dumps(self.messages, indent=2)}")
        
        response = await client.messages.create(
            model="claude-3-5-sonnet-20240620",
            max_tokens=1024,
            system=self.system_prompt,
            messages=self.messages
        )
        assistant_message = response.content[0].text
        self.messages.append({"role": "assistant", "content": assistant_message})
        
        # Update overall memory
        try:
            memory_manager.UpdateOverallMemory(f"User: {user_message}\nAssistant: {assistant_message}")
        except Exception as e:
            logging.error(f"Failed to update overall memory: {e}")
        
        # Check for commands in the assistant's response
        command_result = await self.check_for_commands(assistant_message)
        if command_result:
            return command_result
        
        return assistant_message

    async def check_for_commands(self, message):
        # Check for launch_program command
        launch_match = re.search(r'launch_program\s+(\S+)(?:\s+(.+))?', message)
        if launch_match:
            program = launch_match.group(1)
            arguments = launch_match.group(2) or ""
            return await self.handle_program_launch(f"launch_program {program} {arguments}")
        
        # Check for run_code_in_virtual_env command
        if "run_code_in_virtual_env" in message:
            return await self.handle_python_command(message)
        
        # Check for scrape_website command
        if "scrape_website" in message:
            return await self.handle_python_command(message)
        
        # Add more command checks here as needed
        
        return None

    async def handle_program_launch(self, user_message):
        parts = user_message.split(maxsplit=2)
        program = parts[1]
        arguments = parts[2] if len(parts) > 2 else ""
        
        try:
            # Run LaunchProgram in a separate thread
            await asyncio.to_thread(program_launcher.LaunchProgram, program, arguments)
            launch_response = f"Launched program: {program}" + (f" with arguments: {arguments}" if arguments else "")
            logging.info(f"Program launch successful: {launch_response}")
        except Exception as e:
            launch_response = f"Failed to launch program: {program}. Error: {str(e)}"
            logging.error(f"Program launch failed: {launch_response}")
        
        response = await client.messages.create(
            model="claude-3-5-sonnet-20240620",
            max_tokens=1024,
            system=self.system_prompt,
            messages=self.messages + [{"role": "user", "content": launch_response}]
        )
        assistant_message = response.content[0].text
        self.messages.append({"role": "user", "content": launch_response})
        self.messages.append({"role": "assistant", "content": assistant_message})
        return launch_response + "\n" + assistant_message

    async def handle_program_update(self, update_type):
        update_message = f"Program update received: {update_type}"
        response = await client.messages.create(
            model="claude-3-5-sonnet-20240620",
            max_tokens=1024,
            system=self.system_prompt,
            messages=self.messages + [{"role": "user", "content": update_message}]
        )
        assistant_message = response.content[0].text
        self.messages.append({"role": "user", "content": update_message})
        self.messages.append({"role": "assistant", "content": assistant_message})
        return assistant_message

    async def handle_python_command(self, command):
        result = execute_python_command(command)
        result_message = f"Python command result: {result}"
        response = await client.messages.create(
            model="claude-3-5-sonnet-20240620",
            max_tokens=1024,
            system=self.system_prompt,
            messages=self.messages + [{"role": "user", "content": result_message}]
        )
        assistant_message = response.content[0].text
        self.messages.append({"role": "user", "content": result_message})
        self.messages.append({"role": "assistant", "content": assistant_message})
        return f"Command executed. Result:\n{result}\n\nAssistant response:\n{assistant_message}"

async def handle_client_connection(client_socket):
    chat_session = ChatSession()
    chat_session.load_chat_memory()  # Load previous chat memory if it exists
    while True:
        try:
            request = await asyncio.get_event_loop().sock_recv(client_socket, 4096)
            request = request.decode().strip()
            if not request:
                logging.info("Client disconnected")
                break
            logging.debug(f"Received request: {request}")
            
            response = await chat_session.process_message(request)
            await asyncio.get_event_loop().sock_sendall(client_socket, response.encode())
        except Exception as e:
            logging.error(f"Error in handle_client_connection: {str(e)}")
            logging.debug(traceback.format_exc())
            break
    client_socket.close()

async def start_server():
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.bind(('0.0.0.0', 9999))
    server.listen(5)
    logging.info('Server listening on port 9999')
    while True:
        try:
            client_sock, addr = await asyncio.get_event_loop().sock_accept(server)
            logging.info(f"New connection from {addr}")
            asyncio.create_task(handle_client_connection(client_sock))
        except Exception as e:
            logging.error(f"Error in start_server: {str(e)}")
            logging.debug(traceback.format_exc())

if __name__ == "__main__":
    logging.basicConfig(level=logging.DEBUG)
    try:
        asyncio.run(start_server())
    except Exception as e:
        logging.critical(f"Critical error in main: {str(e)}")
        logging.debug(traceback.format_exc())