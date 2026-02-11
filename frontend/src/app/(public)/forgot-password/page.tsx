import type { Metadata } from "next";
import { PasswordRecoveryForm } from "@/components/auth/PasswordRecoveryForm";
import { AuthPage } from "@/components/auth/AuthPage";

export const metadata: Metadata = {
  title: "Forgot password | ApexAuto",
  robots: { index: false, follow: false },
};

export default function ForgotPasswordPage() {
  return (
    <AuthPage
      title="Forgot password"
      introduction="Enter your account email for a reset link."
      links={<a href="/login">Return to sign in</a>}
    >
      <PasswordRecoveryForm />
    </AuthPage>
  );
}
