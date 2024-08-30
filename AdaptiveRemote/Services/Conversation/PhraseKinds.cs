namespace AdaptiveRemote.Services.Conversation;

[Flags]
internal enum PhraseKinds
{
    WakeWord = 1,
    Commands = 2,
    Confirmation = 4,
    Correction = 8,
    More = 16,
    Less = 32,

    CommandsAndConfirmation = Commands | Confirmation,

    None = 0,
    RequiresLastCommand =
        Confirmation
        | Correction
        | More
        | Less,
    RequiresRepeatableCommand =
        More
        | Less,
    RequiresUndoableCommand =
        Less,
    All = WakeWord
        | Commands
        | Confirmation
        | Correction
        | More
        | Less,
}
