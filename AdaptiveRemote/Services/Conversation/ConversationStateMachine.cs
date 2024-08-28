using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Conversation;

internal class ConversationStateMachine
{
    private readonly IReadOnlyDictionary<string, Command> _commands;

    public ConversationStateMachine(IRemoteDefinitionService definitionService, ILogger<ConversationStateMachine> logger)
    {
        Logger = logger;

        _commands = GetCommands(definitionService);
    }

    public ILogger Logger { get; }
    public bool WantsCommands { get; internal set; }
#pragma warning disable CA1822 // Mark members as static -- work in progress, these will non-static later
    public bool WantsAttention => false;
    public bool WantsConfirmation => false;
#pragma warning restore CA1822 // Mark members as static

    internal ConversationResponse RespondTo(IRecognizedSpeech result)
    {
        List<string> phrases = new();
        List<Command> commands = new();

        if (result.TryGetSemanticValue("command", out string? commandName))
        {
            Logger.LogInformation(Message.ConversationController_Recognized, result.Text, commandName);

            if (_commands.TryGetValue(commandName, out Command? command))
            {
                int repeat = ParseRepeat(result);

                Command.ExecuteDelegate? executeAsync = command.ExecuteAsync;
                if (executeAsync is null)
                {
                    Logger.LogError(Message.ConversationController_CommandMissingExecuteAction, command.Name);
                    phrases.Add(Phrases.Conversation_CommandDisabled(command.Name));
                }
                else if (!command.IsEnabled)
                {
                    Logger.LogError(Message.ConversationController_CommandDisabled, command.Name);
                    phrases.Add(Phrases.Conversation_CommandDisabled(command.Name));
                }
                else
                {
                    phrases.Add(Phrases.Conversation_Sent(command.Name, repeat));
                    commands.AddRange(Enumerable.Repeat(command, repeat));
                }
            }
            else
            {
                Logger.LogError(Message.ConversationController_UnknownCommand, result.Text);
            }
        }

        return new(phrases, commands);

        static int ParseRepeat(IRecognizedSpeech result)
        {
            if (result.TryGetSemanticValue("repeat", out string? repeatString) &&
                int.TryParse(repeatString, out int repeat))
            {
                return repeat;
            }
            return 1;
        }
    }

    private static IReadOnlyDictionary<string, Command> GetCommands(IRemoteDefinitionService definitionService)
    {
        Dictionary<string, Command> commands = new(StringComparer.Ordinal);

        foreach (Command command in definitionService.GetCommands())
        {
            commands[command.Name] = command;
        }

        return commands;
    }
}
