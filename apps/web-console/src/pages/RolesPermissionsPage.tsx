const groups: Array<{ title: string; items: string[] }> = [
  { title: "Portfolio", items: ["View portfolio", "Export portfolio report"] },
  { title: "Projects", items: ["View project", "Edit project binding", "View readiness", "Create snapshot"] },
  { title: "Requirements", items: ["View requirements", "Import requirements", "Mark requirement reviewed", "Link evidence"] },
  { title: "Issues", items: ["View issues", "Review issues", "Close issues", "Mark false positive"] },
  { title: "Documents", items: ["View evidence candidates", "Preview documents", "Promote evidence candidate (disabled)"] },
  { title: "Processing", items: ["Scan landing", "Rebuild manifest", "Dry-run ingest", "Run ingest", "View sync logs"] },
  { title: "Admin", items: ["Manage users", "Manage roles", "Change appearance", "View system health", "Use Dev Mode"] },
];

export function RolesPermissionsPage() {
  return (
    <section className="ema-page ema-page-shell space-y-5">
      <div className="ema-card p-5">
        <h2 className="text-lg font-semibold text-ink">Local UI permission model</h2>
        <p className="mt-1 text-sm text-muted">Demo planning only. This matrix does not enforce backend authorization.</p>
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        {groups.map((group) => (
          <div key={group.title} className="ema-card p-4">
            <h3 className="font-semibold text-ink">{group.title}</h3>
            <ul className="mt-3 space-y-2 text-sm text-muted">
              {group.items.map((item) => (
                <li key={item} className="flex items-center gap-2">
                  <span className="h-2 w-2 flex-shrink-0 rounded-full bg-accent" />
                  {item}
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </section>
  );
}
