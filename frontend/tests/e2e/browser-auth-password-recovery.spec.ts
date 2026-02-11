import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";
import {
  assertInitialRecoveryPage,
  assertRecoveryIngress,
  capturedResetLink,
  expectPreservedSession,
  installSession,
  recoveredPassword,
  recoveryFixture,
  rememberSession,
  signInRecovered,
  upstreamCount,
} from "./password-recovery-helpers";

const origin = process.env.APP_BASE_URL!;
const resetRoute = "/api/auth/password-reset";
const requestRoute = "/api/auth/password-recovery";

if (process.env.AUTH_RECOVERY_SCENARIO) {
  test("recovery request: generic outcomes preserve an uncertain email for deliberate retry", async ({
    page,
    request,
  }, testInfo) => {
    const owner = recoveryFixture();
    await assertInitialRecoveryPage(page, "/forgot-password", "Forgot password");

    if (testInfo.project.name === "desktop-auth") {
      await assertRecoveryIngress(request);
    }

    await page.getByLabel("Email", { exact: true }).fill(owner.Email);
    const bodies: string[] = [];
    const before = upstreamCount(requestRoute);
    await page.route(`**${requestRoute}`, async (route) => {
      bodies.push(route.request().postData()!);
      const response = await route.fetch();
      expect(response.status()).toBe(200);
      await route.abort();
    });
    await page.getByRole("button", { name: "Send reset link", exact: true }).click();
    await expect(
      page.getByRole("button", { name: "Retry same request", exact: true }),
    ).toBeVisible();
    await expect(page.getByLabel("Email", { exact: true })).toHaveValue(owner.Email);
    await expect(page.getByLabel("Email", { exact: true })).toBeDisabled();
    await expect(
      page.getByRole("status").filter({ hasText: "The result is unconfirmed" }),
    ).toBeVisible();
    expect(upstreamCount(requestRoute)).toBe(before + 1);

    await page.unroute(`**${requestRoute}`);
    page.on("request", (attempt) => {
      if (attempt.url().endsWith(requestRoute)) {
        bodies.push(attempt.postData()!);
      }
    });
    await page.getByRole("button", { name: "Retry same request", exact: true }).click();
    const accepted = page
      .getByRole("status")
      .filter({ hasText: "If this account can receive" })
      .last();
    await expect(accepted).toBeVisible();
    const knownMessage = await accepted.textContent();
    expect(bodies[1] === bodies[0], "Recovery retry preserves the original request").toBe(true);
    expect(upstreamCount(requestRoute)).toBe(before + 2);
    const link = await capturedResetLink(request, owner.Email);
    expect(/^#token=[a-f0-9.]+$/.test(link.hash), "Mail keeps the reset token in a fragment").toBe(
      true,
    );

    await page.getByRole("button", { name: "Send reset link", exact: true }).click();
    await expect(accepted).toHaveText(knownMessage!);
    await expect.poll(() => upstreamCount(requestRoute)).toBe(before + 3);
    await expect(page.getByRole("button", { name: "Send reset link", exact: true })).toBeEnabled();
    await page.getByLabel("Email", { exact: true }).fill(`unknown.${randomUUID()}@example.invalid`);
    await page.getByRole("button", { name: "Send reset link", exact: true }).click();
    await expect(accepted).toHaveText(knownMessage!);
    await expect.poll(() => upstreamCount(requestRoute)).toBe(before + 4);
    await expect(page.getByRole("button", { name: "Send reset link", exact: true })).toBeEnabled();
    expect(upstreamCount(resetRoute)).toBe(0);
  });

  test("recovery reset: policy denial and lost committed receipt recover without a new operation", async ({
    page,
    context,
    browser,
    request,
  }) => {
    const owner = recoveryFixture();
    const link = await capturedResetLink(request, owner.Email);
    await installSession(context, owner.OldSessions[0]);
    const unrelated = await browser.newContext({ baseURL: origin });
    await installSession(unrelated, owner.UnrelatedSession!);
    const unrelatedPage = await unrelated.newPage();
    await unrelatedPage.goto("/account");
    await expect(
      unrelatedPage.getByRole("button", { name: "Refresh account", exact: true }),
    ).toBeVisible();

    try {
      await assertInitialRecoveryPage(page, link.href, "Reset password");
      await expect(page.getByRole("button", { name: "Reset password", exact: true })).toBeEnabled();
      const before = upstreamCount(resetRoute);
      await page.getByLabel("New password", { exact: true }).fill("short");
      await page.getByRole("button", { name: "Reset password", exact: true }).click();
      await expect(page.getByRole("alert").filter({ hasText: /password/i })).toBeVisible();
      await expect(page.getByLabel("New password", { exact: true })).toBeEditable();
      expect(
        (await page.getByLabel("New password", { exact: true }).inputValue()) === "short",
        "A denied password remains editable",
      ).toBe(true);
      expect(upstreamCount(resetRoute)).toBe(before + 1);
      await expectPreservedSession(context, owner.OldSessions[0]);

      await page.getByLabel("New password", { exact: true }).fill(recoveredPassword);
      const attempts: string[] = [];
      await page.route(`**${resetRoute}`, async (route) => {
        attempts.push(route.request().postData()!);
        const committed = await route.fetch();
        expect(committed.status()).toBe(200);
        await route.abort();
      });
      await page.getByRole("button", { name: "Reset password", exact: true }).click();
      const retry = page.getByRole("button", { name: "Retry same request", exact: true });
      await expect(retry).toBeVisible();
      expect(
        (await page.getByLabel("New password", { exact: true }).inputValue()) === recoveredPassword,
        "An uncertain reset preserves its password draft",
      ).toBe(true);
      await expect(page.getByLabel("New password", { exact: true })).toBeDisabled();
      expect(
        page.url() === `${origin}${link.pathname}${link.hash}`,
        "An uncertain reset retains its original link",
      ).toBe(true);
      await expectPreservedSession(context, owner.OldSessions[0]);
      expect(upstreamCount(resetRoute)).toBe(before + 2);

      await page.unroute(`**${resetRoute}`);
      page.on("request", (attempt) => {
        if (attempt.url().endsWith(resetRoute)) {
          attempts.push(attempt.postData()!);
        }
      });
      await retry.click();
      await expect
        .poll(() => page.url() === `${origin}/login`, {
          message: "A confirmed reset returns to ordinary sign-in",
        })
        .toBe(true);
      expect(
        attempts[1] === attempts[0],
        "Reset retry preserves the exact token and password",
      ).toBe(true);
      expect(upstreamCount(resetRoute)).toBe(before + 3);
      await expectPreservedSession(context, owner.OldSessions[0]);
      await expectPreservedSession(unrelated, owner.UnrelatedSession!);
      const unrelatedStatus = await unrelatedPage.evaluate(
        async () => (await fetch("/api/account", { cache: "no-store" })).status,
      );
      expect(unrelatedStatus).toBe(200);
      await signInRecovered(page, owner.Email);
      await expect(page.getByText(owner.LegalName, { exact: true })).toBeVisible();
      rememberSession(
        (await context.cookies()).find((cookie) => cookie.name === "apexauto_token")!.value,
      );
    } finally {
      await unrelated.close();
    }
  });

  test("recovery fragment: changing links clears the old draft and ignores a completed stale response", async ({
    page,
    request,
  }) => {
    const owner = recoveryFixture();
    const link = await capturedResetLink(request, owner.Email);
    await assertInitialRecoveryPage(page, "/reset-password", "Reset password");
    await expect(page.getByRole("button", { name: "Reset password", exact: true })).toBeDisabled();
    const invalid = `${randomUUID().replaceAll("-", "")}.${"0".repeat(64)}`;
    await page.evaluate((token) => {
      window.location.hash = `token=${token}`;
    }, invalid);
    await expect(page.getByRole("button", { name: "Reset password", exact: true })).toBeEnabled();
    expect(upstreamCount(resetRoute)).toBe(0);
    await page.getByLabel("New password", { exact: true }).fill("Abandoned123!");
    await page.getByRole("button", { name: "Reset password", exact: true }).click();
    await expect(
      page.getByRole("alert").filter({ hasText: "The reset link is invalid or expired." }),
    ).toBeVisible();
    expect(upstreamCount(resetRoute)).toBe(1);

    await page.evaluate(
      (hash) => {
        window.location.hash = hash;
      },
      `token=${invalid.slice(0, -1)}1`,
    );
    await expect
      .poll(
        async () => (await page.getByLabel("New password", { exact: true }).inputValue()) === "",
      )
      .toBe(true);
    await page.getByLabel("New password", { exact: true }).fill("Abandoned123!");
    await page.evaluate(() => {
      const state: { release?: () => void } = {};
      (window as unknown as { recoveryResponse: typeof state }).recoveryResponse = state;
      const originalFetch = window.fetch.bind(window);
      window.fetch = async (input, options) => {
        const response = await originalFetch(input, options);

        if (input !== "/api/auth/password-reset") {
          return response;
        }

        const body = await response.text();
        return new Promise<Response>((resolve) => {
          state.release = () => resolve(new Response(body, { status: response.status }));
        });
      };
    });
    await page.getByRole("button", { name: "Reset password", exact: true }).click();
    await expect
      .poll(() =>
        page.evaluate(() =>
          Boolean(
            (window as unknown as { recoveryResponse: { release?: () => void } }).recoveryResponse
              .release,
          ),
        ),
      )
      .toBe(true);
    expect(upstreamCount(resetRoute)).toBe(2);
    await page.evaluate((hash) => {
      window.location.hash = hash;
    }, link.hash);
    await expect
      .poll(
        async () => (await page.getByLabel("New password", { exact: true }).inputValue()) === "",
        { message: "Changing links clears the previous password draft" },
      )
      .toBe(true);
    await expect(page.getByRole("button", { name: "Reset password", exact: true })).toBeEnabled();
    await page.evaluate(async () => {
      (
        window as unknown as { recoveryResponse: { release: () => void } }
      ).recoveryResponse.release();
      await new Promise<void>((resolve) =>
        requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
      );
    });
    await expect(
      page.getByText("The reset link is invalid or expired. Request a new link.", { exact: true }),
    ).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Retry same request", exact: true })).toHaveCount(
      0,
    );
    expect(upstreamCount(resetRoute)).toBe(2);
    await page.getByLabel("New password", { exact: true }).fill("FreshDraft123!");
    expect(
      (await page.getByLabel("New password", { exact: true }).inputValue()) === "FreshDraft123!",
      "The replacement link accepts its own draft",
    ).toBe(true);
  });
}
