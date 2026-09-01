import { useEffect, useRef } from "react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { ScrollStrip } from "@/components/ui/ScrollStrip";
import styles from "./StaffQueueTabs.module.css";

function revealTab(button: HTMLButtonElement | null) {
  const strip = button?.parentElement;
  if (!button || !strip || strip.clientWidth === 0) {
    return;
  }

  const bounds = strip.getBoundingClientRect();
  const tab = button.getBoundingClientRect();
  const padding = 16;
  if (tab.left < bounds.left + padding) {
    strip.scrollLeft += tab.left - bounds.left - padding;
    return;
  }
  if (tab.right > bounds.right - padding) {
    strip.scrollLeft += tab.right - bounds.right + padding;
  }
}

export function StaffQueueTabs<T extends string>({
  label,
  tabs,
  value,
  disabled = false,
  countNoun = "records",
  layout = "scroll",
  onChange,
}: {
  label: string;
  tabs: readonly { value: T; label: string; count?: number }[];
  value: T;
  disabled?: boolean;
  countNoun?: string;
  layout?: "scroll" | "queue" | "queue-wide";
  onChange: (value: T) => void;
}) {
  const selectedTab = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    const button = selectedTab.current;
    revealTab(button);
    const strip = button?.parentElement;
    if (!strip) {
      return;
    }
    const observer = new ResizeObserver(() => revealTab(button));
    observer.observe(strip);
    return () => observer.disconnect();
  }, [value]);

  if (tabs.length < 2) {
    return null;
  }
  return (
    <nav className={styles.sections} aria-label={label} data-layout={layout}>
      <ScrollStrip label={label} className={styles.tabStrip}>
        {tabs.map((tab) => (
          <Button
            key={tab.value}
            ref={value === tab.value ? selectedTab : undefined}
            size="compact"
            variant="ghost"
            className={styles.sectionButton}
            aria-pressed={value === tab.value}
            disabled={disabled}
            onClick={() => onChange(tab.value)}
            onFocus={(event) => revealTab(event.currentTarget)}
          >
            <span>{tab.label}</span>
            {tab.count !== undefined && (
              <Badge variant="count" data-selected={value === tab.value}>
                {tab.count}
                <span className="sr-only"> {countNoun}</span>
              </Badge>
            )}
          </Button>
        ))}
      </ScrollStrip>
    </nav>
  );
}
