using IBeam.Identity.Interfaces;
using MedInsights.API.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;

namespace MedInsights.API.DependencyInjection;

public static class OtpAuthServiceDependencyInjection
{
    public static IServiceCollection AddOtpCompleteRetryDecorator(this IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IIdentityOtpAuthService));
        if (descriptor == null) return services;

        services.Remove(descriptor);

        services.Add(new ServiceDescriptor(
            typeof(IIdentityOtpAuthService),
            sp =>
            {
                var inner = CreateService<IIdentityOtpAuthService>(sp, descriptor);
                var logger = sp.GetRequiredService<ILogger<RetryingOtpAuthService>>();
                var configuration = sp.GetRequiredService<IConfiguration>();
                var environment = sp.GetRequiredService<IHostEnvironment>();
                return new RetryingOtpAuthService(inner, logger, configuration, environment);
            },
            descriptor.Lifetime));

        return services;
    }

    private static T CreateService<T>(IServiceProvider sp, ServiceDescriptor descriptor) where T : notnull
    {
        if (descriptor.ImplementationInstance is T instance) return instance;
        if (descriptor.ImplementationFactory != null) return (T)descriptor.ImplementationFactory(sp)!;
        if (descriptor.ImplementationType != null) return (T)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);
        throw new InvalidOperationException($"Invalid service descriptor for {typeof(T).Name}.");
    }
}
