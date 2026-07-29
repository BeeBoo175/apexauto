import type { ReactNode } from "react";
import { connection } from "next/server";
import { StaffFrame } from "@/components/staff/StaffFrame";
import { LoadingProgressReady } from "@/components/ui/LoadingProgress";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";
import { actorIdentity } from "@/lib/auth/actor";
import { staffActor } from "@/lib/staff/server";

export default async function DashboardLayout({ children }: { children: ReactNode }) {
  await connection();
  const { actor, status } = await staffActor("/dashboard");
  if (!actor) {
    return (
      <>
        <LoadingProgressReady />
        <SessionCheckUnavailable status={status} />
      </>
    );
  }
  return (
    <StaffFrame key={actorIdentity(actor)} actor={actor}>
      {children}
    </StaffFrame>
  );
}
