import { Button } from "@/components/ui/Button";
import { ScrollStrip } from "@/components/ui/ScrollStrip";
import { showroomName, type Branch } from "@/lib/branches";
import type { CatalogueFilters } from "@/lib/vehicles/contracts";
import styles from "./CatalogueHero.module.css";
import layout from "./VehicleLayout.module.css";

interface CatalogueHeroProps {
  active: CatalogueFilters;
  showrooms: Branch[] | null;
  busy: boolean;
  onShowroom: (branchId: string) => void;
}

export function CatalogueHero({ active, showrooms, busy, onShowroom }: CatalogueHeroProps) {
  const tabs = [
    { id: "", label: "All showrooms" },
    ...(showrooms ?? []).map((branch) => ({ id: branch.id, label: showroomName(branch.name) })),
  ];

  return (
    <header className={styles.hero}>
      <div className={`${layout.content} ${styles.inner}`}>
        <h1 className={`${layout.heroHeading} ${styles.heading}`}>Used cars for sale</h1>
        <p className={styles.lede}>
          Signed 210-point inspection, upfront pricing, and a 48-hour hold on every car.
        </p>

        {showrooms && (
          <div className={styles.toolbar}>
            <ScrollStrip label="Showrooms" className={styles.tabs} dragScroll>
              {tabs.map((tab) => (
                <Button
                  key={tab.id || "all"}
                  type="button"
                  variant="secondary"
                  size="sm"
                  className={styles.tab}
                  aria-pressed={active.branchId === tab.id}
                  disabled={busy}
                  onClick={() => onShowroom(tab.id)}
                >
                  {tab.label}
                </Button>
              ))}
            </ScrollStrip>
          </div>
        )}
      </div>
    </header>
  );
}
