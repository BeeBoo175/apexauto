"use client";

import { scrollIntoViewRespectingMotion } from "@/lib/motion";
import { useState, type ReactNode } from "react";
import type { InspectionCategoryKey } from "@/lib/guide/inspection-points";
import { GuideBand } from "./GuidePageShell";
import { InspectionCatalogBrowser } from "./InspectionCatalogBrowser";
import { InspectionZoneShowcase } from "./InspectionZoneShowcase";

type CategoryFilter = InspectionCategoryKey | "ALL";

export function InspectionInteractiveGuide({ gradingSection }: { gradingSection: ReactNode }) {
  const [catalogCategory, setCatalogCategory] = useState<CategoryFilter>("FrameChassis");

  function exploreZone(category: InspectionCategoryKey) {
    setCatalogCategory(category);
    const target = document.getElementById("catalog");
    if (target) {
      scrollIntoViewRespectingMotion(target, { block: "start" });
    }
  }

  return (
    <>
      <GuideBand
        id="zones"
        surface="canvas"
        headingId="zones-heading"
        heading="210 checks across five systems"
        description="Inspect each vehicle system in detail, then jump directly into its checklist items."
      >
        <InspectionZoneShowcase onExploreCatalog={exploreZone} />
      </GuideBand>

      <GuideBand
        id="catalog"
        surface="paper"
        headingId="catalog-heading"
        heading="Full 210-point checklist"
        description="Search by keyword or checkpoint code, or filter by vehicle system."
      >
        <InspectionCatalogBrowser
          selectedCategory={catalogCategory}
          onSelectCategory={setCatalogCategory}
        />
      </GuideBand>

      {gradingSection}
    </>
  );
}
