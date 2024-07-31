using Microsoft.Extensions.DependencyInjection;

namespace AdaptiveRemote.Utilities;

internal static class DecoratorServiceCollectionExtensions
{
    public static IServiceCollection AddDecorator<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService, TService> decoratorFactory,
        ServiceLifetime? lifetime = null)
        where TService : class
    {
        // By convention, the last registration wins
        ServiceDescriptor previousRegistration = services.LastOrDefault(
            descriptor => descriptor.ServiceType == typeof(TService)) ?? throw new InvalidOperationException($"Tried to register a decorator for type {typeof(TService).Name} when no such type was registered.");

        // Get a factory to produce the original implementation
        Func<IServiceProvider, object>? decoratedServiceFactory = previousRegistration.ImplementationFactory;
        if (decoratedServiceFactory is null && previousRegistration.ImplementationInstance != null)
        {
            decoratedServiceFactory = _ => previousRegistration.ImplementationInstance;
        }

        if (decoratedServiceFactory is null && previousRegistration.ImplementationType != null)
        {
            decoratedServiceFactory = serviceProvider => ActivatorUtilities.CreateInstance(
                serviceProvider, previousRegistration.ImplementationType, Array.Empty<object>());
        }

        if (decoratedServiceFactory is null) // Should be impossible
        {
            throw new Exception($"Tried to register a decorator for type {typeof(TService).Name}, but the registration being wrapped specified no implementation at all.");
        }

        ServiceDescriptor registration = new ServiceDescriptor(
            typeof(TService), CreateDecorator, lifetime ?? previousRegistration.Lifetime);

        services.Add(registration);

        return services;

        // Local function that creates the decorator instance
        TService CreateDecorator(IServiceProvider serviceProvider)
        {
            TService decoratedInstance = (TService)decoratedServiceFactory(serviceProvider);
            TService decorator = decoratorFactory(serviceProvider, decoratedInstance);
            return decorator;
        }
    }

    /// <summary>
    /// Registers a <typeparamref name="TService"/> decorator on top of the previous registration of that type.
    /// </summary>
    /// <param name="lifetime">If no lifetime is provided, the lifetime of the previous registration is used.</param>
    public static IServiceCollection AddDecorator<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime? lifetime = null)
        where TService : class
        where TImplementation : TService
    {
        return AddDecorator<TService>(
            services,
            (serviceProvider, decoratedInstance) =>
                ActivatorUtilities.CreateInstance<TImplementation>(serviceProvider, decoratedInstance),
            lifetime);
    }
}
