# Decision: Unity version-control policy — source only, data never

**Date:** 2026-08-26
**Status:** Active
**Applies to:** `.gitignore`, `.gitattributes`, `.githooks/pre-commit`, `Unity/City_Digital_Twin/`

---

## Context

Git tracking for the Unity project was set up ad hoc and had three concrete problems.

### 1. A 220 MB scene file, one save away from being committed

`Unity/City_Digital_Twin/Assets/Scenes/SampleScene.unity` measured **220,929,539 bytes** in
the working tree against **11,413 bytes** at `HEAD`.

`CityMeshLoader` builds meshes at edit time from the PLY output in `Processed_data/` and
attaches them with `AddComponent<MeshFilter>().sharedMesh = mesh`
([CityMeshLoader.cs:420-425](../../Unity/City_Digital_Twin/Assets/Scripts/IO/CityMeshLoader.cs#L420-L425)).
Those meshes are runtime objects with no asset backing, so when the scene is saved Unity has
nowhere to put them but the scene YAML itself. The file contains 10 embedded `Mesh` objects
(class id `!u!43`); 20 lines carry 220,856,904 characters of hex vertex data.

Git sees a text file, so `git add -A` would have committed the whole thing, and every
subsequent load-and-save would have added another ~220 MB blob. Nothing had caught this yet
only because the scene had not been staged.

### 2. `core.autocrlf=true` fighting Unity's line endings

Unity writes YAML with LF on every platform. With `autocrlf=true` and no `.gitattributes`
rules, git rewrites those files to CRLF on checkout, Unity saves them back as LF, and every
`.unity` / `.asset` / `.meta` file shows a whole-file diff. `git diff` was already printing
`LF will be replaced by CRLF the next time Git touches it` for the scene and both render
pipeline assets.

### 3. Ignore rules that had drifted

`Unity/GISTesting/` and `Unity/DigitalTwinPerview/` were still listed but were removed from
the repo on 2026-07-17. Missing were the Unity 6 generated folders (`Bee/`, `artifacts/`),
Addressables build output, build products, and — most importantly — any rule stopping mesh
and point-cloud payloads from being added inside `Assets/`.

---

## Decision

**Git tracks source. Git never tracks data.**

Tracked: Python pipeline code, Unity C# scripts and `.asmdef`s, `ProjectSettings/`,
`Packages/manifest.json` + `packages-lock.json`, the clean template scene, render pipeline
and settings assets, configs, docs, tests.

Never tracked, never published: anything under `Raw_data/` or `Processed_data/`, and every
mesh, point cloud, raster, or geometry payload wherever it lands — including inside
`Assets/`. `Unity/*/Library/` alone is 1.8 GB and is regenerated on open.

Three mechanisms implement this.

### `.gitignore` — rewritten

Data patterns (`Raw_data/`, `Processed_data/`, `*.laz`, `*.ply`, `*.gpkg`, `*.tif`, …) are
written **without a leading slash so they match at any depth**, including inside the Unity
project. Each payload extension is paired with its `.meta` twin.

The Unity section covers generated folders with case-insensitive brackets
(`[Ll]ibrary/`, `[Tt]emp/`, `[Bb]uild[s]/`), Unity 6's `Bee/` and `artifacts/`, Addressables
output, build products, and baked lighting data. Dead `GISTesting` / `DigitalTwinPerview`
rules were dropped.

### `.gitattributes` — rewritten

`*.unity`, `*.prefab`, `*.asset`, `*.meta`, `*.mat` and the rest of Unity's YAML formats get
`text eol=lf`, which overrides `core.autocrlf` and ends the CRLF churn. They also get
`merge=unityyamlmerge`; if that driver is not registered git falls back to its normal 3-way
text merge (verified experimentally), so the attribute is safe on a machine without Unity's
SmartMerge configured. Binary formats are marked `binary` so a forced add can never be
line-ending-mangled.

**Git LFS is deliberately not used.** No data is tracked, so nothing is large enough to
justify it.

### `.githooks/pre-commit` — the enforcement layer

Documentation does not stop `git add -f` at 2 a.m. The hook rejects a commit that stages:

| Check | Rejects |
|---|---|
| `DATA` | any path under `Raw_data/` or `Processed_data/` |
| `TOO BIG` | any blob over 5 MB (override: `git config hooks.maxfilesize <bytes>`) |
| `ORPHAN` / `NO META` | a Unity asset without its `.meta`, or a `.meta` without its asset |

The meta check matters because a `.meta` that travels without its asset makes Unity
regenerate the GUID, which silently re-links every reference to that asset in every scene on
the next machine. Folder `.meta` files are handled: git cannot track a directory, so the
hook accepts a folder `.meta` whose directory exists on disk.

**Enable once per clone** (hooks live outside the repo by default, so this is not automatic):

```bash
git config core.hooksPath .githooks
```

---

## Scene workflow

`SampleScene.unity` is tracked **only as a clean ~11 KB template** — camera, light, and the
`CityMeshLoader` component. To work with the city:

1. Open the template scene and load the city from the Tools menu.
2. Do not save over the template. If you want to keep the loaded state locally,
   **File → Save As** into `Assets/Scenes/Generated/`, which is gitignored along with its
   `.meta`.
3. The template is regenerable from `Processed_data/` at any time, so nothing is lost by
   discarding a loaded scene.

The current 220 MB working copy of `SampleScene.unity` was left in place — it is the user's
editor state, and the pre-commit hook now prevents it from being committed. Restore the
clean template when convenient with:

```bash
git restore Unity/City_Digital_Twin/Assets/Scenes/SampleScene.unity
```

`Assets/Meshes/Buildings_500/` keeps a `.gitkeep` so the folder exists on a fresh clone and
its tracked `.meta` is not orphaned. Its contents are ignored.

---

## Alternatives considered

**Bake chunk meshes into `.asset` files and track them with Git LFS.** A fresh clone could
open the scene without running the Python pipeline. Rejected: it publishes derived city
geometry, costs ~220 MB of LFS quota per re-bake, and conflicts with the requirement that no
processed data leave the machine.

**Mark loader output `HideFlags.DontSave` so it can never be serialized.** Structurally the
strongest fix — git status would stay quiet no matter what the editor does. Not adopted now
because it changes runtime behavior (the loaded city disappears on domain reload) and the
hook already closes the version-control hole. Worth revisiting if scene bloat recurs.

**Leave enforcement to `.gitignore` alone.** Rejected: `.gitignore` does not protect a file
that is *already tracked*, which is exactly the case for `SampleScene.unity`.

---

## Consequences

- `git status` is quiet during normal Unity work; only real source changes appear.
- Unity YAML no longer flips between LF and CRLF, so diffs are readable and merges are sane.
- A commit that would carry data or a bloated scene fails loudly instead of succeeding.
- A fresh clone gets a working Unity project but **no city geometry** — it must be
  regenerated by running the mesh pipeline into `Processed_data/`. This is intentional and
  is the documented cost of the no-data rule.
- `--no-verify` bypasses the size and meta checks for a deliberate exception. The data rule
  should be changed here first if it ever needs to change.

---

## Known issue, not addressed here

`.git/` is **2.5 GB** because `Raw_data/` LAZ tiles were committed in earlier history — the
seven largest blobs are 29-47 MB laser tiles, plus a 13 MB validation report. They are
ignored now, but they remain in history and will be transferred on every clone and push.

Removing them requires rewriting history (`git filter-repo`), which invalidates every
existing commit hash and any clone or fork. Flagged for a deliberate decision; not done as a
side effect of this cleanup.

---

## How to verify

```bash
# no tracked file is shadowed by an ignore rule (expect empty output)
git ls-files | git check-ignore --stdin -v

# data and generated paths are ignored
git check-ignore -v Processed_data/building_meshes/x.ply \
                    Unity/City_Digital_Twin/Library/x \
                    Unity/City_Digital_Twin/Assets/Scenes/Generated/city.unity

# the hook rejects a data path
echo x > Processed_data/_t.txt && git add -f Processed_data/_t.txt
sh .githooks/pre-commit   # expect exit 1, "DATA"
git restore --staged Processed_data/_t.txt && rm Processed_data/_t.txt
```
