
namespace AdaptiveRemote.Services;

internal static class IPersistSettingsExtensions
{
    public static void Set(this IPersistSettings persistSettings, string[] nameComponents, object? typedValue)
        => persistSettings.Set(Name(nameComponents), Value(typedValue));
    public static void Set(this IPersistSettings persistSettings, string name, object? typedValue)
        => persistSettings.Set(name, Value(typedValue));
    public static void Set(this IPersistSettings persistSettings, string[] nameComponents, string value)
        => persistSettings.Set(Name(nameComponents), value);

    private static string Name(string[] nameComponents) => throw new NotImplementedException();
    private static string Value(object? typedValue) => throw new NotImplementedException();
}
