import { describe, it, expect } from "vitest";
import { isDeniedPath } from "../denyList";

describe("denyList (LP-001 stub)", () => {
  it("denies known auth paths", () => {
    expect(isDeniedPath("/login")).toBe(true);
    expect(isDeniedPath("/auth/callback")).toBe(true);
    expect(isDeniedPath("/reset-password")).toBe(true);
    expect(isDeniedPath("/checkout/confirm")).toBe(true);
  });
  it("denies the root path", () => {
    expect(isDeniedPath("/")).toBe(true);
  });
  it("allows ordinary app pages", () => {
    expect(isDeniedPath("/trips/42")).toBe(false);
    expect(isDeniedPath("/itineraries")).toBe(false);
  });
  it("denies empty / malformed input defensively", () => {
    expect(isDeniedPath("")).toBe(true);
  });
});
