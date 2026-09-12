import { roleNames, type CurrentActor, type Role } from "@/lib/auth/actor";
import { isRecord, isUuid } from "@/lib/auth/contracts";

export const PICTURE_CONTENT_TYPES = ["image/jpeg", "image/png", "image/webp"];

export function hasPersonAccess(actor: CurrentActor) {
  return actor.role !== "Customer";
}

export interface AccountProfile {
  userId: string;
  role: Role;
  name: string;
  username: string;
  email: string;
  emailVerified: boolean;
  branchId: string | null;
  branchName: string | null;
  mobile: string | null;
  pictureId: string | null;
}

// A colleague or customer as the viewer may see them; hidden details arrive as null.
export interface Person {
  userId: string;
  role: Role;
  name: string;
  username: string;
  isActive: boolean;
  branchId: string | null;
  branchName: string | null;
  email: string | null;
  mobile: string | null;
  pictureId: string | null;
}

function parseRole(value: unknown): Role | null {
  const match = Object.keys(roleNames).find((item) => item === value);
  return match ? (match as Role) : null;
}

function requiredText(value: unknown) {
  return typeof value === "string" && value.trim() ? value : null;
}

function optionalText(value: unknown) {
  if (value === null || value === undefined) {
    return null;
  }
  return requiredText(value);
}

// Undefined marks a present but malformed identifier; null marks an absent one.
function optionalId(value: unknown) {
  if (value === null || value === undefined) {
    return null;
  }
  return isUuid(value) ? value : undefined;
}

export function parseAccountProfile(value: unknown): AccountProfile | null {
  if (!isRecord(value) || !isUuid(value.userId) || typeof value.emailVerified !== "boolean") {
    return null;
  }
  const role = parseRole(value.role);
  const name = requiredText(value.name);
  const username = requiredText(value.username);
  const email = requiredText(value.email);
  const branchId = optionalId(value.branchId);
  const pictureId = optionalId(value.pictureId);
  if (!role || !name || !username || !email) {
    return null;
  }
  if (branchId === undefined || pictureId === undefined) {
    return null;
  }
  return {
    userId: value.userId,
    role,
    name,
    username,
    email,
    emailVerified: value.emailVerified,
    branchId,
    branchName: optionalText(value.branchName),
    mobile: optionalText(value.mobile),
    pictureId,
  };
}

export function parsePerson(value: unknown): Person | null {
  if (!isRecord(value) || !isUuid(value.userId) || typeof value.isActive !== "boolean") {
    return null;
  }
  const role = parseRole(value.role);
  const name = requiredText(value.name);
  const username = requiredText(value.username);
  const branchId = optionalId(value.branchId);
  const pictureId = optionalId(value.pictureId);
  if (!role || !name || !username) {
    return null;
  }
  if (branchId === undefined || pictureId === undefined) {
    return null;
  }
  return {
    userId: value.userId,
    role,
    name,
    username,
    isActive: value.isActive,
    branchId,
    branchName: optionalText(value.branchName),
    email: optionalText(value.email),
    mobile: optionalText(value.mobile),
    pictureId,
  };
}

// A picture is fetched on every view, so no browser or shared cache may keep one after it is removed.
export const PICTURE_CACHE_CONTROL = "private, no-store";

const PICTURE_STATES = ["Available", "Pending", "DeletionPending", "Deleted"];

export function parsePictureReceipt(value: unknown): { pictureId: string; status: string } | null {
  if (
    !isRecord(value) ||
    !isUuid(value.pictureId) ||
    typeof value.status !== "string" ||
    !PICTURE_STATES.includes(value.status)
  ) {
    return null;
  }
  return { pictureId: value.pictureId, status: value.status };
}

// The receipt of the picture that was removed; its reason tells a removal from a replacement.
export function parseRemovedPicture(
  value: unknown,
): { pictureId: string; status: string; removalReason: string } | null {
  const receipt = parsePictureReceipt(value);
  if (
    !receipt ||
    !isRecord(value) ||
    typeof value.removalReason !== "string" ||
    !["DeletionPending", "Deleted"].includes(receipt.status)
  ) {
    return null;
  }
  return {
    pictureId: receipt.pictureId,
    status: receipt.status,
    removalReason: value.removalReason,
  };
}
