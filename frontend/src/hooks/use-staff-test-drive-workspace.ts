"use client";

import { useContext, useEffect, useEffectEvent, useLayoutEffect, useRef } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { StaffLocalHistoryContext } from "@/components/staff/StaffClientEntryBoundary";
import { useGuardedAction } from "./use-guarded-action";
import { useInitialToast } from "./use-initial-toast";
import { useStaffTestDriveList } from "./use-staff-test-drive-list";
import { useStaffTestDriveDetail } from "./use-staff-test-drive-detail";
import type { CurrentActor } from "@/lib/auth/actor";
import type { useStaffTestDriveEntry } from "./use-staff-test-drive-entry";
import {
  staffTestDriveLocation,
  staffTestDriveQuery,
  staffTestDriveUrl,
  testDriveQueue,
  type TestDriveFilters,
} from "@/lib/test-drives/staff-query";
import type { TestDriveStaffCommand } from "@/lib/test-drives/staff-commands";
import { writeAddress } from "@/lib/browser-url";

export type TestDriveEntry = NonNullable<ReturnType<typeof useStaffTestDriveEntry>["entry"]>;

export function useStaffTestDriveWorkspace(actor: CurrentActor, entry: TestDriveEntry) {
  useInitialToast(entry.notice);
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const localHistoryRef = useContext(StaffLocalHistoryContext);
  const agent = actor.role === "SalesAgent";
  const initialHref = staffTestDriveUrl(entry.filters, entry.page, entry.selectedId, entry.section);
  const lastAttempted = useRef(initialHref);
  const confirmed = useRef({ filters: entry.filters, page: entry.page, id: entry.selectedId });
  const list = useStaffTestDriveList(entry.initial, entry.filters, entry.page, commitList);
  const record = useStaffTestDriveDetail(actor, entry.selectedId, entry.selected, commitRecord);
  const guard = useGuardedAction();

  function commit(href: string, mode: "push" | "replace") {
    lastAttempted.current = href;
    if (`${window.location.pathname}${window.location.search}` !== href) {
      writeAddress(href, null, mode);
    }
  }

  function currentSection(id: string) {
    const target = staffTestDriveLocation(window.location.pathname, window.location.search, agent);
    return target?.selectedId === id ? target.section : "";
  }

  function commitRecord(id: string, updateAddress = true) {
    confirmed.current.id = id;
    if (updateAddress) {
      const { filters, page } = confirmed.current;
      commit(staffTestDriveUrl(filters, page, id, currentSection(id)), "push");
    }
  }

  function commitList(filters: TestDriveFilters, page: number, mode: false | "push" | "replace") {
    confirmed.current = { ...confirmed.current, filters, page };
    if (mode) {
      const { id } = confirmed.current;
      commit(staffTestDriveUrl(filters, page, id, currentSection(id)), mode);
    }
  }

  function open(id: string) {
    if (id !== record.id || !record.snapshot) {
      guard.request(() => void record.load(id));
    }
  }

  function close() {
    guard.request(() => {
      record.close(Boolean(record.id));
    }, "Discard and close");
  }

  async function readDestination() {
    const target = staffTestDriveLocation(window.location.pathname, window.location.search, agent);
    if (!target) {
      return false;
    }
    lastAttempted.current = target.href;
    const [rows, detail] = await Promise.all([
      list.load(target.filters, target.page, true, false),
      target.selectedId ? record.load(target.selectedId, false) : Promise.resolve(true),
    ]);
    if (!target.selectedId) {
      record.close(false);
    }
    return rows && Boolean(detail);
  }

  async function refresh(kind?: TestDriveStaffCommand) {
    const target = staffTestDriveLocation(window.location.pathname, window.location.search, agent);
    if (!kind && target && target.selectedId !== record.id) {
      guard.request(() => void readDestination());
      return false;
    }
    if (!kind) {
      return readDestination();
    }
    const detail = await record.load(record.id, false);
    const filters =
      agent && detail
        ? { ...list.active.filters, queue: testDriveQueue(detail) }
        : list.active.filters;
    const page = filters.queue !== list.active.filters.queue ? 1 : list.active.page;
    const rows = await list.load(filters, page, true, "replace");
    return rows && Boolean(detail);
  }

  const synchronize = useEffectEvent(() => {
    const target = staffTestDriveLocation(window.location.pathname, window.location.search, agent);
    if (!target) {
      return;
    }
    if (`${window.location.pathname}${window.location.search}` !== target.href) {
      writeAddress(target.href, null, "replace");
    }
    if (target.href === lastAttempted.current) {
      return;
    }
    const previousUrl = new URL(lastAttempted.current, window.location.origin);
    const previous = staffTestDriveLocation(previousUrl.pathname, previousUrl.search, agent);
    lastAttempted.current = target.href;
    const targetQuery = staffTestDriveQuery(target.filters, target.page);
    if (
      previous?.selectedId === target.selectedId &&
      staffTestDriveQuery(previous.filters, previous.page) === targetQuery
    ) {
      return;
    }
    list.cancel();
    if (targetQuery !== staffTestDriveQuery(list.active.filters, list.active.page)) {
      void list.load(target.filters, target.page, true, false);
    }
    if (target.selectedId !== record.id || record.pendingId) {
      if (target.selectedId) {
        void record.load(target.selectedId, false);
      } else {
        record.close(false);
      }
    }
  });

  useLayoutEffect(() => {
    lastAttempted.current = initialHref;
    if (`${window.location.pathname}${window.location.search}` !== initialHref) {
      writeAddress(initialHref, null, "replace");
    }
    if (localHistoryRef) {
      localHistoryRef.current = null;
      localHistoryRef.pathname = "/dashboard/test-drives";
    }
    return () => {
      if (localHistoryRef) {
        localHistoryRef.current = null;
        localHistoryRef.pathname = undefined;
        localHistoryRef.selectedId = undefined;
      }
    };
  }, [initialHref, localHistoryRef]);

  useLayoutEffect(() => {
    if (localHistoryRef) {
      localHistoryRef.selectedId = record.id;
    }
  }, [localHistoryRef, record.id]);

  const search = searchParams.toString();
  useEffect(() => synchronize(), [pathname, search]);

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
