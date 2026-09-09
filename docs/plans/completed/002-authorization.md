# 002 - Authorization: Role + Permission + Organizational Scope

This vertical slice introduces backend-enforced authorization for the existing organization API. Microsoft Entra remains deferred; the current identity source is temporary Development-only infrastructure.

## Authorization model

Effective authorization is calculated from:

```text
Current actor -> active RoleScopeAssignment -> active Role -> Permission -> target organizational scope
```

Application code authorizes capabilities by permission code, not by role-code comparisons. Roles are only collections of permissions.

## Permission catalog

Initial organization-only permissions:

| Permission | Purpose |
| --- | --- |
| `org.units.read` | Read organizational units. |
| `org.units.manage` | Create and update organizational units. |
| `org.users.read` | Read users inside authorized organizational scope. |
| `org.users.manage` | Create and update users inside authorized organizational scope. |
| `org.assignments.read` | Read user organizational assignments inside authorized scope. |
| `org.assignments.manage` | Manage user organizational assignments inside authorized scope. |

Leave-management permissions are intentionally not created in this slice.

## Role model

Roles are stored in `roles` and connected to permissions through `role_permissions`.

Initial system role codes:

- `EMPLOYEE`
- `SUPERVISOR`
- `MANAGER`
- `HR`
- `TECH_ADMIN`

`EMPLOYEE` intentionally has no cross-user organization permissions in the development seed. `TECH_ADMIN` gets only limited organization read permission and no HR/business authority by default.

## Scope model

`role_scope_assignments` combines:

- user
- role
- scoped organizational unit
- include-descendants flag
- effective period

The role and organizational scope belong to the same assignment. A user can have multiple assignments; authorization is the union of valid active assignments.

## Descendant resolution

`AuthorizationService` resolves active organizational units, builds the parent/child map, and expands descendants only when `IncludeDescendants` is true. If false, only the assignment root unit is authorized.

Examples supported by the development seed:

- Felipe: `MANAGER @ IT`, descendants=true -> IT, Support, Development, Cybersecurity.
- Support Supervisor: `SUPERVISOR @ Support`, descendants=false -> Support only.

Sibling and unrelated branches such as HR are excluded unless granted by another valid assignment.

## Current actor abstraction

Application authorization uses `ICurrentActor`, which exposes the local actor user id. Application/domain code does not depend on ASP.NET `HttpContext`, Microsoft Entra, development headers, or frontend state.

## Development identity mechanism

The API includes a temporary `DevelopmentCurrentActor` adapter that reads `X-Dev-User-Id` only when the host environment is Development.

Strict limitations:

- Development identity works only in `ASPNETCORE_ENVIRONMENT=Development`.
- Non-Development environments ignore `X-Dev-User-Id`.
- Production must not silently accept the development header.
- No passwords or local login are implemented.

Microsoft Entra will later replace the development actor mechanism as the source of `ICurrentActor`. The authorization domain/application logic should not need redesign when that happens.

## HTTP behavior

- `401 Unauthorized`: no current actor/identity is available.
- `403 Forbidden`: actor is known but lacks the required permission/scope for a command or collection.
- `404 Not Found`: used for single-resource reads where revealing existence of out-of-scope resources could leak information.

## Protected endpoints

- `GET /api/org-units` -> `org.units.read`, scoped.
- `GET /api/org-units/tree` -> `org.units.read`, scoped.
- `GET /api/org-units/{id}` -> `org.units.read` for target unit.
- `POST /api/org-units` -> `org.units.manage` for parent unit.
- `PUT /api/org-units/{id}` -> `org.units.manage` for target unit and new parent when present.
- `GET /api/users` -> `org.users.read`, filtered to effective organizational scope.
- `GET /api/users/{id}` -> `org.users.read`, denies out-of-scope target users.
- `POST /api/users` -> `org.users.manage` in at least one valid scope.
- `PUT /api/users/{id}` -> `org.users.manage`, denies out-of-scope target users.
- `GET /api/users/{id}/org-assignments` -> `org.assignments.read`, scoped to target user.
- `POST /api/users/{id}/org-assignments` -> `org.assignments.manage`, scoped to target user and target org unit.

## Development seed

Seeded units:

```text
Company
├── IT
│   ├── Support
│   ├── Development
│   └── Cybersecurity
└── HR
```

Seeded authorization examples:

- Felipe -> `MANAGER @ IT`, descendants=true.
- Support Supervisor -> `SUPERVISOR @ Support`, descendants=false.
- Support User -> `EMPLOYEE @ Support`, descendants=false.
- Development User -> `EMPLOYEE @ Development`, descendants=false.
- Cybersecurity User -> `EMPLOYEE @ Cybersecurity`, descendants=false.

## Frontend development selector

The React app shows a Development-only actor selector and sends `X-Dev-User-Id` only when built/run by Vite in Development mode. This is demonstration infrastructure, not production authentication.

## Security limitations and future work

- Microsoft Entra authentication is still deferred.
- Audit events for authorization administration are not implemented yet.
- Role/scope management endpoints are not exposed yet; development seed creates the initial catalog.
- Org-wide HR behavior is represented as `HR @ Company` with descendants=true when seeded/assigned later.
- Production deployments must introduce the Entra-backed `ICurrentActor` adapter before exposing protected organization APIs.

## Verification plan

- Unit tests cover permission grants/denials, descendant scope, inactive users/roles, effective dates, sibling/unrelated exclusions, and multi-assignment union behavior.
- PostgreSQL integration tests verify persistence mappings and scoped user queries.
- API tests verify `401`, `403`, and Development-only header behavior.
