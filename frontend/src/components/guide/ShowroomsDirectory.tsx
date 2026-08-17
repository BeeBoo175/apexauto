"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { GuideHubBanner } from "@/components/guide/GuidePageShell";
import { Icon } from "@/components/ui/Icon";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import {
  showroomBranches,
  type BranchRegionState,
  type ShowroomBranchInfo,
} from "@/lib/guide/showrooms";
import { catalogueDefaults, catalogueHref } from "@/lib/vehicles/query";
import { MAX_MAP_ZOOM_INDEX, ShowroomMapStage } from "./ShowroomMapStage";
import { ShowroomSlotStrip } from "./ShowroomSlotStrip";
import styles from "./GuideBranches.module.css";

type MapFocusScope = "ALL" | BranchRegionState;

const highlightedStatesByScope: Record<MapFocusScope, readonly string[]> = {
  ALL: ["Selangor", "Kuala Lumpur", "Penang"],
  Selangor: ["Selangor", "Kuala Lumpur"],
  "Pulau Pinang": ["Penang"],
};

export function ShowroomsWorkspace() {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [selangorFocused, setSelangorFocused] = useState(false);
  const [hoveredBranchId, setHoveredBranchId] = useState<string | null>(null);
  const [zoomIndex, setZoomIndex] = useState(0);

  const selectedBranch = showroomBranches.find((branch) => branch.id === selectedId) ?? null;
  let focusRegion: MapFocusScope = "ALL";
  if (selectedBranch !== null) {
    focusRegion = selectedBranch.state;
  } else if (selangorFocused) {
    focusRegion = "Selangor";
  }

  const activeStates = useMemo(() => new Set(highlightedStatesByScope[focusRegion]), [focusRegion]);
  const canReset = selectedBranch !== null || selangorFocused || zoomIndex > 0;

  function selectBranch(branch: ShowroomBranchInfo) {
    if (selectedId === branch.id) {
      setSelectedId(null);
      setSelangorFocused(false);
      setZoomIndex(0);
      return;
    }
    setSelectedId(branch.id);
    setSelangorFocused(false);
    setZoomIndex(0);
  }

  function resetMapView() {
    setSelectedId(null);
    setSelangorFocused(false);
    setZoomIndex(0);
  }

  return (
    <>
      <GuideHubBanner
        title="Our showrooms"
        description="Open Monday to Saturday, 09:00–18:00 MYT."
        actions={[
          { href: "/vehicles", label: "Browse showroom stock", variant: "primary" },
          { href: "/sell", label: "Book seller appraisal", variant: "shell" },
        ]}
      />

      <section
        className={styles.workspaceBand}
        aria-label="Showroom directory and appraisal schedule"
      >
        <div className={layout.content}>
          <div className={styles.workspaceGrid}>
            <div className={styles.leftColumn}>
              <ul className={styles.branchList} aria-label="ApexAuto showrooms">
                {showroomBranches.map((branch) => {
                  const isSelected = branch.id === selectedId;
                  const cardClasses = isSelected
                    ? `${styles.branchCard} ${styles.branchCardSelected}`
                    : styles.branchCard;

                  return (
                    <li
                      key={branch.id}
                      className={cardClasses}
                      onMouseEnter={() => setHoveredBranchId(branch.id)}
                      onMouseLeave={() => setHoveredBranchId(null)}
                    >
                      <div className={styles.branchCardBody}>
                        <h2 className={styles.branchName}>
                          <button
                            type="button"
                            aria-pressed={isSelected}
                            className={styles.branchSelectButton}
                            onFocus={() => setHoveredBranchId(branch.id)}
                            onBlur={() => setHoveredBranchId(null)}
                            onClick={() => selectBranch(branch)}
                          >
                            {branch.name}
                          </button>
                        </h2>
                        <p className={styles.branchAddress}>
                          {branch.address}, {branch.city}, {branch.state}
                        </p>
                      </div>

                      <div className={styles.branchCardFooter}>
                        <Link
                          href={catalogueHref({ ...catalogueDefaults, branchId: branch.id }, 1)}
                          prefetch={false}
                          className={`${layout.arrowLink} ${styles.branchCatalogLink}`}
                        >
                          <span>Browse {branch.shortLabel} cars</span>
                          <Icon name="next" />
                        </Link>
                      </div>
                    </li>
                  );
                })}
              </ul>

              <ShowroomSlotStrip />
            </div>

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
              onSelectBranch={selectBranch}
              onFocusSelangor={() => {
                setSelangorFocused(true);
                setZoomIndex(0);
              }}
            />
          </div>
        </div>
      </section>
    </>
  );
}
