# Blockout — Documento di progetto

Tetris 3D con policubi in un pozzo 3D, mobile portrait, sessione infinita. Costruito sul **55HP Mobile Template**.

- Repo: `55hp/55HPGamesMinimalGame` — sviluppo su `develop`, `main` solo a milestone chiuse
- Snapshot verificato sul codice: `develop` @ `91974d1` (17/09/2026)
- Questo file sostituisce: `README.md` (overview generica del template), `TEMPLATE_REFERENCE.md`, `Blockout_Skin_System_GDD/Technical.md`, `Blockout_Periodic_Table_GDD/Technical.md`, `Blockout_Periodic_Table_Shop_GDD/Technical.md`
- Fonti di design autorevoli: GDD e Documentazione Tecnica su Notion. Se questo file e il codice non coincidono, **vale il codice**. Aggiorna questo file quando cambia qualcosa di strutturale.

---

## 1. Architettura

### Layer
| Layer | Percorso | Namespace | Regola |
|---|---|---|---|
| Core (template) | `Assets/Core/` | `hp55games.Mobile.Core.*` | Generico. Fix qui sono fix del template, niente logica Blockout |
| Polycubes (game-agnostic) | `Assets/GameSpecific/Features/Polycubes/` | `hp55games.Polycubes.*` | Griglia voxel, forme, curve di timing. Niente riferimenti a Blockout |
| Blockout | `Assets/GameSpecific/Features/Blockout/` | `hp55games.Blockout.*` | Tutto ciò che è specifico del gioco |
| Contenuti | `Assets/GameSpecific/Content/` | — | Config asset, skin, dataset elementi |
| Test | `Assets/Tests/PlayMode/{Blockout,Polycubes}` | — | PlayMode, uno per sistema |

### Vincoli (non negoziabili)
- Nessun singleton né stato statico mutabile: tutto via `ServiceRegistry.Register/Resolve/TryResolve`.
- Gameplay ↔ UI disaccoppiati via `IEventBus`.
- Config: `ScriptableObject` con marker `IConfigAsset`, letti via `IConfigCatalogService.Get<T>()` / `GetAll<T>()` (multi-istanza supportata).
- Persistenza: solo `ISaveService` / `SaveData`. Estendere `SaveData`, mai sistemi paralleli.
- Spawn/despawn ripetuti: `IObjectPoolService`.
- Riferimenti asmdef: solo assembly runtime, mai varianti `.Tests` / `.Editor.Tests` dei package.

### Servizi del template riusati (non duplicare)
- **Score**: `IGameContextService.Score` / `BestScore`. Muta il valore, poi pubblica `ScoreChangedEvent` (senza payload). `UIGameplayHUD` rilegge da solo.
- **FSM**: `IGameStateMachine`, stati `MainMenuState`, `GameplayState`, `PauseState`, `ResultState`. `BlockoutGameplayState` prende il posto di `GameplayState` tramite `IGameplayStateFactory`, non lo estende.
- **Bootstrap**: `00_Bootstrap.unity` → `ServiceRegistry.InstallDefaults()` (~15 servizi). Scene: `01_Menu`, `02_Gameplay`, `03_Results`.
- **Gap noto del template**: `IConfigCatalogService` / `ConfigCatalogInstaller` **non** è in `InstallDefaults()` e richiede wiring per scena. Il fix giusto è upstream nel template, non workaround ripetuti qui.

### Servizi Blockout
Registrati da `BlockoutGameplayStateInstaller`:
- `IGameplayStateFactory` → `BlockoutGameplayStateFactory`
- `IBlockoutSkinService` → `BlockoutSkinService`
- `IBlockoutAchievementService` → `BlockoutAchievementService`

---

## 2. Core gameplay

| Parametro | Valore | Dove |
|---|---|---|
| Pozzo | 5 × 5 × 12 (W × D × H) | `BlockoutWell.asset` (default nel codice: H=10, l'asset vince) |
| Forme | 12: 8 tetracubi + 4 pentacubi | `BlockoutShapeSet` |
| Movimento | a step, transizione 0.3s | `BlockoutFallCurve.asset` |
| Camera | prospettica dentro il pozzo, adattiva, griglia wireframe | `BlockoutWellCamera`, `BlockoutWellWireframe` |
| Punteggio | `100 × N²` (N = strati eliminati insieme) | `ScoreCalculator` |

`BlockoutWellCamera` tocca solo il FOV; rotazione e posizione restano quelle impostate a mano sulla camera in scena, salvo `BlockoutWell.asset` → `Camera Position Offset` (`Vector3`, default zero), sommato una sola volta alla posizione a `Start()`. Per trovare i valori: Play da `00_Bootstrap`, pausa, sposta a mano la camera, riporta il delta nell'asset.

### Input touch (`BlockoutInputHandler`)
Ogni gesto viene risolto con un raycast contro il pezzo attivo:
- **Swipe che parte sul pezzo** → rotazione. Orizzontale = AxisA → **Z**, verticale = AxisB → **X**. Y escluso (asse di caduta). Mapping in `PieceController` (`AxisAMapsTo` / `AxisBMapsTo`).
- **Swipe fuori dal pezzo** → traslazione nella direzione risolta.
- **Tap fuori dal pezzo** → traslazione (applicata dopo la finestra double-tap).
- **Tap sul pezzo** → nessun effetto.
- **Double-tap** (entro 0.3s) → hard drop.
- Se il raycast non si risolve (nessun pezzo o camera) lo swipe fa rotazione come fallback.
- Editor: `BlockoutKeyboardInputHandler`.
- Log TAP/SWIPE/IGNORED per-gesto dietro `#if HP55_INPUT_DEBUG` in `InputService` (nessuno stato statico, vedi §1): aggiungi lo scripting define per riattivarli quando serve diagnosticare la detection.

### Difficoltà
Due componenti combinati come moltiplicatore:
1. **Curva per pezzo** (`PhasedIntervalCurve`, `BlockoutFallCurve.asset`): base 3.0s, ÷1.2 fino al pezzo 10, ÷1.1 fino al 20.
2. **Modificatore temporale** (`BlockoutTimeDifficultyModifier`, `BlockoutTimeDifficulty.asset`): base sessione 2.0s, tick ogni 15s con −0.15s, −0.1s per ogni evento di clear, floor combinato 0.5s.

> ⚠️ `_timerDecrementPerTick = 0.15` è stato scelto dall'agente e non è mai stato confermato. Va deciso dopo il playtest su device.

Eventi: `PieceLockedEvent`, `LayersClearedEvent`, `FallIntervalChangedEvent`, `BlockoutGameOverEvent`, `PieceMoveRequestedEvent`, `PieceRotateRequestedEvent`, `HardDropRequestedEvent`.

---

## 3. Sistema skin

Un `BlockoutSkin` (`IConfigAsset`) accoppia sempre **estetica** e **comportamento al clear**, che non sono separabili. C'è un solo skin attivo alla volta ed è puramente cosmetico: non tocca regole, difficoltà o punteggio.

### `BlockoutSkin` — campi
`SkinId`, `DisplayName`, `CostInCoins` (`-1` = `CostNotSetValue`, vedi `HasCostSet`), `UnlockedByDefault`, `PieceColors`, `ClearBehaviour`. Per gli elementi chimici anche: `ElementSymbol`, `AtomicNumber` (>0 solo per gli elementi), `MaterialCategory` (`Metallic` / `Opaque` / `Translucent`), `DensityNormalized`, `UnlockMethod` (`Default` / `Coins` / `Achievement`), `UnlockAchievementId`.

### Comportamenti al clear (`IBlockoutClearBehaviour`, base `BlockoutClearBehaviour` ScriptableObject)
Polimorfici: un nuovo skin non deve toccare il codice condiviso del clear.
- `NeutralClearBehaviour`: gli strati spariscono e basta.
- `JuicyClearBehaviour`: le celle diventano oggetti fisici pooled (`_launchSpeed` 4, `_horizontalScatterSpeed` 1.5).
- `PeriodicMeltClearBehaviour`: effetto "melt" più 24 sferule pooled (6 per lato) generate dal perimetro del pozzo all'altezza dello strato. Ereditano colore e materiale dell'elemento. L'impulso verticale deriva da `DensityNormalized` tramite la curva `_densityToVerticalImpulse` (bassa densità → verso l'alto, alta → verso il basso). Altri parametri: `_outwardEjectSpeed` 2.4, `_horizontalScatterSpeed` 0.65.

### Skin presenti (`Content/Skins/`)
- `BlockoutSkin_Default`: palette a 12 colori, sempre sbloccato, gratis.
- `BlockoutSkin_Profondita`: 300 coins, clear neutro.
- `BlockoutSkin_JuicyClear`: 500 coins, clear fisico.
- `Elements/`: 118 skin-elemento generati da `blockout_periodic_elements.json` con `BlockoutPeriodicElementImporter` (Editor). **Rigenerare con l'importer, non modificare gli asset a mano.**

### `IBlockoutSkinService`
- `ActiveSkin`: mai null se il catalogo ha almeno uno skin; fallback sul default.
- `IsUnlocked(skin)`: vero per `UnlockedByDefault` oppure se l'id è in `SaveData.unlockedSkinIds`.
- `SetActiveSkin(id)`: no-op con warning se lo skin è sconosciuto o bloccato.
- `TryUnlockSkin(id)` → `UnlockSkinResult`: `Success`, `UnknownSkinId`, `AlreadyUnlocked`, `NotEnoughCoins`, `CostNotSet`. Spende coins. È il percorso per lo sblocco a coins.
- `GrantUnlock(id)`: sblocco gratuito e idempotente. Lo usa solo il servizio achievement.
- `GetElementShopEntries()`: i 118 elementi ordinati per numero atomico, con stato sbloccato/attivo e `PeriodicTablePosition` (da `PeriodicTableLayout`).

---

## 4. Tema "Tavola Periodica"

118 elementi, ognuno è un `BlockoutSkin`. Ogni run usa un solo elemento attivo.

### Dataset `Content/Skins/blockout_periodic_elements.json`
Campi per elemento: `atomicNumber`, `symbol`, `nameIt`, `densityGCm3`, `densityIsPredicted`, `densityNormalized` (log, 0–1), `shaderCategory`, `stateAtRoomTemp`, `colorHex`, `unlockCostCoins`, `discoveryYear`, `knownSinceAntiquity`, `unlockMethod`.
- Categorie shader: 92 metallic, 14 translucent, 12 opaque. Lo sbilanciamento riflette la chimica reale ed è voluto.
- Il criterio fisico delle sferule è la **densità**, non la massa atomica.

### Sblocco: tre meccanismi
| Metodo | Elementi | Regola |
|---|---|---|
| `default` | 1 — Carbonio | Sempre sbloccato, costo 0 |
| `coins` | 106 | Costo = anno di scoperta (es. H = 1766) |
| `achievement` | 11 antichi | Sblocco automatico al completamento dell'achievement |

| Elemento | Achievement id | Condizione |
|---|---|---|
| Fe | `first_run_completed` | Prima run completata |
| Cu | `runs_completed_10` | 10 run |
| Zn | `runs_completed_50` | 50 run |
| Sn | `runs_completed_100` | 100 run |
| Pb | `login_streak_2` | 2 giorni consecutivi |
| Ag | `login_streak_5` | 5 giorni consecutivi |
| Au | `login_streak_14` | 14 giorni consecutivi |
| Hg | `single_run_score_5000` | ≥ 5.000 punti in una run |
| Sb | `single_run_score_15000` | ≥ 15.000 punti in una run |
| As | `multi_clear_3plus` | Un clear da ≥ 3 strati |
| S | `elements_unlocked_20` | ≥ 20 elementi sbloccati |

> ⚠️ Le soglie sono placeholder non bilanciati.

### `IBlockoutAchievementService`
`IsCompleted`, `GetDescription`, `RecordRunCompleted`, `RecordRunScore`, `RecordMultiClear`, `RecordLoginForToday` (una volta per sessione nuova, non al resume), `RecheckElementsUnlockedThreshold`. Al completamento cerca lo skin con quel `UnlockAchievementId` e chiama `GrantUnlock`. Lo stato è persistito in `SaveData`.

### Shop (`UI/`)
UI **2D** (il 3D è stato scartato per costo di performance e di lavoro Editor): `UIPeriodicTableShopPage`, `UIPeriodicElementCell`, `UIPeriodicSeriesPlaceholderCell`, `UIPeriodicElementCard`.
- Griglia fedele alla tavola reale: 18 gruppi × 7 periodi, con i buchi veri, scroll orizzontale.
- Lantanidi (57–71) e attinidi (89–103) hanno due placeholder nel gruppo 3 che aprono una vista dedicata con due file scorrevoli.
- Tap su una casella apre la card di dettaglio con animazione di scala (non zoom di camera): simbolo, nome, numero atomico, stato, costo o condizione, azione seleziona/sblocca.
- La card ha tre stati visivi: bloccata (spenta/desaturata), sbloccata (colore pieno e glow), attiva (indicatore distinto).

---

## 5. Economia

- **Guadagno a fine run** (`BlockoutGameplayState.OnWellFull`): `floor(score / 100)` + `5 × N` per ogni clear con N ≥ 2. Aggiorna `SaveData.coins` e `progress.lifetimeCoins`.
- **Spesa**: solo tramite `TryUnlockSkin`.
- **Bonus record personale**: non deciso, non implementato.

### `SaveData` (campi Blockout)
`coins`, `activeSkinId`, `unlockedSkinIds`, `completedAchievementIds`, `runsCompleted`, `loginStreakDays`. `PlayerProgressData.lifetimeCoins` sta nel template.

> ⚠️ `coins = 4000` iniziali è un valore di **test**. Va riportato al valore reale prima del rilascio.

---

## 6. Build mobile e lezioni dal device

- Android: `com.hp55games.blockout`, product `Blockout`, company `55hpgames`, portrait.
- Primo build su device il 16/09/2026.
- **Regola: non usare `AsyncOperation.allowSceneActivation = false`** mentre girano caricamenti Addressables. Su device una scena ferma al 90% blocca tutta la pipeline di caricamento condivisa, comprese le pagine UI. Il preload di gameplay ora completa il load e disattiva la scena (`SceneFlowService`, `MainMenuState`).
- `AddressablesContentLoader` emette un warning se un `InstantiateAsync` resta pending oltre 3s: serve come diagnostico di questo tipo di stallo.
- Il warning "Overlay FadeIn timed out" (`SceneFlowService`) compariva a quasi ogni sessione: `UIOverlayService` instanziava il prefab del fade via Addressables al primo `FadeInAsync` reale, che coincide sempre col tap su Play — proprio mentre il preload di `02_Gameplay` compete per la stessa pipeline Addressables, tutto dentro il budget di 1s pensato solo per il tween. Fix: `IUIOverlayService.PrewarmAsync()`, chiamato da `UIServiceInstaller.Awake()` appena i servizi UI sono registrati, instanzia il fade in anticipo mentre il menu sta ancora caricando.
- Il race FSM/Shop push è stato corretto lato chiamante (`ff7b992`); `UINavigationService` ora serializza anche Push/Replace/Pop internamente (`SemaphoreSlim`), quindi la difesa non dipende più dal solo call site.

---

## 7. Test

PlayMode, uno per sistema. Blockout: `ScoreCalculator`, `BlockoutFallCurve`, `BlockoutTimeDifficultyModifier`, `BlockoutWell`, `BlockoutShapeSet`, `BlockoutSpawner`, `PieceController`, `WellCellRenderer`, `BlockoutClearBehaviour`, `JuicyClearBehaviour`, `PeriodicMeltClearBehaviour`, `PeriodicTableLayout`, `BlockoutSkinService`, `BlockoutAchievementService`, `BlockoutGameplayState`. Polycubes: `VoxelGrid`, `PlacementRules`, `PolycubeGenerator`, `PhasedIntervalCurve`.

Per feature multi-fase: test mirati sui pezzi più rischiosi (es. idempotenza con un contatore reale di chiamate) e QA in scena con dati reali completi, non mock.

---

## 8. Aperti (al 17/09/2026)

### Tecnici
- `BlockoutDebugOverlay` (TEMP) e oggetti runtime TEMP ancora in scena.
- Bundle ID di iOS e Standalone ancora quelli del template URP.
- `Game.Content.asmdef` senza script.
- Chiave di localizzazione `ui.main.shop` mancante; il tasto Credits punta a una pagina inesistente.
- Diversi `Resolve<>` diretti lanciano eccezione se il servizio manca, invece di fallire in modo gestito.
- I commenti di `BlockoutInputHandler` citano `03_input_translation_raycast.md`, che non è nel repo.
- `GetElementShopEntries` esclude Default/Profondità/Juicy Clear rimandando a uno "shop skin separato": nel repo non esiste una pagina UI per quello shop.

### Design e bilanciamento
- Da decidere dopo il playtest: `_timerDecrementPerTick`, soglie achievement, saldo iniziale coins.
- Bonus record personale.
- Polish shop: glow per lo stato sbloccato, animazione della card, illustrazioni solido/gas/liquido. Rimandato insieme al polish UI generale.
- Game feel del core (audio, haptics, camera feedback).

### Fuori scope esplicito
Palette per-pezzo (elementi diversi nella stessa run), direzione laterale delle sferule, achievement "invita un amico", teche 3D, ads.

### Roadmap
Fasi P0 → P4 con gate go/no-go, su Notion sotto *Remake Minimal*. Fase attuale: **P0**, cioè validazione del core su device (controlli, camera, framerate) e pulizia tecnica.
