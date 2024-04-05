using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Models;

public class Listening : RemoteLayoutElement
{
    internal static readonly MvvmProperty<bool> IsListeningProperty = new(nameof(IsListening));
    internal static readonly MvvmProperty<string> StatusMessageProperty = new(nameof(StatusMessage));

    public Listening(string group)
        : base(group, nameof(Listening).ToUpperInvariant())
    {
    }

    internal string StatusMessage
    {
        get => GetValue(StatusMessageProperty);
        set => SetValue(StatusMessageProperty, value);
    }

    internal bool IsListening
    {
        get => GetValue(IsListeningProperty);
        set => SetValue(IsListeningProperty, value);
    }
}
