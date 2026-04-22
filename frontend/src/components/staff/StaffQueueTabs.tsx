import { useEffect, useRef } from "react";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
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
  mobilePresentation = "tabs",
  onChange,
}: {
  label: string;
  tabs: readonly { value: T; label: string }[];
  value: T;
  disabled?: boolean;
  mobilePresentation?: "tabs" | "select";
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
    <nav
      className={styles.sections}
      aria-label={label}
      data-mobile-presentation={mobilePresentation}
    >
      {mobilePresentation === "select" && (
        <div className={styles.mobileSelect}>
          <Select
            label={label}
            labelHidden
            value={value}
            disabled={disabled}
            onChange={(event) => {
              const selected = tabs.find((tab) => tab.value === event.target.value);
              if (selected) {
                onChange(selected.value);
              }
            }}
          >
            {tabs.map((tab) => (
              <option key={tab.value} value={tab.value}>
                {tab.label}
              </option>
            ))}
          </Select>
        </div>
      )}
      <div className={styles.tabStrip}>
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
            {tab.label}
          </Button>
        ))}
      </div>
    </nav>
  );
}
