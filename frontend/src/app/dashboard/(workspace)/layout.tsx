import { Suspense, type ReactNode } from "react";
import { StaffFrame } from "@/components/staff/StaffFrame";
import { StaffLoading } from "@/components/staff/StaffPage";
import { LoadingProgressReady } from "@/components/ui/LoadingProgress";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";
import { actorIdentity } from "@/lib/auth/actor";
import { staffActor } from "@/lib/staff/server";

export default function DashboardLayout({ children }: { children: ReactNode }) {
  return (
    <Suspense
      fallback={
        <StaffFrame>
          <StaffLoading />
        </StaffFrame>
      }
    >
      <AuthenticatedFrame>{children}</AuthenticatedFrame>
    </Suspense>
  );
}

async function AuthenticatedFrame({ children }: { children: ReactNode }) {
  const actor = await staffActor("/dashboard");
  if (!actor) {
    return (
      <>
        <LoadingProgressReady />
        <SessionCheckUnavailable />
      </>
    );
  }
  return (
    <StaffFrame key={actorIdentity(actor)} actor={actor}>
      <Suspense fallback={<StaffLoading />}>{children}</Suspense>
    </StaffFrame>
  );
}
