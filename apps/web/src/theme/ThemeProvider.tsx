"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";

export type ThemeChoice = "light" | "dark" | "system";
export type ResolvedTheme = "light" | "dark";

export const THEME_STORAGE_KEY = "ta.theme";
const THEME_VALUES: readonly ThemeChoice[] = ["light", "dark", "system"];

type ThemeChangedSource = "user" | "system";

export type ThemeChangedDetail = {
  from: ResolvedTheme;
  to: ResolvedTheme;
  source: ThemeChangedSource;
};

type ThemeContextValue = {
  theme: ThemeChoice;
  resolvedTheme: ResolvedTheme;
  setTheme: (next: ThemeChoice) => void;
};

const ThemeContext = createContext<ThemeContextValue | null>(null);

function isThemeChoice(value: unknown): value is ThemeChoice {
  return typeof value === "string" && (THEME_VALUES as readonly string[]).includes(value);
}

function safeReadStoredTheme(): ThemeChoice {
  try {
    const raw = window.localStorage.getItem(THEME_STORAGE_KEY);
    return isThemeChoice(raw) ? raw : "system";
  } catch {
    return "system";
  }
}

function safeWriteStoredTheme(value: ThemeChoice): void {
  try {
    window.localStorage.setItem(THEME_STORAGE_KEY, value);
  } catch {
    /* localStorage may be unavailable (private mode, disabled) — silently ignore. */
  }
}

function systemPrefersDark(): boolean {
  if (typeof window === "undefined" || typeof window.matchMedia !== "function") {
    return false;
  }
  return window.matchMedia("(prefers-color-scheme: dark)").matches;
}

function resolve(choice: ThemeChoice): ResolvedTheme {
  if (choice === "system") return systemPrefersDark() ? "dark" : "light";
  return choice;
}

function applyResolved(resolved: ResolvedTheme): void {
  if (typeof document === "undefined") return;
  document.documentElement.dataset.theme = resolved;
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [theme, setThemeState] = useState<ThemeChoice>("system");
  const [resolvedTheme, setResolvedTheme] = useState<ResolvedTheme>("light");
  const lastResolvedRef = useRef<ResolvedTheme>("light");
  const hydratedRef = useRef(false);

  useEffect(() => {
    const stored = safeReadStoredTheme();
    const resolved = resolve(stored);
    lastResolvedRef.current = resolved;
    setThemeState(stored);
    setResolvedTheme(resolved);
    applyResolved(resolved);
    hydratedRef.current = true;
  }, []);

  useEffect(() => {
    if (!hydratedRef.current) return;
    if (theme !== "system") return;
    if (typeof window === "undefined" || typeof window.matchMedia !== "function") return;

    const mql = window.matchMedia("(prefers-color-scheme: dark)");
    const handler = (event: MediaQueryListEvent) => {
      const next: ResolvedTheme = event.matches ? "dark" : "light";
      const prev = lastResolvedRef.current;
      if (next === prev) return;
      lastResolvedRef.current = next;
      setResolvedTheme(next);
      applyResolved(next);
      dispatchThemeChanged({ from: prev, to: next, source: "system" });
    };

    if (typeof mql.addEventListener === "function") {
      mql.addEventListener("change", handler);
      return () => mql.removeEventListener("change", handler);
    }
    mql.addListener(handler);
    return () => mql.removeListener(handler);
  }, [theme]);

  const setTheme = useCallback((next: ThemeChoice) => {
    if (!isThemeChoice(next)) return;
    setThemeState((current) => {
      if (current === next) return current;
      safeWriteStoredTheme(next);
      const prevResolved = lastResolvedRef.current;
      const nextResolved = resolve(next);
      if (nextResolved !== prevResolved) {
        lastResolvedRef.current = nextResolved;
        setResolvedTheme(nextResolved);
        applyResolved(nextResolved);
        dispatchThemeChanged({ from: prevResolved, to: nextResolved, source: "user" });
      }
      return next;
    });
  }, []);

  const value = useMemo<ThemeContextValue>(
    () => ({ theme, resolvedTheme, setTheme }),
    [theme, resolvedTheme, setTheme],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) {
    throw new Error("useTheme must be used within a ThemeProvider");
  }
  return ctx;
}

function dispatchThemeChanged(detail: ThemeChangedDetail): void {
  if (typeof window === "undefined" || typeof CustomEvent !== "function") return;
  window.dispatchEvent(new CustomEvent<ThemeChangedDetail>("theme.changed", { detail }));
}
