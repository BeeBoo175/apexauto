import { expect, test } from "@playwright/test";
import {
  performanceHref,
  performanceLocation,
  performanceSelection,
} from "../../src/lib/performance/performance-location";

const person = "5b0f3a1e-6c1d-4f4e-9d0a-2f4f6f1a7c11";
const branch = "9a1c2d3e-4b5f-4a6b-8c7d-0e1f2a3b4c5d";

test("performance addresses omit defaults and order the query deterministically", () => {
  expect(performanceLocation("/dashboard/performance", "")?.href).toBe("/dashboard/performance");
  expect(
    performanceLocation(
      "/dashboard/performance",
      "pageSize=50&view=sales&period=thisMonth&status=active&page=2",
    )?.href,
  ).toBe("/dashboard/performance?view=sales&period=thisMonth&status=active&page=2&pageSize=50");
});

test("the open person and tab live in the path, only on a team view", () => {
  const href = `/dashboard/performance/${person}/${branch}/milestones?view=workshop`;
  expect(
    performanceLocation(`/dashboard/performance/${person}/${branch}/milestones`, "view=workshop")
      ?.href,
  ).toBe(href);
  expect(performanceHref({ view: "workshop", person: `${person}:${branch}` })).toBe(
    `/dashboard/performance/${person}/${branch}?view=workshop`,
  );
  expect(performanceHref({ view: "branches", person: `${person}:${branch}` })).toBe(
    "/dashboard/performance?view=branches",
  );
});

test("unknown sections, extra segments and old selection keys are rejected", () => {
  expect(performanceSelection([person, branch, "summary"])).toBeNull();
  expect(performanceSelection([person, branch, "milestones", "x"])).toBeNull();
  expect(performanceSelection([person])).toBeNull();
  expect(performanceLocation("/dashboard/performance", "person=a:b&section=milestones")?.href).toBe(
    "/dashboard/performance",
  );
});
