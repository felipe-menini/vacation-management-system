# 019 - Scoped HR Reporting

EP-19 closes a privacy-minimized HR / Management reporting slice. It provides one scoped aggregate backend endpoint and a frontend dashboard for operational reporting without exposing employee-level, request-level, leave-type, medical, document, balance, comment, reason, or audit details.

## Status

Completed through EP-19C final closure and validation.

## Implemented behavior

### Permission and authorization

- Adds explicit permission `leave.reports.read`.
- Reporting access is enforced by the backend using the existing **Role + Permission + Organizational Scope** model.
- Development seed grants `leave.reports.read` to:
  - `HR` at company/root scope with descendants.
  - `MANAGER` according to the existing scoped manager convention.
- `TECH_ADMIN` does not receive reporting access automatically.
- Frontend navigation is permission/access driven by the protected report endpoint; role names are not treated as security controls.

### Scope enforcement

- Every report metric is filtered by the actor's effective `leave.reports.read` organizational scope.
- Scope is evaluated against the stored `LeaveRequest.OrgUnitId` on each request.
- Historical visibility is therefore tied to the org unit captured when the leave request was created, not to later employee reassignment.
- `IncludeDescendants` semantics are reused from the existing authorization model.
- Optional `orgUnitId` narrows the report to the requested org subtree intersected with the actor's authorized scope; it cannot broaden visibility.
- The org-unit aggregate breakdown returns authorized org units only.

### Endpoint

`GET /api/reports/leave-summary?from=YYYY-MM-DD&to=YYYY-MM-DD[&orgUnitId=...]`

Validation:

- `from` and `to` are required.
- `from <= to` is required.
- The inclusive range is bounded to 366 days.

The endpoint is read-only and returns aggregate DTOs only.

### Current Workload metrics

Current Workload metrics are **current-state workflow counts**, not historical period metrics:

- `PendingApprovalCount`: current scoped requests with `PENDING_APPROVAL`.
- `CancellationRequestedCount`: current scoped requests with `CANCELLATION_REQUESTED`.

These values are not limited by the selected `from` / `to` period.

### Selected-period metrics

Selected-period absence metrics use date overlap semantics:

```text
StartDate <= to
EndDate >= from
```

Operational status semantics:

- `ApprovedAbsenceCount`: overlapping `APPROVED` requests.
- `CompletedAbsenceCount`: overlapping `COMPLETED` requests.
- `CancellationRequestedAbsenceCount`: overlapping `CANCELLATION_REQUESTED` requests because the absence remains operationally effective until cancellation is accepted.
- `ApprovedOrEffectiveAbsenceCount`: overlapping `APPROVED`, `COMPLETED`, and `CANCELLATION_REQUESTED` requests.
- `UniqueEmployeesWithApprovedOrCompletedAbsence`: distinct employees with overlapping `APPROVED` or `COMPLETED` requests.

### Org-unit aggregate breakdown

`OrgUnitBreakdown` returns visible aggregate rows only:

- `OrgUnitId`
- `OrgUnitName`
- `ApprovedOrEffectiveAbsenceCount`
- `UniqueEmployeeCount`

The breakdown uses overlapping `APPROVED`, `COMPLETED`, and `CANCELLATION_REQUESTED` requests.

### Frontend dashboard

- Adds a Reports / HR Dashboard navigation item only when the selected actor can access the protected reporting endpoint.
- Defaults the selected report period to the current calendar month.
- Supports previous month, current month, next month, explicit From / To dates, and manual Refresh.
- Supports optional organization filtering when org-unit data is available; backend scoping remains authoritative.
- Enforces the 366-day maximum range client-side before requesting data.
- Shows Current Workload as visually distinct from Selected Period metrics.
- Renders org-unit breakdown rows responsively on desktop, tablet, and narrow screens.
- Handles loading, zero-data, 401, 403, and generic error states without leaking hidden data.

## Privacy contract

EP-19 exposes aggregate reporting only.

It intentionally does **not** expose or calculate:

- employee-level data or employee rows
- individual leave request lists
- request-level drill-down
- leave type, reason, or medical breakdowns
- comments, cancellation reasons, decision reasons, document metadata, storage keys, balances, audit records, or medical indicators
- misleading percentages, absence rates, utilization rates, attendance rates, or department percentages
- total-day metrics based on summing `CalculatedDays`

This keeps the report useful for operational visibility without turning small teams or sensitive leave categories into indirect disclosure channels.

## Explicit deferrals

The following remain intentionally out of scope for EP-19:

- CSV / Excel export
- PDF export
- request-level drill-down
- employee-level reporting
- leave-type analytics
- medical analytics
- absence-rate percentages
- precise day-utilization allocation
- scheduled reports
- email reports
- Power BI integration
- custom report builder
- Entra integration
- SharePoint migration
