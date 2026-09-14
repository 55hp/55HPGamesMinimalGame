# Blockout — Sistema Skin: Technical Implementation Doc

Riferimento di design: "Blockout — Sistema Skin: Game Design Document" (stesso set di documenti). Questo file copre solo cosa implementare e in che ordine — la struttura interna delle classi è a discrezione di Claude Code, coerente con i pattern già in uso nel repo.

Target repo: `55hp/55HPGamesMinimalGame`, branch `develop`.

## Vincoli architetturali da rispettare

- Nessun singleton / stato statico mutabile — accesso via `ServiceRegistry.Register<T>()`/`Resolve<T>()`/`TryResolve<T>()`, come il resto del progetto.
- Config asset: `IConfigAsset` su uno `ScriptableObject`, risolvibile via `IConfigCatalogService`. Confermato: `IConfigAsset`/`IConfigCatalogService` supporta nativamente più istanze dello stesso tipo tramite `GetAll<T>()` — non è un pattern singleton-only, va bene per un numero crescente di skin nel tempo.
- Persistenza: il template ha già `ISaveService`/`SaveData` (JSON su `Application.persistentDataPath`, via `Load()`/`Save()`). `SaveData` ha già i campi `coins` (int) e `PlayerProgressData.lifetimeCoins` (int), presenti nello schema ma non ancora letti/scritti da nessuna parte del codice. Il sistema skin deve **estendere** questa struttura esistente (nuovi campi tipo `ActiveSkinId`, lista skin sbloccati), non creare un sistema di persistenza parallelo.
- Split game-agnostic/Blockout-specific: se qualche parte del sistema skin risultasse generalizzabile ad altri minigame futuri in questo repo, valuta se va in `Polycubes/` invece che in `Blockout/` — ma non forzare l'astrazione se non è naturale, la maggior parte di questo sistema è verosimilmente Blockout-specific (materiale dei pezzi, comportamento layer-clear).

## Ordine di implementazione

Ogni fase deve essere completa e verificabile (buildabile, testabile) prima di passare alla successiva.

**1. `BlockoutSkin` — data model**
`IConfigAsset` ScriptableObject. Deve includere fin da subito, anche se non ancora usati fino alla fase 6: costo in coins, stato di sblocco (o un modo per derivarlo dal save data), riferimento al materiale/colore, riferimento al comportamento al clear (vedi punto 2). Lo skin di default esistente (palette 12 colori) deve poter essere rappresentato come un `BlockoutSkin` sempre-sbloccato a costo zero.

**2. `IBlockoutClearBehaviour` — interfaccia comportamento al clear**
Interfaccia polimorfica: ogni skin porta il proprio comportamento al layer-clear invece di uno switch/enum centralizzato — un nuovo skin futuro non deve richiedere modifiche al codice condiviso di gestione del clear. Prima implementazione concreta: comportamento neutro/no-op, equivalente a quello attuale, per validare che l'aggancio nel punto di layer-clear esistente funzioni senza regressioni prima di costruire comportamenti più complessi.

**3. Servizio skin attivo**
Un servizio (risolvibile via `ServiceRegistry`) che tiene traccia dello skin correntemente attivo e lo applica al rendering dei pezzi, sostituendo l'attuale palette fissa a 12 colori in `BlockoutSpawner`. Usa `ISaveService`/`SaveData` per persistere quale skin è attivo tra le sessioni (nuovo campo, es. `ActiveSkinId`). In questa fase tutti gli skin esistenti sono trattati come sbloccati — la logica di sblocco reale arriva alla fase 6.

**4. Skin "Profondità" — reale**
Primo `BlockoutSkin` concreto. Comportamento al clear: eredita/usa l'implementazione neutra della fase 2 (nessun nuovo comportamento fisico richiesto). L'estetica specifica (materiale/wireframe narrativo) può essere abbozzata tecnicamente e rifinita manualmente in Editor da Franci in un secondo momento — non bloccante per chiudere questa fase.

**5. Skin "Juicy Clear" — reale**
Secondo `BlockoutSkin` concreto. Richiede una nuova implementazione di `IBlockoutClearBehaviour`: le celle dello strato eliminato spawnano come oggetti fisici (es. sfere) con gravità invertita o comportamento fisico equivalente, invece di sparire direttamente. Usa pooling (`IObjectPoolService`, già presente nel progetto — vedi `WellCellRenderer` come riferimento di pattern) per gli oggetti fisici spawnati, coerentemente con le convenzioni già in uso nel repo per oggetti ripetuti spawn/despawn.

**6. Economia coins**
Wiring dei campi `coins`/`lifetimeCoins` già presenti in `SaveData`/`PlayerProgressData` (attualmente inutilizzati) alla formula di guadagno definita nel GDD: `floor(finalScore / 100)` + `5 × N` per ogni evento di layer-clear multiplo con N layer simultanei durante la run. Applicare il guadagno a fine partita (punto di game-over esistente, `BlockoutGameOverEvent`/`BlockoutGameplayState`). Logica di spesa: sottrarre il costo di uno skin da `coins` al momento dello sblocco, aggiornare lo stato di sblocco persistito (fase 1/3). Bonus per nuovo record personale intenzionalmente escluso dalla formula per ora — non implementare, resta un open item futuro.

**7. Shop UI**
Fuori dallo scope tecnico stretto di questo documento (solo logica sottostante nelle fasi 1-6) — quando si arriva a questa fase, verificare con Franci il livello di dettaglio richiesto prima di procedere, dato che tocca UI/UX non ancora disegnata.

## Note

- Nessuna delle fasi 1-6 richiede una UI: sono tutte verificabili via test PlayMode e/o log di debug, seguendo lo stesso standard di test coverage già usato nelle fasi precedenti di Blockout (vedi `BlockoutTimeDifficultyModifierTests.cs`, `ScoreCalculatorTests.cs` come riferimento di stile).
- Se durante l'implementazione emerge che qualche assunzione di questo documento non regge contro il codice reale attuale (es. il punto di aggancio del layer-clear, o la forma esatta di `SaveData`), verificare contro il codice sorgente piuttosto che assumere che questo documento sia aggiornato — potrebbe non riflettere modifiche successive alla sua stesura.
