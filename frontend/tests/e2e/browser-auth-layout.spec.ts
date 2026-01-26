import { writeFileSync } from "node:fs";
import path from "node:path";
import { test, expect } from "@playwright/test";

test("auth presentation keeps compact controls and stable photography", async ({
  page,
  browser,
}, testInfo) => {
  const evidence = process.env.AUTH_EVIDENCE_DIR!;
  const observations = [];
  const originalViewport = page.viewportSize()!;
  const viewports = [originalViewport, { width: 320, height: 740 }];

  if (testInfo.project.name === "desktop-auth") {
    viewports.push({ width: 1440, height: 640 }, { width: 1024, height: 768 });
  }

  for (const viewport of viewports) {
    await page.setViewportSize(viewport);
    const photoBoxes = [];

    for (const route of [
      "login",
      "register",
      "forgot-password",
      "reset-password",
      "verify-email",
    ]) {
      await page.goto(`/${route}`);
      await expect(page.locator("#auth-title")).toBeVisible();
      const photograph = page.locator("section[aria-labelledby='auth-title'] img");
      await expect(photograph).toHaveJSProperty("complete", true);
      const controls = await page
        .locator("main input:visible, main button:visible")
        .evaluateAll((elements) =>
          elements.map((element) => ({
            label: element.getAttribute("aria-label") || element.textContent || element.id,
            height: element.getBoundingClientRect().height,
            fits:
              element.scrollWidth <= element.clientWidth &&
              element.scrollHeight <= element.clientHeight,
          })),
        );
      for (const control of controls) {
        expect(control.height, `${route}: ${control.label}`).toBe(44);
        expect(control.fits, `${route}: ${control.label} clips`).toBe(true);
      }
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
        true,
      );
      const photo = await photograph.evaluate((image: HTMLImageElement) => {
        const box = image.getBoundingClientRect();
        return {
          width: box.width,
          height: box.height,
          source: image.currentSrc,
          fit: getComputedStyle(image).objectFit,
        };
      });
      photoBoxes.push(photo);
      observations.push({ viewport, route, controls, photo });

      if (viewport === originalViewport) {
        await page.screenshot({
          path: path.join(evidence, `${testInfo.project.name}-${route}-layout.png`),
          fullPage: true,
        });
        if (route === "login") {
          const response = await page.request.get(photo.source);
          expect(response.ok()).toBe(true);
          writeFileSync(
            path.join(evidence, `${testInfo.project.name}-selected-image.bin`),
            await response.body(),
          );
        }
      }

      if (route === "login") {
        const create = await page
          .getByRole("link", { name: "Create an account", exact: true })
          .boundingBox();
        const forgot = await page
          .getByRole("link", { name: "Forgot password?", exact: true })
          .boundingBox();
        expect(create!.y).toBe(forgot!.y);
        expect(create!.x + create!.width).toBeLessThanOrEqual(forgot!.x);
        await page.getByText("Demo credentials", { exact: true }).click();
        for (const helper of await page
          .getByRole("group", { name: "Demo accounts", exact: true })
          .getByRole("button")
          .all()) {
          expect((await helper.boundingBox())!.height).toBe(44);
          expect(
            await helper.evaluate(
              (button) =>
                button.scrollWidth <= button.clientWidth &&
                button.scrollHeight <= button.clientHeight,
            ),
          ).toBe(true);
        }
      }

      if (route === "register") {
        await expect(page.getByLabel("Legal name", { exact: true })).toBeEnabled();
        const initialForm = await page.locator("form").boundingBox();
        // Controlled content growth isolates photo geometry from backend validation behavior.
        await page.locator("form").evaluate((form) => {
          const status = document.createElement("p");
          status.textContent = "Controlled expanded status content. ".repeat(40);
          form.append(status);
        });
        const expanded = await photograph.boundingBox();
        const expandedForm = await page.locator("form").boundingBox();
        expect(expandedForm!.height).toBeGreaterThan(initialForm!.height + 200);
        expect(expanded!.width).toBe(photo.width);
        expect(expanded!.height).toBe(photo.height);
        if (viewport.width > 820) {
          await page.evaluate(() => scrollTo(0, 200));
          expect((await photograph.boundingBox())!.y).toBe(72);
        }
      }
    }

    expect(new Set(photoBoxes.map((box) => `${box.width}x${box.height}`)).size).toBe(1);
    if (viewport.width > 820) {
      expect(photoBoxes[0].width).toBe(viewport.width / 2);
      expect(photoBoxes[0].height).toBe(viewport.height - 72);
    }
  }

  if (testInfo.project.name === "desktop-auth") {
    const highDpi = await browser.newContext({ viewport: originalViewport, deviceScaleFactor: 2 });
    const highDpiPage = await highDpi.newPage();
    await highDpiPage.goto(new URL("/login", page.url()).href);
    const image = highDpiPage.locator("section[aria-labelledby='auth-title'] img");
    await expect(image).toHaveJSProperty("complete", true);
    const source = await image.evaluate((element: HTMLImageElement) => element.currentSrc);
    const response = await highDpiPage.request.get(source);
    expect(response.ok()).toBe(true);
    writeFileSync(path.join(evidence, "desktop-2x-selected-image.bin"), await response.body());
    writeFileSync(
      path.join(evidence, "desktop-2x-source.json"),
      JSON.stringify({ source, devicePixelRatio: 2 }),
    );
    await highDpiPage.screenshot({
      path: path.join(evidence, "desktop-2x-login.png"),
      fullPage: true,
    });
    await highDpi.close();
  }

  writeFileSync(
    path.join(evidence, `${testInfo.project.name}-layout.json`),
    JSON.stringify(observations, null, 2),
  );
});
