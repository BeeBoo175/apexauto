import { expect, test } from "@playwright/test";
import {
  PICTURE_CACHE_CONTROL,
  parsePictureReceipt,
  parseRemovedPicture,
} from "@/lib/profile/contracts";

const id = "6b0f5f0e-6a6c-4c7e-9b53-1f6f2f1f4e11";

test("a protected picture is never cached by the browser or a shared cache", () => {
  expect(PICTURE_CACHE_CONTROL).toBe("private, no-store");
  expect(PICTURE_CACHE_CONTROL).not.toContain("max-age");
});

test("an upload receipt names the picture and its state", () => {
  expect(parsePictureReceipt({ pictureId: id, status: "Available" })).toEqual({
    pictureId: id,
    status: "Available",
  });
  expect(parsePictureReceipt({ pictureId: id, status: "Pending" })?.status).toBe("Pending");
  for (const value of [
    null,
    {},
    { pictureId: id },
    { pictureId: "not-a-uuid", status: "Available" },
    { pictureId: id, status: "Unknown" },
  ]) {
    expect(parsePictureReceipt(value)).toBeNull();
  }
});

test("a removal receipt distinguishes a removal from a replacement of the picture that was seen", () => {
  expect(
    parseRemovedPicture({ pictureId: id, status: "DeletionPending", removalReason: "Removed" }),
  ).toEqual({ pictureId: id, status: "DeletionPending", removalReason: "Removed" });
  expect(
    parseRemovedPicture({ pictureId: id, status: "Deleted", removalReason: "Replaced" }),
  ).toEqual({ pictureId: id, status: "Deleted", removalReason: "Replaced" });
  expect(
    parseRemovedPicture({ pictureId: id, status: "Available", removalReason: "Removed" }),
  ).toBeNull();
  expect(parseRemovedPicture({ pictureId: id, status: "Deleted" })).toBeNull();
});
