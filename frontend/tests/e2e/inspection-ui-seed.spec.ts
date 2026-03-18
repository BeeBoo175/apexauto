import { expect, test } from "@playwright/test";
import path from "node:path";

test("seeded inspections demonstrate grading, rejection, repairs and retained revisions", async ({
  page,
}, info) => {
  test.setTimeout(120_000);
  const mobile = info.project.name === "mobile";
  const prefix = mobile ? "PQA" : "VCA";
  const evidence = process.env.BROWSER_EVIDENCE_DIR!;
  await page.goto("/login");
  await page
    .getByLabel("Email", { exact: true })
    .fill(mobile ? "nur.syafiqah@example.invalid" : "siti.aminah@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
  await page.goto("/dashboard/inspection");
  await page.getByRole("button", { name: /^Filters/ }).click();
  await page.getByRole("radio", { name: "All vehicles", exact: true }).check();
  await page.getByRole("button", { name: "Apply filters", exact: true }).click();
  const table = page.getByRole("table");
  await table.getByRole("button", { name: "Sort by Vehicle", exact: true }).click();
  await expect.poll(() => new URL(page.url()).searchParams.get("direction")).toBe("asc");
  await table.getByRole("button", { name: "Sort by Vehicle", exact: true }).click();
  await expect.poll(() => new URL(page.url()).searchParams.get("direction")).toBe("desc");
  for (let number = 6296; number <= 6309; number++) {
    await expect(
      table.getByRole("button", { name: `Open ${prefix} ${number}`, exact: true }),
    ).toBeVisible();
  }

  const examples = [
    {
      plate: 6296,
      grade: "Grade B",
      status: "Inspection underway",
      revision: "Revision 1 · Draft",
    },
    { plate: 6297, grade: "Grade A", status: "Ready for sale", revision: "Revision 1 · Signed" },
    {
      plate: 6302,
      grade: "Grade C",
      status: "Budget approval needed",
      revision: "Revision 1 · Signed",
    },
    { plate: 6304, grade: "Rejected", status: "Rejected", revision: "Revision 1 · Signed" },
    { plate: 6301, grade: "Grade B", status: "Ready for sale", revision: "Revision 1 · Signed" },
    { plate: 6307, grade: "Grade A", status: "Ready for sale", revision: "Revision 2 · Signed" },
  ];
  for (const example of examples) {
    await table
      .getByRole("button", { name: `Open ${prefix} ${example.plate}`, exact: true })
      .click();
    const dialog = page.getByRole("dialog", { name: `${prefix} ${example.plate}`, exact: true });
    const summary = dialog.getByRole("region", { name: "Vehicle and confirmed assessment" });
    await expect(summary.getByText(example.status, { exact: true })).toBeVisible();
    await expect(summary.getByText(example.revision, { exact: true })).toBeVisible();
    await dialog.getByRole("button", { name: /^Vehicle & (inspection|request) details$/ }).click();
    const details = page.getByRole("dialog", { name: /^Vehicle & (inspection|request) details$/ });
    await expect(details.getByText(example.grade, { exact: true })).toBeVisible();
    await details.getByRole("button", { name: "Close dialog", exact: true }).click();
    if (example.plate === 6307) {
      await dialog
        .getByRole("navigation", { name: "Workshop sections" })
        .getByRole("button", { name: "History", exact: true })
        .click();
      for (const [revision, grade] of [
        [1, "Grade B"],
        [2, "Grade A"],
      ] as const) {
        const event = dialog.getByRole("listitem").filter({
          has: page.getByText(`Inspection revision ${revision} signed`, { exact: true }),
        });
        await event.getByText("View details", { exact: true }).click();
        await expect(event.getByText(new RegExp(`^${grade} · Original estimate`))).toBeVisible();
        await expect(
          event.getByRole("link", { name: `Open signed revision ${revision}`, exact: true }),
        ).toBeVisible();
      }
      await page.screenshot({ path: path.join(evidence, "seeded-reinspection.png") });
    }
    await dialog.getByRole("button", { name: "Close dialog", exact: true }).click();
    await expect(dialog).toHaveCount(0);
  }

  const expectedRepairs = [
    { plate: 6299, status: "Repairs to complete" },
    { plate: 6300, status: "Repairs to complete" },
    { plate: 6301, status: "Ready for sale" },
    { plate: 6306, status: "Budget approval needed" },
    { plate: 6308, status: "Budget approval needed" },
    { plate: 6309, status: "Repairs to complete" },
  ];
  for (const repair of expectedRepairs) {
    const row = table.getByRole("row").filter({ hasText: `${prefix} ${repair.plate}` });
    await expect(row).toHaveCount(1);
    await expect(row.getByText(repair.status, { exact: true })).toBeVisible();
  }
  await page.screenshot({ path: path.join(evidence, "seeded-repair-states.png") });
  for (const plate of [6299, 6300, 6309]) {
    await table.getByRole("button", { name: `Open ${prefix} ${plate}`, exact: true }).click();
    const dialog = page.getByRole("dialog", { name: `${prefix} ${plate}`, exact: true });
    if (plate === 6300) {
      await expect(
        dialog
          .getByRole("region", { name: "Vehicle and confirmed assessment" })
          .getByText("MYR 0.00", { exact: true }),
      ).toBeVisible();
      await expect(dialog.getByRole("button", { name: "Approve budget", exact: true })).toHaveCount(
        0,
      );
    } else {
      const approval =
        plate === 6309
          ? "Earlier approval covers the same work within its approved budget."
          : "Budget approved for this request.";
      await expect(dialog.getByText(approval, { exact: true })).toBeVisible();
    }
    await dialog.getByRole("button", { name: "Close dialog", exact: true }).click();
  }
  for (const plate of [6306, 6308]) {
    await table.getByRole("button", { name: `Open ${prefix} ${plate}`, exact: true }).click();
    const dialog = page.getByRole("dialog", { name: `${prefix} ${plate}`, exact: true });
    await dialog
      .getByRole("navigation", { name: "Workshop sections" })
      .getByRole("button", { name: "History", exact: true })
      .click();
    const original = dialog
      .getByRole("listitem")
      .filter({
        has: page.getByText("Repair budget requested", { exact: true }),
      })
      .filter({ hasText: "Work version 1 ·" });
    await original.getByText("View details", { exact: true }).click();
    await original.getByRole("link", { name: "Open repair request", exact: true }).click();
    const retained = dialog.getByRole("region", { name: "Selected retained repair request" });
    await expect(
      retained.getByRole("heading", { name: "Retained repair request", exact: true }),
    ).toBeVisible();
    await expect(retained.getByText(/This request is historical/)).toBeVisible();
    await expect(dialog.getByRole("button", { name: "Approve budget", exact: true })).toHaveCount(
      0,
    );
    if (plate === 6308) {
      await expect(
        retained.getByText(/Recorded work completed and the affected component reinspected/),
      ).toBeVisible();
      await page.screenshot({ path: path.join(evidence, "seeded-retained-repair.png") });
    }
    await dialog.getByRole("button", { name: "Close dialog", exact: true }).click();
  }

  await table.getByRole("button", { name: `Open ${prefix} 6296`, exact: true }).click();
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
