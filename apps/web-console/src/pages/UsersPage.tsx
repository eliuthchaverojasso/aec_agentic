import { useMemo, useState } from "react";

type DemoUser = { id: number; name: string; role: string; email: string; status: "active" | "inactive" };
const KEY = "ema_demo_users";
const defaults: DemoUser[] = [
  { id: 1, name: "Alex Director", role: "Director", email: "alex@ema.ai", status: "active" },
  { id: 2, name: "BIM Manager", role: "BIM Manager", email: "bim.manager@ema.ai", status: "active" },
  { id: 3, name: "Electrical Lead", role: "Discipline Lead", email: "electrical.lead@ema.ai", status: "active" },
  { id: 4, name: "Reviewer", role: "Reviewer", email: "reviewer@ema.ai", status: "active" },
];

export function UsersPage() {
  const [users, setUsers] = useState<DemoUser[]>(() => {
    try {
      const raw = window.localStorage.getItem(KEY);
      if (!raw) return defaults;
      const parsed = JSON.parse(raw);
      if (!Array.isArray(parsed)) return defaults;
      const safe = parsed
        .filter((item): item is DemoUser => Boolean(item && typeof item === "object"))
        .map((item) => ({
          id: typeof item.id === "number" ? item.id : Date.now(),
          name: typeof item.name === "string" && item.name.trim() ? item.name : "Local Demo User",
          role: typeof item.role === "string" && item.role.trim() ? item.role : "Viewer",
          email: typeof item.email === "string" && item.email.trim() ? item.email : "demo.user@ema.ai",
          status: (item.status === "inactive" ? "inactive" : "active") as "active" | "inactive",
        }));
      return safe.length ? safe : defaults;
    } catch {
      return defaults;
    }
  });
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [role, setRole] = useState("Viewer");
  const activeCount = useMemo(() => users.filter((u) => u.status === "active").length, [users]);

  const save = (next: DemoUser[]) => {
    setUsers(next);
    try {
      window.localStorage.setItem(KEY, JSON.stringify(next));
    } catch {
      // Local demo storage errors should not break route rendering.
    }
  };

  return (
    <section className="ema-page ema-page-shell space-y-5">
      <div className="ema-card p-5">
        <h2 className="text-lg font-semibold text-ink">Local demo users</h2>
        <p className="mt-1 text-sm text-muted">Planning-only user roster. Not production authentication.</p>
        <p className="mt-3 text-sm text-muted">{activeCount} active of {users.length} total</p>
      </div>
      <div className="ema-card p-5">
        <div className="grid gap-3 sm:grid-cols-4">
          <input
            className="ema-input h-10 px-3"
            placeholder="Name"
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
          <input
            className="ema-input h-10 px-3"
            placeholder="Email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
          <input
            className="ema-input h-10 px-3"
            placeholder="Role"
            value={role}
            onChange={(e) => setRole(e.target.value)}
          />
          <button
            className="ema-btn-primary h-10"
            onClick={() => {
              if (!name || !email) return;
              save([...users, { id: Date.now(), name, email, role, status: "active" }]);
              setName("");
              setEmail("");
            }}
          >
            Add user
          </button>
        </div>
        <div className="mt-4 overflow-x-auto">
          <table className="min-w-full text-sm" data-no-glass>
            <thead className="text-left text-xs uppercase tracking-wide text-muted">
              <tr>
                <th className="pb-2 pr-4">Name</th>
                <th className="pb-2 pr-4">Email</th>
                <th className="pb-2 pr-4">Role</th>
                <th className="pb-2 pr-4">Status</th>
                <th className="pb-2" />
              </tr>
            </thead>
            <tbody>
              {users.map((user) => (
                <tr key={user.id} className="border-t border-line">
                  <td className="py-2 pr-4 font-medium text-ink">{user.name}</td>
                  <td className="py-2 pr-4 text-muted">{user.email}</td>
                  <td className="py-2 pr-4 text-muted">{user.role}</td>
                  <td className="py-2 pr-4">
                    <span className={user.status === "active" ? "ema-pill ema-pill-success" : "ema-pill"}>
                      {user.status}
                    </span>
                  </td>
                  <td className="py-2">
                    <button
                      className="ema-btn-ghost text-xs"
                      onClick={() =>
                        save(
                          users.map((u) =>
                            u.id === user.id
                              ? { ...u, status: u.status === "active" ? "inactive" : "active" }
                              : u,
                          ),
                        )
                      }
                    >
                      Toggle
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
