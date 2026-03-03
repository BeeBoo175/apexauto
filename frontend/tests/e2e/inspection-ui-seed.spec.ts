import { expect, test } from "@playwright/test";
import path from "node:path";

test("seeded inspections demonstrate grading, rejection, repairs and retained revisions", async ({
  page,
}, info) => {
  test.setTimeout(120_000);
  const mobile = info.project.name === "mobile";
  const prefix = mobile ? "PQA" : "VCA";
  const technician = mobile ? "mohd.firdaus@example.invalid" : "azlan.rahim@example.invalid";
  const evidence = process.env.BROWSER_EVIDENCE_DIR!;
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(technician);
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/inspection$/);
  const table = page.getByRole("table");
  for (let number = 6296; number <= 6309; number++) {
    await expect(
      table.getByRole("button", { name: `Open ${prefix} ${number}`, exact: true }),
    ).toBeVisible();
  }

  const examples = [
    { plate: 6296, grade: "Grade B", status: "Pending Inspection", revision: "1 · Draft" },
    { plate: 6297, grade: "Grade A", status: "Available", revision: "1 · Signed" },
    { plate: 6302, grade: "Grade C", status: "Pending Inspection", revision: "1 · Signed" },
    { plate: 6304, grade: "Rejected", status: "Rejected", revision: "1 · Signed" },
    { plate: 6301, grade: "Grade B", status: "Available", revision: "1 · Signed" },
    { plate: 6307, grade: "Grade A", status: "Available", revision: "2 · Signed" },
  ];
  for (const example of examples) {
    await table
      .getByRole("button", { name: `Open ${prefix} ${example.plate}`, exact: true })
      .click();
    const dialog = page.getByRole("dialog", { name: "Inspection", exact: true });
    const summary = dialog.locator('section[aria-label="Vehicle and confirmed assessment"]');
    await expect(summary).toContainText(example.grade);
    await expect(summary.getByText(example.status, { exact: true })).toBeVisible();
    await expect(summary.getByText(example.revision, { exact: true })).toBeVisible();
    if (example.plate === 6307) {
      await expect(
        dialog.getByRole("link", { name: "Revision 1 · Grade B", exact: true }),
      ).toBeVisible();
      await expect(
        dialog.getByRole("link", { name: "Revision 2 · Grade A", exact: true }),
      ).toBeVisible();
      await page.screenshot({ path: path.join(evidence, "seeded-reinspection.png") });
    }
    await dialog.getByRole("button", { name: "Close dialog", exact: true }).click();
    await expect(dialog).toHaveCount(0);
  }

  await page.goto("/dashboard/inspection/repairs");
  const repairs = page.getByRole("table");
  const expectedRepairs = [
    { plate: 6299, status: "Approved" },
    { plate: 6300, status: "Approval Not Required" },
    { plate: 6301, status: "Completed" },
    { plate: 6306, status: "Pending Approval" },
    { plate: 6306, status: "Superseded" },
    { plate: 6308, status: "Pending Approval" },
    { plate: 6308, status: "Superseded" },
    { plate: 6309, status: "Approved" },
  ];
  for (const repair of expectedRepairs) {
    const row = repairs.getByRole("row").filter({ hasText: `${prefix} ${repair.plate}` });
    await expect(row.getByText(repair.status, { exact: true })).toBeVisible();
  }
  await page.screenshot({ path: path.join(evidence, "seeded-repair-states.png") });
  const historical = repairs
    .getByRole("row")
    .filter({ hasText: `${prefix} 6308` })
    .filter({ hasText: "Superseded" });
  await historical.getByRole("button", { name: `Open ${prefix} 6308`, exact: true }).click();
  const retained = page.getByRole("dialog", { name: "Repair request", exact: true });
  await expect(retained.getByText("Retained repair request", { exact: true })).toBeVisible();
  await expect(retained.getByText(/This request is historical/)).toBeVisible();
  await expect(
    retained.getByText(/Recorded work completed and the affected component reinspected/),
  ).toBeVisible();
  await page.screenshot({ path: path.join(evidence, "seeded-retained-repair.png") });

  await page.context().clearCookies();
  await page.goto("/login");
  await page
    .getByLabel("Email", { exact: true })
    .fill(mobile ? "nur.syafiqah@example.invalid" : "siti.aminah@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
  await page.goto("/dashboard/inspection");
  await page.getByRole("button", { name: `Open ${prefix} 6296`, exact: true }).click();
  await page.getByRole("button", { name: "Reassign technician", exact: true }).click();
  const reassignment = page.getByRole("dialog", {
    name: "Reassign unsigned inspection",
    exact: true,
  });
  await reassignment.getByRole("button", { name: "Load technicians", exact: true }).click();
  const choices = reassignment.getByLabel("Workshop technician", { exact: true });
  await expect(choices.locator("option")).toHaveCount(3);
  await expect(choices).toContainText(
    mobile ? "amirul.izzat@example.invalid" : "farid.hakim@example.invalid",
  );
  await expect(reassignment.getByLabel("Assignment reason", { exact: true })).toBeVisible();
  await page.screenshot({ path: path.join(evidence, "seeded-reassignment-choices.png") });
});
