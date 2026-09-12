# 015 - Leave Request Completion

EP-15 completes the approved-leave lifecycle with automatic, backend-owned completion and frontend rendering of the persisted terminal state.

## Implemented lifecycle

Approved leave requests can move through the system lifecycle transition:

```text
APPROVED -> COMPLETED
```

Completion is eligible only when the configured business-local date is after the request end date:

```text
BusinessToday > LeaveRequest.EndDate
```

If `BusinessToday == EndDate`, the request remains `APPROVED` because the leave may still be taking place during that business date.

Only `APPROVED` requests are eligible. The system must not auto-complete `DRAFT`, `PENDING_APPROVAL`, `REJECTED`, `CANCELLATION_REQUESTED`, `CANCELLED`, `REVOKED`, or already `COMPLETED` requests.

`CANCELLATION_REQUESTED` is intentionally excluded. A cancellation awaiting decision remains in that lifecycle and is not silently resolved by completion.

Completion records when the system performed the lifecycle transition through `CompletedAtUtc`. Business-local date determines eligibility; UTC time records the actual processing timestamp.

## Preserved business facts

Completion is lifecycle-only:

- no balance ledger mutation
- no `RESERVE`, `RELEASE`, `CONSUME`, `REFUND`, `ADJUSTMENT`, or `EXPIRE`
- no policy re-resolution
- no `PolicyVersionId` change
- no `CalculatedDays` recalculation
- no request date mutation
- no completion notification
- no completion outbox event

Approval remains the workflow stage that settles consuming leave. Completion does not refund balance, consume balance again, or alter frozen policy/calculated data.

## Application and persistence design

EP-15 uses a focused system operation for bounded completion:

- query candidates with `Status = APPROVED AND EndDate < BusinessToday`
- order deterministically by `EndDate`, then `Id`
- apply a bounded batch size
- lock and re-check each request before transitioning it

The row lock and status re-check protect the cancellation race where a worker identifies an approved request, another actor requests cancellation, and the worker later attempts completion. If the request is no longer `APPROVED`, completion skips it cleanly.

## Automatic Worker processing

Completion runs automatically from the existing `Licenses.Worker` host. The Worker performs a completion pass shortly after startup, then waits for the configured polling interval. This gives startup catch-up behavior after downtime: old `APPROVED` requests whose `EndDate` is before the current business-local date are completed without a separate backfill tool.

Processing is configured under `LeaveCompletion`:

- `Enabled`
- `PollInterval`
- `BatchSize`

`BatchSize` is validated and bounded by the application completion service maximum. The default polling interval is intentionally coarse because completion is date-based, not latency-sensitive. Development can override the interval and batch size through Worker configuration.

Each polling iteration reuses the bounded eligible-request query and processes batches until the current backlog is exhausted. It never loads all eligible requests into memory. If a full batch produces no completions because candidates were skipped after lock/re-check, the processor stops that iteration to avoid a pathological tight loop.

Multi-worker safety relies on row lock and status re-check. Multiple Worker instances can discover the same candidate; only one can commit `APPROVED -> COMPLETED`, and the other skips after observing the request is no longer eligible. No distributed lock is introduced.

## System audit

Completion is system-generated. It does not invent a human user or use Technical Admin as actor. Successful automatic completion writes one `leave.request.complete` audit event with:

- `ActorUserId = null`
- `SubjectUserId = LeaveRequest.UserId`
- `ResourceType = LeaveRequest`
- `ResourceId = LeaveRequest.Id`
- `OrgUnitId = LeaveRequest.OrgUnitId`
- minimal metadata: previous status, resulting status, end date, completed timestamp

The lifecycle transition and audit event commit atomically in the same transaction. If audit persistence fails, completion rolls back with it.

The audit metadata intentionally does not include medical/document information and does not serialize the full request.

## API and frontend behavior

Leave request read models expose the persisted status and completion timestamp:

- `Status = COMPLETED`
- `CompletedAtUtc` when the transition has been processed

The frontend renders the stored `COMPLETED` status as a first-class request state in request lists and scoped inspection. Completed requests show the leave date range, calculated days, leave type, and completed timestamp when present.

The frontend does not calculate completion from the browser clock. It does not run timers and does not infer `COMPLETED` from `EndDate`. The backend/Worker persisted status is authoritative.

For `COMPLETED` requests, the UI does not show invalid lifecycle actions:

- Submit
- Approve
- Reject
- Request cancellation
- Decide cancellation
- Revoke
- Complete

There is no manual completion endpoint and no manual Complete button.

## Explicit deferrals

EP-15 intentionally does not implement:

- completion notification/email
- completion outbox event
- manual Complete endpoint
- manual Complete button
- historical backfill utility
- balance changes
- Entra integration
- SharePoint integration
