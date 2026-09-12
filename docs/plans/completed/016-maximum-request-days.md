# EP-16 Maximum Request Days

## Status

Completed.

## Scope

EP-16 adds optional maximum-duration enforcement for individual leave requests and surfaces the resulting validation safely in the frontend.

## Semantics

- `LeavePolicyVersion.MaximumRequestDays` is an optional maximum duration for one leave request.
- `null` means no maximum is enforced and existing request behavior is preserved.
- When configured, the value must be greater than zero.
- Exact equality is allowed: `CalculatedDays == MaximumRequestDays` succeeds.
- Only `CalculatedDays > MaximumRequestDays` is rejected.

## Enforcement point

Maximum request duration is enforced by the backend during submission only.

Draft creation and draft editing may temporarily hold dates that exceed the maximum. The backend validates when a draft is submitted or when manual create-for-other performs its atomic create-and-submit workflow.

## Calculation source

Validation uses the same `CalculatedDays` produced by the existing leave day calculator and frozen onto the request after successful submission.

That means the rule follows existing behavior for:

- `CALENDAR_DAYS`
- `BUSINESS_DAYS`
- `WorkingCalendar`
- dated calendar exceptions
- `FULL_DAY`
- `HALF_DAY`

No independent raw date-span comparison is used. For `BUSINESS_DAYS`, weekends and holidays only affect the maximum through the WorkingCalendar-derived calculated duration. `HALF_DAY` follows the existing calculated quantity semantics.

## Side effects

When maximum-duration validation fails:

- the request remains `DRAFT`
- entered/request data is not lost
- `LeavePolicyVersionId` is not frozen
- `CalculatedDays` is not stored
- no balance reservation or ledger mutation occurs
- no successful `leave.request.submit` audit event is written
- no `LeaveRequestSubmitted` outbox message is created
- no status transition occurs

## Manual create-for-other

Manual create-for-other uses the same submission path and the same maximum-duration rule. There is no HR, manager, supervisor, or administrative bypass in EP-16.

## API behavior

Maximum-duration violations return a stable business-validation response with safe facts:

- `code`
- `maximumRequestDays`
- `calculatedDays`
- `dayCountMode`

Successful submit responses are unchanged.

## Frontend behavior

The frontend formats `MAXIMUM_REQUEST_DAYS_EXCEEDED` as a business-facing validation message using the safe API response data, for example:

> This leave type allows a maximum of 5 business days per request. Your request contains 7 calculated business days.

The UI does not expose exception class names, stack traces, database details, or other internal implementation information.

Self-service behavior:

- draft creation/editing remains allowed
- maximum duration is enforced only when Submit is pressed
- failed submission is shown as a validation error
- the draft remains visibly `DRAFT`
- existing MinimumNoticeDays, overlap, balance, and generic validation handling is preserved

Manual create-for-other behavior:

- maximum-duration validation is displayed through the same safe message formatter
- the operation is not presented as successful when validation fails
- form data is retained by the browser because the form is reset only after success
- no override or bypass button is provided

## Deferred

No policy preview endpoint, frontend WorkingCalendar duration calculator, request splitting workflow, medical-certificate rule, Entra work, SharePoint work, or bypass capability was added in EP-16.
