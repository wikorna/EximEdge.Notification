using EximEdge.Notification.Abstractions.Caching;
using EximEdge.Notification.Abstractions.Messaging;
using EximEdge.Notification.Infrastructure.Caching;
using EximEdge.Notification.Infrastructure.Messaging;
using MassTransit;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EximEdge.Notification.Infrastructure;

public static class Extensions
{
    private static readonly Action<ILogger, string, int, string, string, Exception?> LogRabbitMqConfiguring =
        LoggerMessage.Define<string, int, string, string>(
            LogLevel.Information,
            new EventId(1, nameof(LogRabbitMqConfiguring)),
            "Configuring MassTransit RabbitMQ — Host={Host}, Port={Port}, VHost={VHost}, User={User}");

    /// <summary>
    /// Registers MassTransit with RabbitMQ (or in-memory when disabled) and the shared <see cref="IEventBus"/>.
    /// Call once per host.
    /// <list type="bullet">
    ///   <item><paramref name="configureBus"/> — register module consumers (e.g. <c>cfg.AddEmailConsumers()</c>).</item>
    ///   <item><paramref name="configureEndpoints"/> — explicit receive-endpoint mapping per module.
    ///         When provided, replaces the default <c>ConfigureEndpoints(context)</c> auto-discovery.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureBus = null,
        Action<IBusRegistrationContext, IBusFactoryConfigurator>? configureEndpoints = null)
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
                    var loggerFactory = context.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger("EximEdge.Notification.Infrastructure.Messaging");

                    LogRabbitMqConfiguring(
                        logger,
                        options.Connection.HostName,
                        options.Connection.Port,
                        options.Connection.VirtualHost,
                        options.Connection.UserName,
                        null);

                    rmq.Host(options.Connection.HostName, (ushort)options.Connection.Port, options.Connection.VirtualHost, h =>
                    {
                        h.Username(options.Connection.UserName);
                        h.Password(options.Connection.Password);

                        if (options.Connection.UseSsl)
                        {
                            h.UseSsl(ssl => ssl.ServerName = options.Connection.HostName);
                        }
                    });

                    if (configureEndpoints is not null)
                    {
                        configureEndpoints(context, rmq);
                    }
                    else
                    {
                        rmq.ConfigureEndpoints(context);
                    }
                });
            }
            else
            {
                cfg.UsingInMemory((context, mem) =>
                {
                    if (configureEndpoints is not null)
                    {
                        configureEndpoints(context, mem);
                    }
                    else
                    {
                        mem.ConfigureEndpoints(context);
                    }
                });
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
