import { Component, type ReactNode } from "react";

// Contains a crash to the screen it happened in: the header, navigation and every other screen keep
// working, and the cashier gets a clear message with a retry instead of a blank white page.
type Props = {
  language: "ar" | "en";
  children: ReactNode;
};
type State = { failed: boolean };

export class ScreenBoundary extends Component<Props, State> {
  state: State = { failed: false };

  static getDerivedStateFromError(): State {
    return { failed: true };
  }

  componentDidCatch(error: unknown) {
    console.error("Screen failed to render", error);
  }

  render() {
    if (!this.state.failed) return this.props.children;
    const ar = this.props.language === "ar";
    return (
      <div
        role="alert"
        className="mx-auto mt-10 max-w-lg rounded-xl border border-destructive/40 bg-destructive/10 p-6 text-center"
      >
        <p className="font-semibold text-destructive">
          {ar ? "تعذر عرض هذه الشاشة." : "This screen could not be displayed."}
        </p>
        <p className="mt-2 text-sm text-muted-foreground">
          {ar
            ? "باقي النظام يعمل. أعد المحاولة، وإذا تكرر الخطأ فتحقق من الاتصال بالخادم."
            : "The rest of the system still works. Try again; if it keeps happening, check the server connection."}
        </p>
        <button
          type="button"
          onClick={() => this.setState({ failed: false })}
          className="mt-4 min-h-11 rounded-lg bg-primary px-5 text-sm font-semibold text-primary-foreground"
        >
          {ar ? "إعادة المحاولة" : "Try again"}
        </button>
      </div>
    );
  }
}
