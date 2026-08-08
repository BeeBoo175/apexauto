"use client";

import { LoadingProgressReady } from "@/components/ui/LoadingProgress";
import { useInitialToast } from "@/hooks/use-initial-toast";
import { useVehicleTradeIn } from "@/hooks/use-vehicle-trade-in";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { Taxonomy } from "@/lib/intake/contracts";
import type { CataloguePage } from "@/lib/vehicles/contracts";
import { LandingFeaturedVehicles } from "./LandingFeaturedVehicles";
import { LandingHero } from "./LandingHero";
import { LandingInspectionOverview } from "./LandingInspectionOverview";
import { LandingInteractiveTools } from "./LandingInteractiveTools";
import { LandingSellingAndBranches } from "./LandingSellingAndBranches";

export function LandingStorefront({
  initialVehicles,
  initialBranches,
  initialTaxonomy,
}: {
  initialVehicles: ApiResult<CataloguePage>;
  initialBranches: ApiResult<Page<Branch>>;
  initialTaxonomy: ApiResult<Taxonomy>;
}) {
  const initialError =
    (!initialVehicles.data && initialVehicles.message) ||
    (!initialTaxonomy.data && initialTaxonomy.message) ||
    (!initialBranches.data && initialBranches.message) ||
    null;

  useInitialToast(initialError);

  const tradeIn = useVehicleTradeIn(initialTaxonomy.data ?? null);
  const branches = initialBranches.data?.items ?? null;
  const featuredVehicles = initialVehicles.data?.items ?? [];
  const totalVehicles = initialVehicles.data?.total ?? null;

  function handleStartTradeInMake(make: string) {
    if (make) {
      tradeIn.changeDraft({
        ...tradeIn.draft,
        make,
        model: "",
        trim: "",
      });
    }
    if (!tradeIn.editing) {
      tradeIn.startNewValuation();
    }
    const target = document.getElementById("trade-in-calculator");
    if (target) {
      const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
      target.scrollIntoView({ behavior: reducedMotion ? "auto" : "smooth", block: "start" });
    }
  }

  return (
    <>
      <LoadingProgressReady />
      <LandingHero
        taxonomy={tradeIn.taxonomy ?? initialTaxonomy.data}
        branches={branches}
        totalVehicles={totalVehicles}
        onStartTradeInMake={handleStartTradeInMake}
      />
      <LandingFeaturedVehicles initial={initialVehicles} />
      <LandingInspectionOverview />
      <LandingInteractiveTools tradeIn={tradeIn} featuredVehicles={featuredVehicles} />
      <LandingSellingAndBranches branches={branches} />
    </>
  );
}
