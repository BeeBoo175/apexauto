import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { relayStaffRead, relayStaffWrite, type StaffRelay } from "@/lib/deals/deal-routes";
import { hasSalesAccess } from "./sales";

type Body = Record<string, unknown>;

// The sales workspace's reads and commands, open to sales agents only; the backend scopes each to their branch and claims.
const salesRelay: StaffRelay = {
  upstream: "/api/staff",
  access: hasSalesAccess,
  reads: /^\/(?:sales\/queue|reservations\/[0-9a-f-]{36}\/history)$/i,
  writes: /^\/reservations\/[0-9a-f-]{36}\/(?:claims|releases)$/i,
};

export const relaySalesRead = <T>(
  request: Request,
  resource: string,
  parse: (value: unknown) => T | null,
  keys: string[] = [],
) => relayStaffRead(salesRelay, request, resource, parse, keys);

export const relaySalesWrite = <T>(
  request: Request,
  resource: string,
  shape: (body: Body) => Body | null,
  parse: (value: unknown) => T | null,
) => relayStaffWrite(salesRelay, request, resource, "POST", shape, parse);

// The deal identity doubles as the operation identity, so a lost response can be replayed.
export const shapeClaim = (body: Body) => (isUuid(body.id) ? { id: body.id } : null);

export const shapeRelease = (body: Body) => (Object.keys(body).length === 0 ? {} : null);
