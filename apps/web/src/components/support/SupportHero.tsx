export function SupportHero() {
  return (
    <section className="rounded-3xl border border-zinc-200 bg-white px-5 py-8 shadow-sm sm:px-8">
      <div className="mx-auto max-w-2xl text-center">
        <p className="text-sm font-medium uppercase tracking-[0.2em] text-sky-700">
          Support center
        </p>
        <h1 className="mt-3 text-4xl font-semibold tracking-tight text-zinc-950 sm:text-5xl">
          How can we help?
        </h1>
        <p className="mt-4 text-base leading-7 text-zinc-600">
          Find answers about planning trips, managing bookings, account settings,
          billing, safety updates, and getting help from Travel Assistant.
        </p>
        <form
          className="mt-8 flex flex-col gap-3 sm:flex-row"
          role="search"
          aria-label="Search support articles"
        >
          <label className="sr-only" htmlFor="support-search">
            Search support articles
          </label>
          <input
            id="support-search"
            name="search"
            type="search"
            placeholder="Search support articles"
            className="min-h-11 flex-1 rounded-2xl border border-zinc-300 bg-white px-4 text-base text-zinc-950 outline-none transition placeholder:text-zinc-400 focus:border-sky-600 focus:ring-4 focus:ring-sky-100"
          />
          <button
            type="button"
            className="min-h-11 rounded-2xl bg-zinc-950 px-6 text-base font-medium text-white transition hover:bg-zinc-800 focus:outline-none focus:ring-4 focus:ring-sky-100"
          >
            Search
          </button>
        </form>
      </div>
    </section>
  );
}
