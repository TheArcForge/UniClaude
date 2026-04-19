"use strict";
// src/permissions.ts
Object.defineProperty(exports, "__esModule", { value: true });
exports.SessionTrust = void 0;
class SessionTrust {
    _trusted = new Set();
    isTrusted(tool) {
        return this._trusted.has(tool);
    }
    add(tool) {
        this._trusted.add(tool);
    }
    reset() {
        this._trusted.clear();
    }
    list() {
        return [...this._trusted];
    }
}
exports.SessionTrust = SessionTrust;
//# sourceMappingURL=permissions.js.map