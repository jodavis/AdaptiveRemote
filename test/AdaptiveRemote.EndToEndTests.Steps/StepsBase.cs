using AdaptiveRemote.EndtoEndTests.Host;
using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;
using AdaptiveRemote.EndToEndTests.TestServices;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll.BoDi;
using Reqnroll.Infrastructure;

namespace AdaptiveRemote.EndToEndTests.Steps;

public abstract class StepsBase : IContainerDependentObject
{
    private IObjectContainer? _container;
    private ISimulatedEnvironment? _simulatedEnvironment;
    private ILogger? _logger;
    private TestClient? _testClient;

    public void SetObjectContainer(IObjectContainer container) => _container = container;

    public AdaptiveRemoteHost Host => Environment.Host;

    public TestContext TestContext => GetContainerObject<TestContext>();

    public ISimulatedEnvironment Environment => _simulatedEnvironment ??= GetContainerObject<ISimulatedEnvironment>();

    public ILogger Logger => _logger ??= Host.CreateLogger(GetType().Name);

    public TestClient TestClient => _testClient ??= GetContainerObject<TestClient>();

    private ObjectType GetContainerObject<ObjectType>()
        where ObjectType : notnull
    {
        Assert.IsNotNull(_container, "Attempting to access container object before IContainerDependentObject.SetObjectContainer has been called");
        return _container.Resolve<ObjectType>();
    }

    protected void ProvideContainerObject<ObjectType>(ObjectType instance)
        where ObjectType : class
    {
        Assert.IsNotNull(_container, "Attempting to provide container object before IContainerDependentObject.SetObjectContainer has been called");
        _container.RegisterInstanceAs(instance);
    }

    protected void ProvideContainerObjectFactory<ObjectType>(Func<ObjectType> factory)
        where ObjectType : class
    {
        Assert.IsNotNull(_container, "Attempting to provide container object factory before IContainerDependentObject.SetObjectContainer has been called");
        _container.RegisterFactoryAs(factory);
    }
}
