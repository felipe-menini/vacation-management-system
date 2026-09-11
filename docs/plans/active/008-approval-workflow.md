# 008 - Leave Request Approval Workflow

EP-08 implements the first final approval decision for submitted leave requests.

## Scope

- Decide only `PENDING_APPROVAL -> APPROVED` and `PENDING_APPROVAL -> REJECTED`.
- Use one immutable `LeaveRequestDecision` business-history record per request.
- Require `leave.requests.decide` plus organizational scope over the request's stored `OrgUnitId`.
- Prohibit self-approval/self-rejection in backend application logic.
- Settle the frozen EP-07 balance reservation:
  - approve consuming request: `CONSUME CalculatedDays`
  - reject consuming request: `RELEASE CalculatedDays`
  - non-consuming request: no ledger settlement
- Make decision commands idempotent by required `OperationId`.
- Process decision, status transition, decision record, and balance settlement in one EF/PostgreSQL transaction.
- Expose pending approvals, approve, and reject APIs plus a simple frontend approval inbox.

## Decisions

- EP-08 is a single final decision model. Multi-step approval, routing, delegation, quorum, and escalation are intentionally deferred.
- Approval uses the frozen `LeavePolicyVersionId`, `CalculatedDays`, `BalanceAccountId`, and `BalanceReservationOperationId`; it never re-resolves policy or recalculates days.
- Rejection requires a non-empty trimmed comment. Approval comments are optional.
- `APPROVED` and `REJECTED` are final for this EP.
- The request's stored `OrgUnitId` controls approval scope, not the employee's current assignment.

## Deferred

Cancellation, revocation, completion automation, manager-created requests, multi-step approval, configurable routing, delegation, substitute approvers, notifications, attachments, minimum notice enforcement, AM/PM half-days, staffing rules, accruals, Entra, and SharePoint remain future work.
