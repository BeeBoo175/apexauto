import { isPositiveInteger, isRecord, isUtcInstant } from "@/lib/auth/contracts";

export interface RetentionBounds {
  minimumDays: number;
  maximumDays: number;
  minimumBusinessYears: number;
  maximumBusinessYears: number;
}

export interface RetentionSettings {
  securityArtifactDays: number;
  mailDetailDays: number;
  invitationDays: number;
  businessEvidenceYears: number;
  updatedAtUtc: string;
  version: number;
  bounds: RetentionBounds;
}

export interface RetentionDraft {
  securityArtifactDays: string;
  mailDetailDays: string;
  invitationDays: string;
  businessEvidenceYears: string;
}

export type RetentionDraftErrors = Partial<Record<keyof RetentionDraft, string>>;

export interface RetentionValues {
  securityArtifactDays: number;
  mailDetailDays: number;
  invitationDays: number;
  businessEvidenceYears: number;
}

export const retentionFields = [
  "securityArtifactDays",
  "mailDetailDays",
  "invitationDays",
  "businessEvidenceYears",
] as const;

function parseBounds(value: unknown): RetentionBounds | null {
  if (
    !isRecord(value) ||
    !isPositiveInteger(value.minimumDays) ||
    !isPositiveInteger(value.maximumDays) ||
    value.maximumDays < value.minimumDays ||
    !isPositiveInteger(value.minimumBusinessYears) ||
    !isPositiveInteger(value.maximumBusinessYears) ||
    value.maximumBusinessYears < value.minimumBusinessYears
  ) {
    return null;
  }
  return {
    minimumDays: value.minimumDays,
    maximumDays: value.maximumDays,
    minimumBusinessYears: value.minimumBusinessYears,
    maximumBusinessYears: value.maximumBusinessYears,
  };
}

export function parseRetentionSettings(value: unknown): RetentionSettings | null {
  const bounds = isRecord(value) ? parseBounds(value.bounds) : null;
  if (
    !isRecord(value) ||
    !bounds ||
    !isPositiveInteger(value.securityArtifactDays) ||
    !isPositiveInteger(value.mailDetailDays) ||
    !isPositiveInteger(value.invitationDays) ||
    !isPositiveInteger(value.businessEvidenceYears) ||
    !isUtcInstant(value.updatedAtUtc) ||
    !isPositiveInteger(value.version)
  ) {
    return null;
  }
  return {
    securityArtifactDays: value.securityArtifactDays,
    mailDetailDays: value.mailDetailDays,
    invitationDays: value.invitationDays,
    businessEvidenceYears: value.businessEvidenceYears,
    updatedAtUtc: value.updatedAtUtc,
    version: value.version,
    bounds,
  };
}

export function retentionDraftFrom(settings: RetentionSettings): RetentionDraft {
  return {
    securityArtifactDays: String(settings.securityArtifactDays),
    mailDetailDays: String(settings.mailDetailDays),
    invitationDays: String(settings.invitationDays),
    businessEvidenceYears: String(settings.businessEvidenceYears),
  };
}

function wholeNumber(text: string) {
  return /^\d{1,4}$/.test(text.trim()) ? Number(text.trim()) : null;
}

export function readRetentionDraft(draft: RetentionDraft, bounds: RetentionBounds) {
  const errors: RetentionDraftErrors = {};
  const dayFields = ["securityArtifactDays", "mailDetailDays", "invitationDays"] as const;
  const numbers: Partial<Record<keyof RetentionDraft, number>> = {};
  for (const key of dayFields) {
    const days = wholeNumber(draft[key]);
    if (days === null || days < bounds.minimumDays || days > bounds.maximumDays) {
      errors[key] = `Enter whole days from ${bounds.minimumDays} to ${bounds.maximumDays}.`;
    } else {
      numbers[key] = days;
    }
  }
  const years = wholeNumber(draft.businessEvidenceYears);
  if (
    years === null ||
    years < bounds.minimumBusinessYears ||
    years > bounds.maximumBusinessYears
  ) {
    errors.businessEvidenceYears = `Enter whole years from ${bounds.minimumBusinessYears} to ${bounds.maximumBusinessYears}.`;
  } else {
    numbers.businessEvidenceYears = years;
  }
  if (Object.keys(errors).length > 0) {
    return { values: null, errors };
  }
  return { values: numbers as RetentionValues, errors };
}

export function retentionUnchanged(values: RetentionValues, settings: RetentionSettings) {
  return retentionFields.every((key) => values[key] === settings[key]);
}

// Shortening can make records eligible at once, so it needs an explicit confirmation.
export function retentionShortened(values: RetentionValues, settings: RetentionSettings) {
  return retentionFields.some((key) => values[key] < settings[key]);
}
