import type { Metadata } from "next";
import { PasswordResetForm } from "@/components/auth/PasswordResetForm";
import { AuthPage } from "@/components/auth/AuthPage";

export const metadata: Metadata = {
  title: "Reset password | ApexAuto",
  robots: { index: false, follow: false },
};

export default function ResetPasswordPage() {
  return (
    <AuthPage
      title="Reset password"
      introduction="Set a new password, then return to sign in."
      photoHeading="A fresh start."
      photoDescription="Your account and vehicle records stay together when you reset your password."
      links={
        <>
          <a href="/forgot-password">Request a new reset link</a>
          <a href="/login">Return to sign in</a>
        </>
      }
    >
      <PasswordResetForm />
    </AuthPage>
  );
}
