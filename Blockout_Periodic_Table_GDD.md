# Blockout — Tema "Tavola Periodica": Game Design Document

Riferimento di design precedente: "Blockout — Sistema Skin: Game Design Document" (stesso set di documenti, stesso repo). Questo documento **estende** quel sistema con un terzo `BlockoutSkin` concettualmente diverso dai primi due: invece di uno skin singolo con estetica fissa, è un **set di 118 skin** (uno per elemento chimico) che condividono la stessa logica di comportamento ma variano per dati (colore, materiale, fisica).

Il core loop, le regole di gioco e il punteggio (100×N²) restano invariati — questo documento riguarda solo il layer estetico/meta-progressione, come i due skin precedenti.

## Concetto

Lo shop diventa la tavola periodica stessa: un layout scrollabile orizzontalmente che riproduce la disposizione reale degli elementi (caselle da sinistra/numero atomico basso verso destra/numero atomico alto). Ogni casella è un elemento sbloccabile.

Il giocatore seleziona un elemento sbloccato come "materiale attivo" — esattamente come oggi seleziona "Profondità" o "Juicy Clear". Alla prima installazione, il **Carbonio** è l'elemento di default, già sbloccato senza costo (coerente con essere l'elemento su cui è costruita la vita, e con l'estetica "opaca" neutra già presente nello skin di base).

Non c'è, per ora, la possibilità di assegnare elementi diversi a pezzi diversi nella stessa run — è un'idea per il futuro, esplicitamente fuori scope qui (vedi sezione dedicata).

## Estetica in game

Il materiale e il colore di ogni pezzo che cade sono determinati dall'elemento attivo. Ogni elemento ha:
- Un colore (palette standard di riconoscibilità chimica — lo stesso schema colori usato comunemente in visualizzazioni scientifiche, cioè non arbitrario ma riconoscibile a chi ha familiarità con la chimica)
- Una categoria di shader: metallico, opaco, o traslucido — riflette la natura fisica reale dell'elemento (i metalli sono metallici, i gas/elementi volatili sono traslucidi, i solidi non metallici come carbonio/zolfo/fosforo sono opachi)

Questo dà a ogni elemento un'identità visiva distinta pur riusando lo stesso set di 12 forme (8 tetracubi + 4 pentacubi) già esistente — cambia il "materiale", non la geometria.

## Comportamento al layer-clear: il "melt"

Quando uno strato (o più strati simultaneamente) viene eliminato, l'effetto visivo non è la semplice scomparsa vista nello skin di default né il "juicy" fisico dello skin omonimo — è una **fusione**:

1. Il blocco 5×5 di celle che compone lo strato eliminato scompare con un effetto di scioglimento (melt) — non un pop o un fade generico, ma la sensazione che il materiale si stia letteralmente liquefacendo.
2. Contestualmente, dal perimetro del pozzo, all'altezza dello strato appena eliminato, vengono generate **24 sferette** (6 per lato dei 4 lati del pozzo 5×5). Concettualmente: il materiale che si scioglie viene "tagliato" ed espulso dal pozzo stesso sotto forma di sfere — non è il blocco stesso che si rompe in sfere, è il pozzo che espelle il materiale fuso in forma sferica dal proprio perimetro.
3. Le sferette hanno lo stesso colore/materiale dell'elemento attivo in quel momento (coerenza visiva con il pezzo che le ha generate).

### Fisica delle sferette: la densità conta

Le sferette non cadono tutte allo stesso modo — il loro comportamento fisico iniziale riflette la densità reale dell'elemento attivo:
- Elementi molto leggeri (es. Idrogeno, Elio, gas nobili in generale) → le sferette ricevono un impulso verso l'**alto**, "volano" via dal pozzo.
- Elementi molto densi (es. Osmio, Piombo, i metalli pesanti in fondo alla tavola) → le sferette ricevono un impulso verso il **basso**, cadono rapidamente.
- Elementi di densità intermedia (es. Carbonio, il default) → comportamento vicino a una caduta gravitazionale normale, senza impulso marcato in nessuna direzione.

Il criterio è la densità reale dell'elemento (dato chimico pubblico, non inventato), non la massa atomica: è la densità — quanto un elemento è "compatto" — a determinare intuitivamente se una sostanza "galleggia"/vola o affonda/cade, non il solo peso dell'atomo. Il dataset di riferimento (118 elementi, densità reali, più una normalizzazione già calcolata) è fornito come file separato (`blockout_periodic_elements.json`) — vedi Technical Doc per come viene consumato.

Una direzione laterale del moto delle sferette (es. "l'ossigeno va ai lati") è stata considerata ma **esclusa da questa iterazione**: richiederebbe un secondo asse di dati chimici senza un criterio univoco chiaro, e aggiungerebbe complessità senza una motivazione di design ferma. Se ne riparla eventualmente in futuro, non bloccante ora.

## Sblocco ed economia

Tutti i 118 elementi sono presenti nello shop-tavola-periodica fin dal lancio della feature (non un roll-out progressivo a scaglioni). Ogni elemento ha un costo in coins da definire.

**La formula di costo per i 118 elementi non è definita in questo documento.** Il sistema di coins esistente (formula di guadagno: `floor(score/100) + 5×N` per multi-clear) resta invariato, ma la scala di costi con soli 2 skin precedenti (300/500 coins) non si estende automaticamente a un catalogo di 118 — è un open item esplicito, da ricalibrare separatamente prima o durante la fase di economia (vedi Technical Doc). Il Technical Doc implementa l'infrastruttura di costo/sblocco già pronta a ricevere questi valori quando saranno decisi, senza bloccarsi in attesa della decisione.

## Materiale/shader per categoria

Oltre al colore, ogni elemento porta una categoria di shader (metallico / opaco / traslucido) che determina l'aspetto del materiale del pezzo oltre al colore puro — coerente con l'estetica "candy shader" (HLSL puro, non Shader Graph) già in uso nel sistema skin esistente. La distribuzione reale tra le tre categorie sui 118 elementi non è uniforme (la maggioranza sono metalli) — è un riflesso della chimica reale, non uno squilibrio di design da correggere.

## Fuori scope (questa iterazione)

- Palette personalizzata: assegnare elementi diversi a shape diverse nella stessa run (idea futura esplicitamente menzionata, non bloccante ora)
- Direzione laterale del moto delle sferette basata su una seconda proprietà chimica
- Formula di costo/sblocco per i 118 elementi (economia da ricalibrare separatamente)
- Bonus record personale nella formula coins (già aperto nel documento skin system precedente, resta aperto)
- UI/UX dettagliata dello shop-tavola-periodica (solo la logica sottostante è in scope tecnico)
- Effetti particellari decorativi aggiuntivi sul momento del melt (idea di polish valutata, non richiesta, non bloccante)
