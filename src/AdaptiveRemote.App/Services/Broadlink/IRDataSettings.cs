namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// Contains programmed IR command payloads, keyed by command name.
/// Values are Base64-encoded IR data to be sent to the Broadlink device.
/// Populated from the <c>IRData</c> section of the programmatic settings file.
/// </summary>
internal class IRDataSettings : Dictionary<string, string> { }
