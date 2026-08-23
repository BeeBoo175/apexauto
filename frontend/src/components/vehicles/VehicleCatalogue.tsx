"use client";

import { useState } from "react";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import Link from "next/link";
import { GuideHubBanner } from "@/components/guide/GuidePageShell";
import { Button, buttonClassName } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { type CatalogueDraftSync, useVehicleCatalogue } from "@/hooks/use-vehicle-catalogue";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { Taxonomy } from "@/lib/intake/contracts";
import { countActiveCatalogueFilters } from "@/lib/vehicles/catalogue-controls";
import type { CatalogueFilters as Filters, CataloguePage } from "@/lib/vehicles/contracts";
import { catalogueDefaults } from "@/lib/vehicles/query";
import { CatalogueFilters } from "./CatalogueFilters";
import { CataloguePagination } from "./CataloguePagination";
import { CatalogueToolbar } from "./CatalogueToolbar";
import { VehicleCard } from "./VehicleCard";
import styles from "./Catalogue.module.css";
import layout from "./VehicleLayout.module.css";

interface VehicleCatalogueProps {
  initial: ApiResult<CataloguePage>;
  filters: Filters;
  page: number;
  branches: Page<Branch> | null;
  taxonomy: Taxonomy | null;
}

export function VehicleCatalogue({
  initial,
  filters,
  page,
  branches,
  taxonomy,
}: VehicleCatalogueProps) {
  const [mobileFiltersOpen, setMobileFiltersOpen] = useState(false);
  const catalogue = useVehicleCatalogue(initial, filters, page, branches);
  const { snapshot, active, busy } = catalogue;
  const hasActiveFilters = countActiveCatalogueFilters(active.filters) > 0;

  async function applyFilters(nextFilters: Filters, syncDraft: CatalogueDraftSync = "all") {
    const succeeded = await catalogue.load(nextFilters, 1, { syncDraft });
    if (succeeded) {
      setMobileFiltersOpen(false);
    }
  }

  async function changePage(nextPage: number) {
    const succeeded = await catalogue.load(active.filters, nextPage, { syncDraft: "none" });
    if (succeeded) {
      const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
      window.scrollTo({
        top: 0,
        behavior: reducedMotion ? "auto" : "smooth",
      });
    }
  }

  return (
    <section className={styles.page}>
      <LoadingProgress label="Loading vehicles" active={busy} />
      <LoadingProgress label="Loading showroom branches" active={catalogue.branchBusy} />

      <GuideHubBanner
        title="Used cars for sale"
        description="Signed 210-point inspection, upfront pricing, and a 48-hour hold on every car."
        actions={[
          { href: "/buying-guide", label: "Buying guide", variant: "shell", prefetch: true },
          { href: "/sell", label: "Sell or trade in", variant: "secondary" },
        ]}
      />

      <div className={`${layout.content} ${styles.workspace}`}>
        <CatalogueFilters
          active={active.filters}
          draft={catalogue.draft}
          setDraft={catalogue.setDraft}
          busy={busy}
          apply={(next) => void applyFilters({ ...next, search: active.filters.search }, "all")}
          branches={catalogue.branches}
          branchBusy={catalogue.branchBusy}
          loadBranches={(next) => void catalogue.loadBranches(next)}
          taxonomy={taxonomy}
          mobileOpen={mobileFiltersOpen}
          onToggleMobile={() => setMobileFiltersOpen((current) => !current)}
        />

        <div className={styles.resultsColumn}>
          <CatalogueToolbar
            active={active.filters}
            page={active.page}
            snapshot={snapshot}
            branches={catalogue.branches}
            busy={busy}
            onApply={(next) => void applyFilters(next, "all")}
            onSearch={(search) => void applyFilters({ ...active.filters, search }, "none")}
            onSort={(sort, direction) =>
              void applyFilters({ ...active.filters, sort, direction }, "sort")
            }
          />

          <div aria-busy={busy}>
            {snapshot && snapshot.items.length > 0 && (
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
            )}

            {snapshot && snapshot.total > 0 && snapshot.items.length === 0 && (
              <div className={styles.emptyCard}>
                <h2 className={layout.cardHeading}>No vehicles on page {active.page}</h2>
                <p>
                  Available showroom listings may have updated while browsing. Return to page 1 to
                  keep viewing with your current filters.
                </p>
              </div>
            )}

            {snapshot && snapshot.total > 0 && (
              <CataloguePagination
                page={active.page}
                snapshot={snapshot}
                busy={busy}
                onPageChange={(nextPage) => void changePage(nextPage)}
              />
            )}

            {snapshot && snapshot.total === 0 && (
              <div className={styles.emptyCard}>
                <h2 className={layout.cardHeading}>
                  {hasActiveFilters
                    ? "No vehicles match these filters"
                    : "No inspected cars currently listed"}
                </h2>
                <p>
                  {hasActiveFilters
                    ? "Try a wider MYR price or year range, switch showrooms, or reset filters to see all available cars."
                    : "Showroom listings appear here after inspection and management approval. Value or sell your own car in the meantime."}
                </p>
                <div className={styles.emptyActions}>
                  {hasActiveFilters && (
                    <Button
                      type="button"
                      variant="primary"
                      disabled={busy}
                      onClick={() => void applyFilters({ ...catalogueDefaults }, "all")}
                    >
                      Reset all filters
                    </Button>
                  )}
                  <Link
                    href="/sell"
                    prefetch={false}
                    className={buttonClassName(hasActiveFilters ? "secondary" : "primary", "md")}
                  >
                    Value or sell your car
                  </Link>
                </div>
              </div>
            )}

            {!snapshot && <ReloadNotice subject="Vehicle catalogue" />}
          </div>
        </div>
      </div>
    </section>
  );
}
