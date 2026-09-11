# 012 - Audit Trail

Status: Completed

## Completed scope

EP-12 delivered an append-only audit trail foundation plus successful business audit writes for the currently implemented workflows.

Implemented audit coverage:

- Append-only `AuditEvent` foundation and PostgreSQL persistence.
- Leave request lifecycle audit.
- Sensitive medical document upload/read audit.
- Balance administration audit.
- Leave policy and working calendar administration audit.
- Organization unit and user organizational assignment mutation audit.
- Permission-controlled audit viewer API and frontend page.

## Authorization and visibility

Audit viewing is controlled by the permission:

- `audit.events.read`

The viewer uses the existing Role + Permission + Organizational Scope authorization model. It does not hardcode HR, manager, or technical administrator checks.

Development seeding grants `audit.events.read` to HR with company/root organizational scope including descendants. It is intentionally not granted to `TECH_ADMIN`; technical administration alone does not imply visibility into business or sensitive audit history.

Backend audit queries enforce organizational scope:

- Events with `OrgUnitId` are visible only when the actor has `audit.events.read` over that org unit through the existing scope/descendant semantics.
- Events with `OrgUnitId = null` are treated as global/company-level events and require company/root-level audit scope.
- Frontend filtering is not a security control.

## API and frontend

Implemented read-only API:

- `GET /api/audit-events`

Supported filters:

- `fromUtc`
- `toUtc`
- `action`
- `resourceType`
- `resourceId`
- `actorUserId`
- `subjectUserId`
- `orgUnitId`
- bounded `page` / `pageSize` pagination capped at 100

Sorting is deterministic: `OccurredAtUtc DESC`, then `Id DESC`.

The frontend includes a functional administrative audit viewer with action, resource type, and date range filters, pagination, and expandable metadata display. It exposes no edit/delete controls and no export.

## Privacy and immutability

Audit records remain immutable. The database rejects direct update/delete via the EP-12A trigger and the API exposes no mutation endpoints.

Metadata remains constrained to safe audit context. The viewer must not expose medical file bytes, storage keys, authentication tokens, cookies, authorization headers, passwords, or document content. Seeing a `leave.document.read` event does not grant access to the document itself.

## Known gap

Authorization-management mutation audit remains a gap because the application still has no RoleScopeAssignment or RolePermission administration CRUD/API to audit. Development seeding is not treated as a business administration mutation.

## Deferred capabilities

- RoleScopeAssignment admin CRUD.
- RolePermission admin CRUD.
- Audit record editing/deleting.
- CSV/Excel export.
- SIEM integration.
- Audit retention/purge.
- Automated compliance alerts.
- Failed-login auditing.
- Failed authorization-attempt auditing.
- Microsoft Entra integration.
- SharePoint integration.
