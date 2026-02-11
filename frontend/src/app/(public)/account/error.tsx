"use client";

import { Button } from "@/components/ui/Button";
import { useInitialToast } from "@/hooks/use-initial-toast";

export default function AccountError({ reset }: { reset: () => void }) {
  useInitialToast("Your account could not be loaded. Try again when the service is available.");

  return (
    <section className="max-w-lg mx-auto p-6 flex flex-col gap-5">
      <h1 className="text-2xl font-bold">Your account</h1>
      <Button onClick={reset}>Try loading account again</Button>
      <a href="/login">Sign in</a>
    </section>
  );
}
