"use client";

import { Icon } from "@/components/ui/Icon";
import { useStaffViewMemory } from "@/hooks/use-staff-view-memory";
import styles from "./IntakeCalendar.module.css";
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
  active,
  destinations,
  pending,
  disabled,
  onNavigate,
}: {
  actor: string;
  active: "calendar" | "list";
  destinations: Record<"calendar" | "list", string>;
  pending: IntakeView | null;
  disabled: boolean;
  onNavigate: (href: string) => void;
}) {
  const links = useStaffViewMemory(actor, active, destinations);

  return (
    <nav
      aria-label="Appointment views"
      aria-busy={Boolean(pending)}
      data-view={pending ?? active}
      className={styles.viewSwitch}
    >
      <span className={styles.viewThumb} aria-hidden="true" />
      {views.map((view) => (
        <a
          key={view.id}
          href={links[view.id]}
          title={view.label}
          aria-label={view.label}
          aria-current={active === view.id ? "page" : undefined}
          aria-disabled={disabled || active === view.id}
          data-view={view.id}
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
            if (disabled || active === view.id) {
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
