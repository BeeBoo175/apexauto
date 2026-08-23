"use client";

import { StaffLoading } from "@/components/staff/StaffPage";
import { StaffReloadNotice } from "@/components/staff/StaffReloadNotice";
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
  const { entry, failed } = useSalesClientEntry(actor, parameters);
  if (failed) {
    return <StaffReloadNotice title="Sales" />;
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
  const { Workspace, saleId, section, version, selected, queues, found, list } = entry;
  return (
    <Workspace
      queues={queues}
      found={found}
      list={list}
      selectedSaleId={saleId}
      selectedSale={selected}
      selectedSection={section}
      selectedVersion={version}
    />
  );
}
