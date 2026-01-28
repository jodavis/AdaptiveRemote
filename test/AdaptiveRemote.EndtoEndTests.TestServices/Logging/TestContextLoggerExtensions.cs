using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdaptiveRemote.EndtoEndTests.Logging;

public static class TestContextLoggerExtensions
{
    public static ILoggingBuilder AddTestContext(this ILoggingBuilder builder, TestContext testContext)
        => builder.AddProvider(new TestContextLoggerProvider(testContext));
}
