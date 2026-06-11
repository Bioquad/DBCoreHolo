# Holographic DB — Operation Manual

Complete guide to using the application. For installation and compilation instructions, please refer to the [README](README.md).

---

## Index

1. [Getting Started](#1-getting-started)
2. [The Main Menu](#2-the-main-menu)
3. [The Designer](#3-the-designer)
4. [Camera and Mouse Controls](#4-camera-and-mouse-controls)
5. [Tables](#5-tables)
6. [Fields](#6-fields)
7. [Relationships and ERM Rules](#7-relationships-and-erm-rules)
8. [Organization and Groups](#8-organization-and-groups)
9. [Model Validation](#9-model-validation)
10. [Importing](#10-importing)
11. [Exporting](#11-exporting)
12. [Options](#12-options)
13. [Keyboard Shortcuts](#13-keyboard-shortcuts)
14. [The .hdb Format and Auto-save](#14-the-hdb-format-and-auto-save)
15. [Troubleshooting](#15-troubleshooting)

---

## 1. Getting Started

When you launch the application, the loading screen appears, followed by the terminal-style **main menu**. To create your first model:

1. Choose **[ 2 ] Start new data core** and select the target SQL engine (SQL Server, MySQL/MariaDB, PostgreSQL, or ANSI).
2. The designer opens with an empty sphere. Press **Ins** or the **[ + ] NEW TABLE** button on the bottom panel.
3. Select the new table and add fields using **[ + ] NEW FIELD**. Mark at least one as a **PK** (Primary Key).
4. Create more tables and connect them using **[ > ] NEW RELATIONSHIP**.
5. Save your work with **Ctrl+S** — the project is saved in an `.hdb` file.

---

## 2. The Main Menu

Navigate using either the mouse or **direct numeric keys** (no need to press Enter). Pressing **Escape** takes you back.

| Option | Function |
|---|---|
| **[ 1 ] Load structure from disk** | Opens the loading submenu |
| **[ 2 ] Start new data core** | New project (SQL engine selection) |
| **[ 3 ] Configure parameters** | Application options |
| **[ 4 ] Exit to operating system** | Closes the application |

### Loading Submenu

| Option | Function |
|---|---|
| **Local file (.hdb)** | Opens a saved project |
| **Local file (.mdf)** | Reads the structure of a SQL Server LocalDB database |
| **Import SQL script (.sql)** | Rebuilds the model from a DDL script |
| **Connect to SQL Server** | Imports from a network server |

---

## 3. The Designer

The main window is divided into four zones:

┌──────────────────────────────────────────────────────┐
│ File   Model  View   Edit             [search box]  │  ← Menu bar
├──────────┬───────────────────────────────────────────┤
│          │                                           │
│  Side    │        3D HOLOGRAPHIC SPHERE              │
│  panel   │   (tables, relationships, navigation)     │
│ (lists)  │                                           │
├──────────┴───────────────────────────────────────────┤
│  Contextual bottom panel (table / relationship / empty)│
│  Status bar                                          │
└──────────────────────────────────────────────────────┘

### Menu Bar

- **File**: New, Open, Save, Save As, Import (`.hdb`, `.sql`, `.mdf`), Export (T-SQL, MySQL/MariaDB, PostgreSQL, `.mdf`), Connection, and Server Actions.
- **Model**: New Table, New Matrix, New Relationship, Generate DDL, Validate Model, Show/Hide Sphere, and Side List.
- **View**: **[ \* ] View All** (fits the entire model to the screen), **[ g ] Organize Selected Group**, **[ G ] Organize All Groups**.
- **Edit**: Undo (Ctrl+Z) and Redo (Ctrl+Y).
- **Search Box**: Type the name of a table, and the sphere will rotate to center it.

### Side Panel

Consists of two tabs: **TABLES** and **RELATIONSHIPS**.

- Tables are sorted with **isolated ones first** (0 relationships), followed by connected tables ranked from most to fewest connections. Each item displays `Name (fields/relationships)` using its corresponding group color.
- Clicking an item selects and centers the object on the sphere. Conversely, selecting an object on the sphere highlights its item in the list (two-way synchronization).
- Scroll using the **mouse wheel** or the **▲▼** buttons.
- Keyboard navigation: **↑↓** moves the selection, **Enter** confirms it.

### Contextual Bottom Panel

Changes dynamically based on the current selection:

- **Nothing selected** → Displays the **[ + ] NEW TABLE**, **[ ∑ ] NEW MATRIX**, and **[ > ] NEW RELATIONSHIP** buttons.
- **Table selected** → Displays a header with the table name (`schema.NAME`), action buttons, the fields list (`num | name | type | length | flags | alias | mask | format | description`), and editable rows for the **table description** and **internal note**.
- **Relationship selected** → Displays a details card containing the name, cardinality, source → destination, ON DELETE / ON UPDATE rules, and **EDIT / DELETE / CLOSE** buttons.

---

## 4. Camera and Mouse Controls

| Action | Control |
|---|---|
| **Orbit the sphere** | Right-click + drag (with inertia upon release) |
| **Zoom** | Mouse wheel (zooms into the cursor's position) |
| **Pan the view** | Hold middle-click + drag |
| **Center the hologram** | Quick middle-click (resets rotation, zoom, and panning) |
| **Select table/relationship** | Left-click on the object |
| **Move a table** | Left-click + drag (slides across the sphere's surface) |
| **Group selection** | Left-click on empty background + drag (bounding box); dragging any table within the group moves the entire group |
| **Deselect** | Left-click on the empty background |
| **View all** | Menu *View → [ \* ] View All* |

---

## 5. Tables

### Create

- Press **Ins**, go to *Model → New Table*, or click the **[ + ] NEW TABLE** button.
- New tables are automatically placed onto the sphere using a uniform distribution (Fibonacci). The sphere's radius scales up as the number of tables increases.
- **Matrices** (*Model → New Matrix*) are tables with a specialized visual representation designed for matrix/bulk-record entities, featuring an option to **inherit fields** from the currently selected table.

### Edit

When a table is selected, the bottom panel header provides the following options:

| Button | Function |
|---|---|
| **[ + ] NEW FIELD** | Opens a blank field editor |
| **[ > ] NEW RELATIONSHIP** | Opens the relationship editor with the current table as the source |
| **[ C ] COLOR** | Changes the group color (Orange, Yellow, Green, Blue, Red, Cyan, Magenta, White) |
| **[ - ] DELETE FIELD** | Deletes the selected field from the list |
| **[DEL] DELETE TABLE** | Deletes the table (locked if referenced by other tables) |

- The **table name** can be changed by clicking directly on the name in the header.
- The **description** and **internal note** are edited directly within their text boxes in the bottom panel (saved automatically when leaving the field).

### Delete

The ERM engine continuously checks for incoming relationships. If another table references the PK, deletion is blocked, and you must remove the relationship first. When deleting a table, its outgoing relationships are deleted alongside it (and can be restored with Ctrl+Z).

---

## 6. Fields

Double-clicking a field (or selecting it and pressing **Enter**) opens the **Field Editor**, organized into three tabs:

### BASIC Tab

- **Name** (required) and **Data Type** (the complete T-SQL catalog: integers, exact and approximate decimals, alphanumeric, Unicode, binary, date/time, and special types).
- **Length** (with a MAX option), **Precision**, and **Scale** depending on the type.
- Flags: **PK**, **FK**, **NOT NULL**, **UNIQUE**, **INDEX**.
- **IDENTITY** with seed and increment values.
- **Default value** (NULL, 0, GETDATE(), NEWID()... or free text).
- **Alias** and **Input Mask**.

> Marking a field as a **PK** automatically enforces NOT NULL and disables FK (ERM rule).

### ADVANCED Tab

- **CHECK Expression** and **Collation**.
- **Dynamic Data Masking**: default(), partial(prefix, padding, suffix), email(), random().
- **Computed Column**: formula AS (...) with a PERSISTED option.
- **ROWGUIDCOL** and **FILESTREAM**.

### METADATA Tab

- **Description** (exported as `sp_addextendedproperty` / SQL comments).
- **Caption** and **Display Format** (#,##0.00, dd/MM/yyyy...).

### Delete Fields

Click **[ - ] DELETE FIELD** or press the **Delete** key with the field selected. The application blocks deletion if:

- The field is a **referenced PK** by incoming relationships, or
- The field is an **FK with a defined relationship** (the relationship must be deleted first).

Deletion prompts a confirmation dialog and can be undone with **Ctrl+Z** (the field returns to its original position).

---

## 7. Relationships and ERM Rules

### Create a Relationship

There are three ways to do this:

1. Click the **[ > ] NEW RELATIONSHIP** button (bottom panel or table header).
2. Go to *Model → New Relationship...*
3. **Drag from a field** to another table on the sphere (rubber-banding).

The **Relationship Editor** requires: Source Table → FK Field, Destination Table → PK Field, Cardinality (1:1, 1:M, M:1), **ON DELETE** and **ON UPDATE** actions (NO ACTION, CASCADE, SET NULL, SET DEFAULT, RESTRICT), WITH CHECK/NOCHECK, NOT FOR REPLICATION, relationship disabled flag, and automatic index creation on the FK (recommended). The constraint name is generated automatically (`FK_Source_Destination`).

### Real-time Engine Validations

| Rule | Behavior |
|---|---|
| Destination field **must be a PK** | Blocking error |
| FK and PK **data types** must match | Blocking error |
| **Direct M:M prohibited** | Blocking error — create an intermediary table instead |
| **Duplicate** relationship (same source/destination/field) | Blocking error |
| Tables and fields must exist | Blocking error |
| **Self-relationship** (table linking to itself) | Allowed (reflexive relationship) |

Errors appear in red inside the editor, and the operation cannot be saved until they are resolved. On the sphere, tables with violations **flash in red**.

> Application ERM Convention: The **source** table (the one containing the FK) is always the **child**; the **destination** table (the one providing the PK) is always the **parent**.

---

## 8. Organization and Groups

- **Group Color**: Each table is assigned one of 8 colors. This color tints the table on the sphere, its outgoing relationships, and its corresponding item in the side list.
- ***View → [ G ] Organize All Groups***: Automatically detects communities within the relationship graph (Label Propagation), assigns distinct colors to adjacent groups, brings the **hub** of each group (the table with the most connections) to the foreground, and distributes the rest in concentric circles. Isolated tables are organized into a column on the right.
- ***View → [ g ] Organize Selected Group***: Distributes related tables in a circle around the active table.
- **Bounding Box Group Selection**: Drag across the background to select tables belonging to a group and move them together.

---

## 9. Model Validation

*Model → Validate Model* analyzes the entire project:

- Every table must have **at least one PK**.
- Every field marked as an **FK must have a defined relationship**.
- Every relationship must reference **existing tables**.

The results are displayed in a dialog box listing all errors; affected tables are highlighted on the sphere. Validation also runs automatically before generating DDL.

---

## 10. Importing

### SQL Script (.sql)

*File → Import SQL Script...* — the parser reads `CREATE TABLE` and `ALTER TABLE ... FOREIGN KEY` statements and automatically **normalizes the dialect** (it accepts MySQL and PostgreSQL scripts in addition to T-SQL). A **selection dialog** then appears: choose which tables and relationships to import, and specify whether to create a **new project** or **add them to the current project** (existing tables will be flagged and ignored).

### .mdf File (SQL Server LocalDB)

*File → Import .mdf...* — requires **SQL Server LocalDB** to be installed. The application attaches the file, reads `INFORMATION_SCHEMA` and `sys.foreign_keys`, and reconstructs the model using the same table/relationship selection interface.

### SQL Server Instance

*File → SQL Server...* opens the connection dialog: Server/Instance, Port, **Windows** or **SQL** Authentication (username/password), encryption, and the server database list (via the **▼ LIST** button). The **⚡ TEST** button verifies connectivity without closing the dialog box.

Once connected, *File → Server Actions...* allows you to **import the structure** of the chosen database (with table selection support).

---

## 11. Exporting

### DDL (.sql script)

- *File → Export T-SQL...* — Generates a complete SQL Server script: `CREATE TABLE` statements with all attributes (IDENTITY, computed columns, DEFAULT, CHECK, UNIQUE, MASKED...), `ALTER TABLE ... ADD CONSTRAINT ... FOREIGN KEY` with ON DELETE/UPDATE rules, `NONCLUSTERED` indexes on FKs, and `sp_addextendedproperty` for descriptions.
- *File → Export MySQL / MariaDB...* — Generates a script utilizing backticks, `AUTO_INCREMENT`, and `ENGINE=InnoDB`.
- *File → Export PostgreSQL...* — Generates a script utilizing `GENERATED AS IDENTITY`/`SERIAL`, schemas, and `COMMENT ON`.

*Model → Generate DDL...* opens a **preview** of the script with options to copy it to the clipboard or save it to disk.

### .mdf File

*File → Export to .mdf (LocalDB)...* creates a physical database file. Specify the name, target directory, and operation mode:

| Mode | Behavior |
|---|---|
| **CREATE NEW** | Fails if the database already exists |
| **DROP AND RECREATE** | ⚠ Destructive — wipes out all existing data |
| **APPLY CHANGES** | Schema diff — appends only new tables and columns |

### SQL Server Instance

With an active connection, *Server Actions...* provides options to:

- **Export Full Project** — Creates the new database or applies structural differences.
- **Push Components** — Select specific tables to push; FKs between them are included automatically, and existing tables are skipped.

All destructive or creation-level operations prompt a **prior confirmation**.

---

## 12. Options

Accessible from the main menu via *[ 3 ] Configure parameters* or from within the designer. Available settings include:

- **Color Theme**: Orange (default), Green, Cyan, or White against a dark background.
- **Font Size**: Small, Normal, or Large.
- **Show 3D Sphere Grid**: Can be turned off to view only tables and relationships.
- **Debug Mode**: Displays live camera coordinates in the status bar.
- **Language**: English, Català, Castellano. Changes apply immediately across all open forms; certain UI strings refresh upon restart.

Preferences are saved to `options.json` right next to the executable. This file is user-specific and **should not be pushed to the repository** (it is already excluded by `.gitignore`).

---

## 13. Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| **Ctrl+N** | New project |
| **Ctrl+O** | Open .hdb project |
| **Ctrl+S** | Save |
| **Ctrl+Shift+S** | Save As... |
| **Ctrl+Z** | Undo (up to 50 operations) |
| **Ctrl+Y** | Redo |
| **Ins** | New table |
| **Del** | Delete selected item (Field → Relationship → Table, depending on context) |
| **↑ / ↓** | Navigate the side list or the active table's fields |
| **Enter** | Confirm selection / open the editor for the selected field |
| **Escape** | Cancel dialog / return to the previous menu |

---

## 14. The .hdb Format and Auto-save

Projects are saved as **indented UTF-8 JSON**: tracking project name, SQL engine, timestamps, camera state (rotation, zoom, and panning), alongside the complete definition of tables (with all field attributes) and relationships. When you reopen a project, the camera viewport is restored exactly where you left it.

- **Auto-save** triggers every 5 minutes if the project has a file path assigned. It notifies you via the status bar without disrupting your workflow.
- The window title bar displays an **asterisk** (`*`) whenever there are unsaved changes. The application prompts for confirmation before closing or overwriting a modified project.

Being flat JSON, `.hdb` files are highly Git-friendly, meaning schema modifications can be reviewed cleanly using standard diffs.

---

## 15. Troubleshooting

**"SQL Server LocalDB must be installed"**
Features interacting with `.mdf` files depend on LocalDB. Download it from <https://aka.ms/sqllocaldb> (it is also included with SQL Server Express and Visual Studio).

**Cannot delete a table or a PK field**
This is the intended behavior of the ERM engine: a referenced PK cannot be deleted. Remove any incoming relationships first (select the relationship line on the sphere → DELETE, or press the Del key).

**Server connection times out (timeout)**
Verify your instance name and port. Ensure that the target SQL Server service is configured to accept remote connections (TCP/IP enabled) and check your firewall rules. Run the **⚡ TEST** button first.

**No tables detected when importing a .sql file**
The parser looks specifically for `CREATE TABLE` statements. If the script only contains `INSERT` queries or stored procedures, there is no structural schema to parse. MySQL/PostgreSQL dialects normalize automatically, but highly exotic syntax may require manual tweaking.

**The application launches, but the sphere is invisible**
Check under *Options* that "Show 3D Sphere Grid" is enabled, or trigger *View → [ \* ] View All* to re-center the viewport. A quick click on the **middle mouse button** will also reset your view.

---

*Holographic DB — Operation Manual*