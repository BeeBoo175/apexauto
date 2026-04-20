import { setAppointmentDates, expectWorklistPage } from "./intake-ui-controls";
import { expect, test, type Download } from "@playwright/test";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const day = (offset: number) =>
  new Date(Date.now() + (offset * 24 + 8) * 3600000).toISOString().slice(0, 10);

async function csv(download: Download) {
  return readFileSync((await download.path())!, "utf8").replace(/^\uFEFF/, "");
}

test("server table searches all scoped rows, selects appointments and exports applied matches", async ({
  page,
  context,
}) => {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("aina.sofia@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
  await page.getByRole("link", { name: "Table", exact: true }).click();
  await expect(page.getByRole("link", { name: "Account", exact: true })).toHaveCount(0);
  await setAppointmentDates(page, day(0), day(29));
  await expect(page.getByLabel("From", { exact: true })).toHaveValue(day(0));

  await page.getByRole("button", { name: "Apply", exact: true }).click();
  await expect(page.getByLabel("From", { exact: true })).toBeHidden();
  await expect(page.getByRole("row")).toHaveCount(26);
  await page.getByRole("button", { name: "Next records", exact: true }).click();
  await expectWorklistPage(page, 2);
  const plate = (await page.locator("tbody tr td:nth-child(2)").first().textContent())!.trim();
  await page.getByRole("button", { name: "Previous records", exact: true }).click();
  await expectWorklistPage(page, 1);
  await expect(page.locator("tbody tr").filter({ hasText: plate })).toHaveCount(0);
  await page.getByLabel("Search appointments", { exact: true }).fill(plate.toLowerCase());
  await page.getByRole("button", { name: "Search", exact: true }).click();
  await expect(page.getByRole("row")).toHaveCount(2);
  await expect(page.locator("tbody tr").filter({ hasText: plate })).toBeVisible();
  await expect(page.getByRole("button", { name: "Export full CSV", exact: true })).toBeEnabled();
  await expect(page.getByRole("button", { name: "Export selected CSV", exact: true })).toHaveCount(
    0,
  );
  await expect(page.getByText(/0 selected|loaded$/)).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Clear selection", exact: true })).toHaveCount(0);
  await page.getByRole("checkbox", { name: "Select all loaded rows", exact: true }).check();
  await expect(page.getByRole("button", { name: "Export full CSV", exact: true })).toHaveCount(0);
  await expect(page.getByRole("status").filter({ hasText: /^1 selected$/ })).toBeVisible();
  const selectedEvent = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export selected CSV", exact: true }).click();
  const selected = await csv(await selectedEvent);

  await page.getByRole("button", { name: "Clear selection", exact: true }).click();
  await expect(page.getByRole("button", { name: "Export selected CSV", exact: true })).toHaveCount(
    0,
  );
  await expect(page.getByText(/^\d+ selected$/)).toHaveCount(0);
  // Full export follows the applied query, even while the search draft differs.
  await page.getByLabel("Search appointments", { exact: true }).fill("unapplied draft");
  const fullEvent = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export full CSV", exact: true }).click();
  const full = await csv(await fullEvent);
  expect(full).toBe(selected);
  expect(full.split("\r\n")).toHaveLength(3);
  expect(full).toContain('"Inspection start (UTC)"');
  await page.getByRole("button", { name: "Clear search", exact: true }).click();
  await expect(page.getByRole("row")).toHaveCount(26);
  await expect(page.getByRole("button", { name: "Export selected CSV", exact: true })).toHaveCount(
    0,
  );
  await expect(page.getByRole("button", { name: "Export full CSV", exact: true })).toBeEnabled();
  await page.getByLabel("Rows per page", { exact: true }).selectOption("50");
  await expect(page.getByLabel("Rows per page", { exact: true })).toHaveValue("50");
  await expect(page.getByRole("row")).toHaveCount(44);
  await expect(page.getByRole("button", { name: "Next records", exact: true })).toBeDisabled();
  await page.getByLabel("Rows per page", { exact: true }).selectOption("25");
  await expect(page.getByRole("row")).toHaveCount(26);
  await page.getByLabel("Rows per page", { exact: true }).selectOption("10");
  await expect(page.getByRole("row")).toHaveCount(11);
  const inspectionHeader = page.locator('th[data-column-id="startsAt"]');
  const inspectionSort = page.getByRole("button", { name: "Sort by Inspection time", exact: true });
  await expect(inspectionHeader).toHaveAttribute("aria-sort", "none");
  await inspectionSort.click();
  await expect(inspectionHeader).toHaveAttribute("aria-sort", "ascending");
  await page.getByRole("button", { name: "Refresh worklist", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh worklist", exact: true })).toBeEnabled();
  await expect(inspectionHeader).toHaveAttribute("aria-sort", "ascending");
  const tableRoute = "**/api/staff/intake/appointments?**";
  await page.getByRole("checkbox", { name: "Select all loaded rows", exact: true }).check();
  await page.route(tableRoute, (route) => route.fulfill({ status: 502, json: {} }));
  await inspectionSort.click();
  await expect(page.getByText(/The worklist could not be refreshed/)).toBeVisible();
  await expect(inspectionHeader).toHaveAttribute("aria-sort", "ascending");
  await expect(page.getByRole("status").filter({ hasText: /^10 selected$/ })).toBeVisible();
  await page.unroute(tableRoute);
  await inspectionSort.click();
  await expect(inspectionHeader).toHaveAttribute("aria-sort", "descending");
  await expect(page.getByText(/^\d+ selected$/)).toHaveCount(0);
  await inspectionSort.click();
  await expect(inspectionHeader).toHaveAttribute("aria-sort", "none");
  await page.getByRole("button", { name: "Sort by Registration", exact: true }).click();
  await expect(page.getByRole("columnheader").filter({ hasText: "Registration" })).toHaveAttribute(
    "aria-sort",
    "ascending",
  );
  await page.getByRole("button", { name: "Sort by Registration", exact: true }).press("Enter");
  await expect(page.getByRole("columnheader").filter({ hasText: "Registration" })).toHaveAttribute(
    "aria-sort",
    "descending",
  );
  const plates = await page.locator("tbody tr td:nth-child(2)").allTextContents();
  expect(plates).toEqual([...plates].sort().reverse());
  await page.getByRole("checkbox", { name: "Select all loaded rows", exact: true }).check();
  await expect(page.getByRole("status").filter({ hasText: /^10 selected$/ })).toBeVisible();
  await page.getByRole("button", { name: "Refresh worklist", exact: true }).click();
  await expect(
    page.getByRole("checkbox", { name: "Select all loaded rows", exact: true }),
  ).not.toBeChecked();
  await expect(page.getByText(/^\d+ selected$/)).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Export selected CSV", exact: true })).toHaveCount(
    0,
  );
  const allEvent = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export full CSV", exact: true }).click();
  const all = await csv(await allEvent);
  expect(all.split("\r\n").length).toBeGreaterThan(22);
  expect(all.split("\r\n")[1]).toContain(plates[0]);
  const downloads: string[] = [];
  page.on("download", (download) => downloads.push(download.suggestedFilename()));
  await page.route("**/api/staff/intake/appointments/export?**", (route) => {
    if (route.request().method() === "GET") {
      return route.fulfill({ status: 422, json: {} });
    }
    return route.continue();
  });
  const backgroundEvent = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export full CSV", exact: true }).click();
  await expect(
    page.getByText("This large export is being prepared. The download will start automatically.", {
      exact: true,
    }),
  ).toBeVisible();
  expect(await csv(await backgroundEvent)).toBe(all);
  await page.unroute("**/api/staff/intake/appointments/export?**");
  downloads.length = 0;
  await page.route("**/api/staff/intake/appointments/export?**", (route) =>
    route.fulfill({ status: route.request().method() === "GET" ? 422 : 429, json: {} }),
  );
  await page.getByRole("button", { name: "Export full CSV", exact: true }).click();
  await expect(
    page.getByText(
      "Another export is running or the request limit was reached. Try again shortly.",
      {
        exact: true,
      },
    ),
  ).toBeVisible();
  expect(downloads).toEqual([]);
  await expect(page.getByRole("row")).toHaveCount(11);
  await page.unroute("**/api/staff/intake/appointments/export?**");

  writeFileSync(
    path.join(evidence, "table-export.json"),
    JSON.stringify({ selected, fullRows: all.split("\r\n").length - 2, plates }),
  );
  const cookie = (await context.cookies()).find((entry) => entry.name === "apexauto_token")!;
  const denied = await context.request.get("/api/staff/intake/appointments/export", {
    headers: { Cookie: `apexauto_token=${cookie.value}`, "X-Expected-Actor": "different" },
  });
  expect(denied.status()).toBe(409);
  expect(denied.headers()["cache-control"]).toContain("no-store");
  const headers = { Cookie: `apexauto_token=${cookie.value}` };
  const current = await (
    await context.request.get("/api/staff/intake/appointments", { headers })
  ).json();
  const actor = current.actor;
  const expected = `${actor.userId}:${actor.role}:${actor.branchId ?? ""}`;
  for (const [fault, status] of [
    ["invalid-export", 502],
    ["oversized-export", 413],
  ] as const) {
    try {
      writeFileSync(path.join(evidence, "fault.txt"), fault);
      const failure = await context.request.get("/api/staff/intake/appointments/export", {
        headers: { ...headers, "X-Expected-Actor": expected },
      });
      expect(failure.status()).toBe(status);
      expect(failure.headers()["content-disposition"]).toBeUndefined();
    } finally {
      writeFileSync(path.join(evidence, "fault.txt"), "");
    }
  }
  try {
    writeFileSync(path.join(evidence, "fault.txt"), "invalid-export");
    await page.getByRole("button", { name: "Export full CSV", exact: true }).click();
    await expect(
      page.getByText("CSV export could not be completed. Existing rows remain shown.", {
        exact: true,
      }),
    ).toBeVisible();
    expect(downloads).toEqual([]);
    await expect(page.getByRole("row")).toHaveCount(11);
    writeFileSync(path.join(evidence, "fault.txt"), "stalled-export");
    await page.getByRole("button", { name: "Export full CSV", exact: true }).click();
    await expect.poll(() => existsSync(path.join(evidence, "export-started.json"))).toBe(true);
    await page.goto("/dashboard/intake?view=list");
    await expect.poll(() => existsSync(path.join(evidence, "export-cancelled.json"))).toBe(true);
    expect(downloads).toEqual([]);
  } finally {
    writeFileSync(path.join(evidence, "fault.txt"), "");
  }
  try {
    writeFileSync(path.join(evidence, "assignment.txt"), "group-after-export");
    const changed = await context.request.get("/api/staff/intake/appointments/export", {
      headers: {
        ...headers,
        "X-Expected-Actor": expected,
      },
    });
    expect(changed.status()).toBe(409);
    expect(changed.headers()["content-disposition"]).toBeUndefined();
  } finally {
    writeFileSync(path.join(evidence, "assignment.txt"), "restore");
    await page.reload();
  }
});
