# Blockout — Sistema Skin: Game Design Document

Copre l'estensione del livello Flat con un sistema di skin cosmetiche sbloccabili. Il core loop, le regole di gioco e il punteggio (100×N²) restano invariati — questo documento riguarda solo il layer estetico/meta-progressione sopra il gioco esistente.

## Concetto

Un `BlockoutSkin` è un'entità unica che accoppia:
- **Estetica statica**: materiale/colore dei pezzi che cadono
- **Comportamento al layer-clear**: cosa succede visivamente quando uno strato viene eliminato

Le due parti sono **sempre insieme, non separabili**. Il giocatore ha un solo skin attivo alla volta; cambiare skin sostituisce entrambi gli aspetti in blocco.

Uno skin viene usato in tre contesti: rendering dei pezzi durante il gameplay, preview nello shop, item acquistabile nello shop.

## I due skin iniziali

**"Profondità"**
Estetica: la griglia wireframe sulle pareti del pozzo (già presente ed elemento funzionale per la leggibilità di profondità) diventa anche veicolo di lettura tematica — il pozzo come storia di ciò che si sta costruendo/riempiendo. Concept narrativo specifico non ancora deciso, lasciato aperto per iterazione visiva.
Comportamento al clear: nessun effetto speciale oltre a quello di base del gioco (comportamento "neutro").

**"Juicy Clear"**
Estetica: da definire in fase di iterazione visiva (non bloccante per l'implementazione tecnica).
Comportamento al clear: le celle dello strato eliminato non spariscono e basta — diventano oggetti fisici (es. sfere) soggetti a gravità invertita/fisica reale, per un momento di rilascio/soddisfazione visiva.

Entrambi gli skin sono cosmetici puri: nessuno dei due altera regole, difficoltà o punteggio.

## Sblocco ed economia

Gli skin si sbloccano con una valuta persistente ("coins"), accumulata partita dopo partita. In futuro potrebbe essere integrata anche una via di acquisizione tramite ads — non progettata ora, non bloccante.

**Formula di guadagno coins a fine partita:**
- Base: punteggio finale della partita ÷ 100 (coerente con la scala di scoring esistente 100/400/900/1600/2500)
- Bonus: +5 × N coins per ogni singolo evento di layer-clear multiplo durante la partita, dove N è il numero di strati cancellati simultaneamente in quel clear (es. un clear da 3 strati in un colpo solo vale 9 coins base + 15 coins bonus = 24 coins da quel singolo evento)
- Bonus per nuovo record personale: **non ancora deciso, lasciato aperto** — non bloccante per il resto del sistema

Ogni `BlockoutSkin` ha un costo in coins. Lo skin di default (quello attualmente in gioco, palette a 12 colori) è sempre sbloccato senza costo.

## Fuori scope

- Concept narrativo dettagliato del tema "Profondità"
- Direzione estetica dettagliata di "Juicy Clear"
- Bonus record personale nella formula coins
- Interfaccia utente dello shop (solo la logica sottostante è in scope tecnico, non la UI/UX)
- Acquisizione coins via ads
