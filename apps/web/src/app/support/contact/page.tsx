import Link from "next/link";
import { ContactForm } from "@/components/support/ContactForm";

export default function SupportContactPage() {
  return (
    <div className="flex flex-1 flex-col bg-zinc-50 text-zinc-950">
      <header className="border-b border-zinc-200 bg-white">
        <div className="mx-auto flex max-w-3xl items-center justify-between px-4 py-4">
          <Link href="/support" className="min-h-11 text-sm font-semibold">
            Support
          </Link>
          <Link
            href="/"
            className="inline-flex min-h-11 items-center rounded-xl border border-zinc-300 px-4 text-sm font-medium"
          >
            Home
          </Link>
        </div>
      </header>
      <main className="mx-auto w-full max-w-3xl px-4 py-8 sm:py-12">
        <div className="mb-6">
          <p className="text-sm font-medium uppercase tracking-[0.2em] text-sky-700">
            Contact support
          </p>
          <h1 className="mt-3 text-4xl font-semibold tracking-tight text-zinc-950">
            Submit a request
          </h1>
          <p className="mt-4 text-base leading-7 text-zinc-600">
            Share the details below and we&apos;ll reply to your email with the
            next step.
          </p>
        </div>
        <ContactForm />
      </main>
    </div>
  );
}
