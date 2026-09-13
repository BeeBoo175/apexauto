import type { Metadata } from "next";
import { LandingStorefront } from "@/components/vehicles/LandingStorefront";
import { parsePage } from "@/lib/api";
import { parseBranch } from "@/lib/branches";
import { parseTaxonomy } from "@/lib/intake/parse";
import { requestApiJson } from "@/lib/server/http/api";
import { CATALOGUE_PAGE_SIZE, type CataloguePage } from "@/lib/vehicles/contracts";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { parsePublicDetail } from "@/lib/vehicles/public-detail";

import { RESERVATION_HOLD_HOURS } from "@/lib/vehicles/deposit-terms";

export const dynamic = "force-dynamic";

const PROOF_CANDIDATE_COUNT = 3;

export const metadata: Metadata = {
  title: "Buy Inspected Used Cars & Sell Online | ApexAuto Malaysia",
  description: `Browse 210-point inspected used cars across ApexAuto Malaysian showrooms, estimate monthly repayments, value your trade-in and reserve online for ${RESERVATION_HOLD_HOURS} hours.`,
};

export default async function Home() {
  const [vehicles, branches, taxonomy] = await Promise.all([
    requestApiJson(
      `/api/vehicles?page=1&sort=published&direction=desc&pageSize=${CATALOGUE_PAGE_SIZE}`,
      parseCatalogue,
    ),
    requestApiJson("/api/branches?page=1&pageSize=25", (value) => parsePage(value, parseBranch)),
    requestApiJson("/api/taxonomy", parseTaxonomy),
  ]);

  return (
    <LandingStorefront
      initialVehicles={vehicles}
      initialBranches={branches}
      initialTaxonomy={taxonomy}
      leadDetail={await readProofDetail(vehicles.data?.items ?? [])}
    />
  );
}

// The newest car's signed record is the proof section. A car can be reserved or withdrawn between the catalogue
// read and this one, so the next newest is tried before the section is dropped.
async function readProofDetail(candidates: CataloguePage["items"]) {
  for (const candidate of candidates.slice(0, PROOF_CANDIDATE_COUNT)) {
    const read = await requestApiJson(`/api/vehicles/${candidate.id}`, (value) =>
      parsePublicDetail(value, candidate.id),
    );
    if (read.data) {
      return read.data;
    }
  }
  return null;
}
