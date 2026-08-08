"use client";

import { useEffect, useState } from "react";
import styles from "./GuideLayout.module.css";
import layout from "@/components/vehicles/VehicleLayout.module.css";

export interface JumpNavItem {
  id: string;
  label: string;
}

export function GuideJumpBar({ label, items }: { label: string; items: readonly JumpNavItem[] }) {
  const [activeId, setActiveId] = useState(items[0]?.id ?? "");

  useEffect(() => {
    const syncHash = () => {
      const hash = window.location.hash.replace(/^#/, "");
      if (hash && items.some((item) => item.id === hash)) {
        setActiveId(hash);
      }
    };

    syncHash();
    window.addEventListener("hashchange", syncHash);

    const targets = items
      .map((item) => document.getElementById(item.id))
      .filter((element): element is HTMLElement => element !== null);

    if (targets.length === 0 || typeof IntersectionObserver === "undefined") {
      return () => window.removeEventListener("hashchange", syncHash);
    }

    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((entry) => entry.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);
        if (visible[0]?.target.id) {
          setActiveId(visible[0].target.id);
        }
      },
      { rootMargin: "-140px 0px -55% 0px", threshold: 0.01 },
    );

    for (const target of targets) {
      observer.observe(target);
    }

    return () => {
      observer.disconnect();
      window.removeEventListener("hashchange", syncHash);
    };
  }, [items]);

  return (
    <nav aria-label={label} className={styles.jumpNav}>
      <div className={`${layout.content} ${styles.jumpNavInner}`}>
        {items.map((item) => (
          <a
            key={item.id}
            href={`#${item.id}`}
            aria-current={activeId === item.id ? "location" : undefined}
            onClick={() => setActiveId(item.id)}
          >
            {item.label}
          </a>
        ))}
      </div>
    </nav>
  );
}
