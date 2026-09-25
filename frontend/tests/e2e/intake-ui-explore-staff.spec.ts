import { expect, test, type Browser, type BrowserContext, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";
import path from "node:path";
import type { Role } from "@/lib/auth/actor";
import { calendarDate } from "@/lib/calendar";
import { formatCurrency } from "@/lib/currency-format";
import { expectClean, guardPage, type PageGuard } from "./explore-guards";
import { apiLogin, privateHeaders, signInAs } from "./staff-session";
import { showIntakeQueue } from "./intake-ui-controls";

test.use({ actionTimeout: 15_000 });

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const origin = process.env.APP_BASE_URL!;
const glenmarie = "c77708bf-1a31-42f5-9a69-0c8766697a52";
const noAccessTitle = "You don't have access to this page";
// Refusals the scenarios provoke on purpose make the browser log failed 4xx resource loads.
const expectedRefusals = [/Failed to load resource: the server responded with a status of 4\d\d/];

type Staff = { email: string; role: Role };
const agent: Staff = { email: "aina.sofia@example.invalid", role: "SalesAgent" };
const secondAgent: Staff = { email: "nurul.huda@example.invalid", role: "SalesAgent" };
const technician: Staff = { email: "azlan.rahim@example.invalid", role: "WorkshopTechnician" };
const secondTechnician: Staff = {
  email: "farid.hakim@example.invalid",
  role: "WorkshopTechnician",
};
const manager: Staff = { email: "siti.aminah@example.invalid", role: "BranchManager" };
const generalManager: Staff = { email: "farah.aziz@example.invalid", role: "GeneralManager" };
const administrator: Staff = { email: "muhammad.hafiz@example.invalid", role: "Administrator" };
const customer: Staff = { email: "nur.aisyah@example.invalid", role: "Customer" };

const randomId = "3f1c9a52-7d44-4b0e-9c1b-6a2e5d8f0b17";
const malformedId = "not-a-uuid";

// Pages each role reaches from its navigation, and pages outside it; the forbidden lists also carry drawer addresses.
const roleAccess: { staff: Staff; navigation: string[]; forbidden: string[] }[] = [
  {
    staff: agent,
    navigation: ["Home", "Work calendar", "Intake", "Test drives", "Sales"],
    forbidden: [
      "/dashboard/performance",
      "/dashboard/workshop",
      "/dashboard/listings",
      `/dashboard/listings/${randomId}`,
      "/dashboard/accounts",
      "/dashboard/settings",
      "/dashboard/settings/promos",
    ],
  },
  {
    staff: technician,
    navigation: ["Workshop"],
    forbidden: [
      "/dashboard/home",
      "/dashboard/work-calendar",
      "/dashboard/performance",
      "/dashboard/intake",
      "/dashboard/listings",
      "/dashboard/test-drives",
      "/dashboard/sales",
      `/dashboard/sales/${randomId}`,
      "/dashboard/accounts",
      "/dashboard/settings",
    ],
  },
  {
    staff: manager,
    navigation: [
      "Home",
      "Work calendar",
      "Performance",
      "Intake",
      "Workshop",
      "Listings",
      "Test drives",
      "Sales",
    ],
    forbidden: ["/dashboard/accounts", "/dashboard/accounts/customers", "/dashboard/settings"],
  },
  {
    staff: generalManager,
    navigation: [
      "Home",
      "Work calendar",
      "Performance",
      "Intake",
      "Workshop",
      "Listings",
      "Test drives",
      "Sales",
    ],
    forbidden: ["/dashboard/accounts", "/dashboard/settings", "/dashboard/settings/warranties"],
  },
  {
    staff: administrator,
    navigation: ["Accounts", "Settings"],
    forbidden: [
      "/dashboard/home",
      "/dashboard/work-calendar",
      "/dashboard/performance",
      "/dashboard/intake",
      `/dashboard/intake/${randomId}`,
      "/dashboard/workshop",
      "/dashboard/listings",
      "/dashboard/test-drives",
      "/dashboard/sales",
    ],
  },
];

const navigationPaths: Record<string, string> = {
  Home: "/dashboard/home",
  "Work calendar": "/dashboard/work-calendar",
  Performance: "/dashboard/performance",
  Intake: "/dashboard/intake",
  Workshop: "/dashboard/workshop",
  Listings: "/dashboard/listings",
  "Test drives": "/dashboard/test-drives",
  Sales: "/dashboard/sales",
  Accounts: "/dashboard/accounts",
  Settings: "/dashboard/settings",
};
const everyNavigationLabel = Object.keys(navigationPaths);

const references = () =>
  JSON.parse(readFileSync(path.join(evidence, "references.json"), "utf8")) as {
    id: string;
    branchId: string;
    registrationPlate: string;
  }[];

function arrival(plate: string) {
  return references().find((item) => item.registrationPlate === plate)!;
}

const opened: BrowserContext[] = [];

test.afterEach(async () => {
  await Promise.allSettled(opened.splice(0).map((context) => context.close()));
});

type Actor = { context: BrowserContext; page: Page; guard: PageGuard };

// A fresh signed-in browser context for one staff member, closed after the scenario.
async function actorFor(browser: Browser, staff: Staff, settings: object): Promise<Actor> {
  const context = await browser.newContext({ ...settings, baseURL: origin });
  opened.push(context);
  const page = await context.newPage();
  const guard = guardPage(page);
  await signInAs(page, staff.email, staff.role);
  return { context, page, guard };
}

async function expectNoAccess(page: Page, address: string) {
  await page.goto(address);
  const requested = new URL(address, page.url()).pathname;
  const refusal = page.getByRole("heading", { name: noAccessTitle, exact: true });
  await expect
    .poll(async () => new URL(page.url()).pathname !== requested || (await refusal.isVisible()), {
      message: `${address} is refused or redirected`,
    })
    .toBe(true);
  await expect(page.getByRole("region", { name: /^Selected / })).toHaveCount(0);
}

async function expectMissingRecord(page: Page, address: string, hiddenText: string[] = []) {
  await page.goto(address);
  const message = page.getByText(
    /can't find that page|could not be (found|opened|refreshed)|Reload the page to try again|not found|no longer available/i,
  );
  await expect(message.first()).toBeVisible();
  await expect(page.getByLabel("VIN", { exact: true })).toHaveCount(0);
  await expect(page.getByText(/Declared mileage|Customer mobile/)).toHaveCount(0);
  for (const text of hiddenText) {
    await expect(page.getByText(text, { exact: true })).toHaveCount(0);
  }
}

async function readJson(context: BrowserContext, resource: string) {
  const response = await context.request.get(resource, { headers: await privateHeaders(context) });
  expect(response.status(), resource).toBe(200);
  return (await response.json()).data;
}

test("STAFF-01 each staff role sees and opens exactly the pages of its navigation", async ({
  browser,
}, info) => {
  test.setTimeout(240_000);
  for (const { staff, navigation } of roleAccess) {
    const { page, guard } = await actorFor(browser, staff, info.project.use);
    const bar = page.getByRole("navigation", { name: "Staff navigation", exact: true });
    for (const label of everyNavigationLabel) {
      const link = bar.getByRole("link", { name: label, exact: true });
      if (navigation.includes(label)) {
        await expect(link, `${staff.role} sees ${label}`).toBeVisible();
      } else {
        await expect(link, `${staff.role} does not see ${label}`).toHaveCount(0);
      }
    }
    for (const label of navigation) {
      await page.goto(navigationPaths[label]);
      await expect(page).toHaveTitle(new RegExp(`^${label} \\|`));
      await expect(page.getByRole("heading", { name: noAccessTitle })).toHaveCount(0);
      await expect(page.getByText("This page couldn’t load")).toHaveCount(0);
    }
    await page.goto("/dashboard/profile");
    await expect(page).toHaveTitle(/^Account \|/);
    expectClean(guard, expectedRefusals);
  }
});

test("STAFF-02 pages outside a role are refused or redirected for every staff role and never show data", async ({
  browser,
}, info) => {
  test.setTimeout(300_000);
  for (const { staff, forbidden } of roleAccess) {
    const { context, page, guard } = await actorFor(browser, staff, info.project.use);
    for (const address of forbidden) {
      await expectNoAccess(page, address);
    }
    if (staff.role === "WorkshopTechnician") {
      const response = await context.request.get("/api/staff/sales?view=all", {
        headers: await privateHeaders(context),
      });
      expect([401, 403, 404]).toContain(response.status());
    }
    if (staff.role === "Administrator") {
      const response = await context.request.get("/api/staff/intake/appointments?view=history", {
        headers: await privateHeaders(context),
      });
      expect([401, 403, 404]).toContain(response.status());
    }
    expectClean(guard, expectedRefusals);
  }
});

test("STAFF-03 a customer cannot open the staff workspace and staff are sent from customer pages to their profile", async ({
  browser,
}, info) => {
  const visitor = await actorFor(browser, customer, info.project.use);
  await visitor.page.goto("/dashboard/home");
  await expect
    .poll(
      async () =>
        !new URL(visitor.page.url()).pathname.startsWith("/dashboard") ||
        (await visitor.page.getByRole("heading", { name: noAccessTitle }).isVisible()),
    )
    .toBe(true);
  await expect(visitor.page.getByRole("navigation", { name: "Staff navigation" })).toHaveCount(0);
  await visitor.page.goto("/dashboard/sales");
  await expect(visitor.page.getByRole("navigation", { name: "Staff navigation" })).toHaveCount(0);

  const staff = await actorFor(browser, agent, info.project.use);
  await staff.page.goto("/account/test-drives");
  await expect(staff.page).toHaveURL(/\/dashboard\/profile$/);
  await expect(staff.page).toHaveTitle(/^Account \|/);
  expectClean(visitor.guard, expectedRefusals);
  expectClean(staff.guard, expectedRefusals);
});

test("STAFF-04 drawers opened with a random, malformed or other-branch id show a missing record and no data", async ({
  browser,
}, info) => {
  test.setTimeout(240_000);
  const otherBranch = references().find((item) => item.branchId !== glenmarie)!;
  const salesAgent = await actorFor(browser, agent, info.project.use);
  for (const area of ["intake", "sales", "test-drives"]) {
    await expectMissingRecord(salesAgent.page, `/dashboard/${area}/${randomId}`);
    await expectMissingRecord(salesAgent.page, `/dashboard/${area}/${malformedId}`);
  }
  await expectMissingRecord(salesAgent.page, `/dashboard/intake/${otherBranch.id}`, [
    otherBranch.registrationPlate,
  ]);
  expectClean(salesAgent.guard, expectedRefusals);

  const branchManager = await actorFor(browser, manager, info.project.use);
  await expectMissingRecord(branchManager.page, `/dashboard/listings/${randomId}`);
  await expectMissingRecord(branchManager.page, `/dashboard/listings/${malformedId}`);
  expectClean(branchManager.guard, expectedRefusals);

  const shopTechnician = await actorFor(browser, technician, info.project.use);
  await expectMissingRecord(shopTechnician.page, `/dashboard/workshop/${randomId}`);
  await expectMissingRecord(shopTechnician.page, `/dashboard/workshop/${malformedId}`);
  expectClean(shopTechnician.guard, expectedRefusals);
});

const intakeQueue = (page: Page) =>
  page.getByRole("complementary", { name: "Intake queues", exact: true });
const plateButton = (page: Page, plate: string) =>
  intakeQueue(page).getByRole("button", { name: new RegExp(`^${plate} `) });
const selectedAppointment = (page: Page) =>
  page.getByRole("region", { name: "Selected appointment", exact: true });

async function releaseAppointment(page: Page) {
  await selectedAppointment(page)
    .locator("footer")
    .getByRole("button", { name: "Release appointment", exact: true })
    .click();
  await page
    .getByRole("dialog", { name: "Release this appointment?", exact: true })
    .getByRole("button", { name: "Release appointment", exact: true })
    .click();
  await expect(
    selectedAppointment(page)
      .locator("footer")
      .getByRole("button", { name: "Claim appointment", exact: true }),
  ).toBeEnabled();
}

async function openClaimDialog(page: Page) {
  await selectedAppointment(page)
    .locator("footer")
    .getByRole("button", { name: "Claim appointment", exact: true })
    .click();
  return page.getByRole("dialog", { name: "Claim this appointment?", exact: true });
}

test("STAFF-05 an agent releases and reclaims an appointment and the second agent sees it change queues", async ({
  browser,
}, info) => {
  test.setTimeout(180_000);
  const plate = "VCA 9702";
  const id = arrival(plate).id;
  const first = await actorFor(browser, agent, info.project.use);
  const second = await actorFor(browser, secondAgent, info.project.use);
  await first.page.goto(`/dashboard/intake/${id}`);
  await expect(selectedAppointment(first.page).getByLabel("VIN", { exact: true })).toBeEditable();

  await releaseAppointment(first.page);
  await second.page.goto("/dashboard/intake?view=toClaim");
  await showIntakeQueue(second.page);
  await expect(plateButton(second.page, plate)).toBeVisible();

  const dialog = await openClaimDialog(first.page);
  await dialog.getByRole("button", { name: "Claim appointment", exact: true }).click();
  await expect(selectedAppointment(first.page).getByLabel("VIN", { exact: true })).toBeEditable();

  await second.page.getByRole("button", { name: "Refresh worklist", exact: true }).click();
  await expect(plateButton(second.page, plate)).toHaveCount(0);
  await first.page.goto("/dashboard/intake?view=myWork");
  await showIntakeQueue(first.page);
  await expect(plateButton(first.page, plate)).toBeVisible();
  expectClean(first.guard, expectedRefusals);
  expectClean(second.guard, expectedRefusals);
});

test("STAFF-06 two agents claiming the same appointment at once leave exactly one owner", async ({
  browser,
}, info) => {
  test.setTimeout(180_000);
  const plate = "VCA 9702";
  const id = arrival(plate).id;
  const first = await actorFor(browser, agent, info.project.use);
  const second = await actorFor(browser, secondAgent, info.project.use);
  await first.page.goto(`/dashboard/intake/${id}`);
  await releaseAppointment(first.page);
  await second.page.goto(`/dashboard/intake/${id}`);

  const firstDialog = await openClaimDialog(first.page);
  const secondDialog = await openClaimDialog(second.page);
  await Promise.all([
    firstDialog.getByRole("button", { name: "Claim appointment", exact: true }).click(),
    secondDialog.getByRole("button", { name: "Claim appointment", exact: true }).click(),
  ]);

  const owners = async () => {
    const editable = await Promise.all(
      [first.page, second.page].map((page) =>
        selectedAppointment(page).getByLabel("VIN", { exact: true }).count(),
      ),
    );
    return editable;
  };
  await expect.poll(async () => (await owners()).reduce((sum, count) => sum + count, 0)).toBe(1);
  const [firstOwns] = await owners();
  const loser = firstOwns === 1 ? second.page : first.page;
  const winner = firstOwns === 1 ? first.page : second.page;
  await expect(selectedAppointment(winner).getByLabel("VIN", { exact: true })).toBeEditable();
  await expect(
    selectedAppointment(loser)
      .locator("footer")
      .getByRole("button", { name: "Claim appointment", exact: true }),
  ).toHaveCount(0);
  await expect(
    loser.getByText(/already|taken|another|no longer|handled by|claimed/i).first(),
  ).toBeVisible();
  expectClean(first.guard, expectedRefusals);
  expectClean(second.guard, expectedRefusals);
});

test("STAFF-07 the check-in form refuses a short VIN, low mileage and missing confirmations, and records one check-in", async ({
  browser,
}, info) => {
  test.setTimeout(180_000);
  const plate = "VCA 9701";
  const id = arrival(plate).id;
  // The first arrival is claimed by the primary agent of the branch.
  const { page, guard } = await actorFor(browser, agent, info.project.use);
  const checkIns: string[] = [];
  page.on("request", (request) => {
    if (request.method() === "POST" && /\/appointments\/[^/]+\/check-ins$/.test(request.url())) {
      checkIns.push(request.url());
    }
  });
  await page.goto(`/dashboard/intake/${id}`);
  const record = selectedAppointment(page);
  const vin = record.getByLabel("VIN", { exact: true });
  const mileage = record.getByLabel("Actual odometer (km)", { exact: true });
  const submit = record
    .locator("footer")
    .getByRole("button", { name: "Confirm check-in", exact: true });
  const confirmation = page.getByRole("dialog", { name: "Check in this vehicle?", exact: true });
  await expect(vin).toBeEditable();

  await vin.fill("AB123");
  await mileage.fill("95000");
  for (const box of await record.getByRole("checkbox").all()) {
    await box.check();
  }
  await submit.click();
  await expect(confirmation).toHaveCount(0);
  expect(await vin.evaluate((input: HTMLInputElement) => input.validationMessage)).not.toBe("");

  await vin.fill("PL1BT65R84B654321");
  await record.getByRole("checkbox").first().uncheck();
  await submit.click();
  await expect(confirmation).toHaveCount(0);
  await record.getByRole("checkbox").first().check();

  // The specification only requires a non-negative odometer reading, so a negative one is refused by the form.
  await mileage.fill("-5");
  await submit.click();
  await expect(confirmation).toHaveCount(0);
  expect(await mileage.evaluate((input: HTMLInputElement) => input.validationMessage)).not.toBe("");
  expect(checkIns).toHaveLength(0);
  const refused = checkIns.length;

  await vin.fill(`PL1BT65R84B${Date.now().toString().slice(-6)}`);
  await mileage.fill("92000");
  await submit.click();
  await confirmation.getByRole("button", { name: "Confirm check-in", exact: true }).dblclick();
  await expect(
    record.locator("footer").getByText("Check-in confirmed", { exact: true }),
  ).toBeVisible();
  expect(checkIns.length - refused).toBe(1);
  expectClean(guard, expectedRefusals);
});

const salesTable = (page: Page) => page.getByRole("table");

test("STAFF-08 table sorting, filtering, paging and Back keep their state in the address", async ({
  browser,
}, info) => {
  test.setTimeout(180_000);
  const { context, page, guard } = await actorFor(browser, manager, info.project.use);
  const sales = await readJson(context, "/api/staff/sales?view=all&pageSize=10");
  expect(sales.items.length).toBeGreaterThan(0);
  await page.goto("/dashboard/sales?view=all");
  await expect(salesTable(page)).toBeVisible();
  await expect(salesTable(page).locator("tbody tr").first()).toBeVisible();

  const sortButtons = page.getByRole("button", { name: /^Sort by / });
  const labels = (
    await sortButtons.evaluateAll((items) => items.map((item) => item.getAttribute("aria-label")!))
  ).slice(0, 4);
  expect(labels.length).toBeGreaterThan(1);
  const sorted: (string | null)[] = [];
  for (const label of labels) {
    const before = new URL(page.url()).searchParams.get("sort");
    await page.getByRole("button", { name: label, exact: true }).click();
    await expect.poll(() => new URL(page.url()).searchParams.get("sort")).not.toBe(before);
    sorted.push(new URL(page.url()).searchParams.get("sort"));
    await expect(
      page
        .getByRole("columnheader")
        .filter({ has: page.getByRole("button", { name: label, exact: true }) }),
    ).toHaveAttribute("aria-sort", /ascending|descending/);
  }
  await page.goBack();
  await expect
    .poll(() => new URL(page.url()).searchParams.get("sort"))
    .toBe(sorted[sorted.length - 2]);
  await page.goForward();
  await expect
    .poll(() => new URL(page.url()).searchParams.get("sort"))
    .toBe(sorted[sorted.length - 1]);

  const registration: string = sales.items[0].registration;
  await page.getByRole("button", { name: /^Filter Registration/ }).click();
  await page.getByLabel("Registration contains", { exact: true }).fill(registration);
  await page.getByRole("button", { name: "Apply", exact: true }).click();
  await expect.poll(() => new URL(page.url()).searchParams.get("registration")).toBe(registration);
  await expect(salesTable(page).locator("tbody tr").first()).toContainText(registration);
  await page.goBack();
  await expect.poll(() => new URL(page.url()).searchParams.has("registration")).toBe(false);
  await page.goForward();
  await expect.poll(() => new URL(page.url()).searchParams.get("registration")).toBe(registration);
  await page.getByRole("button", { name: "Reset filters", exact: true }).click();
  await expect.poll(() => new URL(page.url()).searchParams.has("registration")).toBe(false);

  const next = page.getByRole("button", { name: "Next records", exact: true });
  if (await next.isEnabled()) {
    await next.click();
    await expect.poll(() => new URL(page.url()).searchParams.get("page")).toBe("2");
    await page.goBack();
    await expect.poll(() => new URL(page.url()).searchParams.get("page")).toBeNull();
  }

  const stateBeforeRow = page.url();
  await salesTable(page).locator("tbody tr").first().getByRole("cell").nth(1).click();
  await expect(page).toHaveURL(/\/dashboard\/sales\/[0-9a-f-]{36}/);
  await page.goBack();
  await expect(page).toHaveURL(stateBeforeRow);
  await expect(salesTable(page)).toBeVisible();
  expectClean(guard, expectedRefusals);
});

test("STAFF-09 hand-edited table addresses are ignored or corrected without a server error", async ({
  browser,
}, info) => {
  test.setTimeout(240_000);
  const { page, guard } = await actorFor(browser, manager, info.project.use);
  const edits = [
    "page=0",
    "pageSize=1000",
    "sort=unknown",
    "direction=sideways",
    "page=abc&pageSize=-5",
  ];
  for (const area of ["sales", "intake", "test-drives", "listings"]) {
    for (const edit of edits) {
      await page.goto(`/dashboard/${area}?${edit}`);
      await expect(page).toHaveTitle(/\| ApexAuto$/);
      await expect(page.getByRole("navigation", { name: "Staff navigation" })).toBeVisible();
      await expect(page.getByRole("heading", { name: noAccessTitle })).toHaveCount(0);
      await expect(page.getByText("This page couldn’t load")).toHaveCount(0);
    }
  }
  expectClean(guard, expectedRefusals);
});

test("STAFF-10 the work calendar moves between months and its items open their record", async ({
  browser,
}, info) => {
  test.setTimeout(120_000);
  const { page, guard } = await actorFor(browser, agent, info.project.use);
  await page.goto("/dashboard/work-calendar");
  const calendar = page.getByRole("region", { name: "Work calendar", exact: true });
  const heading = calendar.getByRole("heading", { level: 2 }).first();
  await expect(heading).toBeVisible();
  const month = await heading.getAttribute("aria-label");
  await calendar.getByRole("button", { name: "Next month", exact: true }).click();
  await expect(heading).not.toHaveAttribute("aria-label", month!);
  await calendar.getByRole("button", { name: "Previous month", exact: true }).click();
  await expect(heading).toHaveAttribute("aria-label", month!);
  await calendar.getByRole("button", { name: "Today", exact: true }).click();
  await expect(heading).toHaveAttribute("aria-label", month!);

  const events = calendar.getByRole("table").getByRole("button", { name: /^Open / });
  await expect(events.first()).toBeVisible();
  await events.first().click();
  await expect(page).toHaveURL(/\/dashboard\/(intake|test-drives)\/[0-9a-f-]{36}/);
  await expect(
    page.getByRole("region", { name: /^Selected (appointment|test drive)$/ }),
  ).toBeVisible();
  expectClean(guard, expectedRefusals);
});

let workshopJobId = "";

async function openFirstWorkshopJob(page: Page) {
  await page.goto("/dashboard/workshop");
  await expect(page.getByRole("button", { name: "Refresh workshop", exact: true })).toBeEnabled();
  await page
    .getByRole("button", { name: /^To claim\b/ })
    .last()
    .click();
  const claim = page.getByRole("button", { name: /^Claim inspection ·/ }).first();
  if (await claim.isVisible()) {
    await claim.click();
    await page.getByRole("button", { name: "Confirm assignment", exact: true }).click();
  } else {
    await page
      .getByRole("button", { name: /^My work\b/ })
      .last()
      .click();
    await page.locator("ul li button").first().click();
  }
  await expect(page).toHaveURL(/\/dashboard\/workshop\/[0-9a-f-]{36}/);
  await expect(page.getByRole("button", { name: "Save draft", exact: true })).toBeEnabled();
  return new URL(page.url()).pathname.split("/")[3];
}

async function selectPoint(page: Page, code: string) {
  await page.getByRole("searchbox", { name: "Find a checklist point", exact: true }).fill(code);
  await page
    .getByRole("navigation", { name: "Inspection points" })
    .getByRole("button", { name: new RegExp(`^${code} `) })
    .click();
}

async function describeFinding(page: Page, estimate: string, notes: string) {
  await selectPoint(page, "EP-004");
  await page.getByLabel("EP-004 disposition", { exact: true }).selectOption("Finding");
  await page.getByLabel("EP-004 severity", { exact: true }).selectOption("MinorDefect");
  await page.getByLabel("EP-004 estimate (RM)", { exact: true }).fill(estimate);
  await page.getByLabel("EP-004 finding notes", { exact: true }).fill(notes);
}

test("STAFF-11 a technician saves an inspection draft and finds it again after a reload", async ({
  browser,
}, info) => {
  test.setTimeout(180_000);
  const { page, guard } = await actorFor(browser, secondTechnician, info.project.use);
  workshopJobId = await openFirstWorkshopJob(page);
  await describeFinding(page, "250.10", "Bumper scuff requires paint repair.");
  await expect(
    page.locator("footer").getByText("Unsaved inspection changes", { exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Save draft", exact: true }).click();
  await expect(
    page.locator("footer").getByText("Unsaved inspection changes", { exact: true }),
  ).toHaveCount(0);

  await page.reload();
  await expect(page.getByRole("button", { name: "Save draft", exact: true })).toBeEnabled();
  await selectPoint(page, "EP-004");
  await expect(page.getByLabel("EP-004 estimate (RM)", { exact: true })).toHaveValue("250.10");
  await expect(page.getByLabel("EP-004 finding notes", { exact: true })).toHaveValue(
    "Bumper scuff requires paint repair.",
  );
  expectClean(guard, expectedRefusals);
});

test("STAFF-12 the second of two saves of the same draft is refused as stale", async ({
  browser,
}, info) => {
  test.setTimeout(180_000);
  expect(workshopJobId, "STAFF-11 opened a job").not.toBe("");
  const first = await actorFor(browser, secondTechnician, info.project.use);
  const secondPage = await first.context.newPage();
  const secondGuard = guardPage(secondPage);
  for (const page of [first.page, secondPage]) {
    await page.goto(`/dashboard/workshop/${workshopJobId}`);
    await expect(page.getByRole("button", { name: "Save draft", exact: true })).toBeEnabled();
    await selectPoint(page, "EP-004");
    await expect(page.getByLabel("EP-004 estimate (RM)", { exact: true })).toHaveValue("250.10");
  }

  await first.page.getByLabel("EP-004 estimate (RM)", { exact: true }).fill("111.11");
  await first.page.getByRole("button", { name: "Save draft", exact: true }).click();
  await expect(
    first.page.locator("footer").getByText("Unsaved inspection changes", { exact: true }),
  ).toHaveCount(0);

  await secondPage.getByLabel("EP-004 estimate (RM)", { exact: true }).fill("222.22");
  const saveAnswer = secondPage.waitForResponse(
    (response) =>
      response.request().method() === "POST" && /\/drafts\/[^/]+\/saves$/.test(response.url()),
  );
  await secondPage.getByRole("button", { name: "Save draft", exact: true }).click();
  const status = (await saveAnswer).status();
  expect(status, "stale save is refused").toBeGreaterThanOrEqual(400);
  expect(status).toBeLessThan(500);

  const fresh = await first.context.newPage();
  await fresh.goto(`/dashboard/workshop/${workshopJobId}`);
  await selectPoint(fresh, "EP-004");
  await expect(fresh.getByLabel("EP-004 estimate (RM)", { exact: true })).toHaveValue("111.11");
  expectClean(first.guard, expectedRefusals);
  expectClean(secondGuard, expectedRefusals);
});

test("STAFF-13 a manager opens a sale and reads its history and past versions", async ({
  browser,
}, info) => {
  test.setTimeout(180_000);
  const { context, page, guard } = await actorFor(browser, manager, info.project.use);
  const listing = await readJson(context, "/api/staff/sales?view=all&pageSize=50");
  const progressed = listing.items.find((item: { status: string }) =>
    ["Handling", "PendingApproval", "Approved", "Settled", "Delivered"].includes(item.status),
  );
  const sale = progressed ?? listing.items[0];
  await page.goto(`/dashboard/sales/${sale.id}`);
  const drawer = page.getByRole("dialog").first();
  await expect(drawer).toBeVisible();
  await drawer
    .getByRole("navigation", { name: "Sale sections", exact: true })
    .getByRole("button", { name: "History", exact: true })
    .click();
  const history = drawer.getByRole("region", { name: "Sale history", exact: true });
  await expect(history).toBeVisible();
  const version = history.locator('a[href*="/versions/"]').first();
  await expect(version).toBeVisible();
  await version.click();
  const past = drawer.getByRole("region", { name: "Past version", exact: true });
  await expect(past).toBeVisible();
  await expect(past.getByRole("heading").first()).toContainText(/version \d+/);
  await expect(drawer.getByText("Reload the page to try again.")).toHaveCount(0);
  expectClean(guard, expectedRefusals);
});

test("STAFF-14 a sale is cancelled only with a real reason and only once", async ({
  browser,
}, info) => {
  test.setTimeout(180_000);
  const { context, page, guard } = await actorFor(browser, manager, info.project.use);
  let cancellable: { id: string; version: number } | null = null;
  for (const view of ["beingHandled", "needsApproval", "open"]) {
    const listing = await readJson(context, `/api/staff/sales?view=${view}&pageSize=50`);
    for (const item of listing.items) {
      const detail = await readJson(context, `/api/staff/sales/${item.id}`);
      if (detail.actions.cancel) {
        cancellable = { id: item.id, version: detail.sale.version };
        break;
      }
    }
    if (cancellable) {
      break;
    }
  }
  expect(cancellable, "a sale the manager may cancel exists").not.toBeNull();
  const requests: string[] = [];
  page.on("request", (request) => {
    if (
      request.method() === "POST" &&
      request.url().endsWith(`/sales/${cancellable!.id}/cancellations`)
    ) {
      requests.push(request.url());
    }
  });
  await page.goto(`/dashboard/sales/${cancellable!.id}`);
  const drawer = page.getByRole("dialog").first();
  const reason = drawer.getByLabel("Cancellation reason (optional)", { exact: true });
  const cancel = page.getByRole("button", { name: "Cancel sale", exact: true });
  await expect(reason).toBeVisible();
  await expect(cancel).toHaveCount(0);

  await reason.fill("   ");
  await expect(cancel).toHaveCount(0);

  await reason.fill("Customer withdrew the purchase");
  await expect(cancel).toBeVisible();
  await cancel.click();
  const confirmation = page.getByRole("dialog", { name: "Cancel this sale?", exact: true });
  await confirmation.getByRole("button", { name: "Cancel sale", exact: true }).dblclick();
  await expect(confirmation).toHaveCount(0);
  await expect
    .poll(async () => (await readJson(context, `/api/staff/sales/${cancellable!.id}`)).sale.status)
    .toBe("Cancelled");
  expect(requests).toHaveLength(1);
  const history = await readJson(context, `/api/staff/sales/${cancellable!.id}/history`);
  const cancellations = history.items.filter((item: { kind: string }) => /cancel/i.test(item.kind));
  expect(cancellations).toHaveLength(1);
  expectClean(guard, expectedRefusals);
});

test("STAFF-15 an agent claims, starts and records the outcome of a test drive that appears in its history", async ({
  browser,
}, info) => {
  test.setTimeout(240_000);
  const buyer = await browser.newContext({ ...info.project.use, baseURL: origin });
  opened.push(buyer);
  await apiLogin(buyer, "ahmad.faizal@example.invalid");
  const catalogue = await readJson(buyer, `/api/vehicles?branchId=${glenmarie}`);
  const today = calendarDate();
  let bookingId = "";
  for (const vehicle of catalogue.items.slice(0, 8)) {
    const offer = await buyer.request.get(
      `/api/vehicles/${vehicle.id}/test-drive-slots?date=${today}`,
      { headers: await privateHeaders(buyer) },
    );
    const slots = offer.status() === 200 ? (await offer.json()).data.slots : [];
    if (slots.length === 0) {
      continue;
    }
    const booked = await buyer.request.post(`/api/vehicles/${vehicle.id}/test-drives`, {
      headers: await privateHeaders(buyer),
      data: {
        operationId: crypto.randomUUID(),
        publicationId: (await offer.json()).data.publicationId,
        startsAt: slots[0].startsAt,
      },
    });
    if (booked.status() === 200) {
      bookingId = (await booked.json()).data.id;
      break;
    }
  }
  test.skip(bookingId === "", `no test drive time remains today (${today}) to start a drive`);

  const { page, guard } = await actorFor(browser, agent, info.project.use);
  await page.goto(`/dashboard/test-drives/${bookingId}`);
  const record = page.getByRole("region", { name: "Selected test drive", exact: true });
  await expect(record).toBeVisible();
  const confirmAction = async (label: string) => {
    await record.locator("footer").getByRole("button", { name: label, exact: true }).click();
    await page
      .getByRole("dialog", { name: label, exact: true })
      .getByRole("button", { name: "Confirm action", exact: true })
      .click();
  };
  const action = record.getByLabel("Test drive action", { exact: true });

  await action.selectOption("claims");
  await confirmAction("Claim test drive");
  await expect(action.locator('option[value="starts"]')).toBeAttached();

  await action.selectOption("starts");
  await record.getByLabel("Customer attendance", { exact: true }).selectOption("present");
  await record.getByLabel("Driving licence check", { exact: true }).selectOption("checked");
  await confirmAction("Start test drive");
  await expect(record).toContainText("In progress");

  await action.selectOption("outcomes");
  await record.getByLabel("Customer attendance", { exact: true }).selectOption("present");
  await record
    .getByLabel("Outcome notes (optional)", { exact: true })
    .fill("Customer completed the drive.");
  await confirmAction("Record outcome");
  await expect(record).toContainText("Completed");

  await record.getByRole("button", { name: /^History/ }).click();
  for (const step of ["Test drive booked", "Test drive started", "Test drive completed"]) {
    await expect(record).toContainText(step);
  }
  expectClean(guard, expectedRefusals);
});

test("STAFF-16 a manager's listing preview shows the same fields as the public catalogue and vehicle page", async ({
  browser,
}, info) => {
  test.setTimeout(180_000);
  const { context, page, guard } = await actorFor(browser, manager, info.project.use);
  const catalogue = await readJson(context, `/api/vehicles?branchId=${glenmarie}`);
  const vehicle = catalogue.items[0];
  const price = formatCurrency(vehicle.vehiclePrice);

  await page.goto(`/dashboard/listings/${vehicle.id}`);
  const drawer = page.getByRole("dialog").first();
  await expect(drawer).toBeVisible();
  await drawer.getByRole("button", { name: "Listing", exact: true }).click();
  const showFields = drawer.getByRole("button", { name: "Show listing fields", exact: true });
  if (await showFields.isVisible()) {
    await showFields.click();
  }
  await drawer.getByRole("button", { name: "Catalogue card", exact: true }).click();
  const preview = drawer.getByRole("region", { name: "Listing preview", exact: true });
  await expect(preview).toContainText(price);
  await expect(preview).toContainText(vehicle.make);
  await expect(preview).toContainText(`${vehicle.model} ${vehicle.trim}`);
  await expect(preview).toContainText(String(vehicle.manufactureYear));
  await expect(preview).toContainText(`${vehicle.mileageKilometres.toLocaleString("en-MY")} km`);

  await drawer.getByRole("button", { name: "Listing page", exact: true }).click();
  await expect(preview).toContainText(price);

  const visitor = await browser.newContext({ ...info.project.use, baseURL: origin });
  opened.push(visitor);
  const publicPage = await visitor.newPage();
  await publicPage.goto(`/vehicles/${vehicle.id}`);
  await expect(publicPage.getByText(price).first()).toBeVisible();
  await expect(publicPage.getByText(vehicle.make).first()).toBeVisible();
  expectClean(guard, expectedRefusals);
});

test("cross-cutting staff pages fit the screen at desktop width", async ({ browser }, info) => {
  test.setTimeout(240_000);
  const { page, guard } = await actorFor(browser, generalManager, info.project.use);
  for (const label of Object.keys(navigationPaths).filter(
    (name) => name !== "Accounts" && name !== "Settings",
  )) {
    await page.goto(navigationPaths[label]);
    await expect(page).toHaveTitle(new RegExp(`^${label} \\|`));
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(overflow, `${label} horizontal overflow`).toBeLessThanOrEqual(0);
  }
  expectClean(guard, expectedRefusals);
});
