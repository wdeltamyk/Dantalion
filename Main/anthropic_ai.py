import os
import json
import socket
import asyncio
import sys
import clr
import logging
import traceback

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

add_clr_reference('LocalGPT.dll')

try:
    from LocalGPT import FileBrowser, ProgramLauncher
    print("Successfully imported FileBrowser and ProgramLauncher from LocalGPT.dll")
except ImportError as e:
    print(f"Error importing from LocalGPT.dll: {e}")
    sys.exit(1)

file_browser = FileBrowser()
program_launcher = ProgramLauncher()

anthropic_api_key = os.getenv('ANTHROPIC_API_KEY')
client = AsyncAnthropic(api_key=anthropic_api_key)

def load_capabilities():
    with open('capabilities.json', 'r') as f:
        return json.load(f)

capabilities = load_capabilities()

class ChatSession:
    def __init__(self):
        self.system_prompt = json.dumps(capabilities, indent=2)
        self.messages = []

    async def process_message(self, user_message):
        self.messages.append({"role": "user", "content": user_message})
        
        if user_message.startswith("launch_program"):
            return await self.handle_program_launch(user_message)
        elif user_message in ["file modified.", "program terminated."]:
            return await self.handle_program_update()
        elif user_message.startswith(("run_code_in_virtual_env", "scrape_website")):
            return await self.handle_python_command(user_message)
        else:
            response = await client.messages.create(
                model="claude-3-5-sonnet-20240620",
                max_tokens=1024,
                system=self.system_prompt,
                messages=self.messages
            )
            assistant_message = response.content[0].text
            self.messages.append({"role": "assistant", "content": assistant_message})
            return assistant_message

    async def handle_program_launch(self, user_message):
        parts = user_message.split(maxsplit=2)
        program = parts[1]
        arguments = parts[2] if len(parts) > 2 else ""
        
        program_launcher.LaunchProgram(program, arguments)
        launch_response = f"Launched program: {program}" + (f" with arguments: {arguments}" if arguments else "")
        
        response = await client.messages.create(
            model="claude-3-5-sonnet-20240620",
            max_tokens=1024,
            system=self.system_prompt,
            messages=self.messages + [{"role": "user", "content": launch_response}]
        )
        assistant_message = response.content[0].text
        self.messages.append({"role": "assistant", "content": assistant_message})
        return launch_response + "\n" + assistant_message

    async def handle_program_update(self):
        update_message = "Program update received."
        response = await client.messages.create(
            model="claude-3-5-sonnet-20240620",
            max_tokens=1024,
            system=self.system_prompt,
            messages=self.messages + [{"role": "user", "content": update_message}]
        )
        assistant_message = response.content[0].text
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
        self.messages.append({"role": "assistant", "content": assistant_message})
        return f"Command executed. Result:\n{result}\n\nAssistant response:\n{assistant_message}"

async def handle_client_connection(client_socket):
    chat_session = ChatSession()
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
    try:
        asyncio.run(start_server())
    except Exception as e:
        logging.critical(f"Critical error in main: {str(e)}")
        logging.debug(traceback.format_exc())