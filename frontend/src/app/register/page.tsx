import { AuthForm } from "@/components/auth/AuthForm";
import { AuthPage } from "@/components/auth/AuthPage";
import { GuestOnly } from "@/components/auth/GuestOnly";

export default function RegisterPage() {
  return (
    <GuestOnly>
      <AuthPage
        title="Create a customer account"
        introduction="Register your details, then verify your email before booking an appointment."
        photoHeading="Your vehicle. Your account."
        photoDescription="Keep your vehicle intake and appointments together in your ApexAuto account."
        links={<a href="/login">Already registered? Sign in</a>}
      >
        <AuthForm kind="register" destination="/login" />
      </AuthPage>
    </GuestOnly>
  );
}
