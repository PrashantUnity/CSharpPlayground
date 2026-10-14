// A small typed event, shaped like vscode.Event so the sidecar client needs no vscode import and runs in plain Node.

export interface Disposable {
  dispose(): void;
}

export type Event<T> = (listener: (value: T) => void) => Disposable;

export class Emitter<T> {
  private readonly listeners = new Set<(value: T) => void>();

  readonly event: Event<T> = (listener) => {
    this.listeners.add(listener);
    return { dispose: () => this.listeners.delete(listener) };
  };

  fire(value: T): void {
    for (const listener of [...this.listeners]) {
      try {
        listener(value);
      } catch {
        // one listener's failure must not stop the others
      }
    }
  }

  dispose(): void {
    this.listeners.clear();
  }
}
