# 001 - Organizational Structure

This vertical slice adds the internal user profile model, organizational hierarchy, user-to-unit membership, development seed data, administrative API endpoints, and a simple development UI for validation.

## Quick path

1. Run PostgreSQL through Docker Compose or a local compatible instance.
2. Start the API in Development so migrations and seed data run.
3. Open the frontend and verify the organization tree and primary user units.

## Implemented entities

| Entity | Purpose |
| --- | --- |
| `User` | Internal user profile. Uses a generated internal `Id`; `Email` is not the primary key. |
| `OrgUnit` | Organizational hierarchy node using nullable `ParentId`. |
| `UserOrgAssignment` | Membership between users and organizational units, including primary assignment and effective dates. |

## Hierarchy rules

- An organizational unit cannot use itself as parent.
- Application logic rejects parent changes that would create hierarchy cycles.
- Organizational unit codes are normalized to uppercase and must be unique.
- Business records are not hard-deleted by this slice; `IsActive` supports deactivation.

## Membership model

- A user may have multiple organizational assignments.
- Historical assignments remain stored with `EffectiveToUtc`.
- Application logic allows multiple memberships but rejects a second active primary assignment for the same user.
- The database has a PostgreSQL exclusion constraint to prevent overlapping primary assignment periods for a user.

## Database constraints

| Area | Constraint |
| --- | --- |
| `org_units.code` | Unique index. |
| `org_units.parent_id` | Self-referencing foreign key with restrict delete. |
| `org_units` | Check constraint prevents `parent_id = id`. |
| `users.email` | Unique index for profile lookup; not used as primary key. |
| `users.external_identity_id` | Filtered unique index when non-null. |
| `user_org_assignments` | Foreign keys to users and org units with restrict delete. |
| `user_org_assignments` | Check constraint ensures `EffectiveToUtc` is after `EffectiveFromUtc`. |
| `user_org_assignments` | PostgreSQL `btree_gist` exclusion constraint prevents overlapping primary assignment periods per user. |

## Endpoints

These endpoints are intentionally basic administrative endpoints for exercising the model only:

- `GET /api/org-units`
- `GET /api/org-units/tree`
- `GET /api/org-units/{id}`
- `POST /api/org-units`
- `PUT /api/org-units/{id}`
- `GET /api/users`
- `GET /api/users/{id}`
- `POST /api/users`
- `PUT /api/users/{id}`
- `GET /api/users/{id}/org-assignments`
- `POST /api/users/{id}/org-assignments`

> Security note: these endpoints are unsecured only because authentication and authorization are explicitly deferred. They MUST NOT be exposed unsecured in production. The route shape is stable enough to add backend authorization policies later.

## Development seed data

Development startup applies migrations and idempotently seeds:

```text
Company
└── IT
    ├── Support
    ├── Development
    └── Cybersecurity
```

Users:

- Felipe -> IT
- Support User -> Support
- Development User -> Development
- Cybersecurity User -> Cybersecurity

Seed data runs only in Development.

## Microsoft Entra deferred

Microsoft Entra authentication, Graph integration, passwords, roles, permissions, authorization scopes, supervisors, leave policies, balances, requests, approvals, and SharePoint integration are intentionally out of scope.

`User.ExternalIdentityId` is nullable now and is the future link to the Microsoft Entra Object ID. This keeps Entra as an external identity provider and avoids changing the domain model when authentication is added later.

## Open decisions

- Whether user email uniqueness should be case-insensitive through a PostgreSQL `citext` extension or continue with application-level normalization.
- Whether historical primary assignments should allow overlapping effective date ranges when they are closed-ended.
- Whether organization tree reads should eventually exclude inactive units by default or return them with status metadata.
