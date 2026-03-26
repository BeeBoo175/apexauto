import { expect, test } from "@playwright/test";
import { parseCatalogue } from "@/lib/vehicles/parse";
import {
  catalogueDefaults,
  catalogueFilterError,
  catalogueParams,
  catalogueQuery,
} from "@/lib/vehicles/query";

const ids = [
  "47cf2705-95cc-48b6-b907-64bf18dd87da",
  "2f35e1b2-602b-43dc-8c5f-4085893b0ea8",
  "e26136c6-6e1b-46c2-9bb5-c28e2f01dd8c",
];
const vehicle = {
  id: ids[0],
  branchId: ids[1],
  branchName: "Glenmarie",
  make: "Proton",
  model: "Saga",
  trim: "Standard",
  manufactureYear: 2020,
  mileageKilometres: 90000,
  vehiclePrice: 45000,
  transmission: 0,
  fuel: 0,
  coverPhotoId: ids[2],
  photoCount: 4,
  publishedAt: "2026-10-26T00:00:00+00:00",
};

test("catalogue accepts exact eight-item paging and rejects missing, duplicate or private projections", () => {
  const partial = { items: [vehicle], total: 17, page: 3, pageSize: 8 };
  expect(parseCatalogue(partial)?.items[0].transmission).toBe("Automatic");
  expect(parseCatalogue({ items: [], total: 17, page: 4, pageSize: 8 })?.items).toEqual([]);
  for (const pageSize of [0, 10, 25, 50]) {
    expect(parseCatalogue({ ...partial, pageSize })).toBeNull();
  }
  for (const changed of [
    { page: 0 },
    { page: 1.5 },
    { total: -1 },
    { items: [] },
    { items: [vehicle, vehicle], total: 2, page: 1 },
    { items: [{ ...vehicle, id: "invalid" }] },
    { items: [{ ...vehicle, fuel: 5 }] },
    { items: [{ ...vehicle, vehiclePrice: -1 }] },
    { items: [{ ...vehicle, coverPhotoId: null }] },
    { items: [{ ...vehicle, publishedAt: "yesterday" }] },
  ]) {
    expect(parseCatalogue({ ...partial, ...changed })).toBeNull();
  }
  const publicRow = parseCatalogue({
    ...partial,
    items: [{ ...vehicle, buyerEmail: "private", registrationPlate: "private" }],
  })!.items[0];
  expect(publicRow).not.toHaveProperty("buyerEmail");
  expect(publicRow).not.toHaveProperty("registrationPlate");
});

test("catalogue filters round-trip only supported fields and preserve exact price text", () => {
  const state = catalogueParams({
    make: " Proton ",
    minimumPrice: "45000.01",
    fuel: "Electric",
    page: "3",
    customerId: ids[0],
    pageSize: "500",
  });
  const query = Object.fromEntries(new URLSearchParams(catalogueQuery(state.filters, state.page)));
  expect(query).toMatchObject({
    make: "Proton",
    minimumPrice: "45000.01",
    fuel: "Electric",
    page: "3",
  });
  expect(query).not.toHaveProperty("customerId");
  expect(query).not.toHaveProperty("pageSize");
  expect(catalogueFilterError(state.filters)).toBeNull();
  for (const changed of [
    { minimumYear: "1885" },
    { maximumYear: "10000" },
    { minimumYear: "2021", maximumYear: "2020" },
    { minimumPrice: "1.001" },
    { minimumPrice: "2", maximumPrice: "1" },
    { minimumPrice: "9007199254740992" },
    { branchId: "invalid" },
    { fuel: "Hydrogen" },
    { transmission: "CVT" },
    { sort: "customerId" },
    { direction: "sideways" },
    { make: "a\0b" },
  ]) {
    expect(catalogueFilterError({ ...catalogueDefaults, ...changed })).not.toBeNull();
  }
});
