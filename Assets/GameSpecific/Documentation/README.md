# Blockout — Documento di progetto

Tetris 3D con policubi in un pozzo, mobile portrait, sessione infinita. Costruito sul **55HP Mobile Template**.

- **Repo**: `55hp/55HPGamesMinimalGame`. Si lavora su `develop`; `main` riceve solo milestone chiuse.
- **Fonti di verità**: il repo — questo file e il codice. Notion è solo un archivio. Se questo file e il codice non coincidono, **vale il codice**, e questo file va corretto nello stesso commit.
- **Posizione**: questo file sta in `Assets/GameSpecific/Documentation/` perché Bezi vede solo `Assets/`, `Packages/` e `ProjectSettings/`. Il `README.md` alla radice del repo è solo un rimando per GitHub.
- **Istruzioni per gli agenti**: `CLAUDE.md` alla radice (Claude Code). Le regole di Bezi stanno nella configurazione di Bezi. Questo README è documentazione di progetto, non un file di regole.
- **Spec di implementazione e report**: `specs/`, un file per task (`NNNN-slug.md`, modello in `specs/_TEMPLATE.md`). Le spec le scrive Claude Code; Bezi aggiunge i report in coda, sotto `## Report`.

---

## 1. Architettura

| Layer | Percorso | Namespace | Regola |
|---|---|---|---|
| Core (template) | `Assets/Core/`, `Assets/Content/` | `hp55games.Mobile.*` | Generico. Niente logica né contenuti Blockout, prefab compresi |
| Polycubes | `Assets/GameSpecific/Features/Polycubes/` | `hp55games.Polycubes.*` | Game-agnostic: griglia voxel, forme, regole di piazzamento |
| Blockout | `Assets/GameSpecific/Features/Blockout/` | `hp55games.Blockout.*` | Tutto ciò che è specifico del gioco |
| Contenuti Blockout | `Assets/GameSpecific/Content/` | — | Config asset, skin, prefab UI |
| Test | `Assets/Tests/` | — | Vedi §8 |

### Servizi del template riusati

- **Score**: `IGameContextService.Score` / `BestScore`. Aggiorna il valore, poi pubblica `ScoreChangedEvent` (senza payload).
- **FSM**: `IGameStateMachine`. `BlockoutGameplayState` prende il posto di `GameplayState` via `IGameplayStateFactory`. Il fine partita usa `ResultState` del template.
- **Navigazione**: `IUINavigationService`. Push/Replace/Pop sono serializzati da un semaforo, quindi sicuri in concorrenza.
- **Overlay**: `IUIOverlayService`. Il fade è precaricato (`PrewarmAsync`) in `UIServiceInstaller.Awake`.
- **Input**: `IInputService`, sopra il Legacy Input Manager (`activeInputHandler: 0`, `StandaloneInputModule` sull'EventSystem). Il package Input System è installato ma non attivo. Un press che inizia sopra un elemento UI appartiene alla UI e non genera gesti di gioco.
- **Bootstrap**: `00_Bootstrap` → `ServiceRegistry.InstallDefaults()`. Scene: `01_Menu`, `02_Gameplay`, `03_Results`.
- **Back button (Android)**: `AndroidBackButtonHandler` su `00_Bootstrap`, priorità decisa da `BackPressRouter.Decide` a ogni pressione:
  1. stato `PauseState` → `ISceneFlowService.ResumeFromPauseAsync()` (prima dei popup: chiudere il popup di pausa direttamente lascerebbe l'FSM in pausa con `timeScale` 0);
  2. popup aperti → chiude quello in cima;
  3. `CanGoBack` → `PopAsync()`;
  4. root → `IBackAtRootService.Raise()`, che `BlockoutGameplayState` instrada a `GoToPauseAsync()`.

**Gap noto del template**: `IConfigCatalogService` non è in `InstallDefaults()` e richiede un `ConfigCatalogInstaller` in scena. La correzione va fatta nel template.

### Servizi Blockout

Registrati da `BlockoutGameplayStateInstaller`: `IGameplayStateFactory`, `IBlockoutSkinService`, `IBlockoutAchievementService`.

---

## 2. Core gameplay

- **Pozzo**: 5×5×12 (`BlockoutWellConfig`, `BlockoutWell.asset`). Livello 0 in basso (y=0), livello 11 in alto. Pavimento a y=−0,5, centro XZ in (2,2).
- **Forme**: 12 policubi (8 tetracubi + 4 pentacubi), `BlockoutShapeSet`.
- **Selezione forme**: random pesato seedato con cooldown lineare — una forma non si ripete mai due volte di fila (peso 0 subito dopo l'estrazione) e le sue probabilità risalgono di 1 per spawn, con tetto a `Count - 1`. Lo stesso seed produce sempre la stessa sequenza.
- **Movimento**: a step, transizione 0,3s interpolata.
- **Game over**: qualsiasi cella bloccata a Y ≥ altezza del pozzo, valutata **dopo** lock *e* collapse. Oppure spawn rifiutato perché la posizione di partenza è già occupata. Entrambi i casi passano dallo stesso evento `WellFull`.
- **Punteggio**: 100 × N², con N = numero di livelli puliti simultaneamente.

### Camera

`BlockoutWellCamera`: FOV di riferimento autorato a mano, scalato stile `CanvasScaler` per gli altri aspect ratio (si scala l'half-angle verticale inversamente all'aspect corrente rispetto a quello di riferimento, per preservare l'inquadratura orizzontale). Risoluzione di riferimento 1440×3120 (19.5:9).
Valori in scena: Position `(2, 18, 1)`, Rotation `(90, 0, 0)`, FOV `80`. **Sono una scelta autorata, non un risultato da ricalcolare.**

### Input

- **Swipe** = traslazione.
- **Bottoni HUD** (basso a destra): rotazione asse A → Z, rotazione asse B → X (solo orarie; Y è escluso, è l'asse di caduta), hard drop.
- Tap e long-press disattivati.
- **Mancini**: `HudHandednessMirror` + `IUIOptionsService.LeftHanded` specchiano il cluster ricalcolando sempre dai valori originali autorati, quindi nessun drift su toggle ripetuti.

### Difficoltà

Curva a fasi per pezzo (`BlockoutFallCurveConfig`) moltiplicata per un modificatore di sessione (`BlockoutTimeDifficultyModifier` + `BlockoutTimeDifficultyConfig`, decadimento config-driven: intervallo 10s, −10%). Il modificatore gira sul tempo reale della sessione, indipendente dal ciclo di vita del pezzo.

---

## 3. Colori per livello

È il meccanismo visivo centrale del gioco: **il colore comunica la quota.**

- I 12 colori sono autorati su `BlockoutWellConfig._levelColors` e letti via `GetLevelColor(int level)`. **Unica fonte**, condivisa: li leggono sia `BlockoutWellWireframe` (anelli, verticali e riempimento di ogni livello) sia `BlockoutSpawner`.
- Un **pezzo in caduta** usa il colore della skin attiva, uguale per tutte le sue celle.
- Al **lock**, ogni cella prende il colore del proprio livello (`cell.y`). Per cella, non per pezzo: un pezzo a cavallo di due livelli risulta bicolore.
- Dopo un **clear**, `WellCellRenderer.CollapseLayer` ricolora ogni cubo che scende in base alla nuova quota. Senza questo, dopo il primo clear il colore smetterebbe di indicare la quota reale.

Il wireframe del pozzo è costruito livello per livello (ogni livello è un wireframe 5×5×1 a sé, con un gap di 0,06 fra un piano e l'altro) e i cubi sono scalati a 0,88 della cella, così il wireframe resta visibile anche dove ci sono blocchi.

---

## 4. Sistema skin

Una skin definisce **solo l'aspetto del pezzo in caduta**. Non controlla il colore dei cubi bloccati, che viene dal livello.

### Campi di `BlockoutSkin`

| Campo | Ruolo |
|---|---|
| `SkinId` | Identificatore stabile per il save, indipendente dal nome file |
| `DisplayName` | Nome mostrato nello shop |
| `BaseColor` | Colore del pezzo in caduta (URP Lit base color) |
| `Metallic`, `Smoothness` | Parametri PBR, workflow Metallic |
| `EmissionIntensity` | Emissione = `BaseColor` × questo valore. 0 = nessuna |
| `ClearBehaviour` | Reazione visiva al clear. Tutte e 20 usano `NeutralClearBehaviour` |
| `CostInCoins`, `UnlockedByDefault`, `UnlockMethod` | Economia |

`CellSurface.FromSkin(skin)` deriva i valori applicati a ogni cubo tramite `MaterialPropertyBlock`. Il blocco viene **riscritto per intero a ogni `ShowCell`**: i cubi vengono dal pool, quindi un aggiornamento parziale lascerebbe un cubo riusato con i valori della skin precedente.

### Skin presenti

20 skin autorate, generate da `Assets/GameSpecific/Content/Skins/blockout_skins.json` tramite il menu `hp55games/Blockout/Import Skins` (`BlockoutSkinImporter`), in `Content/Skins/Skins/`.

Terra è gratuita e attiva di default. Le altre 19 costano fra 150 e 1200 coins: **valori placeholder, non bilanciati**.

Famiglie: naturali opachi (terra, erba, pietra, sabbia, legno) · acquatici (acqua, ghiaccio, neve) · caldi (fuoco, lava, fumo) · metalli (ferro, rame, oro, cromo) · esotici (cristallo, ossidiana, neon, cosmo, stella).

### `IBlockoutSkinService`

Skin attiva, stato di sblocco, acquisto con coins. Lo stato di sblocco per giocatore sta nel save; `UnlockedByDefault` sull'asset è l'unica eccezione a livello di skin.

---

## 5. Economia

- **Coins a fine run**: `floor(score / 100) + 5 × N` per ogni clear multiplo.
- **Bonus record personale**: non deciso.
- Tutti i valori economici sono placeholder mai tarati contro dati reali di sessione.

---

## 6. Localizzazione

`Assets/Resources/Localization/localization_master.txt`, 9 lingue (en, it, es, fr, de, ja, ko, zh-Hans, pt-BR), 135 chiavi.

**La UI di Blockout lo usa solo in parte**: l'HUD passa da `UILocalizedText`, mentre lo shop ha stringhe hardcoded in italiano nel codice (es. `UIPeriodicElementCard`). Da chiudere durante la riscrittura dello shop, non dopo.

---

## 7. Build mobile

- Android: IL2CPP, minSdk 22. `AndroidTargetSdkVersion` è ancora `Automatic` e va fissato esplicitamente per Google Play.
- Bundle ID Android `com.hp55games.blockout`. **iOS e Standalone sono ancora quelli del template URP** e vanno cambiati.
- Define symbols `IAP_ENABLED`, `ADS_ENABLED`, `ANALYTICS_ENABLED` sono attivi ma nessun file C# li usa: o si integra, o si tolgono.

---

## 8. Test

- **Blockout**: 15 file PlayMode in `Assets/Tests/PlayMode/Blockout/`. Coprono spawner, piece controller, well, score, difficoltà, skin service, achievement, clear behaviour, renderer, mirror HUD.
- **Polycubes**: 3 file PlayMode in `Assets/Tests/PlayMode/Polycubes/` (griglia voxel, generatore, regole di piazzamento).
- **Template**: 13 file PlayMode alla radice di `Assets/Tests/PlayMode/` (bootstrap, navigazione, overlay, popup, input, back button, servizi).
- **EditMode**: 1 file (`CorePresenceTests`).

Tool di analisi statica: menu `hp55games Tools/Static Scan/Run`.

---

## 9. In corso di rimozione

Il tema **Tavola Periodica** (118 elementi come skin, clear "melt" con densità reale, shop a forma di tavola periodica, achievement per gli elementi antichi) è stato **ritirato**. Il codice e gli asset sono ancora nel repo in attesa della rimozione:

- Runtime: `PeriodicTableLayout`, `PieceMaterialCategory`, `PeriodicMeltClearBehaviour`, `UIPeriodicTableShopPage`, `UIPeriodicElementCard`, `UIPeriodicElementCell`, `UIPeriodicSeriesPlaceholderCell`
- Editor: `BlockoutPeriodicElementImporter`
- Campi legacy su `BlockoutSkin`: `_pieceColors`, `_elementSymbol`, `_atomicNumber`, `_materialCategory`, `_densityNormalized`, `_isRadioactive`, più `EditorConfigureElement`
- Asset: 118 `Elements/BlockoutSkin_Element_*.asset`, `blockout_periodic_elements.json`, `BlockoutSkin_Default/Profondita/JuicyClear`, `PeriodicMeltClearBehaviour.asset`, `PeriodicMeltSpherule.prefab`, `JuicyClearBehaviour` + `JuicyClearPhysicsObject.prefab`
- Test: `PeriodicTableLayoutTests`, `PeriodicMeltClearBehaviourTests` da cancellare; `BlockoutSkinServiceTests`, `BlockoutAchievementServiceTests` da riscrivere

`BlockoutSkinService.GetElementShopEntries` diventa `GetShopEntries`; `BlockoutSkinShopEntry` perde `Position`.

---

## 10. Aperti

### Tecnici
- `BlockoutSpawner.Initialize` ha 12 parametri: da raccogliere in un contesto di run, ma solo dopo la rimozione sopra.
- 4 `Debug.Log` informativi a runtime non condizionali (`BlockoutGameplayState` ×2, `BlockoutSpawner`, `BlockoutKeyboardInputHandler`): da mettere dietro define. A runtime, i 35 `LogError` restano attivi e gli 8 `LogWarning` vanno rivisti uno per uno. I log dei tool Editor (`Blockout/Editor/`) sono fuori da questo conteggio e non vanno toccati.
- `BlockoutKeyboardInputHandler` è input di debug da tastiera: va escluso dalle build device (editor check o define).
- Achievement: dopo la rimozione degli sblocchi elemento, verificare se il sistema resta vuoto.
- **Save**: `SaveData` non ha un campo versione. La rimozione dei 118 `SkinId` elemento tocca i save esistenti: proposto un `saveVersion` con migrazione. Da decidere se i coins accumulati si mantengono o si azzerano.
- Rimozione Tavola Periodica (§9) e nuovo shop a 20 skin: da decidere se in un unico passaggio (raccomandato) o separati.

### Design e bilanciamento
- Costi delle skin e coins per run mai tarati su dati reali.
- Bonus record personale non deciso.
- Verso di rotazione dei bottoni HUD: da confermare in playtest (il segno si inverte in `UIBlockoutHUD`).
- Gate fra economia/meta e rilascio: Blockout resta portfolio o diventa prodotto.

### Fuori scope
- Fit camera esatto per device molto diversi dal range telefoni (es. tablet quadrati).
