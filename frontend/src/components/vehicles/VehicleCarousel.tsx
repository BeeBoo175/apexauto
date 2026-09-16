"use client";

import { useRef } from "react";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { prefersReducedMotion } from "@/lib/motion";
import type { PublicVehicle } from "@/lib/vehicles/contracts";
import { VehicleCard } from "./VehicleCard";
import styles from "./VehicleCarousel.module.css";

// A horizontal, scroll-snapped row of the canonical catalogue card with step buttons.
export function VehicleCarousel({
  vehicles,
  label,
}: {
  vehicles: readonly PublicVehicle[];
  label: string;
}) {
  const track = useRef<HTMLUListElement>(null);

  function step(direction: -1 | 1) {
    const element = track.current;
    if (!element) {
      return;
    }
    element.scrollBy({
      left: direction * element.clientWidth * 0.85,
      behavior: prefersReducedMotion() ? "instant" : "smooth",
    });
  }

  return (
    <div
      className={styles.carousel}
      role="group"
      aria-roledescription="carousel"
      aria-label={label}
    >
      <ul ref={track} className={styles.track}>
        {vehicles.map((vehicle) => (
          <li key={vehicle.id} className={styles.slide}>
            <VehicleCard vehicle={vehicle} first={false} photoVersion={0} />
          </li>
        ))}
      </ul>
      <div className={styles.controls}>
        <Button
          type="button"
          size="icon"
          variant="secondary"
          aria-label={`Previous cars, ${label}`}
          onClick={() => step(-1)}
        >
          <Icon name="previous" />
        </Button>
        <Button
          type="button"
          size="icon"
          variant="secondary"
          aria-label={`Next cars, ${label}`}
          onClick={() => step(1)}
        >
          <Icon name="next" />
        </Button>
      </div>
    </div>
  );
}
