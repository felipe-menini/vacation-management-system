# 006 - Balance Accounts and Immutable Balance Ledger

EP-06 implements the accounting foundation for leave balances without LeaveRequest workflow.

## Scope

- Create one `BalanceAccount` per `User + BalanceBucket`.
- Store immutable `BalanceLedgerEntry` rows as the source of truth.
- Derive available and reserved balances from ledger deltas.
- Support semantic operations: `GRANT`, `RESERVE`, `RELEASE`, `CONSUME`, `REFUND`, `ADJUSTMENT`, and `EXPIRE`.
- Require `OperationId` for idempotent mutations.
- Serialize mutations per balance account using PostgreSQL row locks inside the transaction.
- Expose self/scoped read APIs and HR administrative `grant`, `adjust`, and `expire` APIs.
- Add simple frontend visibility and HR forms for direct administrative operations.

## Rules

- No mutable current or available balance column exists.
- Non-adjustment amounts must be positive; adjustment accepts signed non-zero amounts.
- Available and reserved balances may not become negative in EP-06.
- New `GRANT` and `RESERVE` operations require an active bucket.
- Existing historical accounts remain readable if a bucket later becomes inactive.
- Settlement/correction primitives remain possible for existing inactive-bucket accounts.
- Public APIs do not expose arbitrary `RESERVE`, `RELEASE`, `CONSUME`, or `REFUND` operations.

## Persistence

New migration: `20260910232756_BalanceLedger`.

Tables under `licenses`:

- `balance_accounts`
- `balance_ledger_entries`

Database guarantees include unique `(user_id, balance_bucket_id)`, unique `operation_id`, controlled ledger types, semantic delta checks, and a PostgreSQL trigger rejecting ledger row `UPDATE` and `DELETE`.

## Deferred intentionally

LeaveRequest, approval/cancellation workflows, automatic entitlement generation, accrual, carry-over, scheduled expiry, negative balance policy, forecasting, attachments, notifications, Entra, SharePoint, and payroll integration remain future work.
