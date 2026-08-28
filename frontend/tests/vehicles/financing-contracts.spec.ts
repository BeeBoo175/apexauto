import { expect, test } from "@playwright/test";
import { calculateGuideOtr } from "@/lib/guide/buying-guide-math";
import { calculateFinancing, defaultCashContribution } from "@/lib/vehicles/financing";

test("flat estimate reconciles every sen through the final installment", () => {
  expect(defaultCashContribution(60000)).toBe("6000.00");
  expect(defaultCashContribution(0.05)).toBe("0.01");
  expect(calculateFinancing(60000, 6000, 7, 9000)).toEqual({
    principal: 45000,
    months: 84,
    interest: 9450,
    totalRepayments: 54450,
    monthlyInstallment: 648.21,
    finalInstallment: 648.57,
  });
  for (const years of [1, 7, 9]) {
    for (const total of [0.01, 0.49, 1, 45630.15, 60000]) {
      const estimate = calculateFinancing(total, 0, years)!;
      expect(estimate.finalInstallment).toBeGreaterThanOrEqual(0);
      expect(
        Math.round(estimate.monthlyInstallment * 100) * (estimate.months - 1) +
          Math.round(estimate.finalInstallment * 100),
      ).toBe(Math.round(estimate.totalRepayments * 100));
    }
  }
  expect(calculateFinancing(0.5, 0, 1)?.interest).toBe(0.02);
});

test("cash includes the deposit and covered prices require zero financing without surplus", () => {
  expect(calculateFinancing(60000, 6000, 7)?.principal).toBe(54000);
  expect(calculateFinancing(60000, 500, 7)?.principal).toBe(59500);
  for (const cash of [60000, 70000]) {
    expect(calculateFinancing(60000, cash, 7)).toEqual({
      principal: 0,
      months: 84,
      interest: 0,
      totalRepayments: 0,
      monthlyInstallment: 0,
      finalInstallment: 0,
    });
  }
  for (const tradeIn of [54000, 70000]) {
    const estimate = calculateFinancing(60000, 6000, 7, tradeIn)!;
    expect(estimate.principal).toBe(0);
    expect(estimate.interest).toBe(0);
    expect(estimate.totalRepayments).toBe(0);
  }
  expect(calculateFinancing(60000, 500, 7, 9000)?.principal).toBe(50500);
  expect(calculateFinancing(1, 0.33, 1, 0.34)?.principal).toBe(0.33);
});

test("invalid money, unsafe output and non-whole terms never produce a financing estimate", () => {
  for (const amount of [-1, NaN, Infinity, 1.001, Number.MAX_VALUE]) {
    expect(calculateFinancing(amount, 0, 7)).toBeNull();
    expect(calculateFinancing(60000, amount, 7)).toBeNull();
    expect(calculateFinancing(60000, 0, 7, amount)).toBeNull();
    expect(defaultCashContribution(amount)).toBe("");
  }
  for (const years of [0, 10, 1.5, NaN, Infinity]) {
    expect(calculateFinancing(60000, 6000, years)).toBeNull();
  }
  expect(calculateFinancing(80000000000000, 0, 9)).toBeNull();
  expect(calculateFinancing(70000000000000.01, 0, 7)).toBeNull();
});

test("buying guide OTR estimator applies 8% SST on processing fee and caps promos cleanly", () => {
  expect(calculateGuideOtr(80000, 280, 0, 500, 0.08)).toEqual({
    vehiclePrice: 80000,
    processingFee: 500,
    roadTax: 280,
    serviceTax: 40,
    promoDiscount: 0,
    total: 80820,
  });

  expect(calculateGuideOtr(400, 90, 1000, 500, 0.08)).toEqual({
    vehiclePrice: 400,
    processingFee: 500,
    roadTax: 90,
    serviceTax: 40,
    promoDiscount: 400,
    total: 630,
  });

  expect(calculateGuideOtr(0, 90, 0, 500, 0.08)).toBeNull();
  expect(calculateGuideOtr(-100, 90, 0, 500, 0.08)).toBeNull();
  expect(calculateGuideOtr(50000, -1, 0, 500, 0.08)).toBeNull();
});
