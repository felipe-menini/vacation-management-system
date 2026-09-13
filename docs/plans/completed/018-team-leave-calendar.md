# 018 - Team Leave Calendar

EP-18 delivers an operational team-availability calendar. It answers who is expected to be away, on which dates, in which organizational unit, and in which lifecycle state. It is read-only and privacy-minimized; it is not a replacement for detailed leave-request APIs.

## Completed scope

### EP-18A - Backend read model

- Adds `GET /api/leave-calendar`.
- Requires explicit permission `leave.calendar.read`.
- Enforces authorization through the existing Role + Permission + Organizational Scope model.
- Technical administrators do not receive calendar access automatically.
- Uses the stored `LeaveRequest.OrgUnitId` for visibility; later/current employee assignment changes do not alter historical request visibility.
- Reuses existing IncludeDescendants semantics through authorized organizational scope expansion.
- Supports optional `orgUnitId` narrowing. The requested org subtree is intersected with the actor's effective scope and cannot expand access.
- Requires `from=YYYY-MM-DD` and `to=YYYY-MM-DD`.
- Applies overlap semantics: `StartDate <= to` and `EndDate >= from`.
- Bounds the maximum query range to 366 days.
- Supports bounded pagination with `page` and `pageSize`; `pageSize` is capped at 100.
- Uses deterministic ordering: `StartDate ASC`, subject display name, then leave request id.

### EP-18B - Frontend calendar view

- Shows the `Team calendar` navigation entry only when the actor can read the calendar endpoint.
- Provides previous month, current/today month, and next month navigation.
- Sends the selected month as the `from`/`to` query window.
- Reuses existing org-unit tree data for the optional organization filter.
- Respects backend pagination and shows backend `totalCount`.
- Uses a responsive read-only list/timeline layout that remains usable on desktop and mobile.
- Handles loading, empty, unauthorized/forbidden, and generic failure states safely.
- Provides no request detail links and no mutation actions from the calendar.

## Visible statuses

The calendar includes:

- `APPROVED`
- `COMPLETED`
- `CANCELLATION_REQUESTED`

`CANCELLATION_REQUESTED` remains visible because the approved absence remains effective until the cancellation is accepted. The frontend renders it distinctly as pending cancellation, not as already cancelled.

The calendar excludes:

- `DRAFT`
- `PENDING_APPROVAL`
- `REJECTED`
- `CANCELLED`
- `REVOKED`

## Privacy contract

The calendar DTO intentionally exposes only:

- leave request id
- subject user id and display name
- org unit id and name
- start/end dates
- day portion (`FULL_DAY` / `HALF_DAY`)
- status

`HALF_DAY` is exposed only as a half-day marker. AM/PM, morning, afternoon, or other partial-day semantics are intentionally not invented.

The calendar intentionally hides:

- leave type and leave reason
- comments
- approval decision comments or rejection reasons
- cancellation reasons
- policy version and balance details
- audit metadata
- document metadata
- document existence indicators
- private storage keys
- medical indicators or medical-document metadata

## EP-18C final closure

Final integration review found no genuine backend or frontend integration gap requiring a code change. The remaining work was documentation closure, plan archival, and one final validation pass.

## Explicit deferrals

The following are intentionally deferred and were not implemented in EP-18:

- drag/drop scheduling
- editing requests from the calendar
- leave-type/reason exposure
- staffing/capacity forecasting
- staffing alerts
- ICS export
- Outlook calendar sync
- Microsoft Graph calendar integration
- Entra integration changes
- SharePoint integration or migration behavior
