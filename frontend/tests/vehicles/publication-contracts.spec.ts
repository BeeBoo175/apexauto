import { expect, test } from "@playwright/test";
import {
  matchesListingPublication,
  parseListingPublication,
  parseListingPublicationReceipt,
} from "@/lib/vehicles/listing-publication";

const ids = [
  "47cf2705-95cc-48b6-b907-64bf18dd87da",
  "2f35e1b2-602b-43dc-8c5f-4085893b0ea8",
  "e26136c6-6e1b-46c2-9bb5-c28e2f01dd8c",
];
const receipt = {
  id: ids[0],
  vehicleId: ids[1],
  branchId: ids[2],
  revisionId: ids[1],
  signedRevisionId: ids[2],
  actorId: ids[0],
  recordedAt: "2026-10-27T00:00:00+00:00",
};

test("publication receipts validate immutable identities, UTC time and canonical success", () => {
  expect(parseListingPublication({ status: 0, publication: receipt })).toEqual(receipt);
  expect(parseListingPublication({ status: "Succeeded", publication: receipt })).toEqual(receipt);
  expect(parseListingPublicationReceipt({ ...receipt, privateValue: "omitted" })).toEqual(receipt);
  for (const key of ["id", "vehicleId", "branchId", "revisionId", "signedRevisionId", "actorId"]) {
    expect(parseListingPublicationReceipt({ ...receipt, [key]: "invalid" })).toBeNull();
    expect(parseListingPublicationReceipt({ ...receipt, [key]: undefined })).toBeNull();
  }
  for (const recordedAt of [null, "invalid", "2026-10-27T00:00:00", "2026-10-27T08:00:00+08:00"]) {
    expect(parseListingPublicationReceipt({ ...receipt, recordedAt })).toBeNull();
  }
  for (const status of [1, "Conflict", undefined]) {
    expect(parseListingPublication({ status, publication: receipt })).toBeNull();
  }
  expect(parseListingPublication({ status: 0, publication: null })).toBeNull();
});

test("approval confirmation binds the exact operation, saved revision and signed inspection", () => {
  const body = {
    operationId: receipt.id,
    revisionId: receipt.revisionId,
    signedRevisionId: receipt.signedRevisionId,
  };
  expect(matchesListingPublication(receipt, body)).toBe(true);
  expect(matchesListingPublication(receipt, null)).toBe(false);
  for (const key of ["operationId", "revisionId", "signedRevisionId"]) {
    expect(
      matchesListingPublication(receipt, {
        ...body,
        [key]: ids[0] === body[key as keyof typeof body] ? ids[1] : ids[0],
      }),
    ).toBe(false);
  }
});
