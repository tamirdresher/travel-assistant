// LP-001 will replace this with the canonical deny-list (auth pages,
// password reset, /logout, payment confirmation, etc.). This stub keeps
// LP-002/LP-003 compiling and exercises the matching contract that LP-001
// will satisfy. DO NOT extend this list here — push the change to LP-001.

const DENIED_PREFIXES: ReadonlyArray<string> = [
  "/login",
  "/logout",
  "/register",
  "/auth/",
  "/reset-password",
  "/verify",
  "/checkout/confirm",
];

export function isDeniedPath(pathname: string): boolean {
  if (typeof pathname !== "string" || pathname.length === 0) return true;
  if (pathname === "/") return true; // landing page is never restored
  return DENIED_PREFIXES.some(
    (p) => pathname === p || pathname.startsWith(p + "/") || pathname.startsWith(p),
  );
}
