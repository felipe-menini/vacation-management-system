import { useEffect, useMemo, useState } from 'react';

type HealthStatus = 'Loading' | 'Healthy' | 'Unhealthy' | 'Degraded' | 'Unavailable';

type HealthResponse = { status: string };
type OrgUnit = { id: string; name: string; code: string; parentId: string | null; isActive: boolean; children?: OrgUnit[] };
type User = { id: string; displayName: string; email: string; isActive: boolean; primaryOrgUnit: OrgUnit | null };
type DevelopmentActor = { id: string; displayName: string; email: string; primaryOrgUnitName: string | null };

type StatusCardProps = { label: string; status: HealthStatus };

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
  const [adminError, setAdminError] = useState<string | null>(null);

  const selectedActor = useMemo(() => actors.find((actor) => actor.id === selectedActorId) ?? null, [actors, selectedActorId]);

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

    void loadAdminData();

    return () => {
      isMounted = false;
    };
  }, [selectedActorId]);

  function changeSelectedActor(actorId: string) {
    setSelectedActorId(actorId);
    localStorage.setItem(selectedActorStorageKey, actorId);
  }

  return (
    <main className="page-shell">
      <section className="hero">
        <p className="eyebrow">Corporate leave platform</p>
        <h1>Leave Management System</h1>
        <p className="summary">
          Production-oriented walking skeleton with backend-enforced authorization for organizational data.
        </p>
      </section>

      <section className="status-grid" aria-label="System health">
        <StatusCard label="API status" status={apiStatus} />
        <StatusCard label="Database status" status={databaseStatus} />
      </section>

      <section className="admin-panel" aria-label="Development organization admin">
        <div>
          <p className="eyebrow">Development authorization demo</p>
          <h2>Organization</h2>
          <p className="warning">Temporary Development-only actor selection. Production does not send or accept X-Dev-User-Id.</p>
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
