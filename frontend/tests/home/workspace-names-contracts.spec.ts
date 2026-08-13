import { expect, test } from "@playwright/test";
import { staffPageTitles, staffWorkspaceLinks, workspaceMetadata } from "@/lib/staff/navigation";

test("every workspace tab is named after the address segment it opens", () => {
  for (const link of staffWorkspaceLinks) {
    const segment = link.href.split("/").pop()!;
    const words = link.label.toLowerCase().replaceAll(" ", "-");
    expect(words, `${link.label} at ${link.href}`).toBe(segment);
  }
});

test("the browser tab title is the sidebar label", () => {
  for (const link of staffWorkspaceLinks) {
    expect(workspaceMetadata(link.href).title).toBe(link.label);
  }
  expect(workspaceMetadata("/dashboard/account").title).toBe(staffPageTitles["/dashboard/account"]);
});
