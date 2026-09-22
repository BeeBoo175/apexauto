import { expect, test } from "@playwright/test";
import { categoryTitle } from "@/lib/guide/inspection-rules";
import { fuelLabel, fuels } from "@/lib/vehicles/contracts";
import { vehicleTitle } from "@/lib/vehicles/vehicle-title";

test("fuel labels read the same everywhere", () => {
  expect(fuels.map((fuel) => fuelLabel(fuel))).toEqual([
    "Petrol",
    "Diesel",
    "Hybrid",
    "Plug-in hybrid",
    "Electric",
  ]);
  expect(fuelLabel("Plug-in hybrid")).toBe("Plug-in hybrid");
});

test("vehicle titles join year, make, model and trim", () => {
  expect(
    vehicleTitle({ manufactureYear: 2020, make: "Proton", model: "Saga", trim: "Standard" }),
  ).toBe("2020 Proton Saga Standard");
});

test("inspection area titles use the guide names and fall back to a spaced name", () => {
  expect(categoryTitle("FrameChassis")).toBe("Frame & Chassis");
  expect(categoryTitle("UnderbodyBrakesSuspension")).toBe("Underbody, Brakes & Suspension");
  expect(categoryTitle("RoofRack")).toBe("Roof Rack");
});
