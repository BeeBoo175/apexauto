"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Button, buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { Select } from "@/components/ui/Input";
import { showroomName, type Branch } from "@/lib/branches";
import type { Taxonomy } from "@/lib/intake/contracts";
import { FEATURED_QUICK_FILTERS } from "@/lib/vehicles/featured";
import { catalogueDefaults, catalogueHref } from "@/lib/vehicles/query";
import styles from "./LandingHero.module.css";

const BUDGET_OPTIONS = [
  { value: "", label: "Any budget" },
  { value: "50000", label: "Up to MYR 50,000" },
  { value: "75000", label: "Up to MYR 75,000" },
  { value: "100000", label: "Up to MYR 100,000" },
  { value: "150000", label: "Up to MYR 150,000" },
] as const;

const HERO_QUICK_LINKS = FEATURED_QUICK_FILTERS.filter((chip) => chip.showInHero);

interface LandingHeroWorkbenchProps {
  taxonomy: Taxonomy | null;
  branches: Branch[] | null;
  totalVehicles: number | null;
  onStartTradeInMake: (make: string) => void;
}

export function LandingHeroWorkbench({
  taxonomy,
  branches,
  totalVehicles,
  onStartTradeInMake,
}: LandingHeroWorkbenchProps) {
  const router = useRouter();
  const [mode, setMode] = useState<"buy" | "sell">("buy");
  const [make, setMake] = useState("");
  const [model, setModel] = useState("");
  const [maximumPrice, setMaximumPrice] = useState("");
  const [branchId, setBranchId] = useState("");
  const [sellMake, setSellMake] = useState("");

  const selectedMake = taxonomy?.makes.find((item) => item.name === make);

  function handleBuySubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    router.push(
      catalogueHref({
        ...catalogueDefaults,
        make,
        model,
        maximumPrice,
        branchId,
      }),
    );
  }

  function handleSellSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onStartTradeInMake(sellMake);
  }

  return (
    <aside className={styles.workbench} aria-label="Vehicle finder and instant valuation">
      <div className={styles.tabs} role="group" aria-label="Choose buyer or seller flow">
        <Button
          type="button"
          variant={mode === "buy" ? "secondary" : "ghost"}
          size="compact"
          aria-pressed={mode === "buy"}
          onClick={() => setMode("buy")}
        >
          Buy a car
        </Button>
        <Button
          type="button"
          variant={mode === "sell" ? "secondary" : "ghost"}
          size="compact"
          aria-pressed={mode === "sell"}
          onClick={() => setMode("sell")}
        >
          Sell or trade in
        </Button>
      </div>

      {mode === "buy" ? (
        <form
          className={styles.workbenchForm}
          method="get"
          action="/vehicles"
          onSubmit={handleBuySubmit}
        >
          <div className={styles.workbenchFields}>
            <Select
              label="Make"
              name="make"
              value={make}
              disabled={!taxonomy}
              onChange={(event) => {
                setMake(event.target.value);
                setModel("");
              }}
            >
              <option value="">All makes</option>
              {taxonomy?.makes.map((item) => (
                <option key={item.name} value={item.name}>
                  {item.name}
                </option>
              ))}
            </Select>
            <Select
              label="Model"
              name="model"
              value={model}
              disabled={!selectedMake}
              onChange={(event) => setModel(event.target.value)}
            >
              <option value="">All models</option>
              {selectedMake?.models.map((item) => (
                <option key={item.name} value={item.name}>
                  {item.name}
                </option>
              ))}
            </Select>
            <Select
              label="Maximum budget"
              name="maximumPrice"
              value={maximumPrice}
              onChange={(event) => setMaximumPrice(event.target.value)}
            >
              {BUDGET_OPTIONS.map((option) => (
                <option key={option.value || "any"} value={option.value}>
                  {option.label}
                </option>
              ))}
            </Select>
            <Select
              label="Showroom"
              name="branchId"
              value={branchId}
              disabled={!branches || branches.length === 0}
              onChange={(event) => setBranchId(event.target.value)}
            >
              <option value="">All showrooms</option>
              {branches?.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {showroomName(branch.name)}
                </option>
              ))}
            </Select>
          </div>
          <Button type="submit" size="lg">
            {totalVehicles !== null
              ? `Search ${totalVehicles} available cars`
              : "Search available cars"}
          </Button>
          <div className={styles.quickLinks}>
            <span>Quick browse:</span>
            {HERO_QUICK_LINKS.map((chip) => (
              <Link key={chip.id} href={catalogueHref(chip.filters)} prefetch={false}>
                {chip.label}
              </Link>
            ))}
          </div>
        </form>
      ) : (
        <form className={styles.workbenchForm} onSubmit={handleSellSubmit}>
          <p className={styles.sellCopy}>
            Check your vehicle’s indicative MYR range right below, apply it to a monthly payment
            estimate, or book an intake slot online.
          </p>
          <ul className={styles.sellHighlights}>
            <li>
              <Icon name="inspection" />
              <span>Instant non-binding valuation by make, trim and mileage</span>
            </li>
            <li>
              <Icon name="car" />
              <span>Four exterior photos and quick showroom appointment booking</span>
            </li>
            <li>
              <Icon name="calendar" />
              <span>Apply your estimate directly when reserving your next car</span>
            </li>
          </ul>
          {taxonomy && (
            <Select
              label="Your current car make"
              value={sellMake}
              onChange={(event) => setSellMake(event.target.value)}
            >
              <option value="">Choose make to pre-fill estimator</option>
              {taxonomy.makes.map((item) => (
                <option key={item.name} value={item.name}>
                  {item.name}
                </option>
              ))}
            </Select>
          )}
          <div className={styles.sellActions}>
            <Button type="submit" size="lg">
              Estimate trade-in value
            </Button>
            <Link href="/sell" prefetch={false} className={buttonClassName("secondary", "lg")}>
              Book inspection
            </Link>
          </div>
        </form>
      )}
    </aside>
  );
}
