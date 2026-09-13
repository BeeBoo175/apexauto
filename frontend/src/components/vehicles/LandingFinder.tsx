"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import { useFinderCount } from "@/hooks/use-finder-count";
import { showroomName, type Branch } from "@/lib/branches";
import type { Taxonomy } from "@/lib/intake/contracts";
import { catalogueBudgetOptions } from "@/lib/vehicles/featured";
import { catalogueDefaults, catalogueHref } from "@/lib/vehicles/query";
import styles from "./LandingFinder.module.css";
import layout from "./VehicleLayout.module.css";

interface LandingFinderProps {
  taxonomy: Taxonomy | null;
  branches: Branch[] | null;
  totalVehicles: number | null;
}

export function LandingFinder({ taxonomy, branches, totalVehicles }: LandingFinderProps) {
  const router = useRouter();
  const [make, setMake] = useState("");
  const [model, setModel] = useState("");
  const [maximumPrice, setMaximumPrice] = useState("");
  const [branchId, setBranchId] = useState("");

  const selectedMake = taxonomy?.makes.find((item) => item.name === make);
  const filters = { ...catalogueDefaults, make, model, maximumPrice, branchId };
  const count = useFinderCount(filters, totalVehicles);
  const noMatches = count === 0;

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (noMatches) {
      return;
    }
    router.push(catalogueHref(filters));
  }

  let buttonLabel = "Show cars";
  if (count !== null) {
    buttonLabel = `Show ${count} ${count === 1 ? "car" : "cars"}`;
  }

  return (
    <form
      className={`${styles.finder} ${layout.calmFields}`}
      aria-label="Find a car"
      method="get"
      action="/vehicles"
      onSubmit={handleSubmit}
    >
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
        label="Budget"
        name="maximumPrice"
        value={maximumPrice}
        onChange={(event) => setMaximumPrice(event.target.value)}
      >
        {catalogueBudgetOptions.map((option) => (
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
      <Button type="submit" size="lg" className={styles.submit} disabled={noMatches}>
        {buttonLabel}
      </Button>
    </form>
  );
}
