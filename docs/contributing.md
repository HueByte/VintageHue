# Contributing to HueByte Mods

Thank you for your interest in contributing to HueByte mods! This guide will help you get started with contributing to any of our projects.

## 🤝 Ways to Contribute

### 🐛 Bug Reports
- Search existing issues before reporting
- Use our bug report templates
- Provide detailed reproduction steps
- Include system information and logs

### 💡 Feature Requests
- Check our roadmap first
- Use feature request templates
- Explain the use case and benefits
- Consider implementation complexity

### 🔧 Code Contributions
- Fork the repository
- Create a feature branch
- Write tests for new functionality
- Follow coding standards
- Submit a pull request

### 📖 Documentation
- Improve existing documentation
- Add examples and tutorials
- Fix typos and formatting
- Translate content to other languages

## 🚀 Getting Started

### Prerequisites
- .NET 8 SDK or later
- Git for version control
- Your preferred IDE (Visual Studio, VS Code, Rider)
- Basic understanding of C# and async programming

### Setup Development Environment

1. **Fork and Clone**
   ```bash
   git clone https://github.com/YourUsername/HueHordes.git
   cd HueHordes
   ```

2. **Install Dependencies**
   ```bash
   dotnet restore
   ```

3. **Run Tests**
   ```bash
   dotnet test
   ```

4. **Build the Project**
   ```bash
   ./scripts/build.ps1
   ```

## 📋 Coding Standards

### Code Style
- Follow existing code patterns
- Use meaningful variable and method names
- Add XML documentation for public APIs
- Keep methods focused and small
- Use async/await for asynchronous operations

### Testing Requirements
- Write unit tests for new functionality
- Maintain high test coverage (>90%)
- Use descriptive test names
- Include both positive and negative test cases
- Test edge cases and error conditions

### Documentation
- Update README files for significant changes
- Add XML documentation for public methods
- Include code examples in documentation
- Update changelog for user-facing changes

## 🔄 Development Workflow

### Branch Naming
- `feature/description` - New features
- `bugfix/issue-number` - Bug fixes
- `docs/topic` - Documentation updates
- `refactor/component` - Code refactoring

### Commit Messages
Follow conventional commit format:
```
type(scope): description

[optional body]

[optional footer]
```

Types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`

Examples:
- `feat(ai): add formation movement patterns`
- `fix(config): resolve hot-reload file watcher issue`
- `docs(commands): update command reference examples`

### Pull Request Process

1. **Before Submitting**
   - Ensure all tests pass
   - Run code formatting
   - Update documentation
   - Rebase on latest main branch

2. **PR Description**
   - Clear title describing the change
   - Detailed description of what was changed
   - Link to related issues
   - Include screenshots for UI changes

3. **Review Process**
   - All PRs require review
   - Address feedback promptly
   - Keep PRs focused and reasonably sized
   - Update PR when main branch changes

## 🧪 Testing Guidelines

### Test Structure
```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedResult()
{
    // Arrange
    var input = CreateTestInput();

    // Act
    var result = await systemUnderTest.MethodName(input);

    // Assert
    result.Should().NotBeNull();
    result.Property.Should().Be(expectedValue);
}
```

### Test Categories
- **Unit Tests**: Test individual components in isolation
- **Integration Tests**: Test component interactions
- **Performance Tests**: Validate performance requirements
- **End-to-End Tests**: Test complete user scenarios

## 📚 Resources

### Documentation
- [Architecture Overview](HueHordes/articles/development/architecture.md)
- [API Reference](HueHordes/api/index.md)
- [Building from Source](HueHordes/articles/development/building.md)

### Community
- [GitHub Discussions](https://github.com/HueByte/HueHordes/discussions)
- [Issue Tracker](https://github.com/HueByte/HueHordes/issues)

### Development Tools
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Visual Studio Code](https://code.visualstudio.com/)
- [Git](https://git-scm.com/)

## ❓ Questions?

If you have questions about contributing:

1. Check existing documentation
2. Search GitHub discussions
3. Ask in community channels
4. Create a discussion thread

We're here to help make your contribution experience positive and productive!

---

Thank you for contributing to HueByte mods! 🎉