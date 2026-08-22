import { branchLabel } from "@/lib/branches";

export type BranchKey = "glenmarie" | "petaling-jaya" | "bayan-lepas" | "other";

export function getBranchKey(branchName?: string | null): BranchKey {
  if (!branchName) {
    return "other";
  }
  const clean = branchLabel(branchName).toLowerCase();
  if (clean.includes("glenmarie")) {
    return "glenmarie";
  }
  if (clean.includes("petaling") || clean.includes("pj")) {
    return "petaling-jaya";
  }
  if (clean.includes("bayan") || clean.includes("lepas") || clean.includes("penang")) {
    return "bayan-lepas";
  }
  return "other";
}

export function branchShortCode(branchName?: string | null): string {
  const key = getBranchKey(branchName);
  switch (key) {
    case "glenmarie":
      return "GLN";
    case "petaling-jaya":
      return "PJ";
    case "bayan-lepas":
      return "BL";
    default:
      return "HQ";
  }
}
