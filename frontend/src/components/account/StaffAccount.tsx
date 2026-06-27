"use client";

import { AccountPanel } from "@/components/auth/AccountPanel";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import type { AccountProfile } from "@/lib/account/contracts";
import layout from "@/components/staff/Staff.module.css";

// The dashboard's own account page: the same account details a customer manages, inside the staff shell.
export function StaffAccount({ profile }: { profile: AccountProfile }) {
  return (
    <>
      <StaffCommandBar title="Account" />
      <div className={layout.direct}>
        <AccountPanel profile={profile} />
      </div>
    </>
  );
}
