using AdaptiveRemote.EndtoEndTests.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;
using Reqnroll.BoDi;

namespace AdaptiveRemote.EndToEndTests.Steps.Hooks;

[Binding]
public class LoggerFactoryHooks
{
    [BeforeTestRun(Order = 0)]
    public static void BeforeTestRun_RegisterLoggerFactory(IObjectContainer container)
    {
        container.RegisterInstanceAs(new TestContextLoggerProvider());
        container.RegisterInstanceAs(new HostApplicationLoggerProvider());
        container.RegisterFactoryAs(ctx => LoggerFactory.Create(builder =>
        {
            builder.AddProvider(ctx.Resolve<TestContextLoggerProvider>());
            builder.AddProvider(ctx.Resolve<HostApplicationLoggerProvider>());
        }));
    }

    [BeforeScenario]
    public static void BeforeScenario_RegisterTestContextIntoLogger(TestContextLoggerProvider loggerProvider, TestContext testContext)
    {
        loggerProvider.TestContext = testContext;
    }

    [AfterScenario]
    public static void AfterScenario_ClearTestContextFromLogger(TestContextLoggerProvider loggerProvider)
    {
        loggerProvider.TestContext = null;
    }
}
