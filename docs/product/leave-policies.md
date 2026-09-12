# Leave Policies

Leave rules must be configurable and versioned. A request keeps the effective policy version used when it was created or approved, so later policy changes do not rewrite history.

## Configurable parameters by leave type

| Category | Parameters |
| --- | --- |
| Time calculation | Business days or calendar days; Saturdays; Sundays; holiday calendar; AM/PM half-days; future hours support. |
| Overlap | Prohibit, warn, or allow; states considered conflicts; role-based exceptions. |
| Balance | Whether it consumes balance; associated bucket; negative balance; negative limit; reserve-on-submit; consumption moment. |
| Notice | Minimum and maximum advance notice; retroactive requests; exceptions by type. |
| Duration | Minimum per request; maximum consecutive duration; annual maximum; allowed fractioning. |
| Approval | Whether approval is required; number of levels; supervisor, encargado, RRHH; sequential or parallel flow; future conditional auto-approval. |
| Cancellation | Allowed window; approval requirement; balance refund; future partial cancellation. |
| Attachments | Required/optional; threshold after N days; MIME types; maximum size; classification and visibility. |
| Team capacity | Maximum simultaneous absences by unit; minimum staff available; warn or block behavior. |
| Carry-over / validity | Accrual, expiration, maximum transferable amount, and grace period. |

## Configuration precedence

Recommended MVP precedence:

```text
Global policy -> Leave type policy -> Organizational unit override
```

Individual exceptions should be restricted to RRHH and explicitly recorded, preferably as balance movements or temporary exceptions with expiration dates.

## Calendars and holidays

- Working days are stored in configurable working calendars.
- A policy version chooses which working calendar applies when it uses `BUSINESS_DAYS`.
- Weekly working schedules are configurable per calendar; weekends are not hardcoded globally.
- Dated calendar exceptions override weekday rules.
- Leave dates are stored as business dates.
- Action and audit timestamps are stored in UTC and displayed in the organization/user time zone.
- No real-country holiday dataset is hardcoded in EP-05.
- `CALENDAR_DAYS` counts every date and does not exclude weekends or holidays.

## Balance ledger

The system does not store an editable “days remaining” value. It derives visible totals from immutable transactions.

| Transaction | Example | Effect |
| --- | --- | --- |
| `GRANT` | Annual allocation +20 | Increases available balance. |
| `RESERVE` | Pending request -5 | Reduces available and increases pending. |
| `RELEASE` | Rejection/cancellation of pending request +5 | Releases reservation. |
| `CONSUME` | Approval of 5 days | Converts reservation into definitive consumption. |
| `REFUND` | Cancellation/revocation +5 | Returns days according to policy. |
| `ADJUSTMENT` | RRHH correction +/-N | Manual adjustment with mandatory reason. |
| `EXPIRE` | Expiration -N | Reduces expired balance with traceability. |

## Open Decisions

- Does every pending request reserve balance? Recommendation from the proposal: yes for capped leave types.
- What happens when balance is insufficient: block, warn, or allow negative balance?
- Which leave types and buckets currently exist?
- Which leave types require one or more approval levels?
- Is temporary approver delegation required in MVP?

## Related documents

- [Domain model](../architecture/domain-model.md)
- [Database](../architecture/database.md)
- [Workflows](workflows.md)

## Implemented EP-04 policy/version slice

The implemented policy model now separates stable `LeavePolicy` records from effective-dated `LeavePolicyVersion` records. Company-wide policies use `OrgUnitId = null`; OrgUnit overrides use an explicit OrgUnit and may optionally apply to descendants. Company-wide policies persist `AppliesToDescendants = false` because descendant semantics only apply to overrides.

Published versions are immutable and resolver-visible. Draft versions can be edited but never resolve. Published periods for the same policy must not overlap. Policy resolution prefers the deepest applicable OrgUnit override and then falls back to the company-wide LeaveType policy. A future LeaveRequest must reference the exact published `LeavePolicyVersion` used during evaluation.

EP-04 stores day count mode, half-day allowance, notice days/mode, maximum request days, overlap behavior, and balance consumption bucket. Later EPs added working-calendar day calculation, leave requests, approvals, balance ledger integration, documents, audit, and minimum-notice enforcement. Carry-over, expiry automation, Entra, SharePoint, and capacity remain deferred.

## Implemented EP-05 working-calendar slice

EP-05 adds reusable day calculation for inclusive date ranges. `BUSINESS_DAYS` uses the selected `WorkingCalendar`; `CALENDAR_DAYS` does not require a calendar. Later EPs added LeaveRequest creation, submission, half-day validation, and minimum-notice enforcement; AM/PM partial-day semantics remain deferred.

## Implemented EP-07 request usage

Leave requests resolve the applicable published policy version at submission using the request `StartDate`. That exact version is frozen for the entire request. `MinimumNoticeDays` is enforced at submission using the configured business timezone, business-local `DateOnly` today, the policy notice day-count mode, and `WorkingCalendar` for `BUSINESS_DAYS`. Drafts may temporarily violate notice rules.