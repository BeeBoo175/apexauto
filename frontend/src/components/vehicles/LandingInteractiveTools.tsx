"use client";

import { useState } from "react";
import Link from "next/link";
import { TradeInValuation } from "@/components/sell/TradeInValuation";
import { buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import type { useVehicleTradeIn } from "@/hooks/use-vehicle-trade-in";
import type { Quote, VehicleInput } from "@/lib/intake/contracts";
import type { PublicVehicle } from "@/lib/vehicles/contracts";
import { selectDiverseVehicles } from "@/lib/vehicles/featured";
import { catalogueDefaults, catalogueHref } from "@/lib/vehicles/query";
import { VehicleFinancing, type FinancingPreset } from "./VehicleFinancing";
import styles from "./LandingInteractiveTools.module.css";
import layout from "./VehicleLayout.module.css";

const FALLBACK_BUDGET_PRESETS: readonly FinancingPreset[] = [
  { id: "budget-55k", label: "Compact hatchback budget", total: 55000 },
  { id: "budget-85k", label: "Family sedan or SUV budget", total: 85000 },
  { id: "budget-120k", label: "Executive or hybrid budget", total: 120000 },
];

export function LandingInteractiveTools({
  tradeIn,
  featuredVehicles,
}: {
  tradeIn: ReturnType<typeof useVehicleTradeIn>;
  featuredVehicles: PublicVehicle[];
}) {
  const [clearedQuote, setClearedQuote] = useState<{
    vehicle: VehicleInput;
    quote: Quote;
  } | null>(null);

  const distinctVehicles = selectDiverseVehicles(featuredVehicles, 4);
  const presets: FinancingPreset[] =
    distinctVehicles.length > 0
      ? distinctVehicles.map((vehicle) => ({
          id: vehicle.id,
          label: `${vehicle.manufactureYear} ${vehicle.make} ${vehicle.model} ${vehicle.trim}`,
          total: vehicle.vehiclePrice,
        }))
      : [...FALLBACK_BUDGET_PRESETS];

  const activeAllowance =
    tradeIn.accepted && clearedQuote !== tradeIn.accepted ? tradeIn.accepted.quote.minValuation : 0;

  return (
    <section
      className={`${layout.band} ${styles.section}`}
      aria-label="Trade-in valuation and financing calculator"
    >
      <div className={`${layout.content} ${styles.inner}`}>
        <div id="trade-in-calculator" className={styles.toolBlock}>
          <TradeInValuation
            tradeIn={tradeIn}
            headingId="landing-trade-in"
            heading="Estimate your car’s value"
            description="Get an indicative MYR valuation and apply it directly to your monthly loan estimate."
            allowanceLabel="Synced to financing estimate"
            unavailableNotice="Vehicle options are currently unavailable. You can still test monthly repayments in the financing calculator below."
            primaryResultAction={
              <div className={styles.tradeInActions}>
                <Link
                  href="/sell"
                  prefetch={false}
                  className={buttonClassName("primary", "md")}
                  tabIndex={tradeIn.editing ? -1 : undefined}
                >
                  Book inspection to sell <Icon name="next" />
                </Link>
                <a
                  href="#financing-calculator"
                  className={buttonClassName("secondary", "md")}
                  tabIndex={tradeIn.editing ? -1 : undefined}
                >
                  Apply to monthly calculator
                </a>
              </div>
            }
          />
        </div>

        <div id="financing-calculator" className={styles.divider}>
          <VehicleFinancing
            headingId="landing-financing"
            heading="Plan your monthly repayments"
            description="Select a car or enter a budget to estimate monthly instalments over 1 to 9 years."
            total={presets[0].total}
            tradeInAllowance={activeAllowance}
            presets={presets}
            allowCustomTotal
            onClearTradeIn={
              activeAllowance > 0 && tradeIn.accepted
                ? () => setClearedQuote(tradeIn.accepted)
                : undefined
            }
            browseHref={(budget) =>
              catalogueHref({ ...catalogueDefaults, maximumPrice: budget.toFixed(2) })
            }
          />
        </div>
      </div>
    </section>
  );
}
