"use client";

import { useRef, useState } from "react";
import {
  accountsHref,
  accountsListKey,
  accountsLocation,
  accountsWorkspace,
  type AccountSection,
  type AccountSelectionPath,
  type AccountsLocationRules,
  type AccountsState,
} from "@/lib/accounts/accounts-location";
import { useSelectedRecord, useStaffAddress, type AddressMode } from "./use-staff-address";

export interface AccountsList<Filters, Row extends { id: string }> {
  snapshot: { items: Row[] };
  active: { filters: Filters; page: number };
  busy: boolean;
  cancel: () => void;
  load: (
    filters: Filters,
    page: number,
    supersede?: boolean,
    mode?: AddressMode,
  ) => Promise<boolean>;
}

const closed: AccountSelectionPath = { id: "", section: "details" };

// One Accounts view (Staff or Customers): the path selects the account and its section, the query holds the table.
// The drawer needs the row, so an account outside the table the address names is dropped once that table is shown.
export function useAccountsAddress<Filters, Row extends { id: string }>(
  rules: AccountsLocationRules<Filters>,
  initial: AccountsState<Filters>,
  list: () => AccountsList<Filters, Row>,
) {
  const [selection, showSelection] = useState<AccountSelectionPath>({
    id: initial.id,
    section: initial.section,
  });
  // Mirrors the shown selection for list callbacks that complete after later renders.
  const current = useRef(selection);

  function setSelection(next: AccountSelectionPath) {
    current.current = next;
    showSelection(next);
  }

  type Target = NonNullable<ReturnType<typeof accountsLocation<Filters>>>;
  const address = useStaffAddress<AccountsState<Filters>, Target>({
    workspace: accountsWorkspace(rules.view),
    initial,
    href: (state) => accountsHref(rules, state),
    locate: (pathname, search) => accountsLocation(rules, pathname, search),
    synchronize,
  });
  useSelectedRecord(selection.id);

  function shownState(): AccountsState<Filters> {
    const { filters, page } = list().active;
    return { ...selection, filters, page };
  }

  function synchronize(target: Target, previous: Target | null) {
    setSelection({ id: target.id, section: target.section });
    address.update({ id: target.id, section: target.section }, false);
    const sameList =
      previous !== null && accountsListKey(rules, previous) === accountsListKey(rules, target);
    if (sameList) {
      return;
    }
    const shown = list();
    shown.cancel();
    if (accountsListKey(rules, target) !== accountsListKey(rules, { ...target, ...shownState() })) {
      void shown.load(target.filters, target.page, true, false);
    }
  }

  // Applied list changes push; a reload or a post-write read replaces. An account that left the confirmed
  // table (a revoked invitation, a narrowed filter) closes with it.
  function onLocation(filters: Filters, page: number, mode: AddressMode, items: Row[]) {
    const held = current.current.id === "" || items.some((row) => row.id === current.current.id);
    if (held) {
      address.update({ filters, page }, mode);
      return;
    }
    setSelection(closed);
    address.update({ filters, page, ...closed }, mode || "replace");
  }

  function open(id: string) {
    const next = { id, section: "details" as const };
    setSelection(next);
    address.update(next, "push");
  }

  function close() {
    setSelection(closed);
    address.update(closed, "push");
  }

  function choose(section: AccountSection) {
    setSelection({ id: selection.id, section });
    address.update({ section }, "push");
  }

  // Refresh follows the address: after a failed Back/Forward it reads the requested destination.
  function refresh() {
    const target = address.destination();
    const shown = list();
    if (!target || accountsListKey(rules, target) === accountsListKey(rules, shownState())) {
      return shown.load(shown.active.filters, shown.active.page, true, "replace");
    }
    address.attempted(target);
    setSelection({ id: target.id, section: target.section });
    address.update({ id: target.id, section: target.section }, false);
    return shown.load(target.filters, target.page, true, false);
  }

  const selected = list().snapshot.items.find((row) => row.id === selection.id) ?? null;

  return { selected, selection, open, close, choose, refresh, onLocation };
}
