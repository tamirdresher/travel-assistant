import { NextRequest, NextResponse } from "next/server";

export const runtime = "edge";

type ChatMessage = {
  role: "user" | "assistant" | "system";
  content: string;
};

type ChatRequest = {
  messages?: ChatMessage[];
};

const SYSTEM_PROMPT = `You are Travel Assistant, a friendly and concise AI travel planner.
Help users plan trips: suggest destinations, build day-by-day itineraries,
recommend flights and hotels by category (budget/mid/luxury), and surface
practical tips (visa, weather, currency, transit).

Style:
- Keep replies focused and skimmable.
- Use short bullet lists or numbered steps when listing options.
- When asked for flights/hotels, present a few options with rough price bands and trade-offs.
- If you don't know real-time prices, say so and give realistic ranges.
- Always confirm or ask a single clarifying question if the trip is under-specified.`;

function mockReply(userMessage: string): string {
  const m = userMessage.toLowerCase();

  if (m.includes("flight")) {
    return [
      "Here are a few sample flight options (demo data — connect an AI key for real recommendations):",
      "",
      "• ✈️ **Budget** — 1 stop, ~12h, $480–620",
      "• ✈️ **Balanced** — non-stop, ~9h, $720–880",
      "• ✈️ **Premium** — non-stop, lie-flat, $2,100+",
      "",
      "Tell me your origin, dates, and budget and I'll narrow it down.",
    ].join("\n");
  }
  if (m.includes("hotel")) {
    return [
      "Sample hotel picks (demo data):",
      "",
      "• 🏨 **Boutique downtown** — walkable area, $140–180/night",
      "• 🏨 **Family-friendly resort** — pools + kids' club, $220–280/night",
      "• 🏨 **Luxury** — spa & fine dining, $500+/night",
      "",
      "Share your city, dates, and travelers and I'll refine.",
    ].join("\n");
  }
  if (m.includes("itinerary") || m.includes("plan") || m.includes("day")) {
    return [
      "Here's a sample 3-day skeleton (demo mode):",
      "",
      "**Day 1 — Arrive & wander**",
      "• Check in, easy neighborhood walk, sunset viewpoint",
      "",
      "**Day 2 — Highlights**",
      "• Top museum or landmark, local lunch, free afternoon",
      "",
      "**Day 3 — Local flavor**",
      "• Day trip or food tour, relaxed dinner",
      "",
      "Add a destination and dates and I'll fill it in.",
    ].join("\n");
  }

  return [
    "I'm running in **demo mode** (no AI key configured), so I'll improvise!",
    "",
    `You asked: "${userMessage}"`,
    "",
    "Try one of these to see structured suggestions:",
    "• \"Plan a 5-day trip to Tokyo\"",
    "• \"Find me flights to Lisbon\"",
    "• \"Hotels in Barcelona for a family\"",
    "",
    "To get real AI replies, set `OPENAI_API_KEY` (works with OpenAI, Groq, or any OpenAI-compatible provider).",
  ].join("\n");
}

async function callOpenAICompatible(
  apiKey: string,
  baseUrl: string,
  model: string,
  messages: ChatMessage[],
): Promise<string> {
  const res = await fetch(`${baseUrl.replace(/\/$/, "")}/chat/completions`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${apiKey}`,
    },
    body: JSON.stringify({
      model,
      messages: [{ role: "system", content: SYSTEM_PROMPT }, ...messages],
      temperature: 0.6,
      max_tokens: 700,
    }),
  });

  if (!res.ok) {
    const body = await res.text().catch(() => "");
    throw new Error(`Upstream LLM error ${res.status}: ${body.slice(0, 300)}`);
  }

  const data = (await res.json()) as {
    choices?: { message?: { content?: string } }[];
  };
  const reply = data.choices?.[0]?.message?.content?.trim();
  if (!reply) throw new Error("Empty reply from LLM");
  return reply;
}

export async function POST(req: NextRequest) {
  let body: ChatRequest;
  try {
    body = (await req.json()) as ChatRequest;
  } catch {
    return NextResponse.json({ error: "Invalid JSON body" }, { status: 400 });
  }

  const messages = (body.messages ?? []).filter(
    (m): m is ChatMessage =>
      !!m &&
      (m.role === "user" || m.role === "assistant" || m.role === "system") &&
      typeof m.content === "string" &&
      m.content.length > 0,
  );

  if (messages.length === 0) {
    return NextResponse.json(
      { error: "messages array is required" },
      { status: 400 },
    );
  }

  const lastUser =
    [...messages].reverse().find((m) => m.role === "user")?.content ?? "";

  const apiKey = process.env.OPENAI_API_KEY;
  const baseUrl = process.env.OPENAI_BASE_URL || "https://api.openai.com/v1";
  const model = process.env.OPENAI_MODEL || "gpt-4o-mini";

  if (!apiKey) {
    return NextResponse.json({
      reply: mockReply(lastUser),
      mode: "demo",
    });
  }

  try {
    const reply = await callOpenAICompatible(apiKey, baseUrl, model, messages);
    return NextResponse.json({ reply, mode: "live" });
  } catch (e) {
    const detail = e instanceof Error ? e.message : "Unknown error";
    return NextResponse.json(
      {
        reply: `${mockReply(lastUser)}\n\n_(Live mode failed: ${detail}. Falling back to demo.)_`,
        mode: "demo-fallback",
      },
      { status: 200 },
    );
  }
}

export async function GET() {
  return NextResponse.json({
    status: "ok",
    mode: process.env.OPENAI_API_KEY ? "live" : "demo",
  });
}
