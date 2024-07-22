using System;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MdXaml;
using System.Windows.Controls;
using System.Windows.Input;

namespace LocalGPTGui
{
    public partial class MainWindow : Window
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private Process _pythonProcess;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            LaunchPythonScript();  // Launch the Python script
            ConnectToServer();
            UserInputTextBox.KeyDown += UserInputTextBox_KeyDown;
        }

        private void LaunchPythonScript()
        {
            string pythonPath = "python";  // Or the full path to your Python executable
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "anthropic_ai.py");

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"-u {scriptPath}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };

                // Set environment variables if needed
                // startInfo.EnvironmentVariables["OPEN_API_KEY"] = "your_api_key_here";

                _pythonProcess = new Process { StartInfo = startInfo };
                _pythonProcess.OutputDataReceived += (sender, e) => Debug.WriteLine(e.Data);
                _pythonProcess.ErrorDataReceived += (sender, e) => Debug.WriteLine($"Error: {e.Data}");

                _pythonProcess.Start();
                _pythonProcess.BeginOutputReadLine();
                _pythonProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error launching Python script: {ex.Message}");
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ResponseTextBox.Markdown = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing media: {ex.Message}");
            }
        }

        private void UserInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    // Ctrl + Enter: Insert a new line
                    int caretIndex = UserInputTextBox.CaretIndex;
                    UserInputTextBox.Text = UserInputTextBox.Text.Insert(caretIndex, Environment.NewLine);
                    UserInputTextBox.CaretIndex = caretIndex + Environment.NewLine.Length;
                }
                else
                {
                    // Enter alone: Send the message
                    OnSendButtonClick(sender, e);
                }
                e.Handled = true; // Prevent the default Enter behavior
            }
        }

        private async void OnUploadButtonClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string fileContent = File.ReadAllText(openFileDialog.FileName);
                    AppendToResponseTextBox($"You: (Uploaded file: {Path.GetFileName(openFileDialog.FileName)})\n\n");
                    string response = await SendMessageToServer(fileContent);
                    AppendToResponseTextBox($"Dantalion: {response}\n\n");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading file: {ex.Message}", "File Read Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ConnectToServer()
        {
            int retries = 0;
            while (retries < 5)
            {
                try
                {
                    _client = new TcpClient();
                    _client.ConnectAsync("localhost", 9999).Wait(5000); // 5 second timeout
                    if (_client.Connected)
                    {
                        _stream = _client.GetStream();
                        Debug.WriteLine("Successfully connected to server.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Connection attempt {retries + 1} failed: {ex.Message}");
                    retries++;
                    Thread.Sleep(2000); // Wait 2 seconds before retrying
                }
            }
            MessageBox.Show("Failed to connect to server after 5 attempts. Check if the Python script is running correctly.");
        }

        private async void OnSendButtonClick(object sender, RoutedEventArgs e)
        {
            string userInput = UserInputTextBox.Text;
            if (!string.IsNullOrWhiteSpace(userInput))
            {
                UserInputTextBox.Clear();
                AppendToResponseTextBox($"You: {userInput}\n\n");
                string response = await SendMessageToServer(userInput);
                AppendToResponseTextBox($"Dantalion: {response}\n\n");
            }
        }

        private async Task<string> SendMessageToServer(string message)
        {
            if (_client == null || !_client.Connected)
            {
                Debug.WriteLine("Client is not connected. Attempting to reconnect...");
                ConnectToServer();
                if (_client == null || !_client.Connected)
                {
                    return "Error: Unable to connect to the server.";
                }
            }

            try
            {
                // Create a JSON array with a single message object
                var jsonMessage = new[] { new { role = "user", content = message } };
                string jsonString = System.Text.Json.JsonSerializer.Serialize(jsonMessage);

                byte[] data = Encoding.UTF8.GetBytes(jsonString);
                await _stream.WriteAsync(data, 0, data.Length);

                byte[] responseData = new byte[4096];
                int bytesRead = await _stream.ReadAsync(responseData, 0, responseData.Length);
                string response = Encoding.UTF8.GetString(responseData, 0, bytesRead);

                return response;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SendMessageToServer: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private void AppendToResponseTextBox(string markdownText)
        {
            ResponseTextBox.Markdown += markdownText;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (_pythonProcess != null && !_pythonProcess.HasExited)
            {
                _pythonProcess.Kill();
            }
        }

        private void UserInputTextBox_KeyDown_1(object sender, KeyEventArgs e)
        {

        }
    }
}