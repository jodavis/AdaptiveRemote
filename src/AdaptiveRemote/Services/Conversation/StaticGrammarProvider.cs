using System.IO;
using System.Speech.Recognition;
using System.Speech.Recognition.SrgsGrammar;
using System.Xml;

namespace AdaptiveRemote.Services.Conversation;

internal class StaticGrammarProvider : IGrammarProvider
{
    private static readonly Lazy<SrgsDocument> _grammarFile = new(LoadGrammarFile);

    IGrammar IGrammarProvider.LoadGrammar(PhraseKinds phraseKind)
    {
        var grammar = new Grammar(_grammarFile.Value, phraseKind.ToString())
        {
            Name = phraseKind.ToString()
        };
        return new GrammarWrapper(grammar);
    }

    private static SrgsDocument LoadGrammarFile()
    {
        string grammarResourceName = $"{typeof(StaticGrammarProvider).Namespace}.static_grammar.xml";
        using Stream resourceStream = typeof(StaticGrammarProvider).Assembly.GetManifestResourceStream(grammarResourceName)
            ?? throw new Exception($"Could not find assembly resource: {grammarResourceName}");

        return new SrgsDocument(XmlReader.Create(resourceStream));
    }
}
