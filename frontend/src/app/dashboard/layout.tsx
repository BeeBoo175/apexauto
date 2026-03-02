import type { Metadata } from "next";
import { Suspense, type ReactNode } from "react";
import { StaffFrame } from "@/components/staff/StaffFrame";
import { StaffLoading } from "@/components/staff/StaffPage";
import { LoadingProgressProvider, LoadingProgressReady } from "@/components/ui/LoadingProgress";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";
import { actorIdentity } from "@/lib/auth/actor";
import { staffActor } from "@/lib/staff/server";
import styles from "@/app/Shell.module.css";

export const metadata: Metadata = {
  title: "Staff workspace | ApexAuto",
  robots: { index: false, follow: false },
};

export default function DashboardLayout({ children }: { children: ReactNode }) {
  return (
    <main className={styles.main}>
      <LoadingProgressProvider>
        <Suspense
          fallback={
            <StaffFrame>
              <StaffLoading />
            </StaffFrame>
          }
        >
          <AuthenticatedFrame>{children}</AuthenticatedFrame>
        </Suspense>
      </LoadingProgressProvider>
    </main>
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
      {children}
    </StaffFrame>
  );
}
