"use client";

import { useState } from "react";

export default function LoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [remember, setRemember] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      const res = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password, remember }),
      });
      if (!res.ok) {
        const detail = await res.text().catch(() => "");
        throw new Error(detail || `Login failed (${res.status})`);
      }
      window.location.href = "/chat";
    } catch (e) {
      setError(e instanceof Error ? e.message : "Login failed");
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center px-4 py-12">
      <h1 className="mb-1 text-2xl font-semibold tracking-tight text-zinc-900 dark:text-zinc-50">
        Sign in
      </h1>
      <p className="mb-6 text-sm text-zinc-500 dark:text-zinc-400">
        Welcome back to Travel Assistant.
      </p>
      <form onSubmit={onSubmit} aria-labelledby="login-heading" className="space-y-4">
        <h2 id="login-heading" className="sr-only">Login form</h2>
        <div>
          <label htmlFor="login-email" className="block text-xs font-medium text-zinc-700 dark:text-zinc-300">
            Email
          </label>
          <input
            id="login-email"
            data-testid="login-email"
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="mt-1 w-full rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 outline-none focus:border-zinc-500 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-50"
          />
        </div>
        <div>
          <label htmlFor="login-password" className="block text-xs font-medium text-zinc-700 dark:text-zinc-300">
            Password
          </label>
          <input
            id="login-password"
            data-testid="login-password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="mt-1 w-full rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 outline-none focus:border-zinc-500 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-50"
          />
        </div>
        <label className="flex items-center gap-2 text-xs text-zinc-700 dark:text-zinc-300">
          <input
            data-testid="login-remember"
            type="checkbox"
            checked={remember}
            onChange={(e) => setRemember(e.target.checked)}
            className="h-4 w-4 rounded border-zinc-300"
          />
          Remember me
        </label>
        {error ? (
          <div
            role="alert"
            data-testid="login-error"
            className="rounded-md border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-800 dark:border-red-900 dark:bg-red-950 dark:text-red-200"
          >
            {error}
          </div>
        ) : null}
        <button
          data-testid="login-submit"
          type="submit"
          disabled={loading}
          className="w-full rounded-xl bg-zinc-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-zinc-700 disabled:opacity-50 dark:bg-zinc-50 dark:text-zinc-900 dark:hover:bg-zinc-200"
        >
          {loading ? "Signing in…" : "Sign in"}
        </button>
      </form>
    </main>
  );
}
