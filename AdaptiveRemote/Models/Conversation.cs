using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Models;

public class Conversation : RemoteLayoutElement
{
    internal static readonly MvvmProperty<bool> IsListeningProperty = new(nameof(IsListening));
    internal static readonly MvvmProperty<string> StatusMessageProperty = new(nameof(StatusMessage));

    public Conversation(string? placement = null)
        : base(nameof(Conversation).ToUpperInvariant(), placement)
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

    public override string ToString() => StatusMessage;
}
