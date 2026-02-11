"use client";

import { useEffect } from "react";
import { Button } from "@/components/ui/Button";
import { useToast } from "@/components/ui/Toast";

export default function AccountError({ reset }: { reset: () => void }) {
  const { error } = useToast();

  useEffect(() => {
    error("Your account could not be loaded. Try again when the service is available.");
  }, [error]);

  return (
    <section className="max-w-lg mx-auto p-6 flex flex-col gap-5">
      <h1 className="text-2xl font-bold">Your account</h1>
      <Button onClick={reset}>Try loading account again</Button>
      <a href="/login">Sign in</a>
    </section>
  );
}
