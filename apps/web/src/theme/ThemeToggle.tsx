"use client";

import { useId } from "react";
import { type ThemeChoice, useTheme } from "./ThemeProvider";

type Option = { value: ThemeChoice; label: string };

const OPTIONS: readonly Option[] = [
  { value: "light", label: "Light" },
  { value: "system", label: "System" },
  { value: "dark", label: "Dark" },
];

/**
 * Segmented radiogroup theme toggle.
 *
 * NOTE: Markup is a stub pending XD DM-001 (`docs/wireframes/dark-mode/toggle.md`).
 * Visual styling consumes CSS custom props once DM-001 lands; today it uses
 * Tailwind utility classes that already track `[data-theme="dark"]` via the
 * `@custom-variant dark` declaration in `globals.css`.
 */
export function ThemeToggle({ className }: { className?: string }) {
  const { theme, setTheme } = useTheme();
  const groupId = useId();

  return (
    <div
      role="radiogroup"
      aria-label="Theme"
      className={
        "inline-flex items-center gap-0.5 rounded-full border border-zinc-300 bg-white p-0.5 text-xs " +
        "dark:border-zinc-700 dark:bg-zinc-900 " +
        (className ?? "")
      }
    >
      {OPTIONS.map((opt) => {
        const checked = theme === opt.value;
        const id = `${groupId}-${opt.value}`;
        return (
          <label
            key={opt.value}
            htmlFor={id}
            className={
              "cursor-pointer rounded-full px-2.5 py-1 transition select-none " +
              (checked
                ? "bg-zinc-900 text-zinc-50 dark:bg-zinc-50 dark:text-zinc-900"
                : "text-zinc-600 hover:text-zinc-900 dark:text-zinc-300 dark:hover:text-zinc-50")
            }
          >
            <input
              id={id}
              type="radio"
              name={groupId}
              value={opt.value}
              checked={checked}
              onChange={() => setTheme(opt.value)}
              className="sr-only"
              aria-label={opt.label}
            />
            {opt.label}
          </label>
        );
      })}
    </div>
  );
}
