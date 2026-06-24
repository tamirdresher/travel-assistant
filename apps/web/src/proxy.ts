import { NextRequest, NextResponse } from "next/server";

function createNonce(): string {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);

  return btoa(String.fromCharCode(...bytes));
}

function supportSecurityHeaders(nonce: string): Record<string, string> {
  const csp = [
    "default-src 'self'",
    "base-uri 'self'",
    "object-src 'none'",
    "frame-ancestors 'none'",
    "form-action 'self'",
    `script-src 'self' 'nonce-${nonce}'`,
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "font-src 'self'",
    "connect-src 'self'",
    "upgrade-insecure-requests",
  ].join("; ");

  return {
    "Content-Security-Policy": csp,
    "X-Frame-Options": "DENY",
    "Referrer-Policy": "strict-origin-when-cross-origin",
    "Permissions-Policy":
      "camera=(), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()",
    "Strict-Transport-Security": "max-age=31536000; includeSubDomains",
    "X-Content-Type-Options": "nosniff",
  };
}

export function proxy(request: NextRequest) {
  const nonce = createNonce();
  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("x-nonce", nonce);

  const response = NextResponse.next({
    request: {
      headers: requestHeaders,
    },
  });

  for (const [key, value] of Object.entries(supportSecurityHeaders(nonce))) {
    response.headers.set(key, value);
  }

  return response;
}

export const config = {
  matcher: ["/support/:path*", "/api/support/:path*"],
};
