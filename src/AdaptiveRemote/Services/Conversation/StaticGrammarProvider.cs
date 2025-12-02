using System.IO;
using System.Speech.Recognition;
using System.Speech.Recognition.SrgsGrammar;
using System.Xml;

namespace AdaptiveRemote.Services.Conversation;

internal class StaticGrammarProvider : IGrammarProvider
{
    private static readonly Lazy<SrgsDocument> _grammarFile = new(LoadGrammarFile);

    IGrammar IGrammarProvider.LoadGrammar(PhraseKinds phraseKind)
        => new GrammarWrapper(new Grammar(_grammarFile.Value, phraseKind.ToString())
        {
            Name = phraseKind.ToString()
        });

    private static SrgsDocument LoadGrammarFile()
    {
        string grammarResourceName = $"{typeof(StaticGrammarProvider).Namespace}.static_grammar.xml";
        using Stream resourceStream = typeof(StaticGrammarProvider).Assembly.GetManifestResourceStream(grammarResourceName)
            ?? throw new Exception($"Could not find assembly resource: {grammarResourceName}");

        return new SrgsDocument(XmlReader.Create(resourceStream));
    }

    private class GrammarWrapper : IGrammar
    {
        private readonly Grammar _inner;

        public GrammarWrapper(Grammar inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public string? Name
        {
            get => _inner.Name;
            set => _inner.Name = value;
        }

        public bool Enabled
        {
            get => _inner.Enabled;
            set => _inner.Enabled = value;
        }

        public object? GetNativeGrammar() => _inner;
    }
}
