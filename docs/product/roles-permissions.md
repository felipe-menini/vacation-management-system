# Roles and Permissions

Permissions are determined by Role + Organizational Scope. See [authorization.md](../architecture/authorization.md) for enforcement architecture.

## Roles

| Role | Main responsibility |
| --- | --- |
| Employee | Operates only on own balances, requests, comments, and allowed attachments. |
| Supervisor | Manages requests and visibility for people in assigned units, usually without descendants. |
| Encargado | Manages higher-level units and, when configured, descendant units. |
| RRHH | Organization-wide functional access, configuration, balance adjustments, reports, and exceptions. |
| Technical Administrator | Technical parameters, integrations, technical catalogs, and support. No default HR approval authority or medical-document access. |

## MVP permission matrix

| Action | Employee | Supervisor | Encargado | RRHH | Technical admin |
| --- | --- | --- | --- | --- | --- |
| View own balance | Yes | Yes | Yes | Yes | Yes |
| View third-party balance | No | In scope | In scope + descendants | Entire organization | Only if support access is authorized |
| Create own request | Yes | Yes | Yes | Yes | Yes |
| Create leave for others | No | In scope, if enabled | In scope, if enabled | Yes | No by default |
| Approve/reject | No | In scope | In scope | Yes | No |
| Revoke approved leave | No | In scope, if enabled | In scope, if enabled | Yes | No |
| Request own cancellation | Yes | Yes | Yes | Yes | Yes |
| Approve cancellation | No | In scope | In scope | Yes | No |
| Add comments | Own requests | In scope | In scope | Yes | Technical comments only |
| View medical certificate | Own certificate | Configurable, normally No | Configurable, normally No | Yes, according to function | No by default |
| Adjust balance | No | Configurable | Configurable | Yes | No |
| Configure types/policies | No | No | No | Yes | Technical support without functional decision |
| Configure units/scopes | No | No | No | Yes/delegated | Technical setup |
| View audit | Own limited audit | In scope | In scope | Yes | Technical audit |

Manual creation, revocation, and balance adjustment require mandatory reason and audit.



## Implemented organization permissions

The first authorization slice defines only organization-management capabilities:

| Permission | Description |
| --- | --- |
| `org.units.read` | Read organizational units inside assigned scope. |
| `org.units.manage` | Create/update organizational units inside assigned scope. |
| `org.users.read` | Read users whose active organizational membership is inside assigned scope. |
| `org.users.manage` | Create/update users within authorized organizational administration scope. |
| `org.assignments.read` | Read user organizational assignments inside assigned scope. |
| `org.assignments.manage` | Manage user organizational assignments inside assigned scope. |

Leave requests, balances, approvals, audit, and document permissions are intentionally deferred until those capabilities exist. EP-04 adds policy-configuration permissions below.

## Open Decisions

- Who can view medical certificates besides the employee and RRHH?
- Can supervisors and encargados always create manual leave/revoke leave, or does it depend on leave type?
- Which roles may adjust balance outside RRHH, if any?

## Related documents

- [Authorization architecture](../architecture/authorization.md)
- [Security](../architecture/security.md)

## Implemented leave policy permissions

EP-04 adds `leave.policies.read` and `leave.policies.manage`. Development seed grants read to Employee, Supervisor, Manager, and HR. HR receives manage. Technical Administrator does not receive policy manage by default because policy configuration is an HR/business capability, not technical administration.

## Implemented working calendar permissions

EP-05 adds:

| Permission | Description |
| --- | --- |
| `leave.calendars.read` | Read working calendars, weekday rules, dated exceptions, and use the calculation inspector. |
| `leave.calendars.manage` | Create/update working calendars and dated exceptions. |

Development seed grants calendar read to Employee, Supervisor, Manager, and HR. HR receives calendar manage. Technical Administrator does not receive calendar manage by default because working-calendar configuration is a business/HR capability, not automatic technical administration.

## Implemented balance permissions

EP-06 adds:

| Permission | Description |
| --- | --- |
| `leave.balances.read.self` | Read own balances. |
| `leave.balances.read` | Read balances within organizational scope. |
| `leave.balances.manage` | Grant, adjust, and expire balances within organizational scope. |

Development seed grants self-read to Employee, Supervisor, Manager, and HR. Supervisor and Manager receive scoped balance read. HR receives scoped balance read and manage. Technical Administrator receives no automatic business balance permission.

## Implemented leave request permissions

EP-07 adds:

| Permission | Description |
| --- | --- |
| `leave.requests.read.self` | Read own leave requests. |
| `leave.requests.create.self` | Create, edit draft, and submit own leave requests. |
| `leave.requests.read` | Read leave requests inside authorized organizational scope. |

Development seed grants self permissions to Employee, Supervisor, Manager, and HR. Supervisor, Manager, and HR receive scoped read. Technical Administrator receives no automatic business request permission.

## Implemented approval permission

EP-08 adds:

| Permission | Description |
| --- | --- |
| `leave.requests.decide` | Approve or reject submitted leave requests inside authorized organizational scope. |

Development seed grants `leave.requests.decide` to Supervisor, Manager, and HR. Employee does not receive it. Technical Administrator still receives no automatic business decision permission.

Self-approval and self-rejection are prohibited even when the actor has this permission. Scope is evaluated against the request's stored `OrgUnitId`.

## Implemented request lifecycle permissions

EP-09 adds:

| Permission | Description |
| --- | --- |
| `leave.requests.cancel.self` | Request cancellation of own approved leave requests. |
| `leave.requests.cancel.decide` | Approve or reject cancellation requests inside authorized organizational scope. |
| `leave.requests.revoke` | Revoke approved leave requests inside authorized organizational scope. |
| `leave.requests.create.for_others` | Create and submit leave requests for employees inside authorized organizational scope. |

Development seed grants these EP-09 scoped lifecycle permissions to Supervisor, Manager, and HR. Technical Administrator still receives no automatic business request permission.

Cancellation decision and revocation are prohibited on the actor's own request even when the actor has the permission. Manual create-for-others submits through the same policy, calculation, overlap, and balance path as employee submission.

## Implemented leave document permissions

EP-10 adds:

| Permission | Description |
| --- | --- |
| `leave.documents.read.self` | Read documents attached to own leave requests. |
| `leave.documents.upload.self` | Upload documents to own leave requests. |
| `leave.documents.read` | Read leave request documents inside authorized organizational scope. |

Development seed grants own read/upload to Employee, Supervisor, Manager, and HR. Supervisor, Manager, and HR receive scoped document read. Technical Administrator receives no automatic medical document access.

Scoped document access is authorized independently from ordinary request visibility and uses the leave request's stored `OrgUnitId`, not the employee's current assignment.
