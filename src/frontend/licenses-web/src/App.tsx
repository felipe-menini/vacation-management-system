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
type LeaveRequest = { id: string; userId: string; userDisplayName: string | null; orgUnitId: string; orgUnitCode: string | null; orgUnitName: string | null; leaveTypeId: string; leaveTypeCode: string | null; leaveTypeName: string | null; leavePolicyVersionId: string | null; startDate: string; endDate: string; dayPortion: string; calculatedDays: number | null; status: string; comment: string | null; createdAtUtc: string; submittedAtUtc: string | null };
type SubmitLeaveRequestResult = { request: LeaveRequest; warnings: string[]; wasAlreadySubmitted: boolean };

type StatusCardProps = { label: string; status: HealthStatus };
type CatalogKind = 'leave-types' | 'balance-buckets';
const weekdayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

const endpointPrefix = '/api';
const selectedActorStorageKey = 'licenses.devActorId';
const isDevelopment = import.meta.env.DEV;

function developmentHeaders(actorId: string | null): HeadersInit {
  return isDevelopment && actorId ? { 'X-Dev-User-Id': actorId } : {};
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

async function fetchJson<T>(path: string, actorId: string | null = null): Promise<T> {
  const response = await fetch(`${endpointPrefix}${path}`, { headers: developmentHeaders(actorId) });
  if (!response.ok) throw new Error(`Request failed: ${response.status}`);
  return (await response.json()) as T;
}

async function sendJson<T>(path: string, method: 'POST' | 'PUT', body: unknown, actorId: string | null): Promise<T> {
  const response = await fetch(`${endpointPrefix}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', ...developmentHeaders(actorId) },
    body: JSON.stringify(body),
  });
  if (!response.ok) throw new Error(`Request failed: ${response.status}`);
  return (await response.json()) as T;
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
  const [requestMessage, setRequestMessage] = useState<string | null>(null);
  const [isRequestSubmitting, setIsRequestSubmitting] = useState(false);

  const selectedActor = useMemo(() => actors.find((actor) => actor.id === selectedActorId) ?? null, [actors, selectedActorId]);

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

  async function loadLeaveRequests(actorId: string | null) {
    setMyRequests(await fetchJson<LeaveRequest[]>('/leave-requests/me', actorId));
    try {
      setScopedRequests(await fetchJson<LeaveRequest[]>('/leave-requests/scoped', actorId));
    } catch {
      setScopedRequests([]);
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
          setAdminError(null);
        }
      } catch (error) {
        if (isMounted) {
          setOrgTree([]);
          setUsers([]);
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
        }
      }
    })();

    return () => {
      isMounted = false;
    };
  }, [loadUserBalances, selectedActorId, selectedBalanceUserId]);

  function changeSelectedActor(actorId: string) {
    setSelectedActorId(actorId);
    localStorage.setItem(selectedActorStorageKey, actorId);
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


  async function createLeaveRequest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<LeaveRequest>('/leave-requests', 'POST', { orgUnitId: data.get('orgUnitId'), leaveTypeId: data.get('leaveTypeId'), startDate: data.get('startDate'), endDate: data.get('endDate'), dayPortion: data.get('dayPortion'), comment: data.get('comment') || null }, selectedActorId);
      event.currentTarget.reset();
      await loadLeaveRequests(selectedActorId);
      setRequestMessage('Draft request saved.');
    } catch (error) { setRequestMessage(error instanceof Error ? error.message : 'Unable to save leave request.'); }
  }

  async function updateLeaveRequest(event: FormEvent<HTMLFormElement>, requestId: string) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      await sendJson<LeaveRequest>(`/leave-requests/${requestId}`, 'PUT', { orgUnitId: data.get('orgUnitId'), leaveTypeId: data.get('leaveTypeId'), startDate: data.get('startDate'), endDate: data.get('endDate'), dayPortion: data.get('dayPortion'), comment: data.get('comment') || null }, selectedActorId);
      await loadLeaveRequests(selectedActorId);
      setRequestMessage('Draft request updated.');
    } catch (error) { setRequestMessage(error instanceof Error ? error.message : 'Unable to update leave request.'); }
  }

  async function submitLeaveRequest(requestId: string) {
    setIsRequestSubmitting(true);
    try {
      const result = await sendJson<SubmitLeaveRequestResult>(`/leave-requests/${requestId}/submit`, 'POST', {}, selectedActorId);
      await loadLeaveRequests(selectedActorId);
      await loadMyBalances(selectedActorId);
      setRequestMessage(`Submitted: ${result.request.calculatedDays ?? '-'} days${result.warnings.length ? ` · ${result.warnings.join(' · ')}` : ''}`);
    } catch (error) { setRequestMessage(error instanceof Error ? error.message : 'Unable to submit leave request.'); }
    finally { setIsRequestSubmitting(false); }
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
          {requestMessage ? <p className={requestMessage.includes('failed') || requestMessage.includes('403') || requestMessage.includes('401') ? 'error' : 'muted'}>{requestMessage}</p> : null}
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
        <article className="panel-card"><h3>My requests</h3>{myRequests.length === 0 ? <p className="muted">No leave requests yet.</p> : null}<div className="catalog-list">
          {myRequests.map((request) => <div className="policy-card" key={request.id}>
            <strong>{request.leaveTypeCode ?? request.leaveTypeId} · {request.status}</strong>
            <span>{request.orgUnitCode ?? request.orgUnitId} · {request.startDate} → {request.endDate} · {request.dayPortion}</span>
            <span>Calculated: {request.calculatedDays ?? 'pending'} · Policy version: {request.leavePolicyVersionId ?? 'not frozen'}</span>
            <span>{request.comment ?? 'No comment'}</span>
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
          </div>)}
        </div></article>
        {scopedRequests.length > 0 ? <article className="panel-card"><h3>Scoped request inspector</h3><div className="catalog-list">{scopedRequests.map((request) => <div className="version-row" key={request.id}><span>{request.userDisplayName ?? request.userId}</span><span>{request.leaveTypeCode}</span><span>{request.orgUnitCode}</span><span>{request.startDate} → {request.endDate}</span><span>{request.status}</span></div>)}</div></article> : null}
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