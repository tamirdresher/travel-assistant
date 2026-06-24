import Link from "next/link";

export default function SupportContactSuccessPage() {
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
      <main className="mx-auto flex w-full max-w-3xl flex-1 items-center px-4 py-12">
        <section className="w-full rounded-3xl border border-zinc-200 bg-white p-8 text-center shadow-sm">
          <p className="text-sm font-medium uppercase tracking-[0.2em] text-sky-700">
            Request received
          </p>
          <h1 className="mt-3 text-4xl font-semibold tracking-tight text-zinc-950">
            Message sent. We&apos;ll reply to your email with the next step.
          </h1>
          <Link
            href="/support"
            className="mt-8 inline-flex min-h-11 items-center justify-center rounded-xl bg-zinc-950 px-5 text-base font-medium text-white transition hover:bg-zinc-800 focus:outline-none focus:ring-4 focus:ring-sky-100"
          >
            Back to support
          </Link>
        </section>
      </main>
    </div>
  );
}
