import "server-only";

import { readCurrentActor, readOwnAccount } from "@/lib/auth/server";
import type { CustomerVehicleAccess } from "./customer-access";

export async function readCustomerVehicleAccess(
  signal?: AbortSignal,
): Promise<CustomerVehicleAccess> {
  try {
    const current = await readCurrentActor(signal);
    if (!current.actor || current.actor.role !== "Customer") {
      return { status: current.actor ? 403 : current.status, actor: current.actor, account: null };
    }
    const own = await readOwnAccount(signal);
    if (own.account?.userId !== current.actor.userId) {
      return { status: 502, actor: current.actor, account: null };
    }
    return { status: own.status, actor: current.actor, account: own.account };
  } catch {
    return { status: 502, actor: null, account: null };
  }
}
