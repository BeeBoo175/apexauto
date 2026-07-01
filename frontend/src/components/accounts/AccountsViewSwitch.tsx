"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { ViewSwitcher } from "@/components/ui/ViewSwitcher";

type View = "staff" | "customers";

// The accounts page has a Staff view and a Customers view; the command bar switches between them. The thumb
// slides first and the page changes once it has landed, since each view is its own page.
export function AccountsViewSwitch({ view }: { view: View }) {
  const router = useRouter();
  const [shown, setShown] = useState<View>(view);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(
    () => () => {
      if (timer.current) {
        clearTimeout(timer.current);
      }
    },
    [],
  );

  return (
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
        timer.current = setTimeout(
          () =>
            router.push(
              next === "staff" ? "/dashboard/accounts" : "/dashboard/accounts?view=customers",
            ),
          200,
        );
      }}
    />
  );
}
