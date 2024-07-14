using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
namespace LocalGPT
{
    public class FileBrowser
    {
        public string GetRelativePath(string relativeTo, string path)
        {
            var uri = new Uri(relativeTo);
            var rel = Uri.UnescapeDataString(uri.MakeRelativeUri(new Uri(path)).ToString()).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (rel.Contains(Path.DirectorySeparatorChar.ToString()) == false)
            {
                rel = $".{Path.DirectorySeparatorChar}{rel}";
            }
            return rel;
        }

        public string[] ListFilesInDirectory(string directory)
        {
            try
            {
                return Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                    .Select(path => path ?? string.Empty)
                    .Where(path => !string.IsNullOrEmpty(path))
                    .ToArray();
            }
            catch (Exception e)
            {
                return new[] { e.Message };
            }
        }

        public string ReadFileContent(string filePath)
        {
            try
            {
                return File.ReadAllText(filePath);
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public Dictionary<string, string[]> TraverseDirectory(string directory)
        {
            try
            {
                var fileStructure = new Dictionary<string, string[]>();
                foreach (var dir in Directory.GetDirectories(directory, "*", SearchOption.AllDirectories))
                {
                    var relativePath = GetRelativePath(directory, dir);
                    fileStructure[relativePath] = Directory.GetFiles(dir)
                        .Select(Path.GetFileName)
                        .Where(name => name != null)
                        .ToArray()!;
                }
                return fileStructure;
            }
            catch (Exception e)
            {
                return new Dictionary<string, string[]> { { "error", new[] { e.Message } } };
            }
        }

        public void WriteFile(string filePath, string content)
        {
            File.WriteAllText(filePath, content);
        }
    }

    public class ProgramLauncher
    {
        private const string MMAP_FILE = "localgpt_mmap";
        private const long MMAP_SIZE = 4096; // Increased size for larger messages

        public void LaunchProgram(string programPath, string arguments = "")
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = programPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false
                };

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            WriteToMMap(string.Format("PROGRAM_OUTPUT|{0}", e.Data));
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            WriteToMMap(string.Format("PROGRAM_ERROR|{0}", e.Data));
                    };

                    process.Start();
                    WriteToMMap(string.Format("PROGRAM_LAUNCHED|{0}", process.Id));

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();
                    WriteToMMap("PROGRAM_TERMINATED");
                }
            }
            catch (Exception ex)
            {
                WriteToMMap(string.Format("PROGRAM_LAUNCH_ERROR|{0}", ex.Message));
            }
        }

        private void WriteToMMap(string message)
        {
            try
            {
                using (MemoryMappedFile mmf = MemoryMappedFile.CreateOrOpen(MMAP_FILE, MMAP_SIZE))
                using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(0, MMAP_SIZE))
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(message.PadRight((int)MMAP_SIZE, '\0'));
                    accessor.WriteArray(0, buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to memory-mapped file: {ex.Message}");
            }
        }
    }

    public class MemoryManager
    {
        private const string MEMORY_DIR = "memory";
        private const string OVERALL_MEMORY_PREFIX = "overall_memory_";
        private const int MAX_MEMORY_FILE_SIZE = 5000;

        public MemoryManager()
        {
            Console.WriteLine($"MemoryManager constructor called. Current directory: {Directory.GetCurrentDirectory()}");
            EnsureMemoryDirectoryExists();
        }

        private void EnsureMemoryDirectoryExists()
        {
            string fullPath = Path.GetFullPath(MEMORY_DIR);
            Console.WriteLine($"Attempting to ensure memory directory exists at: {fullPath}");
            if (!Directory.Exists(fullPath))
            {
                try
                {
                    Directory.CreateDirectory(fullPath);
                    Console.WriteLine($"Created memory directory at: {fullPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating memory directory: {ex.Message}");
                    throw;
                }
            }
            else
            {
                Console.WriteLine($"Memory directory already exists at: {fullPath}");
            }
        }

        public string CreateChatMemory()
        {
            Console.WriteLine("CreateChatMemory called");
            EnsureMemoryDirectoryExists();
            string fileName = $"chat_memory_{DateTime.Now:yyyyMMddHHmmss}.mmap";
            string filePath = Path.Combine(MEMORY_DIR, fileName);
            Console.WriteLine($"Attempting to create chat memory file at: {filePath}");
            try
            {
                using (MemoryMappedFile.CreateNew(filePath, MAX_MEMORY_FILE_SIZE))
                {
                    // Just create the file, don't write anything yet
                }
                Console.WriteLine($"Successfully created chat memory file: {filePath}");
                return fileName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating chat memory file: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public void UpdateChatMemory(string fileName, string content)
        {
            string filePath = Path.Combine(MEMORY_DIR, fileName);
            using (var mmf = MemoryMappedFile.OpenExisting(filePath))
            using (var accessor = mmf.CreateViewAccessor())
            {
                byte[] contentBytes = Encoding.UTF8.GetBytes(content);
                accessor.WriteArray(0, contentBytes, 0, contentBytes.Length);
            }
        }

        public string LoadChatMemory(string fileName)
        {
            string filePath = Path.Combine(MEMORY_DIR, fileName);
            using (var mmf = MemoryMappedFile.OpenExisting(filePath))
            using (var accessor = mmf.CreateViewAccessor())
            {
                byte[] buffer = new byte[MAX_MEMORY_FILE_SIZE];
                accessor.ReadArray(0, buffer, 0, buffer.Length);
                return Encoding.UTF8.GetString(buffer).TrimEnd('\0');
            }
        }

        public void UpdateOverallMemory(string summary)
        {
            string[] existingFiles = Directory.GetFiles(MEMORY_DIR, $"{OVERALL_MEMORY_PREFIX}*.mmap");
            string lastFile = existingFiles.OrderByDescending(f => f).FirstOrDefault();

            if (lastFile == null || new FileInfo(lastFile).Length + summary.Length > MAX_MEMORY_FILE_SIZE)
            {
                // Create a new file
                string newFileName = $"{OVERALL_MEMORY_PREFIX}{DateTime.Now:yyyyMMddHHmmss}.mmap";
                string newFilePath = Path.Combine(MEMORY_DIR, newFileName);
                using (var mmf = MemoryMappedFile.CreateNew(newFilePath, MAX_MEMORY_FILE_SIZE))
                using (var accessor = mmf.CreateViewAccessor())
                {
                    byte[] summaryBytes = Encoding.UTF8.GetBytes(summary);
                    accessor.WriteArray(0, summaryBytes, 0, summaryBytes.Length);
                }
            }
            else
            {
                // Append to the existing file
                using (var mmf = MemoryMappedFile.OpenExisting(lastFile))
                using (var accessor = mmf.CreateViewAccessor())
                {
                    long currentLength = accessor.ReadInt64(0);
                    byte[] summaryBytes = Encoding.UTF8.GetBytes(summary);
                    accessor.WriteArray(currentLength, summaryBytes, 0, summaryBytes.Length);
                    accessor.Write(0, currentLength + summaryBytes.Length);
                }
            }
        }

        public List<string> LoadOverallMemory()
        {
            List<string> memories = new List<string>();
            string[] memoryFiles = Directory.GetFiles(MEMORY_DIR, $"{OVERALL_MEMORY_PREFIX}*.mmap");

            foreach (string file in memoryFiles.OrderBy(f => f))
            {
                using (var mmf = MemoryMappedFile.OpenExisting(file))
                using (var accessor = mmf.CreateViewAccessor())
                {
                    long length = accessor.ReadInt64(0);
                    byte[] buffer = new byte[length];
                    accessor.ReadArray(8, buffer, 0, (int)length);
                    memories.Add(Encoding.UTF8.GetString(buffer));
                }
            }

            return memories;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: LocalGPT.exe <command> [args...]");
                return;
            }

            string command = args[0];
            MemoryManager memoryManager = new MemoryManager();
            ProgramLauncher programLauncher = new ProgramLauncher();

            switch (command)
            {
                case "create_chat_memory":
                    string fileName = memoryManager.CreateChatMemory();
                    Console.WriteLine(fileName);
                    break;
                case "update_chat_memory":
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Usage: LocalGPT.exe update_chat_memory <fileName> <content>");
                        return;
                    }
                    memoryManager.UpdateChatMemory(args[1], args[2]);
                    Console.WriteLine("Chat memory updated.");
                    break;
                case "load_chat_memory":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Usage: LocalGPT.exe load_chat_memory <fileName>");
                        return;
                    }
                    string content = memoryManager.LoadChatMemory(args[1]);
                    Console.WriteLine(content);
                    break;
                case "update_overall_memory":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Usage: LocalGPT.exe update_overall_memory <summary>");
                        return;
                    }
                    memoryManager.UpdateOverallMemory(args[1]);
                    Console.WriteLine("Overall memory updated.");
                    break;
                case "load_overall_memory":
                    List<string> memories = memoryManager.LoadOverallMemory();
                    foreach (string memory in memories)
                    {
                        Console.WriteLine(memory);
                        Console.WriteLine("---");
                    }
                    break;
                case "launch_program":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Usage: LocalGPT.exe launch_program <programPath> [arguments]");
                        return;
                    }
                    string programPath = args[1];
                    string arguments = args.Length > 2 ? string.Join(" ", args.Skip(2)) : "";
                    programLauncher.LaunchProgram(programPath, arguments);
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    break;
            }
        }
    }
}