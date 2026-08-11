const SANITIZE_PATTERN = /[^A-Z0-9 ]/g;
const COLLAPSE_SPACE_PATTERN = /\s{2,}/g;
const PLATE_PATTERN = /^([A-Z]{1,3})\s*([1-9][0-9]{0,3})(?:\s*([A-Z]))?$/;

export const REGISTRATION_PLATE_MAX_LENGTH = 12;

export function sanitizeRegistrationPlateInput(value: string): string {
  return value
    .toUpperCase()
    .replace(SANITIZE_PATTERN, "")
    .replace(COLLAPSE_SPACE_PATTERN, " ")
    .replace(/^\s+/, "");
}

export function normalizeMalaysianPlate(value: string): string {
  const trimmed = value.trim().toUpperCase().replace(COLLAPSE_SPACE_PATTERN, " ");
  const match = PLATE_PATTERN.exec(trimmed);
  if (!match) {
    return trimmed;
  }
  const [, prefix, digits, suffix] = match;
  return suffix ? `${prefix} ${digits} ${suffix}` : `${prefix} ${digits}`;
}

export function isValidMalaysianPlate(value: string): boolean {
  const trimmed = value.trim().toUpperCase().replace(COLLAPSE_SPACE_PATTERN, " ");
  return PLATE_PATTERN.test(trimmed);
}

export function validateMalaysianPlate(value: string, required = false): string | null {
  const trimmed = value.trim();
  if (!trimmed) {
    return required ? "Enter your Malaysian registration plate (e.g. VCA 6242)." : null;
  }
  if (!isValidMalaysianPlate(trimmed)) {
    return "Use Malaysian plate format: 1–3 letters, 1–4 digits (1–9999), and an optional trailing letter (e.g. VCA 6242 or W 1234 A).";
  }
  return null;
}
