import { expect, test } from "@playwright/test";
import { deflateSync } from "node:zlib";
import { signIn } from "./staff-sign-in";

test.use({ actionTimeout: 15000 });

function crc32(buffer: Buffer) {
  let crc = 0xffffffff;
  for (const byte of buffer) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit += 1) {
      crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
    }
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function chunk(type: string, data: Buffer) {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type), data]);
  const checksum = Buffer.alloc(4);
  checksum.writeUInt32BE(crc32(body));
  return Buffer.concat([length, body, checksum]);
}

// A solid-colour square the picture pipeline accepts as a real image.
function solidPng(size: number) {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(size, 0);
  header.writeUInt32BE(size, 4);
  header[8] = 8;
  header[9] = 2;
  const row = Buffer.concat([Buffer.from([0]), Buffer.alloc(size * 3, 0x7a)]);
  const pixels = deflateSync(Buffer.concat(Array.from({ length: size }, () => row)));
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk("IHDR", header),
    chunk("IDAT", pixels),
    chunk("IEND", Buffer.alloc(0)),
  ]);
}

test("staff manage their own picture on the shared account page and open a colleague in place", async ({
  page,
}) => {
  test.setTimeout(90000);
  await signIn(page, "siti.aminah@example.invalid");
  await expect(page).toHaveURL(/\/dashboard\/work-calendar$/);

  await page.goto("/dashboard/intake?view=list");
  const handler = page
    .locator("tbody tr")
    .filter({ hasText: "@example.invalid" })
    .first()
    .getByRole("button")
    .first();
  await handler.hover();
  await expect(page.getByRole("tooltip")).toBeVisible();
  await expect(page.getByRole("tooltip").locator("[data-pending='true']")).toHaveCount(0);
  await handler.click();
  const profile = page.getByRole("dialog");
  await expect(profile.getByText("Work mobile", { exact: true })).toBeVisible();
  await expect(page).toHaveURL(/\/dashboard\/intake/);
  await expect(profile.locator("[data-pending='true']")).toHaveCount(0);
  await expect(profile.getByText("Branch manager", { exact: true })).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(profile).toBeHidden();
  // The click belonged to the person, not to the row beneath it.
  await expect(page).not.toHaveURL(/appointment=/);
  await expect(page.getByRole("region", { name: "Selected appointment" })).toHaveCount(0);

  await page.goto("/account");
  await expect(page.getByRole("heading", { name: "Your account", exact: true })).toBeVisible();
  await expect(page.getByText("Branch manager", { exact: true })).toBeVisible();
  await expect(page.getByText("Work mobile", { exact: true })).toBeVisible();
  await expect(page.getByRole("link", { name: "Reservations" })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Account", exact: true })).toBeVisible();

  await page.getByLabel("Choose a profile picture").setInputFiles({
    name: "profile.png",
    mimeType: "image/png",
    buffer: solidPng(96),
  });
  await expect(page.getByText("Picture updated.")).toBeVisible();
  await expect(page.locator("img[src^='/api/accounts/']")).toBeVisible();
  await expect
    .poll(() =>
      page
        .locator("img[src^='/api/accounts/']")
        .evaluate((image) => (image as HTMLImageElement).naturalWidth),
    )
    .toBeGreaterThan(0);

  await page.reload();
  await expect(page.locator("img[src^='/api/accounts/']")).toBeVisible();

  await page.goto("/account");
  await page.getByRole("button", { name: "Remove picture" }).click();
  await expect(page.getByText("Picture removed.")).toBeVisible();
  await expect(page.locator("img[src^='/api/accounts/']")).toHaveCount(0);

  await page.getByRole("button", { name: "Change username" }).click();
  await page.getByLabel("Username", { exact: true }).fill("no");
  await page.getByRole("button", { name: "Save username" }).click();
  await expect(page.getByRole("alert").filter({ hasText: "Use 3 to 30" })).toBeVisible();
  await page.getByLabel("Username", { exact: true }).fill("siti.aminah.pj");
  await page.getByRole("button", { name: "Save username" }).click();
  await expect(page.getByText("Username updated.")).toBeVisible();
  await expect(page.getByText("siti.aminah.pj", { exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByText("siti.aminah.pj", { exact: true })).toBeVisible();
});

test("customers keep the sectioned account page and cannot read staff profiles", async ({
  page,
}) => {
  await signIn(page, "nur.aisyah@example.invalid");
  await expect(page).toHaveURL(/\/account$/);
  await expect(page.getByRole("link", { name: "Reservations" })).toBeVisible();

  const denied = await page.request.get(
    "/api/accounts/00000000-0000-4000-8000-000000000001/picture",
  );
  expect(denied.status()).toBeGreaterThanOrEqual(400);
});
