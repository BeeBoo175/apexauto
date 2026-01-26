"use client";

import { AuthPage } from "./AuthPage";
import { Button } from "@/components/ui/Button";

export function SessionCheckUnavailable() {
  return (
    <AuthPage
      title="Check your session"
      introduction="Your session could not be checked. Your browser session has been kept."
      photoHeading="Back to your account."
      photoDescription="Check again when the service is available."
    >
      <p role="status">Try again before signing in or creating another account.</p>
      <Button onClick={() => window.location.reload()}>Try checking session again</Button>
    </AuthPage>
  );
}
