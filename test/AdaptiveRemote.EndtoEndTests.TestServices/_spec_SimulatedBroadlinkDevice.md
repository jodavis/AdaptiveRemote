# Simulated Broadlink device for E2E testing

Initial notes:
# Simulated Broadlink device for E2E testing

Purpose
-------
Provide an in-process simulated Broadlink IR controller for end-to-end tests that behaves like the real device on the wire (UDP) but is controllable and observable by tests.

Goals
-----
- Run in-process following the `SimulatedTiVoDevice` pattern so tests can access recorded messages via DI/service interfaces.
- Implement a simplified discovery protocol so the app will connect to the simulator during tests instead of a real device.
- Use an independent encoder/decoder implementation (not reuse the app's runtime encoder) to avoid masking encoding bugs; share small helper types where safe.
- Expose a test-friendly API to read recorded packets/messages (in-memory) and allow tests to assert non-empty IR payloads. Later we can extend to exact-byte checks and programmable-button behavior.
- Configure simulator activation via .NET `IOptions` and command-line flags (e.g. `--broadlink:Fake=True`) so it integrates with existing launch settings.

Non-goals
---------
- Full, drop-in binary-accurate Broadlink device with every manufacturer nuance. Start with behavior necessary for our services and tests.

Requirements
------------
- The simulator runs in-process and registers services via DI so tests can retrieve a `ISimulatedBroadlinkDevice` interface.
- `ISimulatedEnvironment` will expose two concrete properties: `TiVo` and `Broadlink`. We will remove the generic "get named device" API. Each simulated device may expose its own device-specific API surface; shared environment behavior should be limited to lifecycle and global test helpers.
- The simulator listens for UDP packets on a configurable endpoint and responds using the same packet framing used by the real device (simplified where safe).
- Discovery: implement a test-friendly discovery response that the app's `DeviceLocator` will accept, preventing accidental connections to real devices.
- Encoding/decoding: provide an independent encoder/decoder pair that reproduces the packet layout read by `DeviceConnection` but produces writable packet objects for the simulator to manipulate.
- Recording: record received and sent packets in-memory with timestamps; expose a query API for tests to fetch recorded messages per-test.
- Verification: tests initially only assert that IR payloads are present (non-empty). The simulator should record raw IR bytes so future tests can assert equality.

Design
------

- Project placement: tests/service host area — mirror `SimulatedTiVoDevice` implementation and lifecycle in `test/AdaptiveRemote.EndtoEndTests.TestServices`.

- Public test-facing interface (in-process):
	- `interface ISimulatedBroadlinkDevice`
		- `IReadOnlyList<RecordedPacket> GetRecordedPackets()`
		- `void ClearRecordedPackets()`
		- `Task StartAsync(CancellationToken)` / `Task StopAsync(CancellationToken)` (optional lifecycle control)

- RecordedPacket shape:
	- `DateTimeOffset ReceivedAt`
	- `bool IsInbound` (from app -> device)
	- `PacketType` / CommandCode
	- `byte[] RawPayload` (raw IR bytes when present)
	- `string DebugDescription`

- Discovery behavior:
	- Simulator responds to the UDP discovery probe using a simplified payload that `DeviceLocator` will accept.
	- Discovery response fields (MAC/IP/port) must make the app treat the simulator as a legitimate device.
	- Make discovery configurable (enable/disable) so tests can simulate discovery or pre-configure the app to connect directly.

- Wire protocol handling:
	- Implement an independent `BroadlinkPacketEncoder` and `BroadlinkPacketDecoder` inside the test services area.
	- Decoder must produce writable packet objects (so simulator can mutate fields). Implement the full Broadlink authentication handshake (authenticate request/response, exchange of encryption key, subsequent encrypted payloads) so tests validate auth behavior in the app.
	- Checksum/framing behavior: the simulator should log checksum or framing errors and respond permissively where practical (log + respond), but record a malformed/errored flag on the `RecordedPacket`. Tests should assert on recorded errors when appropriate (so malformed packets become test failures when expected).
	- Do not reference or reuse the app's internal encoder at runtime to avoid masking bugs; unit-test the decoder against sample real-device captures if available. (Note: no real-device captures currently exist in the repo; the author may be able to provide captures later.)

- Integration with tests:
	- The simulator registers `ISimulatedBroadlinkDevice` into DI when the `--broadlink:Fake=True` option is set (bind via `IOptions<BroadlinkTestOptions>` in the test host builder).
	- Tests obtain the `ISimulatedBroadlinkDevice` from the test host's service provider and call `ClearRecordedPackets()` at test start and `GetRecordedPackets()` for assertions.
	- For tests that run the full app host, ensure the app's `DeviceLocator` prefers devices discovered on the loopback address/port used by the simulator when `Fake=True` is set.

Verification strategy
---------------------
- Initial checks: tests assert that a command resulted in at least one recorded outbound packet with a non-empty `RawPayload`.
- Later extensions: allow tests to provide expected raw bytes and assert equality; add programmable-button flows that return specific IR bytes for verification.

Configuration
-------------
 - Expose options type `BroadlinkTestOptions` with:
	- `bool Fake` (default false) — controlled by `--broadlink:Fake=True`
	- `string BindAddress` (restricted to loopback; default `127.0.0.1`)
	- `int DiscoveryPort` / `int DevicePort` (configurable; default ephemeral port for device — bind port `0` — tests should read the actual bound port via the simulator API)
	- `bool EnableDiscoveryResponse` (default true)
	- `string DeviceAddressFilter` (optional) — when provided tests will pass a filter so discovery continues to be exercised but real devices are rejected unless they match the filter.

Implementation clarifications
-----------------------------
1. Real-device captures: None exist currently in the repo; the author may provide captures later. The decoder must be unit-testable against captures when they are available.
2. Encryption/algorithm: Implement the Broadlink authentication/encryption algorithm independently inside the test services (do not call into the app runtime implementation).
3. Device discovery filtering: Tests will pass a `DeviceAddressFilter` so discovery is exercised but real devices will be rejected unless they match the filter. The simulator should honor this filter when composing discovery responses.
4. Bound port visibility: The simulator must expose the actual bound device port via a property on `ISimulatedBroadlinkDevice` (e.g., `BoundPort`) so tests can read it if needed.
5. `ISimulatedEnvironment` shape: expose `TiVo` and `Broadlink` properties as singletons: `ISimulatedEnvironment.TiVo` and `ISimulatedEnvironment.Broadlink`.
6. Malformed packets: The simulator records malformed/errored packets (with a flag on `RecordedPacket`). Tests are responsible for asserting there were no malformed packets when appropriate; the simulator will not throw/fail on receipt.
7. Gherkin placement & step definitions: Add the feature file to the `AdaptiveRemote.EndToEndTests.Features` project. Create new `BroadlinkSteps` step definitions modeled after `TiVoSteps`.
8. Host inclusion: Adding the feature to `AdaptiveRemote.EndToEndTests.Features` will include it in tests for all hosts automatically.

Lifecycle
---------
- Follow `SimulatedTiVoDevice` lifecycle: start when the test host starts, stop when host stops. Support explicit `StartAsync`/`StopAsync` for per-test control if needed.

Implementation notes
--------------------
- Reference implementations: `SimulatedTiVoDevice` (test services) and `src/AdaptiveRemote.App/Services/Broadlink/DeviceConnection.cs` for protocol expectations.
- Keep encoder/decoder code in `test/AdaptiveRemote.EndtoEndTests.TestServices/Broadlink` to avoid coupling with runtime app code.
- Provide a small suite of unit tests for the decoder/encoder using recorded real-device captures (if available) or synthetic packets.
- Create a Gherkin feature that tests a Broadlink command (Power or VolumeUp), similar to the TiVo command test that exists today.
  
Sample Gherkin feature
----------------------
Scenario: Send Power command records non-empty IR payload
    When the app sends the "Power" command to the target device
    Then the simulated Broadlink device should have recorded at least one outbound packet
    And the recorded packet's `RawPayload` should not be empty
    And no recorded packets should be marked as malformed

- After completing this spec, replace it and `_doc_SimulatedTiVoDevice.md` with a combined `_doc_SimulatedDevices.md` that captures the final implementation of both devices, as well as notes on how to add more devices in the future.

Next steps
----------
- Implement `ISimulatedBroadlinkDevice` and in-process simulator following `SimulatedTiVoDevice` skeleton.
- Add options wiring to test host builder and update test launch configs to pass `--broadlink:Fake=True` where appropriate.
- Provide sample test that asserts a non-empty IR payload was recorded.

References
----------
- `src/AdaptiveRemote.App/Services/Broadlink/DeviceConnection.cs`
- `test/AdaptiveRemote.EndtoEndTests.TestServices/SimulatedTiVoDevice` (pattern)
