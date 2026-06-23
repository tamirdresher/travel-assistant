import { describe, it, expect } from "vitest";
import {
  isDenied,
  isSafeRelativePath,
  PATHNAME_DENY_PATTERNS,
  SEARCH_DENY_PATTERNS,
} from "../lastPage.denylist";

describe("lastPage.denylist (LP-001 canonical)", () => {
  it("denies all required pathname families", () => {
    for (const p of [
      "/login",
      "/login/",
      "/signup",
      "/logout/now",
      "/auth",
      "/auth/callback",
      "/oauth/google",
      "/checkout/confirm",
      "/_next/static/x.js",
      "/api/me",
    ]) {
      expect(isDenied(p, "")).toBe(true);
    }
  });

  it("allows ordinary app routes", () => {
    for (const p of ["/trips/42", "/itineraries", "/account/settings"]) {
      expect(isDenied(p, "")).toBe(false);
    }
  });

  it("denies search params containing tokens (case-insensitive)", () => {
    for (const s of [
      "?token=abc",
      "?CODE=xyz",
      "?id_token=jwt",
      "?state=x",
      "?access_token=t",
      "?refresh_token=t",
      "?session=s",
      "?otp=1234",
      "?password=p",
    ]) {
      expect(isDenied("/ok", s)).toBe(true);
    }
  });

  it("isSafeRelativePath rejects unsafe inputs", () => {
    for (const u of [
      "https://evil.example/x",
      "//evil.example",
      "javascript:alert(1)",
      "data:text/html,x",
      "/ok\r\n",
      "/%2F%2Fevil",
      "/" + "a".repeat(1100),
      "",
    ]) {
      expect(isSafeRelativePath(u)).toBe(false);
    }
  });

  it("isSafeRelativePath accepts well-formed relative paths", () => {
    for (const u of ["/", "/trips/42", "/trips/42?tab=overview"]) {
      expect(isSafeRelativePath(u)).toBe(true);
    }
  });

  it("exports frozen pattern arrays for tamper-proofing", () => {
    expect(Object.isFrozen(PATHNAME_DENY_PATTERNS)).toBe(true);
    expect(Object.isFrozen(SEARCH_DENY_PATTERNS)).toBe(true);
  });
});
