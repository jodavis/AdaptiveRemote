using System.IO;
using System.Speech.Recognition;
using System.Speech.Recognition.SrgsGrammar;
using System.Xml;

namespace AdaptiveRemote.Services.Conversation;

internal class StaticGrammarProvider : IGrammarProvider
{
    private const string AttentionRuleName = "Attention";
    private const string CommandsRuleName = "Commands";
    private const string YesNoRuleName = "YesNo";

    private static readonly Lazy<SrgsDocument> _grammarFile = new(LoadGrammarFile);

    Grammar IGrammarProvider.LoadAttentionGrammar() => LoadStaticGrammar(AttentionRuleName);
    Grammar IGrammarProvider.LoadCommandsGrammar() => LoadStaticGrammar(CommandsRuleName);
    Grammar IGrammarProvider.LoadYesNoGrammar() => LoadStaticGrammar(YesNoRuleName);

    private static Grammar LoadStaticGrammar(string ruleName)
        => new(_grammarFile.Value, ruleName)
        {
            Name = ruleName
        };

    private static SrgsDocument LoadGrammarFile()
    {
        string grammarResourceName = $"{typeof(StaticGrammarProvider).Namespace}.static_grammar.xml";
        using Stream resourceStream = typeof(StaticGrammarProvider).Assembly.GetManifestResourceStream(grammarResourceName)
            ?? throw new Exception($"Could not find assembly resource: {grammarResourceName}");

        return new SrgsDocument(XmlReader.Create(resourceStream));
    }
}
