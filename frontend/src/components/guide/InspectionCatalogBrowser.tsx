"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { SearchField } from "@/components/ui/SearchField";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { inspectionPoints, type InspectionCategoryKey } from "@/lib/guide/inspection-points";
import { inspectionCategories, inspectionCategoryTitles } from "@/lib/guide/inspection-rules";
import { label } from "@/lib/inspection/contracts";
import guideLayout from "./GuideLayout.module.css";
import styles from "./InspectionCatalog.module.css";

type CategoryFilter = InspectionCategoryKey | "ALL";

const INITIAL_VISIBLE_COUNT = 12;

export function InspectionCatalogBrowser() {
  const [category, setCategory] = useState<CategoryFilter>("FrameChassis");
  const [query, setQuery] = useState("");
  const [disqualifiersOnly, setDisqualifiersOnly] = useState(false);
  const [expanded, setExpanded] = useState(false);

  const normalizedQuery = query.trim().toLowerCase();
  const effectiveCategory: CategoryFilter = normalizedQuery ? "ALL" : category;
  const visiblePoints = inspectionPoints.filter((point) => {
    if (effectiveCategory !== "ALL" && point.category !== effectiveCategory) {
      return false;
    }
    if (disqualifiersOnly && !point.disqualifierRisk) {
      return false;
    }
    if (!normalizedQuery) {
      return true;
    }
    return (
      point.code.toLowerCase().includes(normalizedQuery) ||
      point.description.toLowerCase().includes(normalizedQuery) ||
      label(point.category).toLowerCase().includes(normalizedQuery)
    );
  });

  const isFiltering = Boolean(normalizedQuery) || disqualifiersOnly;
  const showAll = expanded || isFiltering;
  const displayedPoints = showAll ? visiblePoints : visiblePoints.slice(0, INITIAL_VISIBLE_COUNT);
  const hasMore = !showAll && visiblePoints.length > INITIAL_VISIBLE_COUNT;

  function resetFilters() {
    setCategory("FrameChassis");
    setQuery("");
    setDisqualifiersOnly(false);
    setExpanded(false);
  }

  const activeTitle =
    effectiveCategory === "ALL"
      ? "All 5 inspection systems"
      : inspectionCategoryTitles[effectiveCategory];

  return (
    <div>
      <div className={styles.catalogControls}>
        <div className={styles.toolbarTop}>
          <form
            className={styles.searchWrap}
            onSubmit={(event) => {
              event.preventDefault();
            }}
          >
            <SearchField
              label="Search 210 inspection checkpoints"
              value={query}
              disabled={false}
              onChange={setQuery}
              onClear={query ? () => setQuery("") : undefined}
              placeholder="Search code (e.g. FC-038, ET-027) or check item..."
            />
          </form>

          <div className={styles.toolbarMeta}>
            <button
              type="button"
              className={guideLayout.chip}
              aria-pressed={disqualifiersOnly}
              onClick={() => setDisqualifiersOnly((prev) => !prev)}
            >
              Instant-fail items only
            </button>
            <span className={styles.toolbarStatus} role="status">
              Showing {visiblePoints.length} of {inspectionPoints.length} checks
            </span>
          </div>
        </div>

        <div
          className={guideLayout.chipRow}
          role="group"
          aria-label="Filter checkpoints by category"
        >
          {inspectionCategories.map((item) => (
            <button
              key={item.key}
              type="button"
              className={guideLayout.chip}
              aria-pressed={effectiveCategory === item.key}
              onClick={() => {
                setQuery("");
                setCategory(item.key);
                setExpanded(false);
              }}
            >
              {item.title} ({item.count})
            </button>
          ))}
          <button
            type="button"
            className={guideLayout.chip}
            aria-pressed={effectiveCategory === "ALL"}
            onClick={() => {
              setQuery("");
              setCategory("ALL");
              setExpanded(false);
            }}
          >
            All ({inspectionPoints.length})
          </button>
        </div>
      </div>

      {visiblePoints.length === 0 ? (
        <div className={styles.emptyPoints}>
          <p>No inspection check matches “{query.trim()}”.</p>
          <Button type="button" variant="secondary" size="md" onClick={resetFilters}>
            Reset filters
          </Button>
        </div>
      ) : (
        <div>
          <div className={styles.sheetHeadingRow}>
            <h3 className={layout.cardHeading}>{activeTitle}</h3>
            <span>{visiblePoints.length} items</span>
          </div>
          <ul className={styles.pointRows} aria-label="210-point inspection checklist">
            {displayedPoints.map((point) => (
              <li
                key={point.code}
                className={
                  point.disqualifierRisk
                    ? `${styles.pointRow} ${styles.pointRowRisk}`
                    : styles.pointRow
                }
              >
                <span className={styles.pointCode}>{point.code}</span>
                <div className={styles.pointText}>
                  <span>{point.description}</span>
                  {point.disqualifierRisk && (
                    <span className={styles.riskNote}>Instant-fail rule</span>
                  )}
                </div>
              </li>
            ))}
          </ul>

          {hasMore && (
            <div className={styles.expandWrap}>
              <Button type="button" variant="secondary" size="md" onClick={() => setExpanded(true)}>
                Show all {visiblePoints.length} checks
              </Button>
            </div>
          )}
          {expanded && !isFiltering && visiblePoints.length > INITIAL_VISIBLE_COUNT && (
            <div className={styles.expandWrap}>
              <Button
                type="button"
                variant="secondary"
                size="md"
                onClick={() => setExpanded(false)}
              >
                Show fewer checks
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
