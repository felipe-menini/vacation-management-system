# Product Requirements

The product is a corporate leave management system for employees, supervisors, encargados, RRHH, and technical administrators.

## Goals

- Let employees know their balances and manage their own leave requests.
- Let supervisors and encargados manage people within their organizational scope.
- Let RRHH configure leave types, policies, holidays, balances, calendars, and exceptions without deployments.
- Maintain history and audit of all decisions and relevant changes.
- Prevent balance and request inconsistencies through transactions, reservations, and concurrency validation.
- Protect sensitive documents and restrict access by business need.
- Migrate from SharePoint in a controlled way and reduce its operational role.

## MVP scope

- Microsoft Entra ID SSO and user provisioning.
- Organizational hierarchy, roles, and scopes.
- Configurable leave types and balance buckets.
- Versioned leave policies with main rules.
- Leave drafts, submission, approval, rejection, cancellation, and revocation.
- Manual leave creation by authorized roles.
- Comments and decision history.
- Attachments for leave types that require them.
- Business-day/calendar-day calculation, holidays, and half-days according to policy.
- Configurable overlap and simultaneous absence controls.
- Immutable balance ledger.
- Individual and team calendars.
- Approval inbox and notifications.
- Functional audit and basic reports.
- Initial SharePoint migration and reconciliation.

## MVP screens

| Area | Screens |
| --- | --- |
| Employee | Home / personal summary, new request, my requests, request detail, my balance / movements, my calendar. |
| Supervisor / Encargado | Approval inbox, my team, team calendar, employee detail, manual leave creation. |
| RRHH / Administration | HR dashboard, leave types, policy editor, balances and adjustments, organization and scopes, holidays/calendars, audit, SharePoint migration. |

## UX principles

- Balance cards distinguish available, pending/reserved, and consumed days.
- Destructive or administrative actions such as revoke and balance adjustment require confirmation and reason.
- Validation messages explain the rule that blocked or warned: holiday, insufficient balance, overlap, notice, coverage, or similar.
- Medical certificates never appear as thumbnails in general listings.
- Mobile views prioritize cards, compact filters, and bottom actions; wide tables become responsive lists.

## Out of initial MVP

These are recommended for later phases unless the business declares them mandatory during discovery: native iOS/Android apps, payroll/ERP integration, advanced electronic signature, external BPM engine, advanced analytics/BI, automated legal documentation, complex multi-company setup, and natural-language policy self-service.

## Suggested phase 2

- Deeper Teams/Outlook integration.
- Publish approved absences into calendars.
- Advanced staffing rules, blackout dates, and auto-approval.
- Partial cancellation or modification of approved requests.
- Temporary approver delegation.
- BI dashboards and advanced exports.
- Payroll/ERP/HRIS integration.
- Multi-site or multi-country differentiated policies.
- Self-service reporting and external APIs.

## Non-functional requirements

| Area | Initial objective |
| --- | --- |
| Availability | Business objective to be agreed; stateless backend should allow multiple instances. |
| Performance | Common screens under 2 seconds; business operations under 3 seconds except external integrations. |
| Scalability | Horizontal scale for frontend/API/worker; indexed database and connection pooling. |
| Security | OWASP ASVS/Top 10 guidance, SAST, dependency scanning, no secrets in repo. |
| Accessibility | WCAG 2.1 AA target for main flows. |
| Responsive UI | Desktop, tablet, and mobile without critical size-exclusive functionality. |
| Observability | Structured logs, metrics, traces, correlation IDs, and alerts. |
| Backup | Automated database/storage backups and periodic restore testing. |
| RPO/RTO | Open Decision: define with business before production. |
| Browser compatibility | Supported current Edge/Chrome versions; Safari mobile if user population requires it. |
| Localization | Spanish initially; architecture prepared for i18n if needed. |

## MVP acceptance criteria

1. A corporate user signs in with Entra ID without a local password.
2. An employee cannot access another employee's data except explicitly public calendar information.
3. A supervisor can only act on users inside assigned scope.
4. An encargado with descendants can manage the assigned child units correctly.
5. Overlap, half-day, and business/calendar-day rules can change without deployment.
6. Day calculation shows the applied rule and matches calendar/holiday tests.
7. Available, reserved, and consumed balance can be reconstructed from the ledger.
8. Request actions produce valid transitions and reject invalid transitions.
9. Medical attachments are private and denied to unauthorized roles.
10. Relevant administrative actions generate audit evidence.
11. The application is usable on mobile and desktop viewports.
12. SharePoint migration produces a reconciliation report without unjustified differences.

## Related documents

- [Architecture overview](../architecture/ARCHITECTURE.md)
- [Use cases](use-cases.md)
- [Workflows](workflows.md)
- [Leave policies](leave-policies.md)
