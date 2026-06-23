import Link from "next/link";

export const metadata = {
  title: "Travel Assistant — AI trip planning",
  description: "AI-powered travel planning assistant.",
};

export default function Home() {
  return (
    <main className="mx-auto flex w-full max-w-3xl flex-1 flex-col items-center justify-center px-4 py-16 text-center">
      <span aria-hidden className="text-5xl">✈️</span>
      <h1 className="mt-4 text-3xl font-semibold tracking-tight text-zinc-900 dark:text-zinc-50">
        Travel Assistant
      </h1>
      <p className="mt-2 max-w-md text-sm text-zinc-500 dark:text-zinc-400">
        AI-powered trip planning. Ask about destinations, flights, hotels, and
        day-by-day itineraries.
      </p>
      <div className="mt-8 flex gap-3">
        <Link
          href="/chat"
          data-testid="home-cta-chat"
          className="rounded-xl bg-zinc-900 px-5 py-2.5 text-sm font-medium text-white transition hover:bg-zinc-700 dark:bg-zinc-50 dark:text-zinc-900 dark:hover:bg-zinc-200"
        >
          Start chatting
        </Link>
        <Link
          href="/login"
          data-testid="home-cta-login"
          className="rounded-xl border border-zinc-300 px-5 py-2.5 text-sm font-medium text-zinc-900 transition hover:border-zinc-500 dark:border-zinc-700 dark:text-zinc-50"
        >
          Sign in
        </Link>
      </div>
    </main>
  );
}
