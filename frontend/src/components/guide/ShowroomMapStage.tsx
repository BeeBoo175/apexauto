"use client";

import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { useAnimatedViewBox } from "@/hooks/use-animated-viewbox";
import {
  MALAYSIA_MAP_VIEWS,
  peninsulaCityLabels,
  peninsulaStateShapes,
  projectMalaysiaCoordinate,
  type MapViewPreset,
} from "@/lib/guide/malaysia-map";
import type { BranchRegionState, ShowroomBranchInfo } from "@/lib/guide/showrooms";
import { catalogueDefaults, catalogueHref } from "@/lib/vehicles/query";
import styles from "./ShowroomMapStage.module.css";

const ZOOM_STEPS = [1, 1.5, 2.25] as const;
const GRID_COORDS_X = [160, 320, 480, 640, 800, 960] as const;
const GRID_COORDS_Y = [160, 280, 400, 520, 640] as const;

export const MAX_MAP_ZOOM_INDEX = ZOOM_STEPS.length - 1;

function computeTargetViewBox(
  baseView: MapViewPreset,
  selectedBranch: ShowroomBranchInfo | null,
  zoomIndex: number,
): MapViewPreset {
  const scale = ZOOM_STEPS[zoomIndex] ?? 1;
  const width = baseView.width / scale;
  const height = baseView.height / scale;

  if (!selectedBranch) {
    const cx = baseView.x + baseView.width / 2;
    const cy = baseView.y + baseView.height / 2;
    return { x: cx - width / 2, y: cy - height / 2, width, height };
  }

  const point = projectMalaysiaCoordinate(selectedBranch.lng, selectedBranch.lat);
  return {
    x: point.x - width * 0.5,
    y: point.y - height * 0.5,
    width,
    height,
  };
}

export function ShowroomMapStage({
  activeStates,
  focusRegion,
  branches,
  selectedBranch,
  hoveredBranchId,
  zoomIndex,
  canReset,
  onHoverBranch,
  onZoomIn,
  onZoomOut,
  onReset,
  onSelectBranch,
  onFocusSelangor,
}: {
  activeStates: ReadonlySet<string>;
  focusRegion: "ALL" | BranchRegionState;
  branches: readonly ShowroomBranchInfo[];
  selectedBranch: ShowroomBranchInfo | null;
  hoveredBranchId: string | null;
  zoomIndex: number;
  canReset: boolean;
  onHoverBranch: (id: string | null) => void;
  onZoomIn: () => void;
  onZoomOut: () => void;
  onReset: () => void;
  onSelectBranch: (branch: ShowroomBranchInfo) => void;
  onFocusSelangor: () => void;
}) {
  const baseView = MALAYSIA_MAP_VIEWS[focusRegion];
  const targetView = computeTargetViewBox(baseView, selectedBranch, zoomIndex);
  const currentView = useAnimatedViewBox(targetView);
  const pinScale = Math.max(0.11, currentView.width / 760);
  const labelScale = Math.max(0.11, currentView.width / 780);
  const clusterSelangor = focusRegion === "ALL" && zoomIndex === 0;
  const selangorBranches = branches.filter((b) => b.state === "Selangor");
  const unclusteredBranches = clusterSelangor
    ? branches.filter((b) => b.state !== "Selangor")
    : branches;

  const selangorCentroid =
    selangorBranches.length > 0
      ? projectMalaysiaCoordinate(
          selangorBranches.reduce((sum, b) => sum + b.lng, 0) / selangorBranches.length,
          selangorBranches.reduce((sum, b) => sum + b.lat, 0) / selangorBranches.length,
        )
      : null;

  let headerTitle = "Peninsular Malaysia";
  let headerSubtitle = "Selangor and Pulau Pinang";
  if (selectedBranch) {
    headerTitle = selectedBranch.name;
    headerSubtitle = `${selectedBranch.city}, ${selectedBranch.state}`;
  } else if (focusRegion !== "ALL") {
    headerTitle = focusRegion;
    headerSubtitle = `${selangorBranches.length} showrooms`;
  }

  return (
    <aside className={styles.mapFrame} aria-label="Showroom locator map">
      <div className={styles.mapTopBar}>
        <div className={styles.mapBarHeading}>
          <span className={styles.mapBarTitle}>{headerTitle}</span>
          <span className={styles.mapBarSub}>{headerSubtitle}</span>
        </div>

        <div className={styles.zoomControls} role="group" aria-label="Map zoom controls">
          <button
            type="button"
            className={styles.zoomButton}
            aria-label="Zoom in"
            disabled={zoomIndex >= MAX_MAP_ZOOM_INDEX}
            onClick={onZoomIn}
          >
            +
          </button>
          <button
            type="button"
            className={styles.zoomButton}
            aria-label="Zoom out"
            disabled={zoomIndex === 0}
            onClick={onZoomOut}
          >
            −
          </button>
          <button
            type="button"
            className={styles.zoomButton}
            disabled={!canReset}
            onClick={onReset}
          >
            Reset
          </button>
        </div>
      </div>

      <div className={styles.mapCanvasWrap}>
        <svg
          width={820}
          height={530}
          className={styles.mapSvg}
          viewBox={`${currentView.x} ${currentView.y} ${currentView.width} ${currentView.height}`}
          preserveAspectRatio="xMidYMid meet"
          role="img"
          aria-label={`Peninsular Malaysia map showing ${headerTitle}`}
        >
          {GRID_COORDS_X.map((gx) => (
            <line
              key={`gx-${gx}`}
              x1={gx}
              y1={-40}
              x2={gx}
              y2={820}
              className={styles.mapGridLine}
            />
          ))}
          {GRID_COORDS_Y.map((gy) => (
            <line
              key={`gy-${gy}`}
              x1={-160}
              y1={gy}
              x2={1160}
              y2={gy}
              className={styles.mapGridLine}
            />
          ))}

          {peninsulaStateShapes.map((shape) => (
            <path
              key={shape.name}
              d={shape.d}
              className={
                activeStates.has(shape.name)
                  ? `${styles.mapState} ${styles.mapStateActive}`
                  : styles.mapState
              }
            />
          ))}

          {peninsulaCityLabels
            .filter((city) => city.scope === focusRegion)
            .map((city) => (
              <text
                key={`${city.scope}-${city.name}`}
                x={city.x}
                y={city.y}
                fontSize={12 * labelScale}
                className={styles.mapLabel}
              >
                {city.name}
              </text>
            ))}

          {clusterSelangor && selangorCentroid && selangorBranches.length > 0 && (
            <g
              role="button"
              tabIndex={0}
              aria-label={`Zoom into ${selangorBranches.length} Selangor showrooms`}
              transform={`translate(${selangorCentroid.x}, ${selangorCentroid.y}) scale(${pinScale})`}
              className={
                selangorBranches.some((b) => b.id === hoveredBranchId)
                  ? `${styles.mapPinGroup} ${styles.mapPinHovered}`
                  : styles.mapPinGroup
              }
              onClick={onFocusSelangor}
              onKeyDown={(event) => {
                if (event.key === "Enter" || event.key === " ") {
                  event.preventDefault();
                  onFocusSelangor();
                }
              }}
            >
              <g className={styles.mapPinVisual}>
                <circle cx="0" cy="0" r="22" className={styles.pinHalo} />
                <circle cx="0" cy="0" r="15" className={styles.clusterRing} />
                <text x="0" y="0" className={styles.clusterText}>
                  {selangorBranches.length}
                </text>
                <g transform="translate(0, -30)">
                  <g className={styles.pinTagGroup}>
                    <rect
                      x="-46"
                      y="-11"
                      width="92"
                      height="22"
                      rx="4"
                      className={styles.pinTagBox}
                    />
                    <text x="0" y="0" className={styles.pinTagText}>
                      Selangor (2)
                    </text>
                  </g>
                </g>
              </g>
            </g>
          )}

          {unclusteredBranches.map((branch) => {
            const pos = projectMalaysiaCoordinate(branch.lng, branch.lat);
            const isActive = branch.id === selectedBranch?.id;
            const isHovered = branch.id === hoveredBranchId;
            const pinClasses = [
              styles.mapPinGroup,
              isActive ? styles.mapPinSelected : "",
              isHovered ? styles.mapPinHovered : "",
            ]
              .filter(Boolean)
              .join(" ");
            const tagWidth = Math.max(78, branch.shortLabel.length * 7.2 + 20);

            return (
              <g
                key={branch.id}
                role="button"
                tabIndex={0}
                aria-label={`Focus ${branch.name}, ${branch.city}`}
                aria-pressed={isActive}
                transform={`translate(${pos.x}, ${pos.y}) scale(${pinScale})`}
                className={pinClasses}
                onMouseEnter={() => onHoverBranch(branch.id)}
                onMouseLeave={() => onHoverBranch(null)}
                onClick={() => onSelectBranch(branch)}
                onKeyDown={(event) => {
                  if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    onSelectBranch(branch);
                  }
                }}
              >
                <g className={styles.mapPinVisual}>
                  <circle cx="0" cy="0" r="19" className={styles.pinHalo} />
                  <circle cx="0" cy="0" r="11" className={styles.beaconRing} />
                  <circle cx="0" cy="0" r="4" className={styles.beaconCore} />
                  <g transform="translate(0, -28)">
                    <g className={styles.pinTagGroup}>
                      <rect
                        x={-tagWidth / 2}
                        y="-11"
                        width={tagWidth}
                        height="22"
                        rx="4"
                        className={styles.pinTagBox}
                      />
                      <text x="0" y="0" className={styles.pinTagText}>
                        {branch.shortLabel}
                      </text>
                    </g>
                  </g>
                </g>
              </g>
            );
          })}
        </svg>
      </div>

      <div className={styles.mapFooterBar}>
        {selectedBranch ? (
          <>
            <div className={styles.mapFooterMeta}>
              <strong>
                {selectedBranch.address}, {selectedBranch.city}
              </strong>
              <span>{selectedBranch.hours}</span>
            </div>
            <div className={styles.mapFooterActions}>
              <Link
                href={catalogueHref({ ...catalogueDefaults, branchId: selectedBranch.id }, 1)}
                prefetch={false}
                className={buttonClassName("primary", "sm")}
              >
                Browse {selectedBranch.shortLabel} cars
              </Link>
            </div>
          </>
        ) : (
          <div className={styles.mapFooterMeta}>
            <strong>Select a showroom card or map pin</strong>
            <span>Click again or press Reset to return to the peninsula view</span>
          </div>
        )}
      </div>
    </aside>
  );
}
