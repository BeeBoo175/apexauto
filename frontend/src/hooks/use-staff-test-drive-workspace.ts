"use client";

import { useEffect, useEffectEvent } from "react";
import { useGuardedAction } from "./use-guarded-action";
import { useInitialToast } from "./use-initial-toast";
import { useStaffTestDriveList } from "./use-staff-test-drive-list";
import { useStaffTestDriveDetail } from "./use-staff-test-drive-detail";
import type { CurrentActor } from "@/lib/auth/actor";
import type { useStaffTestDriveEntry } from "./use-staff-test-drive-entry";
import { staffTestDriveParams, staffTestDriveUrl } from "@/lib/test-drives/staff-query";
import type { TestDriveStaffCommand } from "@/lib/test-drives/staff-commands";

export type TestDriveEntry = NonNullable<ReturnType<typeof useStaffTestDriveEntry>["entry"]>;

export function useStaffTestDriveWorkspace(actor: CurrentActor, entry: TestDriveEntry) {
  useInitialToast(entry.notice);
  const list = useStaffTestDriveList(entry.initial, entry.filters, entry.page);
  const record = useStaffTestDriveDetail(actor, entry.selectedId, entry.selected);
  const guard = useGuardedAction();
  const agent = actor.role === "SalesAgent";

  useEffect(() => {
    if (entry.notice) {
      window.history.replaceState(
        null,
        "",
        staffTestDriveUrl(entry.filters, entry.page, entry.selectedId),
      );
    }
  }, [entry]);

  function writeSelection(id: string) {
    const query = new URL(window.location.href);
    if (id) {
      query.searchParams.set("booking", id);
    } else {
      query.searchParams.delete("booking");
    }
    window.history.replaceState(null, "", `${query.pathname}${query.search}`);
  }

  function open(id: string) {
    if (id === record.id && record.snapshot) {
      return;
    }
    guard.request(() => {
      void record.load(id).then((ok) => {
        if (ok) {
          writeSelection(id);
        }
      });
    });
  }

  function close() {
    record.close();
    writeSelection("");
  }

  async function refresh(kind?: TestDriveStaffCommand) {
    let filters = list.active.filters;
    if (agent && kind === "claims") {
      filters = { ...filters, queue: "MyWork" };
    }
    if (agent && kind === "releases") {
      filters = { ...filters, queue: "ToClaim" };
    }
    if (agent && (kind === "outcomes" || kind === "cancellations")) {
      filters = { ...filters, queue: "History" };
    }
    const [rows, detail] = await Promise.all([
      list.load(filters, filters.queue !== list.active.filters.queue ? 1 : list.active.page, true),
      record.id ? record.load() : Promise.resolve(true),
    ]);
    return rows && detail;
  }

  async function restoreConfirmed(next: ReturnType<typeof staffTestDriveParams>) {
    const loaded = await list.load(next.filters, next.page, true);
    if (!loaded) {
      return;
    }
    if (!next.selectedId) {
      close();
      return;
    }
    if (await record.load(next.selectedId)) {
      writeSelection(next.selectedId);
    }
  }

  const restore = useEffectEvent(() => {
    if (window.location.pathname !== "/dashboard/test-drives") {
      return;
    }
    const parameters = Object.fromEntries(new URL(window.location.href).searchParams);
    const next = staffTestDriveParams(parameters, agent);
    const previous = staffTestDriveUrl(list.active.filters, list.active.page, record.id);
    window.history.replaceState(null, "", previous);
    guard.request(() => {
      void restoreConfirmed(next);
    });
  });

  useEffect(() => {
    window.addEventListener("popstate", restore);
    return () => window.removeEventListener("popstate", restore);
  }, []);

  return {
    list,
    record,
    guard,
    agent,
    busy: list.busy || Boolean(record.pendingId),
    open,
    close,
    refresh,
  };
}
