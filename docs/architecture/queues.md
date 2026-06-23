# Worker Queue Contract (APP-9)

**Status:** Accepted · 2026-06-23
**Owners:** application-development-squad
**Consumers:** azure-infrastructure-squad (INF-4 KEDA scale rule)

## Decision

The Worker consumes from a single **Azure Service Bus queue** (not a topic, not Storage Queue).

| Property | Value |
|---|---|
| Queue tech | **Azure Service Bus — Queue** (Standard tier acceptable; Premium for prod) |
| Logical name | **`travel-assistant-worker-jobs`** |
| Connection alias (Aspire / config) | `worker-bus` |
| Message TTL | 1 hour (jobs are idempotent; retry handled by Worker) |
| Max delivery count | 5 (then → dead-letter) |
| Dead-letter queue | enabled (default `$DeadLetterQueue` subqueue) |
| Lock duration | 60s (Worker renews via processor) |
| Sessions | **disabled** (no ordering requirement) |
| Partitioning | disabled |
| Duplicate detection | enabled (10-minute window) on `MessageId` |

### Why Service Bus over Storage Queue
- Need dead-letter, duplicate detection, and FIFO-per-session option for future.
- KEDA `azure-servicebus` scaler is first-class and stable.
- Message size (chat planning payloads with provider results) can exceed Storage Queue's 64 KB limit; SB Standard allows 256 KB and Premium 100 MB.

### Why Queue over Topic
- Single consumer (Worker). No fan-out. A Topic would add a needless subscription hop.
- If we later add a second consumer (e.g., audit pipeline), we migrate to a Topic + two Subscriptions; queue name becomes subscription name.

## KEDA Scale Rule Contract

Azure-infrastructure wires the Container App with this trigger:

```yaml
scale:
  minReplicas: 0
  maxReplicas: 10
  rules:
    - name: sb-queue-depth
      custom:
        type: azure-servicebus
        metadata:
          queueName: travel-assistant-worker-jobs
          namespace: <from-bicep-output>
          messageCount: "20"          # target msgs-per-replica
        auth:
          - secretRef: sb-connection
            parameter: connection
```

**Scale trigger threshold:** **20 messages per replica.**
- Idle (queue empty) → scales to 0.
- 21 msgs → 2 replicas. 200 msgs → 10 replicas (cap).
- Tuned for ~5s per job at p95; revisit after first load test (QA-3).

## Configuration Keys

| Config path | Source | Value (dev) | Value (prod) |
|---|---|---|---|
| `ConnectionStrings:worker-bus` | App Configuration → Key Vault ref | local emulator conn str | SB namespace conn str (Managed Identity preferred when SDK supports it for sessions) |
| `Worker:QueueName` | App Configuration | `travel-assistant-worker-jobs` | same |
| `Worker:MaxConcurrentCalls` | App Configuration | `4` | `8` |
| `Worker:PrefetchCount` | App Configuration | `10` | `20` |

## Producer/Consumer Contract

**Producers** (Api today; possibly Agent in future):
- Publish via `ServiceBusSender` (Aspire `Aspire.Azure.Messaging.ServiceBus` integration).
- Set `MessageId = {chatThreadId}:{turnId}` to enable duplicate detection.
- Set `ApplicationProperties["jobType"]` for routing inside the Worker.

**Consumer** (Worker):
- `ServiceBusProcessor` with `MaxConcurrentCalls = Worker:MaxConcurrentCalls`.
- ACK on success, DLQ on poison after 5 deliveries.
- Emits OTel metric `worker.job.duration_ms` (histogram) and `worker.job.outcome` (counter, tags: `outcome={success|failure|deadlettered}`).

## Provisioning

Azure-infrastructure provisions:
- 1 Service Bus namespace per environment.
- 1 queue named exactly `travel-assistant-worker-jobs` with the properties in the table above.
- Outputs: namespace FQDN, queue name (echoed for verification), connection string secret stored in Key Vault.

## Changes to this contract

Renaming the queue or switching tech requires an ADR and a coordinated PR across app-dev (Worker config) + azure-infra (Bicep + KEDA rule). Do not rename in-place; introduce the new queue, dual-publish, drain old, retire.
