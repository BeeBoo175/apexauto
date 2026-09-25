"use client";

import { useState } from "react";
import Image from "next/image";
import patterns from "@/components/layout/PublicPatterns.module.css";
import { PhotoAccordion, PhotoCollapsedName, PhotoPanel } from "@/components/ui/PhotoAccordion";
import { useShowroomStock } from "@/hooks/use-showroom-stock";
import type { Branch } from "@/lib/branches";
import { showroomPhoto } from "@/lib/guide/showroom-photos";
import {
  SHOWROOM_OPERATING_HOURS,
  resolveShowrooms,
  type ShowroomBranchInfo,
} from "@/lib/guide/showrooms";
import { SHOWROOM_CLOSES_AT, SHOWROOM_OPENS_AT } from "@/lib/guide/visit-terms";
import styles from "./LandingShowrooms.module.css";
import { ShowroomVisitDetails } from "./ShowroomVisitDetails";
import layout from "./VehicleLayout.module.css";

interface LandingShowroomsProps {
  branches: Branch[] | null;
  // Shows the accordion with placeholder text until the showrooms have loaded.
  loading?: boolean;
}

// The directory always lists the same three showrooms, so the silhouette has their count.
const SHELL_SHOWROOMS: ShowroomBranchInfo[] = [0, 1, 2].map((index) => ({
  id: `shell-${index}`,
  name: "Showroom name",
  shortLabel: "Showroom",
  address: "00 Street address line",
  city: "City",
  state: "Selangor",
  hours: SHOWROOM_OPERATING_HOURS,
  lat: 0,
  lng: 0,
}));

function showroomNumber(index: number) {
  return String(index + 1).padStart(2, "0");
}

export function LandingShowrooms({ branches, loading = false }: LandingShowroomsProps) {
  const showrooms = loading ? SHELL_SHOWROOMS : resolveShowrooms(branches ?? []);
  const [chosenId, setChosenId] = useState("");
  // Each showroom's own catalogue total, not a count over the first page of the newest cars.
  const { stock } = useShowroomStock(loading ? [] : showrooms);
  // The first showroom is open until one is chosen, including when the list arrives after the first render.
  const activeId = showrooms.some((showroom) => showroom.id === chosenId)
    ? chosenId
    : (showrooms[0]?.id ?? "");

  if (showrooms.length === 0) {
    return null;
  }

  return (
    <section
      className={`${patterns.band} ${patterns.bandCanvas}`}
      aria-labelledby="landing-showrooms-heading"
      data-shell={loading || undefined}
    >
      <div className={layout.content}>
        <div className={styles.heading}>
          <h2 id="landing-showrooms-heading" className={patterns.sectionTitle}>
            Come visit us
          </h2>
          <p className={patterns.sectionSub}>
            Open Mon–Sat, {SHOWROOM_OPENS_AT}–{SHOWROOM_CLOSES_AT} for test drives and trade-ins.
          </p>
        </div>

        <PhotoAccordion loading={loading}>
          {showrooms.map((showroom, index) => {
            const active = showroom.id === activeId;
            const photo = showroomPhoto(showroom.shortLabel);
            const stockCount = stock[showroom.id] ?? 0;
            return (
              <PhotoPanel
                key={showroom.id}
                active={active}
                photo={
                  !loading && (
                    <Image
                      src={photo.src}
                      alt={active ? photo.alt : ""}
                      fill
                      unoptimized
                      sizes="900px"
                    />
                  )
                }
                trigger={
                  <PhotoCollapsedName>
                    {showroomNumber(index)} {showroom.shortLabel}
                  </PhotoCollapsedName>
                }
                triggerProps={{
                  "aria-label": `${showroomNumber(index)} ${showroom.shortLabel} showroom`,
                  disabled: loading,
                  onClick: () => setChosenId(showroom.id),
                }}
              >
                <ShowroomVisitDetails
                  title={
                    <>
                      <span>{showroomNumber(index)} /</span> {showroom.shortLabel}
                    </>
                  }
                  address={`${showroom.address}, ${showroom.city} • ${showroom.hours}${
                    stockCount > 0 ? ` • ${stockCount} cars on display` : ""
                  }`}
                  href={`/branches?branchId=${showroom.id}`}
                  loading={loading}
                />
              </PhotoPanel>
            );
          })}
        </PhotoAccordion>
      </div>
    </section>
  );
}
