# 007 - Leave Requests and Submission

EP-07 implements the employee leave request aggregate and submission workflow.

## Scope

- Create and edit employee-owned DRAFT leave requests.
- Submit DRAFT requests to PENDING_APPROVAL only.
- Store the request OrgUnitId explicitly at creation time.
- Resolve the published LeavePolicyVersion by LeaveTypeId, OrgUnitId, and StartDate during submission.
- Freeze LeavePolicyVersionId and CalculatedDays on submission.
- Reuse the EP-05 DayCalculator for full-day calculations.
- Reserve balance atomically through the EP-06 balance ledger when the resolved policy consumes balance.
- Expose self request APIs and read-only scoped request inspection.

## Decisions

- Drafts do not freeze policies and have no balance ledger side effects.
- Submission resolves policy using StartDate; that exact published version is used for the entire request.
- Future workflows must use stored CalculatedDays for submitted requests rather than recalculating after calendar or policy changes.
- HALF_DAY is only supported for single-date requests. AM/PM and partial first/last day ranges remain deferred.
- MinimumNoticeDays remains intentionally deferred until company timezone and inclusive/exclusive notice semantics are decided.

## Deferred

Approval, rejection, cancellation, revocation, completion, medical attachments, notifications, minimum notice enforcement, AM/PM half days, multi-day partial days, staffing/capacity rules, automatic grants, accruals, carry-over, Entra, and SharePoint remain future work.
