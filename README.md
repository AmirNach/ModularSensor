# ModularSensor

A drag-and-drop detection sensor for Unity. Put a component on a sensor, mark things as detectable, and build the rules in an inspector tree with AND, OR and NOT. Adding a new rule takes one small class.

- **Visual condition tree.** Combine conditions with AND / OR / NOT. Search, drag, wrap and duplicate nodes, and see a readable summary of the whole expression.
- **One refresh rate per sensor.** The tree is evaluated once per refresh, never less than every 0.5 seconds.
- **Short-circuit evaluation.** The tree is walked depth-first and stops as soon as the answer is known, so cheap checks placed first protect expensive ones such as raycasts.
- **Easy to extend.** A new condition is one class. No enum entry, no registration, no settings code.
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

By default a sensor scans colliders on its layer mask for an `IDetectable`. To get objects from somewhere else, implement `IAllObjectsCollection` and override `Sensor.CreateAllObjectsCollection`:

```csharp
public class MySensor : Sensor
{
    protected override IAllObjectsCollection CreateAllObjectsCollection() => new MyCollection();
}
```

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
