# Leave Requests

Leave requests represent an employee request for a leave type over business dates.

## Lifecycle

Known statuses are DRAFT, PENDING_APPROVAL, APPROVED, REJECTED, CANCELLATION_REQUESTED, CANCELLED, REVOKED, and COMPLETED. EP-07 implements DRAFT -> PENDING_APPROVAL. EP-08 implements the final approval decisions PENDING_APPROVAL -> APPROVED and PENDING_APPROVAL -> REJECTED. EP-09 implements approved-request cancellation, cancellation approval/rejection, revocation, and manual create-for-others.

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

Approval/rejection is idempotent by command `OperationId`, concurrency-safe, and auditable through immutable decision history.

Approval never re-resolves policy, recalculates days, or selects a new balance bucket. The frozen request values from submission are authoritative.

## Cancellation and revocation

EP-09 allows the owner of an `APPROVED` request to request cancellation. The request moves to `CANCELLATION_REQUESTED` and remains an active overlap until an authorized actor resolves it. Approving the cancellation moves it to `CANCELLED`; rejecting the cancellation moves it back to `APPROVED`.

Administrative revocation moves an `APPROVED` request to `REVOKED`. The actor must be authorized for the request's stored organizational unit and cannot revoke their own request.

Approved cancellation and revocation refund consuming leave with a `REFUND` ledger transaction based only on the frozen request values. Rejected cancellation does not mutate balance. Cancellation and revocation history records are immutable; they are not replaced or deleted.

## Manual creation for others

EP-09 allows authorized actors to create a leave request for another employee inside scope. The operation immediately reuses normal submission behavior: assignment validation, policy resolution, calculation, overlap checks, balance reservation, and idempotency. A successful manual create-for-others finishes as `PENDING_APPROVAL`; it does not auto-approve.

## Authorization

Employees can create, edit, submit, list, read, and request cancellation of their own requests. Supervisor, Manager, and HR may read, decide, resolve cancellations, revoke, and create requests for others inside organizational scope when granted the corresponding permissions. Technical administrators receive no automatic business request permission.

MinimumNoticeDays is enforced by the backend at submission. Draft creation/editing may temporarily violate notice rules. Manual create-for-others follows the same rule; no administrative or retroactive bypass exists yet. A failed notice submission leaves the request in DRAFT and has no balance, audit, outbox, status, or approval-history side effects.

## Private medical certificates

EP-10 supports optional `MEDICAL_CERTIFICATE` attachments on leave requests. Documents remain associated with the request across APPROVED, REJECTED, CANCELLED, and REVOKED states. Uploading a document does not change status, recalculate days, re-resolve policy, mutate balances, or alter approval/cancellation history.

Supported uploads are PDF, JPEG, and PNG up to the configured maximum size, with a Development default of 10 MB. The backend validates file extension, declared content type where detectable, magic bytes, non-empty content, and maximum size. OCR, antivirus scanning, previews, retention/deletion policy, and policy-required-document enforcement are deferred.