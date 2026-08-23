"use client";

import { useEffect, useRef, useState } from "react";
import { StatusPage } from "@/components/ui/StatusPage";
import Link from "next/link";
import { Button, buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import type { CataloguePage, PublicVehicle } from "@/lib/vehicles/contracts";
import {
  FEATURED_QUICK_FILTERS,
  selectDiverseVehicles,
  type FeaturedQuickFilter,
} from "@/lib/vehicles/featured";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { catalogueHref, catalogueQuery } from "@/lib/vehicles/query";
import { VehicleCard } from "./VehicleCard";
import styles from "./LandingFeaturedVehicles.module.css";
import layout from "./VehicleLayout.module.css";

export function LandingFeaturedVehicles({ initial }: { initial: ApiResult<CataloguePage> }) {
  const [snapshot, setSnapshot] = useState<CataloguePage | null>(initial.data);
  const [activeId, setActiveId] = useState("all");
  const [busy, setBusy] = useState(false);
  const [photoVersion, setPhotoVersion] = useState(0);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();

  useEffect(
    () => () => {
      controller.current?.abort();
    },
    [],
  );

  const activeChip =
    FEATURED_QUICK_FILTERS.find((item) => item.id === activeId) ?? FEATURED_QUICK_FILTERS[0];
  const browseTargetHref = catalogueHref(activeChip.filters);

  let featuredItems: PublicVehicle[] = [];
  if (snapshot) {
    featuredItems =
      activeId === "all" ? selectDiverseVehicles(snapshot.items, 4) : snapshot.items.slice(0, 4);
  }

  async function selectFilter(chip: FeaturedQuickFilter) {
    if (chip.id === "all" && initial.data) {
      controller.current?.abort();
      controller.current = null;
      setBusy(false);
      setActiveId("all");
      setSnapshot(initial.data);
      return;
    }

    controller.current?.abort();
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);

    try {
      const query = catalogueQuery(chip.filters, 1);
      const result = await apiFetch(`/api/vehicles?${query}`, parseCatalogue, {
        signal: attempt.signal,
      });
      if (attempt.signal.aborted || controller.current !== attempt) {
        return;
      }
      if (!result.data) {
        throw new Error("Featured inventory unavailable");
      }
      setSnapshot(result.data);
      setPhotoVersion((current) => current + 1);
      setActiveId(chip.id);
    } catch {
      if (!attempt.signal.aborted && controller.current === attempt) {
        error("Could not refresh featured cars right now. Showing your previous list.");
      }
    } finally {
      if (!attempt.signal.aborted && controller.current === attempt) {
        setBusy(false);
        controller.current = null;
      }
    }
  }

  return (
    <section
      className={`${layout.band} ${styles.section}`}
      aria-labelledby="featured-vehicles-heading"
    >
      <LoadingProgress label="Loading featured vehicles" active={busy} />
      <div className={`${layout.content} ${styles.inner}`}>
        <header className={styles.header}>
          <div className={styles.heading}>
            <h2 id="featured-vehicles-heading" className={layout.sectionHeading}>
              Inspected cars ready to reserve
            </h2>
            <p>Verified mileage, full photo galleries, and upfront MYR pricing.</p>
          </div>
          <Link href={browseTargetHref} prefetch={false} className={layout.arrowLink}>
            {snapshot
              ? `View all ${snapshot.total} matching cars`
              : "Browse full vehicle catalogue"}
            <Icon name="next" />
          </Link>
        </header>

        <div className={styles.chips} role="group" aria-label="Filter featured inventory">
          {FEATURED_QUICK_FILTERS.map((chip) => {
            const active = activeId === chip.id;
            return (
              <Button
                key={chip.id}
                type="button"
                variant={active ? "primary" : "secondary"}
                size="compact"
                aria-pressed={active}
                disabled={busy}
                onClick={() => void selectFilter(chip)}
              >
                {chip.label}
              </Button>
            );
          })}
        </div>

        <div aria-busy={busy}>
          {snapshot && featuredItems.length > 0 && (
            <>
              <p className={styles.status} aria-live="polite">
                Showing {featuredItems.length} of {snapshot.total} available vehicles
              </p>
              <div className={styles.grid}>
                {featuredItems.map((vehicle, index) => (
                  <VehicleCard
                    key={vehicle.id}
                    vehicle={vehicle}
                    first={index === 0}
                    photoVersion={photoVersion}
                  />
                ))}
              </div>
            </>
          )}

          {snapshot && featuredItems.length === 0 && (
            <div className={styles.emptyCard}>
              <h3>No available vehicles match {activeChip.label}</h3>
              <p>
                Showroom stock updates continuously as inspections complete. Try another category or
                search the full catalogue.
              </p>
              <div className={styles.emptyActions}>
                <Button
                  type="button"
                  variant="primary"
                  onClick={() => void selectFilter(FEATURED_QUICK_FILTERS[0])}
                >
                  Show all featured cars
                </Button>
                <Link
                  href="/vehicles"
                  prefetch={false}
                  className={buttonClassName("secondary", "md")}
                >
                  Browse full catalogue
                </Link>
              </div>
            </div>
          )}

          {!snapshot && (
            <StatusPage
              tone="alert"
              code="503"
              icon="alert-triangle"
              title="Featured cars could not be loaded"
              actions={
                <Link
                  href="/vehicles"
                  prefetch={false}
                  className={buttonClassName("secondary", "md")}
                >
                  Open vehicle catalogue
                </Link>
              }
            >
              Reload the page to try again, or continue to the vehicle catalogue.
            </StatusPage>
          )}
        </div>
      </div>
    </section>
  );
}
