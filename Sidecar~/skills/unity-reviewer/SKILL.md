---
name: unity-reviewer
description: "Use after implementation to review code changes for Unity anti-patterns, performance issues, and correctness problems. Also use when superpowers:requesting-code-review is active to provide Unity-specific review criteria."
---

# Unity Code Reviewer

Run through these checks after modifying or creating C# scripts. Organized by severity.

## Critical (Will Break at Runtime)

### CHECK: Missing Event Unsubscription

**Wrong (common default):**
```csharp
void OnEnable() {
    GameEvents.OnPlayerDied += HandlePlayerDied;
}
// No OnDisable — listener persists after destroy, causes NullReferenceException
```

**Right:**
```csharp
void OnEnable() {
    GameEvents.OnPlayerDied += HandlePlayerDied;
}
void OnDisable() {
    GameEvents.OnPlayerDied -= HandlePlayerDied;
}
```

**Why it matters:** Leaked subscriptions cause NullReferenceExceptions when the destroyed object's method is called. One of the most common Unity bugs.

### CHECK: Unity Fake Null

**Wrong (common default):**
```csharp
var go = GetComponent<SomeComponent>();
var result = go?.DoThing(); // C# null check — DOES NOT catch Unity destroyed objects
```

**Right:**
```csharp
var go = GetComponent<SomeComponent>();
if (go != null) go.DoThing(); // Unity's == override catches destroyed objects
```

**Why it matters:** Unity overrides == to return true for destroyed objects, but C#'s ?. and ?? bypass this override. Object appears non-null to C# but is actually destroyed.

### CHECK: Accessing Unity API from Background Thread

**Wrong:**
```csharp
async Task LoadDataAsync() {
    var data = await FetchFromServer();
    transform.position = data.position; // CRASH: not on main thread
}
```

**Right:**
```csharp
async Task LoadDataAsync() {
    var data = await FetchFromServer();
    await Awaitable.MainThreadAsync(); // Switch back to main thread
    transform.position = data.position;
}
```

**Why it matters:** Unity API is not thread-safe. Accessing transforms, GameObjects, or components from a background thread causes crashes or silent corruption.

### CHECK: Destroy vs DestroyImmediate

**Wrong:**
```csharp
void CleanUp() {
    DestroyImmediate(gameObject); // Dangerous outside editor scripts
}
```

**Right:**
```csharp
void CleanUp() {
    Destroy(gameObject); // Deferred to end of frame, safe
}
```

**Why it matters:** DestroyImmediate removes the object mid-frame, breaking iteration and other components that reference it. Only valid in editor scripts.

## Performance (Tanks Framerate)

### CHECK: GetComponent in Update

**Wrong (common default):**
```csharp
void Update() {
    GetComponent<Rigidbody>().AddForce(Vector3.up); // Lookup every frame
}
```

**Right:**
```csharp
Rigidbody _rb;
void Awake() { _rb = GetComponent<Rigidbody>(); }
void Update() { _rb.AddForce(Vector3.up); }
```

**Why it matters:** GetComponent uses reflection-based lookup. In a hot loop with many objects, this adds up to milliseconds per frame.

### CHECK: GameObject.Find in Update

**Wrong:**
```csharp
void Update() {
    var player = GameObject.Find("Player"); // Linear scan every frame
    transform.LookAt(player.transform);
}
```

**Right:**
```csharp
[SerializeField] Transform _playerTransform; // Set via reference_set or Inspector
void Update() { transform.LookAt(_playerTransform); }
```

**Why it matters:** Find scans the entire scene hierarchy every call. O(n) per frame. Use serialized references set at edit time.

### CHECK: Allocations in Hot Paths

**Wrong (common default):**
```csharp
void Update() {
    var hits = Physics.RaycastAll(transform.position, transform.forward); // Allocates array
    var name = "Player_" + id.ToString(); // Allocates string
    var filtered = enemies.Where(e => e.IsAlive).ToList(); // LINQ allocates
}
```

**Right:**
```csharp
RaycastHit[] _hits = new RaycastHit[32]; // Pre-allocated
void Update() {
    int count = Physics.RaycastNonAlloc(transform.position, transform.forward, _hits);
}
```

**Why it matters:** Every allocation in Update contributes to GC pressure. When GC runs, it causes frame spikes (10-50ms stutters on mobile).

### CHECK: Inappropriate Update vs FixedUpdate

**Wrong:**
```csharp
void Update() {
    _rb.MovePosition(transform.position + dir * speed * Time.deltaTime); // Physics in Update
}
void FixedUpdate() {
    if (Input.GetKeyDown(KeyCode.Space)) Jump(); // Input in FixedUpdate
}
```

**Right:**
```csharp
void Update() {
    if (Input.GetKeyDown(KeyCode.Space)) _shouldJump = true; // Capture input in Update
}
void FixedUpdate() {
    _rb.MovePosition(transform.position + dir * speed * Time.fixedDeltaTime); // Physics in FixedUpdate
    if (_shouldJump) { Jump(); _shouldJump = false; }
}
```

**Why it matters:** Physics operations in Update cause jitter (frame-rate dependent). Input in FixedUpdate misses key presses (FixedUpdate doesn't run every frame).

### CHECK: String Operations in Hot Paths

**Wrong:**
```csharp
void Update() {
    _label.text = "Score: " + score.ToString(); // Allocates every frame
}
```

**Right:**
```csharp
int _lastScore = -1;
void Update() {
    if (score != _lastScore) {
        _label.text = $"Score: {score}"; // Only when changed
        _lastScore = score;
    }
}
```

**Why it matters:** String concatenation allocates. If the value hasn't changed, don't rebuild the string.

## Style (Maintainability Issues)

### CHECK: Public Fields for Inspector

**Wrong (common default):**
```csharp
public float speed = 5f;
public int maxHealth = 100;
```

**Right:**
```csharp
[SerializeField] float _speed = 5f;
[SerializeField] int _maxHealth = 100;
```

**Why it matters:** Public fields expose implementation details. Any script can modify them, making bugs hard to trace. SerializeField gives Inspector access without public exposure.

### CHECK: MonoBehaviour Lifecycle Order

**Wrong:**
```csharp
public class Enemy : MonoBehaviour {
    void Update() { ... }
    void Start() { ... }
    void Awake() { ... }
    void OnDestroy() { ... }
    void OnEnable() { ... }
}
```

**Right:**
```csharp
public class Enemy : MonoBehaviour {
    void Awake() { ... }
    void OnEnable() { ... }
    void Start() { ... }
    void Update() { ... }
    void OnDisable() { ... }
    void OnDestroy() { ... }
}
```

**Why it matters:** Following Unity's execution order in code makes the lifecycle obvious to readers. Consistent across the codebase.

### CHECK: Magic Numbers

**Wrong:**
```csharp
if (health < 20) ShowWarning(); // What is 20?
rb.AddForce(Vector3.up * 9.81f); // Why this value?
```

**Right:**
```csharp
[SerializeField] float _lowHealthThreshold = 20f;
[SerializeField] float _jumpForce = 9.81f;

if (health < _lowHealthThreshold) ShowWarning();
rb.AddForce(Vector3.up * _jumpForce);
```

**Why it matters:** Magic numbers are untunable by designers, undocumented, and scattered. Serialized fields are visible, named, and adjustable without code changes.

## Review Process

When reviewing code after implementation:

1. Scan for Critical checks first — these are bugs that will crash or corrupt.
2. Scan for Performance checks — these cause visible degradation.
3. Scan for Style checks — these cause maintainability debt.
4. Report findings grouped by severity.
5. Fix Critical issues immediately. Performance and Style issues can be batched.

## Related Skills

- uniclaude:unity-performance — deep performance analysis beyond code review
- uniclaude:component-design — proper component structure and lifecycle
