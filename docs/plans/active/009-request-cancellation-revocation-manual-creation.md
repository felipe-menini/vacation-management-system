# 009 - Leave Request Cancellation, Revocation, and Manual Creation

EP-09 extends the EP-07/EP-08 leave request workflow after approval and adds authorized creation on behalf of another employee.

## Scope

- Employee-requested cancellation lifecycle:
  - `APPROVED -> CANCELLATION_REQUESTED`
  - `CANCELLATION_REQUESTED -> CANCELLED`
  - `CANCELLATION_REQUESTED -> APPROVED`
- Administrative revocation:
  - `APPROVED -> REVOKED`
- Balance settlement:
  - approved cancellation of consuming leave creates a `REFUND`
  - revocation of consuming leave creates a `REFUND`
  - rejected cancellation does not mutate balance
- Immutable business history:
  - one cancellation history record per request
  - one revocation history record per request
  - database constraints/triggers prevent deletion or post-decision mutation
- Idempotent commands by operation id:
  - manual create/submit uses `SubmissionOperationId`
  - cancellation request uses `OperationId`
  - cancellation decision uses `OperationId`
  - revocation uses `OperationId`
- Backend authorization and scope:
  - own cancellation requires `leave.requests.cancel.self`
  - cancellation decision requires `leave.requests.cancel.decide` plus request `OrgUnitId` scope
  - revocation requires `leave.requests.revoke` plus request `OrgUnitId` scope
  - manual creation requires `leave.requests.create.for_others` plus target request `OrgUnitId` scope
- Self-decision/self-revocation are prohibited by backend application logic.
- Manual creation for others reuses normal submission validation, policy resolution, day calculation, overlap validation, and balance reservation, then ends in `PENDING_APPROVAL`.
- API and frontend expose a small operational slice for cancellation inbox, revocation, and create-for-employee.

## Implemented artifacts

- Domain:
  - `LeaveRequestCancellation`
  - `LeaveRequestRevocation`
  - extended `LeaveRequestStatus`
- Persistence:
  - migration `20260911015621_LeaveRequestCancellationRevocationManualCreation`
  - `leave_request_cancellations`
  - `leave_request_revocations`
  - `leave_requests.submission_operation_id`
- API:
  - `GET /api/leave-requests/pending-cancellation`
  - `POST /api/leave-requests/{id}/request-cancellation`
  - `POST /api/leave-requests/{id}/approve-cancellation`
  - `POST /api/leave-requests/{id}/reject-cancellation`
  - `POST /api/leave-requests/{id}/revoke`
  - `POST /api/users/{userId}/leave-requests`

## Deferred

Partial cancellation, modifying dates after approval, completion automation, multi-step approval routing, notifications, attachments, and audit-event/outbox integration remain future work.
