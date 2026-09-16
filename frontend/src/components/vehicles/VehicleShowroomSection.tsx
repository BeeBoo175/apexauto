import Image from "next/image";
import Link from "next/link";
import { showroomPhoto } from "@/lib/guide/showroom-photos";
import type { ShowroomBranchInfo } from "@/lib/guide/showrooms";
import landing from "./LandingShowrooms.module.css";
import layout from "./VehicleLayout.module.css";
import styles from "./VehicleShowroomSection.module.css";

interface VehicleShowroomSectionProps {
  showroom: ShowroomBranchInfo | null;
  branchName: string;
}

export function VehicleShowroomSection({ showroom, branchName }: VehicleShowroomSectionProps) {
  const photo = showroomPhoto(showroom?.shortLabel ?? "");

  return (
    <section aria-labelledby="vehicle-showroom" className={styles.section}>
      <h2 id="vehicle-showroom" className={layout.sectionHeading}>
        Showroom
      </h2>
      {/* One section of the home page showroom accordion, always open. */}
      <div className={`${landing.panel} ${landing.single}`} data-active="true">
        <div className={landing.photo}>
          <Image
            src={photo.src}
            alt={photo.alt}
            fill
            unoptimized
            sizes="900px"
            className={landing.image}
          />
        </div>
        <div className={landing.scrim} aria-hidden="true" />
        <div className={landing.details}>
          <div className={landing.summary}>
            <h3 className={landing.name}>{showroom?.shortLabel ?? branchName}</h3>
            {showroom && (
              <p className={landing.address}>
                {showroom.address}, {showroom.city} • {showroom.hours}
              </p>
            )}
          </div>
          <Link
            href={showroom ? `/branches?branch=${showroom.id}` : "/branches"}
            className={landing.visit}
          >
            Plan a visit
          </Link>
        </div>
      </div>
    </section>
  );
}
