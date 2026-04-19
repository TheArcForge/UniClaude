// Sidecar~/tests/attachments.test.ts
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { buildContentBlocks } from "../src/agent.js";
import type { ChatAttachment } from "../src/types.js";

describe("buildContentBlocks", () => {
  it("returns null when no attachments", () => {
    assert.equal(buildContentBlocks("hello", undefined), null);
    assert.equal(buildContentBlocks("hello", []), null);
  });

  it("builds text block for text attachment", () => {
    const attachments: ChatAttachment[] = [
      { type: "text", fileName: "test.cs", content: "class Test {}" },
    ];
    const blocks = buildContentBlocks("review this", attachments)!;

    assert.equal(blocks.length, 2);
    // First block: attachment
    assert.equal(blocks[0].type, "text");
    assert.ok((blocks[0] as { text: string }).text.includes("test.cs"));
    assert.ok((blocks[0] as { text: string }).text.includes("class Test {}"));
    // Last block: user message
    assert.equal(blocks[1].type, "text");
    assert.equal((blocks[1] as { text: string }).text, "review this");
  });

  it("builds image block for image attachment", () => {
    const attachments: ChatAttachment[] = [
      { type: "image", fileName: "screenshot.png", content: "aGVsbG8=", mediaType: "image/png" },
    ];
    const blocks = buildContentBlocks("what is this", attachments)!;

    assert.equal(blocks.length, 2);
    assert.equal(blocks[0].type, "image");
    const source = (blocks[0] as { source: { type: string; media_type: string; data: string } }).source;
    assert.equal(source.type, "base64");
    assert.equal(source.media_type, "image/png");
    assert.equal(source.data, "aGVsbG8=");
  });

  it("puts user text last", () => {
    const attachments: ChatAttachment[] = [
      { type: "text", fileName: "a.cs", content: "aaa" },
      { type: "image", fileName: "b.png", content: "data", mediaType: "image/png" },
    ];
    const blocks = buildContentBlocks("my question", attachments)!;

    assert.equal(blocks.length, 3);
    assert.equal(blocks[2].type, "text");
    assert.equal((blocks[2] as { text: string }).text, "my question");
  });

  it("handles empty message with attachments", () => {
    const attachments: ChatAttachment[] = [
      { type: "text", fileName: "test.cs", content: "code" },
    ];
    const blocks = buildContentBlocks("", attachments)!;

    // Only the attachment block, no empty text block
    assert.equal(blocks.length, 1);
    assert.equal(blocks[0].type, "text");
  });
});

