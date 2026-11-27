"use client";

import { useEffect, useState } from "react";
import { parsePage, type ApiResult, type Page } from "@/lib/api";
import { apiFetch } from "@/lib/api-browser";
import { parseBranch, type Branch } from "@/lib/branches";
import type { Taxonomy } from "@/lib/intake/contracts";
import { parseTaxonomy } from "@/lib/intake/parse";
import { CATALOGUE_PAGE_SIZE, type CataloguePage } from "@/lib/vehicles/contracts";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { parsePublicDetail, type PublicVehicleDetail } from "@/lib/vehicles/public-detail";

export interface LandingData {
  vehicles: ApiResult<CataloguePage>;
  branches: ApiResult<Page<Branch>>;
  taxonomy: ApiResult<Taxonomy>;
  // The signed record of the newest car that still has one.
  leadDetail: PublicVehicleDetail | null;
}

const PROOF_CANDIDATE_COUNT = 3;

function unconfirmed<T>(): ApiResult<T> {
  return {
    status: 503,
    data: null,
    message: "The page could not load everything. Reload the page to try again.",
    uncertain: true,
  };
}

async function settled<T>(read: Promise<ApiResult<T>>, signal: AbortSignal): Promise<ApiResult<T>> {
  try {
    return await read;
  } catch (error) {
    if (signal.aborted) {
      throw error;
    }
    return unconfirmed<T>();
  }
}

async function readProofDetail(candidates: CataloguePage["items"], signal: AbortSignal) {
  // A car can be reserved or withdrawn between the catalogue read and this one, so the next newest is tried.
  for (const candidate of candidates.slice(0, PROOF_CANDIDATE_COUNT)) {
    const read = await settled(
      apiFetch(`/api/vehicles/${candidate.id}`, (value) => parsePublicDetail(value, candidate.id), {
        signal,
      }),
      signal,
    );
    if (read.data) {
      return read.data;
    }
  }
  return null;
}

// The home page is generated ahead of time, so everything that changes is read in the browser after it loads.
// Null means still loading; each section then shows a silhouette with its final size.
export function useLandingData(): LandingData | null {
  const [data, setData] = useState<LandingData | null>(null);

  useEffect(() => {
    const request = new AbortController();
    const { signal } = request;

    async function load() {
      try {
        const [vehicles, branches, taxonomy] = await Promise.all([
          settled(
            apiFetch(
              `/api/vehicles?page=1&sort=published&direction=desc&pageSize=${CATALOGUE_PAGE_SIZE}`,
              parseCatalogue,
              { signal },
            ),
            signal,
          ),
          settled(
            apiFetch(
              "/api/intake/branches?page=1&pageSize=25",
              (value) => parsePage(value, parseBranch),
              {
                signal,
              },
            ),
            signal,
          ),
          settled(apiFetch("/api/intake/taxonomy", parseTaxonomy, { signal }), signal),
        ]);
        const leadDetail = await readProofDetail(vehicles.data?.items ?? [], signal);
        if (!signal.aborted) {
          setData({ vehicles, branches, taxonomy, leadDetail });
        }
      } catch {
        // Cancelled because the page was left; nothing to show.
      }
    }

    void load();
    return () => request.abort();
  }, []);

  return data;
}
