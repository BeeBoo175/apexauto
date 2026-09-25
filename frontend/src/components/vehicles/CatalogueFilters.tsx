import type { Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { Taxonomy } from "@/lib/intake/contracts";
import { useMediaQuery } from "@/hooks/use-media-query";
import type { CatalogueFilters as Filters } from "@/lib/vehicles/contracts";
import { countActiveCatalogueFilters } from "@/lib/vehicles/catalogue-controls";
import { catalogueDefaults } from "@/lib/vehicles/query";
import { CatalogueFilterDrawer } from "./CatalogueFilterDrawer";
import { CatalogueFilterFields } from "./CatalogueFilterFields";
import { CatalogueFilterRail } from "./CatalogueFilterRail";

// The filter drawer replaces the rail at the width where the layout stacks.
const COMPACT_QUERY = "(max-width: 960px)";

interface CatalogueFiltersProps {
  active: Filters;
  draft: Filters;
  setDraft: (filters: Filters) => void;
  busy: boolean;
  apply: (filters: Filters) => void;
  branches: Page<Branch> | null;
  branchBusy: boolean;
  loadBranches: (page: number) => void;
  taxonomy: Taxonomy | null;
  open: boolean;
  onClose: () => void;
  // The hero's showroom tabs already cover the showroom choice when every branch fits there.
  showroomInTabs: boolean;
  // Phones sort from inside the drawer, because the control bar has no room for it.
  mobileSort: React.ReactNode;
}

export function CatalogueFilters({
  active,
  draft,
  setDraft,
  busy,
  apply,
  branches,
  branchBusy,
  loadBranches,
  taxonomy,
  open,
  onClose,
  showroomInTabs,
  mobileSort,
}: CatalogueFiltersProps) {
  const compact = useMediaQuery(COMPACT_QUERY);
  const activeCount = countActiveCatalogueFilters(active);
  const draftCount = countActiveCatalogueFilters(draft);
  const nothingToReset = draftCount + activeCount === 0;

  function handleReset() {
    const resetFilters = {
      ...catalogueDefaults,
      sort: active.sort,
      direction: active.direction,
    };
    if (activeCount === 0) {
      setDraft(resetFilters);
      return;
    }
    apply(resetFilters);
  }

  const fields = (
    <CatalogueFilterFields
      active={active}
      draft={draft}
      setDraft={setDraft}
      apply={apply}
      branches={branches}
      branchBusy={branchBusy}
      loadBranches={loadBranches}
      taxonomy={taxonomy}
      showroomInTabs={showroomInTabs}
      mobileSort={mobileSort}
    />
  );

  if (!compact) {
    return (
      <CatalogueFilterRail
        draft={draft}
        apply={apply}
        fields={fields}
        resetIdle={nothingToReset}
        busy={busy}
        onReset={handleReset}
      />
    );
  }

  return (
    <CatalogueFilterDrawer
      open={open}
      onClose={onClose}
      draft={draft}
      apply={apply}
      fields={fields}
      activeCount={activeCount}
      resetDisabled={busy || nothingToReset}
      onReset={handleReset}
    />
  );
}
