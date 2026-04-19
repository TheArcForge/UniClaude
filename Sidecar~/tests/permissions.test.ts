// tests/permissions.test.ts
import { describe, it, beforeEach } from "node:test";
import assert from "node:assert/strict";
import { SessionTrust } from "../src/permissions.js";

describe("SessionTrust", () => {
  let trust: SessionTrust;

  beforeEach(() => {
    trust = new SessionTrust();
  });

  it("returns false for unknown tools", () => {
    assert.equal(trust.isTrusted("Edit"), false);
  });

  it("returns true after adding a tool", () => {
    trust.add("Edit");
    assert.equal(trust.isTrusted("Edit"), true);
  });

  it("does not cross-trust different tools", () => {
    trust.add("Edit");
    assert.equal(trust.isTrusted("Write"), false);
  });

  it("lists all trusted tools", () => {
    trust.add("Edit");
    trust.add("Bash");
    const tools = trust.list();
    assert.deepEqual(tools.sort(), ["Bash", "Edit"]);
  });

  it("resets all trust", () => {
    trust.add("Edit");
    trust.add("Bash");
    trust.reset();
    assert.equal(trust.isTrusted("Edit"), false);
    assert.equal(trust.isTrusted("Bash"), false);
    assert.deepEqual(trust.list(), []);
  });

  it("handles duplicate adds without error", () => {
    trust.add("Edit");
    trust.add("Edit");
    assert.equal(trust.isTrusted("Edit"), true);
    assert.deepEqual(trust.list(), ["Edit"]);
  });
});
