"use client";

import { useCallback, useId, useMemo, useState } from "react";

export interface TodoItem {
  id: string;
  text: string;
  done: boolean;
}

export interface TodoListProps {
  initialItems?: TodoItem[];
  onChange?: (items: TodoItem[]) => void;
}

const MAX_TEXT_LENGTH = 200;

function makeId(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return `todo-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

export function TodoList({ initialItems = [], onChange }: TodoListProps) {
  const [items, setItems] = useState<TodoItem[]>(initialItems);
  const [draft, setDraft] = useState("");
  const inputId = useId();

  const commit = useCallback(
    (next: TodoItem[]) => {
      setItems(next);
      onChange?.(next);
    },
    [onChange],
  );

  const handleAdd = useCallback(() => {
    const text = draft.trim().slice(0, MAX_TEXT_LENGTH);
    if (!text) return;
    commit([...items, { id: makeId(), text, done: false }]);
    setDraft("");
  }, [draft, items, commit]);

  const handleToggle = useCallback(
    (id: string) => {
      commit(items.map((it) => (it.id === id ? { ...it, done: !it.done } : it)));
    },
    [items, commit],
  );

  const handleRemove = useCallback(
    (id: string) => {
      commit(items.filter((it) => it.id !== id));
    },
    [items, commit],
  );

  const remaining = useMemo(() => items.filter((it) => !it.done).length, [items]);

  return (
    <section aria-label="Todo list" data-testid="todo-list" className="todo-list">
      <div className="todo-list__compose">
        <label htmlFor={inputId} className="todo-list__label">
          New task
        </label>
        <input
          id={inputId}
          type="text"
          value={draft}
          maxLength={MAX_TEXT_LENGTH}
          placeholder="What needs doing?"
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              e.preventDefault();
              handleAdd();
            }
          }}
          data-testid="todo-input"
        />
        <button
          type="button"
          onClick={handleAdd}
          disabled={!draft.trim()}
          data-testid="todo-add"
        >
          Add
        </button>
      </div>

      {items.length === 0 ? (
        <p className="todo-list__empty" data-testid="todo-empty">
          No tasks yet.
        </p>
      ) : (
        <ul className="todo-list__items" data-testid="todo-items">
          {items.map((it) => (
            <li key={it.id} data-testid={`todo-item-${it.id}`} className="todo-list__item">
              <label>
                <input
                  type="checkbox"
                  checked={it.done}
                  onChange={() => handleToggle(it.id)}
                  aria-label={`Mark "${it.text}" as ${it.done ? "not done" : "done"}`}
                />
                <span
                  className={it.done ? "todo-list__text todo-list__text--done" : "todo-list__text"}
                  style={it.done ? { textDecoration: "line-through", opacity: 0.6 } : undefined}
                >
                  {it.text}
                </span>
              </label>
              <button
                type="button"
                onClick={() => handleRemove(it.id)}
                aria-label={`Remove "${it.text}"`}
              >
                Remove
              </button>
            </li>
          ))}
        </ul>
      )}

      <p className="todo-list__status" data-testid="todo-remaining" aria-live="polite">
        {remaining} remaining
      </p>
    </section>
  );
}

export default TodoList;
