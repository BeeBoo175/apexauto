import { expect, test } from "@playwright/test";
import { writeFileSync } from "node:fs";
import path from "node:path";

test.use({ actionTimeout: 15_000 });

test("canonical drive queues retain all full and partial batches", async ({ page }, info) => {
  await page.goto("/login");
  await page
    .getByLabel("Email", { exact: true })
    .fill(
      info.project.name === "mobile" ? "ong.jia.hao@example.invalid" : "aina.sofia@example.invalid",
    );
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
  await page.goto("/dashboard/test-drives");
  const queue = page.getByRole("complementary", { name: "Test drive queues", exact: true });
  for (const name of [/^My work\b/, /^To claim\b/, /^History\b/]) {
    await queue.getByRole("button", { name }).click();
    const rows = queue.locator("ul li button");
    await expect(rows).toHaveCount(25);
    const identities = await rows.allTextContents();
    await expect(queue.getByRole("status")).toHaveText("25 of 51 loaded");
    await queue.getByRole("button", { name: "Load more", exact: true }).click();
    await expect(rows).toHaveCount(50);
    expect((await rows.allTextContents()).slice(0, 25)).toEqual(identities);
    await queue.getByRole("button", { name: "Load more", exact: true }).click();
    await expect(rows).toHaveCount(51);
    await expect(queue.getByRole("status")).toHaveText("51 of 51 loaded");
    await expect(queue.getByRole("button", { name: "Load more", exact: true })).toHaveCount(0);
  }
});

test("canonical unscheduled pages and confirmed claim preserve exact lost-receipt recovery", async ({
  page,
}, info) => {
  const evidence = process.env.BROWSER_EVIDENCE_DIR!;
  await page.goto("/login");
  await page
    .getByLabel("Email", { exact: true })
    .fill(
      info.project.name === "mobile"
        ? "amirul.izzat@example.invalid"
        : "farid.hakim@example.invalid",
    );
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/inspection$/);
  await page.goto("/dashboard/work-calendar");
  const work = page.getByRole("region", { name: "Unscheduled work", exact: true });
  const first: string[] = [];
  for (const [index, count] of [25, 25, 1].entries()) {
    await expect(work.getByLabel("Page number", { exact: true })).toHaveValue(String(index + 1));
    const links = work.locator("ul li a");
    await expect(links).toHaveCount(count);
    const hrefs = await links.evaluateAll((items) =>
      items.map((item) => item.getAttribute("href")!),
    );
    expect(hrefs.every((href) => href.includes("inspection="))).toBe(true);
    first.push(...hrefs);
    if (index < 2) {
      await work.getByRole("button", { name: "Next records", exact: true }).click();
    }
  }
  expect(new Set(first).size).toBe(51);
  await expect(work.getByRole("button", { name: "Next records", exact: true })).toBeDisabled();
  await page.screenshot({
    path: path.join(evidence, "unscheduled-final-page.png"),
    fullPage: true,
  });
  await page.goto("/dashboard/inspection");
  await page.getByRole("button", { name: /^To claim\b/ }).click();
  await page
    .getByRole("button", { name: /^Claim inspection ·/ })
    .first()
    .click();
  const bodies: string[] = [];
  await page.route("**/api/staff/inspection/vehicles/*/assignments", async (route) => {
    bodies.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    if (bodies.length === 1) {
      await route.fulfill({
        status: 502,
        json: {
          status: 502,
          data: null,
          message: "Controlled lost assignment receipt",
          uncertain: true,
        },
      });
      return;
    }
    await route.fulfill({ response });
  });
  await page.getByRole("button", { name: "Confirm assignment", exact: true }).click();
  const confirmation = page.getByRole("dialog", { name: "Claim this inspection?", exact: true });
  await expect(confirmation).toBeVisible();
  expect(bodies).toHaveLength(0);
  await confirmation.getByRole("button", { name: "Confirm assignment", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Reconcile assignment", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("dialog").getByRole("button", { name: "Close dialog", exact: true }),
  ).toBeDisabled();
  await page.getByRole("button", { name: "Reconcile assignment", exact: true }).click();
  expect(bodies).toHaveLength(2);
  expect(bodies[1]).toBe(bodies[0]);
  await expect(page).toHaveURL(/\/dashboard\/inspection\?.*inspection=[a-f0-9-]+/);
  await expect(page.getByRole("button", { name: "Save draft", exact: true })).toBeEnabled();
  await page.unroute("**/api/staff/inspection/vehicles/*/assignments");
  await page.getByRole("button", { name: "Refresh workshop", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh workshop", exact: true })).toBeEnabled();
  await expect(page.getByRole("button", { name: "Save draft", exact: true })).toBeEnabled();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({
    path: path.join(evidence, "confirmed-workshop-claim.png"),
    fullPage: true,
  });
  writeFileSync(
    path.join(evidence, "assignment-volume.json"),
    JSON.stringify({
      unscheduledIdentities: first,
      inspectionId: new URL(page.url()).searchParams.get("inspection"),
      exactReplay: bodies[0] === bodies[1],
    }),
  );
});
