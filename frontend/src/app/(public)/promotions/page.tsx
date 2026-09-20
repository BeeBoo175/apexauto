import { permanentRedirect } from "next/navigation";

// Promo codes are explained in the FAQ; there is no separate promotions page.
export default function PromotionsPage() {
  permanentRedirect("/faq");
}
