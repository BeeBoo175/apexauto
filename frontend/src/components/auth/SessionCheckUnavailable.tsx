"use client";

import { AuthPage } from "./AuthPage";
import { Button } from "@/components/ui/Button";
import { useInitialToast } from "@/hooks/use-initial-toast";

export function SessionCheckUnavailable() {
  useInitialToast(
    "Your session could not be checked. Your browser session has been kept. Try again before continuing.",
  );
  return (
    <AuthPage title="Check your session" introduction="">
      <Button onClick={() => window.location.reload()}>Try checking session again</Button>
    </AuthPage>
  );
}
