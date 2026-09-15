# Blockout — Shop "Tavola Periodica": Game Design Document

Riferimento di design precedente: "Blockout — Tema 'Tavola Periodica': Game Design Document" (stesso set di documenti). Questo documento copre la UI/UX dello shop, esplicitamente lasciata fuori scope in quel documento e nel relativo Technical Doc (Fase 7, "verificare con Franci il livello di dettaglio richiesto"). Copre anche lo sblocco dei 12 elementi noti fin dall'antichità, rimasto aperto nel documento precedente.

## Concetto

Lo shop riproduce la forma reale della tavola periodica — gruppi e periodi con i buchi autentici (il salto tra gruppo 2 e gruppo 13 nei primi due periodi, la separazione di lantanidi/attinidi), non una griglia compattata. La fedeltà alla forma reale è parte dell'identità della feature — è la stessa scelta che ha già motivato l'uso di densità/colore/categoria reali invece di valori arbitrari nel documento precedente.

La UI resta **2D**, non 3D: un'interfaccia piatta con elementi che *suggeriscono* l'estetica della teca a vetro (bordo, glow, ombra) senza essere mesh 3D con illuminazione reale in scena. Decisione esplicita: una versione 3D piena (teche modellate, luci per singola teca, zoom di camera) è stata considerata e scartata per questa iterazione — il costo di performance mobile e di lavoro Editor per 118 oggetti non è giustificato rispetto al beneficio, con uno stack UI 2D che può comunque comunicare bene l'idea di "teca" con tecniche più economiche (shader/overlay, non geometria aggiuntiva).

## Layout

- Orientamento: griglia fedele alla tavola periodica reale (18 colonne/gruppi, 7 periodi/righe, con i buchi nei punti corretti).
- Viewport: almeno 10 caselle visibili verticalmente e 4-5 orizzontalmente in una singola schermata, con scroll orizzontale per il resto.
- Lantanidi e attinidi (periodi 6 e 7, gruppo 3) non occupano il loro spazio reale nella griglia principale: la griglia principale mostra due caselle-placeholder in quella posizione (una per i lantanidi, una per gli attinidi). Toccando una di queste due caselle si apre una vista dedicata — due file di caselle scorrevoli orizzontalmente, una per la serie dei lantanidi (57-71) e una per quella degli attinidi (89-103) — da cui si accede alle singole caselle-elemento con lo stesso comportamento del resto della tavola.

## Interazione

- Tap su una casella-elemento (non un placeholder lantanidi/attinidi): la casella si espande — via animazione di scala/transizione UI, non zoom di camera 3D — fino a una card di dettaglio che mostra: simbolo, nome, numero atomico, stato (sbloccato/bloccato), costo o condizione di sblocco, pulsante per selezionarlo come elemento attivo (se sbloccato) o per procedere allo sblocco (se bloccato e la condizione è soddisfatta/i coins sono sufficienti).
- Tap fuori dalla card, o un pulsante di chiusura esplicito, torna alla vista tavola periodica.

## Stato visivo delle caselle

- **Bloccata**: teca visivamente "spenta" — overlay scuro/desaturato sulla casella, nessun glow. Comunica "non ancora acquisibile" a colpo d'occhio, coerente con l'idea di una teca col vetro oscurato e la luce interna spenta.
- **Sbloccata ma non attiva**: teca "illuminata" — colore pieno dell'elemento, un rim-light/glow sottile che suggerisce l'illuminazione interna della teca, senza il costo di una vera luce 3D in scena.
- **Attiva** (l'elemento correntemente selezionato per il gameplay): uno stato distinto aggiuntivo rispetto a "sbloccata" — un indicatore chiaro (bordo più marcato, badge, o simile) che comunica "questo è l'elemento che stai usando adesso", per evitare che il giocatore perda traccia di quale sia attivo scorrendo tra 118 caselle.

## Rappresentazione della sostanza nella teca

Ogni casella porta una piccola illustrazione/icona 2D che suggerisce la sostanza reale (nello spirito del riferimento — mostrare l'elemento reale, non solo un colore/simbolo astratto), variata per categoria invece di un asset unico per ciascuno dei 118 elementi:
- **Solidi (metallici/opachi)**: una forma compatta, aspetto pieno.
- **Gas**: un effetto leggero/nebuloso — qui, e solo qui, è dove un effetto particellare 2D minimale (poche particelle, non un vero sistema) può aggiungere valore, per comunicare visivamente lo stato gassoso senza appesantire la vista shop dove decine di caselle sono visibili insieme.
- **Liquidi** (Mercurio, Bromo — i soli due nel dataset): un effetto che suggerisce fluidità, distinto dai solidi.

Il dettaglio esatto di queste illustrazioni (icona disegnata vs texture procedurale per categoria) è affinabile in Editor, non bloccante per l'implementazione funzionale dello shop.

## Sblocco: tre meccanismi distinti

Il documento precedente lasciava aperta la formula di costo per i 118 elementi. Si risolve così, distinguendo tre casi:

**1. Carbonio — sempre sbloccato, costo zero.** Elemento di default già definito nel documento skin system. Nessun cambiamento.

**2. 106 elementi con anno di scoperta noto e univoco — costo in coins pari all'anno stesso.** Es. Idrogeno (scoperto 1766) costa 1766 coins, Osmio (1803) costa 1803 coins. Criterio semplice, trasparente al giocatore, e usa un dato chimico reale (coerente con densità/colore/categoria già trattati così nel documento precedente) invece di una scala arbitraria. Non richiede bilanciamento nascosto: il numero visibile nello shop è letteralmente l'anno storico.

**3. 11 elementi noti fin dall'antichità (nessuna data di scoperta puntuale attribuibile) — sbloccati tramite achievement, non coins.** Ferro, Rame, Zinco, Stagno, Piombo, Argento, Oro, Mercurio, Antimonio, Arsenico, Zolfo. Ciascuno legato a un achievement di gioco derivabile da dati che il gioco già produce localmente (run completate, punteggio in una singola run, giorni di gioco consecutivi, un multi-clear da almeno 3 strati, numero di elementi già sbloccati) — nessuna dipendenza da infrastruttura esterna o social:

| Elemento | Achievement |
|---|---|
| Ferro | Completa la prima run |
| Rame | Completa 10 run |
| Zinco | Completa 50 run |
| Stagno | Completa 100 run |
| Piombo | Gioca 2 giorni consecutivi |
| Argento | Gioca 5 giorni consecutivi |
| Oro | Gioca 14 giorni consecutivi |
| Mercurio | Punteggio ≥ 5.000 in una singola run |
| Antimonio | Punteggio ≥ 15.000 in una singola run |
| Arsenico | Un clear multiplo da ≥3 strati simultanei in una run |
| Zolfo | Sblocca almeno 20 elementi (achievement "meta", lega gli antichi al resto della progressione) |

Le soglie numeriche (10/50/100 run, 5.000/15.000 punti) sono valori di buon senso, non calibrati su dati reali di gioco — da considerare placeholder da tarare in playtest, non definitivi.

## Fuori scope (questa iterazione)

- Achievement "invita un amico": introdurrebbe referral tracking/deep link, un salto di complessità tecnica rispetto al resto (tutto derivabile da dati locali) — esplicitamente rimandato, non incluso tra gli 11 antichi.
- Teche 3D, illuminazione reale in scena, zoom di camera.
- Direzione visiva definitiva delle illustrazioni per categoria (solido/gas/liquido) — l'approccio è definito, l'esecuzione visiva è lavoro Editor successivo.
- Bilanciamento reale delle soglie achievement (numeri attuali sono placeholder).
