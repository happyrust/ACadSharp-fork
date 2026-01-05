# ACadSharp Tables System

## OVERVIEW
Core system for managing CAD system resources (Layers, Styles, Blocks, etc.) through protected, name-indexed table collections.

## STRUCTURE
- **Base Hierarchy**:
    - `TableEntry`: Abstract base for all table items, enforcing name constraints and standard flags.
    - `Table<T>`: Abstract base for collections, managing indexing, events, and system protection.
- **Table Registry (The 9 Core Tables)**:
    - `AppIdsTable`: Application identifiers for XData.
    - `BlockRecordsTable`: Metadata and entity containers (including `*Model_Space` and `*Paper_Space`).
    - `DimensionStylesTable`: Complex styling for dimensions with 40+ system variables.
    - `LayersTable`: Visibility and visual property control (Default: "0").
    - `LineTypesTable`: Pattern definitions (Defaults: "ByLayer", "ByBlock", "Continuous").
    - `TextStylesTable`: Font and text formatting (Default: "Standard").
    - `UCSTable`: User Coordinate Systems.
    - `ViewsTable`: Named camera views.
    - `VPortsTable`: Viewport configuration sets.
- **Key Files**:
    - `TableEntry.cs`: Item lifecycle, name change notifications, and standard flags.
    - `Collections/Table.cs`: Protected dictionary management and default entry logic.

## WHERE TO LOOK
- **Base Logic**: `src/ACadSharp/Tables/TableEntry.cs`, `src/ACadSharp/Tables/Collections/Table.cs`.
- **Collection Definitions**: `src/ACadSharp/Tables/Collections/*.cs`.
- **Item Definitions**: `src/ACadSharp/Tables/*.cs`.
- **Standard Flags**: `src/ACadSharp/Tables/StandardFlags.cs`.

## CONVENTIONS
- **Unique Naming**: All entries must have a unique, non-empty name. Tables use case-insensitive lookups via `StringComparer.OrdinalIgnoreCase`.
- **Default Entries**: Tables automatically populate mandatory entries via `CreateDefaultEntries()`. These are defined in the `defaultEntries` property of each specific table.
- **Name Syncing**: Tables subscribe to `OnNameChanged` events from entries. When a name changes, the table automatically updates its internal dictionary key.
- **Metadata Mapping**: Uses `[DxfSubClass(DxfSubclassMarker.TableRecord)]` for item serialization and `[DxfSubClass(DxfSubclassMarker.Table)]` for collections.
- **Standard Flags (Code 70)**: Most table entries use bitwise flags (StandardFlags) to indicate if an entry is "Purgeable", "Externally Dependent", or "Referenced".

## ANTI-PATTERNS
- **Deleting Reserved Entries**: `Remove()` operations on entries in `defaultEntries` (e.g., Layer "0") are blocked and return null.
- **Renaming Protected Items**: Renaming a default entry (e.g., "0" to "Base") throws `ArgumentException` to prevent breaking CAD semantics.
- **Manual Handle Assignment**: Handles are assigned by `CadDocument` upon addition; manual overrides can corrupt the object index.
- **Orphaned Entries**: Creating a `TableEntry` without adding it to a `Table<T>` prevents it from being serialized or correctly referenced by entities.
- **Duplicate Names**: Adding an entry with a conflicting name (case-insensitive) throws `ArgumentException` from the internal dictionary.
- **Empty Names**: Passing null or empty names to a `TableEntry` constructor or setter throws `ArgumentNullException`.
- **Cross-Document References**: Entities and reactors must belong to the same `CadDocument` as the table entries they reference.
