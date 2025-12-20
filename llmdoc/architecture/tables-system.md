# Architecture of Tables System

## 1. Identity

- **What it is:** A unified, generic table management system for CAD document metadata (layers, line types, text styles, blocks, etc.).
- **Purpose:** Provides type-safe, case-insensitive lookup and management of named resources required to be globally unique in a CAD document.

## 2. Core Components

- `src/ACadSharp/Tables/Collections/Table.cs` (Table<T>): Abstract generic base class for all tables, implements ICadCollection<T> and IObservableCadCollection<T>, manages entries via Dictionary with case-insensitive keys.
- `src/ACadSharp/Tables/TableEntry.cs` (TableEntry): Abstract base class for all table entries, inherits from CadObject, implements INamedCadObject with Name and Flags properties.
- `src/ACadSharp/Tables/Layer.cs` (Layer): Table entry for layers, includes color, line type, line weight, material, print flags.
- `src/ACadSharp/Tables/LineType.cs` (LineType): Table entry for line types, includes segments collection, pattern length, complexity flags.
- `src/ACadSharp/Tables/TextStyle.cs` (TextStyle): Table entry for text styles, includes font files, text height, width factor, oblique angle.
- `src/ACadSharp/Tables/BlockRecord.cs` (BlockRecord): Table entry for block definitions, includes associated Block/BlockEnd entities, Entities collection, attribute definitions.
- `src/ACadSharp/Tables/AppId.cs` (AppId): Table entry for application IDs, used for XData association and tracking.
- `src/ACadSharp/Tables/UCS.cs` (UCS): Table entry for user coordinate systems, includes origin and axis vectors.
- `src/ACadSharp/Tables/View.cs` (View): Table entry for named views, includes center, width, height, clipping planes.
- `src/ACadSharp/Tables/VPort.cs` (VPort): Table entry for viewports, includes coordinates, snap/grid settings.
- `src/ACadSharp/Tables/DimensionStyle.cs` (DimensionStyle): Table entry for dimension styles, includes 40+ properties for controlling dimension appearance.
- `src/ACadSharp/Tables/Collections/LayersTable.cs` (LayersTable): Concrete implementation for layers table.
- `src/ACadSharp/Tables/Collections/BlockRecordsTable.cs` (BlockRecordsTable): Concrete implementation for block records table, handles anonymous block naming.

## 3. Execution Flow (LLM Retrieval Map)

### Adding a Table Entry

1. **Creation:** New table entry created (e.g., `new Layer("MyLayer")`).
2. **Validation:** `Add(T item)` validates name, generates default name if empty via `createName()` - `src/ACadSharp/Tables/Collections/Table.cs:43-55`.
3. **Storage:** Entry stored in internal Dictionary with case-insensitive key - `src/ACadSharp/Tables/Collections/Table.cs:32`.
4. **Owner Assignment:** Item's Owner is automatically set to the table/document - `src/ACadSharp/Tables/Collections/Table.cs:39-41`.
5. **Event Trigger:** `OnAdd` event fired with CollectionChangedEventArgs - `src/ACadSharp/Tables/Collections/Table.cs:14`.
6. **Handle Assignment:** Document assigns unique Handle during collection registration - `src/ACadSharp/CadDocument.cs` (RegisterCollection method).

### Querying Table Entries

1. **Name-based Lookup:** `TryGetValue(string key, out T item)` - `src/ACadSharp/Tables/Collections/Table.cs` (inherited implementation).
2. **Case-insensitive Match:** Dictionary uses StringComparer.OrdinalIgnoreCase - `src/ACadSharp/Tables/Collections/Table.cs:32`.
3. **Existence Check:** `Contains(string key)` returns bool.
4. **Iteration:** `GetEnumerator()` via IEnumerable<T> implementation.

### Name Change Handling

1. **Entry Rename:** When TableEntry.Name changes, OnNameChanged event fires - `src/ACadSharp/Tables/TableEntry.cs`.
2. **Dictionary Update:** Table listens to OnNameChanged and updates entry key - `src/ACadSharp/Tables/Collections/Table.cs`.
3. **Consistency:** Ensures single entry per unique name.

### Removing Table Entries

1. **Removal Request:** `Remove(string key)` called - `src/ACadSharp/Tables/Collections/Table.cs`.
2. **Default Protection:** System prevents removal of default entries (e.g., Layer "0", BlockRecord "*Model_Space") - `src/ACadSharp/Tables/Collections/Table.cs`.
3. **Event Trigger:** `OnRemove` event fired.
4. **Cleanup:** Associated references cleaned (e.g., Layer references to LineType verified for validity).

### Default Entry Management

1. **Initialization:** `CreateDefaultEntries()` called during table construction.
2. **Default Specs:**
   - LayersTable: ["0"]
   - LineTypesTable: ["Continuous", "ByBlock", "ByLayer"]
   - TextStylesTable: ["Standard"]
   - BlockRecordsTable: ["*Model_Space", "*Paper_Space"]
   - AppIdsTable: ["ACAD"]
   - VPortsTable: ["*Active"]
   - DimensionStylesTable: ["Standard"]
   - Others: No required defaults
3. **Protection:** Removal of default entries throws exception or returns null.

## 4. Design Rationale

**Unified Interface:** All 9 table types (Layers, LineTypes, TextStyles, BlockRecords, AppIds, UCSs, Views, VPorts, DimensionStyles) inherit from `Table<T>`, ensuring consistent collection semantics across the document.

**Case-insensitive Naming:** CAD systems traditionally treat layer and style names case-insensitively, so Dictionary uses StringComparer.OrdinalIgnoreCase to match this behavior.

**Event-driven Architecture:** OnAdd/OnRemove events allow other subsystems (e.g., Layer.LineType reference tracking) to respond to collection changes without tight coupling.

**Default Entry Protection:** Some entries (Layer "0", ModelSpace, PaperSpace) are fundamental to CAD structure and cannot be removed. Table enforces this invariant.

**Generic Type Safety:** `Table<T>` where T:TableEntry provides compile-time type safety while sharing implementation across all table types.

**Handle Assignment at Document Level:** Ensures all table entries receive unique, persistent Handles for DXF serialization. This happens via `document.RegisterCollection(this)` in the Table constructor - `src/ACadSharp/Tables/Collections/Table.cs:37-41`.
