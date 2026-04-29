import "server-only";
import type { CurrentActor } from "@/lib/auth/actor";
import { isUuid } from "@/lib/auth/contracts";
import { relayStaffRead, relayStaffWrite, type StaffRelay } from "@/lib/deals/deal-routes";
import { hasReservationAccess } from "@/lib/vehicles/reservations";
import { hasSalesAccess } from "./sales";

type Body = Record<string, unknown>;

// Managers choose and give a hold to an agent; both roles may release a claim and read a hold's history.
function holdAccess(actor: CurrentActor, resource: string) {
  if (/\/(?:agents|assignments)$/.test(resource)) {
    return hasReservationAccess(actor);
  }
  if (/\/(?:releases|history)$/.test(resource)) {
    return hasSalesAccess(actor) || hasReservationAccess(actor);
  }
  return hasSalesAccess(actor);
}

// The hold commands and reads of the sales workspace and the managers' reservation drawer; the backend scopes each to the actor's branch and claims.
const salesRelay: StaffRelay = {
  upstream: "/api/staff",
  access: holdAccess,
  reads: /^\/(?:sales\/queue|reservations\/[0-9a-f-]{36}\/(?:history|agents))$/i,
  writes: /^\/reservations\/[0-9a-f-]{36}\/(?:claims|releases|assignments)$/i,
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

export const shapeAssignment = (body: Body) =>
  isUuid(body.id) && isUuid(body.agentId) ? { id: body.id, agentId: body.agentId } : null;

export const shapeRelease = (body: Body) => (Object.keys(body).length === 0 ? {} : null);
