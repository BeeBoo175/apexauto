import { randomUUID } from "node:crypto";
import { expect, test, type Locator, type Page } from "@playwright/test";
import { expectClean, expectNoSidewaysScroll, guardPage } from "./explore-guards";
import { signInAs } from "./staff-session";

const administrator = "muhammad.hafiz@example.invalid";
const administratorName = "Muhammad Hafiz";
const generalManager = "farah.aziz@example.invalid";
const existingEmail = "aina.sofia@example.invalid";

test.beforeEach(({}, info) => {
  test.skip(info.project.name === "mobile", "Administration is a desktop workflow.");
  test.setTimeout(180_000);
});

function uniqueEmail(prefix: string) {
  return `${prefix}-${randomUUID().slice(0, 8)}@example.invalid`;
}

async function signInAdministrator(page: Page) {
  await signInAs(page, administrator, "Administrator");
}

function staffSearch(page: Page) {
  return page.getByRole("searchbox", { name: "Search staff", exact: true });
}

async function searchStaff(page: Page, text: string) {
  const box = staffSearch(page);
  await box.fill(text);
  await box.press("Enter");
  await expect(page).toHaveURL(/search=/);
}

async function expectNoRawCodes(page: Page) {
  const text = await page.locator("body").innerText();
  expect(text, "raw machine codes on screen").not.toMatch(
    /\b(customer-has-reservation|[a-z]+(?:-[a-z]+){2,})\b(?=\s*$)/m,
  );
}

async function openInviteDialog(page: Page) {
  await page.getByRole("button", { name: "Invite staff", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Invite staff", exact: true });
  await expect(dialog).toBeVisible();
  return dialog;
}

async function fillInvite(dialog: Locator, details: { name: string; email: string }) {
  await dialog.getByLabel("Full name", { exact: true }).fill(details.name);
  await dialog.getByLabel("Email", { exact: true }).fill(details.email);
  await dialog.getByLabel("Work mobile", { exact: true }).fill("+60123456789");
  await dialog.getByLabel("Role", { exact: true }).selectOption({ label: "Administrator" });
}

async function inviteAdministrator(page: Page, email: string) {
  const dialog = await openInviteDialog(page);
  await fillInvite(dialog, { name: "Explore Invitee", email });
  await dialog.getByRole("button", { name: "Send invitation", exact: true }).click();
  await expect(dialog).toHaveCount(0);
}

test("ADM-01 the administrator searches, sorts and pages staff and customer accounts", async ({
  page,
}) => {
  const guard = guardPage(page);
  await signInAdministrator(page);
  await expect(page.getByLabel("Staff accounts", { exact: true }).first()).toBeVisible();
  await expectNoSidewaysScroll(page);

  await searchStaff(page, "Aina");
  await expect(page).toHaveURL(/search=Aina/);
  await expect(page.getByRole("row").filter({ hasText: "Aina Sofia" })).toBeVisible();

  await staffSearch(page).fill("");
  await staffSearch(page).press("Enter");
  await expect(page).not.toHaveURL(/search=Aina/);

  await page.getByRole("button", { name: "Sort by Name", exact: true }).click();
  await expect(page).toHaveURL(/sort=name/);
  await page.getByRole("button", { name: "Sort by Name", exact: true }).click();
  await expect(page).toHaveURL(/direction=desc/);

  await page.getByRole("button", { name: /^Invited/ }).click();
  await expect(page).toHaveURL(/view=invited/);
  await page.getByRole("button", { name: /^All/ }).click();
  await expect(page).not.toHaveURL(/view=/);

  // Paging: a small page size makes a second page, and the address carries it.
  await page.getByLabel("Rows per page", { exact: true }).selectOption({ index: 0 });
  const next = page.getByRole("button", { name: "Next records", exact: true });
  await expect(next).toBeEnabled();
  await next.click();
  await expect(page).toHaveURL(/page=2/);
  await page.getByRole("button", { name: "Previous records", exact: true }).click();
  await expect(page).not.toHaveURL(/page=2/);

  // Customers view.
  await page.goto("/dashboard/accounts/customers");
  const customers = page.getByRole("searchbox", { name: "Search customers", exact: true });
  await customers.fill("Nur");
  await customers.press("Enter");
  await expect(page).toHaveURL(/search=Nur/);
  await expect(page.getByRole("row").filter({ hasText: "Nur Aisyah" }).first()).toBeVisible();
  await page.getByRole("button", { name: "Sort by Name", exact: true }).click();
  await expect(page).toHaveURL(/sort=name/);
  await page.getByRole("button", { name: /^Unverified/ }).click();
  await expect(page).toHaveURL(/view=unverified/);
  await expectNoSidewaysScroll(page);
  expectClean(guard);
});

test("ADM-02 searching with wildcards, quotes, long text and emoji is literal and never errors", async ({
  page,
}) => {
  const guard = guardPage(page);
  await signInAdministrator(page);
  const searches = ["%", "_", `' OR 1=1 --`, `"quoted"`, "x".repeat(500), "😀🚗"];
  for (const text of searches) {
    await searchStaff(page, text);
    await expect(page.getByRole("searchbox", { name: "Search staff", exact: true })).toBeVisible();
    await expect(page.getByRole("alert")).toHaveCount(0);
    // A wildcard character must not match everyone: the literal text matches no seeded person.
    await expect(page.getByRole("row").filter({ hasText: "Aina Sofia" })).toHaveCount(0);
  }
  expectClean(guard);
});

test("ADM-03 an invitation is listed, resent and revoked", async ({ page }) => {
  const guard = guardPage(page);
  await signInAdministrator(page);
  const email = uniqueEmail("adm03");
  await inviteAdministrator(page, email);

  await page.getByRole("button", { name: /^Invited/ }).click();
  await expect(page).toHaveURL(/view=invited/);
  await searchStaff(page, email);
  const row = page.getByRole("row").filter({ hasText: email });
  await expect(row).toBeVisible();
  await expect(row).toContainText("Invited");

  await row.click();
  const drawer = page.getByRole("dialog").first();
  await drawer.getByRole("button", { name: "Resend invitation", exact: true }).click();
  await expect(page.getByText("Invitation sent again.")).toBeVisible();
  await expect(row).toContainText("Invited");

  await drawer.getByRole("button", { name: "Revoke", exact: true }).click();
  const confirm = page.getByRole("dialog", { name: "Revoke this invitation?", exact: true });
  await confirm.getByRole("button", { name: "Revoke", exact: true }).click();
  await expect(page.getByRole("row").filter({ hasText: email })).toHaveCount(0);
  expectClean(guard);
});

test("ADM-04 invitation refusals leave nothing behind and a double click sends one", async ({
  page,
}) => {
  const guard = guardPage(page);
  await signInAdministrator(page);

  // An email already used by an account is refused with a message.
  let dialog = await openInviteDialog(page);
  await fillInvite(dialog, { name: "Duplicate Person", email: existingEmail });
  await dialog.getByRole("button", { name: "Send invitation", exact: true }).click();
  await expect(page.getByRole("alert").first()).toBeVisible();
  await expect(dialog).toBeVisible();
  await expectNoRawCodes(page);

  // An invalid address does not send.
  await dialog.getByLabel("Email", { exact: true }).fill("not-an-email");
  await dialog.getByRole("button", { name: "Send invitation", exact: true }).click();
  await expect(dialog).toBeVisible();
  const validity = await dialog
    .getByLabel("Email", { exact: true })
    .evaluate((input: HTMLInputElement) => input.validity.valid);
  expect(validity).toBe(false);

  // A branch role without a branch cannot be sent.
  await dialog.getByLabel("Email", { exact: true }).fill(uniqueEmail("adm04-branch"));
  await dialog.getByLabel("Role", { exact: true }).selectOption({ label: "Sales agent" });
  await expect(dialog.getByLabel("Branch", { exact: true })).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Send invitation", exact: true })).toBeDisabled();
  await dialog.getByRole("button", { name: "Cancel", exact: true }).click();
  await expect(dialog).toHaveCount(0);

  // Double click sends exactly one invitation.
  const email = uniqueEmail("adm04-double");
  dialog = await openInviteDialog(page);
  await fillInvite(dialog, { name: "Double Click", email });
  await dialog.getByRole("button", { name: "Send invitation", exact: true }).dblclick();
  await expect(dialog).toHaveCount(0);
  await page.keyboard.press("Escape");
  await searchStaff(page, email);
  await expect(page.getByRole("row").filter({ hasText: email })).toHaveCount(1);
  expectClean(guard);
});

test("ADM-05 the administrator cannot erase, suspend or deactivate their own account", async ({
  page,
}) => {
  const guard = guardPage(page);
  await signInAdministrator(page);
  await searchStaff(page, administratorName);
  await page.getByRole("row").filter({ hasText: administratorName }).first().click();
  const drawer = page.getByRole("dialog").first();
  await expect(drawer).toBeVisible();
  await expect(drawer.getByRole("button", { name: "Erase account", exact: true })).toHaveCount(0);
  await expect(drawer.getByRole("button", { name: "Suspend account", exact: true })).toHaveCount(0);
  await expect(drawer.getByRole("button", { name: "Deactivate", exact: true })).toHaveCount(0);
  expectClean(guard);
});

test("ADM-06 every settings section loads and its address survives a reload", async ({ page }) => {
  const guard = guardPage(page);
  await signInAdministrator(page);
  const sections = [
    { path: "/dashboard/settings", button: "Settings", marker: "Processing fee (RM)" },
    { path: "/dashboard/settings/promos", button: "Promos", marker: "New promo" },
    {
      path: "/dashboard/settings/warranties",
      button: "Warranty packages",
      marker: "New package",
    },
    {
      path: "/dashboard/settings/retention",
      button: "Retention",
      marker: "Security records (days)",
    },
  ];
  await page.goto("/dashboard/settings");
  for (const section of sections) {
    await page.getByRole("button", { name: section.button, exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`${section.path}$`));
    await expect(
      page
        .getByLabel(section.marker, { exact: true })
        .or(page.getByRole("button", { name: section.marker, exact: true })),
    ).toBeVisible();
    await page.reload();
    await expect(page).toHaveURL(new RegExp(`${section.path}$`));
    await expect(
      page
        .getByLabel(section.marker, { exact: true })
        .or(page.getByRole("button", { name: section.marker, exact: true })),
    ).toBeVisible();
    await expectNoSidewaysScroll(page);
  }
  expectClean(guard);
});

test("ADM-07 purchase and promo forms refuse bad amounts and codes and save nothing", async ({
  page,
}) => {
  const guard = guardPage(page);
  await signInAdministrator(page);

  // Purchase settings: invalid values disable saving and nothing persists.
  await page.goto("/dashboard/settings");
  const fee = page.getByLabel("Processing fee (RM)", { exact: true });
  const original = await fee.inputValue();
  const save = page.getByRole("button", { name: "Save settings" });
  for (const bad of ["-5", "abc", "99999999999999999999"]) {
    await fee.fill(bad);
    await expect(save).toBeDisabled();
    await expect(fee).toHaveAttribute("aria-invalid", "true");
  }
  await page.reload();
  await expect(page.getByLabel("Processing fee (RM)", { exact: true })).toHaveValue(original);

  // Promo form.
  await page.goto("/dashboard/settings/promos");
  await page.getByRole("button", { name: "New promo", exact: true }).click();
  const drawer = page.getByRole("dialog", { name: "New promo", exact: true });
  const create = drawer.getByRole("button", { name: "Create promo", exact: true });
  await drawer.getByLabel("Expires (Malaysia time)", { exact: true }).fill("2031-01-01T10:00");
  await drawer
    .getByLabel("Code", { exact: true })
    .fill(`E2E${randomUUID().slice(0, 6).toUpperCase()}`);
  for (const bad of ["-5", "abc"]) {
    await drawer.getByLabel("Discount (RM)", { exact: true }).fill(bad);
    await expect(create).toBeDisabled();
    await expect(drawer.getByText("Enter an amount with up to two decimals.")).toBeVisible();
  }
  // A 20-digit amount parses but the server refuses it.
  await drawer.getByLabel("Discount (RM)", { exact: true }).fill("99999999999999999999");
  if (await create.isEnabled()) {
    await create.click();
    await expect(page.getByRole("alert").or(drawer.getByRole("status")).first()).toBeVisible();
    await expect(page.getByText("Promo created.")).toHaveCount(0);
  }

  // A code with spaces is not saved.
  const spaced = `BAD ${randomUUID().slice(0, 5).toUpperCase()}`;
  await drawer.getByLabel("Code", { exact: true }).fill(spaced);
  await drawer.getByLabel("Discount (RM)", { exact: true }).fill("10.00");
  await create.click();
  await expect(page.getByText("Promo created.")).toHaveCount(0);
  await expect(drawer).toBeVisible();

  // A duplicate code is refused.
  const code = `E2E${randomUUID().slice(0, 6).toUpperCase()}`;
  await drawer.getByLabel("Code", { exact: true }).fill(code);
  await create.click();
  await expect(page.getByText("Promo created.")).toBeVisible();
  await expect(drawer).toHaveCount(0);
  await page.getByRole("button", { name: "New promo", exact: true }).click();
  const again = page.getByRole("dialog", { name: "New promo", exact: true });
  await again.getByLabel("Code", { exact: true }).fill(code);
  await again.getByLabel("Discount (RM)", { exact: true }).fill("10.00");
  await again.getByLabel("Expires (Malaysia time)", { exact: true }).fill("2031-01-01T10:00");
  await again.getByRole("button", { name: "Create promo", exact: true }).click();
  await expect(again.getByText(/already exists/)).toBeVisible();
  await expect(page.getByText("Promo created.")).toHaveCount(1);
  expectClean(guard);
});

test("ADM-08 saving a promo from a stale second tab is refused", async ({ page, context }) => {
  const guard = guardPage(page);
  await signInAdministrator(page);
  await page.goto("/dashboard/settings/promos");
  const code = `E2E${randomUUID().slice(0, 6).toUpperCase()}`;
  await page.getByRole("button", { name: "New promo", exact: true }).click();
  const creating = page.getByRole("dialog", { name: "New promo", exact: true });
  await creating.getByLabel("Code", { exact: true }).fill(code);
  await creating.getByLabel("Discount (RM)", { exact: true }).fill("10.00");
  await creating.getByLabel("Expires (Malaysia time)", { exact: true }).fill("2031-01-01T10:00");
  await creating.getByRole("button", { name: "Create promo", exact: true }).click();
  await expect(page.getByText("Promo created.")).toBeVisible();

  async function openPromo(tab: Page) {
    await tab.goto("/dashboard/settings/promos");
    await tab.getByRole("searchbox", { name: "Search promos", exact: true }).fill(code);
    await tab.getByRole("searchbox", { name: "Search promos", exact: true }).press("Enter");
    await tab.getByRole("row").filter({ hasText: code }).click();
    const drawer = tab.getByRole("dialog").first();
    await expect(drawer.getByLabel("Discount (RM)", { exact: true })).toBeVisible();
    return drawer;
  }

  const first = await openPromo(page);
  const secondTab = await context.newPage();
  const secondGuard = guardPage(secondTab);
  const second = await openPromo(secondTab);

  await first.getByLabel("Discount (RM)", { exact: true }).fill("11.00");
  await first.getByRole("button", { name: "Save promo", exact: true }).click();
  await expect(page.getByText("Promo saved.")).toBeVisible();

  await second.getByLabel("Discount (RM)", { exact: true }).fill("22.00");
  await second.getByRole("button", { name: "Save promo", exact: true }).click();
  await expect(secondTab.getByText(/changed elsewhere/)).toBeVisible();
  await expect(secondTab.getByText("Promo saved.")).toHaveCount(0);

  // The refused save did not overwrite the first value.
  await page.reload();
  await page.getByRole("searchbox", { name: "Search promos", exact: true }).fill(code);
  await page.getByRole("searchbox", { name: "Search promos", exact: true }).press("Enter");
  await expect(page.getByRole("row").filter({ hasText: code })).toContainText("11.00");
  await secondTab.close();
  expectClean(guard);
  expectClean(secondGuard);
});

test("ADM-09 Enter in the hold date field does not save retention", async ({ page }) => {
  test.fail(
    true,
    "PB-13: the preservation-hold fields sit inside the retention form, so Enter in Hold until saves retention",
  );
  const guard = guardPage(page);
  await signInAdministrator(page);
  await page.goto("/dashboard/settings/retention");
  const security = page.getByLabel("Security records (days)", { exact: true });
  const original = await security.inputValue();
  const extended = String(Number(original) + 10);
  try {
    await security.fill(extended);
    await page.getByLabel("Hold until", { exact: true }).fill("2030-01-31");
    await page.getByLabel("Hold until", { exact: true }).press("Enter");
    await expect(page.getByText("Retention settings saved.")).toHaveCount(0);
    await page.reload();
    await expect(page.getByLabel("Security records (days)", { exact: true })).toHaveValue(original);
  } finally {
    await restoreSecurityDays(page, original);
  }
  expectClean(guard);
});

async function restoreSecurityDays(page: Page, original: string) {
  await page.goto("/dashboard/settings/retention");
  const security = page.getByLabel("Security records (days)", { exact: true });
  if ((await security.inputValue()) === original) {
    return;
  }
  await security.fill(original);
  const understand = page.getByLabel(/I understand/);
  if (await understand.isVisible()) {
    await understand.check();
  }
  await page.getByRole("button", { name: "Save settings" }).click();
  await expect(page.getByText("Retention settings saved.").first()).toBeVisible();
}

test("ADM-10 a retention extension saves, a shortening needs confirmation, and the original is restored", async ({
  page,
}) => {
  const guard = guardPage(page);
  await signInAdministrator(page);
  await page.goto("/dashboard/settings/retention");
  const security = page.getByLabel("Security records (days)", { exact: true });
  const original = await security.inputValue();
  const save = page.getByRole("button", { name: "Save settings" });
  const extended = String(Number(original) + 15);
  const shortened = String(Number(original) + 5);

  try {
    await security.fill(extended);
    await expect(page.getByText(/I understand/)).toHaveCount(0);
    await save.click();
    await expect(page.getByText("Retention settings saved.")).toBeVisible();
    await page.reload();
    await expect(page.getByLabel("Security records (days)", { exact: true })).toHaveValue(extended);

    await page.getByLabel("Security records (days)", { exact: true }).fill(shortened);
    await expect(save).toBeDisabled();
    await page.getByLabel(/I understand/).check();
    await expect(save).toBeEnabled();
    await save.click();
    await expect(page.getByText("Retention settings saved.").first()).toBeVisible();
    await page.reload();
    await expect(page.getByLabel("Security records (days)", { exact: true })).toHaveValue(
      shortened,
    );
  } finally {
    await restoreSecurityDays(page, original);
  }
  await page.reload();
  await expect(page.getByLabel("Security records (days)", { exact: true })).toHaveValue(original);
  expectClean(guard);
});

test("ADM-11 the general manager opens every performance view and filter", async ({ page }) => {
  const guard = guardPage(page);
  await signInAs(page, generalManager, "GeneralManager");
  await page.goto("/dashboard/performance");
  await expect(page.getByRole("navigation", { name: "Performance views" })).toBeVisible();
  await expectNoSidewaysScroll(page);

  const views = [
    { name: "Branches", query: /view=branches/ },
    { name: "Sales & service", query: /view=sales/ },
    { name: "Workshop", query: /view=workshop/ },
  ];
  for (const view of views) {
    await page.getByRole("button", { name: view.name, exact: true }).click();
    await expect(page).toHaveURL(view.query);
    await expect(page.getByRole("button", { name: view.name, exact: true })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  }
  await page.getByRole("button", { name: "Results", exact: true }).click();
  await expect(page).not.toHaveURL(/view=/);

  await page.getByRole("button", { name: "Filters", exact: true }).click();
  await page.getByRole("radio", { name: "This month", exact: true }).check();
  await page.getByRole("button", { name: "Apply", exact: true }).click();
  await expect(page).toHaveURL(/period=thisMonth/);

  await page.getByRole("button", { name: "Filters", exact: true }).click();
  const branch = page.getByLabel("Branch", { exact: true });
  await branch.selectOption({ index: 1 });
  await page.getByRole("button", { name: "Apply", exact: true }).click();
  await expect(page).toHaveURL(/branchId=/);

  await page.getByRole("button", { name: "Reset filters", exact: true }).click();
  await expect(page).not.toHaveURL(/period=|branchId=/);
  await expectNoSidewaysScroll(page);
  expectClean(guard);
});
