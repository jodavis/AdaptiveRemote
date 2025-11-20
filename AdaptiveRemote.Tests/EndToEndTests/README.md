# End-to-End Tests

This directory contains end-to-end tests for the AdaptiveRemote application.

## ApplicationStartupShutdownTests

### Purpose
Verifies that the application can:
1. Start up successfully
2. Initialize all services
3. Reach a "Ready" state
4. Shut down cleanly when requested
5. Exit without errors

### What It Tests
- **Application Startup**: Creates and starts the application host using the same configuration as production
- **Service Initialization**: Waits for all `IScopedLifecycle` services to initialize successfully
- **Ready State**: Confirms the application reaches `LifecyclePhase.Ready`
- **Shutdown**: Triggers shutdown via the `ShutdownCommand` and confirms clean exit
- **Error Detection**: Collects and reports any errors that occur during startup or shutdown

### Key Features
- **Log Collection**: Captures all application logs during the test for diagnostics
- **Timeout Handling**: Has configurable timeouts for startup (30s) and shutdown (30s)
- **Detailed Diagnostics**: On failure, reports:
  - Application lifecycle phase
  - Fatal errors (if any)
  - Current task name
  - Complete log history
  - Task status

### How It Works
1. Creates a test-specific host without a WPF window (for CI compatibility)
2. Builds the host using the same configuration extensions as the real application
3. Injects a custom logging provider to capture all log output
4. Monitors the `LifecycleView.CurrentPhase` property to detect state changes
5. Waits for `LifecyclePhase.Ready` state
6. Triggers shutdown and waits for clean exit

### Configuration
By default, the test uses the application's default configuration, which includes fake/mock services for TV device connections. This allows the test to run in CI environments without requiring actual hardware.

### Future Enhancements
To test with real device connections, a variant of this test could be created that:
- Accepts command-line arguments or configuration to enable real device services
- Includes additional assertions about device connectivity
- Has longer timeouts to account for network operations
