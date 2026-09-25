import Image from "next/image";
import { PhotoAccordion, PhotoPanel } from "@/components/ui/PhotoAccordion";
import { showroomPhoto } from "@/lib/guide/showroom-photos";
import type { ShowroomBranchInfo } from "@/lib/guide/showrooms";
import { ShowroomVisitDetails } from "./ShowroomVisitDetails";
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
      <PhotoAccordion className={styles.panel}>
        <PhotoPanel
          active
          photo={<Image src={photo.src} alt={photo.alt} fill unoptimized sizes="900px" />}
        >
          <ShowroomVisitDetails
            title={showroom?.shortLabel ?? branchName}
            address={showroom && `${showroom.address}, ${showroom.city} • ${showroom.hours}`}
            href={showroom ? `/branches?branchId=${showroom.id}` : "/branches"}
          />
        </PhotoPanel>
      </PhotoAccordion>
    </section>
  );
}
