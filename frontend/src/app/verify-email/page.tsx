import type { Metadata } from "next";
import { VerifyEmailControl } from "@/components/auth/VerifyEmailControl";
import { AuthPage } from "@/components/auth/AuthPage";

export const metadata: Metadata = {
  title: "Verify email | ApexAuto",
  robots: { index: false, follow: false },
};

export default function VerifyEmailPage() {
  return (
    <AuthPage
      title="Verify your email"
      introduction="Confirm your email address before booking your vehicle intake."
    >
      <VerifyEmailControl />
    </AuthPage>
  );
}
