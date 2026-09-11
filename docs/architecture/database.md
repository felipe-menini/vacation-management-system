# Database Architecture

PostgreSQL is the future source of truth for operational data. SharePoint remains only a migration or historical source during transition.

## Persistence decisions

| Topic | Decision |
| --- | --- |
| Primary database | PostgreSQL. |
| ORM | Entity Framework Core, with explicit SQL for optimized queries when needed. |
| Transactions | Required for leave requests, approvals, and balance changes. |
| Timestamps | Technical timestamps are UTC. Business dates remain business dates. |
| Balance | Derived from immutable ledger transactions. |
| Policy history | Versioned with effective date ranges. |
| Migration traceability | Preserve SharePoint source identifiers through `MigrationMapping`. |

## Transaction boundaries

Operations that affect request state, approval state, and balance must run in a single database transaction: submit, reject, final approve, resolve cancellation, revoke, manual adjustment, and migration import.

## Concurrency

Use optimistic concurrency, PostgreSQL row-version strategies such as `xmin`, or an equivalent design to prevent duplicate approvals and double balance consumption.

Critical commands should be idempotent where appropriate. See [API](api.md).

## Ledger transaction types

| Type | Purpose |
| --- | --- |
| `GRANT` | Add entitlement, such as annual vacation allocation. |
| `RESERVE` | Hold balance for a pending request. |
| `RELEASE` | Release a reservation after rejection or pending cancellation. |
| `CONSUME` | Convert approved leave into definitive usage. |
| `REFUND` | Return balance after cancellation or revocation according to policy. |
| `ADJUSTMENT` | Manual credit/debit with mandatory reason. |
| `EXPIRE` | Expire balance with traceability. |

## Indexing priorities

Index expected query paths by user, organizational unit, leave state, leave date range, leave type, balance period, audit actor/entity/timestamp, and migration source identifiers.

## Related documents

- [Domain model](domain-model.md)
- [Workflows](../product/workflows.md)
- [Integrations](integrations.md)

## Implemented policy persistence

EP-04 adds `licenses.leave_policies` and `licenses.leave_policy_versions`. Policy effective dates use PostgreSQL `date` via `DateOnly`. Database constraints validate enum values, effective ranges, balance-bucket consistency, positive version numbers, non-negative notice, and positive maximum request days. PostgreSQL also protects non-overlapping published periods per policy with an exclusion constraint.

Future leave requests must persist the exact `leave_policy_versions.id` used during evaluation. They must not store only a leave type and date and re-resolve later, because policy publications are historical boundaries.

## Implemented working-calendar persistence

EP-05 adds `licenses.working_calendars`, `licenses.working_calendar_weekdays`, and `licenses.working_calendar_exceptions`. Business dates are persisted as PostgreSQL `date` via `DateOnly`; UTC timestamps are only used for technical audit-style creation/update fields.

Persistence guarantees include unique normalized calendar codes, one weekday row per calendar/day, unique exception date per calendar, controlled weekday values, and conservative foreign keys. `licenses.leave_policy_versions.working_calendar_id` is nullable, but a check constraint requires it when either policy day-count mode uses `BUSINESS_DAYS`.

## Implemented balance ledger persistence

EP-06 adds `licenses.balance_accounts` and `licenses.balance_ledger_entries`. `balance_accounts` enforces one account per `(user_id, balance_bucket_id)`. `balance_ledger_entries` enforces unique `operation_id`, controlled entry types, semantic delta checks, and a PostgreSQL trigger that rejects direct `UPDATE` and `DELETE`.

Balance mutation transactions cover account acquisition/creation, idempotency check, row-level account locking, balance derivation, invariant validation, and ledger insertion. There are no mutable current-balance columns.

## Implemented leave request persistence

EP-07 adds `licenses.leave_requests`. Business dates use PostgreSQL `date`; lifecycle timestamps use UTC. Constraints enforce controlled status/day portion values, `end_date >= start_date`, single-date half days, positive calculated days, draft/submitted consistency, and balance-link consistency. Foreign keys use conservative delete behavior to preserve request history.

## Implemented approval persistence

EP-08 adds `licenses.leave_request_decisions` and `leave_requests.decided_at_utc`. Decision rows are append-only business history with one final decision per request, unique command `operation_id`, controlled `APPROVE` / `REJECT` values, required rejection comments, conservative foreign keys, and an immutability trigger rejecting direct `UPDATE`/`DELETE`.

Approval/rejection runs in one PostgreSQL transaction with a row-level lock on `leave_requests`, decision insertion, final status update, and optional balance settlement ledger entry.
