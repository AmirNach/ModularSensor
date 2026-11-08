# Sensor performance: where the time goes and what to fix

Based on the stress test run you pasted (about 700 objects per refresh, two sensors, 50 refreshes) and a reading of the code.
Numbers marked **measured** come from that table. Gains marked **estimate** are my reasoning, not measurements, so check each one after you change it.

## What the numbers say

| Part of a refresh | Avg ms | Share of `Sensor.Refresh` |
|---|---|---|
| `EvaluateTree` | 6.18 | 58% |
| `UpdateDetections` | 2.51 | 23% |
| `CollectCandidates` | 0.98 | 9% |
| `RecordDistances` | 0.70 | 7% |
| everything else (Prepare, Notify, Injectors, events) | 0.14 | 1% |
| **`Sensor.Refresh`** | **10.7** (worst 22.5) | 100% |

`Collection.Scan` is separate: 2.1 ms on average, 5.3 ms worst, once per second per collection, and 85% of it is `LayerAndGetComponentInParent`.

Two things matter more than the average:

1. **It is a hitch, not a steady load.** The whole refresh runs inside one frame. 10.7 ms on average and 22.5 ms at worst is already more than a 60 fps frame (16.6 ms), and that is before the game itself does anything. The scan adds a second hitch every second.
2. **Over half of `EvaluateTree` is not the conditions.** The four leaf conditions add up to about 143 ms of its 309 ms. The rest, roughly 165 ms, is the loop around them: bookkeeping per object, not the checks. Per object that is about 15 µs per refresh, and 2 to 7 µs is the actual condition work.

## The biggest improvements, in order

### 1. Spread the work over several frames (fixes the hitch)

**Evidence:** worst `Refresh` 22.5 ms, worst `Scan` 5.3 ms, both in a single frame.
**Cause:** `RefreshCore` evaluates every object, updates every detection and records every distance in one call.
**Fix:** a time budget per frame (for example 1 to 2 ms). Evaluate the candidates in slices over several frames and commit the result once the last slice is done, so Gained/Lost events still come out as one consistent step. Give each sensor a random start offset so two sensors never refresh in the same frame. Do the same for the scan.
**Gain (estimate):** the worst frame cost drops from about 22 ms to the budget. Average cost stays the same, so do this together with the items below, not instead of them.
**Effort:** medium. Evaluation has to survive a target dying between slices, which the ID and snapshot work already makes safe.

### 2. Stop looking at every object (reduce the count)

**Evidence:** about 700 objects are evaluated per refresh, and the frustum rejects roughly 60% of them (`InFrustum` ran 17,844 times, `WithRaycast` 6,950).
**Cause:** every object in the scene goes through the tree, however far away.
**Fix:** a cheap first cut before the tree. Each condition can report a maximum range (or bounds) it can pass in; the sensor takes the largest, then uses `Physics.OverlapSphereNonAlloc` or a simple grid of positions to select only objects inside it. Objects behind the sensor can be dropped by one dot product before any condition runs.
**Gain (estimate):** proportional to how many objects are out of range. The more of the scene is far away, the bigger the win. With 2,000 objects spread over a large area this is the largest single saving.
**Effort:** medium.

### 3. Cut the per-object bookkeeping in the loop (about half of `EvaluateTree`)

**Evidence:** `EvaluateTree` 309 ms against 143 ms in the leaves, and `Condition.And` costs 217 ms in total although it does almost no work itself.
**Causes found in the code:**
- `Condition.Run` records every leaf result into the `EvaluationTrace` by adding the condition's name to a `HashSet<string>`, for every object, every refresh, hashing the string each time. The trace is only used when something changed, or for the picked object.
- `_trace.Clear()` empties three collections for every object.
- Each object does several string-keyed dictionary operations: `_ids[candidate]`, `ContainsKey(id)`, `_detectedById[id]`, `_seenById`. The identifier is a 36-character GUID in `DetectableEntity`, so each lookup hashes 36 characters.
- `passed != _detectionsCollection.ContainsKey(id)` and a trace snapshot (new object plus two sets) for every state change.

**Fix:**
- Record pass/fail into a small array indexed by the condition's position in the tree (no strings, no hashing) and only turn it into names when an event needs them.
- Give each object a small integer handle once per refresh and use arrays or integer-keyed dictionaries inside the loop. Keep the string ID only at the edges (events, black list).
- Only clear what was written.

**Gain (estimate):** a third to a half of `EvaluateTree`, about 2 to 3 ms of the 6.2.
**Effort:** medium, mostly inside `Sensor` and `Condition.Run`.

### 4. Read each object's data once per refresh

**Evidence:** `RecordDistances` 0.70 ms is pure repeat work, and positions are read again in `UpdateDetections`.
**Cause:** `GetCenterPosition()` on `DetectableEntity` reads `collider.bounds`, which is a call into the engine. In one refresh it is called by `IsAngleClamped`, `WithRaycast`, the probability condition, `RecordDistances`, `UpdateDetections` and `DetectableSnapshot.Update`, plus `Bounds` and `SetBoundsCenter` in the frustum test.
**Fix:** read position, bounds and speed once per object at the start of the refresh into a small struct, and hand that struct to conditions through the context. Compute the distance once and reuse it for `RecordDistances` and `Detection.Refresh`.
**Gain (estimate):** removes `RecordDistances` almost entirely (0.7 ms) and a share of the leaf and `UpdateDetections` time.
**Effort:** low to medium. It touches the `IDetectable` calling pattern, so it needs a compatible path for implementations you do not control.

### 5. Make `UpdateDetections` cheaper (2.5 ms, 23%)

**Evidence:** 125 ms over 50 refreshes.
**Causes found in the code:**
- `DetectableSnapshot.Update` runs for every detected object on every refresh and re-reads text that never changes. `TargetListModel.GetName` reads `component.name`, which creates a new string in Unity on every call. `GetDetectionType()` on `DetectableEntity` is `_entityType.ToString()`, which also creates a string each call. So every detected object creates garbage every refresh.
- `CopyKeys()` copies all keys to a list each refresh, and each key is then looked up again.

**Fix:** read name, type and entity type once when the detection is created, and on later refreshes update only the numbers (position, speed, bounds, last seen). Cache the enum text. Iterate the collection directly instead of copying keys, and mark removals as you go.
**Gain (estimate):** most of the 2.5 ms, and most of the per-refresh garbage.
**Effort:** low.

### 6. Replace the scene scan with registration

**Evidence:** `Collection.Scan` 2.1 ms average, 5.3 ms worst. `FindObjectsByType` itself is cheap (0.3 ms). The cost is `GetComponentInParent<IDetectable>()` called for every collider in the scene (1.77 ms).
**Causes:** the scan looks at every collider, including ones that are not detectable, then builds a new `HashSet` each second. Objects also appear up to one second late.
**Fix:** let detectables register themselves. `DetectableEntity.OnEnable` and `OnDisable` add and remove the object in a shared registry, which costs nothing per second. Offer the same `Register` and `Unregister` calls for the other program's own `IDetectable` objects, and keep the scan as a fallback. Sensors with the same layer mask already share one collection, so the scan runs once per second for all of them.
**Gain (estimate):** the scan hitch and its garbage go away completely.
**Effort:** low for `DetectableEntity`, medium for the other program's objects.

### 7. Make raycasts cheaper and parallel (47 ms, 6,950 calls, about 7 µs each)

**Cause:** the ray ignores no layers by default (`RaycastIgnoreLayerMask` is 0), so every other entity's collider is a possible hit. With 2,000 entities that makes each ray do real work, and it also counts other entities as blockers.
**Fix:** give `WithRaycast` an "obstacle layers" mask (walls and terrain only) as the default. For many rays, collect them and fire them as one `RaycastCommand` batch, which Unity runs on worker threads.
**Gain (estimate):** the layer mask alone can cut cost a lot in a dense scene. Batching moves the remaining cost off the main thread.
**Effort:** low for the mask, high for batching, because the tree is evaluated object by object today.

### 8. Longer term: data-oriented evaluation for the math conditions

`InFrustum`, `IsAngleClamped` and the probability ranges are pure maths (about 2 µs each, 31 to 34 ms each in total). With positions held in arrays they can be evaluated for all objects in one Burst job, at a small fraction of the cost. This is the highest ceiling and the largest redesign, since the tree would need a batch path next to the per-object one.

## Smaller items

- **`Condition.And` max 4.5 ms and `Sensor.Prepare` max 5.3 ms** are spikes far above their averages (0.006 and 0.12 ms). This looks like the first call in a run (compiling the code, allocating the frustum planes) or a garbage collection landing inside them. Re-run with the Unity Profiler and look at the GC.Alloc column to be sure.
- **Order conditions by cost and pass rate.** The tree already stops early, so putting the cheapest condition that rejects the most first is worth it. The frustum is a good first one here. An automatic reorder from measured cost and pass rate is possible.
- **The overlay.** Your run did not show the `Overlay.*` sections, so it was closed. When it is open it rebuilds the list of all objects every 250 ms. Measure it with a few hundred objects before assuming it is free.

## What is not a problem

`FindObjectsByType` (0.3 ms), event handlers (0.4 ms in total), injectors (0.9 ms in total), `NotifyConditions` and the average `Prepare`.

## Limits of these numbers

- **The measuring is not free.** `Condition.And` ran 35,688 times, and each timed call costs a clock read at both ends. The tiny sections (`And`, and the leaf conditions) are inflated by that, so treat them as an upper bound. The totals for `Refresh`, `EvaluateTree` and `UpdateDetections` are less affected. The same sections also appear in the Unity Profiler as markers, where the cost is much smaller.
- **The allocation columns are all 0.0.** The runtime used here does not report allocated bytes through `GC.GetAllocatedBytesForCurrentThread`, so the table says nothing about garbage. The garbage suspects above (strings from `name` and `ToString`, per-scan `HashSet`, trace snapshots) come from reading the code. Confirm them in the Profiler's GC.Alloc column before relying on them.
- It is one run on one machine in the Editor, which is slower than a build. Compare before and after on the same setup rather than reading the absolute milliseconds.

## Suggested order

1. Items 5 and 6 first: small changes, clear savings, and they remove garbage.
2. Items 3 and 4 next: they cut the largest block (`EvaluateTree`) and the repeat reads.
3. Item 2, then item 1 on top, so the hitch is spread over what is left.
4. Items 7 and 8 only if the earlier steps are not enough.

Re-run the stress scene after each step (`Tools > Sensor > Create Stress Test Scene`, then Run benchmark) and compare the `Sensor.Refresh` average and worst numbers.

## Decisions

Items 1 (spreading work over frames) and 8 (Burst) are not being done. The system relies on the sensor's synchronous Unity updates, so both would break it.

## Done: core sensor, same logic

None of this is compiled or measured yet. Re-run the stress scene and compare. The old table also included the cost of timing each condition, so compare with "Time each condition" off and expect `EvaluateTree` to drop partly for that reason.

**Duplicate data removed.** One refresh used to keep the same objects in `_candidates`, `_ids`, `_seenById`, `_detectedById`, `_allDetectedObjects`, `_injected`, `_injectedIds`, `_distances` and `_changeTraces`. All of it is now one array of `RefreshEntry` structs, plus one ID-to-index dictionary.

**Fewer lookups.** ID hashes per object per refresh go from about 8 to 2 (one to catch duplicate IDs, one to find the existing detection). Each entry holds its `Detection`, so the update pass reaches it directly. A dictionary is only walked when a target has disappeared.

**Debug lists built on demand.** `LastCandidates`, `LastInjected`, `LastDistances` and `GetAllDetectedObjects()` are built from the entries when something reads them. With the overlay closed they are never built.

**Less garbage per change.**
- Condition results are a plain list while the tree runs. Name sets are only built when a target is gained or lost.
- A trace snapshot no longer allocates a debug dictionary.
- A sensor detection no longer allocates a `HashSet<Sensor>` it does not use.
- A detector detection shares the sensor's `DetectableSnapshot` instead of allocating its own and reading the target again.

**Fewer engine calls.**
- `SensorContext` holds the sensor's position and rotation once per refresh, and conditions read those.
- Each object's position is read once for distance and snapshot.
- `DetectableEntity` reads `collider.bounds` once per frame and `transform.position` once per update.
- `InFrustum` no longer reads the bounds twice or writes them back when nothing changed.
- `OnDrawGizmos` no longer rebuilds the node list on every repaint while playing.

**Detectable registry.** The scan reads `DetectableRegistry` instead of calling `GetComponentInParent` on every collider, and swaps two sets instead of allocating one. The old search is available through `DetectableRegistry.IncludeUnregisteredSceneObjects`.

**Profiler.** Per-condition timing is now a separate switch, off by default. The allocation columns are gone because they were always 0. The stress report prints garbage per second instead.

### Small behaviour changes
- A snapshot re-reads name, type, entity type and `IsFake` only when the target comes back as a new instance. A rename or type change on the same instance during a detection does not show in the snapshot.
- While two sensors detect a target and the first one loses it, the detector's snapshot stays at that sensor's last values until the last sensor loses it. It used to stay at the values from when the detection began.
- Lost events fire for failed targets first, then for missing ones. They used to follow dictionary order.
- A `DetectableEntity` with no collider is now found, using its own layer.
- A probability condition with "stop rolling" off no longer remembers passes, so switching it on while running starts with a new roll.
- Conditions see the sensor's position from the start of the refresh, even if something moves the sensor during it.

### Measured after this round

| Part of a refresh | Before ms | After ms | Change |
|---|---|---|---|
| **`Sensor.Refresh`** | **10.7** (worst 22.5) | **4.18** (worst 21.2) | -61% |
| `EvaluateTree` | 6.18 | 2.60 | -58% (partly the condition timer being off) |
| `CollectCandidates` | 0.98 | 0.89 | -9% |
| `UpdateDetections` | 2.51 | 0.39 | -85% |
| `RecordDistances` | 0.70 | 0.21 | -70% |

`EvaluateTree` is now 62% of a refresh and `CollectCandidates` 21%. The average fell but the worst refresh did not, so the remaining spikes come from refreshes with many gains or losses at once (see below).

## Done: second round, same logic

Not compiled or measured yet.

**Each target is read once per refresh.** The sensor reads the target's position when it makes the entry and hands it to the conditions through `SensorContext.PositionOf(target)` and `SensorContext.IdOf(target)`. Built-in conditions used to call `GetCenterPosition()` 2 to 4 times per target (angle, probability, frustum, raycast, then the distance), each time going through the interface, a Unity null check and `Time.frameCount`. Now it is once. `RecordDistances` is arithmetic only. For a target other than the one being evaluated, both methods fall back to asking the target, so a custom condition and a test that builds its own context behave as before.

**Raycast without the engine round trip.** On a hit, `WithRaycast` used to call `hit.collider`, `.transform` twice and `IsChildOf`, four calls into the engine per raycast. A target that implements the new optional `IDetectableCollider` (`DetectableEntity` does) gives its collider's instance ID, and a hit on that collider passes by comparing `hit.colliderInstanceID`. Any other hit still goes through `IsChildOf`, so the result is the same.

**Detections found from the detections' side.** Matching existing detections to the entries used to hash every object's ID. When fewer objects are detected than seen, which is the usual case, the sensor walks the detections instead: one hash per detection rather than one per object. Injected targets still look themselves up.

**Black list without hashing.** A sensor usually ignores only its own object. That made every object's ID hashed a third time. With up to 4 ignored IDs they are compared directly.

**Less garbage per gain or loss.** A trace copy kept by a detection no longer allocates two empty lists. `IsMoving` no longer computes its visual's position when the visual is off.

### Still open
- **Spikes from many changes at once.** Each gained or lost target still allocates a trace copy with two `HashSet<string>`, a `Detection` and a `DetectableSnapshot`, and fires events. Most targets pass the same conditions, so the name sets could be shared, but `Detection.AllPassedConditions` is a public, writable `HashSet`, so sharing them is an API change.
- **Raycasts are still one at a time.** `RaycastCommand` with an immediate `Complete()` stays synchronous and runs the casts in parallel. It needs the tree split into a "decide who needs a ray" pass and a "use the results" pass, which changes how the tree short-circuits.
- **The overlay costs 23.6 ms on each of its refreshes (worst 81.8).** That is a frame spike every time it updates, larger than one sensor refresh. `UpdateList` is most of it.
- Attempts and passed rolls in `ProbabilityRollCondition` are kept for objects that were never detected and later destroyed, so they grow with the number of distinct IDs.
- `DetectableEntity.Update` still runs on every object every frame.
- The string ID hash is still paid once per object to catch duplicate IDs. Avoiding it needs a stable integer handle on `IDetectable`, which is an API change.
