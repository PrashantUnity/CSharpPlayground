/**
 * Handles the notifications of one run in the order they were made, one at a time, whatever order they arrive in, and
 * says when everything up to a number has been handled: the engine's reply names the last notification (`lastSeq`) and
 * may arrive before some of them. A notification with no number (an older engine) is handled in the order it arrived.
 */
export class SequencedQueue<T> {
  private next = 1;
  private readonly waiting = new Map<number, T>();
  private draining = false;
  private tail: Promise<void> = Promise.resolve();
  private waiters: Array<{ seq: number; resolve: (done: boolean) => void; timer: NodeJS.Timeout }> = [];

  constructor(private readonly handle: (item: T) => Promise<void>) {}

  push(seq: number | undefined, item: T): void {
    if (!seq || seq < 1) {
      this.tail = this.tail.then(() => this.safely(item));
      return;
    }
    if (seq < this.next) return; // already handled: a repeat
    this.waiting.set(seq, item);
    void this.drain();
  }

  /** Resolves true once every notification up to `seq` has been handled, or false if that has not happened in `timeoutMs`. */
  processed(seq: number, timeoutMs = 10_000): Promise<boolean> {
    if (this.next > seq) return Promise.resolve(true);
    return new Promise((resolve) => {
      const timer = setTimeout(() => {
        this.waiters = this.waiters.filter((w) => w.resolve !== resolve);
        resolve(false);
      }, timeoutMs);
      this.waiters.push({ seq, resolve, timer });
    });
  }

  private async drain(): Promise<void> {
    if (this.draining) return;
    this.draining = true;
    try {
      while (this.waiting.has(this.next)) {
        const item = this.waiting.get(this.next) as T;
        this.waiting.delete(this.next);
        await this.safely(item);
        this.next++;
        this.release();
      }
    } finally {
      this.draining = false;
    }
  }

  private release(): void {
    const done = this.waiters.filter((w) => this.next > w.seq);
    this.waiters = this.waiters.filter((w) => this.next <= w.seq);
    for (const waiter of done) {
      clearTimeout(waiter.timer);
      waiter.resolve(true);
    }
  }

  private async safely(item: T): Promise<void> {
    try {
      await this.handle(item);
    } catch {
      // one notification that cannot be shown must not stop the ones after it
    }
  }
}
