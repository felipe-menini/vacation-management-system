import { useEffect, useState } from 'react';

type HealthStatus = 'Loading' | 'Healthy' | 'Unhealthy' | 'Degraded' | 'Unavailable';

type HealthResponse = { status: string };
type OrgUnit = { id: string; name: string; code: string; parentId: string | null; isActive: boolean; children?: OrgUnit[] };
type User = { id: string; displayName: string; email: string; isActive: boolean; primaryOrgUnit: OrgUnit | null };

type StatusCardProps = { label: string; status: HealthStatus };

const endpointPrefix = '/api';

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

async function fetchJson<T>(path: string): Promise<T> {
  const response = await fetch(`${endpointPrefix}${path}`);
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
  const [adminError, setAdminError] = useState<string | null>(null);

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

    async function loadAdminData() {
      try {
        const [tree, userList] = await Promise.all([
          fetchJson<OrgUnit[]>('/org-units/tree'),
          fetchJson<User[]>('/users'),
        ]);
        if (isMounted) {
          setOrgTree(tree);
          setUsers(userList);
          setAdminError(null);
        }
      } catch (error) {
        if (isMounted) setAdminError(error instanceof Error ? error.message : 'Unable to load admin data.');
      }
    }

    void loadStatuses();
    void loadAdminData();

    return () => {
      isMounted = false;
    };
  }, []);

  return (
    <main className="page-shell">
      <section className="hero">
        <p className="eyebrow">Corporate leave platform</p>
        <h1>Leave Management System</h1>
        <p className="summary">
          Production-oriented walking skeleton with organizational structure validation.
        </p>
      </section>

      <section className="status-grid" aria-label="System health">
        <StatusCard label="API status" status={apiStatus} />
        <StatusCard label="Database status" status={databaseStatus} />
      </section>

      <section className="admin-panel" aria-label="Development organization admin">
        <div>
          <p className="eyebrow">Development admin</p>
          <h2>Organization</h2>
          <p className="warning">Temporary unsecured endpoints. Do not expose this screen in production.</p>
          {adminError ? <p className="error">{adminError}</p> : null}
        </div>
        <div className="admin-grid">
          <article className="panel-card">
            <h3>Organizational tree</h3>
            <OrgTree units={orgTree} />
          </article>
          <article className="panel-card">
            <h3>Users</h3>
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
