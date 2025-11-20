# Copilot Instructions for AdaptiveRemote

## Project Overview
AdaptiveRemote is a remote control application for TV and AV equipment designed for users with limited or total loss of mobility. The application prioritizes accessibility through:
- Voice command support for users with speech capability
- Large-button UI compatible with eye-gaze hardware
- Support for both touch/mouse and specialized input devices

## Technology Stack
- **Platform:** Windows OS only (.NET 8 / net8.0-windows)
- **UI Framework:** WPF with Blazor WebView components (Microsoft.AspNetCore.Components.WebView.Wpf)
- **Language:** C# with nullable reference types enabled
- **Build System:** .NET SDK, MSBuild
- **Testing:** xUnit (in AdaptiveRemote.Tests project)
- **Key Dependencies:**
  - System.Speech for voice recognition
  - Azure services for configuration and monitoring
  - OpenTelemetry for logging and telemetry

## Project Structure
- **AdaptiveRemote:** Main GUI application (WinExe with WPF)
- **AdaptiveRemote.Console:** Console-mode launcher with terminal logging
- **AdaptiveRemote.Tests:** Unit test project

## Development Workflow

### Building
```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the main application
cd AdaptiveRemote && dotnet run

# Run the console application
cd AdaptiveRemote.Console && dotnet run
```

### Testing
```bash
# Run all tests
dotnet test

# Run tests from specific project
cd AdaptiveRemote.Tests && dotnet test
```

## Coding Standards

### Code Style
- Follow the `.editorconfig` rules for formatting and naming conventions
- Code analysis mode is set to Minimum with EnforceCodeStyleInBuild enabled
- Use implicit usings (enabled globally)
- Enable nullable reference types for all code

### Architecture Patterns
- MVVM pattern for UI components
- Dependency injection using Microsoft.Extensions.Hosting
- Service-based architecture (see Services folder)
- Configuration management through Azure Key Vault and appsettings

### Accessibility Guidelines
**CRITICAL:** Always prioritize accessibility in code changes:
1. **Vision accessibility** is the top priority
2. **Speech recognition** support is essential
3. **Eye-gaze input** compatibility must be maintained
4. Keyboard/mouse accessibility is important but less critical
5. **Never introduce accessibility regressions**
6. Test accessibility considerations for all UI changes

## Contribution Guidelines

### Branch Strategy
- Fork the repository and create a feature branch for your changes
- Branch names should include the Jira work ID in the format `ADR-#` (e.g., `ADR-123-feature-name`)
- Issue titles should include the Jira work ID (e.g., "ADR-123: Add new feature") to help track work in Jira

### Code Review
- Code reviews are required before merging
- Self-review is acceptable for solo developers
- Address all review comments before merging

### Testing Requirements
- All unit tests must pass before PR submission
- Add or update tests for code changes
- Test coverage should be maintained or improved

### Commit Messages
- Use clear, descriptive commit messages
- Explain what changed and why
- No strict convention required, but clarity is essential

## Important Files and Conventions
- `.editorconfig`: Code style rules and conventions
- `Directory.Build.props`: Shared MSBuild properties and package references
- `CONTRIBUTING.md`: Detailed contribution guidelines
- `version.json`: Nerdbank.GitVersioning configuration

## Special Considerations
- **Windows-only:** Code cannot be built or tested on Linux/Mac environments
- **Runtime targets:** win-x64 runtime identifier
- **User Secrets:** Project uses UserSecretsId for local development secrets
- **Embedded Resources:** Grammar files for speech recognition are embedded resources
- **LESS compilation:** CSS is compiled from LESS (compilerconfig.json)

## When Making Changes
1. Always check `.editorconfig` for style requirements
2. Ensure nullable reference type annotations are correct
3. Add or update unit tests in AdaptiveRemote.Tests
4. Consider accessibility impact of all UI changes
5. Follow MVVM patterns for view-related code
6. Use dependency injection for services
7. Document complex accessibility features or speech commands
8. Avoid introducing new platform dependencies beyond Windows

## Contact
For questions or support, open an issue and @jodavis.
