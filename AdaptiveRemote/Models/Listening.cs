using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Models;

public class Listening : RemoteLayoutElement
{
    internal static readonly MvvmProperty<bool> IsListeningProperty = new(nameof(IsListening));

    public Listening(string group)
        : base(group, nameof(Listening).ToUpperInvariant())
    {
    }

    internal bool IsListening
    {
        get => GetValue(IsListeningProperty);
        set => SetValue(IsListeningProperty, value);
    }
}
