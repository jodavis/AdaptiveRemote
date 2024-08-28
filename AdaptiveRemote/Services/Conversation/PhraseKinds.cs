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

    CommandsAndConfirmation = 6,

    None = 0,
    All = 63,
}
