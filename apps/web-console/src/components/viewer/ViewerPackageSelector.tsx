import type { ViewerPackage } from "../../lib/viewerTypes";

type Props = {
  packages: ViewerPackage[];
  selectedId?: string | number;
  onSelect: (id: string | number) => void;
};

export function ViewerPackageSelector({ packages, selectedId, onSelect }: Props) {
  if (packages.length === 0) {
    return (
      <div className="ema-card p-4 text-sm text-muted">
        No viewer package found for this project. Register DWFx/RVT/NWD/IFC/SVF in landing, then run Processing / Sync.
      </div>
    );
  }

  return (
    <div className="ema-card p-4">
      <label className="ema-field-label">Package</label>
      <select
        className="ema-select mt-2 h-10 w-full px-3 text-sm"
        value={String(selectedId ?? packages[0].id)}
        onChange={(event) => onSelect(event.target.value)}
      >
        {packages.map((pkg) => (
          <option key={pkg.id} value={String(pkg.id)}>
            {pkg.fileName} ({pkg.sourceFormat.toUpperCase()})
          </option>
        ))}
      </select>
    </div>
  );
}
