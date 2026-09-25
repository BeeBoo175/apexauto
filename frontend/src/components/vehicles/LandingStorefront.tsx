"use client";

import { useInitialToast } from "@/hooks/use-initial-toast";
import { useLandingData } from "@/hooks/use-landing-data";
import styles from "./LandingStorefront.module.css";
import { LandingBuyingSteps } from "./LandingBuyingSteps";
import { LandingFeaturedVehicles } from "./LandingFeaturedVehicles";
import { LandingHero } from "./LandingHero";
import { LandingInspectionRecord } from "./LandingInspectionRecord";
import { LandingSellPanel } from "./LandingSellPanel";
import { LandingShowrooms } from "./LandingShowrooms";

export function LandingStorefront({ holdHours }: { holdHours: number }) {
  const data = useLandingData();

  const failure =
    (data &&
      ((!data.vehicles.data && data.vehicles.message) ||
        (!data.taxonomy.data && data.taxonomy.message) ||
        (!data.branches.data && data.branches.message))) ||
    null;
  useInitialToast(failure);

  const branches = data?.branches.data?.items ?? null;
  const vehicles = data?.vehicles.data?.items ?? [];
  const totalVehicles = data?.vehicles.data?.total ?? null;
  // The proof record says "above" only when it describes the lead card.
  const leadDetail = data?.leadDetail ?? null;
  const leadIsProof = leadDetail !== null && leadDetail.listing.id === vehicles[0]?.id;

  return (
    <div className={styles.storefront}>
      <LandingHero
        taxonomy={data?.taxonomy.data ?? null}
        branches={branches}
        totalVehicles={totalVehicles}
        holdHours={holdHours}
      />
      <LandingFeaturedVehicles result={data?.vehicles ?? null} />
      <LandingBuyingSteps holdHours={holdHours} />
      <LandingInspectionRecord
        detail={leadDetail}
        isLeadCar={leadIsProof}
        loading={data === null}
      />
      <LandingSellPanel taxonomy={data?.taxonomy.data} />
      <LandingShowrooms branches={branches} loading={data === null} />
    </div>
  );
}
