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
- Branch names should include the Jira work ID in the format `ADR-#` (e.g., `ADR-123-feature-name`)
- Issue titles should include the Jira work ID (e.g., "ADR-123: Add new feature") to help track work in Jira

### Code Review
- Review your own code before submitting to a human reviewer
- Address all review comments before merging

## Important Files and Conventions
- `.editorconfig`: Code style rules and conventions
- `Directory.Build.props`: Shared MSBuild properties and package references
- `CONTRIBUTING.md`: Detailed contribution guidelines (see this for full process)
- `version.json`: Nerdbank.GitVersioning configuration
- `AdaptiveRemote/Services/Lifecycle/_doc_Lifecycle.md`: Living design & architecture notes for the Lifecycle subsystem
- `AdaptiveRemote/Services/Commands/_doc_Commands.md`: Living design & architecture notes for the Commands subsystem
- `AdaptiveRemote/Services/Broadlink/_doc_Broadlink.md`: Living design & architecture notes for the subsystem that handles IRCommands
- `AdaptiveRemote/Mvvm/_doc_Mvvm.md`: Living design & architecture notes for the MVVM property change notification subsystem

## Special Considerations
- **Windows-only:** Code cannot be built or tested on Linux/Mac environments
- **Runtime targets:** win-x64 runtime identifier
- **User Secrets:** Project uses UserSecretsId for local development secrets
- **Embedded Resources:** Grammar files for speech recognition are embedded resources
- **LESS compilation:** CSS is compiled from LESS (compilerconfig.json)

## When Making Changes
1. Consider accessibility impact of all UI changes
2. Update related documentation (markdown files and XML doc comments) when code changes
3. Document complex accessibility features or speech commands
4. Avoid introducing new platform dependencies beyond Windows

## Communication Guidelines
- If you have questions or need clarification, add them as comments in the issue or PR conversation
- Focus on substance over praise - skip unnecessary compliments that lack depth
- Engage critically with ideas, questioning assumptions and identifying potential issues
- Don't shy away from disagreement when warranted - ensure agreement is grounded in reason and evidence
- Include a confidence score (out of 100) when your response is not 100% certain
- If confidence is below 90%, explain why
- Avoid giving wrong answers with certainty - it's frustrating for users
