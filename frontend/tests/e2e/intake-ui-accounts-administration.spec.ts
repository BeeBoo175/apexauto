import { expect, test } from "@playwright/test";
import { apiLogin, privateHeaders, signIn, signInAs } from "./staff-session";

const administrator = "muhammad.hafiz@example.invalid";
const colleague = "aina.sofia@example.invalid";
const seededPassword = "Password123!";

// One desktop journey for the account administration workflow: an administrator invites staff, then
// suspends a signed-in colleague, which cuts the session and sign-in, and lifts it again.
test("administrator invites staff and suspends then restores a signed-in colleague", async ({
  page,
  browser,
}, info) => {
  test.skip(info.project.name === "mobile", "Administration is a desktop workflow.");
  test.setTimeout(120_000);
  await signInAs(page, administrator, "Administrator");

  // Invite: the new person appears under Invited with the invitation actions.
  const invited = `invited-${crypto.randomUUID().slice(0, 8)}@example.invalid`;
  await page.getByRole("button", { name: "Invite staff", exact: true }).click();
  const invite = page.getByRole("dialog", { name: "Invite staff", exact: true });
  await invite.getByLabel("Full name", { exact: true }).fill("Journey Invitee");
  await invite.getByLabel("Email", { exact: true }).fill(invited);
  await invite.getByLabel("Work mobile", { exact: true }).fill("+60123456789");
  await invite.getByLabel("Role", { exact: true }).selectOption({ label: "Administrator" });
  await invite.getByRole("button", { name: "Send invitation", exact: true }).click();
  await expect(invite).toHaveCount(0);
  await page.getByRole("searchbox", { name: "Search staff", exact: true }).fill(invited);
  // No Enter and no Search button: pausing in the box applies the search by itself.
  const invitedRow = page.getByRole("row").filter({ hasText: invited });
  await expect(invitedRow).toBeVisible();
  await expect(invitedRow).toContainText("Invited");

  // The colleague is signed in on their own browser context.
  const colleagueContext = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  await apiLogin(colleagueContext, colleague);
  const session = async () =>
    colleagueContext.request.get("/api/staff/intake/appointments?pageSize=10", {
      headers: await privateHeaders(colleagueContext),
    });
  expect((await session()).status()).toBe(200);

  // Suspend: sign-in stops at once and the existing session ends.
  await page.getByRole("searchbox", { name: "Search staff", exact: true }).fill("Aina Sofia");
  await page.getByRole("searchbox", { name: "Search staff", exact: true }).press("Enter");
  await page.getByRole("row").filter({ hasText: "Aina Sofia" }).first().click();
  const drawer = page.getByRole("dialog").first();
  await drawer.getByRole("button", { name: "Suspend account", exact: true }).click();
  const suspend = page.getByRole("dialog", { name: "Suspend account", exact: true });
  await suspend.getByLabel("Reason", { exact: true }).fill("Journey suspension");
  await suspend.getByRole("button", { name: "Suspend account", exact: true }).click();
  await expect(suspend).toHaveCount(0);
  await expect(drawer.getByRole("button", { name: "Lift suspension", exact: true })).toBeVisible();
  await expect.poll(async () => (await session()).status()).not.toBe(200);
  const blocked = await colleagueContext.request.post("/api/auth/login", {
    headers: { Origin: process.env.APP_BASE_URL! },
    data: { loginKey: crypto.randomUUID(), email: colleague, password: seededPassword },
  });
  expect(blocked.status()).not.toBe(200);

  // Lift: the colleague can sign in again.
  await drawer.getByRole("button", { name: "Lift suspension", exact: true }).click();
  const lift = page.getByRole("dialog", { name: "Lift suspension", exact: true });
  await lift.getByRole("button", { name: "Lift suspension", exact: true }).click();
  await expect(lift).toHaveCount(0);
  await expect(drawer.getByRole("button", { name: "Suspend account", exact: true })).toBeVisible();
  await apiLogin(colleagueContext, colleague);
  expect((await session()).status()).toBe(200);
  await colleagueContext.close();

  // After the lift the colleague also signs in through the form.
  const signedOut = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const other = await signedOut.newPage();
  await signIn(other, colleague);
  await expect(other).toHaveURL(/\/dashboard/);
  await signedOut.close();
});
