# API Reference

This folder contains automatically generated API documentation for the HueHordes mod.

## What is API Reference?

API Reference documentation provides detailed information about the code structure, including:

- **Classes and Interfaces**: All public classes, their properties, methods, and constructors
- **Method Signatures**: Parameter types, return values, and detailed descriptions
- **Code Examples**: Usage examples and implementation details
- **Inheritance Hierarchies**: How classes relate to each other and extend base functionality

## How it's Generated

The API documentation is automatically generated from the C# source code using DocFX metadata extraction. This process:

1. Analyzes the HueHordes C# project files
2. Extracts XML documentation comments from the code
3. Creates structured YAML files describing the API
4. Generates browsable HTML documentation

## What You'll Find Here

When the documentation is built, this folder will contain:

- **YAML metadata files** (*.yml) - Structured data about classes and methods
- **Manifest files** - Index of all documented elements
- **Generated pages** - HTML documentation for browsing

These files are automatically generated and should not be manually edited. They are excluded from version control via .gitignore patterns.
