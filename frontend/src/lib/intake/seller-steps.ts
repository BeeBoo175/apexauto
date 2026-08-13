// The seller journey's stages in order, and the address token that names each one (the first has none).
export const sellerStepTokens = ["", "valuation", "photos", "appointment"] as const;

export const SELLER_STEP_KEY = "step";

export function sellerStepFromToken(token: string | null) {
  const index = sellerStepTokens.findIndex((candidate) => candidate === (token ?? ""));
  return index < 0 ? 0 : index;
}

export function sellerStepToken(step: number) {
  return sellerStepTokens[step] ?? "";
}

// The furthest stage the entered work has earned: the valuation unlocks the photos, and four confirmed photos unlock
// the appointment. An address, a Back step or a typed link can never name a stage beyond it.
export function reachableSellerStep(quoted: boolean, confirmedPhotos: number) {
  if (!quoted) {
    return 0;
  }
  return confirmedPhotos >= 4 ? 3 : 2;
}
