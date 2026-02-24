using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Models;

/// <summary>
/// View model for the modal message display. Holds the currently visible message,
/// updated by <see cref="AdaptiveRemote.Services.ModalMessages.IModalMessageService"/>.
/// </summary>
public class ModalMessageView : MvvmObject
{
    internal static readonly MvvmProperty<string?> CurrentMessageProperty = new(nameof(CurrentMessage));

    /// <summary>Gets or sets the message currently displayed in the modal, or null if no message is shown.</summary>
    internal string? CurrentMessage
    {
        get => GetValue(CurrentMessageProperty);
        set => SetValue(CurrentMessageProperty, value);
    }
}
