import { expect, test, type Locator, type Page } from "@playwright/test";
import { calendarDayLabel } from "@/lib/calendar";
import { expectClean, expectNoSidewaysScroll, guardPage } from "./explore-guards";

const origin = process.env.APP_BASE_URL!;

const publicPages = [
  "/",
  "/vehicles",
  "/sell",
  "/branches",
  "/about",
  "/faq",
  "/warranty",
  "/inspection",
  "/buying-guide",
  "/selling-guide",
  "/promotions",
  "/privacy",
  "/terms",
  "/disclaimer",
  "/login",
  "/register",
  "/forgot-password",
];

// Console noise that a deliberately dropped network is expected to produce.
const offlineConsole = [
  /Failed to load resource/,
  /ERR_INTERNET_DISCONNECTED/,
  /Failed to fetch/,
  /net::ERR/,
];

// Raw machine codes must never reach a visitor's screen.
const rawCodes =
  /\b(customer-has-reservation|vehicle-not-available|promo-[a-z-]+|invalid-[a-z-]+)\b/;
const crashMarkers = /Something went wrong|Application error|Internal Server Error/i;

function ringgit(text: string) {
  return Number(text.replace(/[^0-9.]/g, ""));
}

function queryParam(page: Page, name: string) {
  return new URL(page.url()).searchParams.get(name);
}

async function expectNoRawCodes(page: Page) {
  const text = await page.locator("body").innerText();
  expect(text, "raw codes on screen").not.toMatch(rawCodes);
}

function catalogueSummary(page: Page) {
  return page.locator('p[aria-live="polite"]').filter({ hasText: /inspected vehicle/ });
}

async function catalogueTotal(page: Page) {
  const text = await catalogueSummary(page).innerText();
  const match = text.match(/([\d,]+)\s+inspected/);
  expect(match, `summary text: ${text}`).not.toBeNull();
  return Number(match![1].replace(/,/g, ""));
}

async function openCatalogue(page: Page) {
  await page.goto("/vehicles");
  await expect(page.getByRole("heading", { level: 1, name: "Used cars for sale" })).toBeVisible();
  await expect(catalogueSummary(page)).toBeVisible();
  await page.waitForLoadState("networkidle");
}

// The address of the first car on the catalogue, read from its card.
async function firstCarPath(page: Page) {
  await openCatalogue(page);
  const card = page.getByRole("link", { name: /^View details for / }).first();
  await expect(card).toBeVisible();
  return (await card.getAttribute("href"))!;
}

async function openFirstCar(page: Page) {
  const path = await firstCarPath(page);
  await page.goto(path);
  await expect(page.locator("#vehicle-price-box")).toBeVisible();
  // The section strip marks itself once the page has hydrated.
  await expect(page.locator("[data-hydrated]")).toBeAttached();
  return path;
}

async function breakdownRows(box: Locator) {
  return box
    .locator("dl > div")
    .evaluateAll((rows) =>
      rows.map((row) => [
        row.querySelector("dt")?.textContent ?? "",
        row.querySelector("dd")?.textContent ?? "",
      ]),
    );
}

async function readPriceBox(page: Page) {
  const box = page.locator("#vehicle-price-box");
  const vehiclePrice = ringgit(await box.locator("strong").first().innerText());
  const total = ringgit(await box.locator('[aria-live="polite"] strong').innerText());
  const rows = await breakdownRows(box);
  return { vehiclePrice, total, rows };
}

// The promo disclosure toggles on click, so open it only when it is closed.
async function openPromoFormOnce(page: Page) {
  const details = page.locator("#vehicle-price-box details");
  if (!(await details.evaluate((element) => (element as HTMLDetailsElement).open))) {
    await details.locator("summary").click();
  }
  const box = page.locator("#vehicle-price-box");
  return {
    box,
    input: box.getByLabel("Promo code", { exact: true }),
    apply: box.getByRole("button", { name: /^(Apply|Checking…)$/ }),
    status: page.locator("#vehicle-promo-status"),
  };
}

test("PUB-01 every public page answers, has one h1, and fits at desktop and phone width", async ({
  page,
}) => {
  test.setTimeout(180_000);
  const guard = guardPage(page);
  const problems: string[] = [];

  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: width === 1440 ? 1024 : 844 });

    for (const path of publicPages) {
      const response = await page.goto(path);
      if (response?.status() !== 200) {
        problems.push(`${path} @${width}: status ${response?.status()}`);
      }

      const headings = page.getByRole("heading", { level: 1 });
      await headings
        .first()
        .waitFor({ timeout: 10_000 })
        .catch(() => undefined);
      const count = await headings.count();
      if (count !== 1) {
        problems.push(`${path} @${width}: ${count} h1 elements`);
      }

      try {
        if (!(width === 390 && path === "/warranty")) {
          await expectNoSidewaysScroll(page);
        }
      } catch {
        problems.push(`${path} @${width}: sideways scroll`);
      }
    }
  }

  expect(problems).toEqual([]);
  expectClean(guard);
});

test("PUB-01 the warranty page fits at phone width", async ({ page }) => {
  test.fail(true, "NEW: /warranty scrolls sideways at 390px width");
  const guard = guardPage(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/warranty");
  await expect(page.getByRole("heading", { level: 1 })).toHaveCount(1);
  await expectNoSidewaysScroll(page);
  expectClean(guard);
});

test("PUB-02 header menus and footer links all land on working pages", async ({ page }) => {
  test.setTimeout(180_000);
  const guard = guardPage(page);
  const nav = page.getByRole("navigation", { name: "Main navigation" });

  const groups = [
    {
      label: "Buy a car",
      links: [
        ["Browse cars", "/vehicles"],
        ["ApexShield warranty", "/warranty"],
      ],
    },
    {
      label: "How it works",
      links: [
        ["Buying a car", "/buying-guide"],
        ["Selling a car", "/selling-guide"],
        ["210-point inspection", "/inspection"],
      ],
    },
    {
      label: "Help",
      links: [
        ["FAQ", "/faq"],
        ["Disclaimers and notices", "/disclaimer"],
      ],
    },
  ];
  const looseLinks = [
    ["Sell my car", "/sell"],
    ["Showrooms", "/branches"],
    ["About", "/about"],
  ];

  for (const group of groups) {
    for (const [name, path] of group.links) {
      await page.goto("/");
      await expect(page.getByRole("heading", { level: 1 })).toHaveCount(1);
      await nav.getByRole("button", { name: group.label, exact: true }).click();
      await nav.getByRole("link", { name, exact: true }).click();
      await expect(page).toHaveURL(new RegExp(`${path}$`));
      await expect(page.getByRole("heading", { level: 1 }).first()).toBeVisible();
      await expect(nav.getByRole("button", { name: group.label, exact: true })).toHaveAttribute(
        "data-active",
        "true",
      );
      expect((await page.request.get(path)).status(), path).toBe(200);
    }
  }

  for (const [name, path] of looseLinks) {
    await page.goto("/");
    await nav.getByRole("link", { name, exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`${path}$`));
    await expect(nav.getByRole("link", { name, exact: true })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect((await page.request.get(path)).status(), path).toBe(200);
  }

  await page.goto("/");
  const footerPaths = await page
    .locator("footer a[href^='/']")
    .evaluateAll((links) => [...new Set(links.map((link) => link.getAttribute("href")!))]);
  expect(footerPaths.length).toBeGreaterThan(8);

  for (const path of footerPaths) {
    const response = await page.goto(path);
    expect(response?.status(), `footer link ${path}`).toBe(200);
  }

  expectClean(guard, [/ERR_NO_BUFFER_SPACE/]);
});

test("PUB-03 erratic: unknown pages and cars show a designed way back, never a server error", async ({
  page,
}) => {
  const guard = guardPage(page);
  const unknownCar = `/vehicles/${crypto.randomUUID()}`;

  const missing = await page.goto("/does-not-exist");
  expect(missing?.status()).toBe(404);
  await expect(page.getByRole("heading", { name: "We can't find that page" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Back to home", exact: true })).toBeVisible();
  await expectNoRawCodes(page);

  for (const path of ["/vehicles/not-a-uuid", unknownCar]) {
    const response = await page.goto(path);
    expect(response?.status(), path).toBeLessThan(500);
    await expect(
      page.getByRole("heading", { name: "This vehicle is no longer available" }),
    ).toBeVisible();
    await expect(page.getByRole("link", { name: "Browse the catalogue" })).toBeVisible();
    await expect(page.locator("body")).not.toContainText(crashMarkers);
    await expectNoRawCodes(page);
    await expectNoSidewaysScroll(page);
  }

  await page.getByRole("link", { name: "Browse the catalogue" }).click();
  await expect(page).toHaveURL(/\/vehicles$/);
  await expect(page.getByRole("heading", { level: 1, name: "Used cars for sale" })).toBeVisible();

  expectClean(guard, [/status of 404/]);
});

test("PUB-04 catalogue filters, sorting and paging reflect in the URL", async ({ page }) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  await openCatalogue(page);

  const rail = page.getByRole("complementary", { name: "Filter cars" });
  const reset = rail.getByRole("button", { name: "Reset", exact: true });
  const unfilteredTotal = await catalogueTotal(page);

  // Make
  const make = rail.getByLabel("Make", { exact: true });
  const makeName = (await make.locator("option").nth(1).innerText()).trim();
  await make.selectOption({ label: makeName });
  await expect.poll(() => queryParam(page, "make")).toBe(makeName);
  await expect.poll(() => catalogueTotal(page)).toBeLessThan(unfilteredTotal);
  await expect(
    page.getByRole("button", { name: `Remove filter: Make: ${makeName}` }),
  ).toBeVisible();
  await reset.click();
  await expect.poll(() => queryParam(page, "make")).toBeNull();
  await expect.poll(() => catalogueTotal(page)).toBe(unfilteredTotal);

  // Grade
  const firstGrade = rail
    .getByRole("group", { name: "Inspection grade" })
    .getByRole("button")
    .first();
  await firstGrade.click();
  await expect(firstGrade).toHaveAttribute("aria-pressed", "true");
  await expect.poll(() => queryParam(page, "grade")).toMatch(/^[A-Z]/);
  await reset.click();
  await expect.poll(() => queryParam(page, "grade")).toBeNull();

  // Price range
  const maximumPrice = rail.getByLabel("Maximum price (RM)", { exact: true });
  await maximumPrice.fill("100000");
  await maximumPrice.press("Tab");
  await expect.poll(() => queryParam(page, "maximumPrice")).toBe("100000");
  await expect(
    page.getByRole("button", { name: /^Remove filter: Under RM 100,000/ }),
  ).toBeVisible();
  await reset.click();
  await expect.poll(() => queryParam(page, "maximumPrice")).toBeNull();

  // Showroom tabs
  const showroomTab = page
    .locator("header")
    .filter({ hasText: "Used cars for sale" })
    .getByRole("button", { name: /Petaling Jaya/ });
  await showroomTab.click();
  await expect(showroomTab).toHaveAttribute("aria-pressed", "true");
  await expect
    .poll(() => queryParam(page, "branchId"))
    .toBe("68f371ad-223a-43d6-b45b-b878eece79ae");
  await reset.click();
  await expect.poll(() => queryParam(page, "branchId")).toBeNull();

  // Sort
  await page.locator("#catalogue-sort").selectOption("price:asc");
  await expect.poll(() => queryParam(page, "sort")).toBe("price");
  await expect.poll(() => queryParam(page, "direction")).toBe("asc");
  const cards = page.getByRole("link", { name: /^View details for / });
  const priceOf = async (index: number) =>
    ringgit(((await cards.nth(index).innerText()).match(/RM\s[\d,]+\.\d{2}/) ?? ["0"])[0]);
  await expect.poll(async () => (await priceOf(0)) <= (await priceOf(1))).toBe(true);

  // Paging
  await page.locator("#catalogue-sort").selectOption("published:desc");
  // The default order is not written to the address.
  await expect.poll(() => queryParam(page, "sort")).toBeNull();
  const next = page.getByRole("button", { name: "Next page", exact: true });
  await expect(next).toBeEnabled();
  await next.click();
  await expect.poll(() => queryParam(page, "page")).toBe("2");
  await expect(page.getByRole("button", { name: "Page 2", exact: true })).toHaveAttribute(
    "aria-current",
    "page",
  );
  await page.getByRole("button", { name: "Previous page", exact: true }).click();
  await expect.poll(() => queryParam(page, "page")).not.toBe("2");
  await expect(page.getByRole("button", { name: "Page 1", exact: true })).toHaveAttribute(
    "aria-current",
    "page",
  );

  await expectNoSidewaysScroll(page);
  expectClean(guard);
});

test("PUB-04 Back restores the previous filter set", async ({ page }) => {
  test.fail(
    true,
    "NEW: catalogue filter changes replace the history entry, so Back leaves /vehicles instead of restoring the previous filters",
  );
  const guard = guardPage(page);
  await page.goto("/about");
  await openCatalogue(page);

  const rail = page.getByRole("complementary", { name: "Filter cars" });
  const make = rail.getByLabel("Make", { exact: true });
  const makeName = (await make.locator("option").nth(1).innerText()).trim();
  await make.selectOption({ label: makeName });
  await expect.poll(() => queryParam(page, "make")).toBe(makeName);

  await page.locator("#catalogue-sort").selectOption("price:asc");
  await expect.poll(() => queryParam(page, "sort")).toBe("price");

  await page.goBack();
  await expect(page).toHaveURL(/\/vehicles/);
  expect(queryParam(page, "make")).toBe(makeName);
  expect(queryParam(page, "sort")).not.toBe("price");

  expectClean(guard);
});

test("PUB-04 pressing Enter in a price field applies the range", async ({ page }) => {
  test.fail(true, "NEW: Enter in the catalogue price field does not apply the typed range");
  const guard = guardPage(page);
  await openCatalogue(page);
  const rail = page.getByRole("complementary", { name: "Filter cars" });
  const maximumPrice = rail.getByLabel("Maximum price (RM)", { exact: true });
  await maximumPrice.fill("100000");
  await maximumPrice.press("Enter");
  await expect.poll(() => queryParam(page, "maximumPrice")).toBe("100000");
  expectClean(guard);
});

test("PUB-05 erratic: hand-edited catalogue URLs never break the page", async ({ page }) => {
  test.setTimeout(180_000);
  const guard = guardPage(page);
  const hostile = [
    "/vehicles?page=-1",
    "/vehicles?page=9999",
    "/vehicles?pageSize=500",
    "/vehicles?grade=Z",
    "/vehicles?minPrice=abc",
    "/vehicles?minimumPrice=abc",
    "/vehicles?sort=evil",
    "/vehicles?direction=sideways",
    "/vehicles?branchId=not-a-uuid",
    `/vehicles?search=${"x".repeat(5000)}`,
  ];

  for (const path of hostile) {
    const label = path.slice(0, 60);
    const response = await page.goto(path);
    expect(response?.status(), label).toBeLessThan(500);
    await expect(
      page.getByRole("heading", { level: 1, name: "Used cars for sale" }),
      label,
    ).toBeVisible();
    // A sensible result, an empty state, or a plain "could not be loaded" notice, never a crash page.
    await expect(
      page.getByText(/inspected vehicle|could not be loaded|No vehicles|No inspected cars/).first(),
      label,
    ).toBeVisible();
    await expect(page.locator("body"), label).not.toContainText(crashMarkers);
    await expectNoRawCodes(page);
    await expectNoSidewaysScroll(page);
  }

  // The API behind the page answers bad input with a refusal, not a failure.
  for (const query of [
    "page=-1",
    "page=9999",
    "pageSize=500",
    "grade=Z",
    "sort=evil",
    "minimumPrice=abc",
    "direction=sideways",
    `search=${"x".repeat(5000)}`,
  ]) {
    const response = await page.request.get(`/api/vehicles?${query}`);
    expect(response.status(), query.slice(0, 40)).toBeLessThan(500);
  }

  expectClean(guard);
});

test("PUB-06 erratic: markup and SQL in the catalogue search are shown as plain text", async ({
  page,
}) => {
  const guard = guardPage(page);
  const dialogs: string[] = [];
  page.on("dialog", (dialog) => {
    dialogs.push(dialog.message());
    void dialog.dismiss();
  });
  await openCatalogue(page);

  const search = page.getByRole("searchbox", { name: "Search vehicles" });
  for (const text of ["<script>alert(1)</script>", "' OR 1=1 --"]) {
    await search.fill(text);
    await search.press("Enter");
    await expect(
      page.getByRole("button", { name: `Remove filter: Search: ${text}` }),
    ).toBeVisible();
    await expect(page.getByText(/inspected vehicle/).first()).toBeVisible();
    await expect(page.locator("body")).not.toContainText(crashMarkers);
    await expectNoRawCodes(page);
  }

  expect(dialogs).toEqual([]);

  await page.getByRole("button", { name: "Clear all", exact: true }).first().click();
  await expect(page.getByRole("link", { name: /^View details for / }).first()).toBeVisible();

  expectClean(guard);
});

test("PUB-07 vehicle page totals add up, sections update the URL, thumbnails change the photo", async ({
  page,
}) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  await openCatalogue(page);

  const card = page.getByRole("link", { name: /^View details for / }).first();
  const path = (await card.getAttribute("href"))!;
  await card.click();
  await expect(page).toHaveURL(new RegExp(`${path}$`));
  await expect(page.locator("#vehicle-price-box")).toBeVisible();
  await expect(page.locator("[data-hydrated]")).toBeAttached();

  const { vehiclePrice, total, rows } = await readPriceBox(page);
  const fees = rows
    .filter(([label]) => /^(Processing fee|Road tax|SST on processing fee)/.test(label.trim()))
    .reduce((sum, [, amount]) => sum + ringgit(amount), 0);
  expect(rows.length).toBeGreaterThanOrEqual(3);
  expect(vehiclePrice + fees).toBeCloseTo(total, 2);

  // Key facts
  await expect(page.locator("main, body").first()).toContainText(/km/);

  // Section tabs
  const tabs = page.getByRole("navigation", { name: "Vehicle information" });
  const sections = [
    ["Inspection report", "inspection"],
    ["Financing", "financing"],
    ["Showroom", "showroom"],
    ["Overview", "overview"],
  ];
  for (const [label, id] of sections) {
    await tabs.getByRole("link", { name: label, exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`#${id}$`));
    await expect(tabs.getByRole("link", { name: label, exact: true })).toHaveAttribute(
      "aria-current",
      "location",
    );
    await expect(page.locator(`#${id}`)).toHaveAttribute("data-active", "true");
  }

  // Gallery thumbnails
  const thumbnails = page.getByRole("button", { name: /^Show photo \d+$/ });
  const thumbnailCount = await thumbnails.count();
  if (thumbnailCount > 1) {
    await thumbnails.nth(1).click();
    await expect(thumbnails.nth(1)).toHaveAttribute("aria-pressed", "true");
    await expect(thumbnails.nth(0)).toHaveAttribute("aria-pressed", "false");
  }

  await expectNoSidewaysScroll(page);
  expectClean(guard);
});

test("PUB-08 the card's monthly estimate matches the vehicle page", async ({ page }) => {
  const guard = guardPage(page);
  await openCatalogue(page);

  const card = page.getByRole("link", { name: /^View details for / }).first();
  const cardText = await card.innerText();
  const cardMonthly = Number(cardText.match(/est\.\s*RM\s*([\d,]+)\s*\/mo/)![1].replace(/,/g, ""));
  const path = (await card.getAttribute("href"))!;

  await page.goto(path);
  const note = page.locator("#vehicle-price-box").getByText(/About RM/);
  const pageMonthly = ringgit((await note.innerText()).match(/About RM\s*([\d,]+(?:\.\d+)?)/)![1]);

  expect(Math.round(pageMonthly)).toBe(cardMonthly);
  expectClean(guard);
});

test("PUB-09 erratic: promo codes are refused or applied clearly, and removal restores the total", async ({
  page,
}) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  await openFirstCar(page);

  const before = await readPriceBox(page);
  const promo = await openPromoFormOnce(page);

  // Empty
  await expect(promo.apply).toBeDisabled();

  // Wrong case, unknown and over-long codes are refused with something to act on.
  for (const code of ["apex500", "NOPE"]) {
    await promo.input.fill(code);
    await promo.apply.click();
    await expect(page.getByRole("alert").filter({ hasText: /promo/i }).last()).toBeVisible();
    await expect(promo.status).toHaveText("");
    expect((await readPriceBox(page)).total).toBe(before.total);
    await expectNoRawCodes(page);
  }

  await promo.input.fill("x".repeat(300));
  await expect(promo.apply).toBeDisabled();
  expect((await readPriceBox(page)).total).toBe(before.total);

  // The right code, with surrounding space trimmed.
  await promo.input.fill("APEX500 ");
  await promo.apply.click();
  await expect(promo.status).toContainText("APEX500 applied");
  const applied = await readPriceBox(page);
  expect(applied.total).toBeCloseTo(before.total - 500, 2);
  const discount = applied.rows.find(([label]) => label.includes("Promo discount (APEX500)"));
  expect(discount?.[1]).toContain("RM 500.00");

  // Removing it restores the first total.
  await promo.box.getByRole("button", { name: "Remove", exact: true }).click();
  await expect(promo.status).toHaveText("");
  expect((await readPriceBox(page)).total).toBe(before.total);

  // A double press sends one check.
  const checks: string[] = [];
  page.on("request", (request) => {
    if (request.url().includes("/api/promos/validations")) {
      checks.push(request.url());
    }
  });
  await promo.input.fill("APEX500");
  await promo.apply.dblclick();
  await expect(promo.status).toContainText("APEX500 applied");
  expect(checks).toHaveLength(1);

  await expectNoSidewaysScroll(page);
  expectClean(guard, [/status of 400/]);
});

test("PUB-09 the promo discount row carries a minus sign", async ({ page }) => {
  const guard = guardPage(page);
  await openFirstCar(page);

  const promo = await openPromoFormOnce(page);
  await promo.input.fill("APEX500");
  await promo.apply.click();
  await expect(promo.status).toContainText("APEX500 applied");

  const row = promo.box
    .locator("dl > div")
    .filter({ hasText: "Promo discount (APEX500)" })
    .locator("dd");
  await expect(row).toHaveText("−RM 500.00");

  expectClean(guard);
});

test("PUB-10 a visitor who presses Reserve signs in and returns to the same car", async ({
  page,
}) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  const path = await openFirstCar(page);

  await page
    .locator("#vehicle-actions")
    .getByRole("link", { name: /^Reserve for RM\s?500$/ })
    .click();
  await expect(page).toHaveURL(/\/login\?returnTo=/);
  expect(decodeURIComponent(queryParam(page, "returnTo")!)).toBe(path);

  await page.getByLabel("Email", { exact: true }).fill("ahmad.faizal@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();

  await expect(page).toHaveURL(new RegExp(`${path}$`));
  await expect(
    page.locator("#vehicle-actions").getByRole("button", { name: /^Reserve for RM\s?500$/ }),
  ).toBeEnabled();

  expectClean(guard);
});

test("PUB-11 the home page showroom counts match the showrooms page", async ({ page }) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);

  async function counts(section: string, pattern: RegExp) {
    const rows = page.locator(section).locator("li");
    const result: Record<string, number> = {};
    for (const row of await rows.all()) {
      const label = (await row.locator("button[aria-label]").first().getAttribute("aria-label"))!;
      const name = label.replace(/^\d+\s/, "").replace(/ showroom$/, "");
      const match = (await row.textContent())?.match(pattern);
      result[name] = match ? Number(match[1]) : 0;
    }
    return result;
  }

  await page.goto("/");
  const firstTrigger = page.getByRole("button", { name: /^01 .+ showroom$/ });
  await expect(firstTrigger).toBeEnabled();
  const home = await counts(
    'section[aria-labelledby="landing-showrooms-heading"]',
    /(\d+) cars on display/,
  );

  await page.goto("/branches");
  const floor = page.locator('section[aria-labelledby="showrooms-find-heading"] li');
  await expect(floor.first()).toContainText(/cars on the floor/);
  await expect(floor.last()).toContainText(/cars on the floor/);
  const showrooms = await counts(
    'section[aria-labelledby="showrooms-find-heading"]',
    /(\d+) cars on the floor/,
  );

  expect(Object.keys(home).length).toBeGreaterThanOrEqual(3);
  expect(home).toEqual(showrooms);
  expectClean(guard);
});

test("PUB-12 'Plan a visit' for a non-first showroom opens the showrooms page on it", async ({
  page,
}) => {
  const guard = guardPage(page);

  await page.goto("/");
  const second = page.getByRole("button", { name: /^02 .+ showroom$/ });
  await expect(second).toBeEnabled();
  const label = (await second.getAttribute("aria-label"))!;
  const shortLabel = label.replace(/^02 /, "").replace(/ showroom$/, "");
  await second.click();

  const panel = page.getByRole("listitem").filter({ has: second });
  const plan = panel.getByRole("link", { name: "Plan a visit", exact: true });
  const href = (await plan.getAttribute("href"))!;
  expect(href).toMatch(/^\/branches\?branchId=[0-9a-f-]{36}$/);
  await plan.click();

  await expect(page).toHaveURL(/\/branches\?branchId=/);
  await expect(
    page.getByRole("button", { name: `${shortLabel} showroom`, exact: true }),
  ).toHaveAttribute("aria-expanded", "true");
  await expect(page.getByRole("heading", { name: "Book a visit" })).toBeVisible();
  await expect(page.getByText(new RegExp(`^At .*${shortLabel}`))).toBeVisible();

  expectClean(guard);
});

test("PUB-13 'Book appraisal' carries the chosen showroom, day and bay to the seller form", async ({
  page,
}) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  await page.goto("/branches");

  const book = page.getByRole("link", { name: "Book appraisal", exact: true });
  await expect(book).toHaveAttribute("aria-disabled", "true");

  const day = page.getByRole("group", { name: "Day", exact: true }).getByRole("button").nth(2);
  const dayLabel = await day.getAttribute("aria-label");
  const slotsResponse = page.waitForResponse((response) => {
    const url = new URL(response.url());
    const date = url.searchParams.get("date");
    return (
      url.pathname === "/api/intake/slots" && date !== null && calendarDayLabel(date) === dayLabel
    );
  });
  await day.click();
  const response = await slotsResponse;
  expect(response.ok()).toBe(true);
  await expect(day).toHaveAttribute("aria-pressed", "true");
  const slotsUrl = new URL(response.url());
  const branchId = slotsUrl.searchParams.get("branchId")!;
  const { data: offer } = await response.json();

  const bays = page.locator('[role="group"][aria-label="Time"] button');
  await expect(bays.first()).toBeVisible();
  await bays.first().click();
  await expect(bays.first()).toHaveAttribute("aria-pressed", "true");
  await expect(book).not.toHaveAttribute("aria-disabled", "true");

  const target = new URL((await book.getAttribute("href"))!, origin);
  expect(target.pathname).toBe("/sell");
  expect(target.searchParams.get("branchId")).toBe(branchId);
  expect(target.searchParams.get("startsAt")).toBe(offer.slots[0].startsAt);
  await book.click();
  await expect(page).toHaveURL(target.href);

  expectClean(guard);
});

test("PUB-14 erratic: rapid day changes leave the last day chosen with its own bays", async ({
  page,
}) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  const offers = new Map<string, number>();
  const requestedDates: string[] = [];

  page.on("request", (request) => {
    if (request.url().includes("/api/intake/slots")) {
      requestedDates.push(new URL(request.url()).searchParams.get("date")!);
    }
  });
  page.on("response", async (response) => {
    if (!response.url().includes("/api/intake/slots") || response.status() !== 200) {
      return;
    }
    const date = new URL(response.url()).searchParams.get("date")!;
    const body = await response.json().catch(() => null);
    const offer = body?.data ?? body;
    if (offer?.slots) {
      offers.set(date, offer.slots.length);
    }
  });

  await page.goto("/branches");
  const days = page.locator('[role="group"][aria-label="Day"] button');
  const bays = page.locator('[role="group"][aria-label="Time"] button');
  const book = page.getByRole("link", { name: "Book appraisal", exact: true });
  const dayCount = await days.count();
  expect(dayCount).toBe(7);

  // Every chip in a row without waiting for any answer.
  for (let index = 0; index < dayCount; index += 1) {
    await days.nth(index).click();
  }
  await expect(days.nth(dayCount - 1)).toHaveAttribute("aria-pressed", "true");
  for (let index = 0; index < dayCount - 1; index += 1) {
    await expect(days.nth(index)).toHaveAttribute("aria-pressed", "false");
  }

  const lastDate = requestedDates.at(-1)!;
  await expect.poll(() => offers.get(lastDate)).not.toBeUndefined();
  await expect(bays).toHaveCount(offers.get(lastDate)!);

  // A bay, then a different day: nothing stale stays chosen.
  await bays.first().click();
  await expect(bays.first()).toHaveAttribute("aria-pressed", "true");
  await days.nth(1).click();
  await expect(days.nth(1)).toHaveAttribute("aria-pressed", "true");
  await expect(days.nth(dayCount - 1)).toHaveAttribute("aria-pressed", "false");
  await expect(book).toHaveAttribute("aria-disabled", "true");
  await expect(
    page.locator('[role="group"][aria-label="Time"] button[aria-pressed="true"]'),
  ).toHaveCount(0);

  const secondDate = requestedDates.at(-1)!;
  await expect.poll(() => offers.get(secondDate)).not.toBeUndefined();
  await expect(bays).toHaveCount(offers.get(secondDate)!);

  await expectNoSidewaysScroll(page);
  expectClean(guard);
});

test("PUB-15 FAQ search, accordion and topic links", async ({ page }) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  await page.goto("/faq");
  await expect(page.getByRole("heading", { level: 1, name: "FAQ" })).toBeVisible();

  const status = page.locator("[role='status']").filter({ hasText: /^\d+ of \d+$/ });
  const search = page.getByRole("searchbox", { name: "Search questions" });
  const triggers = page
    .locator("button[aria-controls^='faq-panel-'], button[aria-expanded]")
    .filter({
      has: page.locator("span[class*='triggerQuestion']"),
    });

  const totalText = await status.innerText();
  const total = Number(totalText.match(/of (\d+)/)![1]);

  // Search narrows the list to matching questions only.
  await search.fill("deposit");
  await expect
    .poll(async () => Number((await status.innerText()).match(/^(\d+)/)![1]))
    .toBeLessThan(total);
  const shown = Number((await status.innerText()).match(/^(\d+)/)![1]);
  expect(shown).toBeGreaterThan(0);
  await expect(triggers).toHaveCount(shown);

  // An empty result says so and offers a way back.
  await search.fill("zzzqqqxxx");
  await expect(page.getByText("No questions match “zzzqqqxxx”.")).toBeVisible();
  await page.getByRole("button", { name: "Reset topic and search", exact: true }).click();
  await expect(status).toHaveText(`${total} of ${total}`);
  await expect(search).toHaveValue("");

  // Answers open and close.
  const second = triggers.nth(1);
  await expect(second).toHaveAttribute("aria-expanded", "false");
  await second.click();
  await expect(second).toHaveAttribute("aria-expanded", "true");
  await second.click();
  await expect(second).toHaveAttribute("aria-expanded", "false");

  // Topic buttons filter by category.
  const topic = page.getByRole("group", { name: "Filter FAQ topics" }).getByRole("button").nth(1);
  const topicCount = Number((await topic.locator("span").last().innerText()).trim());
  await topic.click();
  await expect(topic).toHaveAttribute("aria-pressed", "true");
  await expect(status).toHaveText(`${topicCount} of ${total}`);

  // The link inside an open answer leads to a working page.
  const answerLink = page.locator("[id^='faq-panel-'] a").first();
  await expect(answerLink).toBeVisible();
  const destination = (await answerLink.getAttribute("href"))!;
  await answerLink.click();
  await expect(page).toHaveURL(new RegExp(`${destination.split("#")[0]}`));
  await expect(page.getByRole("heading", { level: 1 }).first()).toBeVisible();

  expectClean(guard);
});

test("PUB-16 erratic: a dropped network gives a clear failure and recovers when it returns", async ({
  page,
  context,
}) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  const carPath = await firstCarPath(page);

  // The catalogue keeps its last confirmed cars and says the refresh failed.
  const firstCard = page.getByRole("link", { name: /^View details for / }).first();
  await context.setOffline(true);
  await page.locator("#catalogue-sort").selectOption("price:asc");
  await expect(
    page.getByRole("alert").filter({ hasText: "The catalogue could not be loaded" }),
  ).toBeVisible();
  await expect(firstCard).toBeVisible();
  expect(queryParam(page, "sort")).toBeNull();

  // Opening a car while offline cannot render, but nothing is left half-broken.
  await expect(page.goto(carPath)).rejects.toThrow(/ERR_INTERNET_DISCONNECTED/);

  // Back online the same actions work.
  await context.setOffline(false);
  await page.goto("/vehicles");
  await expect(catalogueSummary(page)).toBeVisible();
  await page.waitForLoadState("networkidle");
  await page.locator("#catalogue-sort").selectOption("price:asc");
  await expect.poll(() => queryParam(page, "sort")).toBe("price");

  // A promo check that fails offline can be retried deliberately.
  await page.goto(carPath);
  await expect(page.locator("[data-hydrated]")).toBeAttached();
  const promo = await openPromoFormOnce(page);
  await promo.input.fill("APEX500");
  await context.setOffline(true);
  await promo.apply.click();
  await expect(
    page.getByRole("alert").filter({ hasText: "The promo could not be checked" }),
  ).toBeVisible();
  await expect(promo.status).toHaveText("");
  await context.setOffline(false);
  await promo.apply.click();
  await expect(promo.status).toContainText("APEX500 applied");

  expectClean(guard, offlineConsole);
});

test("PUB-17 the demo disclosure shows once and stays hidden after it is acknowledged", async ({
  browser,
}, info) => {
  const context = await browser.newContext({
    ...info.project.use,
    baseURL: origin,
    storageState: { cookies: [], origins: [] },
  });
  const page = await context.newPage();
  const guard = guardPage(page);
  const dialog = page.getByRole("dialog", { name: "This is a demonstration site" });

  await page.goto("/");
  await expect(dialog).toBeVisible();

  // Escape does not dismiss it; only the button does.
  await page.keyboard.press("Escape");
  await expect(dialog).toBeVisible();

  await dialog.getByRole("button", { name: "I understand", exact: true }).click();
  await expect(dialog).toBeHidden();

  await page.goto("/vehicles");
  await expect(page.getByRole("heading", { level: 1, name: "Used cars for sale" })).toBeVisible();
  await expect(dialog).toHaveCount(0);

  await page.reload();
  await expect(page.getByRole("heading", { level: 1, name: "Used cars for sale" })).toBeVisible();
  await expect(dialog).toHaveCount(0);

  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1 })).toHaveCount(1);
  await expect(dialog).toHaveCount(0);

  expectClean(guard);
  await context.close();
});
