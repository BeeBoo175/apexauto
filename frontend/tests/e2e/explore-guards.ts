import { expect, type Page } from "@playwright/test";

export type PageGuard = {
  serverErrors: string[];
  consoleErrors: string[];
};

// Records every 5xx answer and console error on the page so a scenario can assert none happened.
export function guardPage(page: Page): PageGuard {
  const guard: PageGuard = { serverErrors: [], consoleErrors: [] };

  page.on("response", (response) => {
    if (response.status() >= 500) {
      guard.serverErrors.push(
        `${response.status()} ${response.request().method()} ${response.url()}`,
      );
    }
  });

  page.on("console", (message) => {
    if (message.type() === "error") {
      guard.consoleErrors.push(message.text());
    }
  });

  page.on("pageerror", (error) => {
    guard.consoleErrors.push(error.message);
  });

  return guard;
}

// A scenario passes only when no request failed on the server and no script error reached the console.
// Erratic scenarios that deliberately drop the network pass allowedConsole patterns for the expected failures.
export function expectClean(guard: PageGuard, allowedConsole: RegExp[] = []) {
  expect(guard.serverErrors, "server errors").toEqual([]);
  const unexpected = guard.consoleErrors.filter(
    (message) => !allowedConsole.some((pattern) => pattern.test(message)),
  );
  expect(unexpected, "console errors").toEqual([]);
}

export async function expectNoSidewaysScroll(page: Page) {
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow, "horizontal overflow in pixels").toBeLessThanOrEqual(0);
}
