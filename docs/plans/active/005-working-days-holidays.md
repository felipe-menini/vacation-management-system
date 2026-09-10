# 005 - Working Calendars, Holidays/Exceptions, and Day Calculation

EP-05 introduces configurable working calendars and deterministic day calculation primitives for future Leave Request and Leave Policy evaluation. It does not implement leave requests, approvals, balances, staffing rules, notice enforcement, or real public-holiday synchronization.

## Scope

- Store stable `WorkingCalendar` records with normalized immutable business codes.
- Configure one weekday rule per `DayOfWeek`; no global weekend rule is hardcoded.
- Store dated exceptions where `IsWorkingDay = false` represents a non-working exception and `IsWorkingDay = true` represents an exceptional working date.
- Calculate inclusive `CALENDAR_DAYS` and `BUSINESS_DAYS` ranges using `DateOnly`.
- Link `LeavePolicyVersion.WorkingCalendarId` when day count or notice mode uses `BUSINESS_DAYS`.
- Add backend-enforced calendar read/manage permissions.
- Add simple Development/admin UI for calendar administration and day calculation inspection.

## Rules

- Explicit dated exceptions override weekday rules.
- `CALENDAR_DAYS` counts every date in the inclusive range.
- `BUSINESS_DAYS` requires a working calendar and counts only dates resolved as working days.
- End date before start date is rejected.
- New published policy versions using `BUSINESS_DAYS` require an active working calendar.
- Historical published policy versions remain readable if their referenced calendar is later deactivated.

## Persistence

New EP-05 migration: `20260910173109_WorkingCalendars`.

Tables under `licenses`:

- `working_calendars`
- `working_calendar_weekdays`
- `working_calendar_exceptions`

`leave_policy_versions` now has nullable `working_calendar_id` with conservative foreign-key behavior.

## Development seed

Development-only seed creates `STANDARD_UY_DEV` with Monday-Friday working and Saturday-Sunday non-working, plus artificial sample exceptions. It is illustrative configuration only and is not a production Uruguay holiday dataset.

## Deferred intentionally

- LeaveRequest workflow and fields.
- Notice-day enforcement and inclusive/exclusive submission-date semantics.
- AM/PM or multi-day half-day request behavior.
- Real-country holiday import/synchronization.
- Balance ledger, approvals, attachments, Entra, SharePoint, and notifications.
