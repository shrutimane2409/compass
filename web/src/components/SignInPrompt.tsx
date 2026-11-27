import { useAccount } from "../account";
import { GoogleButton } from "./GoogleButton";

export function SignInPrompt({ title, text }: { title: string; text: string }) {
  const { enabled, loading } = useAccount();
  if (loading) return null;
  return (
    <section className="panel prompt">
      <h2>{title}</h2>
      <p className="muted">{text}</p>
      {enabled ? <GoogleButton /> : <p className="muted small">Sign-in isn't available right now. You can keep using Compass as a guest.</p>}
    </section>
  );
}
