import { isMyr, parseMyr } from "@/lib/currency-input";

export interface FinancingEstimate {
  principal: number;
  months: number;
  interest: number;
  totalRepayments: number;
  monthlyInstallment: number;
  finalInstallment: number;
}

export function minorUnits(amount: number): bigint | null {
  if (!isMyr(amount)) {
    return null;
  }
  return BigInt(Math.round(amount * 100));
}

export function roundedDivision(amount: bigint, divisor: bigint): bigint {
  const quotient = amount / divisor;
  return quotient + ((amount % divisor) * BigInt(2) >= divisor ? BigInt(1) : BigInt(0));
}

export function exactMyr(sen: bigint): number | null {
  if (sen < BigInt(0)) {
    return null;
  }
  const whole = sen / BigInt(100);
  const fraction = (sen % BigInt(100)).toString().padStart(2, "0");
  return parseMyr(`${whole}.${fraction}`);
}

export function defaultCashContribution(total: number): string {
  const totalSen = minorUnits(total);
  if (totalSen === null) {
    return "";
  }
  return exactMyr(roundedDivision(totalSen, BigInt(10)))?.toFixed(2) ?? "";
}

// This local portfolio estimate mirrors PurchasePricing; the backend owns agreed terms.
export function calculateFinancing(
  total: number,
  cashContribution: number,
  termYears: number,
  indicativeTradeIn = 0,
): FinancingEstimate | null {
  const totalSen = minorUnits(total);
  const cashSen = minorUnits(cashContribution);
  const tradeInSen = minorUnits(indicativeTradeIn);
  if (
    totalSen === null ||
    cashSen === null ||
    tradeInSen === null ||
    !Number.isInteger(termYears) ||
    termYears < 1 ||
    termYears > 9
  ) {
    return null;
  }
  // Any paid reservation deposit is already included in cash, never deducted again.
  const remaining = totalSen - cashSen - tradeInSen;
  const principal = remaining > BigInt(0) ? remaining : BigInt(0);
  const interest = roundedDivision(principal * BigInt(3) * BigInt(termYears), BigInt(100));
  const repayments = principal + interest;
  if (repayments > BigInt(Number.MAX_SAFE_INTEGER)) {
    return null;
  }
  const months = termYears * 12;
  const monthsCount = BigInt(months);
  let installment = roundedDivision(repayments, monthsCount);
  // Match canonical tiny-estimate handling so the final payment stays nonnegative.
  if (installment * (monthsCount - BigInt(1)) > repayments) {
    installment = repayments / monthsCount;
  }
  const principalMyr = exactMyr(principal);
  const interestMyr = exactMyr(interest);
  const repaymentsMyr = exactMyr(repayments);
  const installmentMyr = exactMyr(installment);
  const finalMyr = exactMyr(repayments - installment * (monthsCount - BigInt(1)));
  if (
    principalMyr === null ||
    interestMyr === null ||
    repaymentsMyr === null ||
    installmentMyr === null ||
    finalMyr === null
  ) {
    return null;
  }
  return {
    principal: principalMyr,
    months,
    interest: interestMyr,
    totalRepayments: repaymentsMyr,
    monthlyInstallment: installmentMyr,
    finalInstallment: finalMyr,
  };
}
