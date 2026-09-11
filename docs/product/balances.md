# Balances

Balances are accounting data. The immutable ledger is the source of truth; the application does not store mutable `CurrentBalance` or `AvailableBalance` columns.

## Model

A `BalanceAccount` groups one `User` and one `BalanceBucket`. There is at most one account for each user/bucket pair. Account creation is lazy and idempotent when the first valid balance operation needs it.

`Available` and `Reserved` are derived:

```text
Available = SUM(AvailableDelta)
Reserved  = SUM(ReservedDelta)
```

Amounts are decimal and support fractional days such as `0.5`.

## Ledger semantics

| Type | Available delta | Reserved delta | Use |
| --- | ---: | ---: | --- |
| `GRANT` | `+X` | `0` | Explicit entitlement/sample allocation. |
| `RESERVE` | `-X` | `+X` | Internal future LeaveRequest hold. |
| `RELEASE` | `+X` | `-X` | Internal release of a hold. |
| `CONSUME` | `0` | `-X` | Internal final use of reserved balance. |
| `REFUND` | `+X` | `0` | Internal future cancellation/revocation primitive. |
| `ADJUSTMENT` | signed non-zero | `0` | HR correction with reason. |
| `EXPIRE` | `-X` | `0` | Explicit HR expiry primitive. |

Until a future negative-balance policy exists, neither available nor reserved balances may become negative.

## Idempotency and concurrency

Every mutation has an `OperationId`. Repeating the same logical command returns the existing ledger result; reusing the same operation id with different payload is rejected. Mutations run in a database transaction and lock the `BalanceAccount` row before deriving balances and inserting the ledger entry.

## Visibility and operations

Employees can read their own balances. Supervisor/manager/HR reads are constrained by organizational scope. HR balance management is also scope-constrained. Technical administrators do not receive business balance permissions automatically.

Public administrative APIs expose only `GRANT`, `ADJUSTMENT`, and `EXPIRE`. Future LeaveRequest workflow will use `RESERVE`, `RELEASE`, `CONSUME`, and `REFUND`; EP-06 does not implement LeaveRequest.

Inactive buckets do not invalidate history. New grants/reserves require active buckets, while settlement/correction operations on existing accounts remain available to preserve accounting integrity.

## Implemented EP-07 request reservation

Submitted consuming leave requests reserve the frozen calculated quantity through the existing `RESERVE` ledger operation. The request stores the reservation operation id so retries and future approval/rejection settlement use the same accounting link. Future APPROVE will use `CONSUME`; future REJECT will use `RELEASE`.

## Implemented EP-08 approval settlement

Approval/rejection settles the EP-07 reservation using the frozen request data:

- `APPROVE` on a consuming request creates one idempotent `CONSUME CalculatedDays` settlement: available stays unchanged and reserved decreases.
- `REJECT` on a consuming request creates one idempotent `RELEASE CalculatedDays` settlement: available increases and reserved decreases.
- Non-consuming requests create no balance ledger settlement.

Approval does not create a second `RESERVE`, does not grant or adjust balance, and does not recalculate the quantity from current policy/calendar configuration.
