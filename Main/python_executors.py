import os
import json
import subprocess
import venv
from webscraper import scrape_website

class VirtualEnvManager:
    def __init__(self, env_dir='venv'):
        self.env_dir = env_dir
        self.python_executable = os.path.join(env_dir, 'bin', 'python') if os.name != 'nt' else os.path.join(env_dir, 'Scripts', 'python.exe')

    def create_virtual_env(self):
        if not os.path.exists(self.env_dir):
            venv.create(self.env_dir, with_pip=True)
        else:
            print(f"Virtual environment already exists at {self.env_dir}")

    def install_packages(self, packages):
        subprocess.check_call([self.python_executable, '-m', 'pip', 'install'] + packages)

    def run_code(self, code):
        with open('temp_code.py', 'w') as code_file:
            code_file.write(code)
        result = subprocess.run([self.python_executable, 'temp_code.py'], capture_output=True, text=True)
        os.remove('temp_code.py')
        return result.stdout, result.stderr

def run_code_in_virtual_env(code, packages=None):
    manager = VirtualEnvManager()
    manager.create_virtual_env()
    if packages:
        manager.install_packages(packages)
    stdout, stderr = manager.run_code(code)
    return stdout, stderr

def execute_python_command(command):
    if command.startswith("run_code_in_virtual_env"):
        # Extract code and packages from the command
        parts = command.split(" ", 2)
        if len(parts) < 3:
            return "Invalid command format. Use: run_code_in_virtual_env [packages] code"
        packages = parts[1].strip("[]").split(",") if parts[1] != "[]" else None
        code = parts[2]
        return run_code_in_virtual_env(code, packages)
    elif command.startswith("scrape_website"):
        parts = command.split(" ")
        if len(parts) < 2:
            return "Invalid command format. Use: scrape_website domain [subdomain]"
        domain = parts[1]
        subdomain = parts[2] if len(parts) > 2 else None
        return scrape_website(domain, subdomain)
    else:
        return "Unknown Python command."

# Example usage
if __name__ == "__main__":
    # This part is for testing the functions standalone
    print(execute_python_command("run_code_in_virtual_env [] print('Hello, World!')"))
    print(execute_python_command("scrape_website example.com"))