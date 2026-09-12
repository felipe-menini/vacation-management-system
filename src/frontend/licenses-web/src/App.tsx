import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';

type HealthStatus = 'Loading' | 'Healthy' | 'Unhealthy' | 'Degraded' | 'Unavailable';

type HealthResponse = { status: string };
type OrgUnit = { id: string; name: string; code: string; parentId: string | null; isActive: boolean; children?: OrgUnit[] };
type User = { id: string; displayName: string; email: string; isActive: boolean; primaryOrgUnit: OrgUnit | null };
type DevelopmentActor = { id: string; displayName: string; email: string; primaryOrgUnitName: string | null };
type LeaveType = { id: string; code: string; name: string; description: string | null; isActive: boolean; sortOrder: number };
type BalanceBucket = { id: string; code: string; name: string; description: string | null; unit: string; isActive: boolean };
type LeavePolicy = { id: string; leaveTypeId: string; leaveTypeCode: string; leaveTypeName: string; orgUnitId: string | null; orgUnitCode: string | null; orgUnitName: string | null; appliesToDescendants: boolean; isActive: boolean };
type LeavePolicyVersion = { id: string; leavePolicyId: string; versionNumber: number; status: string; effectiveFrom: string; effectiveTo: string | null; dayCountMode: string; allowHalfDay: boolean; minimumNoticeDays: number | null; noticeDayCountMode: string; maximumRequestDays: number | null; overlapBehavior: string; consumesBalance: boolean; balanceBucketId: string | null; balanceBucketCode: string | null; balanceBucketName: string | null; workingCalendarId: string | null; workingCalendarCode: string | null; workingCalendarName: string | null };
type ResolvePolicyResult = { found: boolean; policy: LeavePolicy | null; version: LeavePolicyVersion | null; reason: string | null };
type WorkingCalendarWeekday = { dayOfWeek: string; isWorkingDay: boolean };
type WorkingCalendarException = { id: string; workingCalendarId: string; date: string; name: string; isWorkingDay: boolean };
type WorkingCalendar = { id: string; code: string; name: string; description: string | null; isActive: boolean; weekdays: WorkingCalendarWeekday[]; exceptions: WorkingCalendarException[] };
type DayCalculationResult = { startDate: string; endDate: string; dayCountMode: string; workingCalendarId: string | null; calculatedDays: number; details: { date: string; isCounted: boolean }[] };
type BalanceSnapshot = { userId: string; balanceBucketId: string; balanceBucketCode: string; balanceBucketName: string; unit: string; available: number; reserved: number };
type BalanceLedgerEntry = { id: string; operationId: string; type: string; availableDelta: number; reservedDelta: number; reason: string; createdByUserId: string | null; createdByUserName: string | null; createdAtUtc: string };
type LeaveRequestDecision = { id: string; leaveRequestId: string; decision: string; decidedByUserId: string; decidedByUserDisplayName: string | null; comment: string | null; operationId: string; balanceSettlementOperationId: string | null; createdAtUtc: string };
type LeaveRequestCancellation = { id: string; leaveRequestId: string; requestedByUserId: string; requestedByUserDisplayName: string | null; reason: string; operationId: string; requestedAtUtc: string; decision: string | null; decidedByUserId: string | null; decidedByUserDisplayName: string | null; decisionComment: string | null; decisionOperationId: string | null; balanceSettlementOperationId: string | null; decidedAtUtc: string | null };
type LeaveRequestRevocation = { id: string; leaveRequestId: string; revokedByUserId: string; revokedByUserDisplayName: string | null; reason: string; operationId: string; balanceSettlementOperationId: string | null; createdAtUtc: string };
type LeaveRequestDocument = { id: string; leaveRequestId: string; kind: string; originalFileName: string; contentType: string; sizeBytes: number; sha256: string; uploadedByUserId: string; uploadedByUserDisplayName: string | null; createdAtUtc: string };
type LeaveRequest = { id: string; userId: string; userDisplayName: string | null; orgUnitId: string; orgUnitCode: string | null; orgUnitName: string | null; leaveTypeId: string; leaveTypeCode: string | null; leaveTypeName: string | null; leavePolicyVersionId: string | null; startDate: string; endDate: string; dayPortion: string; calculatedDays: number | null; status: string; comment: string | null; balanceAccountId: string | null; balanceReservationOperationId: string | null; submissionOperationId: string | null; createdByUserId: string; createdByUserDisplayName: string | null; createdAtUtc: string; submittedAtUtc: string | null; decidedAtUtc: string | null; cancellationRequestedAtUtc: string | null; cancellationDecidedAtUtc: string | null; revokedAtUtc: string | null; completedAtUtc: string | null; decision: LeaveRequestDecision | null; cancellation: LeaveRequestCancellation | null; revocation: LeaveRequestRevocation | null; documents: LeaveRequestDocument[] };
type SubmitLeaveRequestResult = { request: LeaveRequest; warnings: string[]; wasAlreadySubmitted: boolean };
type AuditEvent = { id: string; occurredAtUtc: string; action: string; resourceType: string; resourceId: string | null; actorUserId: string | null; actorDisplayName: string | null; subjectUserId: string | null; subjectDisplayName: string | null; orgUnitId: string | null; orgUnitName: string | null; correlationId: string | null; metadataJson: string | null };
type AuditEventListResult = { items: AuditEvent[]; page: number; pageSize: number; totalCount: number; hasNextPage: boolean };
type AuditFilters = { action: string; resourceType: string; fromUtc: string; toUtc: string; page: number; pageSize: number };
type Role = { id: string; code: string; name: string; description: string; isActive: boolean };
type UserOrgAssignment = { id: string; userId: string; orgUnitId: string; orgUnitName: string; isPrimary: boolean; effectiveFromUtc: string; effectiveToUtc: string | null };
type RoleScopeAssignment = { id: string; userId: string; roleId: string; roleCode: string; orgUnitId: string; includeDescendants: boolean; effectiveFromUtc: string; effectiveToUtc: string | null };
type ApiErrorPayload = { error?: string; title?: string; detail?: string; code?: string; minimumNoticeDays?: number; calculatedNoticeDays?: number | null; noticeDayCountMode?: string; businessToday?: string | null; startDate?: string | null };

type StatusCardProps = { label: string; status: HealthStatus };
type CatalogKind = 'leave-types' | 'balance-buckets';
const weekdayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

const endpointPrefix = '/api';
const selectedActorStorageKey = 'licenses.devActorId';
const isDevelopment = import.meta.env.DEV;

function developmentHeaders(actorId: string | null): HeadersInit {
  return isDevelopment && actorId ? { 'X-Dev-User-Id': actorId } : {};
}

class ApiError extends Error {
  constructor(message: string, public readonly status: number, public readonly payload: ApiErrorPayload | null) {
    super(message);
  }
}

async function fetchHealth(path: string): Promise<HealthStatus> {
  try {
    const response = await fetch(`${endpointPrefix}${path}`);
    if (!response.ok) return 'Unhealthy';
    const payload = (await response.json()) as HealthResponse;
    return payload.status === 'Healthy' ? 'Healthy' : 'Degraded';
  } catch {
    return 'Unavailable';
  }
}

function formatApiError(payload: ApiErrorPayload, fallback: string): string {
  if (payload.code === 'INSUFFICIENT_MINIMUM_NOTICE' && typeof payload.minimumNoticeDays === 'number') {
    const mode = payload.noticeDayCountMode === 'BUSINESS_DAYS' ? 'business days' : 'calendar days';
    const provided = typeof payload.calculatedNoticeDays === 'number'
      ? ` Your request currently provides ${payload.calculatedNoticeDays}.`
      : '';
    const businessDate = payload.businessToday ? ` Based on business date ${payload.businessToday}.` : '';
    return `This leave type requires at least ${payload.minimumNoticeDays} ${mode} of notice.${provided}${businessDate}`;
  }

  if (payload.code === 'MINIMUM_NOTICE_CALCULATION_FAILED') {
    return 'Minimum notice could not be validated for this leave request. Please review the selected dates and try again.';
  }

  return payload.error ?? payload.detail ?? payload.title ?? fallback;
}

async function readApiFailure(response: Response): Promise<ApiError> {
  const fallback = response.status === 403
    ? 'You do not have authority for this scope or operation.'
    : response.status === 404
      ? 'The requested user, role, or org unit was not found or is outside your scope.'
      : `Request failed: ${response.status}`;
  try {
    const payload = await response.json() as ApiErrorPayload;
    return new ApiError(formatApiError(payload, fallback), response.status, payload);
  } catch {
    return new ApiError(fallback, response.status, null);
  }
}

async function fetchJson<T>(path: string, actorId: string | null = null): Promise<T> {
  const response = await fetch(`${endpointPrefix}${path}`, { headers: developmentHeaders(actorId) });
  if (!response.ok) throw await readApiFailure(response);
  return (await response.json()) as T;
}

async function sendMultipart<T>(path: string, body: FormData, actorId: string | null): Promise<T> {
  const response = await fetch(`${endpointPrefix}${path}`, { method: 'POST', headers: developmentHeaders(actorId), body });
  if (!response.ok) throw await readApiFailure(response);
  return (await response.json()) as T;
}

async function downloadDocument(documentId: string, fileName: string, actorId: string | null): Promise<void> {
  const response = await fetch(`${endpointPrefix}/leave-request-documents/${documentId}/content`, { headers: developmentHeaders(actorId) });
  if (!response.ok) throw await readApiFailure(response);
  const blob = await response.blob();
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

async function sendJson<T>(path: string, method: 'POST' | 'PUT', body: unknown, actorId: string | null): Promise<T> {
  const response = await fetch(`${endpointPrefix}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', ...developmentHeaders(actorId) },
    body: JSON.stringify(body),
  });
  if (!response.ok) throw await readApiFailure(response);
  return (await response.json()) as T;
}

function dateInputToUtc(value: FormDataEntryValue | null): string | null {
  if (!value) return null;
  const text = String(value);
  return text ? new Date(`${text}T00:00:00Z`).toISOString() : null;
}

function toDateInput(value: string | null): string {
  return value ? value.slice(0, 10) : '';
}

function statusBadgeClass(status: string): string {
  return status.toLowerCase().replaceAll('_', '-');
}

function formatTimestamp(value: string | null): string {
  return value ? new Date(value).toLocaleString() : 'not recorded';
}

function orgUnitLabel(units: OrgUnit[], id: string): string {
  const unit = flattenOrgUnits(units).find((candidate) => candidate.id === id);
  return unit ? `${unit.code} - ${unit.name}` : id;
}

function isCurrentAssignment(effectiveFromUtc: string, effectiveToUtc: string | null): boolean {
  const now = Date.now();
  return new Date(effectiveFromUtc).getTime() <= now && (!effectiveToUtc || new Date(effectiveToUtc).getTime() > now);
}

function versionPayloadFromForm(data: FormData) {
  const consumesBalance = data.get('consumesBalance') === 'on';
  return {
    effectiveFrom: data.get('effectiveFrom'),
    effectiveTo: data.get('effectiveTo') || null,
    dayCountMode: data.get('dayCountMode'),
    allowHalfDay: data.get('allowHalfDay') === 'on',
    minimumNoticeDays: data.get('minimumNoticeDays') === '' ? null : Number(data.get('minimumNoticeDays')),
    noticeDayCountMode: data.get('noticeDayCountMode'),
    maximumRequestDays: data.get('maximumRequestDays') === '' ? null : Number(data.get('maximumRequestDays')),
    overlapBehavior: data.get('overlapBehavior'),
    consumesBalance,
    balanceBucketId: consumesBalance ? data.get('balanceBucketId') || null : null,
    workingCalendarId: data.get('workingCalendarId') || null,
  };
}

function weekdayPayload(data: FormData): WorkingCalendarWeekday[] {
  return weekdayNames.map((dayOfWeek) => ({ dayOfWeek, isWorkingDay: data.get(`weekday-${dayOfWeek}`) === 'on' }));
}

function StatusCard({ label, status }: StatusCardProps) {
  return (
    <article className="status-card">
      <span>{label}</span>
      <strong data-status={status.toLowerCase()}>{status}</strong>
    </article>
  );
}

function OrgTree({ units }: { units: OrgUnit[] }) {
  if (units.length === 0) return <p className="muted">No organizational units found.</p>;
  return (
    <ul className="org-tree">
      {units.map((unit) => (
        <li key={unit.id}>
          <span>{unit.name}</span> <code>{unit.code}</code>
          {unit.children && unit.children.length > 0 ? <OrgTree units={unit.children} /> : null}
        </li>
      ))}
    </ul>
  );
}

export function App() {
  const [apiStatus, setApiStatus] = useState<HealthStatus>('Loading');
  const [databaseStatus, setDatabaseStatus] = useState<HealthStatus>('Loading');
  const [orgTree, setOrgTree] = useState<OrgUnit[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [actors, setActors] = useState<DevelopmentActor[]>([]);
  const [selectedActorId, setSelectedActorId] = useState<string | null>(() => localStorage.getItem(selectedActorStorageKey));
  const [leaveTypes, setLeaveTypes] = useState<LeaveType[]>([]);
  const [balanceBuckets, setBalanceBuckets] = useState<BalanceBucket[]>([]);
  const [catalogTab, setCatalogTab] = useState<CatalogKind>('leave-types');
  const [adminError, setAdminError] = useState<string | null>(null);
  const [catalogMessage, setCatalogMessage] = useState<string | null>(null);
  const [policies, setPolicies] = useState<LeavePolicy[]>([]);
  const [versionsByPolicy, setVersionsByPolicy] = useState<Record<string, LeavePolicyVersion[]>>({});
  const [policyMessage, setPolicyMessage] = useState<string | null>(null);
  const [resolvedPolicy, setResolvedPolicy] = useState<ResolvePolicyResult | null>(null);
  const [calendars, setCalendars] = useState<WorkingCalendar[]>([]);
  const [calendarMessage, setCalendarMessage] = useState<string | null>(null);
  const [dayCalculation, setDayCalculation] = useState<DayCalculationResult | null>(null);
  const [myBalances, setMyBalances] = useState<BalanceSnapshot[]>([]);
  const [selectedBalanceUserId, setSelectedBalanceUserId] = useState<string>('');
  const [selectedLedgerBucketId, setSelectedLedgerBucketId] = useState<string>('');
  const [selectedUserBalances, setSelectedUserBalances] = useState<BalanceSnapshot[]>([]);
  const [ledgerEntries, setLedgerEntries] = useState<BalanceLedgerEntry[]>([]);
  const [balanceMessage, setBalanceMessage] = useState<string | null>(null);
  const [isBalanceSubmitting, setIsBalanceSubmitting] = useState(false);
  const [myRequests, setMyRequests] = useState<LeaveRequest[]>([]);
  const [scopedRequests, setScopedRequests] = useState<LeaveRequest[]>([]);
  const [pendingApprovals, setPendingApprovals] = useState<LeaveRequest[]>([]);
  const [pendingCancellations, setPendingCancellations] = useState<LeaveRequest[]>([]);
  const [processingDecisionId, setProcessingDecisionId] = useState<string | null>(null);
  const [requestMessage, setRequestMessage] = useState<string | null>(null);
  const [isRequestMessageError, setIsRequestMessageError] = useState(false);
  const [isRequestSubmitting, setIsRequestSubmitting] = useState(false);
  const [uploadingDocumentRequestId, setUploadingDocumentRequestId] = useState<string | null>(null);
  const [auditEvents, setAuditEvents] = useState<AuditEventListResult | null>(null);
  const [auditFilters, setAuditFilters] = useState<AuditFilters>({ action: '', resourceType: '', fromUtc: '', toUtc: '', page: 1, pageSize: 25 });
  const [auditMessage, setAuditMessage] = useState<string | null>(null);
  const [canReadAudit, setCanReadAudit] = useState(false);
  const [selectedAdminUserId, setSelectedAdminUserId] = useState<string>('');
  const [roles, setRoles] = useState<Role[]>([]);
  const [orgAssignments, setOrgAssignments] = useState<UserOrgAssignment[]>([]);
  const [roleScopes, setRoleScopes] = useState<RoleScopeAssignment[]>([]);
  const [adminMessage, setAdminMessage] = useState<string | null>(null);
  const [canReadUsers, setCanReadUsers] = useState(false);
  const [canManageRoleScopes, setCanManageRoleScopes] = useState(false);

  const selectedActor = useMemo(() => actors.find((actor) => actor.id === selectedActorId) ?? null, [actors, selectedActorId]);
  const selectedAdminUser = useMemo(() => users.find((user) => user.id === selectedAdminUserId) ?? users[0] ?? null, [selectedAdminUserId, users]);
  const selectedAdminUserIsActor = selectedActorId !== null && selectedAdminUser?.id === selectedActorId;
  const visibleOrgUnits = useMemo(() => flattenOrgUnits(orgTree).filter((unit) => unit.isActive), [orgTree]);

  async function loadCatalog(actorId: string | null) {
    const [types, buckets] = await Promise.all([
      fetchJson<LeaveType[]>('/leave-types', actorId),
      fetchJson<BalanceBucket[]>('/balance-buckets', actorId),
    ]);
    setLeaveTypes(types);
    setBalanceBuckets(buckets);
  }

  async function loadPolicies(actorId: string | null) {
    const list = await fetchJson<LeavePolicy[]>('/leave-policies', actorId);
    const versionEntries = await Promise.all(list.map(async (policy) => [policy.id, await fetchJson<LeavePolicyVersion[]>(`/leave-policies/${policy.id}/versions`, actorId)] as const));
    setPolicies(list);
    setVersionsByPolicy(Object.fromEntries(versionEntries));
  }

  async function loadCalendars(actorId: string | null) {
    setCalendars(await fetchJson<WorkingCalendar[]>('/working-calendars', actorId));
  }

  async function loadMyBalances(actorId: string | null) {
    setMyBalances(await fetchJson<BalanceSnapshot[]>('/balances/me', actorId));
  }

  const loadAuditEvents = useCallback(async (actorId: string | null, filters: AuditFilters = auditFilters) => {
    const params = new URLSearchParams({ page: String(filters.page), pageSize: String(filters.pageSize) });
    if (filters.action.trim()) params.set('action', filters.action.trim());
    if (filters.resourceType.trim()) params.set('resourceType', filters.resourceType.trim());
    if (filters.fromUtc) params.set('fromUtc', new Date(filters.fromUtc).toISOString());
    if (filters.toUtc) params.set('toUtc', new Date(filters.toUtc).toISOString());
    setAuditEvents(await fetchJson<AuditEventListResult>(`/audit-events?${params.toString()}`, actorId));
  }, [auditFilters]);

  async function loadLeaveRequests(actorId: string | null) {
    setMyRequests(await fetchJson<LeaveRequest[]>('/leave-requests/me', actorId));
    try {
      setScopedRequests(await fetchJson<LeaveRequest[]>('/leave-requests/scoped', actorId));
    } catch {
      setScopedRequests([]);
    }
    try {
      setPendingApprovals(await fetchJson<LeaveRequest[]>('/leave-requests/pending-approval', actorId));
    } catch {
      setPendingApprovals([]);
    }
    try {
      setPendingCancellations(await fetchJson<LeaveRequest[]>('/leave-requests/pending-cancellation', actorId));
    } catch {
      setPendingCancellations([]);
    }
  }

  async function loadAdminDetails(userId: string, actorId: string | null) {
    if (!userId) {
      setOrgAssignments([]);
      setRoleScopes([]);
      return;
    }
    const assignments = await fetchJson<UserOrgAssignment[]>(`/users/${userId}/org-assignments`, actorId);
    setOrgAssignments(assignments);
    try {
      const [roleList, scopes] = await Promise.all([
        fetchJson<Role[]>('/roles', actorId),
        fetchJson<RoleScopeAssignment[]>(`/users/${userId}/role-scopes`, actorId),
      ]);
      setRoles(roleList);
      setRoleScopes(scopes);
      setCanManageRoleScopes(true);
    } catch {
      setRoles([]);
      setRoleScopes([]);
      setCanManageRoleScopes(false);
    }
  }

  const loadUserBalances = useCallback(async (userId: string, actorId: string | null) => {
    if (!userId) {
      setSelectedUserBalances([]);
      setLedgerEntries([]);
      return;
    }
    const balances = await fetchJson<BalanceSnapshot[]>(`/users/${userId}/balances`, actorId);
    setSelectedUserBalances(balances);
    if (!selectedLedgerBucketId && balances.length > 0) setSelectedLedgerBucketId(balances[0].balanceBucketId);
  }, [selectedLedgerBucketId]);

  useEffect(() => {
    let isMounted = true;

    async function loadStatuses() {
      const [api, database] = await Promise.all([
        fetchHealth('/health'),
        fetchHealth('/health/ready'),
      ]);
      if (isMounted) {
        setApiStatus(api);
        setDatabaseStatus(database);
      }
    }

    async function loadActors() {
      if (!isDevelopment) return;
      try {
        const actorList = await fetchJson<DevelopmentActor[]>('/dev/actors');
        if (!isMounted) return;
        setActors(actorList);
        if (!selectedActorId && actorList.length > 0) {
          setSelectedActorId(actorList[0].id);
          localStorage.setItem(selectedActorStorageKey, actorList[0].id);
        }
      } catch {
        if (isMounted) setActors([]);
      }
    }

    void loadStatuses();
    void loadActors();

    return () => {
      isMounted = false;
    };
  }, [selectedActorId]);

  useEffect(() => {
    let isMounted = true;

    async function loadAdminData() {
      try {
        const [tree, userList] = await Promise.all([
          fetchJson<OrgUnit[]>('/org-units/tree', selectedActorId),
          fetchJson<User[]>('/users', selectedActorId),
        ]);
        if (isMounted) {
          setOrgTree(tree);
          setUsers(userList);
          setCanReadUsers(true);
          const nextAdminUserId = selectedAdminUserId || userList[0]?.id || '';
          if (!selectedAdminUserId && nextAdminUserId) setSelectedAdminUserId(nextAdminUserId);
          if (nextAdminUserId) await loadAdminDetails(nextAdminUserId, selectedActorId);
          setAdminError(null);
        }
      } catch (error) {
        if (isMounted) {
          setOrgTree([]);
          setUsers([]);
          setCanReadUsers(false);
          setSelectedAdminUserId('');
          setAdminError(error instanceof Error ? error.message : 'Unable to load admin data.');
        }
      }
    }

    async function loadCatalogData() {
      try {
        await loadCatalog(selectedActorId);
        if (isMounted) setCatalogMessage(null);
      } catch (error) {
        if (isMounted) {
          setLeaveTypes([]);
          setBalanceBuckets([]);
          setCatalogMessage(error instanceof Error ? error.message : 'Unable to load catalog data.');
        }
      }
    }

    async function loadPolicyData() {
      try {
        await loadPolicies(selectedActorId);
        if (isMounted) setPolicyMessage(null);
      } catch (error) {
        if (isMounted) {
          setPolicies([]);
          setVersionsByPolicy({});
          setPolicyMessage(error instanceof Error ? error.message : 'Unable to load policy data.');
        }
      }
    }

    async function loadCalendarData() {
      try {
        await loadCalendars(selectedActorId);
        if (isMounted) setCalendarMessage(null);
      } catch (error) {
        if (isMounted) {
          setCalendars([]);
          setCalendarMessage(error instanceof Error ? error.message : 'Unable to load working calendars.');
        }
      }
    }

    void loadAdminData();
    void loadCatalogData();
    void loadPolicyData();
    void loadCalendarData();
    void (async () => {
      try {
        await loadMyBalances(selectedActorId);
        await loadLeaveRequests(selectedActorId);
        try {
          await loadAuditEvents(selectedActorId);
          if (isMounted) { setCanReadAudit(true); setAuditMessage(null); }
        } catch (error) {
          if (isMounted) { setCanReadAudit(false); setAuditEvents(null); setAuditMessage(error instanceof Error ? error.message : 'Unable to load audit events.'); }
        }
        if (selectedBalanceUserId) await loadUserBalances(selectedBalanceUserId, selectedActorId);
        if (isMounted) setBalanceMessage(null);
      } catch (error) {
        if (isMounted) {
          setMyBalances([]);
          setSelectedUserBalances([]);
          setLedgerEntries([]);
          setBalanceMessage(error instanceof Error ? error.message : 'Unable to load balances.');
          setMyRequests([]);
          setScopedRequests([]);
          setPendingApprovals([]);
          setPendingCancellations([]);
        }
      }
    })();

    return () => {
      isMounted = false;
    };
  }, [loadAuditEvents, loadUserBalances, selectedActorId, selectedAdminUserId, selectedBalanceUserId]);


  function changeSelectedActor(actorId: string) {
    setSelectedActorId(actorId);
    localStorage.setItem(selectedActorStorageKey, actorId);
  }

  async function refreshAdministration(userId = selectedAdminUser?.id ?? '') {
    const userList = await fetchJson<User[]>('/users', selectedActorId);
    setUsers(userList);
    setCanReadUsers(true);
    if (userId) await loadAdminDetails(userId, selectedActorId);
  }

  async function createInternalUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      const created = await sendJson<User>('/users', 'POST', { displayName: data.get('displayName'), email: data.get('email') }, selectedActorId);
      event.currentTarget.reset();
      setSelectedAdminUserId(created.id);
      await refreshAdministration(created.id);
      setAdminMessage('Internal application user created. No local credentials were created.');
    } catch (error) {
      setAdminMessage(error instanceof Error ? error.message : 'Unable to create user.');
    }
  }

  async function updateInternalUser(event: FormEvent<HTMLFormElement>, userId: string) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<User>(`/users/${userId}`, 'PUT', { displayName: data.get('displayName'), email: data.get('email') }, selectedActorId);
      await refreshAdministration(userId);
      setAdminMessage('User profile updated.');
    } catch (error) {
      setAdminMessage(error instanceof Error ? error.message : 'Unable to update user.');
    }
  }

  async function setUserActive(user: User, isActive: boolean) {
    if (!isActive && !window.confirm(`Deactivate ${user.displayName}? They will remain as an application user record.`)) return;
    try {
      await sendJson<User>(`/users/${user.id}/${isActive ? 'activate' : 'deactivate'}`, 'POST', {}, selectedActorId);
      await refreshAdministration(user.id);
      setAdminMessage(isActive ? 'User activated.' : 'User deactivated.');
    } catch (error) {
      setAdminMessage(error instanceof Error ? error.message : 'Unable to update user state.');
    }
  }

  async function createOrgAssignment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedAdminUser) return;
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<UserOrgAssignment>(`/users/${selectedAdminUser.id}/org-assignments`, 'POST', {
        orgUnitId: data.get('orgUnitId'),
        isPrimary: data.get('isPrimary') === 'on',
        effectiveFromUtc: dateInputToUtc(data.get('effectiveFromUtc')),
        effectiveToUtc: dateInputToUtc(data.get('effectiveToUtc')),
      }, selectedActorId);
      event.currentTarget.reset();
      await refreshAdministration(selectedAdminUser.id);
      setAdminMessage('Organization assignment saved.');
    } catch (error) {
      setAdminMessage(error instanceof Error ? error.message : 'Unable to save organization assignment.');
    }
  }

  async function updateOrgAssignment(event: FormEvent<HTMLFormElement>, assignmentId: string) {
    event.preventDefault();
    if (!selectedAdminUser) return;
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<UserOrgAssignment>(`/users/${selectedAdminUser.id}/org-assignments/${assignmentId}`, 'PUT', {
        orgUnitId: data.get('orgUnitId'),
        isPrimary: data.get('isPrimary') === 'on',
        effectiveFromUtc: dateInputToUtc(data.get('effectiveFromUtc')),
        effectiveToUtc: dateInputToUtc(data.get('effectiveToUtc')),
      }, selectedActorId);
      await refreshAdministration(selectedAdminUser.id);
      setAdminMessage('Organization assignment updated.');
    } catch (error) {
      setAdminMessage(error instanceof Error ? error.message : 'Unable to update organization assignment.');
    }
  }

  async function endOrgAssignment(event: FormEvent<HTMLFormElement>, assignmentId: string) {
    event.preventDefault();
    if (!selectedAdminUser) return;
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<UserOrgAssignment>(`/users/${selectedAdminUser.id}/org-assignments/${assignmentId}/end`, 'POST', {
        effectiveToUtc: dateInputToUtc(data.get('effectiveToUtc')),
      }, selectedActorId);
      await refreshAdministration(selectedAdminUser.id);
      setAdminMessage('Organization assignment ended.');
    } catch (error) {
      setAdminMessage(error instanceof Error ? error.message : 'Unable to end organization assignment.');
    }
  }

  async function assignRoleScope(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedAdminUser || selectedAdminUserIsActor) return;
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<RoleScopeAssignment>(`/users/${selectedAdminUser.id}/role-scopes`, 'POST', {
        roleId: data.get('roleId'),
        orgUnitId: data.get('orgUnitId'),
        includeDescendants: data.get('includeDescendants') === 'on',
        effectiveFromUtc: dateInputToUtc(data.get('effectiveFromUtc')),
        effectiveToUtc: dateInputToUtc(data.get('effectiveToUtc')),
      }, selectedActorId);
      event.currentTarget.reset();
      await refreshAdministration(selectedAdminUser.id);
      setAdminMessage('Role scope assigned.');
    } catch (error) {
      setAdminMessage(error instanceof Error ? error.message : 'Unable to assign role scope.');
    }
  }

  async function updateRoleScope(event: FormEvent<HTMLFormElement>, assignmentId: string) {
    event.preventDefault();
    if (!selectedAdminUser || selectedAdminUserIsActor) return;
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<RoleScopeAssignment>(`/users/${selectedAdminUser.id}/role-scopes/${assignmentId}`, 'PUT', {
        orgUnitId: data.get('orgUnitId'),
        includeDescendants: data.get('includeDescendants') === 'on',
        effectiveFromUtc: dateInputToUtc(data.get('effectiveFromUtc')),
        effectiveToUtc: dateInputToUtc(data.get('effectiveToUtc')),
      }, selectedActorId);
      await refreshAdministration(selectedAdminUser.id);
      setAdminMessage('Role scope updated.');
    } catch (error) {
      setAdminMessage(error instanceof Error ? error.message : 'Unable to update role scope.');
    }
  }

  async function revokeRoleScope(event: FormEvent<HTMLFormElement>, assignmentId: string) {
    event.preventDefault();
    if (!selectedAdminUser || selectedAdminUserIsActor) return;
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<RoleScopeAssignment>(`/users/${selectedAdminUser.id}/role-scopes/${assignmentId}/revoke`, 'POST', {
        effectiveToUtc: dateInputToUtc(data.get('effectiveToUtc')),
      }, selectedActorId);
      await refreshAdministration(selectedAdminUser.id);
      setAdminMessage('Role scope revoked.');
    } catch (error) {
      setAdminMessage(error instanceof Error ? error.message : 'Unable to revoke role scope.');
    }
  }

  async function applyAuditFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const nextFilters: AuditFilters = { action: String(data.get('action') ?? ''), resourceType: String(data.get('resourceType') ?? ''), fromUtc: String(data.get('fromUtc') ?? ''), toUtc: String(data.get('toUtc') ?? ''), page: 1, pageSize: auditFilters.pageSize };
    setAuditFilters(nextFilters);
    try { await loadAuditEvents(selectedActorId, nextFilters); setAuditMessage(null); } catch (error) { setAuditMessage(error instanceof Error ? error.message : 'Unable to load audit events.'); }
  }

  async function changeAuditPage(page: number) {
    const nextFilters = { ...auditFilters, page };
    setAuditFilters(nextFilters);
    try { await loadAuditEvents(selectedActorId, nextFilters); setAuditMessage(null); } catch (error) { setAuditMessage(error instanceof Error ? error.message : 'Unable to load audit events.'); }
  }

  async function createLeaveType(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<LeaveType>('/leave-types', 'POST', {
        code: data.get('code'),
        name: data.get('name'),
        description: data.get('description') || null,
        sortOrder: Number(data.get('sortOrder') || 0),
      }, selectedActorId);
      event.currentTarget.reset();
      await loadCatalog(selectedActorId);
      setCatalogMessage('Leave type saved.');
    } catch (error) {
      setCatalogMessage(error instanceof Error ? error.message : 'Unable to save leave type.');
    }
  }

  async function updateLeaveType(item: LeaveType) {
    try {
      await sendJson<LeaveType>(`/leave-types/${item.id}`, 'PUT', {
        name: item.name,
        description: item.description,
        sortOrder: item.sortOrder,
        isActive: !item.isActive,
      }, selectedActorId);
      await loadCatalog(selectedActorId);
      setCatalogMessage('Leave type updated.');
    } catch (error) {
      setCatalogMessage(error instanceof Error ? error.message : 'Unable to update leave type.');
    }
  }

  async function createBalanceBucket(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<BalanceBucket>('/balance-buckets', 'POST', {
        code: data.get('code'),
        name: data.get('name'),
        description: data.get('description') || null,
        unit: 'DAY',
      }, selectedActorId);
      event.currentTarget.reset();
      await loadCatalog(selectedActorId);
      setCatalogMessage('Balance bucket saved.');
    } catch (error) {
      setCatalogMessage(error instanceof Error ? error.message : 'Unable to save balance bucket.');
    }
  }

  async function updateBalanceBucket(item: BalanceBucket) {
    try {
      await sendJson<BalanceBucket>(`/balance-buckets/${item.id}`, 'PUT', {
        name: item.name,
        description: item.description,
        unit: item.unit,
        isActive: !item.isActive,
      }, selectedActorId);
      await loadCatalog(selectedActorId);
      setCatalogMessage('Balance bucket updated.');
    } catch (error) {
      setCatalogMessage(error instanceof Error ? error.message : 'Unable to update balance bucket.');
    }
  }

  async function createPolicy(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<LeavePolicy>('/leave-policies', 'POST', {
        leaveTypeId: data.get('leaveTypeId'),
        orgUnitId: data.get('orgUnitId') || null,
        appliesToDescendants: data.get('appliesToDescendants') === 'on',
      }, selectedActorId);
      event.currentTarget.reset();
      await loadPolicies(selectedActorId);
      setPolicyMessage('Policy saved.');
    } catch (error) {
      setPolicyMessage(error instanceof Error ? error.message : 'Unable to save policy.');
    }
  }

  async function createVersion(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<LeavePolicyVersion>(`/leave-policies/${data.get('policyId')}/versions`, 'POST', versionPayloadFromForm(data), selectedActorId);
      event.currentTarget.reset();
      await loadPolicies(selectedActorId);
      setPolicyMessage('Draft version saved.');
    } catch (error) {
      setPolicyMessage(error instanceof Error ? error.message : 'Unable to save policy version.');
    }
  }

  async function updateVersion(event: FormEvent<HTMLFormElement>, versionId: string) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<LeavePolicyVersion>(`/leave-policy-versions/${versionId}`, 'PUT', versionPayloadFromForm(data), selectedActorId);
      await loadPolicies(selectedActorId);
      setPolicyMessage('Draft version updated.');
    } catch (error) {
      setPolicyMessage(error instanceof Error ? error.message : 'Unable to update policy version.');
    }
  }

  async function publishVersion(versionId: string) {
    try {
      await sendJson<LeavePolicyVersion>(`/leave-policy-versions/${versionId}/publish`, 'POST', {}, selectedActorId);
      await loadPolicies(selectedActorId);
      setPolicyMessage('Version published.');
    } catch (error) {
      setPolicyMessage(error instanceof Error ? error.message : 'Unable to publish version.');
    }
  }

  async function resolvePolicy(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      setResolvedPolicy(await fetchJson<ResolvePolicyResult>(`/leave-policies/resolve?leaveTypeId=${data.get('leaveTypeId')}&orgUnitId=${data.get('orgUnitId') || ''}&date=${data.get('date')}`, selectedActorId));
    } catch (error) {
      setPolicyMessage(error instanceof Error ? error.message : 'Unable to resolve policy.');
    }
  }

  async function createCalendar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<WorkingCalendar>('/working-calendars', 'POST', {
        code: data.get('code'),
        name: data.get('name'),
        description: data.get('description') || null,
        weekdays: weekdayPayload(data),
      }, selectedActorId);
      event.currentTarget.reset();
      await loadCalendars(selectedActorId);
      setCalendarMessage('Working calendar saved.');
    } catch (error) {
      setCalendarMessage(error instanceof Error ? error.message : 'Unable to save working calendar.');
    }
  }

  async function toggleCalendar(calendar: WorkingCalendar) {
    try {
      await sendJson<WorkingCalendar>(`/working-calendars/${calendar.id}`, 'PUT', {
        name: calendar.name,
        description: calendar.description,
        isActive: !calendar.isActive,
        weekdays: calendar.weekdays,
      }, selectedActorId);
      await loadCalendars(selectedActorId);
      setCalendarMessage('Working calendar updated.');
    } catch (error) {
      setCalendarMessage(error instanceof Error ? error.message : 'Unable to update working calendar.');
    }
  }

  async function createCalendarException(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<WorkingCalendarException>(`/working-calendars/${data.get('workingCalendarId')}/exceptions`, 'POST', {
        date: data.get('date'),
        name: data.get('name'),
        isWorkingDay: data.get('isWorkingDay') === 'on',
      }, selectedActorId);
      event.currentTarget.reset();
      await loadCalendars(selectedActorId);
      setCalendarMessage('Calendar exception saved.');
    } catch (error) {
      setCalendarMessage(error instanceof Error ? error.message : 'Unable to save calendar exception.');
    }
  }

  async function loadLedger(userId: string, balanceBucketId: string) {
    if (!userId || !balanceBucketId) {
      setLedgerEntries([]);
      return;
    }
    setLedgerEntries(await fetchJson<BalanceLedgerEntry[]>(`/users/${userId}/balances/${balanceBucketId}/ledger`, selectedActorId));
  }

  async function selectBalanceUser(userId: string) {
    setSelectedBalanceUserId(userId);
    setSelectedLedgerBucketId('');
    try {
      await loadUserBalances(userId, selectedActorId);
      setBalanceMessage(null);
    } catch (error) {
      setSelectedUserBalances([]);
      setLedgerEntries([]);
      setBalanceMessage(error instanceof Error ? error.message : 'Unable to load user balances.');
    }
  }

  async function submitBalanceOperation(event: FormEvent<HTMLFormElement>, operation: 'grant' | 'adjust' | 'expire') {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const userId = String(data.get('userId') || selectedBalanceUserId);
    const balanceBucketId = String(data.get('balanceBucketId') || selectedLedgerBucketId);
    if (!userId || !balanceBucketId) return;
    setIsBalanceSubmitting(true);
    try {
      await sendJson(`/users/${userId}/balances/${balanceBucketId}/${operation}`, 'POST', { operationId: crypto.randomUUID(), amount: Number(data.get('amount')), reason: data.get('reason') }, selectedActorId);
      event.currentTarget.reset();
      await loadMyBalances(selectedActorId);
      await loadUserBalances(userId, selectedActorId);
      await loadLedger(userId, balanceBucketId);
      setBalanceMessage(`Balance ${operation} posted.`);
    } catch (error) {
      setBalanceMessage(error instanceof Error ? error.message : 'Unable to post balance operation.');
    } finally {
      setIsBalanceSubmitting(false);
    }
  }

  async function calculateDays(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      const params = new URLSearchParams({
        mode: String(data.get('mode')),
        startDate: String(data.get('startDate')),
        endDate: String(data.get('endDate')),
      });
      const workingCalendarId = String(data.get('workingCalendarId') ?? '');
      if (workingCalendarId) params.set('workingCalendarId', workingCalendarId);
      setDayCalculation(await fetchJson<DayCalculationResult>(`/day-calculation?${params}`, selectedActorId));
    } catch (error) {
      setCalendarMessage(error instanceof Error ? error.message : 'Unable to calculate days.');
    }
  }



  async function uploadMedicalCertificate(event: FormEvent<HTMLFormElement>, requestId: string) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const file = data.get('file');
    if (!(file instanceof File) || file.size === 0) {
      setRequestMessage('Choose a PDF, JPEG, or PNG file to upload.');
      return;
    }
    try {
      setUploadingDocumentRequestId(requestId);
      const payload = new FormData();
      payload.append('file', file);
      await sendMultipart<LeaveRequestDocument>(`/leave-requests/${requestId}/documents`, payload, selectedActorId);
      event.currentTarget.reset();
      await loadLeaveRequests(selectedActorId);
      setIsRequestMessageError(false);
      setRequestMessage('Medical certificate uploaded.');
    } catch (error) {
      setIsRequestMessageError(true);
      setRequestMessage(error instanceof Error ? error.message : 'Unable to upload medical certificate.');
    } finally {
      setUploadingDocumentRequestId(null);
    }
  }

  async function downloadLeaveRequestDocument(document: LeaveRequestDocument) {
    try {
      await downloadDocument(document.id, document.originalFileName, selectedActorId);
    } catch (error) {
      setIsRequestMessageError(true);
      setRequestMessage(error instanceof Error ? error.message : 'Unable to download document.');
    }
  }

  async function createLeaveRequest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<LeaveRequest>('/leave-requests', 'POST', { orgUnitId: data.get('orgUnitId'), leaveTypeId: data.get('leaveTypeId'), startDate: data.get('startDate'), endDate: data.get('endDate'), dayPortion: data.get('dayPortion'), comment: data.get('comment') || null }, selectedActorId);
      event.currentTarget.reset();
      await loadLeaveRequests(selectedActorId);
      setIsRequestMessageError(false);
      setRequestMessage('Draft request saved.');
    } catch (error) {
      setIsRequestMessageError(true);
      setRequestMessage(error instanceof Error ? error.message : 'Unable to save leave request.');
    }
  }

  async function updateLeaveRequest(event: FormEvent<HTMLFormElement>, requestId: string) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<LeaveRequest>(`/leave-requests/${requestId}`, 'PUT', { orgUnitId: data.get('orgUnitId'), leaveTypeId: data.get('leaveTypeId'), startDate: data.get('startDate'), endDate: data.get('endDate'), dayPortion: data.get('dayPortion'), comment: data.get('comment') || null }, selectedActorId);
      await loadLeaveRequests(selectedActorId);
      setIsRequestMessageError(false);
      setRequestMessage('Draft request updated.');
    } catch (error) {
      setIsRequestMessageError(true);
      setRequestMessage(error instanceof Error ? error.message : 'Unable to update leave request.');
    }
  }

  async function submitLeaveRequest(requestId: string) {
    setIsRequestSubmitting(true);
    try {
      const result = await sendJson<SubmitLeaveRequestResult>(`/leave-requests/${requestId}/submit`, 'POST', {}, selectedActorId);
      await loadLeaveRequests(selectedActorId);
      await loadMyBalances(selectedActorId);
      setIsRequestMessageError(false);
      setRequestMessage(`Submitted: ${result.request.calculatedDays ?? '-'} days${result.warnings.length ? ` · ${result.warnings.join(' · ')}` : ''}`);
    } catch (error) {
      setIsRequestMessageError(true);
      setRequestMessage(error instanceof Error ? error.message : 'Unable to submit leave request.');
    }
    finally { setIsRequestSubmitting(false); }
  }

  async function requestCancellation(event: FormEvent<HTMLFormElement>, requestId: string) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      setProcessingDecisionId(requestId);
      await sendJson(`/leave-requests/${requestId}/request-cancellation`, 'POST', { operationId: crypto.randomUUID(), reason: data.get('reason') }, selectedActorId);
      await loadLeaveRequests(selectedActorId);
      setIsRequestMessageError(false);
      setRequestMessage('Cancellation requested.');
    } catch (error) {
      setIsRequestMessageError(true);
      setRequestMessage(error instanceof Error ? error.message : 'Unable to request cancellation.');
    } finally { setProcessingDecisionId(null); }
  }

  async function decideCancellation(event: FormEvent<HTMLFormElement>, requestId: string, decision: 'approve' | 'reject') {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      setProcessingDecisionId(requestId);
      await sendJson(`/leave-requests/${requestId}/${decision}-cancellation`, 'POST', { operationId: crypto.randomUUID(), comment: data.get('comment') || null }, selectedActorId);
      await loadLeaveRequests(selectedActorId);
      await loadMyBalances(selectedActorId);
      setIsRequestMessageError(false);
      setRequestMessage(`Cancellation ${decision === 'approve' ? 'approved' : 'rejected'}.`);
    } catch (error) {
      setIsRequestMessageError(true);
      setRequestMessage(error instanceof Error ? error.message : `Unable to ${decision} cancellation.`);
    } finally { setProcessingDecisionId(null); }
  }

  async function revokeLeaveRequest(event: FormEvent<HTMLFormElement>, requestId: string) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      setProcessingDecisionId(requestId);
      await sendJson(`/leave-requests/${requestId}/revoke`, 'POST', { operationId: crypto.randomUUID(), reason: data.get('reason') }, selectedActorId);
      await loadLeaveRequests(selectedActorId);
      await loadMyBalances(selectedActorId);
      setIsRequestMessageError(false);
      setRequestMessage('Request revoked.');
    } catch (error) {
      setIsRequestMessageError(true);
      setRequestMessage(error instanceof Error ? error.message : 'Unable to revoke request.');
    } finally { setProcessingDecisionId(null); }
  }

  async function createLeaveRequestForUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const userId = String(data.get('userId') ?? '');
    try {
      const result = await sendJson<SubmitLeaveRequestResult>(`/users/${userId}/leave-requests`, 'POST', { orgUnitId: data.get('orgUnitId'), leaveTypeId: data.get('leaveTypeId'), startDate: data.get('startDate'), endDate: data.get('endDate'), dayPortion: data.get('dayPortion'), comment: data.get('comment') || null, submissionOperationId: crypto.randomUUID() }, selectedActorId);
      event.currentTarget.reset();
      await loadLeaveRequests(selectedActorId);
      await loadMyBalances(selectedActorId);
      setIsRequestMessageError(false);
      setRequestMessage(`Manual request created as ${result.request.status}.`);
    } catch (error) {
      setIsRequestMessageError(true);
      setRequestMessage(error instanceof Error ? error.message : 'Unable to create request for employee.');
    }
  }

  async function decideLeaveRequest(event: FormEvent<HTMLFormElement>, requestId: string, decision: 'approve' | 'reject') {
    event.preventDefault();
    const operationId = crypto.randomUUID();
    const data = new FormData(event.currentTarget);
    try {
      setProcessingDecisionId(requestId);
      await sendJson(`/leave-requests/${requestId}/${decision}`, 'POST', { operationId, comment: data.get('comment') || null }, selectedActorId);
      await loadLeaveRequests(selectedActorId);
      await loadMyBalances(selectedActorId);
      setIsRequestMessageError(false);
      setRequestMessage(`Request ${decision === 'approve' ? 'approved' : 'rejected'}.`);
    } catch (error) {
      setIsRequestMessageError(true);
      setRequestMessage(error instanceof Error ? error.message : `Unable to ${decision} request.`);
    } finally {
      setProcessingDecisionId(null);
    }
  }

  return (
    <main className="page-shell">
      <section className="hero">
        <p className="eyebrow">Corporate leave platform</p>
        <h1>Leave Management System</h1>
        <p className="summary">
          Production-oriented walking skeleton with backend-enforced authorization and configurable leave catalog resources.
        </p>
      </section>

      <section className="status-grid" aria-label="System health">
        <StatusCard label="API status" status={apiStatus} />
        <StatusCard label="Database status" status={databaseStatus} />
      </section>

      <section className="admin-panel" aria-label="Development authorization demo">
        <div>
          <p className="eyebrow">Development authorization demo</p>
          <h2>Current actor</h2>
          <p className="warning">Temporary Development-only actor selection. Backend permissions still decide access.</p>
          {isDevelopment ? (
            <label className="actor-selector">
              <span>Act as</span>
              <select value={selectedActorId ?? ''} onChange={(event) => changeSelectedActor(event.target.value)}>
                {actors.map((actor) => (
                  <option key={actor.id} value={actor.id}>
                    {actor.displayName} {actor.primaryOrgUnitName ? `- ${actor.primaryOrgUnitName}` : ''}
                  </option>
                ))}
              </select>
            </label>
          ) : null}
          {selectedActor ? <p className="muted">Current actor: {selectedActor.displayName}</p> : null}
        </div>
      </section>

      {canReadUsers ? <section className="admin-panel" aria-label="Administration">
        <div>
          <p className="eyebrow">Administration</p>
          <h2>Users, organizational assignments, and role scopes</h2>
          <p className="muted">These are application user records, not local login accounts. The backend remains authoritative for every mutation.</p>
          {adminMessage ? <p className={adminMessage.includes('authority') || adminMessage.includes('failed') || adminMessage.includes('not found') ? 'error' : 'muted'}>{adminMessage}</p> : null}
          {adminError ? <p className="error">{adminError}</p> : null}
        </div>
        <div className="admin-grid">
          <article className="panel-card">
            <h3>Internal users</h3>
            <div className="catalog-list">
              {users.map((user) => (
                <button type="button" className={`catalog-row selectable-row ${selectedAdminUser?.id === user.id ? 'selected' : ''}`} key={user.id} onClick={() => { setSelectedAdminUserId(user.id); void loadAdminDetails(user.id, selectedActorId); }}>
                  <span>{user.displayName}</span>
                  <span>{user.email}</span>
                  <span className={`badge ${user.isActive ? 'published' : 'draft'}`}>{user.isActive ? 'Active' : 'Inactive'}</span>
                  <span>{user.primaryOrgUnit?.name ?? 'No primary unit'}</span>
                </button>
              ))}
            </div>
          </article>
          <form className="panel-card catalog-form" onSubmit={createInternalUser}>
            <h3>Create internal user</h3>
            <p className="muted">Creates the application record only. No password, token, local login, or external identity is created.</p>
            <input name="displayName" placeholder="Display name" required />
            <input name="email" type="email" placeholder="email@example.com" required />
            <button type="submit">Create user</button>
          </form>
        </div>

        {selectedAdminUser ? <div className="admin-grid">
          <form className="panel-card catalog-form" onSubmit={(event) => updateInternalUser(event, selectedAdminUser.id)}>
            <h3>User profile</h3>
            <input name="displayName" placeholder="Display name" required defaultValue={selectedAdminUser.displayName} />
            <input name="email" type="email" placeholder="email@example.com" required defaultValue={selectedAdminUser.email} />
            <p className="muted">External identity and credentials are intentionally not editable here.</p>
            <button type="submit">Update profile</button>
            {selectedAdminUser.isActive
              ? <button type="button" onClick={() => void setUserActive(selectedAdminUser, false)}>Deactivate</button>
              : <button type="button" onClick={() => void setUserActive(selectedAdminUser, true)}>Activate</button>}
          </form>
          <form className="panel-card catalog-form" onSubmit={createOrgAssignment}>
            <h3>Create organization assignment</h3>
            <select name="orgUnitId" required><option value="">Select org unit</option>{visibleOrgUnits.map((unit) => <option key={unit.id} value={unit.id}>{unit.code} - {unit.name}</option>)}</select>
            <label><input name="isPrimary" type="checkbox" /> Primary assignment</label>
            <input name="effectiveFromUtc" type="date" required />
            <input name="effectiveToUtc" type="date" />
            <button type="submit">Create assignment</button>
          </form>
        </div> : null}

        {selectedAdminUser ? <article className="panel-card">
          <h3>Organization assignments for {selectedAdminUser.displayName}</h3>
          {orgAssignments.length === 0 ? <p className="muted">No assignments visible.</p> : null}
          <div className="catalog-list">
            {orgAssignments.map((assignment) => (
              <details className="version-row" key={assignment.id}>
                <summary><strong>{assignment.orgUnitName}</strong> <span className={`badge ${isCurrentAssignment(assignment.effectiveFromUtc, assignment.effectiveToUtc) ? 'published' : 'draft'}`}>{isCurrentAssignment(assignment.effectiveFromUtc, assignment.effectiveToUtc) ? 'Current' : 'Historical'}</span> <span>{assignment.isPrimary ? 'Primary' : 'Secondary'}</span> <span>{toDateInput(assignment.effectiveFromUtc)} to {toDateInput(assignment.effectiveToUtc) || 'open'}</span></summary>
                <form className="catalog-form inline-form" onSubmit={(event) => updateOrgAssignment(event, assignment.id)}>
                  <select name="orgUnitId" required defaultValue={assignment.orgUnitId}>{visibleOrgUnits.map((unit) => <option key={unit.id} value={unit.id}>{unit.code} - {unit.name}</option>)}</select>
                  <label><input name="isPrimary" type="checkbox" defaultChecked={assignment.isPrimary} /> Primary assignment</label>
                  <input name="effectiveFromUtc" type="date" required defaultValue={toDateInput(assignment.effectiveFromUtc)} />
                  <input name="effectiveToUtc" type="date" defaultValue={toDateInput(assignment.effectiveToUtc)} />
                  <button type="submit">Update assignment</button>
                </form>
                <form className="catalog-form inline-form" onSubmit={(event) => endOrgAssignment(event, assignment.id)}>
                  <input name="effectiveToUtc" type="date" required defaultValue={toDateInput(new Date().toISOString())} />
                  <button type="submit">End assignment</button>
                </form>
              </details>
            ))}
          </div>
        </article> : null}

        {selectedAdminUser && canManageRoleScopes ? <section className="panel-card">
          <h3>Role scopes for {selectedAdminUser.displayName}</h3>
          {selectedAdminUserIsActor ? <p className="warning">Self role-scope mutation is disabled here because the backend forbids self-escalation and self-lockout.</p> : null}
          <form className="catalog-form" onSubmit={assignRoleScope}>
            <h4>Assign existing role</h4>
            <select name="roleId" required disabled={selectedAdminUserIsActor}><option value="">Select role</option>{roles.filter((role) => role.isActive).map((role) => <option key={role.id} value={role.id}>{role.code} - {role.name}</option>)}</select>
            <select name="orgUnitId" required disabled={selectedAdminUserIsActor}><option value="">Select org unit</option>{visibleOrgUnits.map((unit) => <option key={unit.id} value={unit.id}>{unit.code} - {unit.name}</option>)}</select>
            <label><input name="includeDescendants" type="checkbox" disabled={selectedAdminUserIsActor} /> Include descendants</label>
            <input name="effectiveFromUtc" type="date" required disabled={selectedAdminUserIsActor} />
            <input name="effectiveToUtc" type="date" disabled={selectedAdminUserIsActor} />
            <button type="submit" disabled={selectedAdminUserIsActor}>Assign role scope</button>
          </form>
          {roleScopes.length === 0 ? <p className="muted">No role scopes visible.</p> : null}
          <div className="catalog-list inline-form">
            {roleScopes.map((scope) => (
              <details className="version-row" key={scope.id}>
                <summary><strong>{scope.roleCode}</strong> <span>{orgUnitLabel(orgTree, scope.orgUnitId)}</span> <span>{scope.includeDescendants ? 'Includes descendants' : 'Exact unit'}</span> <span className={`badge ${isCurrentAssignment(scope.effectiveFromUtc, scope.effectiveToUtc) ? 'published' : 'draft'}`}>{isCurrentAssignment(scope.effectiveFromUtc, scope.effectiveToUtc) ? 'Current' : 'Historical'}</span></summary>
                <form className="catalog-form inline-form" onSubmit={(event) => updateRoleScope(event, scope.id)}>
                  <select name="orgUnitId" required defaultValue={scope.orgUnitId} disabled={selectedAdminUserIsActor}>{visibleOrgUnits.map((unit) => <option key={unit.id} value={unit.id}>{unit.code} - {unit.name}</option>)}</select>
                  <label><input name="includeDescendants" type="checkbox" defaultChecked={scope.includeDescendants} disabled={selectedAdminUserIsActor} /> Include descendants</label>
                  <input name="effectiveFromUtc" type="date" required defaultValue={toDateInput(scope.effectiveFromUtc)} disabled={selectedAdminUserIsActor} />
                  <input name="effectiveToUtc" type="date" defaultValue={toDateInput(scope.effectiveToUtc)} disabled={selectedAdminUserIsActor} />
                  <button type="submit" disabled={selectedAdminUserIsActor}>Update role scope</button>
                </form>
                <form className="catalog-form inline-form" onSubmit={(event) => revokeRoleScope(event, scope.id)}>
                  <input name="effectiveToUtc" type="date" required defaultValue={toDateInput(new Date().toISOString())} disabled={selectedAdminUserIsActor} />
                  <button type="submit" disabled={selectedAdminUserIsActor}>Revoke role scope</button>
                </form>
              </details>
            ))}
          </div>
        </section> : null}
      </section> : null}

      <section className="admin-panel" aria-label="Leave catalog admin">
        <div>
          <p className="eyebrow">Leave catalog</p>
          <h2>Leave Types and Balance Buckets</h2>
          <p className="muted">Catalog records are independent. Policies will later decide whether a leave type consumes a bucket.</p>
          {catalogMessage ? <p className={catalogMessage.includes('failed') || catalogMessage.includes('403') || catalogMessage.includes('401') ? 'error' : 'muted'}>{catalogMessage}</p> : null}
        </div>
        <div className="tabs" role="tablist" aria-label="Catalog sections">
          <button type="button" className={catalogTab === 'leave-types' ? 'active' : ''} onClick={() => setCatalogTab('leave-types')}>Leave Types</button>
          <button type="button" className={catalogTab === 'balance-buckets' ? 'active' : ''} onClick={() => setCatalogTab('balance-buckets')}>Balance Buckets</button>
        </div>
        {catalogTab === 'leave-types' ? (
          <CatalogTable title="Leave Types" rows={leaveTypes.map((item) => [item.code, item.name, item.isActive ? 'Active' : 'Inactive'])} onToggle={leaveTypes.map((item) => () => updateLeaveType(item))} />
        ) : (
          <CatalogTable title="Balance Buckets" rows={balanceBuckets.map((item) => [item.code, item.name, item.unit, item.isActive ? 'Active' : 'Inactive'])} onToggle={balanceBuckets.map((item) => () => updateBalanceBucket(item))} />
        )}
        <div className="admin-grid">
          <CatalogForm title="Create leave type" codePlaceholder="VACATION" namePlaceholder="Vacation" onSubmit={createLeaveType} includeSortOrder />
          <CatalogForm title="Create balance bucket" codePlaceholder="VACATION_DAYS" namePlaceholder="Vacation Days" onSubmit={createBalanceBucket} unit="DAY" />
        </div>
      </section>

      <section className="admin-panel" aria-label="Working calendar admin">
        <div>
          <p className="eyebrow">Working calendars</p>
          <h2>Weekdays, holidays, and exceptions</h2>
          <p className="muted">Weekends are not hardcoded: each calendar defines its own weekly working days, and dated exceptions override them.</p>
          {calendarMessage ? <p className={calendarMessage.includes('failed') || calendarMessage.includes('403') || calendarMessage.includes('401') ? 'error' : 'muted'}>{calendarMessage}</p> : null}
        </div>
        <div className="admin-grid">
          <form className="panel-card catalog-form" onSubmit={createCalendar}>
            <h3>Create working calendar</h3>
            <input name="code" placeholder="STANDARD_UY_DEV" required />
            <input name="name" placeholder="Standard development calendar" required />
            <input name="description" placeholder="Description" />
            <div className="checkbox-grid">
              {weekdayNames.map((day) => (
                <label key={day}><input name={`weekday-${day}`} type="checkbox" defaultChecked={!['Saturday', 'Sunday'].includes(day)} /> {day}</label>
              ))}
            </div>
            <button type="submit">Create calendar</button>
          </form>
          <form className="panel-card catalog-form" onSubmit={createCalendarException}>
            <h3>Create dated exception</h3>
            <select name="workingCalendarId" required>
              <option value="">Select calendar</option>
              {calendars.map((calendar) => <option key={calendar.id} value={calendar.id}>{calendar.code}</option>)}
            </select>
            <input name="date" type="date" required />
            <input name="name" placeholder="Sample non-working weekday" required />
            <label><input name="isWorkingDay" type="checkbox" /> Exceptional working day</label>
            <button type="submit">Create exception</button>
          </form>
        </div>
        <article className="panel-card">
          <h3>Configured calendars</h3>
          <div className="catalog-list">
            {calendars.map((calendar) => (
              <div className="policy-card" key={calendar.id}>
                <strong>{calendar.code} - {calendar.name}</strong>
                <span>{calendar.isActive ? 'Active' : 'Inactive'} · {calendar.description ?? 'No description'}</span>
                <span>{calendar.weekdays.map((day) => `${day.dayOfWeek.slice(0, 3)}:${day.isWorkingDay ? 'work' : 'off'}`).join(' · ')}</span>
                <div className="version-row">
                  <span>Exceptions:</span>
                  <span>{calendar.exceptions.length === 0 ? 'none' : calendar.exceptions.map((item) => `${item.date} ${item.name} (${item.isWorkingDay ? 'working' : 'non-working'})`).join(' · ')}</span>
                </div>
                <button type="button" onClick={() => toggleCalendar(calendar)}>Toggle active</button>
              </div>
            ))}
          </div>
        </article>
        <form className="panel-card catalog-form" onSubmit={calculateDays}>
          <h3>Day calculation inspector</h3>
          <select name="mode" defaultValue="BUSINESS_DAYS"><option>BUSINESS_DAYS</option><option>CALENDAR_DAYS</option></select>
          <select name="workingCalendarId">
            <option value="">No working calendar</option>
            {calendars.map((calendar) => <option key={calendar.id} value={calendar.id}>{calendar.code}</option>)}
          </select>
          <input name="startDate" type="date" required defaultValue="2026-08-10" />
          <input name="endDate" type="date" required defaultValue="2026-08-14" />
          <button type="submit">Calculate</button>
          {dayCalculation ? <p className="muted">Calculated days: <strong>{dayCalculation.calculatedDays}</strong></p> : null}
        </form>
      </section>



      <section className="admin-panel" aria-label="Leave requests">
        <div>
          <p className="eyebrow">Leave requests</p>
          <h2>My requests</h2>
          <p className="muted">Drafts have no ledger side effects. Submission freezes policy version and calculated quantity.</p>
          {requestMessage ? <p className={isRequestMessageError ? 'error' : 'muted'}>{requestMessage}</p> : null}
        </div>
        <form className="panel-card catalog-form" onSubmit={createLeaveRequest}>
          <h3>Create draft</h3>
          <select name="leaveTypeId" required><option value="">Select leave type</option>{leaveTypes.filter((type) => type.isActive).map((type) => <option key={type.id} value={type.id}>{type.code} - {type.name}</option>)}</select>
          <select name="orgUnitId" required><option value="">Select org unit</option>{flattenOrgUnits(orgTree).filter((unit) => unit.isActive).map((unit) => <option key={unit.id} value={unit.id}>{unit.code} - {unit.name}</option>)}</select>
          <input name="startDate" type="date" required />
          <input name="endDate" type="date" required />
          <select name="dayPortion" defaultValue="FULL_DAY"><option>FULL_DAY</option><option>HALF_DAY</option></select>
          <input name="comment" placeholder="Comment" />
          <button type="submit">Create draft</button>
        </form>
        <form className="panel-card catalog-form" onSubmit={createLeaveRequestForUser}>
          <h3>Create request for employee</h3>
          <select name="userId" required><option value="">Select employee</option>{users.filter((user) => user.isActive).map((user) => <option key={user.id} value={user.id}>{user.displayName}</option>)}</select>
          <select name="leaveTypeId" required><option value="">Select leave type</option>{leaveTypes.filter((type) => type.isActive).map((type) => <option key={type.id} value={type.id}>{type.code} - {type.name}</option>)}</select>
          <select name="orgUnitId" required><option value="">Select org unit</option>{flattenOrgUnits(orgTree).filter((unit) => unit.isActive).map((unit) => <option key={unit.id} value={unit.id}>{unit.code} - {unit.name}</option>)}</select>
          <input name="startDate" type="date" required />
          <input name="endDate" type="date" required />
          <select name="dayPortion" defaultValue="FULL_DAY"><option>FULL_DAY</option><option>HALF_DAY</option></select>
          <input name="comment" placeholder="Comment" />
          <button type="submit">Create and submit</button>
        </form>
        <article className="panel-card"><h3>My requests</h3>{myRequests.length === 0 ? <p className="muted">No leave requests yet.</p> : null}<div className="catalog-list">
          {myRequests.map((request) => <div className="policy-card" key={request.id}>
            <strong>{request.leaveTypeCode ?? request.leaveTypeId} <span className={`badge ${statusBadgeClass(request.status)}`}>{request.status}</span></strong>
            <span>{request.orgUnitCode ?? request.orgUnitId} · {request.startDate} → {request.endDate} · {request.dayPortion}</span>
            <span>Leave type: {request.leaveTypeName ?? request.leaveTypeCode ?? request.leaveTypeId} · Calculated days: {request.calculatedDays ?? 'pending'} · Policy version: {request.leavePolicyVersionId ?? 'not frozen'}</span>
            {request.status === 'COMPLETED' ? <span>Completed: {formatTimestamp(request.completedAtUtc)}</span> : null}
            <span>{request.comment ?? 'No comment'}</span>
            <span>Created by: {request.createdByUserDisplayName ?? request.createdByUserId}</span>
            {request.decision ? <span>Decision: {request.decision.decision} by {request.decision.decidedByUserDisplayName ?? request.decision.decidedByUserId} on {new Date(request.decision.createdAtUtc).toLocaleString()}{request.decision.comment ? ` · ${request.decision.comment}` : ''}</span> : null}
            {request.cancellation ? <span>Cancellation: {request.cancellation.reason} · {request.cancellation.decision ? `${request.cancellation.decision}${request.cancellation.decisionComment ? ` · ${request.cancellation.decisionComment}` : ''}` : 'pending'}</span> : null}
            {request.revocation ? <span>Revoked: {request.revocation.reason} by {request.revocation.revokedByUserDisplayName ?? request.revocation.revokedByUserId}</span> : null}

            <div className="document-list">
              <strong>Documents</strong>
              {request.documents.length === 0 ? <span className="muted">No documents attached.</span> : request.documents.map((document) => (
                <button type="button" className="catalog-row" key={document.id} onClick={() => void downloadLeaveRequestDocument(document)}>
                  <span>{document.kind}</span><span>{document.originalFileName}</span><span>{Math.ceil(document.sizeBytes / 1024)} KB</span>
                </button>
              ))}
            </div>
            {request.status !== 'COMPLETED' ? <form className="catalog-form inline-form" onSubmit={(event) => uploadMedicalCertificate(event, request.id)}>
              <input name="file" type="file" accept="application/pdf,image/jpeg,image/png" required />
              <button type="submit" disabled={uploadingDocumentRequestId === request.id}>Upload medical certificate</button>
            </form> : null}

            {request.status === 'DRAFT' ? <details><summary>Edit draft</summary><form className="catalog-form inline-form" onSubmit={(event) => updateLeaveRequest(event, request.id)}>
              <select name="leaveTypeId" required defaultValue={request.leaveTypeId}>{leaveTypes.map((type) => <option key={type.id} value={type.id}>{type.code}</option>)}</select>
              <select name="orgUnitId" required defaultValue={request.orgUnitId}>{flattenOrgUnits(orgTree).map((unit) => <option key={unit.id} value={unit.id}>{unit.code}</option>)}</select>
              <input name="startDate" type="date" required defaultValue={request.startDate} />
              <input name="endDate" type="date" required defaultValue={request.endDate} />
              <select name="dayPortion" defaultValue={request.dayPortion}><option>FULL_DAY</option><option>HALF_DAY</option></select>
              <input name="comment" placeholder="Comment" defaultValue={request.comment ?? ''} />
              <button type="submit">Update draft</button>
            </form></details> : null}
            {request.status === 'DRAFT' ? <button type="button" disabled={isRequestSubmitting} onClick={() => submitLeaveRequest(request.id)}>Submit</button> : null}
            {request.status === 'APPROVED' ? <form className="catalog-form inline-form" onSubmit={(event) => requestCancellation(event, request.id)}><input name="reason" placeholder="Cancellation reason" required /><button type="submit" disabled={processingDecisionId === request.id}>Request cancellation</button></form> : null}
          </div>)}
        </div></article>
        <article className="panel-card"><h3>Pending approvals</h3>{pendingApprovals.length === 0 ? <p className="muted">No pending approvals visible for this actor.</p> : null}<div className="catalog-list">
          {pendingApprovals.map((request) => <div className="version-row" key={request.id}>
            <strong>{request.userDisplayName ?? request.userId} · {request.leaveTypeCode ?? request.leaveTypeName ?? request.leaveTypeId}</strong>
            <span>{request.orgUnitCode ?? request.orgUnitName ?? request.orgUnitId} · {request.startDate} → {request.endDate} · {request.calculatedDays ?? 'pending'} days</span>
            <span>Submitted: {request.submittedAtUtc ? new Date(request.submittedAtUtc).toLocaleString() : 'not submitted'}</span>
            <span>{request.comment ?? 'No employee comment'}</span>
            {request.userId !== selectedActorId ? <div className="decision-actions">
              <form className="catalog-form inline-form" onSubmit={(event) => decideLeaveRequest(event, request.id, 'approve')}>
                <input name="comment" placeholder="Optional approval comment" />
                <button type="submit" disabled={processingDecisionId === request.id}>Approve</button>
              </form>
              <form className="catalog-form inline-form" onSubmit={(event) => decideLeaveRequest(event, request.id, 'reject')}>
                <input name="comment" placeholder="Required rejection reason" required />
                <button type="submit" disabled={processingDecisionId === request.id}>Reject</button>
              </form>
            </div> : <p className="muted">Own request: decision controls hidden.</p>}
          </div>)}
        </div></article>
        <article className="panel-card"><h3>Pending cancellations</h3>{pendingCancellations.length === 0 ? <p className="muted">No pending cancellations visible for this actor.</p> : null}<div className="catalog-list">
          {pendingCancellations.map((request) => <div className="version-row" key={request.id}>
            <strong>{request.userDisplayName ?? request.userId} · {request.leaveTypeCode ?? request.leaveTypeName ?? request.leaveTypeId}</strong>
            <span>{request.orgUnitCode ?? request.orgUnitName ?? request.orgUnitId} · {request.startDate} → {request.endDate} · {request.calculatedDays ?? 'pending'} days</span>
            <span>Reason: {request.cancellation?.reason ?? 'No reason'}</span>
            {request.userId !== selectedActorId ? <div className="decision-actions">
              <form className="catalog-form inline-form" onSubmit={(event) => decideCancellation(event, request.id, 'approve')}>
                <input name="comment" placeholder="Optional approval comment" />
                <button type="submit" disabled={processingDecisionId === request.id}>Approve cancellation</button>
              </form>
              <form className="catalog-form inline-form" onSubmit={(event) => decideCancellation(event, request.id, 'reject')}>
                <input name="comment" placeholder="Required rejection reason" required />
                <button type="submit" disabled={processingDecisionId === request.id}>Reject cancellation</button>
              </form>
            </div> : <p className="muted">Own request: cancellation decision controls hidden.</p>}
          </div>)}
        </div></article>
        {scopedRequests.length > 0 ? <article className="panel-card"><h3>Scoped request inspector</h3><div className="catalog-list">{scopedRequests.map((request) => <div className="version-row" key={request.id}><span>{request.userDisplayName ?? request.userId}</span><span>{request.leaveTypeCode}</span><span>{request.orgUnitCode}</span><span>{request.startDate} → {request.endDate}</span><span className={`badge ${statusBadgeClass(request.status)}`}>{request.status}</span><span>Calculated days: {request.calculatedDays ?? 'pending'} · Leave type: {request.leaveTypeName ?? request.leaveTypeCode ?? request.leaveTypeId}</span>{request.status === 'COMPLETED' ? <span>Completed: {formatTimestamp(request.completedAtUtc)}</span> : null}{request.decision ? <span>{request.decision.decision} · {request.decision.comment ?? 'No decision comment'}</span> : null}{request.cancellation ? <span>Cancellation: {request.cancellation.reason}</span> : null}{request.revocation ? <span>Revocation: {request.revocation.reason}</span> : null}{request.documents.length > 0 ? <span>Documents: {request.documents.map((document) => <button type="button" key={document.id} onClick={() => void downloadLeaveRequestDocument(document)}>{document.originalFileName}</button>)}</span> : <span className="muted">No documents</span>}{request.status === 'APPROVED' && request.userId !== selectedActorId ? <form className="catalog-form inline-form" onSubmit={(event) => revokeLeaveRequest(event, request.id)}><input name="reason" placeholder="Revocation reason" required /><button type="submit" disabled={processingDecisionId === request.id}>Revoke</button></form> : null}</div>)}</div></article> : null}
      </section>

      <section className="admin-panel" aria-label="Balance ledger">
        <div>
          <p className="eyebrow">Balances</p>
          <h2>My balances and scoped balance ledger</h2>
          <p className="muted">Balances are derived from immutable ledger entries. HR actions expose grant, adjustment, and expiry only.</p>
          {balanceMessage ? <p className={balanceMessage.includes('failed') || balanceMessage.includes('403') || balanceMessage.includes('401') ? 'error' : 'muted'}>{balanceMessage}</p> : null}
        </div>
        <article className="panel-card">
          <h3>My balances</h3>
          {myBalances.length === 0 ? <p className="muted">No balance accounts visible.</p> : null}
          <div className="balance-grid">
            {myBalances.map((balance) => (
              <div className="balance-card" key={balance.balanceBucketId}>
                <strong>{balance.balanceBucketName}</strong>
                <code>{balance.balanceBucketCode}</code>
                <span>Available: {balance.available} {balance.unit}</span>
                <span>Reserved: {balance.reserved} {balance.unit}</span>
              </div>
            ))}
          </div>
        </article>
        <div className="admin-grid">
          <article className="panel-card catalog-form">
            <h3>User balance inspection</h3>
            <select value={selectedBalanceUserId} onChange={(event) => selectBalanceUser(event.target.value)}>
              <option value="">Select user in scope</option>
              {users.map((user) => <option key={user.id} value={user.id}>{user.displayName}</option>)}
            </select>
            <div className="catalog-list">
              {selectedUserBalances.map((balance) => (
                <button type="button" className="catalog-row" key={balance.balanceBucketId} onClick={() => { setSelectedLedgerBucketId(balance.balanceBucketId); void loadLedger(selectedBalanceUserId, balance.balanceBucketId); }}>
                  <span>{balance.balanceBucketName}</span>
                  <span>Available {balance.available}</span>
                  <span>Reserved {balance.reserved}</span>
                </button>
              ))}
            </div>
          </article>
          <article className="panel-card">
            <h3>Ledger history</h3>
            {ledgerEntries.length === 0 ? <p className="muted">Select a user balance to view ledger entries.</p> : null}
            <div className="catalog-list">
              {ledgerEntries.map((entry) => (
                <div className="version-row" key={entry.id}>
                  <span className="badge published">{entry.type}</span>
                  <span>Available {entry.availableDelta}, reserved {entry.reservedDelta}</span>
                  <span>{entry.reason}</span>
                  <span>{new Date(entry.createdAtUtc).toLocaleString()} · {entry.createdByUserName ?? 'System'}</span>
                </div>
              ))}
            </div>
          </article>
        </div>
        <div className="admin-grid">
          {(['grant', 'adjust', 'expire'] as const).map((operation) => (
            <form className="panel-card catalog-form" key={operation} onSubmit={(event) => submitBalanceOperation(event, operation)}>
              <h3>{operation.toUpperCase()}</h3>
              <select name="userId" required defaultValue={selectedBalanceUserId}>
                <option value="">Select user</option>
                {users.map((user) => <option key={user.id} value={user.id}>{user.displayName}</option>)}
              </select>
              <select name="balanceBucketId" required defaultValue={selectedLedgerBucketId}>
                <option value="">Select bucket</option>
                {balanceBuckets.map((bucket) => <option key={bucket.id} value={bucket.id}>{bucket.code}</option>)}
              </select>
              <input name="amount" type="number" step="0.5" placeholder={operation === 'adjust' ? 'Signed amount' : 'Amount'} required />
              <input name="reason" placeholder="Mandatory reason" required />
              <button type="submit" disabled={isBalanceSubmitting}>Post {operation}</button>
            </form>
          ))}
        </div>
      </section>

      <section className="admin-panel" aria-label="Leave policy admin">
        <div>
          <p className="eyebrow">Leave policies</p>
          <h2>Policy configuration and versioning</h2>
          <p className="muted">Published versions are immutable. Future leave requests must store the exact policy version used.</p>
          {policyMessage ? <p className={policyMessage.includes('failed') || policyMessage.includes('403') || policyMessage.includes('401') ? 'error' : 'muted'}>{policyMessage}</p> : null}
        </div>
        <div className="admin-grid">
          <form className="panel-card catalog-form" onSubmit={createPolicy}>
            <h3>Create policy</h3>
            <select name="leaveTypeId" required>
              <option value="">Select leave type</option>
              {leaveTypes.map((type) => <option key={type.id} value={type.id}>{type.code} - {type.name}</option>)}
            </select>
            <select name="orgUnitId">
              <option value="">Company-wide</option>
              {flattenOrgUnits(orgTree).map((unit) => <option key={unit.id} value={unit.id}>{unit.code} - {unit.name}</option>)}
            </select>
            <label><input name="appliesToDescendants" type="checkbox" /> Applies to descendants</label>
            <button type="submit">Create policy</button>
          </form>
          <form className="panel-card catalog-form" onSubmit={createVersion}>
            <h3>Create draft version</h3>
            <select name="policyId" required>
              <option value="">Select policy</option>
              {policies.map((policy) => <option key={policy.id} value={policy.id}>{policy.leaveTypeCode} - {policy.orgUnitCode ?? 'COMPANY'}</option>)}
            </select>
            <input name="effectiveFrom" type="date" required />
            <input name="effectiveTo" type="date" />
            <select name="dayCountMode" defaultValue="BUSINESS_DAYS"><option>BUSINESS_DAYS</option><option>CALENDAR_DAYS</option></select>
            <label><input name="allowHalfDay" type="checkbox" /> Allow half day</label>
            <input name="minimumNoticeDays" type="number" min="0" placeholder="Minimum notice days" />
            <select name="noticeDayCountMode" defaultValue="CALENDAR_DAYS"><option>BUSINESS_DAYS</option><option>CALENDAR_DAYS</option></select>
            <select name="workingCalendarId">
              <option value="">No working calendar</option>
              {calendars.filter((calendar) => calendar.isActive).map((calendar) => <option key={calendar.id} value={calendar.id}>{calendar.code}</option>)}
            </select>
            <input name="maximumRequestDays" type="number" min="0.5" step="0.5" placeholder="Maximum request days" />
            <select name="overlapBehavior" defaultValue="BLOCK"><option>BLOCK</option><option>WARN</option><option>ALLOW</option></select>
            <label><input name="consumesBalance" type="checkbox" /> Consumes balance</label>
            <select name="balanceBucketId">
              <option value="">No bucket</option>
              {balanceBuckets.map((bucket) => <option key={bucket.id} value={bucket.id}>{bucket.code}</option>)}
            </select>
            <button type="submit">Create draft</button>
          </form>
        </div>
        <article className="panel-card">
          <h3>Configured policies</h3>
          <div className="catalog-list">
            {policies.map((policy) => (
              <div className="policy-card" key={policy.id}>
                <strong>{policy.leaveTypeCode} - {policy.orgUnitCode ?? 'COMPANY'}</strong>
                <span>{policy.orgUnitId ? `Override${policy.appliesToDescendants ? ' with descendants' : ''}` : 'Company-wide'} · {policy.isActive ? 'Active' : 'Inactive'}</span>
                {(versionsByPolicy[policy.id] ?? []).map((version) => (
                  <div className="version-row" key={version.id}>
                    <span className={`badge ${version.status.toLowerCase()}`}>{version.status}</span>
                    <span>v{version.versionNumber}: {version.effectiveFrom} → {version.effectiveTo ?? 'open'}</span>
                    <span>{version.dayCountMode}, calendar: {version.workingCalendarCode ?? 'none'}, half day: {version.allowHalfDay ? 'yes' : 'no'}, notice: {version.minimumNoticeDays ?? 'none'} {version.noticeDayCountMode}, max: {version.maximumRequestDays ?? 'none'}, overlap: {version.overlapBehavior}, balance: {version.consumesBalance ? version.balanceBucketCode : 'no'}</span>
                    {version.status === 'DRAFT' ? (
                      <>
                        <details>
                          <summary>Edit draft</summary>
                          <form className="catalog-form inline-form" onSubmit={(event) => updateVersion(event, version.id)}>
                            <input name="effectiveFrom" type="date" required defaultValue={version.effectiveFrom} />
                            <input name="effectiveTo" type="date" defaultValue={version.effectiveTo ?? ''} />
                            <select name="dayCountMode" defaultValue={version.dayCountMode}><option>BUSINESS_DAYS</option><option>CALENDAR_DAYS</option></select>
                            <label><input name="allowHalfDay" type="checkbox" defaultChecked={version.allowHalfDay} /> Allow half day</label>
                            <input name="minimumNoticeDays" type="number" min="0" placeholder="Minimum notice days" defaultValue={version.minimumNoticeDays ?? ''} />
                            <select name="noticeDayCountMode" defaultValue={version.noticeDayCountMode}><option>BUSINESS_DAYS</option><option>CALENDAR_DAYS</option></select>
                            <select name="workingCalendarId" defaultValue={version.workingCalendarId ?? ''}>
                              <option value="">No working calendar</option>
                              {calendars.filter((calendar) => calendar.isActive || calendar.id === version.workingCalendarId).map((calendar) => <option key={calendar.id} value={calendar.id}>{calendar.code}</option>)}
                            </select>
                            <input name="maximumRequestDays" type="number" min="0.5" step="0.5" placeholder="Maximum request days" defaultValue={version.maximumRequestDays ?? ''} />
                            <select name="overlapBehavior" defaultValue={version.overlapBehavior}><option>BLOCK</option><option>WARN</option><option>ALLOW</option></select>
                            <label><input name="consumesBalance" type="checkbox" defaultChecked={version.consumesBalance} /> Consumes balance</label>
                            <select name="balanceBucketId" defaultValue={version.balanceBucketId ?? ''}>
                              <option value="">No bucket</option>
                              {balanceBuckets.map((bucket) => <option key={bucket.id} value={bucket.id}>{bucket.code}</option>)}
                            </select>
                            <button type="submit">Update draft</button>
                          </form>
                        </details>
                        <button type="button" onClick={() => publishVersion(version.id)}>Publish</button>
                      </>
                    ) : null}
                  </div>
                ))}
              </div>
            ))}
          </div>
        </article>
        <form className="panel-card catalog-form" onSubmit={resolvePolicy}>
          <h3>Resolve policy</h3>
          <select name="leaveTypeId" required>
            <option value="">Select leave type</option>
            {leaveTypes.map((type) => <option key={type.id} value={type.id}>{type.code}</option>)}
          </select>
          <select name="orgUnitId">
            <option value="">No org unit</option>
            {flattenOrgUnits(orgTree).map((unit) => <option key={unit.id} value={unit.id}>{unit.code}</option>)}
          </select>
          <input name="date" type="date" required defaultValue="2026-01-01" />
          <button type="submit">Resolve</button>
          {resolvedPolicy ? <p className="muted">{resolvedPolicy.found ? `Resolved ${resolvedPolicy.policy?.leaveTypeCode} ${resolvedPolicy.policy?.orgUnitCode ?? 'COMPANY'} v${resolvedPolicy.version?.versionNumber}` : resolvedPolicy.reason}</p> : null}
        </form>
      </section>

      {canReadAudit ? <section className="admin-panel" aria-label="Audit events">
        <div className="section-heading"><div><p className="eyebrow">Audit</p><h2>Audit events</h2></div>{auditMessage ? <p className="error">{auditMessage}</p> : null}</div>
        <form className="panel-card catalog-form" onSubmit={applyAuditFilters}>
          <h3>Filters</h3><input name="action" placeholder="Action" defaultValue={auditFilters.action} /><input name="resourceType" placeholder="Resource type" defaultValue={auditFilters.resourceType} />
          <label>From <input name="fromUtc" type="datetime-local" defaultValue={auditFilters.fromUtc} /></label><label>To <input name="toUtc" type="datetime-local" defaultValue={auditFilters.toUtc} /></label><button type="submit">Apply filters</button>
        </form>
        <div className="catalog-list">{auditEvents?.items.length ? auditEvents.items.map((event) => <details className="version-row" key={event.id}><summary><strong>{new Date(event.occurredAtUtc).toLocaleString()}</strong> <span>{event.action}</span> <span>{event.actorDisplayName ?? event.actorUserId ?? 'System'}</span> <span>{event.subjectDisplayName ?? event.subjectUserId ?? 'No subject'}</span> <span>{event.resourceType}{event.resourceId ? ` / ${event.resourceId}` : ''}</span> <span>{event.orgUnitName ?? 'Global'}</span></summary><pre>{event.metadataJson ? JSON.stringify(JSON.parse(event.metadataJson), null, 2) : 'No metadata'}</pre>{event.correlationId ? <p className="muted">Correlation: {event.correlationId}</p> : null}</details>) : <p className="muted">No audit events visible for the selected filters.</p>}</div>
        {auditEvents ? <div className="inline-form"><button type="button" disabled={auditEvents.page <= 1} onClick={() => void changeAuditPage(auditEvents.page - 1)}>Previous</button><span>Page {auditEvents.page} · {auditEvents.totalCount} events</span><button type="button" disabled={!auditEvents.hasNextPage} onClick={() => void changeAuditPage(auditEvents.page + 1)}>Next</button></div> : null}
      </section> : null}

      <section className="admin-panel" aria-label="Development organization admin">
        <div>
          <p className="eyebrow">Organization</p>
          <h2>Scoped organization data</h2>
          {adminError ? <p className="error">{adminError}</p> : null}
        </div>
        <div className="admin-grid">
          <article className="panel-card">
            <h3>Organizational tree</h3>
            <OrgTree units={orgTree} />
          </article>
          <article className="panel-card">
            <h3>Users in scope</h3>
            <ul className="user-list">
              {users.map((user) => (
                <li key={user.id}>
                  <strong>{user.displayName}</strong>
                  <span>{user.email}</span>
                  <em>{user.primaryOrgUnit?.name ?? 'No primary unit'}</em>
                </li>
              ))}
            </ul>
          </article>
        </div>
      </section>
    </main>
  );
}

function CatalogTable({ title, rows, onToggle }: { title: string; rows: string[][]; onToggle: Array<() => void> }) {
  return (
    <article className="panel-card">
      <h3>{title}</h3>
      {rows.length === 0 ? <p className="muted">No records visible for this actor.</p> : null}
      <div className="catalog-list">
        {rows.map((row, index) => (
          <div className="catalog-row" key={row[0]}>
            {row.map((value) => <span key={value}>{value}</span>)}
            <button type="button" onClick={onToggle[index]}>Toggle active</button>
          </div>
        ))}
      </div>
    </article>
  );
}

function CatalogForm({ title, codePlaceholder, namePlaceholder, onSubmit, includeSortOrder = false, unit }: { title: string; codePlaceholder: string; namePlaceholder: string; onSubmit: (event: FormEvent<HTMLFormElement>) => void; includeSortOrder?: boolean; unit?: string }) {
  return (
    <form className="panel-card catalog-form" onSubmit={onSubmit}>
      <h3>{title}</h3>
      <input name="code" placeholder={codePlaceholder} required />
      <input name="name" placeholder={namePlaceholder} required />
      <input name="description" placeholder="Description" />
      {includeSortOrder ? <input name="sortOrder" type="number" min="0" defaultValue="0" /> : null}
      {unit ? <input name="unit" value={unit} readOnly aria-label="Unit" /> : null}
      <button type="submit">Create</button>
    </form>
  );
}

function flattenOrgUnits(units: OrgUnit[]): OrgUnit[] {
  return units.flatMap((unit) => [unit, ...flattenOrgUnits(unit.children ?? [])]);
}
