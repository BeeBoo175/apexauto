"use client";

import { useEffect, useState } from "react";
import { Icon } from "@/components/ui/Icon";
import styles from "./IntakeCalendar.module.css";

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
  active,
  destinations,
}: {
  actor: string;
  active: "calendar" | "list";
  destinations: Record<"calendar" | "list", string>;
}) {
  const links = destinations;
  const [destination, setDestination] = useState<(typeof views)[number] | null>(null);

  useEffect(() => {
    const navigation = (window as Window & { navigation?: EventTarget }).navigation;
    function reconcile() {
      setDestination(null);
    }
    function cancel(event: KeyboardEvent) {
      if (event.key === "Escape") {
        reconcile();
      }
    }
    navigation?.addEventListener("navigateerror", reconcile);
    window.addEventListener("pageshow", reconcile);
    window.addEventListener("keydown", cancel);
    return () => {
      navigation?.removeEventListener("navigateerror", reconcile);
      window.removeEventListener("pageshow", reconcile);
      window.removeEventListener("keydown", cancel);
    };
  }, []);

  return (
    <nav
      aria-label="Appointment views"
      aria-busy={Boolean(destination)}
      data-view={destination?.id ?? active}
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
            // Native navigation starts now; the browser carries the thumb between documents.
            setDestination(view);
          }}
        >
          <Icon name={view.icon} />
          <span className="sr-only">{view.label}</span>
        </a>
      ))}
    </nav>
  );
}
