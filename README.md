# PathMotion

Author a motion path **directly in the Unity Scene view**, key the speed yourself,
watch it move live in the editor, then bake it into a normal `AnimationClip` you can
keep tweaking in the Animation window.

No Play mode required to preview. No runtime dependencies — Unity core API only
(works on Unity 2020.3+).

---

## Install

### Option A — Package Manager (recommended)

`Window > Package Manager > + > Add package from git URL…`

```
https://github.com/JohnnieRunner2026/PathMotion.git
```

### Option B — copy the folder

Copy `Runtime/` and `Editor/` into `Assets/PathMotion/` in your project.

---

## Quick start

1. `GameObject > PathMotion > Create Path + Mover Demo`
   (or `Create MotionPath (with curve)` to get just the path)
2. Select the **MotionPath** object → the curve is drawn in the Scene view.
3. **Click a blue dot** to select a waypoint, then use the normal **Move (W)** and
   **Rotate (E)** gizmos to drag / twist it.
   Rotating a point bends the curve at that point (Hermite tangent).
4. **Double-click on the curve** to insert a new waypoint right there.
5. Select the **PathPlayer** and shape `Progress Curve`:
   `x` = normalized time (0→1), `y` = distance along the path (0→1).
   Flat = pauses, steep = rushes, S-shape = ease in/out. This is your speed.
6. Watch it move **without entering Play mode** — it plays automatically in the editor.
7. `Export to AnimationClip (.anim)` bakes it into a clip you can open in the
   Animation window and re-key by hand.

---

## What each piece does

| Script | Role |
| --- | --- |
| `MotionPath` | The path itself: waypoint positions + rotations, `CatmullRom` / `Linear` / `Hermite` |
| `PathPlayer` | Moves a target along a path using a keyable `progressCurve`, and exports the clip |
| `MotionPathEditor` | Scene handles for the points, inspector for the point list |
| `MotionPathSceneDrawer` | Always-on curve drawing (does not depend on selection) |
| `PathPlayerPreviewTicker` | Drives the live editor preview every editor tick |
| `PathMotionMenu` | `GameObject > PathMotion` menu entries |

## Path modes

- **Hermite** (default) — each waypoint's rotation *is* the curve tangent, so
  rotating a point shapes the curve.
- **Catmull-Rom** — smooth curve that only follows the point positions.
- **Linear** — straight segments between points.

## Speed

`PathPlayer.duration` is the length of one pass. `progressCurve` remaps time onto
path progress, so speed is whatever you key it to be — it is **not** forced to be
constant.

## Editor preview

`Preview In Editor` is **on by default**: any `PathPlayer` with a path animates in
the Scene view immediately. Pausing restores the object to the pose it had before
the preview started, and entering Play mode restores it too. `Preview Speed` slows
the preview down (it does not affect the exported clip).

If the `Path` field is left empty, the player falls back to a `MotionPath` on the
same GameObject and then to the first one in the scene, and the inspector says
which one it picked.

## Export

`Export to AnimationClip` bakes `localPosition` (and `localRotation` when
`Orient To Path` is on) at 30 fps into a legacy clip, and attaches an `Animation`
component so it is playable right away.
