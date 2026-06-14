import type { CurrentActor } from "@/lib/auth/actor";
import type { CustomerAccount } from "@/lib/auth/contracts";

// What a vehicle page knows about the signed-in customer; reservation and test-drive actions both read it.
export interface CustomerVehicleAccess {
  status: number;
  actor: CurrentActor | null;
  account: CustomerAccount | null;
}
