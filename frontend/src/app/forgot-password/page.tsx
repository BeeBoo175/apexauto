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
      introduction="Request a link to get back into your account."
      photoHeading="Back to your account."
      photoDescription="Recover access to your ApexAuto account with your email address."
      links={<a href="/login">Return to sign in</a>}
    >
      <PasswordRecoveryForm />
    </AuthPage>
  );
}
