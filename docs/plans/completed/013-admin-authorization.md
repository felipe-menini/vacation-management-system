# 013 - Admin Authorization Foundation

Status: Completed

EP-13 completed the administration foundation for internal application users, organizational assignments, and role + organizational scope assignments.

## Completed scope

- EP-13A: backend application/domain/persistence foundations and audit records.
- EP-13B: HTTP endpoints and explicit backend authorization permissions.
- EP-13C: practical frontend administration UI.

## Users

Users are internal application records, not local login accounts.

- `User.Id` remains the stable internal application identity.
- Administrators with root/company `org.users.manage` authority can create users, update display name and email, activate users, and deactivate users.
- No password, local credential, local login, token, or credential management is introduced.
- External identity editing is intentionally not exposed in the frontend or EP-13B write contracts.
- Future Microsoft Entra/OIDC mapping remains deferred.

## Organization assignments

`UserOrgAssignment` remains the temporal assignment model for user membership in organizational units.

- Administrators with `org.assignments.manage` can create, update, and end assignments inside authorized organizational scope.
- Updates check authority over both the old assignment scope and the requested new scope.
- Existing hierarchy validity, primary assignment, valid-period, and overlap protections remain owned by the backend application/domain layer.
- The frontend performs only basic required-field/date capture and displays backend validation errors.

## Role scope assignments

`RoleScopeAssignment` remains the model for effective authorization: role + organizational scope.

- Administrators with `authorization.role_scopes.manage` can assign existing active roles to users at an org unit scope, choose `IncludeDescendants`, update assignment effective periods/scope, and revoke assignments.
- Role-scope creation is authorized against the requested target scope.
- Role-scope updates check authority over both the old scope and the requested new scope.
- Self role-scope assignment, update, and revocation are prohibited by the backend; the frontend also disables self-mutation controls where the current actor is known.
- Duplicate active role scopes and invalid effective periods are rejected by backend logic and surfaced in the UI.

## Roles and permissions

Roles, permissions, and role-permission mappings remain application-controlled.

EP-13 does not add:

- Role CRUD
- Permission CRUD
- RolePermission editor
- privilege-change approval workflow

The frontend uses `GET /api/roles` only to assign existing roles.

## Authorization model

Backend authorization is authoritative. Frontend visibility is convenience only.

- User administration requires root/company `org.users.manage` authority.
- Organizational assignment administration uses `org.assignments.manage` and organizational scope checks.
- Role-scope administration uses `authorization.role_scopes.manage` and organizational scope checks.
- Effective authorization remains role + organizational scope.
- `TECH_ADMIN` has no automatic business administration authority merely because it is technical.

## Frontend administration UI

The administration UI provides:

- Internal users list with active/inactive status.
- Create internal user form with explicit no-local-credentials messaging.
- Profile update for display name and email only.
- Activate/deactivate actions, with confirmation for deactivation.
- Selected-user organization assignment list, current/historical state, create/update/end actions.
- Selected-user role-scope list, current/historical state, assign/update/revoke actions.
- Self role-scope mutation controls disabled with explanation.
- Clear API error display for conflicts, invalid periods, overlap, unauthorized scope, missing resources, and self-mutation rejection.

No frontend audit writing was added. Successful admin operations continue to produce backend audit events:

- `user.*`
- `organization.assignment.*`
- `authorization.role_scope.*`

## Explicit deferrals

Not implemented in EP-13:

- Microsoft Entra
- OIDC
- local passwords
- login UI
- invitations
- Microsoft Graph
- Role CRUD
- Permission CRUD
- RolePermission editor
- privilege-change approval workflow
- bulk CSV import
- SharePoint synchronization
