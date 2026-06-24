"use client";

import { FormEvent, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { SUPPORT_CATEGORIES } from "./HelpCategoryGrid";

type FieldName = "name" | "email" | "topic" | "message";
type Errors = Partial<Record<FieldName, string>>;

const VALIDATION_MESSAGES = {
  name: "Name is required.",
  emailRequired: "Email address is required.",
  emailInvalid: "Enter a valid email address.",
  topic: "Topic is required.",
  messageRequired: "Message is required.",
  messageMin: "Message must be at least 20 characters.",
} as const;

export function ContactForm() {
  const router = useRouter();
  const [errors, setErrors] = useState<Errors>({});
  const nameRef = useRef<HTMLInputElement>(null);
  const emailRef = useRef<HTMLInputElement>(null);
  const topicRef = useRef<HTMLSelectElement>(null);
  const messageRef = useRef<HTMLTextAreaElement>(null);

  function validate(formData: FormData) {
    const nextErrors: Errors = {};
    const name = String(formData.get("name") ?? "").trim();
    const email = String(formData.get("email") ?? "").trim();
    const topic = String(formData.get("topic") ?? "");
    const message = String(formData.get("message") ?? "").trim();

    if (!name) nextErrors.name = VALIDATION_MESSAGES.name;
    if (!email) {
      nextErrors.email = VALIDATION_MESSAGES.emailRequired;
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      nextErrors.email = VALIDATION_MESSAGES.emailInvalid;
    }
    if (!topic) nextErrors.topic = VALIDATION_MESSAGES.topic;
    if (!message) {
      nextErrors.message = VALIDATION_MESSAGES.messageRequired;
    } else if (message.length < 20) {
      nextErrors.message = VALIDATION_MESSAGES.messageMin;
    }

    return nextErrors;
  }

  function focusFirstInvalid(nextErrors: Errors) {
    const firstInvalid = (Object.keys(nextErrors) as FieldName[])[0];
    const refs = {
      name: nameRef,
      email: emailRef,
      topic: topicRef,
      message: messageRef,
    };
    if (firstInvalid) refs[firstInvalid].current?.focus();
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const formData = new FormData(form);
    const nextErrors = validate(formData);

    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      focusFirstInvalid(nextErrors);
      return;
    }

    console.log("Support request submitted", Object.fromEntries(formData));
    router.push("/support/contact/success");
  }

  return (
    <form
      noValidate
      onSubmit={handleSubmit}
      className="space-y-5 rounded-3xl border border-zinc-200 bg-white p-5 shadow-sm sm:p-8"
    >
      <FieldError id="form-error" message={null} />
      <div>
        <label
          htmlFor="name"
          className="block text-sm font-medium text-zinc-950"
        >
          Name, required
        </label>
        <input
          ref={nameRef}
          id="name"
          name="name"
          type="text"
          autoComplete="name"
          required
          aria-invalid={Boolean(errors.name)}
          aria-describedby={errors.name ? "name-error" : undefined}
          className="mt-2 min-h-11 w-full rounded-xl border border-zinc-300 px-4 text-base text-zinc-950 outline-none transition focus:border-sky-600 focus:ring-4 focus:ring-sky-100"
        />
        <FieldError id="name-error" message={errors.name} />
      </div>

      <div>
        <label
          htmlFor="email"
          className="block text-sm font-medium text-zinc-950"
        >
          Email address, required
        </label>
        <input
          ref={emailRef}
          id="email"
          name="email"
          type="email"
          autoComplete="email"
          required
          aria-invalid={Boolean(errors.email)}
          aria-describedby={errors.email ? "email-error" : undefined}
          className="mt-2 min-h-11 w-full rounded-xl border border-zinc-300 px-4 text-base text-zinc-950 outline-none transition focus:border-sky-600 focus:ring-4 focus:ring-sky-100"
        />
        <FieldError id="email-error" message={errors.email} />
      </div>

      <div>
        <label
          htmlFor="topic"
          className="block text-sm font-medium text-zinc-950"
        >
          Topic, required
        </label>
        <select
          ref={topicRef}
          id="topic"
          name="topic"
          required
          defaultValue=""
          aria-invalid={Boolean(errors.topic)}
          aria-describedby={errors.topic ? "topic-error" : undefined}
          className="mt-2 min-h-11 w-full rounded-xl border border-zinc-300 bg-white px-4 text-base text-zinc-950 outline-none transition focus:border-sky-600 focus:ring-4 focus:ring-sky-100"
        >
          <option value="" disabled>
            Select a topic
          </option>
          {SUPPORT_CATEGORIES.map((category) => (
            <option key={category.slug} value={category.slug}>
              {category.title}
            </option>
          ))}
        </select>
        <FieldError id="topic-error" message={errors.topic} />
      </div>

      <div>
        <label
          htmlFor="message"
          className="block text-sm font-medium text-zinc-950"
        >
          Message, required
        </label>
        <textarea
          ref={messageRef}
          id="message"
          name="message"
          required
          minLength={20}
          rows={6}
          aria-invalid={Boolean(errors.message)}
          aria-describedby={errors.message ? "message-error" : "message-hint"}
          className="mt-2 w-full rounded-xl border border-zinc-300 px-4 py-3 text-base text-zinc-950 outline-none transition focus:border-sky-600 focus:ring-4 focus:ring-sky-100"
        />
        <p id="message-hint" className="mt-2 text-sm text-zinc-500">
          Minimum 20 characters.
        </p>
        <FieldError id="message-error" message={errors.message} />
      </div>

      <div>
        <label
          htmlFor="tripReference"
          className="block text-sm font-medium text-zinc-950"
        >
          Trip reference, optional
        </label>
        <input
          id="tripReference"
          name="tripReference"
          type="text"
          className="mt-2 min-h-11 w-full rounded-xl border border-zinc-300 px-4 text-base text-zinc-950 outline-none transition focus:border-sky-600 focus:ring-4 focus:ring-sky-100"
        />
      </div>

      <button
        type="submit"
        className="min-h-11 w-full rounded-xl bg-zinc-950 px-5 text-base font-medium text-white transition hover:bg-zinc-800 focus:outline-none focus:ring-4 focus:ring-sky-100"
      >
        Send message
      </button>
    </form>
  );
}

function FieldError({ id, message }: { id: string; message?: string | null }) {
  if (!message) return null;

  return (
    <p id={id} role="alert" className="mt-2 text-sm font-medium text-red-700">
      {message}
    </p>
  );
}
