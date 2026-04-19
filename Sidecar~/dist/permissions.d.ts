export declare class SessionTrust {
    private _trusted;
    isTrusted(tool: string): boolean;
    add(tool: string): void;
    reset(): void;
    list(): string[];
}
