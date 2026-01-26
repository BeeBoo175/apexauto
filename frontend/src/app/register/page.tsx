import { AuthForm } from "@/components/auth/AuthForm";
import { AuthPage } from "@/components/auth/AuthPage";
import { GuestOnly } from "@/components/auth/GuestOnly";

export default function RegisterPage() {
  return (
    <GuestOnly>
      <AuthPage
        title="Create an account"
        introduction="Verify your email after registering to book an appointment."
        links={
          <span>
            Already registered? <a href="/login">Sign in</a>
          </span>
        }
      >
        <AuthForm kind="register" destination="/login" />
      </AuthPage>
    </GuestOnly>
  );
}
