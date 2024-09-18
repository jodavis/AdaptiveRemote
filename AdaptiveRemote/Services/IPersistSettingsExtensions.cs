
namespace AdaptiveRemote.Services;

internal static class IPersistSettingsExtensions
{
    public static void Set(this IPersistSettings persistSettings, string[] nameComponents, object? typedValue)
        => persistSettings.Set(Name(nameComponents), Value(typedValue));
    public static void Set(this IPersistSettings persistSettings, string name, object? typedValue)
        => persistSettings.Set(name, Value(typedValue));
    public static void Set(this IPersistSettings persistSettings, string[] nameComponents, string value)
        => persistSettings.Set(Name(nameComponents), value);

    private static string Name(string[] nameComponents) => string.Join(":", nameComponents);
    private static string Value(object? typedValue)
        => typedValue switch
        {
            // FUTURE: If there are better conversions than "ToString" for certain
            // types, add them here, and add unit tests to PersistSettingsExtensionsTests
            null => string.Empty,
            _ => typedValue.ToString() ?? string.Empty
        };
}
