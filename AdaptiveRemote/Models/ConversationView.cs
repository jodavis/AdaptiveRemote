using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Models;

public class ConversationView : RemoteLayoutElement
{
    internal static readonly MvvmProperty<bool> IsListeningProperty = new(nameof(IsListening));
    internal static readonly MvvmProperty<string> StatusMessageProperty = new(nameof(StatusMessage));
    internal static readonly MvvmProperty<string?> SpeakingMessageProperty = new(nameof(SpeakingMessage));

    public ConversationView(string? placement = null)
        : base(nameof(ConversationView).ToUpperInvariant(), placement)
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

    internal string? SpeakingMessage
    {
        get => GetValue(SpeakingMessageProperty);
        set => SetValue(SpeakingMessageProperty, value);
    }

    public override string ToString() => SpeakingMessage ?? StatusMessage;
}
