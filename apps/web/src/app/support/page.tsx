import Link from "next/link";
import { ContactPanel } from "@/components/support/ContactPanel";
import { HelpCategoryGrid } from "@/components/support/HelpCategoryGrid";
import { PopularArticlesList } from "@/components/support/PopularArticlesList";
import { SupportHero } from "@/components/support/SupportHero";

export default function SupportPage() {
  return (
    <div className="flex flex-1 flex-col bg-zinc-50 text-zinc-950">
      <header className="border-b border-zinc-200 bg-white">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-4">
          <Link
            href="/"
            className="min-h-11 text-sm font-semibold text-zinc-950"
          >
            Travel Assistant
          </Link>
          <Link
            href="/support/contact"
            className="inline-flex min-h-11 items-center rounded-xl border border-zinc-300 px-4 text-sm font-medium text-zinc-950"
          >
            Contact
          </Link>
        </div>
      </header>
      <main className="mx-auto w-full max-w-5xl space-y-10 px-4 py-8 sm:py-12">
        <SupportHero />
        <HelpCategoryGrid />
        <PopularArticlesList />
        <ContactPanel />
      </main>
    </div>
  );
}
