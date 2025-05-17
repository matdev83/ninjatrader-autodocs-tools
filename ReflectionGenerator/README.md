# NT8 Docs Builder - Reflection Generator

A .NET tool that generates code scaffolding and documentation from .NET assemblies using reflection. This tool is part of the NT8 Docs Builder project and helps in generating type definitions and documentation for NinjaTrader 8 assemblies.

This tool is used to automatically generate contents of the following repository:
[NinjaTrader 8™ NinjaScript Auto-Generated Scaffolding & Documentation](https://github.com/matdev83/ninjatrader-autodocs/)

## Features

- Generates code scaffolding from .NET assemblies
- Preserves important attributes like `[Serializable]`, `[DataContract]`, and `[Flags]`
- Maintains XML documentation comments
- Handles public types, properties, and methods
- Supports command-line configuration
- Generates partial classes for easy extension

## Requirements

- .NET 9.0 SDK or later
- Windows operating system (for NinjaTrader 8 compatibility)

## Installation

1. Clone the repository:
```bash
git clone https://github.com/matdev83/ninjatrader-autodocs-tools.git
cd ninjatrader-autodocs-tools/tools/ReflectionGenerator
```

2. Build the project:
```bash
dotnet build
```

## Usage

Run the tool with the following command:

```bash
dotnet run -- --dll=<path-to-dll> [--output=<output-directory>]
```

### Parameters

- `--dll`: Path to the DLL file to process (required)
- `--output`: Output directory for generated code (default: `./generated-code`)

### Example

To generate code based on the most important files containing NinjaTrader's public API, you can use the following commands:

```bash
dotnet run -- --dll=<path-to-dll> [--output=<output-directory>]
dotnet run -- --dll="C:\Users\<yourusername>\Documents\NinjaTrader 8\bin\Custom\NinjaTrader.Vendor.dll" -output="<output-directory-root>\Vendor"
dotnet run -- --dll="C:\Users\<yourusername>\Documents\NinjaTrader 8\bin\Custom\NinjaTrader.Custom.dll" -output="<output-directory-root>\Custom"
dotnet run -- --dll="%ProgramFiles%\NinjaTrader 8\bin\NinjaTrader.Client.dll" --output="<output-directory-root>\Client"
```

## Output

The tool generates code files in the specified output directory with the following features:

- Preserved type hierarchy and interfaces
- Public properties with `[DataMember]` attributes
- Public methods with XML documentation
- Proper namespace organization
- Support for partial classes
- Handling of obsolete members with `[Obsolete]` attributes

## Project Structure

```
ReflectionGenerator/
├── Program.cs              # Main program logic
├── ReflectionGenerator.csproj  # Project file
└── generated-code/         # Default output directory
```

## Dependencies

- Mono.Cecil (0.11.6) - For assembly reflection and manipulation

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Disclaimer

- This repository **does not contain any original source code** from NinjaTrader or any of its components.
- All data and code here are derived from **publicly exported structures** that can be legally inferred through .NET reflection.
- The author is **not affiliated with NinjaTrader** or its parent companies.
- **Use of the NinjaTrader 8 platform requires a valid license** and is subject to its licensing agreement.  
  You must comply with all terms and only use these materials if you are properly licensed.