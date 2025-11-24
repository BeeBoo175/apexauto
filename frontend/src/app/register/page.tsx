import { AuthForm } from "@/components/auth/AuthForm";
import { Card } from "@/components/ui/Card";

export default function RegisterPage() {
  return (
    <section className="max-w-lg mx-auto p-6 flex flex-col gap-5">
      <h1 className="text-2xl font-bold">Create a customer account</h1>
      <Card>
        <AuthForm kind="register" destination="/login" />
      </Card>
      <a href="/login" className="underline">
        Already registered? Sign in
      </a>
    </section>
  );
}
