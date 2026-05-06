"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { useSalesClientEntry } from "@/hooks/use-sales-client-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";

export function SalesClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const { entry, failed, retry } = useSalesClientEntry(actor, parameters);
  if (failed) {
    return (
      <StaffNotice title="Sales">
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry sales
        </Button>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  if (entry.kind === "manager") {
    const { Table } = entry;
    return (
      <Table
        actor={actor}
        initial={entry.initial}
        filters={entry.filters}
        page={entry.page}
        saleId={entry.saleId}
        section={entry.section}
        version={entry.version}
        selected={entry.selected}
      />
    );
  }
  const { Workspace, saleId, section, version, selected, queues, initialView } = entry;
  return (
    <Workspace
      queues={queues}
      initialView={initialView}
      selectedSaleId={saleId}
      selectedSale={selected}
      selectedSection={section}
      selectedVersion={version}
    />
  );
}
