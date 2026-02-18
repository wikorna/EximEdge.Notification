using EximEdge.Notification.Abstractions.Caching;
using EximEdge.Notification.Abstractions.Messaging;
using EximEdge.Notification.Infrastructure.Caching;
using EximEdge.Notification.Infrastructure.Messaging;
using MassTransit;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace EximEdge.Notification.Infrastructure;

public static class Extensions
{
    /// <summary>
    /// Registers MassTransit with RabbitMQ (or in-memory when disabled) and the shared <see cref="IEventBus"/>.
    /// Call once per host. Pass <paramref name="configureBus"/> to add module consumers in the WorkerHost.
    /// </summary>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        var section = configuration.GetSection(RabbitMqOptions.SectionName);
        services.Configure<RabbitMqOptions>(section);

        var options = section.Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        services.AddMassTransit(cfg =>
        {
            configureBus?.Invoke(cfg);

            if (options.Enabled)
            {
                cfg.UsingRabbitMq((context, rmq) =>
                {
                    rmq.Host(options.Connection.HostName, (ushort)options.Connection.Port, options.Connection.VirtualHost, h =>
                    {
                        h.Username(options.Connection.UserName);
                        h.Password(options.Connection.Password);

                        if (options.Connection.UseSsl)
                        {
                            h.UseSsl(ssl => ssl.ServerName = options.Connection.HostName);
                        }
                    });

                    rmq.ConfigureEndpoints(context);
                });
            }
            else
            {
                cfg.UsingInMemory((context, mem) => mem.ConfigureEndpoints(context));
            }
        });

        services.AddScoped<IEventBus, MassTransitEventBus>();

        return services;
    }

    /// <summary>
    /// Registers HybridCache (L1 in-memory) with optional Redis L2 distributed cache, <see cref="ICacheService"/>,
    /// and a Redis health check. Configuration is read from the <c>Cache</c> section.
    /// <para>When <c>Cache:Enabled</c> is <c>false</c>, HybridCache operates in L1-only mode.</para>
    /// </summary>
    public static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(CacheOptions.SectionName);
        services.Configure<CacheOptions>(section);

        var cacheOpts = section.Get<CacheOptions>() ?? new CacheOptions();

        // L1 in-memory via HybridCache — always active
        services.AddHybridCache(hybrid =>
        {
            hybrid.MaximumPayloadBytes = cacheOpts.MaximumPayloadBytes;
            hybrid.MaximumKeyLength = cacheOpts.MaximumKeyLength;
            hybrid.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(cacheOpts.DefaultExpirationMinutes),
                LocalCacheExpiration = TimeSpan.FromMinutes(cacheOpts.LocalCacheExpirationMinutes)
            };
        });

        if (cacheOpts.Enabled && !string.IsNullOrWhiteSpace(cacheOpts.RedisConnectionString))
        {
            // L2 distributed cache backed by Redis
            services.AddStackExchangeRedisCache(redis =>
            {
                redis.Configuration = cacheOpts.RedisConnectionString;
                redis.InstanceName = cacheOpts.InstanceName;
            });

            // Shared multiplexer for health checks (AbortOnConnectFail=false → app starts even if Redis is down)
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var config = ConfigurationOptions.Parse(cacheOpts.RedisConnectionString);
                config.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(config);
            });
        }

        services.AddScoped<ICacheService, HybridCacheService>();

        // Health check: reports Healthy when Redis disabled, PING latency when enabled
        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);

        return services;
    }
}
