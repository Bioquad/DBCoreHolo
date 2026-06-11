# Holografic DB — Manual de funcionament

Guia completa d'ús de l'aplicació. Per a la instal·lació i compilació, consulta el [README](README.md).

---

## Índex

1. [Primers passos](#1-primers-passos)
2. [El menú principal](#2-el-menú-principal)
3. [El dissenyador](#3-el-dissenyador)
4. [Controls de càmera i ratolí](#4-controls-de-càmera-i-ratolí)
5. [Taules](#5-taules)
6. [Camps](#6-camps)
7. [Relacions i regles MER](#7-relacions-i-regles-mer)
8. [Organització i grups](#8-organització-i-grups)
9. [Validació del model](#9-validació-del-model)
10. [Importar](#10-importar)
11. [Exportar](#11-exportar)
12. [Opcions](#12-opcions)
13. [Dreceres de teclat](#13-dreceres-de-teclat)
14. [El format .hdb i l'auto-desat](#14-el-format-hdb-i-lauto-desat)
15. [Resolució de problemes](#15-resolució-de-problemes)

---

## 1. Primers passos

En arrencar l'aplicació apareix la pantalla de càrrega i, tot seguit, el **menú principal** estil terminal. Per crear el teu primer model:

1. Tria **[ 2 ] Iniciar nou nucli de dades** i selecciona el motor SQL de destí (SQL Server, MySQL/MariaDB, PostgreSQL o ANSI).
2. S'obre el dissenyador amb l'esfera buida. Prem **Ins** o el botó **[ + ] NOVA TAULA** del panell inferior.
3. Selecciona la taula nova i afegeix camps amb **[ + ] NOU CAMP**. Marca'n almenys un com a **PK**.
4. Crea més taules i connecta-les amb **[ > ] NOVA RELACIÓ**.
5. Desa amb **Ctrl+S** — el projecte es guarda en un fitxer `.hdb`.

---

## 2. El menú principal

Navegació amb el ratolí o amb les **tecles numèriques directes** (sense Enter). **Escape** torna enrere.

| Opció | Funció |
|---|---|
| **[ 1 ] Carregar estructura des de disc** | Obre el submenú de càrrega |
| **[ 2 ] Iniciar nou nucli de dades** | Nou projecte (tria de motor SQL) |
| **[ 3 ] Configurar paràmetres** | Opcions de l'aplicació |
| **[ 4 ] Sortir al sistema operatiu** | Tanca l'aplicació |

### Submenú de càrrega

| Opció | Funció |
|---|---|
| **Fitxer local (.hdb)** | Obre un projecte desat |
| **Fitxer local (.mdf)** | Llegeix l'estructura d'una base de dades SQL Server LocalDB |
| **Importar script SQL (.sql)** | Reconstrueix el model a partir d'un script DDL |
| **Connectar servidor SQL Server** | Importa des d'un servidor en xarxa |

---

## 3. El dissenyador

La finestra principal té quatre zones:

```
┌──────────────────────────────────────────────────────┐
│ Arxiu  Model  Vista  Editar          [caixa de cerca]│  ← Barra de menú
├──────────┬───────────────────────────────────────────┤
│          │                                           │
│  Panell  │        ESFERA HOLOGRÀFICA 3D              │
│  lateral │   (taules, relacions, navegació)          │
│ (llistes)│                                           │
├──────────┴───────────────────────────────────────────┤
│  Panell inferior contextual (taula / relació / buit) │
│  Barra d'estat                                       │
└──────────────────────────────────────────────────────┘
```

### Barra de menú

- **Arxiu**: nou, obrir, desar, desar com, importar (`.hdb`, `.sql`, `.mdf`), exportar (T-SQL, MySQL/MariaDB, PostgreSQL, `.mdf`), connexió i accions de servidor.
- **Model**: nova taula, nova matriu, nova relació, generar DDL, validar model, mostrar/amagar esfera i llista lateral.
- **Vista**: **[ \* ] Veure tot** (encaixa tot el model a pantalla), **[ g ] Organitzar grup seleccionat**, **[ G ] Organitzar tots els grups**.
- **Editar**: desfer (Ctrl+Z) i refer (Ctrl+Y).
- **Caixa de cerca**: escriu el nom d'una taula i l'esfera rota fins a centrar-la.

### Panell lateral

Dues pestanyes: **TAULES** i **RELACIONS**.

- Les taules s'ordenen amb les **aïllades primer** (0 relacions) i després les relacionades, de més a menys connexions. Cada element mostra `Nom (camps/relacions)` amb el color del seu grup.
- Clic sobre un element → selecciona i centra l'objecte a l'esfera. La selecció a l'esfera també ressalta l'element de la llista (sincronització bidireccional).
- Desplaçament amb la **roda del ratolí** o els botons **▲▼**.
- Navegació amb teclat: **↑↓** mou la selecció, **Enter** la confirma.

### Panell inferior contextual

Canvia segons què hi hagi seleccionat:

- **Res seleccionat** → botons **[ + ] NOVA TAULA**, **[ ∑ ] NOVA MATRIU**, **[ > ] NOVA RELACIÓ**.
- **Taula seleccionada** → capçalera amb el nom (`esquema.NOM`), botons d'acció, la llista de camps (`núm | nom | tipus | longitud | flags | àlies | màscara | format | descripció`) i una fila editable amb la **descripció de la taula** i la **nota interna**.
- **Relació seleccionada** → fitxa amb nom, cardinalitat, origen → destí, ON DELETE / ON UPDATE i botons **EDITAR / ELIMINAR / TANCAR**.

---

## 4. Controls de càmera i ratolí

| Acció | Control |
|---|---|
| **Orbitar l'esfera** | Botó dret + arrossegar (amb inèrcia en deixar anar) |
| **Zoom** | Roda del ratolí (el zoom s'apropa cap al cursor) |
| **Desplaçar la vista (pan)** | Botó central mantingut + arrossegar |
| **Centrar l'holograma** | Clic ràpid del botó central (reinicia rotació, zoom i desplaçament) |
| **Seleccionar taula/relació** | Clic esquerre sobre l'objecte |
| **Moure una taula** | Clic esquerre + arrossegar (es desplaça per la superfície de l'esfera) |
| **Selecció de grup** | Clic esquerre sobre fons buit + arrossegar (rectangle); arrossegar qualsevol taula del grup mou tot el grup |
| **Desseleccionar** | Clic esquerre sobre el fons buit |
| **Veure-ho tot** | Menú *Vista → [ \* ] Veure tot* |

---

## 5. Taules

### Crear

- **Ins**, el menú *Model → Nova taula*, o el botó **[ + ] NOVA TAULA**.
- Les taules noves es col·loquen automàticament sobre l'esfera amb distribució uniforme (Fibonacci). El radi de l'esfera creix amb el nombre de taules.
- Les **matrius** (*Model → Nova matriu*) són taules amb representació visual especial, pensades per a entitats de tipus matriu/registre massiu, amb l'opció d'**heretar camps** de la taula seleccionada.

### Editar

Amb la taula seleccionada, la capçalera del panell inferior ofereix:

| Botó | Funció |
|---|---|
| **[ + ] NOU CAMP** | Obre l'editor de camp buit |
| **[ > ] NOVA RELACIÓ** | Obre l'editor de relació amb la taula com a origen |
| **[ C ] COLOR** | Canvia el color de grup (taronja, groc, verd, blau, vermell, cian, magenta, blanc) |
| **[ - ] ELIMINAR CAMP** | Elimina el camp seleccionat a la llista |
| **[DEL] ELIMINAR TAULA** | Elimina la taula (bloquejat si altres taules la referencien) |

- El **nom de la taula** es pot canviar fent clic sobre el nom a la capçalera.
- La **descripció** i la **nota interna** s'editen directament a les caixes del panell inferior (es desen en sortir del camp).

### Eliminar

El motor MER comprova si hi ha relacions entrants. Si una altra taula referencia la PK, l'eliminació es bloqueja: primer cal eliminar la relació. En eliminar una taula, les seves relacions sortints s'eliminen amb ella (i es restauren amb Ctrl+Z).

---

## 6. Camps

Doble clic sobre un camp (o seleccionar-lo i prémer **Enter**) obre l'**editor de camp**, organitzat en tres pestanyes:

### Pestanya BÀSIC

- **Nom** (obligatori) i **tipus de dada** (tot el catàleg T-SQL: enters, decimals exactes i aproximats, alfanumèrics, Unicode, binaris, data/hora, especials).
- **Longitud** (amb opció MAX), **precisió** i **escala** segons el tipus.
- Flags: **PK**, **FK**, **NOT NULL**, **UNIQUE**, **ÍNDEX**.
- **IDENTITY** amb llavor i increment.
- **Valor per defecte** (NULL, 0, GETDATE(), NEWID()... o text lliure).
- **Àlies** i **màscara d'entrada**.

> Marcar un camp com a **PK** força automàticament NOT NULL i desactiva FK (regla MER).

### Pestanya AVANÇAT

- **Expressió CHECK** i **col·lació**.
- **Dynamic Data Masking**: default(), partial(prefix, padding, sufix), email(), random().
- **Columna calculada**: fórmula AS (...) amb opció PERSISTED.
- **ROWGUIDCOL** i **FILESTREAM**.

### Pestanya METADADES

- **Descripció** (s'exporta com a `sp_addextendedproperty` / comentaris SQL).
- **Caption** i **format de visualització** (#,##0.00, dd/MM/yyyy...).

### Eliminar camps

**[ - ] ELIMINAR CAMP** o la tecla **Supr** amb el camp seleccionat. L'aplicació bloqueja l'eliminació si:

- el camp és una **PK referenciada** per relacions entrants, o
- el camp és una **FK amb relació definida** (cal eliminar primer la relació).

L'eliminació demana confirmació i es pot desfer amb **Ctrl+Z** (el camp es restaura a la seva posició original).

---

## 7. Relacions i regles MER

### Crear una relació

Tres maneres:

1. Botó **[ > ] NOVA RELACIÓ** (panell inferior o capçalera de taula).
2. Menú *Model → Nova relació...*
3. **Arrossegar des d'un camp** cap a una altra taula sobre l'esfera (goma elàstica).

L'**editor de relació** demana: taula origen → camp FK, taula destí → camp PK, cardinalitat (1:1, 1:M, M:1), accions **ON DELETE** i **ON UPDATE** (NO ACTION, CASCADE, SET NULL, SET DEFAULT, RESTRICT), WITH CHECK/NOCHECK, NOT FOR REPLICATION, relació desactivada i creació automàtica d'índex sobre la FK (recomanat). El nom del constraint es genera automàticament (`FK_Origen_Desti`).

### Regles que el motor valida en temps real

| Regla | Comportament |
|---|---|
| El camp destí **ha de ser PK** | Error bloquejant |
| Els **tipus de dada** de FK i PK han de coincidir | Error bloquejant |
| **M:M directa prohibida** | Error bloquejant — crea una taula intermediària |
| Relació **duplicada** (mateix origen/destí/camp) | Error bloquejant |
| Taules i camps han d'existir | Error bloquejant |
| **Auto-relació** (taula amb si mateixa) | Permesa (relació reflexiva) |

Els errors apareixen en vermell dins l'editor i l'operació no es desa fins que es corregeixen. Sobre l'esfera, les taules amb infraccions **parpellegen en vermell**.

> Convenció MER de l'aplicació: la taula **origen** (la que conté la FK) és sempre la **filla**; la taula **destí** (la que aporta la PK) és sempre la **mare**.

---

## 8. Organització i grups

- **Color de grup**: cada taula té un dels 8 colors. El color tenyeix la taula a l'esfera, les seves relacions sortints i el seu element a la llista lateral.
- ***Vista → [ G ] Organitzar tots els grups***: detecta automàticament comunitats al graf de relacions (Label Propagation), assigna colors diferents a grups adjacents, situa el **hub** de cada grup (la taula amb més relacions) en primer pla i distribueix la resta en cercles concèntrics. Les taules aïllades es col·loquen en una columna a la dreta.
- ***Vista → [ g ] Organitzar grup seleccionat***: distribueix les taules relacionades en cercle al voltant de la taula activa.
- **Selecció de grup amb rectangle**: arrossega sobre el fons per seleccionar les taules d'un grup i moure-les conjuntament.

---

## 9. Validació del model

*Model → Validar model* comprova el projecte sencer:

- Cada taula ha de tenir **almenys una PK**.
- Cada camp marcat com a **FK ha de tenir una relació** definida.
- Cada relació ha de referenciar **taules existents**.

El resultat es mostra en un diàleg amb la llista d'errors; les taules afectades queden marcades a l'esfera. La validació també s'executa abans de generar DDL.

---

## 10. Importar

### Script SQL (.sql)

*Arxiu → Importar SQL Script...* — el parser llegeix sentències `CREATE TABLE` i `ALTER TABLE ... FOREIGN KEY` i **normalitza el dialecte** automàticament (accepta scripts MySQL i PostgreSQL a més de T-SQL). Després apareix el **diàleg de selecció**: tria quines taules i relacions importar, i si vols crear un **projecte nou** o **afegir-les al projecte actual** (les taules que ja existeixen s'indiquen i s'ignoren).

### Fitxer .mdf (SQL Server LocalDB)

*Arxiu → Importar .mdf...* — requereix **SQL Server LocalDB** instal·lat. L'aplicació adjunta el fitxer, llegeix `INFORMATION_SCHEMA` i `sys.foreign_keys`, i reconstrueix el model amb la mateixa selecció de taules/relacions.

### Servidor SQL Server

*Arxiu → SQL Server...* obre el diàleg de connexió: servidor/instància, port, autenticació **Windows** o **SQL** (usuari/contrasenya), xifratge i llistat de bases de dades del servidor (botó **▼ LLISTAR**). El botó **⚡ TEST** verifica la connexió sense sortir del diàleg.

Un cop connectat, *Arxiu → Accions al servidor...* permet **importar l'estructura** de la base de dades triada (amb selecció de taules).

---

## 11. Exportar

### DDL (script .sql)

- *Arxiu → Exportar T-SQL...* — script complet SQL Server: `CREATE TABLE` amb tots els atributs (IDENTITY, computed columns, DEFAULT, CHECK, UNIQUE, MASKED...), `ALTER TABLE ... ADD CONSTRAINT ... FOREIGN KEY` amb ON DELETE/UPDATE, índexs `NONCLUSTERED` sobre les FK i `sp_addextendedproperty` per a les descripcions.
- *Arxiu → Exportar MySQL / MariaDB...* — backticks, `AUTO_INCREMENT`, `ENGINE=InnoDB`.
- *Arxiu → Exportar PostgreSQL...* — `GENERATED AS IDENTITY`/`SERIAL`, esquemes, `COMMENT ON`.

*Model → Generar DDL...* mostra la **previsualització** del script amb botons per copiar al porta-retalls o desar.

### Fitxer .mdf

*Arxiu → Exportar a .mdf (LocalDB)...* crea una base de dades física. Tria nom, directori de destí i mode:

| Mode | Comportament |
|---|---|
| **CREAR NOU** | Falla si la base de dades ja existeix |
| **ELIMINAR I RECREAR** | ⚠ Destructiu — esborra totes les dades existents |
| **APLICAR DIFERÈNCIES** | Afegeix només taules i columnes noves |

### Servidor SQL Server

Amb una connexió activa, *Accions al servidor...* ofereix:

- **Exportar projecte complet** — crea la BD nova o aplica diferències.
- **Enviar parts** — selecciona taules concretes; les FK entre elles s'inclouen automàticament i les taules ja existents s'ignoren.

Totes les operacions destructives o de creació demanen **confirmació prèvia**.

---

## 12. Opcions

Menú principal *[ 3 ] Configurar paràmetres* o des del dissenyador. Disponible:

- **Tema de color**: taronja (per defecte), verd, cian o blanc, sobre fons fosc.
- **Mida de font**: petita, normal o gran.
- **Mostrar la malla de l'esfera 3D** (es pot desactivar per veure només taules i relacions).
- **Mode debug**: mostra coordenades de càmera a la barra d'estat.
- **Idioma**: English, Català, Castellano. El canvi s'aplica a tots els formularis oberts; alguns textos es completen en reiniciar.

Les preferències es desen a `opcions.json`, al costat de l'executable. Aquest fitxer és personal i **no s'ha de pujar al repositori** (ja l'exclou el `.gitignore`).

---

## 13. Dreceres de teclat

| Drecera | Acció |
|---|---|
| **Ctrl+N** | Nou projecte |
| **Ctrl+O** | Obrir projecte .hdb |
| **Ctrl+S** | Desar |
| **Ctrl+Shift+S** | Desar com... |
| **Ctrl+Z** | Desfer (fins a 50 operacions) |
| **Ctrl+Y** | Refer |
| **Ins** | Nova taula |
| **Supr** | Eliminar l'element seleccionat (camp → relació → taula, segons context) |
| **↑ / ↓** | Navegar per la llista lateral o pels camps de la taula activa |
| **Enter** | Confirmar selecció / obrir l'editor del camp seleccionat |
| **Escape** | Cancel·lar diàleg / tornar enrere al menú |

---

## 14. El format .hdb i l'auto-desat

Els projectes es desen com a **JSON UTF-8 indentat**: nom del projecte, motor SQL, dates, estat de la càmera (rotació, zoom i desplaçament) i la llista completa de taules (amb tots els atributs de cada camp) i relacions. En obrir un projecte, la càmera es restaura tal com l'havies deixat.

- L'**auto-desat** actua cada 5 minuts si el projecte ja té fitxer assignat, i ho indica a la barra d'estat sense interrompre't.
- El títol de la finestra mostra un **asterisc** (`*`) quan hi ha canvis sense desar, i l'aplicació demana confirmació abans de tancar o substituir un projecte modificat.

Com que és JSON pla, els fitxers `.hdb` funcionen molt bé amb Git: els canvis es poden revisar amb un diff normal.

---

## 15. Resolució de problemes

**"Cal tenir SQL Server LocalDB instal·lat"**
Les funcions `.mdf` requereixen LocalDB. Descarrega'l de <https://aka.ms/sqllocaldb> (inclòs també amb SQL Server Express i amb Visual Studio).

**No es pot eliminar una taula o un camp PK**
És el comportament esperat del motor MER: una PK referenciada no es pot eliminar. Elimina primer les relacions entrants (selecciona la línia de relació a l'esfera → ELIMINAR, o tecla Supr).

**La connexió al servidor expira (timeout)**
Comprova el nom de la instància i el port, que el servei SQL Server accepti connexions remotes (TCP/IP activat) i el tallafoc. Prova primer el botó **⚡ TEST**.

**No es detecten taules en importar un .sql**
El parser busca sentències `CREATE TABLE`. Si el script només conté `INSERT` o procediments, no hi ha estructura a importar. Els dialectes MySQL/PostgreSQL es normalitzen automàticament, però sintaxis molt exòtiques poden requerir adaptació manual.

**L'aplicació arrenca però l'esfera no es veu**
Comprova a *Opcions* que "Mostrar la malla de l'esfera 3D" estigui activat, o prem *Vista → [ \* ] Veure tot* per recentrar la càmera. El clic ràpid del **botó central** del ratolí també reinicia la vista.

---

*Holografic DB — Manual de funcionament*
