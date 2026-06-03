"use client";

import { useState } from "react";
import { Input, Select } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";
import type { CurrentActor } from "@/lib/auth/actor";
import type { useWorkCalendar } from "@/hooks/use-work-calendar";
import { BranchFilter } from "./BranchFilter";
import styles from "./WorkCalendar.module.css";

export function WorkCalendarFilters({
  actor,
  calendar,
}: {
  actor: CurrentActor;
  calendar: ReturnType<typeof useWorkCalendar>;
}) {
  const { filters, pending, branches } = calendar;
  const [email, setEmail] = useState(filters.employee === "unassigned" ? "" : filters.employee);
  const [mode, setMode] = useState(filters.employee === "unassigned" ? "unassigned" : "all");
  const manager = actor.role === "BranchManager" || actor.role === "GeneralManager";
  const disabled = pending.scheduled || pending.unscheduled;
  if (!manager) {
    return null;
  }
  return (
    <div className={styles.filters}>
      {actor.role === "GeneralManager" && (
        <BranchFilter
          branches={branches}
          branchId={filters.branchId}
          emptyLabel="Choose a branch"
          selectedBranchName={
            calendar.scheduled.data?.items.find((item) => item.branchId === filters.branchId)
              ?.branchName ??
            calendar.unscheduled.data?.items.find((item) => item.branchId === filters.branchId)
              ?.branchName
          }
          disabled={disabled || pending.branches}
          onChange={(branchId) => void calendar.changeScope({ ...filters, branchId })}
          onPage={(page) => void calendar.loadBranches(page)}
        />
      )}
      {manager && (
        <form
          className={styles.filters}
          onSubmit={(event) => {
            event.preventDefault();
            void calendar.changeScope({
              ...filters,
              employee: mode === "unassigned" ? "unassigned" : email,
            });
          }}
        >
          <Select
            label="Employee scope"
            labelHidden
            density="compact"
            value={mode}
            disabled={disabled}
            onChange={(event) => setMode(event.target.value)}
          >
            <option value="all">All employees</option>
            <option value="unassigned">Unassigned</option>
          </Select>
          {mode !== "unassigned" && (
            <Input
              label="Employee email"
              labelHidden
              density="compact"
              placeholder="Filter employee email"
              maxLength={160}
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          )}
          <Button
            type="submit"
            size="compact"
            variant="secondary"
            disabled={disabled || calendar.branchRequired}
          >
            Apply
          </Button>
          {filters.employee && (
            <Button
              type="button"
              size="compact"
              variant="secondary"
              disabled={disabled}
              onClick={() => {
                setEmail("");
                setMode("all");
                void calendar.changeScope({ ...filters, employee: "" });
              }}
            >
              Clear employee
            </Button>
          )}
        </form>
      )}
    </div>
  );
}
