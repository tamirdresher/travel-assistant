const FAQ_ITEMS = [
  {
    question: "How do I cancel a trip?",
    answer:
      "Open your itinerary, review any booking provider rules, and remove the trip from your saved plans when you are ready.",
  },
  {
    question: "How does the AI assistant work?",
    answer:
      "Travel Assistant uses your prompt and trip context to suggest destinations, routes, stays, and itinerary ideas. Always verify critical details with the provider.",
  },
  {
    question: "What payment methods are accepted?",
    answer:
      "Payment options depend on the booking partner. We will show available methods before you complete a reservation.",
  },
  {
    question: "How do I update my account email?",
    answer:
      "Go to account settings, choose your email address, and follow the verification steps for the new address.",
  },
  {
    question: "Can I share an itinerary with someone?",
    answer:
      "Yes. Open the itinerary and use the share option to copy a link or invite another traveler when sharing is available.",
  },
  {
    question: "What if my flight is disrupted?",
    answer:
      "Check the airline's latest update first, then use Travel Assistant to explore alternate routes, hotels, and schedule changes.",
  },
] as const;

export function PopularArticlesList() {
  return (
    <section aria-labelledby="popular-articles" className="space-y-4">
      <div>
        <h2
          id="popular-articles"
          className="text-2xl font-semibold tracking-tight text-zinc-950"
        >
          Popular articles
        </h2>
        <p className="mt-2 text-sm leading-6 text-zinc-600">
          Quick answers to common questions.
        </p>
      </div>
      <div className="divide-y divide-zinc-200 rounded-2xl border border-zinc-200 bg-white shadow-sm">
        {/* TODO: Replace placeholder FAQ source with approved product FAQ content. */}
        {FAQ_ITEMS.map((item) => (
          <details key={item.question} className="group">
            <summary className="flex min-h-11 cursor-pointer list-none items-center justify-between gap-4 px-5 py-4 text-left text-base font-medium text-zinc-950 marker:hidden focus:outline-none focus:ring-4 focus:ring-inset focus:ring-sky-100">
              {item.question}
              <span
                aria-hidden
                className="text-xl leading-none text-zinc-400 transition group-open:rotate-45"
              >
                +
              </span>
            </summary>
            <p className="px-5 pb-5 text-sm leading-6 text-zinc-600">
              {item.answer}
            </p>
          </details>
        ))}
      </div>
    </section>
  );
}
