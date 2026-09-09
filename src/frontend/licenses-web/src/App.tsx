import { FormEvent, useEffect, useMemo, useState } from 'react';

type HealthStatus = 'Loading' | 'Healthy' | 'Unhealthy' | 'Degraded' | 'Unavailable';

type HealthResponse = { status: string };
type OrgUnit = { id: string; name: string; code: string; parentId: string | null; isActive: boolean; children?: OrgUnit[] };
type User = { id: string; displayName: string; email: string; isActive: boolean; primaryOrgUnit: OrgUnit | null };
type DevelopmentActor = { id: string; displayName: string; email: string; primaryOrgUnitName: string | null };
type LeaveType = { id: string; code: string; name: string; description: string | null; isActive: boolean; sortOrder: number };
type BalanceBucket = { id: string; code: string; name: string; description: string | null; unit: string; isActive: boolean };

type StatusCardProps = { label: string; status: HealthStatus };
type CatalogKind = 'leave-types' | 'balance-buckets';

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

  const selectedActor = useMemo(() => actors.find((actor) => actor.id === selectedActorId) ?? null, [actors, selectedActorId]);

  async function loadCatalog(actorId: string | null) {
    const [types, buckets] = await Promise.all([
      fetchJson<LeaveType[]>('/leave-types', actorId),
      fetchJson<BalanceBucket[]>('/balance-buckets', actorId),
    ]);
    setLeaveTypes(types);
    setBalanceBuckets(buckets);
  }

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

    void loadAdminData();
    void loadCatalogData();

    return () => {
      isMounted = false;
    };
  }, [selectedActorId]);

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
