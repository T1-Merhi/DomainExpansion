# Domain Expansion

A 2D endless survival shooter in C# on [raylib-cs](https://github.com/ChrisDill/Raylib-cs), with no game engine and no third-party gameplay libraries.

You play a rotating polygon. Each side is a weapon mount, and the side facing your cursor is the one that fires — so aiming and choosing a weapon are the same act. Kill enemies, spend coins between waves, add sides, and survive as long as the curve lets you.

## Requirements

- .NET 10 SDK

## Running

```bash
dotnet run
```

```bash
dotnet run -- --admin
```

Both come from the same build, so you can run one of each side by side. VS Code users get **Player** and **Admin** entries in the debug dropdown.

## Controls

| Input | Action |
|---|---|
| `WASD` / arrows | Move |
| Mouse | Aim — the active side always faces the cursor |
| Left click | Fire the active mount |
| Mouse wheel | Rotate a different side into firing position |
| Right click | Open / close the upgrade shop |
| `ESC` | Pause menu (the only way to leave a run) |
| `ENTER` | Skip the remaining rest period |

Debug keys during a run: `J` damage, `H` heal, `G` coins, `Z`/`X`/`C` spawn an enemy, `E` cycle the active weapon, `3`–`8` change shape, `F3` toggle the overlay.

## Architecture

The central split is **simulation versus rendering**. `World` holds all game state and contains no raylib call of any kind; renderers read it and never mutate it. That separation is what allows the simulation to be driven headlessly, which is how two runtime bugs were found that a green build had missed.

```
GameEngine/   Window, main loop, scene routing, settings, JSON loading
Scenes/       IScene implementations: menu, game, death, pause, admin, test arena
Game/
  Sim/        World, Player, Enemy, Bullet, pooling, collision
  Stats/      StatId, Modifier, StatBlock
  Weapons/    Weapon definitions and per-mount instances
  Enemies/    Per-type behaviours
  Waves/      Wave generation, scaling and the wave state machine
  Upgrades/   Upgrade catalogue and purchase rules
  Scoring/    Leaderboard and run results
  Render/     Everything that draws
  Config/     Config loading, tuning values, admin mode
Data/         JSON definitions shipped as read-only defaults
```

### Fixed timestep

`World.Tick()` takes no delta time — one tick is always 1/60s by construction. `GameScene` owns the accumulator, so menus stay frame-based while the simulation stays fixed. This makes fire rates and spawn intervals exact whatever the frame rate, and prevents fast projectiles tunnelling through enemies during a frame spike.

Time scale in the test arena multiplies real time *before* the accumulator, never the step itself, so the simulation slows without ever seeing a variable timestep.

### Scenes and events

Scenes receive only `AssetManager` and `GameSettings` — never the engine. They report what happened by raising a `GameEvent`; `SceneManager` alone decides which scene loads next.

Transitions are **deferred**: an event raised inside `Update` is queued and applied after that `Update` returns, so a scene is never unloaded while its own code is still on the stack.

### Stats and upgrades

Every tunable number resolves through `StatBlock` as `(base + Σ Add) × Π (1 + Mult)`, cached behind a dirty flag. Upgrades are entries in `upgrades.json` with a cost formula rather than a table, and levels are keyed by upgrade id — so **adding an upgrade to JSON needs no code change** to become purchasable and levellable. `appliesTo` restricts one to particular weapons.

Each mount carries its own `StatBlock`, so upgrading one side leaves an identical weapon on another side untouched.

### Pooling

Bullets, enemies, explosions and floating text all use `Pool<T>`, which keeps active items contiguous and reuses instances rather than allocating. `ReturnAt` swaps the last active item into the freed slot to stay O(1) — which means **despawn loops must iterate backwards**, or they skip the swapped-in item.

## Configuration

Gameplay values live in JSON and are editable without recompiling.

| File | Contents |
|---|---|
| `player.json` | Turret size, speed, health, feedback timings |
| `weapons.json` | Rifle, shotgun, grenade — stats, colour, on-hit behaviour |
| `enemies.json` | Per-type stats, radius, colour, rewards |
| `waves.json` | Count curves, scaling curves, per-wave overrides, pacing |
| `upgrades.json` | Upgrade definitions and cost curves |
| `effects.json` | Shake, flash and telegraph timings |

**Config is not read from the build output.** The canonical location is:

```
%APPDATA%/DomainExpansion/Data
```

The `Data/` folder beside the executable holds pristine defaults that seed that folder on first run and back reset-to-default afterwards. One shared location means two instances always agree; per-output copies would let a Debug and a Release build silently diverge.

Loading is defensive throughout: reads retry if a file is mid-write, validation runs per file, and anything invalid falls back to the last known-good copy with a logged warning rather than crashing.

A few values stay in code on purpose — tick rate, the step clamp, spatial grid cell size and pool capacities are engine constants, not balance.

## Admin mode

`--admin` adds a config editor and a test arena. Both are **absent** in player mode: the menu entries are not in the options array, and `SceneManager` refuses to route to them regardless of what raises the event.

The editor works on the JSON tree rather than typed classes, so every field in every config file is editable — including ones added later. Sibling `*.schema.json` files supply min, max and step; unannotated fields still render against a derived range. Saves are atomic (temp file, then replace) so the other instance never reads a partial file.

The test arena offers spawning by type and count, wave jumping, god mode, infinite coins, grant-all-upgrades, kill-all, adjustable time scale, and a live readout of DPS, enemy counts and resolved player stats.

### Two-instance tuning

Run an admin instance and a player instance together. Edit and save in admin; die and restart in the player instance and the new values take effect — **no relaunch needed**, because a restart constructs a fresh `GameScene`, and that is where config reloads. The admin HUD shows a config version and timestamp so you can confirm a reload happened.

There is deliberately no mid-run hot reload; changing stats under live entities needs a defined re-resolve point rather than a file-watcher callback.

## Assets

Loaded by folder convention and keyed by filename without extension:

```
Assets/Textures/{Sprites,Tiles,Backgrounds}
Assets/Sounds/{SFX,BGM}
```

Missing folders are skipped rather than treated as an error.

## Local state

`settings.json` and `leaderboard.json` are generated beside the executable on first run and are intentionally untracked.
