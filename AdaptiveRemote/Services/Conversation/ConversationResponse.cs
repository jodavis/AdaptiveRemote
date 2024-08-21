using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Conversation;

internal record ConversationResponse(IEnumerable<string> Phrases, IEnumerable<Command> Commands);
