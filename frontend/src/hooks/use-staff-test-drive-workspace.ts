"use client";

import { useEffect, useEffectEvent } from "react";
import { useGuardedAction } from "./use-guarded-action";
import { useInitialToast } from "./use-initial-toast";
import { useStaffTestDriveList } from "./use-staff-test-drive-list";
import { useStaffTestDriveDetail } from "./use-staff-test-drive-detail";
import type { CurrentActor } from "@/lib/auth/actor";
import type { useStaffTestDriveEntry } from "./use-staff-test-drive-entry";
import {
  staffTestDriveParams,
  staffTestDriveUrl,
  testDriveQueue,
  testDriveSectionParameter,
} from "@/lib/test-drives/staff-query";
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

  function open(id: string) {
    if (id === record.id && record.snapshot) {
      return;
    }
    guard.request(() => {
      void record.load(id);
    });
  }

  async function refresh(kind?: TestDriveStaffCommand) {
    let filters = list.active.filters;
    if (agent && kind) {
      const detail = await record.load();
      if (detail) {
        filters = { ...filters, queue: testDriveQueue(detail) };
      }
      const rows = await list.load(
        filters,
        filters.queue !== list.active.filters.queue ? 1 : list.active.page,
        true,
      );
      return rows && Boolean(detail);
    }
    const [rows, detail] = await Promise.all([
      list.load(filters, list.active.page, true),
      record.id ? record.load() : Promise.resolve(true),
    ]);
    return rows && Boolean(detail);
  }

  async function restoreConfirmed(next: ReturnType<typeof staffTestDriveParams>) {
    const loaded = await list.load(next.filters, next.page, true);
    if (!loaded) {
      return;
    }
    if (!next.selectedId) {
      record.close();
      return;
    }
    await record.load(next.selectedId);
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
    close: () => {
      record.close();
      const url = new URL(window.location.href);
      url.searchParams.delete(testDriveSectionParameter);
      window.history.replaceState(null, "", url);
    },
    refresh,
  };
}
