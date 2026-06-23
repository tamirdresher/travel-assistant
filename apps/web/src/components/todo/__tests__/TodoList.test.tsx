// @vitest-environment jsdom
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { TodoList } from "../TodoList";

describe("TodoList", () => {
  it("renders empty state initially", () => {
    render(<TodoList />);
    expect(screen.getByTestId("todo-empty")).toBeInTheDocument();
    expect(screen.getByTestId("todo-remaining")).toHaveTextContent("0 remaining");
  });

  it("adds a task via the Add button", () => {
    const onChange = vi.fn();
    render(<TodoList onChange={onChange} />);
    fireEvent.change(screen.getByTestId("todo-input"), { target: { value: "buy milk" } });
    fireEvent.click(screen.getByTestId("todo-add"));

    expect(screen.getByText("buy milk")).toBeInTheDocument();
    expect(screen.getByTestId("todo-remaining")).toHaveTextContent("1 remaining");
    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange.mock.calls[0][0][0]).toMatchObject({ text: "buy milk", done: false });
  });

  it("adds a task on Enter key", () => {
    render(<TodoList />);
    const input = screen.getByTestId("todo-input");
    fireEvent.change(input, { target: { value: "ship code" } });
    fireEvent.keyDown(input, { key: "Enter" });
    expect(screen.getByText("ship code")).toBeInTheDocument();
  });

  it("ignores empty/whitespace-only input", () => {
    render(<TodoList />);
    fireEvent.change(screen.getByTestId("todo-input"), { target: { value: "   " } });
    expect(screen.getByTestId("todo-add")).toBeDisabled();
    fireEvent.click(screen.getByTestId("todo-add"));
    expect(screen.getByTestId("todo-empty")).toBeInTheDocument();
  });

  it("toggles done state and updates remaining count", () => {
    render(
      <TodoList
        initialItems={[
          { id: "a", text: "one", done: false },
          { id: "b", text: "two", done: false },
        ]}
      />,
    );
    expect(screen.getByTestId("todo-remaining")).toHaveTextContent("2 remaining");
    fireEvent.click(screen.getByRole("checkbox", { name: /Mark "one" as done/ }));
    expect(screen.getByTestId("todo-remaining")).toHaveTextContent("1 remaining");
  });

  it("removes a task", () => {
    render(<TodoList initialItems={[{ id: "a", text: "drop me", done: false }]} />);
    fireEvent.click(screen.getByRole("button", { name: /Remove "drop me"/ }));
    expect(screen.queryByText("drop me")).not.toBeInTheDocument();
    expect(screen.getByTestId("todo-empty")).toBeInTheDocument();
  });

  it("trims whitespace and caps length on add", () => {
    render(<TodoList />);
    const long = "x".repeat(300);
    fireEvent.change(screen.getByTestId("todo-input"), { target: { value: `  ${long}  ` } });
    fireEvent.click(screen.getByTestId("todo-add"));
    const items = screen.getAllByRole("listitem");
    expect(items).toHaveLength(1);
    expect(items[0].textContent).toContain("x".repeat(200));
    expect(items[0].textContent).not.toContain("x".repeat(201));
  });
});
