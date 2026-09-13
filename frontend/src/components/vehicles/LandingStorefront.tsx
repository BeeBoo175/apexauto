"use client";

import { useInitialToast } from "@/hooks/use-initial-toast";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { Taxonomy } from "@/lib/intake/contracts";
import type { CataloguePage } from "@/lib/vehicles/contracts";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import { LandingBuyingSteps } from "./LandingBuyingSteps";
import { LandingFeaturedVehicles } from "./LandingFeaturedVehicles";
import { LandingHero } from "./LandingHero";
import { LandingInspectionRecord } from "./LandingInspectionRecord";
import { LandingSellPanel } from "./LandingSellPanel";
import { LandingShowrooms } from "./LandingShowrooms";

export function LandingStorefront({
  initialVehicles,
  initialBranches,
  initialTaxonomy,
  leadDetail,
}: {
  initialVehicles: ApiResult<CataloguePage>;
  initialBranches: ApiResult<Page<Branch>>;
  initialTaxonomy: ApiResult<Taxonomy>;
  leadDetail: PublicVehicleDetail | null;
}) {
  const initialError =
    (!initialVehicles.data && initialVehicles.message) ||
    (!initialTaxonomy.data && initialTaxonomy.message) ||
    (!initialBranches.data && initialBranches.message) ||
    null;

  useInitialToast(initialError);

  const branches = initialBranches.data?.items ?? null;
  const vehicles = initialVehicles.data?.items ?? [];
  const totalVehicles = initialVehicles.data?.total ?? null;
  // The proof record says "above" only when it describes the lead card.
  const leadIsProof = leadDetail !== null && leadDetail.listing.id === vehicles[0]?.id;

  return (
    <>
      <LandingHero
        taxonomy={initialTaxonomy.data ?? null}
        branches={branches}
        totalVehicles={totalVehicles}
      />
      <LandingFeaturedVehicles initial={initialVehicles} />
      <LandingBuyingSteps />
      <LandingInspectionRecord detail={leadDetail} isLeadCar={leadIsProof} />
      <LandingSellPanel taxonomy={initialTaxonomy.data} />
      <LandingShowrooms branches={branches} vehicles={vehicles} />
    </>
  );
}
