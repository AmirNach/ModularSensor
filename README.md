# ModularSensor

A drag-and-drop detection sensor for Unity. Put a component on a sensor, mark things as detectable, and build the rules in an inspector tree with AND, OR and NOT. Adding a new rule takes one small class.

- **Visual condition tree.** Combine conditions with AND / OR / NOT. Search, drag, wrap and duplicate nodes, and see a readable summary of the whole expression.
- **One refresh rate per sensor.** The tree is evaluated once per refresh, never less than every 0.5 seconds.
- **Short-circuit evaluation.** The tree is walked depth-first and stops as soon as the answer is known, so cheap checks placed first protect expensive ones such as raycasts.
- **Easy to extend.** A new condition is one class. No enum entry, no registration, no settings code.
- **Condition visuals.** Switch on "Show visual" on a single condition to draw what it did, such as the raycast line from the sensor to each object.
- **Live debugging.** In Play mode, the objects a sensor or detector looks at are grouped as passed, failed and injected, in collapsible lists with a filter by name, so hundreds of objects stay manageable. Pick one to see which conditions passed, failed or were never reached, for one sensor or for every sensor of a detector.
- **Merge-friendly.** The tree lives in a ScriptableObject, not in the scene.
- **No third-party packages.**

Built and tested on Unity **6000.3.25f1**. The example scene is created for this project's URP setup.

## Quick start

1. **Make things detectable.** Add the `DetectableEntity` component to any object that has a collider, and pick its entity type.
2. **Create settings.** `Assets > Create > Sensor > Sensor Settings`.
3. **Add a sensor.** Add the `Sensor` component to an object (or `SensorWithCamera` if it should see through a camera) and assign the settings. The **New** button next to the field creates and assigns an asset for you.
4. **Build the tree.** Select the settings asset and add conditions with **+ Add condition**. The tree shows up in the asset inspector, the Sensor inspector, and a roomy window under `Tools > Sensor > Condition Tree`.
5. **Listen.** Subscribe to `Sensor.OnNewDetectionEvent` and `OnLostDetectionEvent`, or use a `Detector` to merge several sensors (see below).

Want to see it running first? Use `Tools > Sensor > Create Example Scene`. It builds a scene with moving targets, a sensor, a detector and a log listener.

## How it works

```
every refresh (SensorSettings.RefreshRate, minimum 0.5 s):

  objects from IAllObjectsCollection ──► condition tree, one object at a time ──┐
                                         (AND / OR / NOT, depth-first)          ├─► detected set
  injectors (fake detections) ───────────────────────────────────────────────────┘        │
                                                                                          ▼
                                                      compared with last refresh: New / Lost events
```

| Part | What it does |
|---|---|
| `Sensor` | Runs the refresh loop, evaluates the tree, reports New and Lost detections. Each sensor works on its own copy of the tree, so sensors can share one settings asset. |
| `SensorWithCamera` | A sensor with a camera. `InFrustum` uses the camera's frustum and `InViewPort` works. |
| `SensorSettings` | ScriptableObject holding the refresh rate, the condition tree, the injectors and the detection ID range. |
| `Detector` | Merges one or more sensors into one result (see below). |
| `DetectableEntity` | Marks an object as detectable. Holds its entity type and tracks its speed. |
| `IAllObjectsCollection` | Decides which objects a sensor looks at. The default scans colliders on a layer mask. |

### Detector

A `Detector` behaves like one big sensor made from several. It treats the sensors as a union with unique values:

- A target is **new** for the detector when the first sensor starts detecting it.
- If another sensor then detects the same target, the detector raises no new event.
- A target is **lost** only when the last sensor stops detecting it.

### Events

```csharp
detector.OnNewDetectionFromDetectorEvent += detection =>
    Debug.Log($"New {detection.ID}, {detection.DistanceFromSensorToDetection:0} m away");

detector.OnLostDetectionFromDetectorEvent += detection =>
    Debug.Log($"Lost {detection.ID}");
```

A `Detection` also lists which leaf conditions the target passed (`AllPassedConditions`) and failed (`AllFailedConditions`). Conditions skipped by short-circuiting appear in neither list.

### Targets that disappear

A target can be destroyed, or become invalid, while it is detected. The lost event still fires, and the `Detection` it carries is complete:

```csharp
sensor.OnLostDetectionEvent += detection =>
{
    // detection.DetectionComponent may be destroyed by now. detection.Snapshot never is.
    DetectableSnapshot target = detection.Snapshot;
    Debug.Log($"Lost {target.Name} ({target.Identifier}) at {target.Position} because {detection.LostReason}");
};
```

- `Snapshot` is a copy of what the target looked like at the sensor's last refresh: identifier, name, type, entity type, position, speed and bounds. It is an `IDetectable` that never throws.
- `LostReason` says why: `ConditionsFailed`, `TargetDestroyed` (gone, invalid, or no longer found), or `SensorDisabled`.
- Targets are tracked by `GetIdentifier()`, not by object. If your program rebuilds an `IDetectable` for the same thing, it stays the same detection with the same detection ID, as long as the identifier is the same. Identifiers must be unique and should be a cached string, since they are read on every refresh. An object without one is ignored, with one warning.
- If your `IDetectable` wraps something that can go away, override `bool IsValid` to return false when it does. The sensor then never calls the object again.

## Built-in conditions

| Group | Condition | Passes when |
|---|---|---|
| Operators | And | every child passes (stops at the first failure) |
| | Or | any child passes (stops at the first pass) |
| | Not | its child fails |
| Spatial | In frustum | the target is inside the sensor's view frustum, or the camera's |
| | In viewport | the target is inside a rectangle of the camera's viewport (needs `SensorWithCamera`) |
| | Is angle clamped | the horizontal angle from the sensor's forward is within a range |
| World | With raycast | nothing blocks the line from the sensor to the target |
| | Is moving | the target is faster than a set speed |
| Probability | In absolute ranges | a random roll succeeds for the target's 3D distance |
| | In horizontal ranges | the same, using the horizontal distance only |
| | In absolute ranges per entity | the same, with different odds per entity type, and an optional attempt limit |

An empty AND or OR evaluates to false, and an empty tree detects nothing.

## QA overlay (in the running game)

Add a **Sensor Debug Overlay** to any object (`Add Component > Sensor > Sensor Debug Overlay`). In the Editor's Game view and in development builds a small **Sensors** button appears in the top-right corner. It opens a panel where QA can:

- pick any Sensor or Detector in the scene,
- see its objects grouped as passed, failed and injected, filter them by name and pick one,
- see the condition tree with Passed, Failed or Not reached beside each condition for the picked object,
- expand a condition and **change its values while the game runs**. Every serialized field gets a control (numbers, sliders, toggles, drop-downs, vectors, layer masks, lists of values or of serializable classes), including the fields of conditions you write later, with no extra code,
- press **Reset values** to go back to what the settings asset says.

Changes apply to that sensor's own running copy of the tree. Nothing is written to the settings asset, and they are lost when the game stops. In release builds the overlay switches itself off.

**Opening it from code, or without code.** The button and your own code open and close the overlay by dispatching commands, so nothing needs a reference to the overlay:

```csharp
SensorDebug.Dispatch(new ToggleOverlayCommand());
SensorDebug.Dispatch(new FocusSensorCommand(mySensor));   // open it on a sensor
```

Or add **Sensor Debug Commands** to any object and point a UI button, a UnityEvent or an Input System action at its `Toggle`, `Open` or `Close`. `Dispatch` returns `false` and does nothing when no overlay is listening. To add a command of your own, implement `ISensorDebugCommand`.

UI Toolkit needs an `EventSystem` to receive the mouse. If the scene has none, the overlay adds one that matches your input setup (the Input System package or the old Input Manager).

## Seeing what conditions do (debug mode)

Conditions that can draw have a **Show visual** checkbox, in the inspector and in the QA overlay. It is **off by default** and is set per condition, so only the ones you are debugging draw. Each also has its own **Visual color** (a different default for each condition). Objects that failed the condition are drawn in the same color but fainter. While it is off the condition does no extra work. When on, it draws what it did for every object in the last refresh, as gizmos in the Scene view and in the Game view with **Gizmos** on:

| Condition | Draws |
|---|---|
| With raycast | a line from the sensor to the object when the way is clear. When blocked: a faint line to what blocked it, a cross where it hit, and a fainter line on to the object |
| Is angle clamped | the allowed wedge, and a small cross under each object (faint when outside) |
| In viewport | a cross on each object (faint when outside) |
| Is moving | a short tick above each object (faint when too slow) |
| Probability conditions | a cross above each object (faint when the roll failed) |
| In frustum | the frustum, as a wireframe (this replaces the old sensor option "Show sensor frustum") |

With an object picked in the QA overlay, only that object is drawn. From code: `((WithRaycast)sensor.RuntimeRoot).ShowVisual = true`.

To draw something for your own condition, inherit `VisualCondition` instead of `Condition` (it adds the checkbox) and draw through `VisualsFor(context)` in `Evaluate`. It is null when the checkbox is off, so the check is free:

```csharp
public override bool Evaluate(IDetectable target, in SensorContext context)
{
    bool passed = target.GetCenterPosition().y > MinHeight;
    VisualsFor(context)?.Line(context.SensorTransform.position, target.GetCenterPosition(), ColorOf(passed));   // VisualColor, fainter if failed
    return passed;
}
```

`Line` and `Cross` take any `Color`. `ColorOf(passed)` gives the condition's own color, fainter when it failed. A condition that draws nothing needs no change.

## Add your own condition

Write one class. Public fields become its settings in the inspector, and it shows up in the add menu on its own.

```csharp
using System;
using SensorSystem;

[Serializable, ConditionMenu("World/Above height")]   // the menu path is optional
public class AboveHeight : Condition
{
    public float MinHeight = 2f;                        // appears in the inspector

    public override string Summary => $"Above {MinHeight} m";   // optional, shown on the collapsed card

    public override bool Evaluate(IDetectable target, in SensorContext context)
        => target.GetCenterPosition().y > MinHeight;
}
```

Other things a condition can do, all optional: `Prepare` runs once per refresh before any target is checked, `OnTargetGained` and `OnTargetLost` tell it when the sensor starts or stops detecting a target, and `DrawGizmos` draws in the scene view. Mark runtime state `[NonSerialized]`.

Conditions can depend on each other through the tree itself: `And(InFrustum, IsMoving)` only checks speed for targets that are already in the frustum.

## Injectors (fake detections)

An injector adds detections straight into a sensor's result, **bypassing the tree**. The built-in ones create fake targets at random times, types and distances, to simulate false alarms. Add them under **Injected detections** in the settings asset.

To write your own, inherit `DetectionInjector` and add your detections in `Collect`. Like conditions, they appear in the add menu automatically.

## Plug in your own objects

By default a sensor takes the objects in the detectable registry (below) that are on its layer mask. To get objects from somewhere else, implement `IAllObjectsCollection` and override `Sensor.CreateAllObjectsCollection`:

```csharp
public class MySensor : Sensor
{
    protected override IAllObjectsCollection CreateAllObjectsCollection() => new MyCollection();
}
```

### The detectable registry

Sensors do not search the scene. Every detectable object is kept in `DetectableRegistry`, and the default collection reads it once a second and keeps the ones on the sensor's layer mask. `DetectableEntity` registers itself when it is enabled and leaves when it is disabled or destroyed. Anything else that implements `IDetectable`, such as a wrapper around an object from another program, does the same:

```csharp
void OnEnable()  => DetectableRegistry.Register(this);
void OnDisable() => DetectableRegistry.Unregister(this);
```

A component uses its GameObject's layer for the layer mask. Implement `IDetectableLayer` to give another layer, and an object that is not a component is always included. If you have `IDetectable` components that never register, set `DetectableRegistry.IncludeUnregisteredSceneObjects = true` to bring back the old search through every collider. It is off by default because it is slow in big scenes.

## Working in a team

- **Scene merges.** A sensor in the scene only stores a reference to its settings asset plus a few options. The condition tree is stored in the asset, so editing the tree changes only the `.asset` file. Making the sensor a prefab keeps scene changes smaller still.
- **Unity's smart merge.** `.gitattributes` already marks Unity files for it. Each person sets it up once, using the path of their own editor install:

  ```bash
  git config merge.unityyamlmerge.name "Unity SmartMerge"
  git config merge.unityyamlmerge.driver '"<Unity install>/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p %O %B %A %A'
  ```

## Tests

Open `Window > General > Test Runner`. The edit mode tests (`Sensor.Tests`) cover the tree logic, tree editing and the editor windows, and the play mode tests (`Sensor.PlayTests`) cover the running sensor and detector.

## Project layout

```
Assets/Sensor/
  Runtime/        Sensor.Runtime assembly: sensor, detector, conditions, injectors, utilities
  Editor/         Sensor.Editor assembly: tree editor, inspectors, window, styles
  Example/        example scripts and the example scene builder
  Tests/          edit mode and play mode tests
```

## Notes

- `EntityType` is a placeholder enum (`Unknown`, `Person`, `Vehicle`, `Aircraft`, `Drone`). Edit it to match your project.
- The repository is a full Unity project so the example scene and tests run as they are. To use the sensor elsewhere, copy the `Assets/Sensor` folder.

## License

[MIT](LICENSE). It covers the code in `Assets/Sensor`. The rest of the project (`Assets/Settings`, `Assets/TutorialInfo` and similar) comes from Unity's standard URP project template and stays under Unity's own terms.
