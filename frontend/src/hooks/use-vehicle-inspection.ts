"use client";

import { useState } from "react";
import { label } from "@/lib/inspection/contracts";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";

export function useVehicleInspection(checklist: PublicVehicleDetail["checklist"]) {
  const [category, setCategory] = useState("all");
  const [search, setSearch] = useState("");
  const [findingsOnly, setFindingsOnly] = useState(false);
  const query = search.trim().toLocaleLowerCase("en-MY");
  const categories = Array.from(new Set(checklist.points.map((point) => point.category)));
  const findings = checklist.points.filter((point) => point.kind === "Finding");
  const visible = new Set(
    checklist.points
      .filter((point) => {
        if (category !== "all" && category !== point.category) {
          return false;
        }
        if (findingsOnly && point.kind !== "Finding") {
          return false;
        }
        const text = [
          point.code,
          point.description,
          label(point.category),
          point.severity && label(point.severity),
          point.kind && label(point.kind),
        ].join(" ");
        return text.toLocaleLowerCase("en-MY").includes(query);
      })
      .map((point) => point.code),
  );

  function reset() {
    setCategory("all");
    setSearch("");
    setFindingsOnly(false);
  }

  return {
    category,
    setCategory,
    search,
    setSearch,
    findingsOnly,
    setFindingsOnly,
    categories,
    findings,
    visible,
    reset,
  };
}
