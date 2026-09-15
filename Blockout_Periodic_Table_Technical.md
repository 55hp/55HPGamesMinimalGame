# Blockout — Tema "Tavola Periodica": Technical Implementation Doc

Riferimento di design: "Blockout — Tema 'Tavola Periodica': Game Design Document" (stesso set di documenti). Riferimento architetturale pregresso: "Blockout — Sistema Skin: Technical Implementation Doc" — questo documento ne è un'estensione diretta, stessi vincoli, stesso stile di verifica.

Target repo: `55hp/55HPGamesMinimalGame`, branch `develop`.

Dataset di riferimento: `blockout_periodic_elements.json` (118 elementi, fornito come file separato — va importato nel repo, es. sotto `Assets/GameSpecific/Blockout/Content/Data/` o percorso equivalente coerente con le convenzioni già in uso). Contiene per ogni elemento: simbolo, nome, numero atomico, densità reale (g/cm³), una normalizzazione logaritmica della densità già calcolata (0 = più leggero, 1 = più denso), categoria shader (metallic/opaque/translucent), colore, stato di aggregazione. Il campo costo-sblocco è presente ma `null` — non ancora popolato (vedi Fase 5 e nota economia).

## Vincoli architetturali da rispettare

- Stessi vincoli del documento tecnico Skin System precedente: nessun singleton/stato statico mutabile, `ServiceRegistry` per l'accesso ai servizi, `IConfigAsset`/`IConfigCatalogService` per i dati, `ISaveService`/`SaveData` per la persistenza, pooling via `IObjectPoolService` per oggetti spawn/despawn ripetuti.
- Questo sistema **riusa** l'infrastruttura skin esistente (`BlockoutSkin`, `IBlockoutClearBehaviour`, il servizio skin attivo) invece di introdurne una parallela. Un elemento della tavola periodica è, architetturalmente, un `BlockoutSkin` come "Profondità" o "Juicy Clear" — la differenza è che ce ne sono 118 invece di 1, generati/derivati da un dataset invece che progettati singolarmente a mano.
- Non introdurre nulla in `Polycubes/` (layer game-agnostic): questo sistema è concettualmente Blockout-specific quanto lo erano gli skin precedenti — resta in `Blockout/`.
- Verificare contro il codice sorgente reale lo stato attuale di `BlockoutSkin`/`IBlockoutClearBehaviour`/il servizio skin prima di procedere: questo documento assume che le Fasi 1-6 del sistema skin precedente siano complete (lo erano al 2026-09-14), ma se qualcosa è cambiato nel frattempo, il codice reale ha la priorità su questo documento.

## Ordine di implementazione

**1. Estensione dati — `BlockoutSkin` per elemento chimico**
Il data model `BlockoutSkin` esistente va esteso (non ricreato) per rappresentare un elemento: aggiungere i campi necessari a portare simbolo, numero atomico, categoria shader, densità normalizzata. Valutare se generare i 118 asset `BlockoutSkin` a partire dal JSON con uno script di importazione Editor (coerente con l'ordine di grandezza — 118 asset creati a mano non è ragionevole) piuttosto che con creazione manuale una per una. Il Carbonio deve essere marcato come sempre-sbloccato a costo zero, stesso pattern già usato per lo skin di default a 12 colori.

**2. Shader/materiale per categoria**
Tre varianti di materiale (metallico, opaco, traslucido) coerenti con l'approccio HLSL puro già in uso per il candy shader esistente — non Shader Graph. Ogni `BlockoutSkin`-elemento referenzia colore + categoria, che insieme determinano quale variante di materiale/parametri applicare al pezzo. Il pattern di applicazione materiale già esistente (skin → rendering pezzo, gestito dal servizio skin attivo) va esteso per leggere questi due campi, non riscritto.

**3. `IBlockoutClearBehaviour` — comportamento "melt + sferette"**
Nuova implementazione dell'interfaccia esistente (stesso pattern di `JuicyClearBehaviour`, che va usato come riferimento diretto di codice per pooling/spawn di oggetti fisici al clear). Differenze rispetto a `JuicyClearBehaviour` da implementare:
- Le celle dello strato eliminato scompaiono con un effetto melt (shader/animazione — la scomparsa non è istantanea né un semplice fade, ma comunica scioglimento; il dettaglio esatto dell'effetto è affinabile in Editor, non bloccante per chiudere questa fase con un'implementazione funzionale).
- Vengono spawnate 24 sferette (pooled, `IObjectPoolService`, stesso pattern di `JuicyClearPhysicsObject`) posizionate sul perimetro del pozzo (6 per lato, 4 lati) all'altezza dello strato eliminato, non all'interno del volume del blocco stesso.
- Le sferette ereditano colore/materiale dall'elemento attivo al momento del clear.
- L'impulso fisico iniziale delle sferette è derivato dal campo di densità normalizzata dell'elemento attivo: normalizzazione bassa → impulso verso l'alto, normalizzazione alta → impulso verso il basso, valori intermedi → comportamento gravitazionale prossimo al normale. La curva esatta di mappatura (normalizzazione → intensità/direzione impulso) è un parametro tarabile, non hardcoded — esporlo come valori configurabili (es. sullo stesso `IConfigAsset` o un asset di configurazione dedicato), così da poter essere bilanciato senza ricompilare.

**4. Shop-tavola-periodica — solo logica dati, non UI**
Come per il sistema skin precedente, la UI/UX dello shop è fuori scope tecnico stretto qui. Ciò che è in scope: l'infrastruttura dati che la UI consumerà — una query/metodo che restituisce tutti i 118 `BlockoutSkin`-elemento con il loro stato (sbloccato/bloccato, costo, elemento attivo corrente), ordinabile per numero atomico per rispecchiare il layout a tavola periodica. Riusa `IConfigCatalogService.GetAll<BlockoutSkin>()`, già confermato capace di gestire multi-istanza nativamente.

**5. Economia — infrastruttura pronta, valori assenti**
Il meccanismo di sblocco/spesa coins (già implementato nella Fase 6 del sistema skin precedente: `IsUnlocked`/`TryUnlockSkin`/`UnlockSkinResult`) si applica agli elementi senza modifiche — sono `BlockoutSkin` come gli altri. Il lavoro qui è solo: assicurarsi che il costo-sblocco dei 118 elementi (attualmente `null` nel dataset fornito) possa essere popolato in un secondo momento senza richiedere altre modifiche architetturali — es. un campo numerico opzionale che, se non impostato, blocca lo sblocco finché non viene definito, piuttosto che assumere un default silenzioso. Non inventare una formula di costo in questa fase: è un open item esplicito del GDD.

## Nota per Claude Code

Se durante l'implementazione emerge che un'assunzione di questo documento non regge contro il codice reale attuale (in particolare lo stato del sistema skin precedente, che potrebbe essere avanzato oltre il 2026-09-14), verificare contro il codice sorgente piuttosto che assumere questo documento aggiornato.

---

## Orchestrazione Claude Code ↔ Bezi (per Franci)

Split di responsabilità coerente con come avete lavorato finora sul sistema skin:

**Claude Code (fuori editor, da questi due documenti):**
- Fasi 1, 3, 4, 5 — tutto ciò che è dati/logica/servizi: estensione `BlockoutSkin`, script di importazione dei 118 asset dal JSON, `IBlockoutClearBehaviour` (parte logica: spawn, pooling, calcolo impulso da densità normalizzata), infrastruttura query per lo shop, wiring economia.
- Verificabile via test PlayMode, coerente con lo standard già seguito (`BlockoutSkinServiceTests.cs` come riferimento).

**Bezi (in Editor):**
- Fase 2 — i tre shader/materiale (metallico/opaco/traslucido): lavoro visivo diretto in Editor, non delegabile a un prompt testuale con la stessa qualità.
- Rifinitura dell'effetto melt (Fase 3): Claude Code può implementare la struttura funzionale (le celle scompaiono, le sferette spawnano correttamente), ma il *feel* dello scioglimento — shader di dissolve, timing, curve di animazione — è lavoro d'Editor iterativo, stesso trattamento riservato alla shader "Juicy Clear" nello skin system precedente.
- Import/verifica visiva dei 118 asset generati in Fase 1: un controllo a campione che colori/materiali si leggano bene sui pezzi in scena, specialmente le categorie meno rappresentate (traslucido, 14 elementi) rispetto a quella dominante (metallico, 92 elementi).

**Ordine pratico suggerito:** dai a Claude Code questo documento + il Technical Doc + il JSON insieme, fagli eseguire Fasi 1, 3 (parte logica), 4, 5 in sequenza autonoma come avete fatto per il sistema skin. Quando arriva a un punto che richiede scelte visive (shader, curva impulso da tarare "a occhio", feel del melt), si ferma e lo segnala — a quel punto passi tu la palla a Bezi in Editor, poi torni a Claude Code se restano parti logiche da chiudere dopo l'aggiustamento visivo.
