# Blockout — Shop "Tavola Periodica": Technical Implementation Doc

Riferimento di design: "Blockout — Shop 'Tavola Periodica': Game Design Document" (stesso set di documenti). Riferimento architetturale pregresso: i due Technical Doc precedenti (Sistema Skin, Tema Tavola Periodica) — stessi vincoli, stesso stile di verifica.

Target repo: `55hp/55HPGamesMinimalGame`, branch `develop`.

Dataset di riferimento: `blockout_periodic_elements.json`, aggiornato con i campi `unlockMethod` ("default" | "coins" | "achievement"), `unlockCostCoins` (popolato per i 106 elementi a costo-coins, `null` altrove), `unlockAchievementId` (stringa identificativa, presente solo per gli 11 elementi ad achievement).

## Vincoli architetturali da rispettare

- Stessi vincoli dei due documenti precedenti: `ServiceRegistry`, `IConfigAsset`/`IConfigCatalogService`, `ISaveService`/`SaveData`, pooling via `IObjectPoolService`.
- Questa è UI sopra un'infrastruttura dati già esistente (`GetAll<BlockoutSkin>()`, `IsUnlocked`/`TryUnlockSkin`/`SetActiveSkin` dal sistema skin). Non duplicare logica di sblocco/attivazione già presente — lo shop la consuma, non la reimplementa.
- Nuovo elemento architetturale introdotto qui: un sistema di achievement. Verificare prima se il template (`55HPGamesMobileTemplate`) ha già un servizio achievement generico (coerente con l'impostazione "estendi l'esistente, non duplicare" già seguita per `ISaveService`/coins) — se non esiste, va introdotto come nuovo servizio minimale via `ServiceRegistry`, scope limitato a tracciare il completamento dei trigger elencati nel GDD (prima run, N run completate, streak di login, punteggio soglia, multi-clear N≥3, N elementi sbloccati), persistito via `ISaveService`/`SaveData` come i coins.

## Ordine di implementazione

**1. Estensione dati — costo/sblocco per i 118 elementi**
`BlockoutSkin` (già esteso nella Fase 1 del documento precedente per i campi elemento) riceve i tre nuovi campi dal dataset: `unlockMethod`, `unlockCostCoins` (già presente come concetto ma finora sempre `null`/`-1` — ora popolato per 106 elementi), `unlockAchievementId`. Rigenerare/aggiornare i 118 asset dallo script di importazione già esistente (Fase 1 del documento precedente), non ricrearli a mano.

**2. Servizio achievement (se non già presente nel template)**
Servizio minimale, `ServiceRegistry`-based, che espone il completamento dei trigger elencati nel GDD e persiste lo stato via `ISaveService`/`SaveData` (nuova struttura dati, es. `HashSet<string>`/lista di achievement id completati). I trigger da coprire:
- Prima run completata
- Run completate: soglie 10/50/100
- Login streak: soglie 2/5/14 giorni consecutivi
- Punteggio singola run: soglie 5.000/15.000
- Multi-clear con N≥3 strati simultanei in una run (dato già disponibile: è lo stesso N usato nella formula coins esistente)
- Elementi sbloccati: soglia 20

Ogni trigger, al momento in cui la condizione diventa vera, sblocca automaticamente il `BlockoutSkin`-elemento associato (via `unlockAchievementId` → lookup nel catalogo) — nessuna azione manuale richiesta nello shop, a differenza dello sblocco a coins (Fase 3/6 del documento precedente), dove l'azione di spesa resta esplicita. La logica di "sblocco effettivo" (stato persistito) resta `TryUnlockSkin`/equivalente già esistente, chiamata direttamente dal servizio achievement al momento in cui la condizione diventa vera, non su richiesta dell'utente.

**3. Query dati per lo shop**
Un metodo/servizio che restituisce tutti i 118 `BlockoutSkin`-elemento con stato completo per il rendering: sbloccato/bloccato, elemento attivo o no, costo (coins) o achievement associato con relativo stato di completamento, ordinati/indicizzati per numero atomico e per posizione nella griglia (gruppo/periodo, coerente col layout del GDD). Riusa `IConfigCatalogService.GetAll<BlockoutSkin>()`.

**4. UI — griglia principale**
Layout scrollabile orizzontalmente, fedele a gruppi/periodi reali con i buchi corretti (vedi GDD per il pattern esatto: colonna 1 = 7 righe, colonna 2 = 6 righe da periodo 2, colonne 3-12 = 4 righe da periodo 4, etc.). Due caselle-placeholder in riga 6 e riga 7, colonna 3, per l'accesso a lantanidi/attinidi. Ogni casella riflette lo stato visivo definito nel GDD (bloccata/sbloccata/attiva) — tre stati distinti, non due.

**5. UI — vista lantanidi/attinidi**
Vista dedicata raggiunta dal tap sui due placeholder, due file scorrevoli orizzontalmente (57-71, 89-103), stesso comportamento di casella delle altre.

**6. UI — card di dettaglio elemento**
Transizione/animazione di scala dalla casella alla card a schermo pieno (o quasi), non zoom di camera. Contenuto: simbolo, nome, numero atomico, stato, costo/condizione di sblocco, azione (seleziona se sbloccato, sblocca se le condizioni sono soddisfatte, altrimenti mostra la condizione mancante).

**7. Illustrazione per categoria (solido/gas/liquido)**
Tre varianti visive minime per le caselle (coerenti con `shaderCategory`/`stateAtRoomTemp` già nel dataset): solido compatto, gas con effetto particellare 2D leggero, liquido con effetto di fluidità. L'implementazione funzionale (which variant renders for which element) è delegabile a Claude Code; l'aspetto visivo specifico delle tre varianti è lavoro Editor (vedi orchestrazione sotto).

## Nota per Claude Code

Come nei documenti precedenti: se un'assunzione qui non regge contro lo stato reale del codice (in particolare la presenza/assenza di un servizio achievement esistente, o lo stato attuale dell'infrastruttura skin dopo il lavoro già fatto sul Tema Tavola Periodica), verificare contro il codice sorgente piuttosto che assumere questo documento aggiornato.

---

## Orchestrazione Claude Code ↔ Bezi (per Franci)

Split diverso dai due documenti precedenti: qui il lavoro UI/Editor è una parte sostanziale, non solo rifinitura a valle.

**Claude Code (fuori editor, da questi due documenti + il precedente):**
- Fasi 1, 2, 3 — dati, servizio achievement, query per lo shop. Stesso trattamento delle fasi logiche precedenti: verificabile via test PlayMode.
- Fasi 4, 5, 6 — la **struttura funzionale** della UI (layout scrollabile, navigazione, transizioni, binding dati↔caselle) può essere scritta da Claude Code se il progetto usa UI Toolkit/uGUI in modo programmabile — ma la resa visiva finale (spaziatura, proporzioni, leggibilità reale su schermo mobile) va verificata e rifinita in Editor da Franci/Bezi, non presa per buona dal solo codice.

**Bezi (in Editor):**
- Fase 7 — le tre varianti visive (solido/gas/liquido): lavoro visivo diretto, stesso trattamento riservato agli shader candy e al feel del melt nei documenti precedenti.
- Verifica visiva della griglia scrollabile con dati reali (118 caselle, non un mock a 5 elementi): leggibilità su viewport mobile reale, comportamento dello scroll, resa dei tre stati (bloccata/sbloccata/attiva) a colpo d'occhio.
- Rifinitura della transizione casella→card (Fase 6): il feel dell'animazione è lavoro iterativo d'Editor, non qualcosa da bloccare in attesa che il codice sia "perfetto" al primo tentativo.

**Ordine pratico suggerito:** dai a Claude Code i tre documenti (Sistema Skin, Tema Tavola Periodica, Shop) insieme al JSON aggiornato, fasi 1-3 autonome, poi 4-6 come primo passaggio funzionale, quindi passi la palla a Bezi per verifica visiva reale e Fase 7 (illustrazioni). Come nei documenti precedenti, quando Claude Code arriva a un punto che richiede una scelta visiva a occhio, si ferma e lo segnala invece di indovinare.
