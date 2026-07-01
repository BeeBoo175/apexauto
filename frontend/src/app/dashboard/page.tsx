import { redirect } from "next/navigation";
import { staffActor } from "@/lib/staff/server";
import { actorDestination } from "@/lib/auth/actor";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";
import { LoadingProgressReady } from "@/components/ui/LoadingProgress";

// The staff entry only sends each role to its own workspace.
export default async function DashboardPage() {
  const actor = await staffActor("/dashboard");
  if (!actor) {
    return (
      <>
        <LoadingProgressReady />
        <SessionCheckUnavailable />
      </>
    );
  }
  redirect(actorDestination(actor));
}
