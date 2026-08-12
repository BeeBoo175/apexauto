import { expect, test } from "@playwright/test";
import {
  ON_BEHALF_OF_HEADER,
  assistedHeaders,
  parseCustomerLookup,
} from "@/lib/staff/assisted-customer";

const customerId = "4f9c1a52-7d63-4b0e-8a15-2c5d9e3b7f10";

test("an assisted request names its customer in one header and an ordinary one adds nothing", () => {
  expect(assistedHeaders(customerId)).toEqual({ [ON_BEHALF_OF_HEADER]: customerId });
  expect(assistedHeaders()).toEqual({});
});

test("customer lookup keeps only well formed masked rows", () => {
  const row = {
    id: customerId,
    name: "Aisyah Rahman",
    email: "a***@example.com",
    mobile: "******6789",
  };

  expect(parseCustomerLookup({ items: [row] })).toEqual({ items: [row] });
  expect(parseCustomerLookup({ items: [] })).toEqual({ items: [] });
});

test("customer lookup rejects malformed rows and oversized answers", () => {
  const row = {
    id: customerId,
    name: "Aisyah Rahman",
    email: "a***@example.com",
    mobile: "******6789",
  };

  expect(parseCustomerLookup({ items: [{ ...row, id: "not-a-uuid" }] })).toBeNull();
  expect(parseCustomerLookup({ items: [{ ...row, email: 1 }] })).toBeNull();
  expect(parseCustomerLookup({ items: Array.from({ length: 11 }, () => row) })).toBeNull();
  expect(parseCustomerLookup({})).toBeNull();
});
