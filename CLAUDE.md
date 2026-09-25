# CLAUDE.md

Blockout — tetris 3D con policubi in un pozzo 5×5×12, mobile portrait, Unity 2022.3.62f3 + URP, sopra il **55HP Mobile Template**.
Documentazione di progetto: `Assets/GameSpecific/Documentation/README.md` (architettura, gameplay, sistemi, aperti), d'ora in poi "il README". Il `README.md` alla radice è solo uno stub per GitHub: non metterci stato del progetto. **Fonte di verità: il repo** — il README e il codice. Notion non fa parte del flusso di lavoro degli agenti: è un archivio di Franci, non va letto né scritto. **Se codice e documenti divergono, vale il codice**, e il documento va corretto nello stesso commit.

Questo file contiene solo ciò che cambia *come* lavori. Per *com'è fatto* il progetto, leggi il README: non duplicarlo qui.

## Comandi

Progetto Unity Editor: non c'è build CLI, npm o lint.

- **Aprire**: Unity Hub, Editor `2022.3.62f3` esatto (deve combaciare con `ProjectSettings/ProjectVersion.txt`, altrimenti Unity reimporta tutto).
- **Play**: sempre da `Assets/Scenes/00_Bootstrap.unity`. Partire da `02_Gameplay` salta l'installazione dei servizi e fallisce.
- **Test**: Window → General → Test Runner. PlayMode in `Assets/Tests/PlayMode/` (sottocartelle `Blockout/`, `Polycubes/`, più i test del template alla radice), EditMode in `Assets/Tests/EditMode/`.
- **Analisi statica**: menu `hp55games Tools/Static Scan/Run` (`Assets/Editor/HP55_StaticScan.cs`). Controlla `FindObjectOfType` in Update, `Resources.Load` a runtime e altri pattern vietati qui sotto — **va eseguito prima di dichiarare finito un task che tocca il runtime**.
- **Importer skin**: menu `hp55games/Blockout/Import Skins` — rigenera i 20 asset da `blockout_skins.json`. Dopo, serve il bottone **"Scan Content folders"** nell'Inspector dell'asset `ConfigCatalog` (non è una voce di menu).
- `.sln`/`.csproj` alla radice sono generati da Unity: non modificarli a mano.

## L'Editor di Franci è quasi sempre aperto

Mentre lavori, l'Editor Unity è di solito già aperto su questo progetto (se `Library/EditorInstance.json` esiste, un'istanza è viva).

- **Mai lanciare Unity da CLI** (`-batchmode -executeMethod ... -quit`) su questo progetto: va in conflitto con l'istanza aperta (lock di `Library`, rischio di GUID duplicati). I test li lancia Franci dal Test Runner; tu indica quali.
- **Un `.cs` nuovo non ha `.meta` finché Franci non torna sull'Editor.** Quindi non scrivere a mano `.meta` né `.asset` YAML che referenziano il GUID di uno script appena creato: il GUID non esiste ancora. Creazione di istanze e assegnazioni restano un passo Editor (vedi Ownership).

## Ownership — chi tocca cosa

| Attore | Possiede | Non tocca |
|---|---|---|
| **Claude Code** (tu) | Script C#, test, asmdef, il README, le spec (sopra `## Report`), questo file | Scene, prefab, `.asset`, Addressables, i report di Bezi |
| **Bezi** (dentro l'Editor) | Scene, prefab, riferimenti Inspector, valori negli asset, layout UI, Addressables | Logica C#, salvo richiesta esplicita |
| **Franci** | Decisioni di design, priorità, push, playtest su device, piccole modifiche isolate nell'Editor | — |

**IMPORTANT — il confine codice/scena appartiene a Bezi.** Se uno script ha bisogno di un oggetto di scena o di un prefab, dichiari un `[SerializeField]` e ti fermi lì. Il collegamento lo fa Bezi. Setup Editor mancante si **segnala nel report**, non si compensa da codice.

Nel report, distingui:
- **per Bezi**: wiring, setup di scena/prefab, creazione e assegnazione di asset che fanno parte di un lavoro Editor vero;
- **per Franci**: una singola modifica isolata (un valore su un asset, rimuovere un asset, un'assegnazione). Bezi per regola rimanda a Franci questi casi, quindi non girarglieli.

### Spec e report di Bezi

Tutto sta nel repo, dentro `Assets/` perché Bezi vede solo `Assets/`, `Packages/` e `ProjectSettings/` (niente file alla radice, questo compreso).

- **Spec**: `Assets/GameSpecific/Documentation/specs/NNNN-slug.md`, un file per task, dal modello `specs/_TEMPLATE.md`. La scrivi tu quando un task ha una parte Editor, prima di passarlo a Bezi, con il commit di riferimento compilato.
- **Report**: Bezi li aggiunge in coda alla stessa spec, sotto `## Report`, e non modifica niente sopra. Tu non modifichi i report. `[///MANUAL_CHANGES]` marca modifiche fatte a mano nell'Editor.
- **I file nuovi sotto `Assets/` ricevono il `.meta` solo quando Franci torna sull'Editor** (vedi sopra): una spec appena creata può essere committata senza `.meta`, che arriva in un commit successivo.

**Bezi non vede il lavoro non committato**, e lo stesso vale per i prompt di Franci scritti guardando GitHub. Se un report o un prompt contraddice quello che hai su disco, il repo è indietro rispetto a te: controlla il file su disco prima di agire su numeri di riga o stati descritti, e non "correggere" il tuo lavoro sulla base di un report vecchio.

Bezi indica in ogni report **il commit su cui sta lavorando**. Confrontalo con il tuo stato locale (`git log`, `git status`): se è più vecchio del tuo lavoro, anche solo per modifiche non committate, il report descrive uno stato superato. Leggilo tenendone conto.

## Regole che cambiano cosa scrivi

1. **Vietato collegare a runtime oggetti autorabili**: niente `AddComponent`, `FindObjectOfType`, `GameObject.Find`, `transform.Find`, né `Camera.main` usato per trovare o attaccare componenti.
   Eccezione: oggetti generati dinamicamente per natura (pezzi, celle, linee procedurali). Se si ripetono, passano da `IObjectPoolService`.
2. **Riferimento nullo**: `Debug.LogError` che dice cosa manca e dove, poi il componente si disabilita. Mai ricostruire da codice layout, anchor, componenti UI o valori che stanno nel prefab.
3. **Una sola fonte di verità per ogni valore.** Un parametro vive nel prefab/scena **oppure** in un config asset, mai in entrambi. Il codice non sovrascrive valori autorati.
4. **Mai `ServiceRegistry.Resolve<>`** — lancia eccezione. Usa `TryResolve` e gestisci il caso mancante.
5. **Niente singleton né stato statico mutabile.** Tutto passa da `ServiceRegistry`.
6. **Gameplay ↔ UI solo via `IEventBus`.** Gli stati FSM navigano verso le pagine, non manipolano oggetti UI.
7. **Config**: `ScriptableObject` con `IConfigAsset`, letti via `IConfigCatalogService.Get<T>()` / `GetAll<T>()`. Mai `Resources.Load` a runtime (eccezione già esistente: `LocalizationService`).
8. **Persistenza**: solo `ISaveService` / `SaveData`, estendendoli.
9. **asmdef**: solo assembly runtime, mai le varianti `.Tests` / `.Editor.Tests` dei package.
10. **Log di debug rumorosi** dietro uno scripting define (es. `HP55_INPUT_DEBUG`), mai dietro flag statici. I `LogError` su condizioni reali restano sempre attivi.
11. **Stringhe mostrate al giocatore**: ogni stringa **nuova** passa da una chiave in `Assets/Resources/Localization/localization_master.txt` (9 lingue), mai testo hardcoded. Le stringhe hardcoded già presenti sono debito noto (README §6), da chiudere con lo shop: non convertirle di passaggio in task che non c'entrano.
12. **Chiavi Addressables**: costanti in `hp55games.Addr`, mai stringhe inline.
13. **Commit locali; push solo su conferma di Franci.** Se il task coinvolge anche Bezi, si pusha quando sono pronte entrambe le parti.
14. **Nessun riferimento a file che non esistono nel repo.**
15. **Testo UI sempre TextMeshPro.**
16. **Input: è attivo solo il Legacy Input Manager** (`activeInputHandler: 0`), con `StandaloneInputModule` sull'EventSystem. Il package `com.unity.inputsystem` è installato ma non attivo: non usarne le API a meno di una migrazione decisa esplicitamente.
17. **Codice Blockout fuori da `Assets/Core/`.** Il template resta generico; Polycubes resta game-agnostic. Mappa dei layer e namespace: README §1.

## Trappole note

- **`IConfigCatalogService` non è in `InstallDefaults()`** del template (README §1): la correzione va fatta nel template, non aggirata qui.
- **Lock → clear → respawn è tutto sincrono**, in un'unica catena di chiamate senza confine di frame (`PieceController.Lock()` → `BlockoutSpawner.OnPieceLocked` → `SpawnNext()`). Conseguenze:
  - `HandleLayerClears` deve girare **prima** di `SpawnNext()`, altrimenti il collapse trascina giù le celle del pezzo nuovo già mostrate.
  - Una nuova reazione al clear che deve avvenire *prima* dello spawn passa da `PieceController.Locked` (porta gli Y dei livelli puliti) ed è chiamata da `OnPieceLocked`, **non** dall'event bus: `LayersClearedEvent` arriva quando il pezzo successivo è già in scena. L'event bus va bene solo per reazioni indipendenti dall'ordine (punteggio, difficoltà).
- **Game over** si valuta solo dopo lock *e* collapse: un pezzo può bloccarsi sopra la soglia ed essere legittimo se il collapse lo riporta dentro.
- **Colore per livello**: `BlockoutWellConfig.GetLevelColor(int)` è l'unica fonte dei 12 colori. Se aggiungi un percorso che colora una cella bloccata, deve passare da lì — incluso dopo un collapse, altrimenti il colore smette di indicare la quota reale.
- **`WellCellRenderer` usa `MaterialPropertyBlock`**: i cubi vengono dal pool, quindi il blocco va riscritto per intero a ogni `ShowCell`, mai aggiornato parzialmente, o un cubo riusato si porta dietro i valori della skin precedente.

## Lingua

Commenti e documentazione esistenti sono misti italiano/inglese. **Adegua il file che stai modificando**, non convertirlo.
