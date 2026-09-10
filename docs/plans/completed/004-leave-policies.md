# 004 - Leave Policy Configuration and Versioning

EP-04 implements configurable, effective-dated, versioned leave policies. It stores policy rules and resolves which published version applies, but it does not implement leave requests, approval flows, day calculations, holidays, balance ledger operations, overlaps, attachments, or entitlement/carry-over rules.

## Model

`LeavePolicy` is the stable policy definition for exactly one `LeaveType` and one scope identity:

- `OrgUnitId = null`: company-wide policy for that leave type.
- `OrgUnitId != null`: organizational override for that leave type.
- `AppliesToDescendants`: only meaningful for organizational overrides. Company-wide policies are persisted with `false` as the canonical value.
- `IsActive`: inactive policy definitions are ignored by resolution and cannot publish new versions.

`LeavePolicyVersion` stores the historical rule set:

- positive sequential `VersionNumber` within a policy,
- `DRAFT` or `PUBLISHED` status,
- `DateOnly` effective business dates,
- immutable once published,
- nullable `EffectiveTo`, with `EffectiveTo >= EffectiveFrom` when present.

A future `LeaveRequest` must store/reference the exact `LeavePolicyVersion` used when the request is evaluated. It must not re-resolve silently after later policy versions are published.

## Implemented rule fields

EP-04 stores only these configurable fields:

- `DayCountMode`: `BUSINESS_DAYS` or `CALENDAR_DAYS`.
- `AllowHalfDay`.
- `MinimumNoticeDays`: nullable non-negative integer.
- `NoticeDayCountMode`: `BUSINESS_DAYS` or `CALENDAR_DAYS`.
- `MaximumRequestDays`: nullable decimal, greater than zero when present.
- `OverlapBehavior`: `BLOCK`, `WARN`, or `ALLOW`.
- `ConsumesBalance` and optional `BalanceBucketId`.

Balance consistency rules:

- `ConsumesBalance = true` requires `BalanceBucketId`.
- `ConsumesBalance = false` requires `BalanceBucketId = null`.
- publishing a consuming version requires an active `BalanceBucket`.
- historical published versions may keep referencing a bucket that is later deactivated.

The LeaveType-to-BalanceBucket relationship belongs to `LeavePolicyVersion`, not to `LeaveType`.

## Lifecycle

Draft versions may be edited. Publishing is explicit. Published versions cannot be mutated by ordinary update operations. Published effective periods for the same policy cannot overlap; this is checked in application logic and protected in PostgreSQL with an exclusion constraint for `PUBLISHED` rows.

Inactive `LeaveType` and inactive `BalanceBucket` records block new publication, but do not invalidate already published historical versions.

## Resolution

`ResolvePolicy(leaveTypeId, orgUnitId, date)` returns a clear no-policy result when no published applicable version exists.

Resolution behavior:

1. consider only active policies,
2. consider only `PUBLISHED` versions effective on the requested business date,
3. include company-wide policy for the leave type,
4. include matching exact OrgUnit overrides,
5. include ancestor overrides only when `AppliesToDescendants = true`,
6. choose the deepest/more-specific override,
7. fall back to the company-wide policy only if no override applies.

Draft, expired, future, and inactive-policy versions are ignored.

## Authorization

New permissions:

- `leave.policies.read`
- `leave.policies.manage`

Development seed grants read to Employee, Supervisor, Manager, and HR. HR receives manage. Technical Administrator does not receive policy manage by default because policy administration is an HR/business capability.

## API

Implemented endpoints:

- `GET /api/leave-policies`
- `GET /api/leave-policies/{id}`
- `POST /api/leave-policies`
- `PUT /api/leave-policies/{id}`
- `GET /api/leave-policies/{id}/versions`
- `POST /api/leave-policies/{id}/versions`
- `GET /api/leave-policy-versions/{id}`
- `PUT /api/leave-policy-versions/{id}`
- `POST /api/leave-policy-versions/{id}/publish`
- `GET /api/leave-policies/resolve?leaveTypeId=&orgUnitId=&date=`

GET endpoints require `leave.policies.read`; mutation and publish endpoints require `leave.policies.manage`. There are no DELETE endpoints.

## Database

New tables under the `licenses` schema:

- `leave_policies`
- `leave_policy_versions`

Foreign keys point to `leave_types`, `org_units`, `leave_policies`, and `balance_buckets`. There is no `LeaveType -> BalanceBucket` foreign key.

## Development seed

Development-only examples are idempotent and illustrative, not final production HR configuration:

- company-wide VACATION policy version 1, effective 2026-01-01, business-day mode, half-days enabled, 7 calendar-day notice, max 15 days, BLOCK overlaps, consumes `VACATION_DAYS`.
- company-wide MEDICAL policy version 1, effective 2026-01-01, calendar-day mode, no half-days, 0 calendar-day notice, ALLOW overlaps, no balance consumption.
- IT VACATION override version 1, effective 2026-01-01, descendant enabled, max 10 days, WARN overlaps, consumes `VACATION_DAYS`.

## Frontend

The development/admin UI can list policies and versions, create policies, create draft versions, edit drafts, publish drafts, and run a simple policy resolution inspector. Backend authorization remains the security control; UI visibility is convenience only, not a security boundary.

## Explicit non-goals

EP-04 does not implement leave requests, balance ledger entries, user balances, approvals, holidays, attachments, entitlement generation, carry-over, business-day calculation, or actual leave-request overlap detection. `OverlapBehavior` is stored as policy configuration for future request evaluation.
