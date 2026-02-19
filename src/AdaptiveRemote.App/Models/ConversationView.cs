using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Models;

public class ConversationView : RemoteLayoutElement
{
    public static readonly MvvmProperty<bool> IsListeningProperty = new(nameof(IsListening));
    internal static readonly MvvmProperty<string> StatusMessageProperty = new(nameof(StatusMessage));
    internal static readonly MvvmProperty<string?> SpeakingMessageProperty = new(nameof(SpeakingMessage));
    internal static readonly MvvmProperty<Action?> ToggleListeningProperty = new(nameof(ToggleListening));

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

    internal Action? ToggleListening
    {
        get => GetValue(ToggleListeningProperty);
        set => SetValue(ToggleListeningProperty, value);
    }

    public override string ToString() => SpeakingMessage ?? StatusMessage;
}
