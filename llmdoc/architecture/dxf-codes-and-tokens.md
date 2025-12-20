# Architecture of DXF Codes and Tokens System

## 1. Identity

- **What it is:** A system of enumeration and constants that define all valid DXF group codes, file tokens (section/entity names), subclass markers, and their associated data types.
- **Purpose:** To provide a unified, type-safe reference for DXF format parsing and generation, enabling correct interpretation of code-value pairs and structural elements throughout the I/O subsystem.

## 2. Core Components

- `src/ACadSharp/DxfCode.cs` (DxfCode): An enumeration containing all valid DXF codes (from -9999 to 481+), including special codes like Handle (5), XCoordinate (10), LayerName (8), etc.

- `src/ACadSharp/DxfFileToken.cs` (DxfFileToken): A static class defining string constants for all DXF file structural tokens—section names (HEADER, TABLES, BLOCKS, ENTITIES, OBJECTS), entity/object type names (CIRCLE, LINE, POLYLINE), and special markers (SECTION, ENDSEC, EOF).

- `src/ACadSharp/DxfSubclassMarker.cs` (DxfSubclassMarker): A static class containing all DXF subclass marker constants (e.g., "AcDbEntity", "AcDbCircle", "AcDbLine"). Each marker uniquely identifies a subclass within the DXF hierarchy.

- `src/ACadSharp/GroupCodeValue.cs` (GroupCodeValue): A utility class that maps DXF codes to their data types via the `TransformValue(int code)` method, which returns a `GroupCodeValueType` enum (String, Int16, Int32, Int64, Double, Handle, ObjectId, Bool, Byte, Chunk, Point3D, etc.).

- `src/ACadSharp/GroupCodeValueType.cs` (GroupCodeValueType): An enumeration defining all DXF value data types (String, Int16, Int32, Int64, Double, Handle, ObjectId, Bool, Byte, Chunk, Point3D, ExtendedDataString, ExtendedDataChunk, ExtendedDataHandle, ExtendedDataDouble, ExtendedDataInt16, ExtendedDataInt32, None, Comment).

## 3. Execution Flow (LLM Retrieval Map)

### 3.1 DXF Code Classification

**Code Range Semantics** (`src/ACadSharp/GroupCodeValue.cs:5-112`):

DXF codes are organized into ranges, each range mapping to a specific data type:

- **Codes 0–9:** Strings (entity type names, text, block names).
  - Code 0: Entity/object type (e.g., "CIRCLE", "LINE").
  - Codes 1–4, 6–9: General text strings (layer, linetype, style names).
  - Code 5: Handle (hexadecimal entity reference).

- **Codes 10–39:** 3D Points (XYZ coordinates).
  - Code 10, 20, 30: Primary point (Center, StartPoint, EndPoint).
  - Codes 11–19, 21–29, 31–39: Secondary/tertiary points and normals.
  - Three consecutive codes (X, Y, Z) represent a single XYZ value.

- **Codes 40–59:** Double-precision floating-point (radii, lengths, angles, scales).
  - Examples: Code 40 = Radius, Code 41 = Scale factor, Code 50 = Rotation angle.

- **Codes 60–79:** 16-bit signed integers (flags, counts, colors).
  - Examples: Code 60 = Invisible, Code 62 = Color number, Code 70 = Bit flags.

- **Codes 90–99:** 32-bit signed integers (larger counts and values).
  - Example: Code 90 = Count of items in collections.

- **Code 100:** Subclass marker (transition between inheritance layers).
  - Value: Subclass name string (e.g., "AcDbEntity", "AcDbCircle").

- **Codes 101–102:** Group markers and string values.

- **Code 105:** Alternative handle (used in certain object types, e.g., DimensionStyle).

- **Codes 110–139:** 3D Double-precision points (additional coordinate sets).

- **Codes 160–169:** 64-bit signed integers.

- **Codes 170–179, 270–279, 280–289:** Additional 16-bit integers.

- **Codes 290–299:** Boolean values (0 = False, 1 = True).

- **Codes 300–309:** Additional text strings.

- **Codes 310–319:** Binary data chunks (byte arrays).

- **Codes 320–329, 330–369, 390–399:** Handle and object ID references.

- **Codes 370–379, 380–389, 400–409:** Additional 16-bit integers.

- **Codes 410–419:** String values.

- **Codes 420–429, 440–449:** Color values (RGB).

- **Codes 430–439, 470–479:** String values.

- **Codes 450–459, 460–469:** Integer and double-precision values.

- **Code 999:** Comment (ASCII DXF only; ignored in binary).

- **Codes 1000–1071:** Extended data (XDATA) ranges with specialized semantics.

### 3.2 DXF File Structure via Tokens

**Section Organization** (`src/ACadSharp/DxfFileToken.cs`):

A DXF file is organized into named sections, each delimited by tokens:

```
SECTION
  HEADER     (file header variables)
ENDSEC
SECTION
  CLASSES    (class definitions)
ENDSEC
SECTION
  TABLES     (symbol tables)
ENDSEC
SECTION
  BLOCKS     (block definitions)
ENDSEC
SECTION
  ENTITIES   (model space entities)
ENDSEC
SECTION
  OBJECTS    (non-graphical objects, dictionaries)
ENDSEC
EOF
```

**Key Tokens:**
- `BeginSection` ("SECTION"), `EndSection` ("ENDSEC"): Section delimiters.
- `HeaderSection` ("HEADER"), `ClassesSection` ("CLASSES"), `TablesSection` ("TABLES"), etc.: Section identifiers.
- `EndOfFile` ("EOF"): File terminator.
- `EndSequence` ("SEQEND"): Sequence-end marker for polylines and other multi-entity groups.

**Special Markers:**
- `ReactorsToken` ("{ACAD_REACTORS"): Persistent reactor chain reference.
- `DictionaryToken` ("{ACAD_XDICTIONARY"): Extended dictionary reference.

### 3.3 Subclass Hierarchy via Markers

**Subclass Marker Semantics** (`src/ACadSharp/DxfSubclassMarker.cs`):

Each DXF subclass is identified by a marker string (group code 100). These markers correspond to C# class hierarchy levels:

- **Base Markers:**
  - "AcDbPlaceHolder": Placeholder for empty base classes.
  - "AcDbObject": Root marker for all objects (not graphics).
  - "AcDbEntity": Root marker for all graphical entities.

- **Entity-Specific Markers:**
  - "AcDbLine": Line entity subclass.
  - "AcDbCircle": Circle entity subclass.
  - "AcDbArc": Arc entity subclass.
  - "AcDb2LineAngularDimension", "AcDb3PointAngularDimension": Dimension subclasses.
  - "AcDbAlignedDimension", "AcDbRotatedDimension": Additional dimension types.
  - And 100+ more for other entity types (polylines, splines, hatches, etc.).

- **Table-Entry Markers:**
  - "AcDbRegAppTableRecord": Application ID table entry.
  - "AcDbLayerTableRecord": Layer table entry.
  - "AcDbLinetypeTableRecord": Linetype table entry.

- **Object-Specific Markers:**
  - "AcDbDictionary": Dictionary object.
  - "AcDbGroup": Group object.
  - "AcDbLayout": Layout object.
  - "AcDbMaterial": Material object.

**Reading with Subclass Markers** (`src/ACadSharp/IO/DXF/DxfStreamReader/*Reader.cs`):
- When code 100 is encountered, the reader transitions to the named subclass.
- Properties are looked up within the active subclass's `DxfProperties` dictionary.
- Multiple subclasses in sequence represent inheritance layers.

**Writing with Subclass Markers** (`src/ACadSharp/IO/DXF/DxfStreamWriter/*Writer.cs`):
- Each `DxfMap.SubClasses` entry is written as a marker (code 100, value = subclass name).
- Properties of that subclass follow immediately.

### 3.4 Type Conversion Pipeline

**Code → Type Lookup** (`src/ACadSharp/GroupCodeValue.cs`):

```
DxfCode (integer)
  ↓
GroupCodeValue.TransformValue(code)
  ↓
GroupCodeValueType (enum: String, Int16, Double, Handle, etc.)
  ↓
Value Parser/Converter (DxfStreamReader or CSUtilities.ValueConverter)
  ↓
C# Property Value
```

**Example: Code 10 (XCoordinate):**
1. Reader encounters code 10.
2. `GroupCodeValue.TransformValue(10)` returns `GroupCodeValueType.Point3D`.
3. Reader interprets the next value as the X coordinate of a Point3D.
4. Reader continues reading code 20 (Y) and code 30 (Z).
5. XYZ object is constructed and assigned to the property.

**Example: Code 62 (Color):**
1. Reader encounters code 62.
2. `GroupCodeValue.TransformValue(62)` returns `GroupCodeValueType.Int16`.
3. Reader parses the next value as a 16-bit integer (0–255 for standard colors, negative for RGB).
4. Integer is converted to a C# `Color` enum or object via `DxfProperty.SetValue()`.

### 3.5 Special Codes and Their Roles

**Critical Codes:**

- **Code 0 (Start/EntityType):** Identifies the entity or object class name (e.g., "CIRCLE", "LAYER").
- **Code 5 (Handle):** Unique hexadecimal identifier for every object in the drawing. Used for cross-references and persistence.
- **Code 100 (SubclassMarker):** Marks the beginning of a subclass data block within an entity/object.
- **Code 330 / 360 (ObjectId):** References to other objects by handle (soft/hard references).

**Extended Data (XDATA) Range (Codes 1000–1071):**
- Codes 1000–1003: Extended text strings.
- Code 1004: Extended binary chunk.
- Codes 1005–1009: Extended handles.
- Codes 1010–1059: Extended 3D doubles.
- Codes 1060–1070: Extended integers.
- Code 1071: Extended 32-bit integer.

## 4. Design Rationale

### 4.1 Code-Range Mapping

Rather than a flat enumeration of 500+ codes, the system groups codes by range and associates each range with a data type. This:
- Reduces memory footprint (range queries vs. full enumeration).
- Enables automatic type inference (code → type without lookup table).
- Simplifies DXF spec conformance (ranges align with official DXF documentation).

### 4.2 Subclass Markers as Inheritance Delimiters

Subclass markers (code 100) in DXF directly mirror C# class inheritance:
- Each C# class (decorated with `[DxfSubClass]`) corresponds to a subclass marker in DXF.
- This bidirectional mapping ensures that serialization/deserialization preserves class structure.
- Reading: Markers guide which properties to expect next.
- Writing: Inheritance order determines marker sequence.

### 4.3 Token Constants vs. Magic Strings

Using `DxfFileToken` constants instead of hardcoded strings:
- Enables compile-time checking (typos caught early).
- Centralizes DXF vocabulary in one place for maintainability.
- Simplifies refactoring across the I/O subsystem.

### 4.4 Extensibility

The enumeration-based design allows:
- **Future Code Support:** New codes can be added to `DxfCode` and ranges to `GroupCodeValue` as DXF spec evolves.
- **Custom Subclasses:** New entity types can define custom subclass markers (in `DxfSubclassMarker`) and inherit from existing base classes.
- **Type System Evolution:** Additional `GroupCodeValueType` entries support new data representations (if needed).

## 5. Code Reference Locations

### Code Enumeration

- `src/ACadSharp/DxfCode.cs` — Complete enum of DXF codes (-9999 to 481+).

### Type Mapping

- `src/ACadSharp/GroupCodeValue.cs:5-112` — Code range → type mapping logic.
- `src/ACadSharp/GroupCodeValueType.cs` — Enumeration of all DXF value types.

### File Tokens

- `src/ACadSharp/DxfFileToken.cs` — Section names, entity type tokens, special markers.

### Subclass Markers

- `src/ACadSharp/DxfSubclassMarker.cs` — 100+ subclass marker constants for all entity/object/table types.

### Usage in I/O

- `src/ACadSharp/IO/DXF/DxfStreamReader/*Reader.cs` — Code lookup and type conversion during reading.
- `src/ACadSharp/IO/DXF/DxfStreamWriter/*Writer.cs` — Code and token output during writing.

