# Blockout — Documento di progetto

Tetris 3D con policubi in un pozzo, mobile portrait, sessione infinita. Costruito sul **55HP Mobile Template**.

- **Repo**: `55hp/55HPGamesMinimalGame`. Si lavora su `develop`; `main` riceve solo milestone chiuse.
- **Ultimo aggiornamento**: 18/09/2026, base `develop` @ `004e773`.
- **Fonti di verità**: il design (GDD e documentazione tecnica) sta su Notion. Se questo file e il codice non coincidono, **vale il codice**, e questo file va corretto nello stesso commit.
- **Voci ⏳**: descrivono lo **stato target** del refactor authoring (§9). Finché il refactor non è chiuso, il codice può ancora non corrispondere.

---

## 0. Workflow e ownership (leggere prima di tutto)

| Attore | Possiede | Non tocca |
|---|---|---|
| **Claude Code** (fuori Editor) | Script C#, test, asmdef, questo README | Scene, prefab, file `.asset`, impostazioni Addressables |
| **Bezi** (dentro Unity Editor) | Scene, prefab, riferimenti in Inspector, valori negli asset, layout UI, Addressables | Logica C# (salvo richiesta esplicita) |
| **Franci** | Decisioni di design, priorità, push, playtest su device | — |

### Regole per Claude Code

1. **Il confine tra codice e scena appartiene a Bezi.** Se uno script ha bisogno di un oggetto di scena o prefab, dichiara un `[SerializeField]`. Il collegamento in Inspector lo fa Bezi.
2. **Vietato collegare a runtime oggetti che si possono autorare.** Niente `AddComponent`, `FindObjectOfType`, `GameObject.Find`, `transform.Find`, e niente `Camera.main` usato per trovare o attaccare componenti.
   - Eccezione: oggetti generati dinamicamente per natura (pezzi, celle, sferule, linee procedurali). Se si ripetono, passano dal pool.
3. **Setup Editor mancante: si segnala, non si compensa.**
   - Se un riferimento è nullo: `Debug.LogError` chiaro (cosa manca e dove), poi il componente si disabilita.
   - Vietato ricostruire da codice layout, anchor, componenti UI o valori che dovrebbero stare nel prefab.
   - Se serve lavoro in Editor, fermati e scrivilo nel report come richiesta per Bezi.
4. **Una sola fonte di verità per ogni valore.** Un parametro vive o nel prefab/scena o in un config asset, mai in entrambi. Il codice non sovrascrive valori autorati.
5. **Mai `ServiceRegistry.Resolve<>`**, che lancia eccezione. Usa `TryResolve` e gestisci l'errore.
6. **Commit locali, push solo su conferma di Franci.** Se il task coinvolge anche Bezi, si pusha quando sono pronte entrambe le parti.
7. **Nessun riferimento a file che non esistono nel repo.**
8. **Aggiorna questo README** quando cambiano architettura, contratti o voci di §9.

---

## 1. Architettura

### Layer
| Layer | Percorso | Namespace | Regola |
|---|---|---|---|
| Core (template) | `Assets/Core/`, `Assets/Content/` | `hp55games.Mobile.*` | Generico. **Niente logica né contenuti Blockout**, prefab compresi |
| Polycubes | `Assets/GameSpecific/Features/Polycubes/` | `hp55games.Polycubes.*` | Game-agnostic: griglia voxel, forme, timing |
| Blockout | `Assets/GameSpecific/Features/Blockout/` | `hp55games.Blockout.*` | Tutto ciò che è specifico del gioco |
| Contenuti Blockout | `Assets/GameSpecific/Content/` | — | Config asset, skin, dataset, prefab UI Blockout |
| Test | `Assets/Tests/PlayMode/{Blockout,Polycubes}` | — | PlayMode, uno per sistema |

### Vincoli
- **Niente singleton né stato statico mutabile.** Tutto passa da `ServiceRegistry`.
- **Gameplay ↔ UI solo via `IEventBus`.** Gli stati FSM navigano verso le pagine, non manipolano oggetti UI.
- **Config**: `ScriptableObject` con `IConfigAsset`, letti via `IConfigCatalogService.Get<T>()` / `GetAll<T>()`.
- **Persistenza**: solo `ISaveService` / `SaveData`, estendendoli.
- **Spawn/despawn ripetuti**: `IObjectPoolService`.
- **asmdef**: solo assembly runtime, mai `.Tests` / `.Editor.Tests`.
- **Log di debug rumorosi**: dietro uno scripting define (es. `HP55_INPUT_DEBUG`), mai dietro flag statici.

### Servizi del template riusati
- **Score**: `IGameContextService.Score` / `BestScore`. Aggiorna il valore, poi pubblica `ScoreChangedEvent` (senza payload).
- **FSM**: `IGameStateMachine`. `BlockoutGameplayState` prende il posto di `GameplayState` via `IGameplayStateFactory`. Il fine partita usa `ResultState` del template.
- **Navigazione**: `IUINavigationService`. Push, Replace e Pop sono serializzati da un semaforo, quindi sicuri anche se chiamati in concorrenza.
- **Overlay**: `IUIOverlayService`. Il fade è precaricato (`PrewarmAsync`) in `UIServiceInstaller.Awake`; le chiamate concorrenti condividono lo stesso task di istanziazione.
- **Back button (Android)**: `AndroidBackButtonHandler` (`KeyCode.Escape`, anche il tasto Escape in Editor/desktop) chiude il popup in cima allo stack se `IUIPopupService.HasOpenPopups`, altrimenti fa `PopAsync()` sulla pagina corrente se `IUINavigationService.CanGoBack`. Nessuna gestione per-schermata. ⏳ Non è ancora attaccato a nessun GameObject — va messo su un oggetto persistente (es. `GameBootstrap`), Bezi.
- **Input**: `IInputService`. Un press che inizia sopra un elemento UI appartiene alla UI e non genera gesti di gioco.
- **Bootstrap**: `00_Bootstrap` → `ServiceRegistry.InstallDefaults()`. Scene: `01_Menu`, `02_Gameplay`, `03_Results`. In Editor si fa Play sempre da `00_Bootstrap`.
- **Gap noto del template**: `IConfigCatalogService` non è in `InstallDefaults()` e richiede un `ConfigCatalogInstaller` in scena. La correzione va fatta nel template.

### Servizi Blockout
Registrati da `BlockoutGameplayStateInstaller`:
- `IGameplayStateFactory`
- `IBlockoutSkinService`
- `IBlockoutAchievementService`

---

## 2. Core gameplay

| Parametro | Valore | Dove |
|---|---|---|
| Pozzo | 5 × 5 × 12 (W × D × H) | `BlockoutWell.asset` |
| Forme | fino a 12: 8 tetracubi + 4 pentacubi | `BlockoutShapeSet` |
| Movimento | a step, transizione 0.3s | `BlockoutFallCurve.asset` |
| Punteggio | `100 × N²`, con N = strati eliminati insieme | `ScoreCalculator` |

### Selezione forme — `BlockoutShapeSet`
- I 12 pezzi canonici (8 tetracubi generati da `PolycubeGenerator`, 4 pentacubi planari selezionati automaticamente) hanno un nome stabile assegnato per ordine di generazione — `Tetracube01`…`08`, `Pentacube01`…`04` — non lettere curate a mano (I/L/T/S), non esiste ancora quella curation. `BlockoutShapeSet.AllPieceNames` espone l'elenco.
- **`BlockoutShapeSelectionConfig`** (`IConfigAsset`, opzionale): una lista di `(pieceName, enabled)`. Un pezzo non elencato è abilitato di default. `BuildDefault()` filtra i 12 pezzi in base a questo; `GetPieceEntries()` espone la lista completa `(nome, abilitato)` per un'eventuale UI di selezione.
- Se la config disabilita **tutti** i pezzi, `BuildDefault()` ignora il filtro e torna ai 12 completi (log di errore) — non è possibile lasciare il gioco senza pezzi da generare.
- ⏳ Nessun asset `BlockoutShapeSelectionConfig` esiste ancora — va creato in Editor solo se/quando serve escludere dei pezzi (Bezi, §9).

### Camera — `BlockoutWellCamera`
- ⏳ Componente autorato sulla Main Camera di `02_Gameplay` (da verificare/completare in Editor, §9). La posizione è solo il Transform in scena, centrato sul pozzo: `(2, 20, 2)` con origine a zero — nessun offset da codice (`CameraPositionOffset` rimosso il 17/09, §9).
- Il FOV viene ricalcolato a runtime da: dimensioni del pozzo, aspect ratio, safe area, spazio HUD.
- Il padding orizzontale è `BlockoutWellConfig.HorizontalPaddingScreenFraction`: frazione della larghezza schermo **per lato** (default 0.1), costante su qualsiasi aspect ratio.
- Se la camera non è centrata in X/Z, il frustum si allarga per tenere il pozzo in frame, ma non ricentra (impossibile con un frustum simmetrico senza lens shift).

### Input — gesti (`BlockoutInputHandler`)
- **Swipe** (sul pezzo o fuori, posizione ignorata) → traslazione diretta nella direzione dello swipe sullo schermo: su/destra/giù/sinistra → pezzo su/destra/giù/sinistra. Nessun raycast, nessun gesto ruota più il pezzo (17/09) — la rotazione è solo bottoni, vedi sotto.
- **Tap fuori dal pezzo** → traslazione, proiettata sul piano orizzontale **all'altezza del pezzo attivo** (raycast, a differenza dello swipe: un tap non ha un delta da cui ricavare la direzione).
- **Tap sul pezzo** → nessun effetto.
- **Doppio tap** → **rimosso**.

### Input — bottoni HUD
Barra inferiore in horizontal layout. Ogni bottone è alto 1/10 dello schermo e largo 1/4, con un piccolo padding dal bordo.

| Bottone | Evento pubblicato |
|---|---|
| `BTN_RotateLeft` | `PieceRotateRequestedEvent(AxisA)` — **unico modo per ruotare** |
| `BTN_HardDrop` | `HardDropRequestedEvent` (unico modo per l'hard drop) |
| `BTN_RotateRight` | `PieceRotateRequestedEvent(AxisB)` — **unico modo per ruotare** |

- I bottoni si aggiungono ai gesti (traslazione), non li sostituiscono.
- Pubblicano gli stessi eventi dei gesti: non esiste un percorso di input parallelo.
- In Editor c'è anche `BlockoutKeyboardInputHandler`.

### Rotazione — vincoli (`PlacementRules.CanPlaceAt`)
Una rotazione è rifiutata (il pezzo resta nell'orientamento precedente) se farebbe sforare **le pareti laterali o il pavimento** del pozzo. **Eccezione (18/09): la faccia superiore no** — quella senza wireframe (§2, Camera) — quindi un pezzo può sporgere sopra il pozzo ruotando, non solo cadendo. `PlacementRules.CanPlaceAt(grid, shape, origin, allowAboveTop: true)` è l'overload usato solo da `PieceController.HandleRotateRequested`; ogni altro controllo di piazzamento (caduta, movimento, hard drop, lock) resta rigoroso su tutti i lati, overload a 3 argomenti invariato.

### Difficoltà
Due componenti che si moltiplicano:
1. **Curva per pezzo** (`PhasedIntervalCurve`): intervallo base 3.0s, ÷1.2 fino al pezzo 10, ÷1.1 fino al pezzo 20.
2. **Modificatore temporale** (`BlockoutTimeDifficultyModifier`): intervallo base 2.0s, −0.15s ogni 15s, −0.1s per ogni clear. Minimo combinato 0.5s.

> ⚠️ Il valore `_timerDecrementPerTick = 0.15` non è mai stato confermato. Si decide dopo il playtest.

> ℹ️ **Il modificatore temporale non si ferma in pausa, di proposito** (`BlockoutSpawner.Update` lo ticka con `Time.unscaledDeltaTime`, non `Time.deltaTime`) — "non pausabile" per spec: mettere in pausa per prendere tempo non deve congelare la rampa di difficoltà. `PieceController` (caduta/step del pezzo attivo) usa invece `Time.deltaTime` scalato e si ferma correttamente con `Time.timeScale = 0`. Verificato e confermato il 17/09 — non è un bug, non toccare senza una decisione di design esplicita.

### Eventi
`PieceLockedEvent`, `LayersClearedEvent`, `FallIntervalChangedEvent`, `BlockoutGameOverEvent`, `PieceMoveRequestedEvent`, `PieceRotateRequestedEvent`, `HardDropRequestedEvent`.

---

## 3. UI Blockout

### ⏳ HUD — `UIBlockoutHUD`
- **Prefab**: `Assets/GameSpecific/Content/UI/BlockoutHUD.prefab`
- **Chiave Addressables**: `content/ui/screens/blockout_hud`
- **Contenuto**: score, bottone pausa, barra inferiore con `BTN_RotateLeft` / `BTN_HardDrop` / `BTN_RotateRight`. **Niente Lives.**
- **Riferimenti**: tutti `[SerializeField]`, collegati da Bezi.
- **Comportamento**: aggiorna lo score su `ScoreChangedEvent`; i bottoni pubblicano gli eventi di input.
- **Navigazione**: `BlockoutGameplayState` ci arriva con `ReplaceAsync` e non gli attacca nulla.
- Il `UIGameHUD.prefab` del template resta generico.

### Shop Tavola Periodica
Classi: `UIPeriodicTableShopPage`, `UIPeriodicElementCell`, `UIPeriodicSeriesPlaceholderCell`, `UIPeriodicElementCard`.
- UI 2D, griglia fedele alla tavola reale: 18 gruppi × 7 periodi, buchi compresi, scroll orizzontale.
- Lantanidi (57–71) e attinidi (89–103) si aprono da due placeholder nel gruppo 3, in una vista con due file scorrevoli.
- Il tap su una cella apre una card con animazione di scala.
- Tre stati visivi per cella: bloccata / sbloccata / attiva.
- ⏳ Layout group, ContentSizeFitter, ScrollRect e anchor della pagina sono autorati nel prefab, non forzati da codice.

---

## 4. Sistema skin

Un `BlockoutSkin` (`IConfigAsset`) accoppia sempre **estetica** e **comportamento al clear**, non separabili. Uno solo è attivo alla volta ed è puramente cosmetico.

### Campi di `BlockoutSkin`
- **Comuni**: `SkinId`, `DisplayName`, `CostInCoins` (`-1` = non impostato), `UnlockedByDefault`, `PieceColors`, `ClearBehaviour`.
- **Solo elementi chimici**: `ElementSymbol`, `AtomicNumber` (>0 solo per gli elementi), `MaterialCategory` (Metallic / Opaque / Translucent), `DensityNormalized`, `UnlockMethod` (Default / Coins / Achievement), `UnlockAchievementId`.

### Comportamenti al clear
Implementano `IBlockoutClearBehaviour` (ScriptableObject). Sono polimorfici: un nuovo skin non tocca il codice condiviso.
- **`NeutralClearBehaviour`**: gli strati spariscono e basta.
- **`JuicyClearBehaviour`**: le celle diventano oggetti fisici, presi dal pool.
- **`PeriodicMeltClearBehaviour`**: effetto melt più 24 sferule dal pool (6 per lato), generate dal perimetro del pozzo.
  - Colore e materiale sono quelli dell'elemento.
  - L'impulso verticale deriva da `DensityNormalized` tramite una curva configurabile: densità bassa → verso l'alto, alta → verso il basso.

### Skin presenti (`Content/Skins/`)
- `Default`: palette a 12 colori, gratis, sempre sbloccato.
- `Profondita`: 300 coins.
- `JuicyClear`: 500 coins.
- `Elements/`: 118 skin elemento generati da `blockout_periodic_elements.json` con `BlockoutPeriodicElementImporter`. **Si rigenerano con l'importer, non si modificano a mano.**

### `IBlockoutSkinService`
- **`ActiveSkin`**: con fallback sul default.
- **`IsUnlocked`**
- **`SetActiveSkin(id)`**: no-op se lo skin è sconosciuto o bloccato.
- **`TryUnlockSkin(id)`**: restituisce `Success` / `UnknownSkinId` / `AlreadyUnlocked` / `NotEnoughCoins` / `CostNotSet`.
- **`GrantUnlock(id)`**: sblocco gratuito e idempotente, usato solo dagli achievement.
- **`GetElementShopEntries()`**: i 118 elementi ordinati per numero atomico, con la loro posizione nella tavola.

---

## 5. Tema Tavola Periodica

### Dataset
File: `Content/Skins/blockout_periodic_elements.json`.
- **Campi**: `atomicNumber`, `symbol`, `nameIt`, `densityGCm3`, `densityIsPredicted`, `densityNormalized`, `shaderCategory`, `stateAtRoomTemp`, `colorHex`, `unlockCostCoins`, `discoveryYear`, `knownSinceAntiquity`, `unlockMethod`.
- **Categorie shader**: 92 metallic, 14 translucent, 12 opaque. La proporzione riflette la chimica reale.
- **Sferule**: il comportamento dipende dalla densità, non dalla massa atomica.

### Sblocco
| Metodo | Elementi | Regola |
|---|---|---|
| `default` | Carbonio | Sempre sbloccato |
| `coins` | 106 | Costo = anno di scoperta |
| `achievement` | 11 antichi | Sblocco automatico al completamento |

### Achievement
Le soglie sono placeholder, non ancora bilanciate.

| Elemento | Id | Condizione |
|---|---|---|
| Fe | `first_run_completed` | Prima run |
| Cu / Zn / Sn | `runs_completed_10/50/100` | 10 / 50 / 100 run |
| Pb / Ag / Au | `login_streak_2/5/14` | 2 / 5 / 14 giorni consecutivi |
| Hg / Sb | `single_run_score_5000/15000` | Punteggio in una singola run |
| As | `multi_clear_3plus` | Clear da almeno 3 strati |
| S | `elements_unlocked_20` | Almeno 20 elementi sbloccati |

### `IBlockoutAchievementService`
- Metodi: `IsCompleted`, `GetDescription`, `RecordRunCompleted`, `RecordRunScore`, `RecordMultiClear`, `RecordLoginForToday`, `RecheckElementsUnlockedThreshold`.
- Al completamento di un achievement chiama `GrantUnlock` sullo skin collegato.

---

## 6. Economia

- **Guadagno a fine run**: `floor(score / 100)` + `5 × N` per ogni clear con N ≥ 2. Calcolato in `BlockoutGameplayState.OnWellFull`.
- **Spesa**: solo tramite `TryUnlockSkin`.
- **Bonus record personale**: non ancora deciso.
- **`SaveData`**: `coins`, `activeSkinId`, `unlockedSkinIds`, `completedAchievementIds`, `runsCompleted`, `loginStreakDays`, `progress.lifetimeCoins`.

> ⚠️ `coins = 4000` iniziali è un valore di test da ripristinare prima del rilascio.

---

## 7. Build mobile e lezioni dal device

- **Android**: `com.hp55games.blockout`, portrait. Primo test su device: 16/09/2026.
- **Mai `AsyncOperation.allowSceneActivation = false`** mentre sono in corso caricamenti Addressables. Su device blocca l'intera pipeline, pagine UI comprese.
- **Diagnostica**: `AddressablesContentLoader` emette un warning se un `InstantiateAsync` resta pending oltre 3s.
- **Validazione camera e input**: solo su device reale. Il Game View non basta.

---

## 8. Test

Test PlayMode, uno per sistema:
- **Blockout**: ScoreCalculator, FallCurve, TimeDifficultyModifier, Well, ShapeSet, Spawner, PieceController, WellCellRenderer, ClearBehaviour, JuicyClear, PeriodicMeltClear, PeriodicTableLayout, SkinService, AchievementService, GameplayState.
- **Polycubes**: VoxelGrid, PlacementRules, PolycubeGenerator, PhasedIntervalCurve.
- **Core**: UINavigationService, UIOverlayService.

---

## 9. Aperti

### ✅ Refactor authoring — lato codice (chiuso 17/09)
Rimossi i workaround a runtime che violavano §0:
- `BlockoutGameplayState`: niente più `AddComponent<BlockoutWellCamera>` su `Camera.main` né `FindObjectOfType<UIGameplayHUD>`/`AddComponent<BlockoutHUDInputButtons>`. Naviga a `blockout_hud` e basta.
- `BlockoutHUDInputButtons` eliminato, sostituito da `UIBlockoutHUD` (score, pausa, 3 bottoni azione — tutti riferimenti `[SerializeField]`).
- `BlockoutSpawner` si registra in `ServiceRegistry` (non più cercato con `FindObjectOfType` da `BlockoutGameplayState`/`BlockoutInputHandler`); il suo `_cellRenderer` è ora `[SerializeField]` invece di un `FindObjectOfType` interno.
- `BlockoutWellCamera`: nessun `FindObjectOfType`/`Camera.main` da sostituire (non ce n'erano — l'unico lookup, `UIRoot.FindOrCache()` per il `CanvasScaler`, resta: `UIRoot` vive nella scena separata `91_UI_Root`, un riferimento diretto non è possibile cross-scene).
- `CameraPositionOffset` rimosso (campo, uso, nessun test lo referenziava): la posizione ha ora una sola fonte, il Transform in scena.
- Shop: rimossi `EnsureFullScreenRect`, `EnsureSeriesSubViewIsScrollable`, il fallback `AddComponent<Outline>` in `UIPeriodicElementCell`.
- `Resolve<>` che lanciavano eccezione sistemati: `BlockoutSkinService` (3), `BlockoutAchievementService` (2), `BlockoutGameplayState` (1).
- Commenti morti (`02_camera_investigation.md`, `03_input_translation_raycast.md`) e nomi `(TEMP)` negli oggetti runtime rimossi.
- `ServiceRegistry.Unregister<T>()` aggiunto (mancava del tutto); `BlockoutSpawner` e `FeedbackService` (Core) ora si deregistrano in `OnDestroy` invece di contare solo sul sovrascrivere la entry al prossimo `Awake`.
- `_worldPadding` (world-space, leftover pre-percentuale) rimosso da `BlockoutWellCamera`. Il padding è solo orizzontale (`HorizontalPaddingScreenFraction`) — l'asse verticale/depth non ha alcun margine.

### ⏳ Refactor authoring — lato Editor (Bezi, ancora aperto)
- **`BlockoutHUD.prefab`** (nuovo, `Assets/GameSpecific/Content/UI/`, chiave Addressables `content/ui/screens/blockout_hud`): componente `UIBlockoutHUD` con `_scoreLabel`, `_pauseButton`, `_rotateLeftButton`/`_hardDropButton`/`_rotateRightButton` da collegare.
- **`UIGameHUD.prefab`** (template): va riportato allo stato originale — i bottoni aggiunti in `b8c299d` appartengono al nuovo `BlockoutHUD.prefab`, non al prefab generico.
- **`BlockoutSpawner`** (scena `02_Gameplay`): collegare `_cellRenderer` (`WellCellRenderer`) in Inspector — oggi senza reference non c'è visuale, ma il gioco funziona lo stesso.
- **`BlockoutWellCamera`**: da verificare che sia autorato sulla Main Camera di `02_Gameplay` con `_wellOrigin`/`_hudTopReservedCanvasUnits` collegati (`_worldPadding` non esiste più, va tolto se presente in scena).
- **Shop** (`UIPeriodicTableShopPage`/`UIPeriodicElementCell`, nel prefab): pagina root full-stretch (anchorMin 0,0 / anchorMax 1,1 / offset 0,0 / pivot 0.5,0.5); `_seriesSubViewContainer` con `HorizontalLayoutGroup` (childControlWidth=false, childControlHeight=true, childForceExpandWidth=false, childForceExpandHeight=true, childAlignment=MiddleLeft, spacing=8) + `ContentSizeFitter` (horizontalFit=PreferredSize); il suo genitore con uno `ScrollRect` (content = il container, viewport = se stesso, horizontal=true, vertical=false, movementType=Elastic); `UIPeriodicElementCell._activeIndicator` ora obbligatorio (es. `Outline` su `_elementColor`, effectColor ~(255,214,51), effectDistance (3,-3)).
- **`AndroidBackButtonHandler`** (nuovo, §1): attaccarlo a un GameObject persistente (`GameBootstrap` o simile in `00_Bootstrap`) — nessun `[SerializeField]`, nessun'altra configurazione necessaria.
- **`BlockoutShapeSelectionConfig`** (nuovo, opzionale, §2): da creare solo se/quando serve escludere dei pezzi dalla rotazione (`hp55games/Blockout/Shape Selection Config`, poi aggiungerlo al `ConfigCatalog`). Nomi validi: `BlockoutShapeSet.AllPieceNames` (`Tetracube01`…`08`, `Pentacube01`…`04`).

### Tecnici
- `PieceController` viene creato e distrutto a ogni pezzo, senza pool.
- Bundle ID di iOS e Standalone ancora quelli del template.
- Chiave di localizzazione `ui.main.shop` mancante; Credits punta a una pagina inesistente.
- Gli skin non elemento non hanno una UI di acquisto.

### Design e bilanciamento
- **Dopo il playtest**: `_timerDecrementPerTick`, soglie achievement, saldo iniziale coins, verso di rotazione dei bottoni.
- **Da decidere**: bonus record personale.
- **Rimandati**: polish dello shop, game feel (audio, haptics).

### Fuori scope
Palette diversa per ogni pezzo, direzione laterale delle sferule, achievement "invita un amico", teche 3D, ads.

### Roadmap
Fasi P0 → P4 con gate go/no-go (Notion, *Remake Minimal*). Fase attuale: **P0**, validazione del core su device.
