"use client";

import { prefersReducedMotion } from "@/lib/motion";
import { Fragment, useState } from "react";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import Link from "next/link";
import { Button, buttonClassName } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { type CatalogueDraftSync, useVehicleCatalogue } from "@/hooks/use-vehicle-catalogue";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { GuideWarranty } from "@/lib/guide/purchase-guide";
import type { Taxonomy } from "@/lib/intake/contracts";
import {
  countActiveCatalogueFilters,
  showroomTabBranches,
} from "@/lib/vehicles/catalogue-controls";
import type { CatalogueFilters as Filters, CataloguePage } from "@/lib/vehicles/contracts";
import { catalogueDefaults } from "@/lib/vehicles/query";
import { CatalogueBackToTop } from "./CatalogueBackToTop";
import { CatalogueFilters } from "./CatalogueFilters";
import { CatalogueHero } from "./CatalogueHero";
import { CatalogueControlBar } from "./CatalogueControlBar";
import { CatalogueSort } from "./CatalogueSort";
import { CatalogueWarrantyBand } from "./CatalogueWarrantyBand";
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
  warranties: readonly GuideWarranty[] | null;
  holdHours: number;
}

// The warranty band breaks the grid after two full rows of three.
const WARRANTY_BAND_AFTER = 6;

export function VehicleCatalogue({
  initial,
  filters,
  page,
  branches,
  taxonomy,
  warranties,
  holdHours,
}: VehicleCatalogueProps) {
  const [filtersOpen, setFiltersOpen] = useState(false);
  const catalogue = useVehicleCatalogue(initial, filters, page, branches);
  const { snapshot, active, busy } = catalogue;
  const activeFilterCount = countActiveCatalogueFilters(active.filters);
  const hasActiveFilters = activeFilterCount > 0;
  const showrooms = showroomTabBranches(catalogue.branches);

  async function applyFilters(nextFilters: Filters, syncDraft: CatalogueDraftSync = "all") {
    await catalogue.load(nextFilters, 1, { syncDraft });
  }

  async function changePage(nextPage: number) {
    const succeeded = await catalogue.load(active.filters, nextPage, { syncDraft: "none" });
    if (succeeded) {
      window.scrollTo({
        top: 0,
        behavior: prefersReducedMotion() ? "auto" : "smooth",
      });
    }
  }

  return (
    <section className={styles.page}>
      <LoadingProgress label="Loading vehicles" active={busy} />
      <LoadingProgress label="Loading showroom branches" active={catalogue.branchBusy} />
      <CatalogueBackToTop />

      <CatalogueHero
        active={active.filters}
        showrooms={showrooms}
        busy={busy}
        holdHours={holdHours}
        onShowroom={(branchId) => void applyFilters({ ...active.filters, branchId }, "all")}
      />

      <CatalogueControlBar
        active={active.filters}
        activeFilterCount={activeFilterCount}
        busy={busy}
        filtersOpen={filtersOpen}
        onSearch={(search) => void applyFilters({ ...active.filters, search }, "none")}
        onSort={(sort, direction) =>
          void applyFilters({ ...active.filters, sort, direction }, "sort")
        }
        onOpenFilters={() => setFiltersOpen(true)}
      />

      <div className={`${layout.content} ${styles.workspace}`}>
        <CatalogueFilters
          active={active.filters}
          draft={catalogue.draft}
          setDraft={catalogue.setDraft}
          busy={busy}
          apply={(next) =>
            void catalogue.load({ ...next, search: active.filters.search }, 1, {
              syncDraft: "all",
            })
          }
          branches={catalogue.branches}
          branchBusy={catalogue.branchBusy}
          loadBranches={(next) => void catalogue.loadBranches(next)}
          taxonomy={taxonomy}
          open={filtersOpen}
          onClose={() => setFiltersOpen(false)}
          showroomInTabs={showrooms !== null}
          mobileSort={
            <CatalogueSort
              id="catalogue-sort-mobile"
              active={active.filters}
              busy={busy}
              onSort={(sort, direction) =>
                void applyFilters({ ...active.filters, sort, direction }, "sort")
              }
            />
          }
        />

        <div className={styles.resultsColumn}>
          <CatalogueToolbar
            active={active.filters}
            page={active.page}
            snapshot={snapshot}
            branches={catalogue.branches}
            busy={busy}
            onApply={(next) => void applyFilters(next, "all")}
          />

          <div aria-busy={busy}>
            {snapshot && snapshot.items.length > 0 && (
              <div className={styles.vehicles}>
                {snapshot.items.map((vehicle, index) => (
                  <Fragment key={vehicle.id}>
                    <VehicleCard
                      vehicle={vehicle}
                      first={index === 0}
                      photoVersion={catalogue.photoVersion}
                    />
                    {index === WARRANTY_BAND_AFTER - 1 &&
                      snapshot.items.length > WARRANTY_BAND_AFTER &&
                      warranties &&
                      warranties.length > 0 && <CatalogueWarrantyBand warranties={warranties} />}
                  </Fragment>
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
                    ? "Remove a filter above to see more cars, or clear them all."
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
                      Clear all
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
