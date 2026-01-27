using AdaptiveRemote.EndtoEndTests.Host;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll.BoDi;
using Reqnroll.Infrastructure;

namespace AdaptiveRemote.EndToEndTests.Steps;

public abstract class StepsBase : IContainerDependentObject
{
    private IObjectContainer? _container;
    private AdaptiveRemoteHost? _host;
    private ILogger? _logger;

    public void SetObjectContainer(IObjectContainer container) => _container = container;

    public AdaptiveRemoteHost Host => _host ??= GetContainerObject<AdaptiveRemoteHost>();

    public bool IsHostRunning => _host?.IsRunning == true;

    public TestContext TestContext => GetContainerObject<TestContext>();

    public ILogger Logger => _logger ??= Host.CreateLogger(GetType().Name);

    protected ObjectType GetContainerObject<ObjectType>()
        where ObjectType : notnull
    {
        Assert.IsNotNull(_container, "Attempting to access container object before IContainerDependentObject.SetObjectContainer has been called");
        return _container.Resolve<ObjectType>();
    }

    protected ObjectType? GetContainerObjectOrDefault<ObjectType>()
        where ObjectType : class
    {
        if (_container == null)
        {
            return null;
        }

        try
        {
            return _container.Resolve<ObjectType>();
        }
        catch
        {
            return null;
        }
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
