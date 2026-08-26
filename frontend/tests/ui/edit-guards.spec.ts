import { expect, test } from "@playwright/test";
import { fixtureScript } from "./edit-guard-fixture";

test.beforeEach(async ({ page }) => {
  await page.setContent('<div id="root"></div>');
  await page.addScriptTag({ content: fixtureScript() });
  await expect(page.getByRole("dialog", { name: "Outer editor" })).toBeVisible();
});

test("dirty editors confirm Close, Escape and native dismissal without losing drafts on Stay", async ({
  page,
}) => {
  const dialog = page.getByRole("dialog", { name: "Outer editor" });
  await page.getByLabel("First draft").fill("retained first draft");
  await page.getByLabel("First draft").fill("retained first draft again");
  await page.getByLabel("Second draft").fill("retained second draft");
  await expect(dialog.getByRole("button", { name: "Close dialog", exact: true })).toBeEnabled();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeVisible();
  const confirmation = page.getByRole("dialog", { name: "Discard unsaved changes?" });
  await expect(confirmation).toBeVisible();
  await confirmation.getByRole("button", { name: "Keep editing" }).click();
  await expect(page.getByLabel("First draft")).toHaveValue("retained first draft again");
  expect(await page.evaluate(() => Reflect.get(window, "localEscape"))).toBe(true);
  expect(await page.evaluate(() => Reflect.get(window, "navigationLocked")())).toBe(true);
  const cancelled = await dialog.evaluate((element) =>
    element.dispatchEvent(new Event("cancel", { cancelable: true })),
  );
  expect(cancelled).toBe(false);
  await expect(dialog).toBeVisible();
  await expect(confirmation).toBeVisible();
  await confirmation.getByRole("button", { name: "Keep editing" }).click();
  await page.getByLabel("First draft").fill("");
  expect(await page.evaluate(() => Reflect.get(window, "navigationLocked")())).toBe(true);
  await dialog.evaluate((element) => (element as HTMLDialogElement).close());
  await expect(dialog).toBeVisible();
  await expect(page.getByLabel("Second draft")).toHaveValue("retained second draft");
  await expect(confirmation).toBeVisible();
  await confirmation.getByRole("button", { name: "Discard and close" }).click();
  expect(await page.evaluate(() => Reflect.get(window, "navigationLocked")())).toBe(false);
  await expect(dialog).toHaveCount(0);
});

test("a clean nested dialog closes alone and its cleanup cannot release the outer edit", async ({
  page,
}) => {
  await page.getByLabel("First draft").fill("outer draft");
  await page.getByRole("button", { name: "Open reference" }).click();
  const inner = page.getByRole("dialog", { name: "Inner editor" });
  await page.getByLabel("Inner draft").fill("inner draft");
  await page.keyboard.press("Escape");
  await expect(inner).toBeVisible();
  await page
    .getByRole("dialog", { name: "Discard unsaved changes?" })
    .getByRole("button", { name: "Keep editing" })
    .click();
  await page.getByLabel("Inner draft").fill("");
  await page.keyboard.press("Escape");
  await expect(inner).toHaveCount(0);
  const outer = page.getByRole("dialog", { name: "Outer editor" });
  await expect(outer).toBeVisible();
  await expect(outer.getByRole("button", { name: "Close dialog", exact: true })).toBeEnabled();
  expect(await page.evaluate(() => Reflect.get(window, "navigationLocked")())).toBe(true);
});

test("a changing explicit operation lock blocks native dismissal and releases cleanly", async ({
  page,
}) => {
  const dialog = page.getByRole("dialog", { name: "Outer editor" });
  await page.evaluate(() => Reflect.get(window, "explicitLock")(true));
  await expect(dialog.getByRole("button", { name: "Close dialog", exact: true })).toBeDisabled();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeVisible();
  await page.evaluate(() => Reflect.get(window, "explicitLock")(false));
  await expect(dialog.getByRole("button", { name: "Close dialog", exact: true })).toBeEnabled();
  await dialog.getByRole("button", { name: "Close dialog", exact: true }).click();
  await expect(dialog).toHaveCount(0);
});

test("visible blocking guidance updates when the same operation becomes unconfirmed", async ({
  page,
}) => {
  await page.evaluate(() => Reflect.get(window, "operationState")("pending"));
  await expect(page.getByLabel("Blocking reason")).toContainText("wait for the action");
  await page.evaluate(() => Reflect.get(window, "operationState")("uncertain"));
  await expect(page.getByLabel("Blocking reason")).toContainText(
    "reconcile the unconfirmed action",
  );
  await page.evaluate(() => Reflect.get(window, "operationState")(null));
  await expect(page.getByLabel("Blocking reason")).toHaveText("");
});

test("uncertain settings retain their draft and exact retry until a successful explicit read", async ({
  page,
}) => {
  const bodies: string[] = [];
  let readable = false;
  const settings = {
    processingFee: 500,
    sstRate: 0.08,
    maxAgentDiscountAmount: 500,
    reservationHoldHours: 48,
    updatedAtUtc: "2026-11-01T02:00:00Z",
    version: 1,
    bounds: {
      maximumFee: 100000,
      maximumSstRate: 1,
      maximumDiscount: 100000,
      minimumHoldHours: 1,
      maximumHoldHours: 168,
    },
  };
  await page.route("https://edit-guard.test/**", async (route) => {
    const request = route.request();
    if (!request.url().endsWith("/api/staff/settings")) {
      await route.fulfill({ contentType: "text/html", body: '<div id="root"></div>' });
      return;
    }
    if (request.method() === "PUT") {
      bodies.push(request.postData() ?? "");
    }
    const status = request.method() === "GET" && readable ? 200 : 503;
    await route.fulfill({
      status,
      contentType: "application/json",
      body: JSON.stringify({
        status,
        uncertain: status !== 200,
        message: "The save is unconfirmed.",
        data: status === 200 ? settings : null,
        actor: {
          userId: "4f1d2c3b-5a6e-4b7c-8d9e-0f1a2b3c4d5e",
          role: "Administrator",
          branchId: null,
        },
      }),
    });
  });
  await page.goto("https://edit-guard.test/");
  await page.addScriptTag({ content: fixtureScript("settings") });
  const fee = page.getByLabel("Processing fee (RM)");
  await fee.fill("501.00 ");
  await page.getByRole("button", { name: "Save settings", exact: true }).click();
  await expect(page.getByRole("button", { name: "Retry same save" })).toBeEnabled();
  await expect(fee).toBeDisabled();
  await expect(page.getByRole("button", { name: "Discard changes" })).toBeDisabled();
  await page.getByRole("button", { name: "Retry same save" }).click();
  await expect.poll(() => bodies.length).toBe(2);
  expect(bodies[1]).toBe(bodies[0]);
  await page.getByRole("button", { name: "Load latest settings" }).click();
  await expect(page.getByRole("button", { name: "Retry same save" })).toBeEnabled();
  await expect(fee).toBeDisabled();
  expect(await page.evaluate(() => Reflect.get(window, "navigationLocked")())).toBe(true);
  readable = true;
  await page.getByRole("button", { name: "Load latest settings" }).click();
  await expect(fee).toBeEnabled();
  await expect(fee).toHaveValue("500.00");
  expect(await page.evaluate(() => Reflect.get(window, "navigationLocked")())).toBe(false);
});

for (const outcome of ["existing", "missing"] as const) {
  test(`an uncertain promo create can recover a ${outcome} captured identity after conflict`, async ({
    page,
  }) => {
    const bodies: string[] = [];
    const reads: string[] = [];
    let createdId = "";
    await page.route("https://edit-guard.test/**", async (route) => {
      const request = route.request();
      if (!request.url().includes("/api/staff/settings/promos")) {
        await route.fulfill({ contentType: "text/html", body: '<div id="root"></div>' });
        return;
      }
      let status = 404;
      if (request.method() === "POST") {
        bodies.push(request.postData() ?? "");
        createdId = JSON.parse(bodies[0]).id;
        status = bodies.length === 1 ? 503 : 409;
      } else {
        reads.push(request.url());
        if (bodies.length > 1 && outcome === "existing") {
          status = 200;
        }
      }
      await route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify({
          status,
          uncertain: status === 503,
          message: "Create needs checking.",
          data:
            status === 200
              ? {
                  id: createdId,
                  code: "APEX500",
                  discount: 500,
                  expiresAtUtc: "2030-12-01T04:00:00Z",
                  isActive: true,
                  version: 2,
                }
              : null,
          actor: {
            userId: "4f1d2c3b-5a6e-4b7c-8d9e-0f1a2b3c4d5e",
            role: "Administrator",
            branchId: null,
          },
        }),
      });
    });
    await page.goto("https://edit-guard.test/");
    await page.addScriptTag({ content: fixtureScript("promo") });
    await page.getByLabel("Code", { exact: true }).fill("APEX500");
    await page.getByLabel("Discount (RM)", { exact: true }).fill("500.00");
    await page.getByLabel("Expires (Malaysia time)").fill("2030-12-01T12:00");
    await page.getByRole("button", { name: "Create promo", exact: true }).click();
    await page.getByRole("button", { name: "Check saved promo" }).click();
    await expect(page.getByLabel("Code", { exact: true })).toBeDisabled();
    await page.getByRole("button", { name: "Retry same save" }).click();
    await expect.poll(() => bodies.length).toBe(2);
    expect(bodies[1]).toBe(bodies[0]);
    await page.getByRole("button", { name: "Check saved promo" }).click();
    expect(reads.every((url) => url.endsWith(`/${createdId}`))).toBe(true);
    if (outcome === "existing") {
      await expect(page.getByText("Promo checked", { exact: true })).toBeVisible();
      return;
    }
    await expect(page.getByLabel("Code", { exact: true })).toBeEnabled();
    await expect(page.getByRole("button", { name: "Discard changes", exact: true })).toBeEnabled();
  });
}
