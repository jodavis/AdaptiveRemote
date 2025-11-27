namespace AdaptiveRemote.Services.Conversation;

/// <summary>
/// A simple fake grammar implementation for testing purposes
/// </summary>
internal class FakeGrammar : IGrammar
{
    public FakeGrammar(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public bool Enabled { get; set; }
}
