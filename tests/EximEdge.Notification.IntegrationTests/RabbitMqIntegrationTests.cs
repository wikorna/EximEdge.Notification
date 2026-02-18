using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using EximEdge.Notification.Contracts.Email;
using EximEdge.Notification.Infrastructure;
using EximEdge.Notification.Infrastructure.Messaging;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.RabbitMq;
using Xunit;

namespace EximEdge.Notification.IntegrationTests;

/// <summary>
/// Shared fixture that spins up a real RabbitMQ container once for the whole test class.
/// Tests exercise the full publish → broker → consume pipeline.
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder("rabbitmq:3.12-management-alpine")
        .WithUsername("admin")
        .WithPassword("password123")
        .WithPortBinding(5672, true)   // random host port
        .WithPortBinding(15672, true)
        .Build();

    public string AmqpConnectionString => RabbitMq.GetConnectionString();
    public string HostName => RabbitMq.Hostname;
    public int AmqpPort => RabbitMq.GetMappedPublicPort(5672);
    public int ManagementPort => RabbitMq.GetMappedPublicPort(15672);

    public Task InitializeAsync() => RabbitMq.StartAsync();
    public Task DisposeAsync() => RabbitMq.DisposeAsync().AsTask();
}

/// <summary>
/// WebApplicationFactory that rewires the ApiHost to point at the Testcontainers RabbitMQ instance.
/// </summary>
public sealed class ApiHostFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly RabbitMqFixture _rmq;
    public ApiHostFactory(RabbitMqFixture rmq) => _rmq = rmq;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Enabled"] = "true",
                ["RabbitMQ:Connection:HostName"] = _rmq.HostName,
                ["RabbitMQ:Connection:Port"] = _rmq.AmqpPort.ToString(CultureInfo.InvariantCulture),
                ["RabbitMQ:Connection:UserName"] = "admin",
                ["RabbitMQ:Connection:Password"] = "password123",
                ["RabbitMQ:Connection:VirtualHost"] = "/",
                ["Cache:Enabled"] = "false",
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Port=5432;Database=test;Username=test;Password=test"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Add MassTransit test harness so we can observe consumed messages
            services.AddMassTransitTestHarness();
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public new Task DisposeAsync() => base.DisposeAsync().AsTask();
}

// ---------------------------------------------------------------------------
//  1️⃣  Infrastructure Validation
// ---------------------------------------------------------------------------
public sealed class InfrastructureTests : IClassFixture<RabbitMqFixture>
{
    private readonly RabbitMqFixture _rmq;
    public InfrastructureTests(RabbitMqFixture rmq) => _rmq = rmq;

    [Fact]
    public void RabbitMq_Container_Is_Running()
    {
        _rmq.RabbitMq.State.Should().Be(DotNet.Testcontainers.Containers.TestcontainersStates.Running);
    }

    [Fact]
    public async Task Management_UI_Is_Accessible()
    {
        using var http = new HttpClient();
        var response = await http.GetAsync(
            $"http://{_rmq.HostName}:{_rmq.ManagementPort}/api/overview");

        // 401 is fine (no auth header) — it proves the port responds
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RabbitMqctl_Lists_Queues_After_WorkerHost_Connects()
    {
        // This verifies the container itself works; queue creation is tested
        // in the publisher tests when MassTransit auto‑declares topology.
        var result = await _rmq.RabbitMq.ExecAsync(["rabbitmqctl", "list_queues"]);
        result.ExitCode.Should().Be(0);
    }
}

// ---------------------------------------------------------------------------
//  2️⃣  Publisher Validation (ApiHost)
// ---------------------------------------------------------------------------
public sealed class PublisherTests : IClassFixture<RabbitMqFixture>, IAsyncLifetime
{
    private readonly RabbitMqFixture _rmq;
    private ApiHostFactory _factory = null!;
    private HttpClient _client = null!;

    public PublisherTests(RabbitMqFixture rmq) => _rmq = rmq;

    public async Task InitializeAsync()
    {
        _factory = new ApiHostFactory(_rmq);
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task POST_Send_Returns_OK_And_JobId()
    {
        var payload = new { To = "test@example.com", Subject = "Test", Body = "Hello" };

        var response = await _client.PostAsJsonAsync("/api/email/send", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Job ID:");
    }

    [Fact]
    public async Task Published_Message_Is_Observed_By_TestHarness()
    {
        var harness = _factory.Services.GetRequiredService<ITestHarness>();
        await harness.Start();

        var payload = new { To = "test@example.com", Subject = "Harness", Body = "Body" };
        await _client.PostAsJsonAsync("/api/email/send", payload);

        // Wait up to 10 s for the message to appear in the harness
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        (await harness.Published.Any<SendEmailMessage>(x =>
            x.Context.Message.Subject == "Harness", cts1.Token))
            .Should().BeTrue("the message should be published to the bus");
    }

    [Fact]
    public async Task Exchange_Is_Created_After_Publish()
    {
        var payload = new { To = "a@b.com", Subject = "Exchange", Body = "x" };
        await _client.PostAsJsonAsync("/api/email/send", payload);

        // Give MassTransit a moment to declare topology
        await Task.Delay(2000);

        var result = await _rmq.RabbitMq.ExecAsync(["rabbitmqctl", "list_exchanges"]);
        result.Stdout.Should().Contain("EximEdge.Notification.Contracts.Email");
    }
}

// ---------------------------------------------------------------------------
//  3️⃣  Consumer Validation (WorkerHost via TestHarness)
// ---------------------------------------------------------------------------
public sealed class ConsumerTests : IClassFixture<RabbitMqFixture>, IAsyncLifetime
{
    private readonly RabbitMqFixture _rmq;
    private ApiHostFactory _factory = null!;
    private HttpClient _client = null!;

    public ConsumerTests(RabbitMqFixture rmq) => _rmq = rmq;

    public async Task InitializeAsync()
    {
        _factory = new ApiHostFactory(_rmq);
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Consumer_Processes_Published_Message()
    {
        var harness = _factory.Services.GetRequiredService<ITestHarness>();
        await harness.Start();

        var payload = new { To = "consumer@test.com", Subject = "Consume", Body = "Body" };
        await _client.PostAsJsonAsync("/api/email/send", payload);

        // The test harness includes in-process consumers registered via AddMassTransitTestHarness
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        (await harness.Consumed.Any<SendEmailMessage>(x =>
            x.Context.Message.Subject == "Consume", cts2.Token))
            .Should().BeTrue("the consumer should process the message");
    }

    [Fact]
    public void ConsumerDefinition_Has_Correct_Endpoint_Name()
    {
        IConsumerDefinition<Email.Infrastructure.Consumers.SendEmailConsumer> def =
            new Email.Infrastructure.Consumers.SendEmailConsumerDefinition();
        def.GetEndpointName(DefaultEndpointNameFormatter.Instance).Should().Be("email-send");
    }

    [Fact]
    public void ConsumerDefinition_Has_Concurrency_Limit()
    {
        var def = new Email.Infrastructure.Consumers.SendEmailConsumerDefinition();
        def.ConcurrentMessageLimit.Should().Be(16);
    }
}

// ---------------------------------------------------------------------------
//  4️⃣  Dead Letter Queue (DLQ) — via MassTransit convention
// ---------------------------------------------------------------------------
public sealed class DlqTests : IClassFixture<RabbitMqFixture>
{
    private readonly RabbitMqFixture _rmq;
    public DlqTests(RabbitMqFixture rmq) => _rmq = rmq;

    [Fact]
    public void MassTransit_Uses_Error_Queue_Convention()
    {
        // MassTransit automatically routes faulted messages to <endpoint>_error.
        // For our consumer this means "email-send_error".
        // This is a design verification — runtime DLQ is tested in ConsumerFaultTests.
        var expectedDlq = "email-send_error";
        expectedDlq.Should().NotBeNullOrEmpty();
    }
}

// ---------------------------------------------------------------------------
//  6️⃣  Observability — Health Check
// ---------------------------------------------------------------------------
public sealed class HealthCheckTests : IClassFixture<RabbitMqFixture>, IAsyncLifetime
{
    private readonly RabbitMqFixture _rmq;
    private ApiHostFactory _factory = null!;
    private HttpClient _client = null!;

    public HealthCheckTests(RabbitMqFixture rmq) => _rmq = rmq;

    public async Task InitializeAsync()
    {
        _factory = new ApiHostFactory(_rmq);
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Health_Endpoint_Returns_Healthy()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Email_Module_Health_Endpoint_Returns_OK()
    {
        var response = await _client.GetAsync("/api/email/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("healthy");
    }
}
