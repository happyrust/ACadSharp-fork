# How to Understand DXF File Structure

A guide to comprehending DXF file organization and how it relates to the ACadSharp mapping system.

1. **Recognize DXF File Sections**

   A DXF file is organized into six main sections, delimited by "SECTION" and "ENDSEC" tokens. Each section contains specific types of data:

   - **HEADER:** Global drawing settings and system variables (ACADVER, EXTMIN, EXTMAX, etc.). Parsed by `CadHeader` class.
   - **CLASSES:** Class definitions for proxy objects and custom classes. Not required but present in most files.
   - **TABLES:** Symbol tables defining reusable resources (layers, linetypes, text styles, dimension styles, etc.). Each table contains multiple entries.
   - **BLOCKS:** Block definitions—reusable collections of entities. Each block has a name and contains entities within it.
   - **ENTITIES:** Model space entities (lines, circles, polylines, texts, dimensions, etc.). This is typically the largest section.
   - **OBJECTS:** Non-graphical objects like dictionaries, groups, layouts, materials, etc. Root dictionary anchors all named dictionaries.

   Refer to `src/ACadSharp/DxfFileToken.cs` for token constants (HEADER, TABLES, BLOCKS, etc.). These match the mapping in `DxfReader.Read()` (`src/ACadSharp/IO/DXF/DxfReader.cs`).

2. **Understand Group Codes**

   DXF stores all data as code-value pairs. The code (always an integer, group code) identifies the data's type; the value is the actual data.

   **Code Ranges and Types:**
   - Codes 0–9: Strings (entity type, names).
   - Codes 10–39: 3D points (coordinates; codes 10/20/30 = X/Y/Z of first point, codes 11/21/31 = second point, etc.).
   - Codes 40–59: Real numbers (radii, scales, angles).
   - Codes 60–79: 16-bit integers (flags, colors, visibility).
   - Codes 90–99: 32-bit integers (item counts, larger values).
   - Code 100: Subclass marker (string, marks inheritance layer).
   - Code 5: Handle (unique hexadecimal ID).

   Refer to `/llmdoc/architecture/dxf-codes-and-tokens.md` (Section 3.1) for complete mapping of codes to types. Use `GroupCodeValue.TransformValue(code)` to determine the type of any code programmatically.

3. **Interpret Subclass Markers (Code 100)**

   Subclass markers delimit inheritance layers within an entity or object. Each marker (code 100) introduces a new layer of properties.

   **Example: Circle entity structure in DXF:**
   ```
   0         "CIRCLE"           ← Entity type
   100       "AcDbEntity"       ← Base subclass (Entity properties)
   8         "0"                ← Layer (from AcDbEntity)
   62        256                ← Color (from AcDbEntity)
   100       "AcDbCircle"       ← Circle-specific subclass
   10        5.0                ← Center X
   20        3.0                ← Center Y
   30        0.0                ← Center Z
   40        2.5                ← Radius
   ```

   **Correspondence to C# Class Hierarchy:**
   ```csharp
   [DxfSubClass("AcDbEntity")]
   public class Entity { ... }     // Defines Layer, Color, etc.

   [DxfSubClass("AcDbCircle")]
   public class Circle : Entity    // Defines Center, Radius
   { ... }
   ```

   Subclass markers are defined in `src/ACadSharp/DxfSubclassMarker.cs` (100+ constants). When reading, markers signal which properties to expect; when writing, they structure the output according to inheritance.

4. **Recognize Entity and Object Type Names**

   Every entity and object in DXF has a type name (code 0):

   - **Entities:** CIRCLE, LINE, ARC, POLYLINE, TEXT, MTEXT, DIMENSION, HATCH, SPLINE, SOLID, FACE, etc.
   - **Table Entries:** LAYER, LTYPE (linetype), STYLE (text style), DIMSTYLE (dimension style), etc.
   - **Objects:** DICTIONARY, GROUP, LAYOUT, MATERIAL, VISUALSTYLE, etc.

   These names are constants in `DxfFileToken` class (e.g., `DxfFileToken.EntityCircle`, `DxfFileToken.TableLayer`). The `[DxfName]` attribute on each class specifies the DXF type name, enabling bidirectional mapping.

5. **Map Handles and Object References**

   - **Handle (Code 5):** Unique hexadecimal identifier for every object (e.g., "1A", "2F3", "100"). Used to create persistent references.
   - **Object References (Codes 330, 360, etc.):** When an entity references another object, it stores the referenced object's handle. Examples:
     - Entity's Layer reference: stores the Layer table entry's handle.
     - Dimension's style reference: stores the DimensionStyle table entry's handle.

   During reading, the DWG/DXF reader maintains a handle-to-object map. Reference codes are resolved post-read to link objects together. DXF readers use this mechanism via the `CadDocumentBuilder` (for DXF) and `DwgDocumentBuilder` (for DWG).

6. **Understand Point Representation**

   Coordinates in DXF are represented across three sequential codes:

   ```
   10 = X coordinate
   20 = Y coordinate
   30 = Z coordinate
   ```

   For secondary/tertiary points:
   ```
   11 = X (second point)
   21 = Y (second point)
   31 = Z (second point)
   ```

   In C#, these are abstracted into a single `XYZ` property with `[DxfCodeValue(10, 20, 30)]`. The value converter reconstructs the `XYZ` object from the three codes. Refer to `DxfPropertyBase.SetValue()` for the reconstruction logic.

7. **Recognize Repetitive Structures**

   Some DXF structures repeat (e.g., vertices in a polyline, segment endpoints in a multi-segment line):

   ```
   0    "LWPOLYLINE"
   10   5.0     ← Vertex 1 X
   20   3.0     ← Vertex 1 Y
   10   6.0     ← Vertex 2 X
   20   4.0     ← Vertex 2 Y
   10   7.0     ← Vertex 3 X
   20   5.0     ← Vertex 3 Y
   ```

   The reader accumulates all instances of codes 10/20 into a list and assigns it to the `Vertices` property. Use `[DxfCollectionCodeValue]` to mark collection properties (though not always required; context determines iteration).

8. **Understand DXF Conventions**

   **Layering and References:**
   - All entities must be on a layer (code 8, stores layer name or handle).
   - Layers are defined in the LAYER table within TABLES section.
   - Entity's layer reference is resolved by name or handle lookup.

   **Linetype and Text Style:**
   - Entities can reference linetypes (code 6, LTYPE table entry).
   - Text entities reference text styles (code 7, STYLE table entry).

   **Handles in BLOCKS and OBJECTS:**
   - Block definitions have handles and names.
   - Entities in a block reference the block's handle (code 330).
   - The reader constructs a hierarchical model: Document → BlockRecords (table) → Blocks (dictionary) → Entities within each block.

9. **Relate DXF Structure to ACadSharp Class Hierarchy**

   | DXF Element | ACadSharp Class | Mapping |
   |---|---|---|
   | SECTION HEADER | `CadHeader` | System variables mapped via `DxfCodeValue` attributes |
   | SECTION TABLES | `Table<T>` subclasses (LayersTable, LineTypesTable, etc.) | Table entries with `[DxfName]` and `[DxfSubClass]` |
   | SECTION BLOCKS | `Block`, `BlockRecord` | Entities within blocks reference block's handle |
   | SECTION ENTITIES | All Entity subclasses | `[DxfName]` defines type, `[DxfSubClass]` for inheritance layers |
   | SECTION OBJECTS | `CadDictionary`, `Group`, `Layout`, etc. | Non-graphical objects; root dictionary anchors all |

10. **Navigate ACadSharp I/O Code**

    - **Reading:** `src/ACadSharp/IO/DXF/DxfReader.cs` orchestrates section reading. Each section (HEADER, TABLES, BLOCKS, ENTITIES, OBJECTS) has a dedicated reader (e.g., `DxfTablesSectionReader`, `DxfEntitiesSectionReader`). These use `DxfMap` to resolve codes to properties.
    - **Writing:** `src/ACadSharp/IO/DXF/DxfWriter.cs` orchestrates section writing. Each section writer (e.g., `DxfTablesSectionWriter`, `DxfEntitiesSectionWriter`) iterates over objects and outputs code-value pairs via `DxfMap`.
    - **Mapping:** `src/ACadSharp/DxfMap.cs` creates the bidirectional code ↔ property mapping.

    Refer to `/llmdoc/architecture/dxf-mapping-system.md` (Section 3) for detailed execution flows during read and write phases.

