# Leave Requests

Leave requests represent an employee request for a leave type over business dates.

## Lifecycle

Known statuses are DRAFT, PENDING_APPROVAL, APPROVED, REJECTED, CANCELLATION_REQUESTED, CANCELLED, REVOKED, and COMPLETED. EP-07 implements DRAFT -> PENDING_APPROVAL. EP-08 implements only the final approval decisions PENDING_APPROVAL -> APPROVED and PENDING_APPROVAL -> REJECTED.

A DRAFT may be edited by its owner. Once submitted, core request fields, the frozen policy version, calculated days, and balance reservation linkage are not editable through the draft update API.

## Submission

Submission is transactional. The backend revalidates leave type activity, the stored OrgUnit assignment at StartDate, date order, effective published policy, policy request rules, overlaps, and balance reservation.

Policy resolution uses StartDate. The exact LeavePolicyVersionId is frozen on the request, and CalculatedDays is stored as the evaluated quantity. Future policy or WorkingCalendar changes must not silently recalculate a submitted request.

## Calculation and half days

FULL_DAY uses the EP-05 DayCalculator. CALENDAR_DAYS counts the inclusive date range. BUSINESS_DAYS uses the WorkingCalendar referenced by the frozen policy version.

HALF_DAY is limited to StartDate == EndDate, requires AllowHalfDay, and stores 0.5. For BUSINESS_DAYS, the requested date must be a working day. AM/PM and multi-day partial day semantics are deferred.

## Overlaps

Overlap checks are per employee: existing.StartDate <= candidate.EndDate and existing.EndDate >= candidate.StartDate. DRAFT, REJECTED, CANCELLED, and REVOKED do not block. PENDING_APPROVAL is active in EP-07; APPROVED and CANCELLATION_REQUESTED are recognized as active for future compatibility.

Policy OverlapBehavior controls submission: BLOCK rejects, WARN submits with a warning, ALLOW submits normally.

## Balance integration

Non-consuming policies create no account or ledger entry. Consuming policies reserve CalculatedDays through the EP-06 RESERVE ledger semantic using the policy BalanceBucketId. The request stores BalanceAccountId and BalanceReservationOperationId for future settlement.

Submission is idempotent: once PENDING_APPROVAL, retry returns the submitted request and does not reserve again. Future APPROVE will convert the existing reservation using CONSUME. Future REJECT will release it using RELEASE.

## Approval and rejection

EP-08 uses a single final decision model. An authorized actor with `leave.requests.decide` and organizational scope over the request's stored `OrgUnitId` may approve or reject a submitted request. The actor may not decide their own request, even if they hold supervisor, manager, or HR permissions.

Approval comments are optional. Rejection requires a non-empty reason. The employee's original request comment is not changed by approval or rejection.

Approval/rejection is idempotent by command `OperationId`, concurrency-safe, and auditable through immutable decision history. `APPROVED` and `REJECTED` are final for this EP. Cancellation, revocation, completion, and multi-step approval are deferred.

Approval never re-resolves policy, recalculates days, or selects a new balance bucket. The frozen request values from submission are authoritative.

## Authorization

Employees can create, edit, submit, list, and read their own requests. Supervisor, Manager, and HR may read requests inside organizational scope. EP-07 does not allow manager/HR creation on behalf or mutation of another user's request. Technical administrators receive no automatic business request permission.

MinimumNoticeDays validation remains intentionally deferred until company timezone and inclusive/exclusive notice semantics are explicitly decided.
