import { expect, test } from "@playwright/test";
import { PHOTO_CACHE_CONTROL } from "@/lib/photo-cache";
import { parsePictureReceipt, parseRemovedPicture } from "@/lib/profile/contracts";

const id = "6b0f5f0e-6a6c-4c7e-9b53-1f6f2f1f4e11";

test("a stored photograph is kept only by the viewer's own browser, for at most a day", () => {
  expect(PHOTO_CACHE_CONTROL).toBe("private, max-age=86400");
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
