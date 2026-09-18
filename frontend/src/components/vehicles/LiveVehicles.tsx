"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { buttonClassName } from "@/components/ui/Button";
import { StatusPage } from "@/components/ui/StatusPage";
import { apiFetch } from "@/lib/api-browser";
import { CATALOGUE_PAGE_SIZE, type PublicVehicle } from "@/lib/vehicles/contracts";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { catalogueDefaults, catalogueQuery } from "@/lib/vehicles/query";
import styles from "./LiveVehicles.module.css";
import { VehicleBrowseMore } from "./VehicleBrowseMore";

type LiveState =
  | { status: "loading" }
  | { status: "ready"; vehicles: readonly PublicVehicle[] }
  | { status: "failed"; code: number };

const DEFAULT_LIMIT = 10;

function cataloguePath(filters: typeof catalogueDefaults) {
  return `/api/vehicles?${catalogueQuery(filters, 1)}&pageSize=${CATALOGUE_PAGE_SIZE}`;
}

// A carousel of cars for pages that are generated ahead of time or should not wait on the catalogue. The cars are
// read in the browser once the page loads: a shell holds the row's space meanwhile, and a failed read says so in
// place of the row. With a showroom the cars there come first, then the rest of the catalogue.
export function LiveVehicles({
  title,
  branchId,
  excludeId,
  limit = DEFAULT_LIMIT,
}: {
  title: string;
  branchId?: string;
  excludeId?: string;
  limit?: number;
}) {
  const [state, setState] = useState<LiveState>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const request = new AbortController();
    async function load() {
      try {
        const reads = await Promise.all([
          ...(branchId
            ? [
                apiFetch(cataloguePath({ ...catalogueDefaults, branchId }), parseCatalogue, {
                  signal: request.signal,
                }),
              ]
            : []),
          apiFetch(cataloguePath(catalogueDefaults), parseCatalogue, { signal: request.signal }),
        ]);
        if (request.signal.aborted) {
          return;
        }
        const seen = new Set<string>();
        const vehicles = reads
          .flatMap((read) => read.data?.items ?? [])
          .filter((vehicle) => {
            if (vehicle.id === excludeId || seen.has(vehicle.id)) {
              return false;
            }
            seen.add(vehicle.id);
            return true;
          })
          .slice(0, limit);
        const failed = reads.find((read) => !read.data);
        setState(
          vehicles.length === 0 && failed
            ? { status: "failed", code: failed.status }
            : { status: "ready", vehicles },
        );
      } catch {
        // A read that was cancelled because the page changed is not a failure; any other throw is one.
        if (!request.signal.aborted) {
          setState({ status: "failed", code: 503 });
        }
      }
    }
    void load();
    return () => request.abort();
  }, [attempt, branchId, excludeId, limit]);

  function retry() {
    setState({ status: "loading" });
    setAttempt((value) => value + 1);
  }

  if (state.status === "ready") {
    return state.vehicles.length > 0 ? (
      <VehicleBrowseMore vehicles={state.vehicles} title={title} />
    ) : null;
  }

  if (state.status === "failed") {
    return (
      <div className={styles.frame}>
        <StatusPage
          compact
          tone="alert"
          code={String(state.code)}
          icon="alert-triangle"
          title="Cars unavailable"
          actions={
            <>
              <button type="button" className={buttonClassName("primary", "md")} onClick={retry}>
                Try again
              </button>
              <Link
                href="/vehicles"
                prefetch={false}
                className={buttonClassName("secondary", "md")}
              >
                See all cars
              </Link>
            </>
          }
        >
          The cars on sale could not be loaded just now.
        </StatusPage>
      </div>
    );
  }

  return (
    <>
      <p role="status" className="sr-only">
        {`Loading ${title.toLowerCase()}`}
      </p>
      <VehicleBrowseMore vehicles={[]} title={title} loading />
    </>
  );
}
