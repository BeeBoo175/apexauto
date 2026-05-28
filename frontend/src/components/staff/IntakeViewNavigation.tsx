"use client";

import { Icon } from "@/components/ui/Icon";
import { useStaffViewMemory } from "@/hooks/use-staff-view-memory";
import styles from "@/components/ui/ViewSwitcher.module.css";
import type { IntakeView } from "@/lib/staff/view-memory";

const views = [
  {
    id: "calendar",
    label: "Calendar",
    icon: "calendar",
  },
  {
    id: "list",
    label: "Table",
    icon: "table",
  },
] as const;

export function IntakeViewNavigation({
  actor,
  agent,
  active,
  destinations,
  pending,
  disabled,
  onNavigate,
  onPrepare,
  onCancel,
}: {
  actor: string;
  agent: boolean;
  active: IntakeView;
  destinations: Record<IntakeView, string>;
  pending: IntakeView | null;
  disabled: boolean;
  onNavigate: (href: string) => void;
  onPrepare: (view: IntakeView) => void;
  onCancel: () => void;
}) {
  const workView = agent ? "queue" : "list";
  const links = useStaffViewMemory(actor, active, destinations, workView);
  const visibleViews: { id: IntakeView; label: string; icon: "calendar" | "table" }[] =
    views.map((view) => {
      if (view.id === "calendar") {
        return view;
      }
      return { ...view, id: workView, label: agent ? "Work queues" : "Table" };
    });
  const selected = pending ?? active;

  function prepare(view: IntakeView) {
    if (!disabled && view !== active) {
      onPrepare(view);
    }
  }

  return (
    <nav
      aria-label="Appointment views"
      aria-busy={Boolean(pending)}
      data-second={(pending ?? active) !== "calendar"}
      className={styles.switch}
    >
      <span className={styles.thumb} aria-hidden="true" />
      {visibleViews.map((view) => (
        <a
          key={view.id}
          className={styles.option}
          href={links[view.id]}
          title={view.label}
          aria-label={view.label}
          aria-current={selected === view.id ? "page" : undefined}
          aria-disabled={disabled || selected === view.id}
          data-view={view.id}
          onPointerEnter={() => prepare(view.id)}
          onFocus={() => prepare(view.id)}
          onPointerDown={(event) => {
            if (event.button === 0) {
              prepare(view.id);
            }
          }}
          onClick={(event) => {
            if (
              event.button !== 0 ||
              event.metaKey ||
              event.ctrlKey ||
              event.shiftKey ||
              event.altKey
            ) {
              return;
            }
            event.preventDefault();
            if (disabled || selected === view.id) {
              return;
            }
            if (view.id === active && pending) {
              onCancel();
              return;
            }
            onNavigate(links[view.id]);
          }}
        >
          <Icon name={view.icon} />
          <span className="sr-only">{view.label}</span>
        </a>
      ))}
    </nav>
  );
}
