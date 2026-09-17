"use client";

import { useState } from "react";
import { label } from "@/lib/inspection/contracts";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";

type ChecklistPoint = PublicVehicleDetail["checklist"]["points"][number];

export interface InspectionArea {
  category: string;
  total: number;
  findingCount: number;
  // The checkpoints that match the current search and filter.
  points: ChecklistPoint[];
  open: boolean;
}

// The signed report grouped by inspection area. Areas start closed; searching or filtering to findings
// opens every area that still has a match, so results are never hidden behind a collapsed header.
export function useVehicleInspection(checklist: PublicVehicleDetail["checklist"]) {
  const [search, setSearch] = useState("");
  const [findingsOnly, setFindingsOnly] = useState(false);
  const [opened, setOpened] = useState<ReadonlySet<string>>(new Set());
  const query = search.trim().toLocaleLowerCase("en-MY");
  const filtering = query !== "" || findingsOnly;
  const findings = checklist.points.filter((point) => point.kind === "Finding");

  function matches(point: ChecklistPoint) {
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
  }

  const categories = Array.from(new Set(checklist.points.map((point) => point.category)));
  const areas: InspectionArea[] = categories.flatMap((category) => {
    const inArea = checklist.points.filter((point) => point.category === category);
    const points = inArea.filter(matches);
    if (points.length === 0) {
      return [];
    }
    return [
      {
        category,
        total: inArea.length,
        findingCount: inArea.filter((point) => point.kind === "Finding").length,
        points,
        open: filtering || opened.has(category),
      },
    ];
  });

  function toggleArea(category: string) {
    setOpened((current) => {
      const next = new Set(current);
      if (next.has(category)) {
        next.delete(category);
      } else {
        next.add(category);
      }
      return next;
    });
  }

  function reset() {
    setSearch("");
    setFindingsOnly(false);
  }

  return {
    search,
    setSearch,
    findingsOnly,
    setFindingsOnly,
    filtering,
    findings,
    areas,
    toggleArea,
    reset,
  };
}
