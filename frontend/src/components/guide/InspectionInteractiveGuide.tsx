"use client";

import { scrollIntoViewRespectingMotion } from "@/lib/motion";
import { useState, type ReactNode } from "react";
import type { InspectionCategorySummary } from "@/lib/guide/inspection-rules";
import type { InspectionPointItem, InspectionCategoryKey } from "@/lib/guide/inspection-points";
import { GuideBand } from "./GuidePageShell";
import { InspectionCatalogBrowser } from "./InspectionCatalogBrowser";
import { InspectionZoneShowcase } from "./InspectionZoneShowcase";

type CategoryFilter = InspectionCategoryKey | "ALL";

export function InspectionInteractiveGuide({
  gradingSection,
  points,
  categories,
}: {
  gradingSection: ReactNode;
  points: InspectionPointItem[];
  categories: InspectionCategorySummary[];
}) {
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
        heading={`${points.length} checks across ${categories.length} systems`}
        description="Inspect each vehicle system in detail, then jump directly into its checklist items."
      >
        <InspectionZoneShowcase categories={categories} onExploreCatalog={exploreZone} />
      </GuideBand>

      <GuideBand
        id="catalog"
        surface="paper"
        headingId="catalog-heading"
        heading={`Full ${points.length}-point checklist`}
        description="Search by keyword or checkpoint code, or filter by vehicle system."
      >
        <InspectionCatalogBrowser
          points={points}
          categories={categories}
          selectedCategory={catalogCategory}
          onSelectCategory={setCatalogCategory}
        />
      </GuideBand>

      {gradingSection}
    </>
  );
}
