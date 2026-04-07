namespace AdaptiveRemote.Contracts;

// Shared behavioral interface — prevents drift between the compiled and raw command types.
// Adding a new behavioral property means updating this interface first; the compiler
// will flag any implementing record that doesn't follow.
public interface ICommandProperties
{
    CommandType Type { get; }
    string Name { get; }
    string Label { get; }
    string? Glyph { get; }
    string SpeakPhrase { get; }
    string? Reverse { get; }
}
