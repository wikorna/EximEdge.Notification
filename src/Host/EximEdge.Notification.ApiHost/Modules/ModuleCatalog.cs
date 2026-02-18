using Email.Api;

namespace EximEdge.Notification.ApiHost.Modules
{
    public static class ModuleCatalog
    {
        public static IServiceCollection AddModule(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddEmailModule(configuration);
            return services;
        }
    }
}
