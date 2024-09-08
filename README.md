# Dantalion (Archived)

![Dantalion Main Image](Dantalion.png)

## Project Status: Archived

This project has been archived and is no longer actively maintained. It is provided on an as-is basis under the MIT license, allowing for full modification with attribution to the original project/author required.

## Overview

Dantalion was an experimental local AI project that utilized the Anthropic API (Claude 3.5) for natural language processing. The project aimed to create a local interface for AI interactions, with plans for integration of multiple AI models.

## Key Features

- GUI interface for AI interactions
- Integration with Anthropic API (Claude 3.5)
- File handling capabilities
- Console connection for debugging purposes

## Technical Requirements

- .NET 6.0 and 8.0 runtimes
- Python 3.x
- Visual Studio 2022 (for C# component modifications)

### NuGet Packages
- MdXaml (version 1.27.0)
- WPFMediaKit (version 2.2.1)

## Setup and Configuration

### API Key Configuration

To use the Anthropic API, set up an environment variable:

1. Open the Start menu and search for "environment"
2. Select "Edit the system environment variables"
3. In the new window, click "Environment Variables..."
4. Under "User variables", click "New..."
5. Set the variable name as `ANTHROPIC_API_KEY` and the value as your API key

![Environment Variable Setup](image-1.png)
![Environment Variable Setup](image-2.png)
![Environment Variable Setup](image-3.png)
![Environment Variable Setup](image-4.png)

## Usage

### GUI
Run `LocalGPTGUI.exe` from the "Main" folder.

### Console
For console operation or debugging:

1. Navigate to the Python files directory:
   ```
   cd "Path\to\your\python\files"
   ```
2. Run the main script:
   ```
   python anthropic_ai.py
   ```
3. In a second console, run:
   ```
   python console_connect.py
   ```

## Development Notes

- `LocalGPT.dll/exe` must target .NET 6.0 framework for proper Python integration
- `LocalGPTGui.exe` is built with .NET 8.0 framework
- The project consists of a WPF GUI (`LocalGPTGUI`) and a console application (`LocalGPT`)

## Future Development

While this project is archived, a spiritual successor is in development. The new project aims to use local, open-source models to enable offline operation and address data privacy concerns.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
