import { expect, test, type Page } from "@playwright/test";
import path from "node:path";

async function signIn(page: Page) {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("aina.sofia@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh calendar", exact: true })).toBeEnabled();
}

async function holdRead(page: Page, routePattern: string) {
  let release = () => {};
  let observed = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const requested = new Promise<void>((resolve) => {
    observed = resolve;
  });
  await page.route(routePattern, async (route) => {
    const response = await route.fetch();
    observed();
    await gate;
    await route.fulfill({ response });
  });
  return { release, requested, dispose: () => page.unroute(routePattern) };
}

const tableRoute = "**/api/staff/intake/appointments?**";
const calendarRoute = "**/api/staff/intake/calendar?**";

test("view changes use their own loading feedback and refresh completes repeated fast turns", async ({
  page,
}) => {
  await signIn(page);
  const refresh = page.getByRole("button", { name: /^Refresh (calendar|worklist)$/ });
  const icon = refresh.locator("span");
  const tableRead = await holdRead(page, tableRoute);
  await page.getByRole("link", { name: "Table", exact: true }).click();
  await tableRead.requested;
  await expect(page.getByRole("navigation", { name: "Appointment views" })).toHaveAttribute(
    "aria-busy",
    "true",
  );
  await expect(refresh).toBeDisabled();
  await expect(refresh).toHaveAttribute("aria-busy", "false");
  await expect(refresh).toHaveCSS("opacity", "1");
  await expect(icon).not.toHaveAttribute("data-spinning");
  await page.screenshot({
    path: path.join(process.env.AUTH_EVIDENCE_DIR!, "refresh-view-pending.png"),
    fullPage: true,
  });
  tableRead.release();
  await expect(page.getByRole("table", { name: "Scoped intake appointments" })).toBeVisible();
  await expect(refresh).toBeEnabled();
  await expect(icon).not.toHaveAttribute("data-spinning");
  await tableRead.dispose();

  const refreshRead = await holdRead(page, tableRoute);
  await refresh.click();
  await refreshRead.requested;
  await expect(refresh).toHaveCSS("opacity", "1");
  await expect(icon).toHaveAttribute("data-spinning");
  const turn = await icon.evaluateHandle((node) => {
    const animation = node.getAnimations()[0];
    animation.pause();
    animation.currentTime = 250;
    return { node, animation };
  });
  try {
    refreshRead.release();
    await expect(refresh).toHaveAttribute("aria-busy", "false");
    expect(
      await icon.evaluate(
        (node, original) =>
          node === original.node && node.getAnimations()[0] === original.animation,
        turn,
      ),
      "the partially completed refresh turn must survive its response",
    ).toBe(true);
    await expect(refresh).toBeEnabled();

    // New requests during acknowledgement continue the same turn without blocking ready data.
    for (let attempt = 0; attempt < 2; attempt += 1) {
      const response = page.waitForResponse((result) =>
        result.url().includes("/api/staff/intake/appointments?"),
      );
      await refresh.click();
      await response;
      await expect(refresh).toBeEnabled();
      expect(
        await icon.evaluate(
          (node, original) => node.getAnimations()[0] === original.animation,
          turn,
        ),
      ).toBe(true);
    }
    await turn.evaluate(({ animation }) => animation.play());
    await expect(icon).not.toHaveAttribute("data-spinning");
  } finally {
    refreshRead.release();
    await refreshRead.dispose();
    await turn.dispose();
  }

  const calendarView = await holdRead(page, calendarRoute);
  await page.getByRole("link", { name: "Calendar", exact: true }).click();
  await calendarView.requested;
  await expect(page.getByRole("navigation", { name: "Appointment views" })).toHaveAttribute(
    "aria-busy",
    "true",
  );
  await expect(refresh).toBeDisabled();
  await expect(refresh).toHaveCSS("opacity", "1");
  await expect(icon).not.toHaveAttribute("data-spinning");
  calendarView.release();
  await expect(page.getByRole("region", { name: "Intake calendar", exact: true })).toBeVisible();
  await expect(refresh).toBeEnabled();
  await expect(icon).not.toHaveAttribute("data-spinning");
  await calendarView.dispose();
  const calendarRead = await holdRead(page, calendarRoute);
  await refresh.click();
  await calendarRead.requested;
  await expect(refresh).toHaveAttribute("aria-busy", "true");
  await expect(refresh).toHaveCSS("opacity", "1");
  // Keep the read pending beyond a full turn, then release it in the following turn.
  await expect
    .poll(() => icon.evaluate((node) => Number(node.getAnimations()[0]?.currentTime ?? 0)))
    .toBeGreaterThan(800);
  const slowTurn = await icon.evaluateHandle((node) => {
    const animation = node.getAnimations()[0];
    animation.pause();
    animation.currentTime = 950;
    return animation;
  });
  try {
    await expect(refresh).toBeDisabled();
    calendarRead.release();
    await expect(page.getByRole("region", { name: "Intake calendar", exact: true })).toBeVisible();
    await expect(refresh).toBeEnabled();
    expect(
      await icon.evaluate((node, original) => node.getAnimations()[0] === original, slowTurn),
    ).toBe(true);
    await slowTurn.evaluate((animation) => animation.play());
    await expect(icon).not.toHaveAttribute("data-spinning");
  } finally {
    calendarRead.release();
    await calendarRead.dispose();
    await slowTurn.dispose();
  }

  const response = page.waitForResponse((result) =>
    result.url().includes("/api/staff/intake/calendar?"),
  );
  await refresh.click();
  await response;
  await expect(refresh).toBeEnabled();
  await expect(icon).toHaveAttribute("data-spinning");
  await expect(icon).not.toHaveAttribute("data-spinning");
  await page.screenshot({
    path: path.join(process.env.AUTH_EVIDENCE_DIR!, "refresh-complete.png"),
    fullPage: true,
  });
});

test("reduced motion retains finite refresh feedback without rotating or delaying content", async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await signIn(page);
  const refresh = page.getByRole("button", { name: /^Refresh (calendar|worklist)$/ });
  const icon = refresh.locator("span");
  await page.getByRole("link", { name: "Table", exact: true }).click();
  await expect(page.getByRole("table", { name: "Scoped intake appointments" })).toBeVisible();
  await expect(refresh).toBeEnabled();
  await expect(icon).not.toHaveAttribute("data-spinning");
  const read = await holdRead(page, tableRoute);
  await refresh.click();
  await read.requested;
  await expect(refresh).toHaveCSS("opacity", "1");
  await expect(icon).toHaveAttribute("data-spinning");
  const turn = await icon.evaluateHandle((node) => {
    const animation = node.getAnimations()[0];
    animation.pause();
    animation.currentTime = 250;
    return animation;
  });
  try {
    expect(await icon.evaluate((node) => getComputedStyle(node).transform)).toBe("none");
    read.release();
    await expect(page.getByRole("table", { name: "Scoped intake appointments" })).toBeVisible();
    await expect(refresh).toBeEnabled();
    expect(
      await icon.evaluate((node, original) => node.getAnimations()[0] === original, turn),
    ).toBe(true);
    await turn.evaluate((animation) => animation.play());
    await expect(icon).not.toHaveAttribute("data-spinning");
  } finally {
    read.release();
    await read.dispose();
    await turn.dispose();
  }
});

test("refresh retains full opacity during drawer loading and appointment refresh", async ({
  page,
}) => {
  await signIn(page);
  await page.getByRole("link", { name: "Table", exact: true }).click();
  const workspaceRefresh = page.getByRole("button", { name: "Refresh worklist", exact: true });
  await expect(workspaceRefresh).toBeEnabled();
  const originalRefresh = await workspaceRefresh.elementHandle();
  const detailRoute = "**/api/staff/intake/appointments/*";
  const detailRead = await holdRead(page, detailRoute);
  await page
    .getByRole("link", { name: /^Open / })
    .first()
    .click();
  await detailRead.requested;
  expect(
    await originalRefresh!.evaluate((node) => ({
      disabled: node.matches(":disabled"),
      opacity: getComputedStyle(node).opacity,
    })),
  ).toEqual({ disabled: true, opacity: "1" });
  detailRead.release();
  const dialog = page.getByRole("dialog", { name: "Intake appointment", exact: true });
  const appointmentRefresh = dialog.getByRole("button", {
    name: "Refresh appointment",
    exact: true,
  });
  await expect(appointmentRefresh).toBeEnabled();
  await detailRead.dispose();

  const refreshRead = await holdRead(page, detailRoute);
  await appointmentRefresh.click();
  await refreshRead.requested;
  await expect(appointmentRefresh).toBeDisabled();
  await expect(appointmentRefresh).toHaveCSS("opacity", "1");
  refreshRead.release();
  await expect(appointmentRefresh).toBeEnabled();
  await refreshRead.dispose();
  await dialog.getByRole("button", { name: "Close dialog", exact: true }).click();
  await expect(workspaceRefresh).toBeEnabled();
});
