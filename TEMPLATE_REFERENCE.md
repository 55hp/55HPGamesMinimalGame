# 55HP Mobile Template — Reference for Blockout work

Verified live against `55hp/55HPGamesMobileTemplate`, branch `develop`, 2026-09-13.
This repo (`55HPGamesMinimalGame`) is a copy of the template at an earlier point —
**the template has moved on since the fork**, so don't assume this repo's copy of
Core.Runtime matches develop exactly. This file exists so you don't need to
re-discover these contracts by grepping the template repo each time.

If something here looks stale or contradicts what's actually in this repo's
`Assets/Core/`, trust this repo's actual code — re-verify against the template
only if you're about to duplicate something it might already solve.

## Score

Don't build a Blockout-specific score store. The template already has one:

- `IGameContextService` (`Assets/Core/Runtime/Context/`) — resolve via
  `ServiceRegistry`. Properties: `Score`, `BestScore` (both `int`), plus
  `Lives`, `CurrentLevelId`, `CurrentRunSeed`, `ProfileId`, `IsDebug`.
  `ResetRun()` zeroes `Score` and `Lives` (not `ProfileId`/`IsDebug`).
- `ScoreChangedEvent` (`Assets/Core/Runtime/Events/CoreGameplayEvents/` in this
  repo's copy — namespace `hp55games.Mobile.Core.Gameplay.Events` on
  `develop`) — payload-less `IEvent`. Publish it *after* mutating
  `context.Score`; subscribers re-read the value themselves rather than
  receiving it in the event.
- `UIGameplayHUD` already subscribes to `ScoreChangedEvent` and re-reads
  `context.Score`/`context.Lives` to update its labels. It's wired into
  `GameplayState.EnterAsync`'s HUD setup already. **No UI work needed** for
  Blockout's score display — just write to `context.Score` and publish the
  event.

## Game states / FSM

`IGameStateMachine`/`GameStateMachine` already exists
(`Assets/Core/Runtime/Architecture/GameFSM/`). Don't build a new one.

- `IGameState`: `Task EnterAsync(CancellationToken ct)`,
  `Task ExitAsync(CancellationToken ct)`. Registered via
  `IGameStateMachine.ChangeStateAsync(new XState())`.
- Template's default states (`Assets/Core/Runtime/Architecture/States/`):
  `MainMenuState`, `GameplayState`, `PauseState`, `ResultState`.
- `GameplayState` is deliberately generic — music crossfade, `IGameContextService.ResetRun()`,
  opens the gameplay HUD via `IUINavigationService`. It has **zero game-specific
  logic** by design; it's the slot a game-specific state (like
  `BlockoutGameplayState`) takes instead, not something to extend or modify.
- Check `ResultState`'s actual contract in this repo before assuming it's
  sufficient for Blockout's game-over screen — don't build a
  Blockout-specific result state unless it genuinely isn't.

## Bootstrap / service registration

`GameBootstrap` (`Assets/Core/Runtime/Bootstrap/GameBootstrap.cs` on
`develop` — may not exist yet in this repo's copy) runs
`ServiceRegistry.InstallDefaults()` then loads Menu additively. `InstallDefaults()`
registers ~15 default services (`ILog`, `IEventBus`, `IConfigService`,
`ISaveService`, `ITimeService`, `IContentLoader`, `IGameStateMachine`,
`IObjectPoolService`, `IUIOptionsService`, `ILocalizationService`,
`IGameContextService`, `IInputService`, `IHapticsService`, `IAdsService`,
and more) synchronously.

**Known template gap, not yet fixed upstream (as of 2026-09-13):**
`IConfigCatalogService`/`ConfigCatalogInstaller` is *not* part of
`InstallDefaults()` — it still requires manual per-scene `MonoBehaviour`
wiring in a scene that loads before its consumers. This is a real gap in the
template itself, not something to route around cleverly in Blockout — if it
comes up again, the correct long-term fix is upstream (folding it into
`InstallDefaults()` or the bootstrap sequence), not a repeated per-scene
workaround.

## General patterns already established in this repo

(For fuller context — you've likely already internalized these from the
existing Blockout code, listed here for completeness.)

- No singletons/static mutable state — everything through `ServiceRegistry`.
- Gameplay↔UI decoupling via `IEventBus`.
- Config assets: `IConfigAsset` marker on a `ScriptableObject`, resolved via
  `IConfigCatalogService.Get<T>()`/`GetAll<T>()`.
- Pooling via `IObjectPoolService` for repeated spawn/despawn.
