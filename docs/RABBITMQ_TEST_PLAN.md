# EximEdge.Notification — RabbitMQ Integration Test Plan

> **Based on actual codebase analysis** — MassTransit 8.4, .NET 10, Modular Monolith

---

## Architecture‑Aware Context

| Component | Technology | Key fact |
|---|---|---|
| Broker abstraction | MassTransit 8.4 (not raw RabbitMQ.Client) | Publisher confirms, DLQ, retry are **MassTransit‑managed** |
| Publisher | `IEventBus` → `MassTransitEventBus` → `IPublishEndpoint` | Scoped; no raw channel management |
| Consumer | `SendEmailConsumer` + `SendEmailConsumerDefinition` | Endpoint: `email-send`, concurrency: 16 |
| Retry policy | Exponential 3×(1 s→30 s, step 5 s) | After exhaustion → `email-send_error` |
| DLQ | MassTransit convention: `<endpoint>_error` | Automatic — `email-send_error` |
| Exchange type | MassTransit default: **fanout** per message type | `EximEdge.Notification.Contracts.Email:SendEmailMessage` |
| Serialization | System.Text.Json (MassTransit default in 8.x) | |
| Config section | `RabbitMQ:Enabled`, `RabbitMQ:Connection:*` | Shared by ApiHost + WorkerHost |

### ⚠️ Critical Gap Found During Analysis

**`MapEmailEndpoints()` was never called in `Program.cs`.** The `/api/email/send` endpoint
was unreachable. **Fixed** as part of this test plan delivery.

---

## 1️⃣ Infrastructure Validation

### Docker Commands

```bash
# Start the testing stack
docker compose -f docker-compose.testing.yml up -d

# Verify RabbitMQ is healthy
docker inspect eximedge-rabbitmq-test --format='{{.State.Health.Status}}'
# Expected: healthy

# Management UI
curl -s http://localhost:15672/api/overview -u admin:password123 | jq .rabbitmq_version
# Expected: "3.12.x"

# List exchanges (after app starts)
docker exec eximedge-rabbitmq-test rabbitmqctl list_exchanges
# Look for: EximEdge.Notification.Contracts.Email:SendEmailMessage

# List queues
docker exec eximedge-rabbitmq-test rabbitmqctl list_queues name durable auto_delete messages
# Expected rows:
#   email-send           true   false   0
#   email-send_error     true   false   0
#   email-send_skipped   true   false   0

# List bindings
docker exec eximedge-rabbitmq-test rabbitmqctl list_bindings source_name destination_name routing_key
```

### Manual Checklist

| # | Check | Command / Action | Expected |
|---|---|---|---|
| 1.1 | Container running | `docker ps --filter name=eximedge-rabbitmq-test` | Status: Up, healthy |
| 1.2 | Management UI | Browser → `http://localhost:15672` | Login with admin/password123 |
| 1.3 | AMQP port | `Test-NetConnection localhost -Port 5672` | TcpTestSucceeded: True |
| 1.4 | Exchange created | Management UI → Exchanges tab | `EximEdge.Notification.Contracts.Email:SendEmailMessage` (fanout, durable) |
| 1.5 | Queue created | Management UI → Queues tab | `email-send` (durable, autoDelete=false) |
| 1.6 | Error queue | Management UI → Queues tab | `email-send_error` exists |
| 1.7 | Binding correct | Management UI → `email-send` queue → Bindings | Bound to the SendEmailMessage exchange |

---

## 2️⃣ Publisher Validation (ApiHost)

### Postman / curl Tests

```bash
# Basic publish
curl -X POST http://localhost:5000/api/email/send \
  -H "Content-Type: application/json" \
  -d '{"to":"test@example.com","subject":"Test","body":"Hello World"}'
# Expected: 200 OK, body contains "Job ID: <guid>"

# Publish 10 rapid messages
for i in $(seq 1 10); do
  curl -s -X POST http://localhost:5000/api/email/send \
    -H "Content-Type: application/json" \
    -d "{\"to\":\"batch${i}@test.com\",\"subject\":\"Batch ${i}\",\"body\":\"Message ${i}\"}" &
done
wait
echo "All sent"

# Check queue depth
docker exec eximedge-rabbitmq-test rabbitmqctl list_queues name messages
```

### Postman Collection (import as JSON)

```json
{
  "info": {
    "name": "EximEdge RabbitMQ Tests",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "Health Check",
      "request": {
        "method": "GET",
        "url": "{{baseUrl}}/health"
      },
      "event": [{
        "listen": "test",
        "script": {
          "exec": [
            "pm.test('Health is OK', function () {",
            "  pm.response.to.have.status(200);",
            "});"
          ]
        }
      }]
    },
    {
      "name": "Email Module Health",
      "request": {
        "method": "GET",
        "url": "{{baseUrl}}/api/email/health"
      },
      "event": [{
        "listen": "test",
        "script": {
          "exec": [
            "pm.test('Email module healthy', function () {",
            "  pm.response.to.have.status(200);",
            "  pm.expect(pm.response.text()).to.include('healthy');",
            "});"
          ]
        }
      }]
    },
    {
      "name": "Send Email",
      "request": {
        "method": "POST",
        "url": "{{baseUrl}}/api/email/send",
        "header": [{"key":"Content-Type","value":"application/json"}],
        "body": {
          "mode": "raw",
          "raw": "{\"to\":\"test@example.com\",\"subject\":\"Postman Test\",\"body\":\"Hello from Postman\"}"
        }
      },
      "event": [{
        "listen": "test",
        "script": {
          "exec": [
            "pm.test('Returns 200 with Job ID', function () {",
            "  pm.response.to.have.status(200);",
            "  pm.expect(pm.response.text()).to.include('Job ID');",
            "});"
          ]
        }
      }]
    }
  ],
  "variable": [
    {"key": "baseUrl", "value": "http://localhost:5000"}
  ]
}
```

### What MassTransit Provides Automatically

| Concern | MassTransit behaviour | Your code |
|---|---|---|
| Publisher confirms | ✅ Enabled by default in MassTransit.RabbitMQ | No action needed |
| Message persistence | ✅ `DeliveryMode = Persistent` by default | No action needed |
| CorrelationId | ✅ Auto‑generated `ConversationId` + `MessageId` | Inspect via Management UI → Get Message |
| Serialization | ✅ System.Text.Json, `Content-Type: application/vnd.masstransit+json` | No action needed |

---

## 3️⃣ Consumer Validation (WorkerHost)

### What's Configured

```
Consumer:         SendEmailConsumer
Endpoint:         email-send
ConcurrentLimit:  16
Retry:            Exponential(3, 1s → 30s, step 5s)
DLQ:              email-send_error (MassTransit automatic)
ACK mode:         Manual (MassTransit default — ACKs after Consume() completes)
Prefetch:         MassTransit auto‑calculates from ConcurrentMessageLimit
```

### Manual Test Steps

| # | Scenario | Steps | Expected |
|---|---|---|---|
| 3.1 | Happy path | POST `/api/email/send` → check WorkerHost logs | Log: `Consumed email message {JobId}…` |
| 3.2 | Message ACKed | Management UI → `email-send` → Messages: 0 | Queue is empty after processing |
| 3.3 | Consumer logs | `dotnet run --project src/Host/EximEdge.Notification.WorkerHost` | Structured log with JobId, To, Subject |

---

## 4️⃣ Dead Letter Queue (DLQ)

### How It Works in This Project

MassTransit automatically moves faulted messages (after retry exhaustion) to `email-send_error`.
The error queue message includes:

- Original payload
- `MT-Fault-Message` header with exception details
- `MT-Fault-Timestamp`
- `MT-Reason` = `"Fault"`
- Full exception stack trace in the fault envelope

### Test: Force a DLQ Message

To test DLQ, temporarily make the consumer throw:

```csharp
// In SendEmailConsumer.Consume() — TEMPORARY for testing
throw new InvalidOperationException("Simulated failure for DLQ test");
```

Then:
1. POST `/api/email/send` with any payload
2. Wait ~40 seconds (3 retries with exponential backoff)
3. Check `email-send_error` queue in Management UI
4. Click "Get Message" — inspect `MT-Fault-*` headers

### rabbitmqctl Verification

```bash
# After forcing failure
docker exec eximedge-rabbitmq-test rabbitmqctl list_queues name messages
# Expected:
#   email-send         0
#   email-send_error   1

# Inspect the error message (Management UI API)
curl -s -u admin:password123 \
  http://localhost:15672/api/queues/%2F/email-send_error/get \
  -H "Content-Type: application/json" \
  -d '{"count":1,"ackmode":"ack_requeue_true","encoding":"auto"}' | jq '.[0].properties.headers'
```

---

## 5️⃣ Concurrency & Throughput

### Load Test Script (PowerShell)

```powershell
$baseUrl = "http://localhost:5000/api/email/send"
$body = '{"to":"load@test.com","subject":"Load Test","body":"Message"}'
$count = 1000

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$tasks = 1..$count | ForEach-Object {
    Invoke-RestMethod -Uri $baseUrl -Method POST -ContentType "application/json" -Body $body -ErrorAction SilentlyContinue
}
$stopwatch.Stop()

Write-Host "Sent $count messages in $($stopwatch.Elapsed.TotalSeconds) seconds"
Write-Host "Throughput: $([math]::Round($count / $stopwatch.Elapsed.TotalSeconds, 1)) msg/s"
```

### Monitoring During Load Test

```bash
# Live queue depth
watch -n 1 'docker exec eximedge-rabbitmq-test rabbitmqctl list_queues name messages'

# Consumer process metrics
docker stats eximedge-rabbitmq-test

# RabbitMQ message rates
curl -s -u admin:password123 http://localhost:15672/api/queues/%2F/email-send | \
  jq '{messages: .messages, consumers: .consumers, message_stats: .message_stats}'
```

### Expected Results

| Metric | Acceptable Range |
|---|---|
| Publish throughput | > 500 msg/s (single API instance) |
| Consumer prefetch | Auto (~16 based on ConcurrentMessageLimit) |
| email-send depth during load | Temporarily spikes, drains to 0 |
| email-send_error | 0 (no failures in happy path) |
| Memory (RabbitMQ) | < 200 MB for 1000 messages |

---

## 6️⃣ Observability

### Structured Logging Verification

**WorkerHost** logs this via `LoggerMessage` source generator:

```
info: Email.Infrastructure.Consumers.SendEmailConsumer
      Consumed email message 3f2a... for recipient 'test@example.com', subject 'Test'.
```

### Correlation Tracing

MassTransit automatically propagates these headers on every message:

| Header | Source | Purpose |
|---|---|---|
| `MT-Activity-Id` | .NET Activity / OpenTelemetry | Distributed trace propagation |
| `MessageId` | MassTransit auto | Unique per message |
| `ConversationId` | MassTransit auto | Correlates related messages |
| `CorrelationId` | Optional (set via `Publish(msg, ctx => ctx.CorrelationId = ...)`) | Business correlation |

### Health Check

```bash
curl http://localhost:5000/health
# Expected: Healthy (includes Redis check when enabled)

curl http://localhost:5000/api/email/health
# Expected: Email API is healthy.
```

---

## 7️⃣ Failure Scenarios

### 7.1 RabbitMQ Down at Startup

```bash
docker stop eximedge-rabbitmq-test
dotnet run --project src/Host/EximEdge.Notification.ApiHost
# Expected: MassTransit retries connection in background.
#           API starts but publish calls will fail until RabbitMQ is back.

docker start eximedge-rabbitmq-test
# Expected: MassTransit reconnects automatically. Publishes resume.
```

### 7.2 RabbitMQ Down During Publish

```bash
# 1. Start everything normally
# 2. POST a message (should succeed)
# 3. Stop RabbitMQ:
docker stop eximedge-rabbitmq-test
# 4. POST another message:
curl -X POST http://localhost:5000/api/email/send \
  -H "Content-Type: application/json" \
  -d '{"to":"x@x.com","subject":"Fail","body":"x"}'
# Expected: 500 Internal Server Error with exception message

# 5. Restart RabbitMQ:
docker start eximedge-rabbitmq-test
# 6. POST again → should succeed (MassTransit auto-reconnects)
```

### 7.3 Consumer Crash Mid‑Processing

```bash
# While WorkerHost is processing messages, kill it:
# Ctrl+C or kill the process
# Messages currently being processed are NACKed and requeued by RabbitMQ.
# Restart WorkerHost → messages are re-delivered.
```

### 7.4 Poison Message

A message that always fails processing. After 3 retries it moves to `email-send_error`.
Verify by temporarily throwing in the consumer (see section 4).

### 7.5 In‑Memory Fallback (RabbitMQ Disabled)

```json
{ "RabbitMQ": { "Enabled": false } }
```

MassTransit switches to in-memory transport. Useful for local dev without Docker.
Publish and consume happen in-process, no broker required.

---

## 8️⃣ Automated Integration Test

### Running the Tests

```bash
# Ensure Docker is running (Testcontainers uses it)
dotnet test tests/EximEdge.Notification.IntegrationTests \
  --logger "console;verbosity=detailed"
```

### Test Matrix

| Test Class | Test | Validates |
|---|---|---|
| `InfrastructureTests` | `RabbitMq_Container_Is_Running` | Docker + Testcontainers |
| `InfrastructureTests` | `Management_UI_Is_Accessible` | Management plugin |
| `InfrastructureTests` | `RabbitMqctl_Lists_Queues_After_WorkerHost_Connects` | rabbitmqctl works |
| `PublisherTests` | `POST_Send_Returns_OK_And_JobId` | API → MediatR → EventBus |
| `PublisherTests` | `Published_Message_Is_Observed_By_TestHarness` | MassTransit publish pipeline |
| `PublisherTests` | `Exchange_Is_Created_After_Publish` | Topology auto-declaration |
| `ConsumerTests` | `Consumer_Processes_Published_Message` | Full publish → consume |
| `ConsumerTests` | `ConsumerDefinition_Has_Correct_Endpoint_Name` | Endpoint = `email-send` |
| `ConsumerTests` | `ConsumerDefinition_Has_Concurrency_Limit` | ConcurrentMessageLimit = 16 |
| `HealthCheckTests` | `Health_Endpoint_Returns_Healthy` | /health |
| `HealthCheckTests` | `Email_Module_Health_Endpoint_Returns_OK` | /api/email/health |

---

## Troubleshooting Checklist

| Symptom | Likely Cause | Fix |
|---|---|---|
| `Connection refused` on 5672 | RabbitMQ not running | `docker compose up -d` |
| Exchange not created | App didn't start / config `Enabled: false` | Check config + app startup logs |
| Messages stuck in queue | WorkerHost not running | Start WorkerHost |
| Messages in `email-send_error` | Consumer throwing exceptions | Check WorkerHost logs for stack trace |
| `email-send_error` filling up | Persistent bug in consumer logic | Fix consumer, then purge error queue |
| No consumers visible | WorkerHost didn't register consumer | Verify `AddConsumer<SendEmailConsumer>` in Program.cs |
| Duplicate messages | Consumer crashed before ACK | Expected with at-least-once; make consumer idempotent |
| Serialization error | Contract mismatch between publisher/consumer | Ensure both reference same `SendEmailMessage` from Contracts |
| `/api/email/send` returns 404 | `MapEmailEndpoints()` not called | Fixed in this PR — verify in Program.cs |
| `RabbitMQ:Connection:HostName` mismatch | ApiHost says `rabbitmq-dev`, WorkerHost says `localhost` | Align appsettings or use env vars |

---

## Production Readiness Checklist

| # | Category | Item | Status |
|---|---|---|---|
| 1 | **Config** | RabbitMQ credentials in secrets/vault (not appsettings) | ⬜ TODO |
| 2 | **Config** | `UseSsl: true` for production broker | ⬜ TODO |
| 3 | **Config** | Consistent HostName across ApiHost + WorkerHost | ⚠️ Currently mismatched! |
| 4 | **Reliability** | Publisher exception handling in endpoint (currently catches generic Exception) | ⚠️ Improve |
| 5 | **Reliability** | Outbox pattern for guaranteed publish with DB transaction | ⬜ TODO (MassTransit.EntityFrameworkCore available) |
| 6 | **Reliability** | Idempotent consumer (dedup by JobId) | ⬜ TODO |
| 7 | **Consumer** | Actual email delivery implemented (currently `Task.CompletedTask`) | ⬜ TODO |
| 8 | **Observability** | OpenTelemetry exporter configured | ⬜ TODO |
| 9 | **Observability** | RabbitMQ health check endpoint (`/health/messaging`) | ⬜ TODO |
| 10 | **Observability** | Structured logging includes CorrelationId in consumer | ⚠️ Log JobId only |
| 11 | **DLQ** | Error queue monitoring / alerting | ⬜ TODO |
| 12 | **DLQ** | Dead letter reprocessing strategy | ⬜ TODO |
| 13 | **Security** | API input validation beyond FluentValidation | ⬜ Review |
| 14 | **Security** | Rate limiting on `/api/email/send` | ⬜ TODO |
| 15 | **Deployment** | Docker health checks for all services | ✅ In docker-compose.testing.yml |
| 16 | **Deployment** | Kubernetes readiness/liveness probes | ⬜ TODO |
