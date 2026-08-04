"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { accountsWorkspace } from "@/lib/accounts/accounts-location";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { ViewSwitcher } from "@/components/ui/ViewSwitcher";

type View = "staff" | "customers";

// The accounts page has a Staff view and a Customers view; the command bar switches between them. The page change
// starts at once and the shared loading bar shows it, while the thumb slides on its own.
export function AccountsViewSwitch({ view }: { view: View }) {
  const router = useRouter();
  const [shown, setShown] = useState<View>(view);
  const [pending, startTransition] = useTransition();

  return (
    <>
      <LoadingProgress label="Opening accounts view" active={pending} />
      <ViewSwitcher<View>
        label="Account views"
        labelled
        value={shown}
        options={[
          { value: "staff", label: "Staff", icon: "users" },
          { value: "customers", label: "Customers", icon: "user" },
        ]}
        onChange={(next) => {
          setShown(next);
          startTransition(() => router.push(accountsWorkspace(next)));
        }}
      />
    </>
  );
}
