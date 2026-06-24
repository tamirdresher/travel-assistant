import Link from "next/link";

export const SUPPORT_CATEGORIES = [
  {
    title: "Trips & itineraries",
    slug: "trips-itineraries",
    description: "Build, update, and share travel plans.",
  },
  {
    title: "Bookings & reservations",
    slug: "bookings-reservations",
    description: "Understand hotels, flights, confirmations, and changes.",
  },
  {
    title: "AI assistant behavior",
    slug: "ai-assistant-behavior",
    description: "Learn how recommendations and answers are generated.",
  },
  {
    title: "Account & login",
    slug: "account-login",
    description: "Manage sign-in, profile, and email settings.",
  },
  {
    title: "Billing & subscriptions",
    slug: "billing-subscriptions",
    description: "Review plans, invoices, payments, and cancellations.",
  },
  {
    title: "Travel safety & disruptions",
    slug: "travel-safety-disruptions",
    description: "Get help with delays, cancellations, and travel alerts.",
  },
  {
    title: "Privacy & data",
    slug: "privacy-data",
    description: "Control your data, privacy settings, and exports.",
  },
  {
    title: "Technical issues",
    slug: "technical-issues",
    description: "Troubleshoot app, browser, and performance problems.",
  },
] as const;

type HelpCategory = (typeof SUPPORT_CATEGORIES)[number];

export function HelpCategoryGrid() {
  return (
    <section aria-labelledby="support-categories" className="space-y-4">
      <div>
        <h2
          id="support-categories"
          className="text-2xl font-semibold tracking-tight text-zinc-950"
        >
          Browse help topics
        </h2>
        <p className="mt-2 text-sm leading-6 text-zinc-600">
          Choose a category to find the support path that best matches your
          question.
        </p>
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        {SUPPORT_CATEGORIES.map((category) => (
          <HelpCategoryCard key={category.slug} category={category} />
        ))}
      </div>
    </section>
  );
}

export function HelpCategoryCard({ category }: { category: HelpCategory }) {
  return (
    <Link
      href={`/support/category/${category.slug}`}
      className="group block min-h-28 rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-sky-300 hover:shadow-md focus:outline-none focus:ring-4 focus:ring-sky-100"
    >
      <h3 className="text-base font-semibold text-zinc-950 group-hover:text-sky-800">
        {category.title}
      </h3>
      <p className="mt-2 text-sm leading-6 text-zinc-600">
        {category.description}
      </p>
    </Link>
  );
}
