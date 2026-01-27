using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdaptiveRemote.EndtoEndTests.Logging;

public static class TestContextLoggerExtensions
{
    public static ILoggingBuilder AddTestContext(this ILoggingBuilder builder, TestContext testContext)
        => builder.AddProvider(new TestContextLoggerProvider(testContext));

    public static ILogger GetLogger(this TestContext testContext, string categoryName)
    {
        ILoggerFactory factory = LoggerFactory.Create(builder =>
        {
            builder.AddTestContext(testContext);
        });

        return factory.CreateLogger(categoryName);
    }
}
