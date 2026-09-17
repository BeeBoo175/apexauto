"use client";

import { useEffect, useState } from "react";
import { Icon } from "@/components/ui/Icon";
import { prefersReducedMotion } from "@/lib/motion";
import styles from "./CatalogueBackToTop.module.css";

// Appears once the first screens of cars are behind the visitor.
const SHOW_AFTER_PX = 900;

export function CatalogueBackToTop() {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    function update() {
      setVisible(window.scrollY > SHOW_AFTER_PX);
    }
    update();
    window.addEventListener("scroll", update, { passive: true });
    return () => window.removeEventListener("scroll", update);
  }, []);

  return (
    <button
      type="button"
      className={styles.button}
      data-visible={visible || undefined}
      aria-label="Back to top"
      tabIndex={visible ? 0 : -1}
      onClick={() =>
        window.scrollTo({ top: 0, behavior: prefersReducedMotion() ? "auto" : "smooth" })
      }
    >
      <span className={styles.arrow}>
        <Icon name="down" />
      </span>
    </button>
  );
}
