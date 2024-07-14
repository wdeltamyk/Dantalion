import socket
import sys

def connect_to_server(host='localhost', port=9999):
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.connect((host, port))
        return sock
    except Exception as e:
        print(f"Error connecting to server: {e}")
        sys.exit(1)

def send_message(sock, message):
    try:
        sock.sendall(message.encode())
        response = sock.recv(4096).decode()
        return response
    except Exception as e:
        print(f"Error communicating with server: {e}")
        return None

def main():
    sock = connect_to_server()
    print("Connected to server. Type 'quit' to exit.")

    while True:
        user_input = input("You: ")
        if user_input.lower() == 'quit':
            break

        response = send_message(sock, user_input)
        if response:
            print(f"AI: {response}")

    sock.close()
    print("Disconnected from server.")

if __name__ == "__main__":
    main()