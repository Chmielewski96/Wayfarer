# Wayfarer — Architecture Overview

Wayfarer is a Unity 6 (URP) third-person prototype — a personal training project (the third in a "North Star" series) built around a Sea Mage character kit: water-based spells, ice surfing, swimming, and light exploration/collectible systems. This document is a brief map of how the codebase is organized and how its major systems talk to each other, meant as an orientation point rather than exhaustive documentation.

## Movement: a coexistence pattern, not a state machine

The single biggest architectural decision in this project is how alternate movement modes are layered on top of normal movement. `PlayerController` (`Assets/Scripts/Player/PlayerController.cs`) owns the default CharacterController-based walk/run/jump/gravity loop and drives most of the Animator's parameters (`Speed`, `IsGrounded`, `IsAiming`, `MoveX`, `SelectedSpellSlot`). Rather than building a formal state machine, alternate modes are separate `MonoBehaviour`s that live on the same GameObject and take over entirely while active:

- **`IceSurfController`** (`Assets/Scripts/Movement/`) — persistent-velocity ice surfing with carving, slope acceleration, a Q-triggered speed boost, and jump kickflips.
- **`SwimController`** (`Assets/Scripts/Movement/`) — floating/swimming, triggered automatically by world position (falling below a water surface's height) rather than an input toggle.

Each of these calls back into `PlayerController.SetSurfing(bool)` / `SetSwimming(bool)`, which flips an internal flag that makes `PlayerController.Update()` early-return entirely while that mode is active. Momentum is handed off explicitly rather than snapped to zero: `AddExternalVelocity()` carries horizontal speed back into normal movement (with ground-friction-style decay), while `SetVerticalVelocity()` sets pure gravity-driven vertical speed directly (used for the swim jump-out, so it follows the same clean arc as a normal jump instead of decaying). This hand-off pattern is the thing to replicate if a third movement mode is ever added.

Water/land detection for swimming deliberately uses actual terrain height at the character's XZ position (`Terrain.SampleHeight`) compared against the water surface, rather than the character's own animated float height — using the character's dynamic position for that check was tried first and caused flicker right at shorelines.

## Camera

Camera work is built on Cinemachine, split into two virtual cameras arbitrated by `CameraSwitcher`: an "explore" camera (default third-person orbit, `ThirdPersonCameraController` handles scroll-to-zoom) and an "aim" camera (activated while aiming, driven by `AimCameraController`'s yaw/pitch look input). Priority swapping between the two Cinemachine cameras is how the transition is handled, rather than manually blending values.

## Spells

Spells are authored as `ScriptableObject` data assets (`SpellData` base class, `Assets/Scripts/Spells/`) rather than one `MonoBehaviour` subclass per spell — each spell asset implements `Cast(SpellCastContext)`, and `SpellCastContext` bundles everything a spell needs (origin, aim point, target mask, water blob prefab) so spells don't need back-references to the player. Current spells: `IceBoltSpellData` (projectile, see `IceBoltProjectile`), `FrostConeSpellData` (cone AoE with a ground telegraph via `GroundConeIndicator`), `ShatterSpellData` (sky-strike combo finisher via `ShatterStrikeController`, bonus damage against frozen targets).

`PlayerSpellCaster` (`Assets/Scripts/Player/`) holds up to 6 spell slots (Q/E/R/Z/X/C), handles selection, cooldowns, aiming-gated casting, and hand VFX spawning (`HandCastVfx`), and drives cast animations on a dedicated `UpperBody` Animator layer so casting can blend over movement. Casting is blocked while surfing or swimming (both checked directly on `PlayerController`).

## Combat

Minimal and shared: `Health` (`Assets/Scripts/Combat/`) is a generic damageable component any target carries, and `Freezable` tracks frozen status for the Frost Cone → Shatter combo (swaps materials to an ice look while frozen, exposes `IsFrozen` for Shatter's bonus-damage check).

## Water resource & water blobs

`WaterResource` (`Assets/Scripts/Player/`) is the Sea Mage's mana-equivalent: large pool, slow passive regen by design — meant to be topped up by collecting `WaterBlob` pickups (dropped by ice/water spells, with a "magnetic" pull toward the player in range) rather than waiting. `WaterResourceUI` drives the on-screen bar. Surfing and swim jump-outs also draw from this pool.

## Exploration & collectibles

A newer, separate system for the exploration side of the project: `SeashellCollectible` (`Assets/Scripts/Environment/`) is a floating, bobbing pickup requiring an F-press within range (shows a billboarded "F - Collect" world-space prompt via `BillboardToCamera`), reporting to a `SeashellManager` singleton that tracks a running total and exposes an event for future systems (a skill tree, not yet built) to hook into. The shell's mesh is procedurally generated (`ShellMeshGenerator`) as a placeholder since no 3D model generation provider is configured in this project yet.

## Environment

Terrain is a single Unity `Terrain` object (currently ~667×667, flattened for building out the first quest location — an earlier, larger procedurally-sculpted terrain was intentionally discarded in favor of hand-sculpting). Water is currently a plain flat transparent plane (`WaterLevelPlane`) used as a level reference for sculpting and as the swim system's water-surface reference — no animated water shader is implemented yet (an earlier custom HLSL shader and a Shader-Graph-tutorial-based attempt were both built, evaluated, and ultimately scrapped in favor of starting clean).

## Input

Input System-based, defined in `Assets/PlayerControls.inputactions` with action maps for `CameraControls`, `Gameplay` (move/jump/aim/surf/deselect), and `Spells` (cast + 6 slot selects). Most systems consume this through `InputActionReference` fields wired in the inspector. A few single-purpose keys (the surf-mode Q boost, the swim F-interact) intentionally poll `Keyboard.current` directly instead, since they're self-contained abilities that don't need a shared action map entry.

## Folder reference

```
Assets/Scripts/
  Player/        PlayerController, PlayerSpellCaster, WaterResource, SpellOriginFollow
  Movement/      IceSurfController, SwimController, SimpleChaseCamera, SurfTrailVFX
  Spells/        SpellData + concrete spells, SpellCastContext, projectiles, controllers
  Combat/        Health, Freezable
  Environment/   SeashellCollectible, SeashellManager, ShellMeshGenerator
  Camera/        platform-specific look-input helpers (editor sensitivity, Mac axis invert)
  UI/            BillboardToCamera, WaterResourceUI
  (root)         camera controllers (ThirdPersonCameraController, AimCameraController,
                 CameraSwitcher), generated PlayerControls wrapper
```

---
*Generated as a working reference — reflects the state of the project as of this session. Re-generate or update by hand as the project evolves.*
