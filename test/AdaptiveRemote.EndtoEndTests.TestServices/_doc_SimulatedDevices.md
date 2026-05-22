# Simulated Devices for E2E Testing

Summary: Describes in-process device simulators used in E2E tests — implementing real wire protocols without physical hardware.

## Overview

The AdaptiveRemote test suite includes in-process simulated devices that enable end-to-end testing without requiring physical hardware. These simulators implement the actual wire protocols used by real devices, allowing comprehensive testing of device discovery, authentication, command transmission, and error handling.

## Available Simulated Devices

### SimulatedTiVoDevice

Simulates a TiVo DVR device for testing TiVo command integration.

**Protocol:** TCP-based ASCII protocol with carriage return (`\r`) line terminators  
**Port:** Always uses ephemeral port (0) for test isolation  
**Location:** [`test/AdaptiveRemote.EndtoEndTests.TestServices/SimulatedTiVo/`](./SimulatedTiVo/)

**Implementation:** See [`SimulatedTiVoDevice.cs`](./SimulatedTiVo/SimulatedTiVoDevice.cs)

**Key Features:**
- TCP server accepting commands in TiVo IRCODE format (e.g., `IRCODE PLAY\r`)
- Records all incoming messages with timestamps
- Thread-safe message recording using `ConcurrentBag`
- Ephemeral port support for parallel test execution

### SimulatedBroadlinkDevice

Simulates a Broadlink IR controller for testing IR command transmission to TVs and AV equipment.

**Protocol:** UDP-based binary protocol with authentication, encryption, and checksums  
**Port:** Always uses ephemeral port (0) for test isolation  
**Location:** [`test/AdaptiveRemote.EndtoEndTests.TestServices/SimulatedBroadlink/`](./SimulatedBroadlink/)

**Implementation:** See [`SimulatedBroadlinkDevice.cs`](./SimulatedBroadlink/SimulatedBroadlinkDevice.cs)

**Key Features:**
- UDP server handling discovery, authentication, and command packets
- Independent encoder/decoder implementation (separate from app code to catch encoding bugs)
- AES encryption for authenticated sessions
- Records packets with IR payload data for verification
- Supports discovery protocol with configurable endpoint
- Tracks malformed packets for error testing

## Architecture

### Core Interfaces

The simulated device system is built around a hierarchy of interfaces:

- **[`ISimulatedTiVoDevice`](./SimulatedTiVo/ISimulatedDevice.cs)** - Base interface for simulated devices
- **[`ISimulatedBroadlinkDevice`](./SimulatedBroadlink/ISimulatedBroadlinkDevice.cs)** - Extends base with Broadlink-specific packet recording
- **[`ISimulatedDeviceBuilder`](./SimulatedTiVo/ISimulatedDeviceBuilder.cs)** - Builder pattern for device creation
- **[`ISimulatedEnvironment`](./Host/ISimulatedEnvironment.cs)** - Container managing device lifecycle

### Test Environment Setup

The [`SimulatedEnvironment`](./Host/SimulatedEnvironment.cs) class manages device lifecycle:

1. Accepts `ILoggerFactory` in constructor
2. Creates device builders internally
3. Starts both TiVo and Broadlink devices
4. Exposes devices via `TiVo` and `Broadlink` properties
5. Handles cleanup on disposal

See [`HostSteps.cs`](../../AdaptiveRemote.EndToEndTests.Steps/HostSteps.cs) for test setup implementation.

## Test Integration

### Lifecycle Management

Simulated devices are automatically managed by the test framework via Reqnroll hooks in [`HostSteps.cs`](../../AdaptiveRemote.EndToEndTests.Steps/HostSteps.cs):

1. **BeforeScenario (Order=50):** Creates `SimulatedEnvironment` with `ILoggerFactory`, which internally starts both devices
2. **BeforeScenario (Order=200):** Starts host application with device configuration (IP:Port)
3. **AfterScenario:** Disposes environment, which stops and cleans up devices

### Step Definitions

Test logic is implemented as extension methods for reusability:

- **TiVo Steps:** See [`TiVoSteps.cs`](../../AdaptiveRemote.EndToEndTests.Steps/TiVoSteps.cs)
- **Broadlink Steps:** See [`BroadlinkSteps.cs`](../../AdaptiveRemote.EndToEndTests.Steps/BroadlinkSteps.cs)
- **Broadlink Extensions:** See [`ISimulatedBroadlinkDeviceExtensions.cs`](./SimulatedBroadlink/ISimulatedBroadlinkDeviceExtensions.cs)

### Gherkin Features

Test scenarios use Gherkin syntax:

- **TiVo:** [`TiVoDevice.feature`](../../AdaptiveRemote.EndToEndTests.Features/TiVoDevice.feature)
- **Broadlink:** [`BroadlinkDevice.feature`](../../AdaptiveRemote.EndToEndTests.Features/BroadlinkDevice.feature)
- **Startup:** [`ApplicationStartupAndShutdown.feature`](../../AdaptiveRemote.EndToEndTests.Features/ApplicationStartupAndShutdown.feature)

#### Broadlink Device Feature

Example test scenario - see [`BroadlinkDevice.feature`](../../AdaptiveRemote.EndToEndTests.Features/BroadlinkDevice.feature) for full implementation.

## Protocol Implementation Details

### TiVo Protocol

**Message Format:** ASCII text with `\r` line terminator  
**Command Example:** `IRCODE PLAY\r`

**Key Implementation Details:**
- Custom `ReadLineAsync` to handle `\r` terminator (not `\n` or `\r\n`)
- Messages recorded without line terminator
- Connection handling supports sequential connections (not simultaneous)

See [`SimulatedTiVoDevice.cs`](./SimulatedTiVo/SimulatedTiVoDevice.cs) for complete implementation.

### Broadlink Protocol

**Message Format:** Binary packets with headers, checksums, and encrypted payloads

**Packet Structure:** `[Preamble: 8 bytes] [Header: 0x38 bytes] [Payload: variable]`

For protocol details, see:
- [`BroadlinkPacketDecoder.cs`](./SimulatedBroadlink/BroadlinkPacketDecoder.cs) - Packet parsing
- [`BroadlinkPacketEncoder.cs`](./SimulatedBroadlink/BroadlinkPacketEncoder.cs) - Response building
- [`BroadlinkEncryption.cs`](./SimulatedBroadlink/BroadlinkEncryption.cs) - AES encryption

**Discovery Protocol:**
1. App broadcasts `ScanRequestPacket` to configured address/port
2. Device responds with `ScanResponsePacket` containing MAC, device type, and IP
3. App selects first discovered device

**Authentication Flow:**
1. App sends authenticate request (command 0x65) with default encryption
2. Device generates session ID and encryption key
3. Device responds with encrypted session credentials
4. App switches to session-specific encryption for subsequent commands

**Send Data Flow:**
1. App sends IR data packet (command 0x6A) with session encryption
2. Device decrypts payload and extracts IR bytes
3. Device records packet with IR payload for verification
4. Device sends success response

**Encryption:** AES-128-CBC with device-specific keys after authentication

**Checksums:**
- Payload checksum: Sum of payload bytes + 0xBEAF seed
- Packet checksum: Sum of entire packet (excluding checksum field) + 0xBEAF seed

## Design Decisions

### Why In-Process Simulation?

**Benefits:**
- Simpler lifecycle management (no separate processes)
- Direct access to recorded data (no IPC overhead)
- Easier debugging (single process to attach debugger)
- Reduced test infrastructure complexity

**Tradeoffs:**
- Shares process memory with application under test
- Cannot test process isolation scenarios

### Why Independent Protocol Implementation?

For Broadlink, the simulator uses an independent encoder/decoder separate from the application runtime code.

**Benefits:**
- Catches encoding/decoding bugs that would be masked by shared implementation
- Validates protocol correctness against "real device" behavior
- Enables protocol evolution testing

**Implementation:**
- Shared small types (e.g., constants, simple records) where safe
- Independent crypto/checksum implementations
- Independent packet parsing logic

### Why Loopback and Ephemeral Ports?

**Security:**
- No external network exposure
- No firewall configuration required

**Parallel Testing:**
- Ephemeral ports (port 0) enable parallel test execution
- No port conflicts between test runs

**CI/CD Compatibility:**
- Works in containerized environments
- No admin/root privileges required (except port 80, which we avoid)

## Adding New Simulated Devices

To add a new simulated device:

1. **Create device implementation:**
   - Implement `ISimulatedDevice` interface
   - Create device-specific interface if needed (like `ISimulatedBroadlinkDevice`)
   - Implement wire protocol (TCP, UDP, HTTP, etc.)
   - Record messages/packets for verification

2. **Create builder:**
   - Implement `ISimulatedDeviceBuilder`
   - Support port configuration
   - Return running device instance

3. **Update ISimulatedEnvironment:**
   - Add typed property for new device (e.g., `IRoku`, `IAppleTV`)
   - Update `SimulatedEnvironment` implementation

4. **Create step definitions:**
   - Add steps for device-specific verification
   - Use existing patterns from `TiVoSteps` and `BroadlinkSteps`

5. **Add Gherkin features:**
   - Create feature file testing device integration
   - Cover key scenarios (discovery, commands, errors)

6. **Update test infrastructure:**
   - Register device in `HostSteps.OnBeforeScenario_SetUpSimulatedEnvironment`
   - Configure application to use simulated device

## Testing Best Practices

### Message/Packet Verification

- **Clear before test:** Call `ClearRecordedMessages()` / `ClearRecordedPackets()` at test start
- **Use polling:** Use `WaitHelpers.ExecuteWithRetries` for assertions (accounts for timing)
- **Check specifics:** Verify command format, not just presence
- **Assert no errors:** Check for malformed packets/messages when relevant

### Device Configuration

- **Use loopback:** Always bind simulators to `127.0.0.1` for security
- **Use ephemeral ports:** Bind to port 0 to avoid conflicts
- **Read actual port:** Use `device.Port` to get assigned port number
- **Configure app:** Pass device endpoint to application via command-line args

### Error Handling

- **Record errors:** Simulators should record malformed packets/messages, not throw
- **Test negative cases:** Include tests for malformed input, timeouts, auth failures
- **Clear error messages:** Provide detailed failure messages with recorded data

## Known Limitations

### TiVo Device

- **No response simulation:** Only records incoming messages, doesn't send responses
- **Single connection:** Handles connections sequentially, not simultaneously
- **No replay:** Messages recorded but not replayed

### Broadlink Device

- **Simplified protocol:** Implements minimum required for app testing, not full device spec
- **No real captures:** No real device packet captures for validation (could be added)
- **Basic error handling:** Records errors but doesn't simulate all device error conditions

## Future Enhancements

### General

- **Response simulation:** Allow tests to script device responses
- **Message filtering:** Add query methods for filtering recorded messages
- **Performance metrics:** Track connection counts, durations, bandwidth
- **Snapshot/restore:** Save and restore device state for complex scenarios

### TiVo Specific

- **Bidirectional communication:** Send messages back to application
- **Connection metrics:** Track connection lifecycle

### Broadlink Specific

- **Exact byte verification:** Compare recorded IR bytes against expected values
- **Programmable responses:** Allow tests to define specific IR patterns
- **Real device validation:** Test against captured real device packets
- **Error injection:** Simulate checksum errors, malformed packets, timeouts

## References

- **TiVo Protocol:** I8Beef.TiVo library
- **Broadlink Protocol:** https://github.com/mjg59/python-broadlink/blob/master/protocol.md
- **Test Framework:** Reqnroll (SpecFlow successor) with MSTest
- **Original Specifications:**
  - `_spec_SimulatedTiVoDevice.md` (superseded)
  - `_spec_SimulatedBroadlinkDevice.md` (superseded)
