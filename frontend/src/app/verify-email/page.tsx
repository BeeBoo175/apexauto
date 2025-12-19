import type { Metadata } from "next";
import { VerifyEmailControl } from "@/components/auth/VerifyEmailControl";
import { Card } from "@/components/ui/Card";

export const metadata: Metadata = {
  title: "Verify email | ApexAuto",
  robots: { index: false, follow: false },
};

export default function VerifyEmailPage() {
  return (
    <section className="max-w-lg mx-auto p-6 flex flex-col gap-5">
      <h1 className="text-2xl font-bold">Verify your email</h1>
      <Card>
        <VerifyEmailControl />
      </Card>
    </section>
  );
}
