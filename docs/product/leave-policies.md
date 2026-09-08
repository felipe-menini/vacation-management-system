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

- Holidays are stored in calendars assigned by site, country, or organizational unit.
- A policy chooses which calendar applies.
- Leave dates are stored as business dates.
- Action and audit timestamps are stored in UTC and displayed in the organization/user time zone.

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
