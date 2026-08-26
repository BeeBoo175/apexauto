"use client";

import { useState } from "react";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { Input, Select } from "@/components/ui/Input";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { formatCurrency } from "@/lib/currency-format";
import { RESERVATION_DEPOSIT, RESERVATION_HOLD_HOURS } from "@/lib/vehicles/deposit-terms";
import { parseMyr } from "@/lib/currency-input";
import {
  calculateGuideOtr,
  DEFAULT_PROCESSING_FEE,
  guidePriceTiers,
  guideRoadTaxOptions,
  guideSamplePromos,
} from "@/lib/guide/buying-guide-math";
import guideLayout from "./GuideLayout.module.css";
import styles from "./BuyingGuideSections.module.css";

export function BuyingGuidePricingSection() {
  const [selectedTierId, setSelectedTierId] = useState(guidePriceTiers[1].id);
  const [priceDraft, setPriceDraft] = useState(guidePriceTiers[1].vehiclePrice.toFixed(2));
  const [roadTaxAmount, setRoadTaxAmount] = useState(guidePriceTiers[1].roadTax);
  const [selectedPromoCode, setSelectedPromoCode] = useState(guideSamplePromos[0].code);

  const parsedPrice = parseMyr(priceDraft);
  const promo =
    guideSamplePromos.find((item) => item.code === selectedPromoCode) ?? guideSamplePromos[0];
  const estimate =
    parsedPrice !== null && parsedPrice > 0
      ? calculateGuideOtr(parsedPrice, roadTaxAmount, promo.discount, DEFAULT_PROCESSING_FEE)
      : null;

  function applyTier(tierId: string) {
    const tier = guidePriceTiers.find((item) => item.id === tierId);
    if (!tier) {
      return;
    }
    setSelectedTierId(tier.id);
    setPriceDraft(tier.vehiclePrice.toFixed(2));
    setRoadTaxAmount(tier.roadTax);
  }

  return (
    <div className={styles.split}>
      <div className={`${styles.formColumn} ${layout.calmFields}`}>
        <div className={styles.chipGroup}>
          <span className={styles.chipGroupLabel}>Vehicle presets</span>
          <div className={styles.presetGrid}>
            {guidePriceTiers.map((tier) => (
              <button
                key={tier.id}
                type="button"
                className={guideLayout.chip}
                aria-pressed={selectedTierId === tier.id}
                onClick={() => applyTier(tier.id)}
              >
                {tier.label}
              </button>
            ))}
          </div>
        </div>

        <div className={styles.fieldGrid}>
          <Input
            id="guide-otr-price"
            label="Vehicle price (MYR)"
            inputMode="decimal"
            maxLength={14}
            value={priceDraft}
            aria-invalid={estimate === null ? true : undefined}
            onChange={(event) => {
              setSelectedTierId("custom");
              setPriceDraft(event.target.value);
            }}
          />
          <Select
            id="guide-otr-road-tax"
            label="Road tax tier"
            value={String(roadTaxAmount)}
            onChange={(event) => setRoadTaxAmount(Number(event.target.value))}
          >
            {guideRoadTaxOptions.map((option) => (
              <option key={option.amount} value={option.amount}>
                {option.label}
              </option>
            ))}
          </Select>
        </div>

        <div className={styles.chipGroup}>
          <span className={styles.chipGroupLabel}>Sample promo code</span>
          <div className={styles.presetGrid}>
            {guideSamplePromos.map((item) => (
              <button
                key={item.code}
                type="button"
                className={guideLayout.chip}
                aria-pressed={selectedPromoCode === item.code}
                onClick={() => setSelectedPromoCode(item.code)}
              >
                {item.label}
              </button>
            ))}
          </div>
          <p className={styles.helperText}>One promo code per booking.</p>
        </div>

        <div className={styles.holdRule}>
          <strong>
            How the {RESERVATION_HOLD_HOURS}-hour {formatCurrency(RESERVATION_DEPOSIT, "RM")} hold
            works
          </strong>
          <ul className={styles.holdBullets}>
            <li>
              <Icon name="check" />
              <span>
                Locks the vehicle for {RESERVATION_HOLD_HOURS} hours toward your down payment
              </span>
            </li>
            <li>
              <Icon name="check" />
              <span>Instant simulated refund if cancelled or expired</span>
            </li>
          </ul>
        </div>
      </div>

      <aside className={guideLayout.receiptPanel} aria-label="OTR price breakdown">
        <div className={guideLayout.receiptHeader}>
          <h3 className={layout.cardHeading}>OTR price breakdown</h3>
          <span className={guideLayout.receiptMeta}>8% SST on fee only</span>
        </div>

        {estimate === null ? (
          <p className={styles.fieldError}>
            Enter a positive vehicle price in MYR to calculate OTR costs.
          </p>
        ) : (
          <>
            <dl className={guideLayout.ledger}>
              <div className={guideLayout.ledgerRow}>
                <dt>Vehicle price</dt>
                <dd>{formatCurrency(estimate.vehiclePrice)}</dd>
              </div>
              {estimate.promoDiscount > 0 && (
                <div className={`${guideLayout.ledgerRow} ${guideLayout.ledgerDiscount}`}>
                  <dt>Promo discount ({promo.code})</dt>
                  <dd>−{formatCurrency(estimate.promoDiscount)}</dd>
                </div>
              )}
              <div className={guideLayout.ledgerRow}>
                <dt>Processing fee</dt>
                <dd>{formatCurrency(estimate.processingFee)}</dd>
              </div>
              <div className={guideLayout.ledgerRow}>
                <dt>Road tax (illustrative tier)</dt>
                <dd>{formatCurrency(estimate.roadTax)}</dd>
              </div>
              <div className={guideLayout.ledgerRow}>
                <dt>Service tax (8% SST on fee)</dt>
                <dd>{formatCurrency(estimate.serviceTax)}</dd>
              </div>
            </dl>

            <div className={styles.totalBand} aria-live="polite" aria-atomic="true">
              <span>Estimated OTR total</span>
              <strong>{formatCurrency(estimate.total)}</strong>
            </div>

            <p className={guideLayout.receiptNote}>
              Excludes comprehensive insurance and optional ApexShield coverage.
            </p>
          </>
        )}

        <div>
          <Link href="/vehicles" prefetch={false} className={buttonClassName("primary", "md")}>
            Browse inspected cars
          </Link>
        </div>
      </aside>
    </div>
  );
}
