import { memo } from "react";
import { peninsulaCityLabels, peninsulaStateShapes } from "@/lib/guide/malaysia-map";
import type { BranchRegionState } from "@/lib/guide/showrooms";
import styles from "./ShowroomMapStage.module.css";

const GRID_COORDS_X = [160, 320, 480, 640, 800, 960] as const;
const GRID_COORDS_Y = [160, 280, 400, 520, 640] as const;

// Memoised so pin hover, which re-renders the stage, does not rebuild the grid, state outlines and city labels.
export const ShowroomMapBackdrop = memo(function ShowroomMapBackdrop({
  activeStates,
  focusRegion,
  labelScale,
}: {
  activeStates: ReadonlySet<string>;
  focusRegion: "ALL" | BranchRegionState;
  labelScale: number;
}) {
  return (
    <>
      {GRID_COORDS_X.map((gx) => (
        <line key={`gx-${gx}`} x1={gx} y1={-40} x2={gx} y2={820} className={styles.mapGridLine} />
      ))}
      {GRID_COORDS_Y.map((gy) => (
        <line key={`gy-${gy}`} x1={-160} y1={gy} x2={1160} y2={gy} className={styles.mapGridLine} />
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
    </>
  );
});
