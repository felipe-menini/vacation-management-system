# EP-14 Policy Enforcement

## Status

Completed.

## EP-14A Business time and minimum notice foundation

- Business time zone is explicit application configuration: `BusinessTime:TimeZoneId`.
- Development configuration uses `America/Montevideo`, but application/domain logic does not hardcode Uruguay.
- Persisted lifecycle timestamps remain UTC. Business leave rules derive `DateOnly` values from UTC clock time plus the configured business time zone.
- `IClock.UtcNow` is the only clock input for notice calculation. `BusinessDateProvider` converts it to business-local `Today`.
- Minimum-notice calculation is fact-only. It does not accept or reject a leave request by itself.
- Notice days are eligible days strictly before `StartDate`, starting at business-local today. `StartDate` itself is excluded.
- `CALENDAR_DAYS` notice is the date difference between business-local today and `StartDate`.
- `BUSINESS_DAYS` notice reuses `WorkingCalendar.IsWorkingDay(DateOnly)`, honoring weekday rules and dated exceptions.
- `BUSINESS_DAYS` notice requires a valid `WorkingCalendarId`; it never falls back to calendar days.

## EP-14B Minimum notice enforcement during submission

- `LeavePolicyVersion.MinimumNoticeDays` is enforced only when a leave request is submitted into the approval workflow.
- Draft create/update may temporarily hold dates that fail notice rules.
- Minimum notice is a mandatory hard validation. There is no warning-only mode.
- Submission resolves the policy version for the request `StartDate` through the existing policy resolver, then uses that same resolved version for minimum-notice validation and frozen-policy behavior.
- Calendar/business-day notice semantics come from `IMinimumNoticeCalculator`: business-local today, `NoticeDayCountMode`, and `WorkingCalendar` for `BUSINESS_DAYS`.
- A failed minimum-notice validation occurs before submission side effects: no transition to `PENDING_APPROVAL`, no frozen `PolicyVersionId`, no balance reservation/ledger mutation, no `LeaveRequestSubmitted` outbox message, no successful submit audit, and no approval workflow history.
- Manual create-for-other follows the same normal submission semantics and does not bypass minimum notice.
- There is no administrative, HR, manager, or retroactive bypass in EP-14.

## EP-14C Frontend UX and closure

- Self-service draft creation and draft editing remain allowed without client-side minimum-notice blocking.
- Minimum notice is surfaced only when the backend rejects `Submit`.
- Rejected self-service submission remains visibly `DRAFT`; entered/saved request state is preserved because the frontend does not clear or replace the draft on submit failure.
- Manual create-for-other surfaces the same minimum-notice validation and is not presented as successful when the backend rejects it.
- The frontend formats the safe business-validation fields returned by the API: required minimum notice days, calculated notice days, notice day count mode, and business-local current date when available.
- The frontend does not show stack traces, exception class names, configuration internals, or a bypass control.
- No separate React notice calculator was added; backend validation remains authoritative.
- No client-side policy preview/hint was added because the required resolved policy/notice calculation data is not already available without extra API work.

## Explicit deferrals

- Minimum-notice bypass permission.
- Retroactive leave requests.
- Warning-only notice policy.
- Per-user timezone.
- Per-org timezone.
- Holiday import.
- Uruguay holiday hardcoding.
- Entra integration.
- SharePoint integration.
