import type { Metadata } from "next";
import { PasswordResetForm } from "@/components/auth/PasswordResetForm";
import { Card } from "@/components/ui/Card";

export const metadata: Metadata = {
  title: "Reset password | ApexAuto",
  robots: { index: false, follow: false },
};

export default function ResetPasswordPage() {
  return (
    <section className="max-w-lg mx-auto p-6 flex flex-col gap-5">
      <h1 className="text-2xl font-bold">Reset password</h1>
      <Card>
        <PasswordResetForm />
      </Card>
      <a href="/forgot-password" className="underline">
        Request a new reset link
      </a>
      <a href="/login" className="underline">
        Return to sign in
      </a>
    </section>
  );
}
