// src/permissions.ts

export class SessionTrust {
  private _trusted: Set<string> = new Set();

  isTrusted(tool: string): boolean {
    return this._trusted.has(tool);
  }

  add(tool: string): void {
    this._trusted.add(tool);
  }

  reset(): void {
    this._trusted.clear();
  }

  list(): string[] {
    return [...this._trusted];
  }
}
