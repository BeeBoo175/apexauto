"use client";

import { useState } from "react";
import Image from "next/image";
import Link from "next/link";
import patterns from "@/components/layout/PublicPatterns.module.css";
import type { Branch } from "@/lib/branches";
import { resolveShowrooms } from "@/lib/guide/showrooms";
import type { PublicVehicle } from "@/lib/vehicles/contracts";
import styles from "./LandingShowrooms.module.css";
import layout from "./VehicleLayout.module.css";

interface LandingShowroomsProps {
  branches: Branch[] | null;
  vehicles: PublicVehicle[];
}

interface ShowroomPhoto {
  src: string;
  alt: string;
}

const SHOWROOM_PHOTOS: Record<string, ShowroomPhoto> = {
  glenmarie: {
    src: "/images/showroom-panorama.jpg",
    alt: "ApexAuto Glenmarie experience gallery floor with vehicle lineup",
  },
  "petaling jaya": {
    src: "/images/showroom-seasia-floor.jpg",
    alt: "ApexAuto Petaling Jaya showroom and display bays",
  },
  "bayan lepas": {
    src: "/images/showroom-architecture.jpg",
    alt: "ApexAuto Bayan Lepas showroom floor",
  },
};

const DEFAULT_PHOTO: ShowroomPhoto = {
  src: "/images/showroom-panorama.jpg",
  alt: "ApexAuto vehicle display floor",
};

function showroomNumber(index: number) {
  return String(index + 1).padStart(2, "0");
}

export function LandingShowrooms({ branches, vehicles }: LandingShowroomsProps) {
  const showrooms = resolveShowrooms(branches ?? []);
  const [activeId, setActiveId] = useState(showrooms[0]?.id ?? "");

  if (showrooms.length === 0) {
    return null;
  }

  return (
    <section
      className={`${patterns.band} ${patterns.bandCanvas}`}
      aria-labelledby="landing-showrooms-heading"
    >
      <div className={layout.content}>
        <div className={styles.heading}>
          <h2 id="landing-showrooms-heading" className={patterns.sectionTitle}>
            Come visit us
          </h2>
          <p className={patterns.sectionSub}>
            Open Mon–Sat, 09:00–18:00 for test drives and trade-ins.
          </p>
        </div>

        <ul className={styles.accordion}>
          {showrooms.map((showroom, index) => {
            const active = showroom.id === activeId;
            const photo = SHOWROOM_PHOTOS[showroom.shortLabel.toLowerCase()] ?? DEFAULT_PHOTO;
            const stockCount = vehicles.filter(
              (vehicle) => vehicle.branchId === showroom.id,
            ).length;
            return (
              <li key={showroom.id} className={styles.panel} data-active={active}>
                <div className={styles.photo}>
                  <Image
                    src={photo.src}
                    alt={active ? photo.alt : ""}
                    fill
                    unoptimized
                    sizes="900px"
                    className={styles.image}
                  />
                </div>
                <div className={styles.scrim} aria-hidden="true" />
                <button
                  type="button"
                  className={styles.trigger}
                  aria-expanded={active}
                  aria-label={`${showroomNumber(index)} ${showroom.shortLabel} showroom`}
                  onClick={() => setActiveId(showroom.id)}
                >
                  <span className={styles.collapsedName} aria-hidden="true">
                    {showroomNumber(index)} {showroom.shortLabel}
                  </span>
                </button>
                <div className={styles.details} aria-hidden={!active}>
                  <div className={styles.summary}>
                    <h3 className={styles.name}>
                      <span>{showroomNumber(index)} /</span> {showroom.shortLabel}
                    </h3>
                    <p className={styles.address}>
                      {showroom.address}, {showroom.city} • {showroom.hours}
                      {stockCount > 0 ? ` • ${stockCount} cars on display` : ""}
                    </p>
                  </div>
                  <Link
                    href={`/branches?branch=${showroom.id}`}
                    tabIndex={active ? undefined : -1}
                    className={styles.visit}
                  >
                    Plan a visit
                  </Link>
                </div>
              </li>
            );
          })}
        </ul>
      </div>
    </section>
  );
}
