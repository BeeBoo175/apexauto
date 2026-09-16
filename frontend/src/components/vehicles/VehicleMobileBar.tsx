"use client";

import { useEffect, useState } from "react";
import { buttonClassName } from "@/components/ui/Button";
import styles from "./VehicleMobileBar.module.css";

interface VehicleMobileBarProps {
  price: string;
  reserveLabel: string;
}

// On phones the price and Reserve stay in a bottom bar once the price box's own actions scroll out of view, so
// only one red action is on screen at a time.
export function VehicleMobileBar({ price, reserveLabel }: VehicleMobileBarProps) {
  const [actionsVisible, setActionsVisible] = useState(true);

  useEffect(() => {
    const actions = document.getElementById("vehicle-actions");
    if (!actions) {
      return;
    }
    const observer = new IntersectionObserver(([entry]) => setActionsVisible(entry.isIntersecting));
    observer.observe(actions);
    return () => observer.disconnect();
  }, []);

  return (
    <div className={styles.bar} data-hidden={actionsVisible || undefined}>
      <span className={styles.price}>{price}</span>
      <a
        href="#vehicle-actions"
        className={buttonClassName("primary", "md")}
        tabIndex={actionsVisible ? -1 : undefined}
      >
        {reserveLabel}
      </a>
    </div>
  );
}
