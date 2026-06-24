import { createHmac, timingSafeEqual } from "crypto";
import { NextRequest, NextResponse } from "next/server";
import { z } from "zod";

const RATE_LIMIT_WINDOW_MS = 15 * 60 * 1000;
const RATE_LIMIT_MAX_SUBMISSIONS = 5;
const MIN_FORM_FILL_MS = 2_000;
const MAX_HONEYPOT_AGE_MS = 24 * 60 * 60 * 1000;

const SUPPORT_TOPICS = [
  "account",
  "billing",
  "booking",
  "trip-planning",
  "technical",
  "safety",
  "accessibility",
  "other",
] as const;

type RateLimitEntry = {
  count: number;
  resetAt: number;
};

const rateLimitStore =
  globalThis.__supportContactRateLimitStore ??
  new Map<string, RateLimitEntry>();

globalThis.__supportContactRateLimitStore = rateLimitStore;

const attachmentMetadataSchema = z
  .object({
    fileName: z.string().trim().min(1).max(255),
    mimeType: z.string().trim().min(1).max(100),
    sizeBytes: z.number().int().nonnegative().max(10 * 1024 * 1024),
  })
  .strict();

const contactRequestSchema = z
  .object({
    name: z
      .string()
      .transform(stripControlCharacters)
      .pipe(z.string().trim().min(1).max(100)),
    email: z.string().trim().email().max(254),
    topic: z.enum(SUPPORT_TOPICS),
    message: z
      .string()
      .transform(stripControlCharacters)
      .pipe(z.string().trim().min(20).max(5000)),
    tripId: z
      .string()
      .trim()
      .regex(/^[A-Za-z0-9-]{1,64}$/)
      .optional()
      .or(z.literal("").transform(() => undefined)),
    attachment: attachmentMetadataSchema.optional(),
    honeypot: z
      .object({
        value: z.string().max(0).optional().default(""),
        issuedAt: z.number().int().positive(),
        signature: z.string().min(32).max(256),
      })
      .strict(),
  })
  .strict();

type ContactRequest = z.infer<typeof contactRequestSchema>;

declare global {
  // eslint-disable-next-line no-var
  var __supportContactRateLimitStore: Map<string, RateLimitEntry> | undefined;
}

function stripControlCharacters(value: string): string {
  return value.replace(/[\u0000-\u001F\u007F-\u009F]/g, "");
}

function jsonResponse(
  body: Record<string, unknown>,
  init: ResponseInit = {},
): NextResponse {
  const headers = new Headers(init.headers);
  headers.set("Content-Type", "application/json");
  headers.set("X-Content-Type-Options", "nosniff");

  return NextResponse.json(body, {
    ...init,
    headers,
  });
}

function getClientIp(req: NextRequest): string {
  const forwardedFor = req.headers.get("x-forwarded-for");
  const firstForwardedIp = forwardedFor?.split(",")[0]?.trim();
  return firstForwardedIp || req.headers.get("x-real-ip") || "unknown";
}

function isRateLimited(ip: string, now = Date.now()): boolean {
  for (const [key, entry] of rateLimitStore) {
    if (entry.resetAt <= now) {
      rateLimitStore.delete(key);
    }
  }

  const entry = rateLimitStore.get(ip);
  if (!entry) {
    rateLimitStore.set(ip, {
      count: 1,
      resetAt: now + RATE_LIMIT_WINDOW_MS,
    });
    return false;
  }

  if (entry.count >= RATE_LIMIT_MAX_SUBMISSIONS) {
    return true;
  }

  entry.count += 1;
  return false;
}

function getHmacSecret(): string {
  return process.env.SUPPORT_FORM_HMAC_SECRET ?? "development-support-form-secret";
}

function signHoneypot(issuedAt: number): string {
  return createHmac("sha256", getHmacSecret())
    .update(`support-contact:${issuedAt}`)
    .digest("hex");
}

function signaturesMatch(expected: string, actual: string): boolean {
  const expectedBuffer = Buffer.from(expected, "hex");
  const actualBuffer = Buffer.from(actual, "hex");

  return (
    expectedBuffer.length === actualBuffer.length &&
    timingSafeEqual(expectedBuffer, actualBuffer)
  );
}

function hasValidHoneypot(honeypot: ContactRequest["honeypot"]): boolean {
  const now = Date.now();
  const ageMs = now - honeypot.issuedAt;

  if (
    honeypot.value ||
    ageMs < MIN_FORM_FILL_MS ||
    ageMs > MAX_HONEYPOT_AGE_MS
  ) {
    return false;
  }

  return signaturesMatch(signHoneypot(honeypot.issuedAt), honeypot.signature);
}

export async function GET() {
  const issuedAt = Date.now();

  return jsonResponse({
    honeypot: {
      fieldName: "companyWebsite",
      issuedAt,
      signature: signHoneypot(issuedAt),
    },
    topics: SUPPORT_TOPICS,
  });
}

export async function POST(req: NextRequest) {
  const ip = getClientIp(req);

  // TODO: Replace this process-local limiter with Redis/Upstash before
  // deploying multiple server instances.
  if (isRateLimited(ip)) {
    return jsonResponse(
      { error: "Too many requests. Please try again later." },
      { status: 429 },
    );
  }

  let body: unknown;
  try {
    body = await req.json();
  } catch {
    return jsonResponse({ error: "Invalid support request." }, { status: 400 });
  }

  const parsed = contactRequestSchema.safeParse(body);
  if (!parsed.success || !hasValidHoneypot(parsed.data.honeypot)) {
    return jsonResponse({ error: "Invalid support request." }, { status: 400 });
  }

  try {
    const submission = parsed.data;

    return jsonResponse(
      {
        id: crypto.randomUUID(),
        status: "received",
        topic: submission.topic,
      },
      { status: 202 },
    );
  } catch {
    return jsonResponse(
      { error: "We could not submit your request. Please try again later." },
      { status: 500 },
    );
  }
}
