import { expect, test } from "@playwright/test";
import { parseTestDriveConflict } from "@/lib/test-drives/errors";
import { parseReservationConflict } from "@/lib/vehicles/reservation-errors";

test("a customer is told in the second person that they already hold a booking", () => {
  expect(parseReservationConflict("customer-has-reservation")?.message).toMatch(/^You already/);
  expect(parseTestDriveConflict("customer-has-test-drive")?.message).toMatch(/^You already/);
});

test("staff booking for a customer read the same conflicts about the customer", () => {
  expect(parseReservationConflict("customer-has-reservation", true)?.message).toMatch(
    /^This customer already/,
  );
  expect(parseTestDriveConflict("customer-has-test-drive", true)?.message).toMatch(
    /^This customer already/,
  );
});

test("conflicts that need no rewording are identical for staff and customers", () => {
  expect(parseReservationConflict("vehicle-reserved", true)).toEqual(
    parseReservationConflict("vehicle-reserved"),
  );
  expect(parseTestDriveConflict("test-drive-slot-unavailable", true)).toEqual(
    parseTestDriveConflict("test-drive-slot-unavailable"),
  );
  expect(parseTestDriveConflict("unknown-code", true)).toBeUndefined();
});
