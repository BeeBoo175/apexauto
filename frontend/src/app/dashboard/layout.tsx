import type { Metadata } from "next";
import type { ReactNode } from "react";
import { StaffFrame } from "@/components/staff/StaffFrame";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";
import { actorIdentity } from "@/lib/auth/actor";
import { staffActor } from "@/lib/staff/server";

export const metadata: Metadata = {
  title: "Staff workspace | ApexAuto",
  robots: { index: false, follow: false },
};

export default async function DashboardLayout({ children }: { children: ReactNode }) {
  const actor = await staffActor("/dashboard");
  if (!actor) {
    return <SessionCheckUnavailable />;
  }
  return (
    <main className="flex-1">
      <StaffFrame key={actorIdentity(actor)} actor={actor}>
        {children}
      </StaffFrame>
    </main>
  );
}
