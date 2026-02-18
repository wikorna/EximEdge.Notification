using Microsoft.Extensions.DependencyInjection;

namespace Email.Application;

public static class Extensions
{
    public static IServiceCollection AddEmailApplication(this IServiceCollection services)
    {
        // Application services
        var appAssembly = typeof(AssemblyMarker).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(appAssembly));
        return services;
    }
}
