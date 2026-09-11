# EP-11: Notifications Outbox

EP-11 closes the outbound workflow notification foundation. It emits leave lifecycle events inside the same PostgreSQL transaction as the workflow change, persists them in `licenses.outbox_messages`, and processes them from `Licenses.Worker` through a provider-neutral notification sender abstraction.

## Scope

- Emit focused lifecycle events for leave request submission, approval, rejection, cancellation request, cancellation approval/rejection, and revocation.
- Persist outbox rows atomically with the leave workflow transaction.
- Process committed outbox rows from PostgreSQL using a background worker and retry/dead-letter state.
- Resolve recipients through existing backend authorization, permissions, and organizational scope rules.
- Use the Development logging sender for local delivery visibility.
- Keep real external delivery deferred until the customer supplies the required Microsoft tenant/configuration.

## Non-goals

EP-11 does not implement Microsoft Graph mail, SMTP, Azure Communication Services, SendGrid, Teams, push notifications, Entra authentication, a notification center, user preferences, localization, or a new notification provider.

## Lifecycle events

- `LeaveRequestSubmitted`
- `LeaveRequestApproved`
- `LeaveRequestRejected`
- `LeaveCancellationRequested`
- `LeaveCancellationApproved`
- `LeaveCancellationRejected`
- `LeaveRequestRevoked`

Payloads contain only simple identifiers and business facts needed for notification delivery, such as leave request id, subject user id, organizational unit id, actor/decision user id when relevant, and occurrence timestamp. They do not include EF entities, medical document contents, storage keys, private URLs, or attachment bytes.

## Transactional outbox

Workflow services enqueue lifecycle events through `IApplicationEventOutbox` before saving the workflow transaction. PostgreSQL commits the business state change and `licenses.outbox_messages` row together, so a rollback leaves no notification row and a committed workflow can be retried independently of request processing.

`EventType + CorrelationId` is unique and reuses existing workflow operation ids where applicable. This prevents duplicate outbox rows when idempotent commands are retried.

## Worker processing

`Licenses.Worker` registers and runs `OutboxWorker`. The worker resolves `OutboxMessageProcessor` per polling iteration and processes eligible rows from PostgreSQL.

Worker behavior is configured through `OutboxProcessing`:

- `PollInterval`
- `BatchSize`
- `MaxAttempts`
- `InitialRetryDelay`
- `MaxRetryDelay`
- `ProcessingLeaseDuration`

Base settings provide conservative defaults. Development overrides the polling interval for faster feedback.

## PostgreSQL polling and concurrency

The processor claims work directly in PostgreSQL with `UPDATE ... WHERE id IN (SELECT ... FOR UPDATE SKIP LOCKED) ... RETURNING id`. Claiming sets `processing_lease_expires_at_utc`, and the EF change tracker is cleared before message loading because raw SQL updates bypass tracked entity state.

`SKIP LOCKED` allows multiple worker instances to poll safely without processing the same outbox message concurrently. A processing lease prevents immediately reclaiming in-flight work and allows recovery after worker crashes once the lease expires.

## Retry, backoff, and dead letters

Failed delivery increments `attempts`, stores bounded `last_error`, clears the processing lease, and schedules `next_attempt_at_utc` using bounded exponential backoff.

When `attempts` reaches `MaxAttempts`, the message is marked with `dead_lettered_at_utc`, `next_attempt_at_utc` is cleared, and the worker no longer claims it. Dead-lettered messages are retained for operational inspection and are not continuously retried.

## Recipient resolution

Outbound notifications use the existing backend authorization model: role + organizational scope.

- Submitted requests notify active users with `leave.requests.decide` over the request's stored org unit scope.
- Cancellation requests notify active users with `leave.requests.cancel.decide` over the request's stored org unit scope.
- Approval, rejection, cancellation decision, and revocation events notify the active subject employee.
- Decision recipient resolution honors descendant scope assignments.
- The subject employee is excluded from decision-recipient lists.
- `TECH_ADMIN` receives no notification solely because of the technical role; it must have an in-scope leave decision permission like any other role.

## Delivery abstraction

Application owns provider-neutral notification contracts:

- `NotificationMessage`
- `INotificationSender`
- `INotificationDeliveryPipeline`

Infrastructure renders deterministic outbound workflow messages and sends them through `INotificationSender`. The domain and application layers do not depend on Microsoft Graph, SMTP, Azure, SharePoint, HTTP, or React.

## Development and non-Development behavior

Development uses `DevelopmentLoggingNotificationSender`, which writes a development-only log entry for the rendered notification.

Non-Development uses `UnconfiguredNotificationSender` until a real provider is configured. It fails clearly instead of silently marking notifications as delivered, so retry and dead-letter state expose the missing provider configuration.

External delivery is at-least-once. Future providers should use `OutboxMessage.Id`, `CorrelationId`, or the notification idempotency key when provider idempotency is available.

## Deferred real provider

A real Microsoft provider is explicitly deferred until the customer supplies the Microsoft tenant and required configuration. The future provider should implement the existing `INotificationSender` abstraction without introducing Microsoft dependencies into the domain or application layers.

## Validation

- Application and infrastructure tests cover lifecycle event enqueueing, transaction rollback, outbox uniqueness, successful processing, retry state, dead-letter behavior, recipient resolution, Development logging sender, and unconfigured non-Development sender behavior.
- Final validation for EP-11C should include backend build/tests, EF pending-model check, frontend lint/typecheck/build, and `docker compose config`.
