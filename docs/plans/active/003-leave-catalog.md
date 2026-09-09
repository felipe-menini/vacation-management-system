# 003 - Leave Catalog

EP-03 implements the configurable catalogs for leave types and balance buckets. It defines what catalog records exist, not how policies consume or calculate them.

## Implemented entities

| Entity | Purpose |
| --- | --- |
| `LeaveType` | Stable user-facing catalog entry such as Vacation, Medical Leave, Study Leave, or Bereavement. |
| `BalanceBucket` | Stable balance concept such as Vacation Days or Medical Examination Days. |

## LeaveType model

Fields:

- `Id` internal `Guid` identifier.
- `Code` required, unique, normalized uppercase stable business code.
- `Name` required display name.
- `Description` optional text.
- `IsActive` supports deactivation without hard delete.
- `SortOrder` non-negative display ordering.
- `CreatedAtUtc` / `UpdatedAtUtc` technical UTC timestamps.

`Code` is intentionally not part of the normal update operation. Historical references should keep stable business identity even if names or active status change.

## BalanceBucket model

Fields:

- `Id` internal `Guid` identifier.
- `Code` required, unique, normalized uppercase stable business code.
- `Name` required display name.
- `Description` optional text.
- `Unit` controlled measurement unit. EP-03 supports `DAY` only.
- `IsActive` supports deactivation without hard delete.
- `CreatedAtUtc` / `UpdatedAtUtc` technical UTC timestamps.

Half-days are expected later as decimal day quantities such as `0.5`. EP-03 does not implement half-day policy, conversions, user balances, or ledgers.

## Database

New tables in the `licenses` schema:

- `licenses.leave_types`
- `licenses.balance_buckets`

Both tables have primary keys, unique code indexes, length constraints, UTC timestamp columns, and no foreign key between each other.

## API endpoints

Leave Types:

- `GET /api/leave-types`
- `GET /api/leave-types/{id}`
- `POST /api/leave-types`
- `PUT /api/leave-types/{id}`

Balance Buckets:

- `GET /api/balance-buckets`
- `GET /api/balance-buckets/{id}`
- `POST /api/balance-buckets`
- `PUT /api/balance-buckets/{id}`

No DELETE endpoints exist. Administrative lists return active and inactive records by default; `isActive` can be used as a simple filter.

## Authorization

New permissions:

- `leave.catalog.read`
- `leave.catalog.manage`

Development seed behavior:

- `EMPLOYEE`: `leave.catalog.read`
- `SUPERVISOR`: `leave.catalog.read`
- `MANAGER`: `leave.catalog.read`
- `HR`: `leave.catalog.read`, `leave.catalog.manage`
- `TECH_ADMIN`: no automatic leave catalog manage permission

Catalog resources are global resources. They do not belong to an `OrgUnit`, so EP-03 uses a permission-only global authorization check: a known active actor must have at least one active role assignment whose active role grants the required permission. This deliberately avoids fake organizational ownership and avoids introducing global scope assignments in this slice.

Current limitation: global permission semantics still depend on the existence of an active role assignment because EP-02 roles are assigned through `RoleScopeAssignment`. This may be revisited when role assignment administration matures.

All mutation endpoints are backend protected:

- Missing actor returns `401`.
- Known actor without required permission returns `403`.

Frontend visibility is only usability. Backend authorization remains the security control.

## Development seed

Development-only idempotent catalog examples:

Leave Types:

- `VACATION` - Vacation
- `MEDICAL` - Medical Leave
- `MEDICAL_EXAM` - Medical Examination
- `STUDY` - Study Leave
- `BEREAVEMENT` - Bereavement Leave

Balance Buckets:

- `VACATION_DAYS` - Vacation Days - `DAY`
- `MEDICAL_EXAM_DAYS` - Medical Examination Days - `DAY`

These records are examples for manual testing and are not finalized production HR configuration.

## Frontend behavior

The Development actor selector remains available. The leave catalog screen displays Leave Types and Balance Buckets and provides simple create/toggle-active operations. Read-only actors can view catalog data; mutation attempts are still denied by the backend unless the actor has `leave.catalog.manage`.

## Explicit non-goals

EP-03 does not implement:

- `LeaveType` to `BalanceBucket` linkage.
- Employee balances.
- Balance ledger or transactions.
- Entitlements, carry-over, expiry, or negative balance rules.
- Leave requests, approvals, cancellations, revocations, overlaps, holidays, half-day validation, notice rules, or policy versioning.
- Microsoft Entra, Microsoft Graph, or SharePoint integration.

## Architectural decision

`LeaveType` is NOT directly linked to `BalanceBucket` in EP-03.

The future policy/versioning model will determine whether a leave type consumes balance and which bucket applies. For example, Vacation may later consume the Vacation Days bucket through an effective `LeavePolicyVersion`, but that relationship is deliberately outside this catalog slice.
