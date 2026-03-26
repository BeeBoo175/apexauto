"use client";

import { Button } from "@/components/ui/Button";
import { useVehicleCatalogue } from "@/hooks/use-vehicle-catalogue";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { CatalogueFilters as Filters, CataloguePage } from "@/lib/vehicles/contracts";
import { CatalogueFilters } from "./CatalogueFilters";
import { VehicleCard } from "./VehicleCard";
import styles from "./Catalogue.module.css";

export function VehicleCatalogue({
  initial,
  filters,
  page,
  branches,
}: {
  initial: ApiResult<CataloguePage>;
  filters: Filters;
  page: number;
  branches: Page<Branch> | null;
}) {
  const catalogue = useVehicleCatalogue(initial, filters, page, branches);
  const { snapshot, active, busy } = catalogue;
  return (
    <section className={styles.page}>
      <header className={styles.heading}>
        <div>
          <h1>Find your next car</h1>
          <p>Explore inspected vehicles approved for our showroom.</p>
        </div>
        <Button variant="secondary" disabled={busy} onClick={() => void catalogue.load()}>
          {busy ? "Refreshing vehicles…" : "Refresh vehicles"}
        </Button>
      </header>
      <details className={styles.filterDisclosure}>
        <summary>Filter and sort vehicles</summary>
        <CatalogueFilters
          draft={catalogue.draft}
          setDraft={catalogue.setDraft}
          busy={busy}
          apply={(next) => void catalogue.load(next, 1)}
          branches={catalogue.branches}
          branchBusy={catalogue.branchBusy}
          loadBranches={(next) => void catalogue.loadBranches(next)}
        />
      </details>
      <div aria-busy={busy}>
        {snapshot && (
          <>
            <p className={styles.summary} aria-live="polite">
              {snapshot.total} available vehicles · Page {active.page} of{" "}
              {Math.max(1, Math.ceil(snapshot.total / snapshot.pageSize))}
            </p>
            {snapshot.items.length === 0 && (
              <p className={styles.empty}>
                No vehicles match these filters. Try a wider price or year range, or clear the
                filters.
              </p>
            )}
            <div className={styles.vehicles}>
              {snapshot.items.map((vehicle, index) => (
                <VehicleCard
                  key={vehicle.id}
                  vehicle={vehicle}
                  first={index === 0}
                  photoVersion={catalogue.photoVersion}
                />
              ))}
            </div>
            <nav className={styles.paging} aria-label="Vehicle catalogue pages">
              <Button
                variant="secondary"
                disabled={busy || active.page <= 1}
                onClick={() => void catalogue.load(active.filters, active.page - 1)}
              >
                Previous vehicles
              </Button>
              <Button
                variant="secondary"
                disabled={busy || active.page * snapshot.pageSize >= snapshot.total}
                onClick={() => void catalogue.load(active.filters, active.page + 1)}
              >
                Next vehicles
              </Button>
            </nav>
          </>
        )}
      </div>
    </section>
  );
}
