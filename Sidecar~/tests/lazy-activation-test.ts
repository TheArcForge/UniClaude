// tests/lazy-activation-test.ts
// Standalone API validation for lazy tool activation.
// Uses the Agent SDK (inherits Claude Code auth — no API key needed).
// Run: cd Sidecar~ && npx tsx tests/lazy-activation-test.ts

import { query, createSdkMcpServer, tool } from "@anthropic-ai/claude-agent-sdk";

const SYSTEM_PROMPT =
  "You are an AI assistant embedded in the Unity editor. You have access to tools for modifying the Unity project. Use available tools when the user's request requires editor operations.";

const TOOL_DESCRIPTION = [
  "Enable Unity editor tools for direct manipulation of the Unity project.",
  "Call this ONLY when you need to perform an action in the Unity editor:",
  "- Create/modify/delete GameObjects, scenes, or prefabs",
  "- Add/remove/configure components and their properties",
  "- Create/edit assets, materials, or animations",
  "- Read/write/delete project files on disk",
  "- Run tests or read console output",
  "",
  "Do NOT call this tool for:",
  "- Explaining Unity concepts, APIs, or best practices",
  "- Writing or reviewing C# code or scripts",
  "- Answering questions about how Unity features work",
  "- Designing systems, architectures, or game logic",
  "- Any request that can be answered with knowledge alone",
].join("\n");

interface TestCase {
  message: string;
  expectActivation: boolean;
}

const TEST_CASES: TestCase[] = [
  // ── True positives — should call enable_unity_tools (20) ──
  { message: "Create a cube in the scene", expectActivation: true },
  { message: "Add a Rigidbody to the player object", expectActivation: true },
  { message: "Open the Main Menu scene", expectActivation: true },
  { message: "Set the camera's FOV to 90", expectActivation: true },
  { message: "Create a red material and apply it to the floor", expectActivation: true },
  { message: "Delete all empty GameObjects", expectActivation: true },
  { message: "Run the EditMode tests", expectActivation: true },
  { message: "Show me the console errors", expectActivation: true },
  { message: "Create a prefab from the Player object", expectActivation: true },
  { message: "Add a PointLight as child of Room", expectActivation: true },
  { message: "Rename the Player GameObject to Hero", expectActivation: true },
  { message: "Set the skybox material to night sky", expectActivation: true },
  { message: "Import this texture into the Assets folder", expectActivation: true },
  { message: "Move the Camera to position (0, 10, -5)", expectActivation: true },
  { message: "Add a BoxCollider to all walls", expectActivation: true },
  { message: "Create a new empty scene called TestLevel", expectActivation: true },
  { message: "Check what components are on the Enemy object", expectActivation: true },
  { message: "Apply the overrides on the Player prefab", expectActivation: true },
  { message: "Assign the WalkAnimation clip to the Animator", expectActivation: true },
  { message: "Duplicate the Level1 scene", expectActivation: true },

  // ── True negatives — should NOT call enable_unity_tools (20) ──
  { message: "Explain how NavMesh works in Unity", expectActivation: false },
  { message: "What's the difference between Update and FixedUpdate?", expectActivation: false },
  { message: "Write a singleton pattern in C#", expectActivation: false },
  { message: "How do coroutines work?", expectActivation: false },
  { message: "Review this code for bugs", expectActivation: false },
  { message: "What's the best way to handle input in Unity?", expectActivation: false },
  { message: "Explain the Unity rendering pipeline", expectActivation: false },
  { message: "Help me design an inventory system", expectActivation: false },
  { message: "What does SerializeField do?", expectActivation: false },
  { message: "How should I structure my project folders?", expectActivation: false },
  { message: "What are ScriptableObjects and when should I use them?", expectActivation: false },
  { message: "Explain the difference between Awake and Start", expectActivation: false },
  { message: "Write a health bar script using UI Toolkit", expectActivation: false },
  { message: "How does Unity's garbage collector work?", expectActivation: false },
  { message: "What's the best practice for object pooling?", expectActivation: false },
  { message: "Explain quaternion rotation in simple terms", expectActivation: false },
  { message: "How do I optimize draw calls in my game?", expectActivation: false },
  { message: "Write a state machine for enemy AI", expectActivation: false },
  { message: "What's the difference between physics layers and sorting layers?", expectActivation: false },
  { message: "How do I implement save/load functionality?", expectActivation: false },
];

async function runTest(tc: TestCase): Promise<boolean> {
  let calledTool = false;
  const abortController = new AbortController();

  const metaServer = createSdkMcpServer({
    name: "uniclaude-meta",
    tools: [
      tool("enable_unity_tools", TOOL_DESCRIPTION, {}, async () => {
        calledTool = true;
        return { content: [{ type: "text" as const, text: "Unity editor tools enabled." }] };
      }),
    ],
  });

  try {
    const conversation = query({
      prompt: tc.message,
      options: {
        model: "claude-sonnet-4-20250514",
        systemPrompt: { type: "preset" as const, preset: "claude_code" as const, append: SYSTEM_PROMPT },
        tools: [
          "Read", "Bash", "Grep", "Glob", "Agent",
          "TodoRead", "TodoWrite",
          "TaskCreate", "TaskUpdate", "TaskGet", "TaskList", "TaskOutput", "TaskStop",
          "NotebookEdit", "WebFetch", "WebSearch",
        ],
        mcpServers: { "uniclaude-meta": metaServer },
        abortController,
        canUseTool: async (_tool: string, input: Record<string, unknown>) => ({
          behavior: "allow" as const,
          updatedInput: input,
        }),
      },
    });

    for await (const msg of conversation) {
      const message = msg as Record<string, unknown>;
      if (message.type === "result") break;
    }
  } catch {
    if (!abortController.signal.aborted) {
      return false;
    }
  }

  return calledTool === tc.expectActivation;
}

async function main() {
  console.log("Lazy Tool Activation — API Validation Test (Agent SDK)");
  console.log("=".repeat(60));
  console.log();

  let passed = 0;
  let tpPass = 0;
  let tnPass = 0;
  let tpTotal = 0;
  let tnTotal = 0;

  for (const tc of TEST_CASES) {
    const ok = await runTest(tc);
    if (ok) passed++;

    if (tc.expectActivation) {
      tpTotal++;
      if (ok) tpPass++;
    } else {
      tnTotal++;
      if (ok) tnPass++;
    }

    const expected = tc.expectActivation ? "ACTIVATE" : "SKIP";
    const actual = ok ? expected : (tc.expectActivation ? "SKIP" : "ACTIVATE");
    const status = ok ? "PASS" : "FAIL";
    console.log(`  [${status}] ${tc.message}`);
    console.log(`         expected: ${expected}  actual: ${actual}`);
  }

  console.log();
  console.log("=".repeat(60));
  const pct = ((passed / TEST_CASES.length) * 100).toFixed(0);
  console.log(`Result: ${passed}/${TEST_CASES.length} (${pct}%)`);
  console.log(`  True positives:  ${tpPass}/${tpTotal}`);
  console.log(`  True negatives:  ${tnPass}/${tnTotal}`);
  console.log(
    Number(pct) >= 95
      ? "PASS — Model reliably distinguishes tool vs. non-tool requests"
      : "FAIL — Model does not meet 95% reliability threshold"
  );

  process.exit(Number(pct) >= 95 ? 0 : 1);
}

main();
