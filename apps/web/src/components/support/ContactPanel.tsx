import Link from "next/link";

export function ContactPanel() {
  return (
    <section aria-labelledby="contact-support" className="space-y-4">
      <div>
        <h2
          id="contact-support"
          className="text-2xl font-semibold tracking-tight text-zinc-950"
        >
          Contact support
        </h2>
        <p className="mt-2 text-sm leading-6 text-zinc-600">
          Need more help? Choose the channel that works best for you.
        </p>
      </div>
      <div className="grid gap-3 md:grid-cols-3">
        <article className="rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm">
          <h3 className="text-base font-semibold text-zinc-950">Chat</h3>
          <p className="mt-2 text-sm leading-6 text-zinc-600">
            Instant chat support is not available in the MVP.
          </p>
          <button
            type="button"
            disabled
            className="mt-4 min-h-11 w-full rounded-xl border border-zinc-200 bg-zinc-100 px-4 text-sm font-medium text-zinc-500"
          >
            Coming soon
          </button>
        </article>
        <article className="rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm">
          <h3 className="text-base font-semibold text-zinc-950">Email</h3>
          <p className="mt-2 text-sm leading-6 text-zinc-600">
            Send details and we&apos;ll follow up by email.
          </p>
          <a
            href="mailto:support@travel-assistant.example"
            className="mt-4 inline-flex min-h-11 w-full items-center justify-center rounded-xl border border-zinc-300 px-4 text-sm font-medium text-zinc-950 transition hover:border-sky-400 focus:outline-none focus:ring-4 focus:ring-sky-100"
          >
            Email support
          </a>
        </article>
        <article className="rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm">
          <h3 className="text-base font-semibold text-zinc-950">
            Submit request
          </h3>
          <p className="mt-2 text-sm leading-6 text-zinc-600">
            Tell us what happened so we can route your request.
          </p>
          <Link
            href="/support/contact"
            className="mt-4 inline-flex min-h-11 w-full items-center justify-center rounded-xl bg-zinc-950 px-4 text-sm font-medium text-white transition hover:bg-zinc-800 focus:outline-none focus:ring-4 focus:ring-sky-100"
          >
            Submit request
          </Link>
        </article>
      </div>
    </section>
  );
}
