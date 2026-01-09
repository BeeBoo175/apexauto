import type { Metadata } from "next";
import { PasswordRecoveryForm } from "@/components/auth/PasswordRecoveryForm";
import { Card } from "@/components/ui/Card";

export const metadata: Metadata = {
  title: "Forgot password | ApexAuto",
  robots: { index: false, follow: false },
};

export default function ForgotPasswordPage() {
  return (
    <section className="max-w-lg mx-auto p-6 flex flex-col gap-5">
      <h1 className="text-2xl font-bold">Forgot password</h1>
      <Card>
        <PasswordRecoveryForm />
      </Card>
      <a href="/login" className="underline">
        Return to sign in
      </a>
    </section>
  );
}
