import { expect, test } from "@playwright/test";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { parseListings } from "@/lib/vehicles/parse-listings";
import { parseListingWorkspace } from "@/lib/vehicles/listing-workspace";
import {
  listingDraft,
  listingDraftContents,
  matchesListingSave,
  parseListingSave,
  parseListingReceipt,
} from "@/lib/vehicles/listing-draft";
import {
  hasListingAccess,
  listingParams,
  listingQuery,
  listingPublication,
} from "@/lib/vehicles/listings";
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

test("listing draft preserves exact prices and validates immutable save receipts", () => {
  const value = {
    vehiclePrice: "45000.01",
    roadTax: "90.00",
    transmission: "Manual",
    fuel: "Hybrid",
    description: " Maintained vehicle\nReady to view ",
    photoIds: [ids[2], ids[1]],
  };
  const contents = listingDraftContents(value)!;
  expect(contents).toEqual({
    vehiclePrice: 45000.01,
    roadTax: 90,
    transmission: 1,
    fuel: 2,
    description: "Maintained vehicle\nReady to view",
    photoIds: value.photoIds,
  });
  for (const changed of [
    { vehiclePrice: "1.001" },
    { roadTax: "-1" },
    { vehiclePrice: "9007199254740992" },
    { transmission: "CVT" },
    { fuel: "Hydrogen" },
    { description: " " },
    { description: "a\0b" },
    { description: "a".repeat(2001) },
    { photoIds: [ids[2], ids[2]] },
    { photoIds: ["invalid"] },
  ]) {
    expect(listingDraftContents({ ...value, ...changed })).toBeNull();
  }
  const request = {
    ...contents,
    operationId: ids[2],
    expectedVersion: 1,
    photoIds: [ids[2], ids[1]],
  };
  const raw = {
    ...contents,
    id: ids[2],
    actorId: ids[1],
    vehicleId: ids[0],
    branchId: ids[1],
    version: 2,
    recordedAt: vehicle.publishedAt,
    photoIds: request.photoIds,
    privateNotes: "omitted",
  };
  const receipt = parseListingSave({ status: 0, draft: raw })!;
  expect(parseListingReceipt(receipt)).toEqual(receipt);
  expect(receipt).not.toHaveProperty("privateNotes");
  expect(listingDraft(receipt)).toEqual({ ...value, description: contents.description });
  expect(matchesListingSave(receipt, request)).toBe(true);
  for (const changed of [
    { operationId: ids[1] },
    { expectedVersion: 2 },
    { vehiclePrice: 45000.02 },
    { roadTax: 91 },
    { transmission: 0 },
    { fuel: 0 },
    { description: "Changed" },
    { photoIds: [...request.photoIds].reverse() },
    { photoIds: [] },
  ]) {
    expect(matchesListingSave(receipt, { ...request, ...changed })).toBe(false);
  }
  for (const changed of [
    { actorId: "invalid" },
    { id: "invalid" },
    { version: 0 },
    { photoIds: [ids[2], ids[2]] },
  ]) {
    expect(parseListingSave({ status: 0, draft: { ...raw, ...changed } })).toBeNull();
  }
  expect(parseListingSave({ status: 4, draft: raw })).toBeNull();
});

test("manager listings validate private projections and independent staff paging", () => {
  const row = {
    vehicleId: ids[0],
    branchId: ids[1],
    branchName: "Glenmarie",
    registrationPlate: "VCA 5172",
    make: "Proton",
    model: "Saga",
    trim: "Standard",
    manufactureYear: 2020,
    mileageKilometres: 90000,
    vehicleStatus: 2,
    revisionId: ids[2],
    version: 1,
    vehiclePrice: 45000,
    publicationId: ids[1],
    publishedRevisionId: ids[2],
    isWithdrawn: false,
    isPublic: true,
    approvedVehiclePrice: 45000,
    savedAt: "2026-10-26T00:00:00Z",
    approvedAt: "2026-10-26T00:00:00Z",
    withdrawnAt: null,
    isReserved: false,
    hasCurrentPublicationInspection: true,
  };
  const page = { items: [row], total: 1, page: 1, pageSize: 10 };
  const parsed = parseListings(page)!.items[0];
  expect(parsed.vehicleStatus).toBe("Available");
  expect(listingPublication(parsed)).toBe("Published");
  expect(listingPublication({ ...parsed, revisionId: ids[0] })).toBe(
    "Published · saved changes not live",
  );
  expect(listingPublication({ ...parsed, isPublic: false })).toBe("Hidden");
  for (const changed of [
    { vehicleId: "invalid" },
    { vehicleStatus: 3 },
    { vehiclePrice: -1 },
    { version: 1.5 },
    { publicationId: "invalid" },
    { isPublic: "true" },
    { vehiclePrice: Infinity },
  ]) {
    expect(parseListings({ ...page, items: [{ ...row, ...changed }] })).toBeNull();
  }
  expect(parseListings({ ...page, pageSize: 8 })).toBeNull();
  expect(parseListings({ ...page, items: [row, row], total: 2 })).toBeNull();
  expect(
    parseListings({ ...page, items: [{ ...row, buyerEmail: "private" }] })!.items[0],
  ).not.toHaveProperty("buyerEmail");
  const query = listingParams({
    page: "2",
    pageSize: "10",
    branchId: ids[1],
    search: " Saga ",
    customerId: ids[0],
  });
  expect(Object.fromEntries(new URLSearchParams(listingQuery(query.filters, query.page)))).toEqual({
    page: "2",
    pageSize: "10",
    branchId: ids[1],
    search: "Saga",
  });
  expect(listingParams({ page: "-1", pageSize: "8", branchId: "invalid" })).toEqual({
    page: 1,
    filters: {
      search: "",
      pageSize: 25,
      branchId: "",
      sort: "",
      direction: "asc",
      registration: "",
      vehicle: "",
      inspection: "",
      publication: "",
      minimumPrice: "",
      maximumPrice: "",
      minimumPublishedPrice: "",
      maximumPublishedPrice: "",
      from: "",
      to: "",
    },
    listingId: "",
    section: "listing",
    record: null,
  });
  for (const role of [
    "Customer",
    "SalesAgent",
    "WorkshopTechnician",
    "Administrator",
    "BranchManager",
    "GeneralManager",
  ] as const) {
    expect(hasListingAccess({ userId: ids[0], role, branchId: null })).toBe(
      ["BranchManager", "GeneralManager"].includes(role),
    );
  }
});

test("listing sort query round-trips supported columns and clears invalid sort inputs", () => {
  for (const sort of ["registration", "vehicle", "branch", "inspection", "price", "publication"]) {
    const state = listingParams({ sort, direction: "desc", page: "2" });
    expect(
      Object.fromEntries(new URLSearchParams(listingQuery(state.filters, state.page))),
    ).toMatchObject({ sort, direction: "desc", page: "2" });
  }
  const invalid = listingParams({ sort: "customerEmail", direction: "sideways" });
  expect(invalid.filters).toMatchObject({ sort: "", direction: "asc" });
  expect(new URLSearchParams(listingQuery(invalid.filters)).has("sort")).toBe(false);
});

test("listing workspace validates revision and publication relationships before rendering", () => {
  const row = {
    ...vehicle,
    vehicleId: ids[0],
    registrationPlate: "VCA 5172",
    vehicleStatus: "Available",
    revisionId: ids[2],
    version: 1,
    publicationId: ids[1],
    publishedRevisionId: ids[2],
    isWithdrawn: false,
    isPublic: true,
    approvedVehiclePrice: 45000,
    savedAt: vehicle.publishedAt,
    approvedAt: vehicle.publishedAt,
    withdrawnAt: null,
    isReserved: false,
    hasCurrentPublicationInspection: true,
  };
  const revision = {
    id: ids[2],
    vehicleId: ids[0],
    branchId: ids[1],
    actorId: ids[1],
    version: 1,
    vehiclePrice: 45000,
    roadTax: 90,
    transmission: 0,
    fuel: 0,
    description: "Maintained Proton Saga",
    recordedAt: vehicle.publishedAt,
    photoIds: [ids[2]],
  };
  const publication = {
    id: ids[1],
    vehicleId: ids[0],
    branchId: ids[1],
    revisionId: ids[2],
    signedRevisionId: ids[2],
    recordedAt: vehicle.publishedAt,
  };
  const workspace = {
    vehicle: row,
    draft: revision,
    approvedRevision: revision,
    publication,
    withdrawal: null,
    currentSignedRevisionId: ids[2],
    currentSignedReport: { inspectionId: ids[0], revision: 2, privateNotes: "omitted" },
    vin: "MHKA1BA1JGK123456",
    hold: { buyer: "Nur Aina", agent: null, expiresAtUtc: "2026-10-28T00:00:00Z" },
  };
  expect(parseListingWorkspace(workspace)?.vin).toBe("MHKA1BA1JGK123456");
  expect(parseListingWorkspace({ ...workspace, hold: undefined })?.hold).toBeNull();
  expect(parseListingWorkspace({ ...workspace, hold: { buyer: "" } })).toBeNull();
  expect(parseListingWorkspace({ ...workspace, vin: "" })).toBeNull();
  expect(parseListingWorkspace(workspace)?.draft?.transmission).toBe("Automatic");
  expect(parseListingWorkspace(parseListingWorkspace(workspace))).toEqual(
    parseListingWorkspace(workspace),
  );
  const withdrawnWorkspace = {
    ...workspace,
    vehicle: { ...row, isWithdrawn: true, isPublic: false },
    withdrawal: {
      vehicleId: ids[0],
      branchId: ids[1],
      publicationId: ids[1],
      reason: "Listing review",
      recordedAt: vehicle.publishedAt,
    },
  };
  expect(parseListingWorkspace(withdrawnWorkspace)?.withdrawal?.reason).toBe("Listing review");
  expect(parseListingWorkspace(parseListingWorkspace(withdrawnWorkspace))).toEqual(
    parseListingWorkspace(withdrawnWorkspace),
  );
  expect(parseListingWorkspace(workspace)?.draft).not.toHaveProperty("actorId");
  expect(parseListingWorkspace(workspace)?.currentSignedReport).toEqual({
    inspectionId: ids[0],
    revision: 2,
  });
  for (const changed of [
    { draft: { ...revision, vehicleId: ids[1] } },
    { draft: { ...revision, branchId: ids[0] } },
    { draft: { ...revision, version: 2 } },
    { draft: { ...revision, id: ids[0] } },
    { draft: { ...revision, photoIds: [ids[2], ids[2]] } },
    { draft: { ...revision, transmission: 8 } },
    { draft: { ...revision, roadTax: -1 } },
    { draft: { ...revision, recordedAt: "yesterday" } },
    { publication: { ...publication, revisionId: ids[0] } },
    { publication: { ...publication, branchId: ids[0] } },
    { approvedRevision: null, publication: { ...publication, revisionId: undefined } },
    { publication: null },
    { withdrawal: { publicationId: ids[1], reason: "Withdrawn" } },
    { currentSignedRevisionId: "invalid" },
    { currentSignedRevisionId: null },
    { currentSignedReport: null },
    { currentSignedReport: { inspectionId: "invalid", revision: 2 } },
    { currentSignedReport: { inspectionId: ids[0], revision: 0 } },
    { currentSignedReport: { inspectionId: ids[0], revision: 1.5 } },
    { currentSignedReport: { inspectionId: ids[0], revision: Number.MAX_SAFE_INTEGER + 1 } },
  ]) {
    expect(parseListingWorkspace({ ...workspace, ...changed })).toBeNull();
  }
  expect(
    parseListingWorkspace({
      ...workspace,
      vehicle: {
        ...row,
        revisionId: null,
        version: 0,
        vehiclePrice: null,
        publicationId: null,
        publishedRevisionId: null,
        isPublic: false,
      },
      draft: null,
      approvedRevision: null,
      publication: null,
      currentSignedRevisionId: null,
      currentSignedReport: null,
      vin: "MHKA1BA1JGK123456",
      hold: null,
    })?.draft,
  ).toBeNull();
  expect(listingParams({ listing: ids[0], section: "publication" })).toMatchObject({
    listingId: ids[0],
    section: "publication",
  });
  expect(listingParams({ listing: ids[0], section: "card" })).toMatchObject({
    listingId: ids[0],
    section: "card",
  });
});

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
