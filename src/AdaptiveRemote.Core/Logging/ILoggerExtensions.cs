using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Logging;

internal static class ILoggerExtensions
{
    private static readonly IReadOnlyDictionary<Message, string> LoggingMessages = InitializeLoggingMessages();

    public static void LogDebug(this ILogger logger, Message message, params object?[] args)
        => logger.LogDebug(message.ToEventId(), FindExceptionIn(args), message.ToMessageText(), args);
    public static void LogInformation(this ILogger logger, Message message, params object?[] args)
        => logger.LogInformation(message.ToEventId(), FindExceptionIn(args), message.ToMessageText(), args);
    public static void LogWarning(this ILogger logger, Message message, params object?[] args)
        => logger.LogWarning(message.ToEventId(), FindExceptionIn(args), message.ToMessageText(), args);
    public static void LogError(this ILogger logger, Message message, params object?[] args)
        => logger.LogError(message.ToEventId(), FindExceptionIn(args), message.ToMessageText(), args);
    public static void LogAndThrowError(this ILogger logger, Message message, params object?[] args)
    {
        string messageText = string.Format(message.ToMessageText(), args);
        Exception exception = new InvalidOperationException(messageText);
        logger.LogError(message.ToEventId(), exception, messageText);
        throw exception;
    }

    private static EventId ToEventId(this Message message)
        => new((int)message, message.ToString());
    private static string ToMessageText(this Message message)
        => LoggingMessages.TryGetValue(message, out string? messageText)
            ? messageText
            : message.ToString();
    private static Exception? FindExceptionIn(object?[] args)
        => args.OfType<Exception>().FirstOrDefault();

    private static IReadOnlyDictionary<Message, string> InitializeLoggingMessages()
    {
        System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        return Enum.GetValues<Message>()
            .Select(x => (key: x, value: typeof(LoggingMessages).GetProperty(x.ToString(), flags)?.GetValue(null)))
            .Where(x => x.value is not null)
            .ToDictionary(x => x.key, x => (string)x.value!);
    }
}
