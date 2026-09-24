"use client";

import { useMemo, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useAddressParam } from "@/hooks/use-address-param";
import { useShowroomOpenStatus } from "@/hooks/use-showroom-open-status";
import { useShowroomStock } from "@/hooks/use-showroom-stock";
import patterns from "@/components/layout/PublicPatterns.module.css";
import { buttonClassName } from "@/components/ui/Button";
import { LiveVehicles } from "@/components/vehicles/LiveVehicles";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { showroomPhoto } from "@/lib/guide/showroom-photos";
import type { BranchRegionState, ShowroomBranchInfo } from "@/lib/guide/showrooms";
import { SHOWROOM_CLOSES_AT, SHOWROOM_OPENS_AT } from "@/lib/guide/visit-terms";
import { catalogueDefaults, catalogueHref } from "@/lib/vehicles/query";
import { MAX_MAP_ZOOM_INDEX, ShowroomMapStage } from "./ShowroomMapStage";
import { ShowroomVisitBooking } from "./ShowroomVisitBooking";
import styles from "./ShowroomsWorkspace.module.css";

type MapFocusScope = "ALL" | BranchRegionState;

const highlightedStatesByScope: Record<MapFocusScope, readonly string[]> = {
  ALL: ["Selangor", "Kuala Lumpur", "Penang"],
  Selangor: ["Selangor", "Kuala Lumpur"],
  "Pulau Pinang": ["Penang"],
};

export function ShowroomsWorkspace({
  showroomBranches,
}: {
  showroomBranches: readonly ShowroomBranchInfo[];
}) {
  // The page is generated ahead of time, so a link's showroom is read from the address in the browser.
  const addressedId = useAddressParam("branchId");
  const [chosenId, setChosenId] = useState<string | null>(null);
  const addressed = showroomBranches.find((branch) => branch.id === addressedId);
  const panelId = chosenId ?? addressed?.id ?? showroomBranches[0]?.id ?? "";
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [selangorFocused, setSelangorFocused] = useState(false);
  const [hoveredBranchId, setHoveredBranchId] = useState<string | null>(null);
  const [zoomIndex, setZoomIndex] = useState(0);
  // Null until the page is running, so the server and the first browser render agree; the status line reserves its
  // space.
  const openStatus = useShowroomOpenStatus();
  const { ready, stock } = useShowroomStock(showroomBranches);

  const panel = showroomBranches.find((branch) => branch.id === panelId) ?? showroomBranches[0];
  const selectedBranch = showroomBranches.find((branch) => branch.id === selectedId) ?? null;
  let focusRegion: MapFocusScope = "ALL";
  if (selectedBranch !== null) {
    focusRegion = selectedBranch.state;
  } else if (selangorFocused) {
    focusRegion = "Selangor";
  }

  const activeStates = useMemo(() => new Set(highlightedStatesByScope[focusRegion]), [focusRegion]);
  const canReset = selectedBranch !== null || selangorFocused || zoomIndex > 0;

  if (!panel) {
    return null;
  }

  // A panel opens its showroom and points the map at it.
  function openBranch(branch: ShowroomBranchInfo) {
    setChosenId(branch.id);
    setSelectedId(branch.id);
    setSelangorFocused(false);
    setZoomIndex(0);
  }

  // A pin chooses a showroom too; pressing the focused pin again returns to the peninsula view.
  function chooseBranch(branch: ShowroomBranchInfo) {
    setChosenId(branch.id);
    setSelangorFocused(false);
    setZoomIndex(0);
    setSelectedId(selectedId === branch.id ? null : branch.id);
  }

  function resetMapView() {
    setSelectedId(null);
    setSelangorFocused(false);
    setZoomIndex(0);
  }

  return (
    <>
      <section className={patterns.hero} aria-labelledby="showrooms-title">
        <Image
          src="/images/hero-night-showroom.jpg"
          alt=""
          fill
          priority
          sizes="100vw"
          className={patterns.heroPhoto}
        />
        <div className={patterns.heroScrim} aria-hidden="true" />
        <div className={`${layout.content} ${patterns.heroInner} ${patterns.rise}`}>
          <h1 id="showrooms-title" className={layout.heroHeading}>
            Our showrooms
          </h1>
          <p className={patterns.heroLede}>
            {showroomBranches.length} showrooms in Selangor and Penang, open Monday to Saturday from
            {SHOWROOM_OPENS_AT} to {SHOWROOM_CLOSES_AT}. Every car on the floor has a signed
            inspection record.
          </p>
          <div className={patterns.heroActions}>
            <a href="#book-a-visit" className={buttonClassName("primary", "lg")}>
              Book a visit
            </a>
          </div>
        </div>
      </section>

      <section
        className={`${patterns.band} ${patterns.bandPaper}`}
        aria-labelledby="showrooms-find-heading"
      >
        <div className={layout.content}>
          <h2 id="showrooms-find-heading" className={`${layout.sectionHeading} ${styles.heading}`}>
            Find a showroom
          </h2>

          <div className={styles.workspace}>
            <ul className={styles.stack}>
              {showroomBranches.map((branch) => {
                const active = branch.id === panel.id;
                const photo = showroomPhoto(branch.shortLabel);
                const count = stock[branch.id];
                return (
                  <li key={branch.id} className={styles.panel} data-active={active}>
                    <div className={styles.photo}>
                      <Image
                        src={photo.src}
                        alt={active ? photo.alt : ""}
                        fill
                        unoptimized
                        sizes="(max-width: 960px) 100vw, 640px"
                        className={styles.image}
                      />
                    </div>
                    <div className={styles.scrim} aria-hidden="true" />
                    <button
                      type="button"
                      className={styles.trigger}
                      aria-expanded={active}
                      aria-label={`${branch.shortLabel} showroom`}
                      onClick={() => openBranch(branch)}
                      onMouseEnter={() => setHoveredBranchId(branch.id)}
                      onMouseLeave={() => setHoveredBranchId(null)}
                    >
                      <span className={styles.collapsedName} aria-hidden="true">
                        {branch.shortLabel}
                      </span>
                    </button>
                    <div className={styles.details} aria-hidden={!active}>
                      <h3 className={styles.name}>{branch.name}</h3>
                      <p className={styles.status} data-open={openStatus?.open ?? false}>
                        {openStatus ? (
                          <>
                            <span className={styles.lamp} aria-hidden="true" />
                            {openStatus.label}
                          </>
                        ) : (
                          <>
                            <span
                              className={`${styles.lamp} ${styles.lampShell}`}
                              aria-hidden="true"
                            />
                            <span className={styles.shell} aria-hidden="true">
                              Open now, closes at {SHOWROOM_CLOSES_AT}
                            </span>
                          </>
                        )}
                      </p>
                      <p className={styles.address}>
                        {branch.address}, {branch.city}, {branch.state}
                      </p>
                      <p className={styles.hours}>
                        Mon – Sat, {SHOWROOM_OPENS_AT} – {SHOWROOM_CLOSES_AT}
                        {count !== undefined && ` • ${count} cars on the floor`}
                        {count === undefined && !ready && (
                          <>
                            {" • "}
                            <span className={styles.shell} aria-hidden="true">
                              00 cars on the floor
                            </span>
                          </>
                        )}
                      </p>
                      <div className={styles.actions}>
                        <Link
                          href={catalogueHref({ ...catalogueDefaults, branchId: branch.id })}
                          prefetch={false}
                          tabIndex={active ? undefined : -1}
                          className={styles.primary}
                        >
                          See the cars here
                        </Link>
                        <a
                          href={`https://www.google.com/maps/dir/?api=1&destination=${branch.lat},${branch.lng}`}
                          target="_blank"
                          rel="noopener noreferrer"
                          tabIndex={active ? undefined : -1}
                          className={styles.secondary}
                        >
                          Get directions
                        </a>
                      </div>
                    </div>
                  </li>
                );
              })}
            </ul>

            <div className={styles.map}>
              <ShowroomMapStage
                activeStates={activeStates}
                focusRegion={focusRegion}
                branches={showroomBranches}
                selectedBranch={selectedBranch}
                hoveredBranchId={hoveredBranchId}
                zoomIndex={zoomIndex}
                canReset={canReset}
                onHoverBranch={setHoveredBranchId}
                onZoomIn={() => setZoomIndex((z) => Math.min(MAX_MAP_ZOOM_INDEX, z + 1))}
                onZoomOut={() => setZoomIndex((z) => Math.max(0, z - 1))}
                onReset={resetMapView}
                onSelectBranch={chooseBranch}
                onFocusSelangor={() => {
                  setSelangorFocused(true);
                  setZoomIndex(0);
                }}
              />
            </div>
          </div>
        </div>
      </section>
      <section
        id="book-a-visit"
        className={`${patterns.band} ${styles.bookBand}`}
        aria-labelledby="showrooms-booking-heading"
      >
        <div className={layout.content}>
          <div className={patterns.sectionHead}>
            <h2 id="showrooms-booking-heading" className={layout.sectionHeading}>
              Book a visit
            </h2>
            <p className={patterns.lede}>
              At {panel.name}. To change the showroom, open another one above.
            </p>
          </div>
          <ShowroomVisitBooking showroom={panel} />
        </div>
      </section>

      <section
        className={`${patterns.band} ${patterns.bandPaper}`}
        aria-label={`Cars at ${panel.shortLabel}`}
      >
        <div className={layout.content}>
          <LiveVehicles
            key={panel.id}
            title={`On the floor at ${panel.shortLabel}`}
            branchId={panel.id}
          />
        </div>
      </section>
    </>
  );
}
