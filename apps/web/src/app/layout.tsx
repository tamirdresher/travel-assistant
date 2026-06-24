import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Travel Assistant",
  description:
    "AI-powered travel planning assistant — ask about flights, hotels, and itineraries.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="en"
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col">
        {children}
        <footer className="border-t border-zinc-200 bg-white">
          <nav
            aria-label="Footer"
            className="mx-auto flex min-h-16 w-full max-w-5xl flex-col gap-2 px-4 py-4 text-sm text-zinc-600 sm:flex-row sm:items-center sm:justify-between"
          >
            <span>Travel Assistant</span>
            <a
              href="/support"
              className="inline-flex min-h-11 items-center rounded-xl px-1 font-medium text-zinc-950 hover:text-sky-800 focus:outline-none focus:ring-4 focus:ring-sky-100"
            >
              Support
            </a>
          </nav>
        </footer>
      </body>
    </html>
  );
}
