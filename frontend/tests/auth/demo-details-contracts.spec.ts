import { test, expect } from "@playwright/test";
import { demoDetails, demoNric } from "@/lib/demo/demo-details";

function sequence(values: number[]) {
  let index = 0;
  return () => values[index++ % values.length];
}

test("generated NRICs have twelve digits and an impossible birth month", () => {
  for (let seed = 0; seed < 200; seed++) {
    const nric = demoNric(() => Math.random());
    expect(nric).toMatch(/^[0-9]{12}$/);
    expect(["00", "13"]).toContain(nric.slice(2, 4));
  }
});

test("both impossible months are produced", () => {
  expect(demoNric(sequence([0, 0.2, 0.2, 0.2, 0.2])).slice(2, 4)).toBe("00");
  expect(demoNric(sequence([0, 0.9, 0.2, 0.2, 0.2])).slice(2, 4)).toBe("13");
});

test("demo details fill only the four profile fields within backend limits", () => {
  const details = demoDetails(sequence([0.1, 0.4, 0.7, 0.9]));
  expect(Object.keys(details).sort()).toEqual([
    "legalName",
    "mobile",
    "nric",
    "residentialAddress",
  ]);
  expect(details.nric).toMatch(/^[0-9]{12}$/);
  expect(details.mobile.length).toBeLessThanOrEqual(30);
  expect(details.legalName.length).toBeGreaterThan(0);
  expect(details.residentialAddress.length).toBeGreaterThan(0);
});

test("an injected random source makes the result repeatable", () => {
  expect(demoDetails(sequence([0.3, 0.6]))).toEqual(demoDetails(sequence([0.3, 0.6])));
});
