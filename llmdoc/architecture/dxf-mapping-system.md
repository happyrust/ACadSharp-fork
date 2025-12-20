# Architecture of DXF Mapping System

## 1. Identity

- **What it is:** A metadata-driven architecture that dynamically generates mappings between C# object properties and DXF (Data eXchange Format) code values through attributes and reflection.
- **Purpose:** To provide a unified, declarative mechanism for serializing/deserializing CAD objects to and from DXF format without requiring manual mapping code for each entity type.

## 2. Core Components

The DXF mapping system consists of five primary architectural layers:

- `src/ACadSharp/DxfMap.cs` (DxfMap): The main factory class that creates and caches type-level DXF mappings. Orchestrates the creation of `SubClasses` dictionary and coordinates reflection-based property discovery.

- `src/ACadSharp/DxfMapBase.cs` (DxfMapBase): Abstract base class providing common mapping infrastructure. Contains the `DxfProperties` dictionary (code → property mapping) and `cadObjectMapDxf()` reflection utility.

- `src/ACadSharp/DxfClassMap.cs` (DxfClassMap): Represents a single DXF subclass mapping. Maps individual DXF codes to properties within a specific subclass context. Includes its own cache for frequently accessed subclass mappings.

- `src/ACadSharp/DxfProperty.cs` (DxfProperty): Encapsulates a single property-to-code binding. Contains the `PropertyInfo` reference, assigned DXF code(s), and value getter/setter operations. Supports multiple codes per property.

- `src/ACadSharp/DxfPropertyBase.cs` (DxfPropertyBase): Generic base for property-code bindings with type-safe value conversion. Implements `SetValue()` with support for complex types (XYZ, Color, Transparency, enums).

- `src/ACadSharp/Attributes/` (Attribute Classes): Declarative metadata system—`DxfNameAttribute` (class-level DXF name), `DxfSubClassAttribute` (subclass marker), `DxfCodeValueAttribute` (property-level code binding), `DxfCollectionCodeValueAttribute` (collection property codes).

## 3. Execution Flow (LLM Retrieval Map)

### 3.1 Mapping Creation Phase

**Step 1: Initiation** — `DxfMap.Create<T>()` or `DxfMap.Create(Type)` is called:
- Checks the static `ConcurrentDictionary<Type, DxfMap> _cache` for existing mapping.
- If found, returns cached instance; otherwise, proceeds to reflection-based creation.

**Step 2: Type Hierarchy Traversal** — `DxfMap.Create()` (`src/ACadSharp/DxfMap.cs:34-96`):
- Walks up the type inheritance chain from `T` to `CadObject`.
- For each type in the chain, retrieves the `DxfNameAttribute` (class name) and `DxfSubClassAttribute` (subclass marker).
- Handles special case: `DimensionStyle` replaces code 5 with code 105 (`src/ACadSharp/DxfMap.cs:91-96`).

**Step 3: Subclass Registration** — Within the hierarchy loop:
- If a type has `[DxfSubClass]`, creates a new `DxfClassMap` instance with the subclass name.
- Calls `addClassProperties()` to populate this subclass's `DxfProperties` dictionary.
- Stores the subclass in the parent `DxfMap.SubClasses` dictionary (keyed by subclass name).
- SubClasses are then reversed in order to match DXF inheritance order.

**Step 4: Property Reflection** — `DxfMapBase.cadObjectMapDxf()` (`src/ACadSharp/DxfMapBase.cs:28-48`):
- Uses reflection to enumerate public instance properties of the given type (declared in that type only, not inherited).
- For each property with `[DxfCodeValue(...)]` attribute, creates a `DxfProperty` instance.
- One property can map to multiple DXF codes (e.g., `Center [DxfCodeValue(10, 20, 30)]` for XYZ coordinates).
- Adds each code-property pair to the subclass's `DxfProperties` dictionary.

**Step 5: Caching** — Upon completion:
- Stores the constructed `DxfMap` in the static cache.
- Returns the map for immediate use.

### 3.2 Data Reading Phase (I/O Integration)

**Entry Point** — `DxfReader.readEntities()` or equivalent section reader encounters an entity or object definition in the DXF file.

**Code-to-Property Resolution** (`src/ACadSharp/IO/DXF/DxfStreamReader/*Reader.cs`):
- Reads a DXF code-value pair (e.g., code=10, value=5.0).
- Uses the cached `DxfMap` for the entity type to look up the property associated with code 10.
- Retrieves the corresponding `DxfProperty` from `DxfMap.DxfProperties[10]`.

**Value Assignment** — `DxfProperty.SetValue<T>()` (`src/ACadSharp/DxfPropertyBase.cs:57-80+`):
- Calls `SetValue(code, obj, value)` with the DXF code and raw value.
- The method uses `ValueConverter` (from CSUtilities) to convert the raw DXF value to the target C# type.
- Handles special cases:
  - **XYZ/XY vectors:** Three consecutive codes (10, 20, 30) → single `XYZ` property.
  - **Enums:** Numeric value → enum constant lookup.
  - **Colors, Margins, Transparency:** Custom conversion logic per type.
- Calls `PropertyInfo.SetValue(obj, convertedValue)` to update the object.

### 3.3 Data Writing Phase

**Entry Point** — `DxfWriter.writeEntities()` or equivalent section writer iterates over objects to serialize.

**Property Enumeration** (`src/ACadSharp/IO/DXF/DxfStreamWriter/*Writer.cs`):
- Retrieves the `DxfMap` for the entity type.
- Iterates over the subclasses in order (via `DxfMap.SubClasses`).
- For each subclass, writes the subclass marker (e.g., code=100, value="AcDbCircle").
- Enumerates all code-property pairs in the subclass's `DxfProperties` dictionary.

**Code-Value Pair Writing**:
- For each `DxfProperty`, calls `GetValue<T>()` to extract the current property value.
- Converts the C# value back to DXF representation (inverse of reading phase).
- Writes the DXF code-value pair to the output stream (binary or ASCII).

### 3.4 Subclass Handling

**Subclass Markers** (`DxfSubclassMarker` constants):
- Subclasses are delimited by group code 100 (subclass marker).
- Example: A CIRCLE entity has two subclasses: "AcDbEntity" (from Entity base class) and "AcDbCircle" (specific to Circle).
- During reading: Code 100 signals transition to the next subclass; properties are mapped to the active subclass context.
- During writing: Each subclass is written with its marker, followed by its properties in order.

**SubClasses Dictionary** (`DxfMap.SubClasses`):
- Ordered dictionary mapping subclass name → `DxfClassMap`.
- Order is reversed after construction to match DXF convention (base class first).
- Each subclass maintains its own `DxfProperties` dictionary for isolated property lookups.

### 3.5 Caching Strategy

**Two-Level Cache:**
- **Type-Level:** `DxfMap._cache` stores complete mappings per type (e.g., all subclasses of Circle).
- **Subclass-Level:** `DxfClassMap._cache` stores individual subclass mappings (useful for shared subclasses across types).

**Invalidation:**
- `DxfMap.ClearCache()` and `DxfClassMap.ClearCache()` manually clear the cache.
- Typically used during testing or when custom types are dynamically added.

**Thread Safety:**
- Uses `ConcurrentDictionary` to avoid race conditions in multi-threaded scenarios.

## 4. Design Rationale

### 4.1 Metadata-Driven Approach

The system prioritizes **declarative specification** over procedural code:
- Developers annotate properties with `[DxfCodeValue(10, 20, 30)]` once.
- The mapping system automatically generates the necessary read/write logic.
- Reduces boilerplate and minimizes the risk of manual mapping errors.

### 4.2 Reflection and Performance

- **First-Access Overhead:** Reflection is performed only on first use of a type (via `DxfMap.Create()`), which is cached.
- **Subsequent Accesses:** O(1) dictionary lookups via code → property mapping.
- **Trade-off:** Accepts initial startup cost in exchange for runtime efficiency and flexibility.

### 4.3 Multi-Code Property Support

Properties can map to multiple DXF codes:
- Example: `Center [DxfCodeValue(10, 20, 30)]` maps the X, Y, Z coordinates of an XYZ object.
- During reading: Codes 10, 20, 30 → all point to the same `Center` property; the reader accumulates values and constructs the XYZ.
- During writing: The single `Center` property is decomposed into three code-value pairs.

### 4.4 Subclass Hierarchy Alignment

The `SubClasses` dictionary order mirrors the DXF subclass hierarchy:
- Base class subclasses appear first (e.g., "AcDbObject", "AcDbEntity").
- Derived class subclasses appear later (e.g., "AcDbCircle").
- This order ensures correct serialization and conforms to DXF spec.

### 4.5 Value Conversion Abstraction

`DxfPropertyBase.SetValue()` abstracts value conversion:
- DXF values are generic objects (often numeric strings from ASCII or binary data).
- The method inspects the target property's type and applies appropriate conversion.
- Supports complex types (vectors, colors, enums) without cluttering the I/O layer.
- Leverages CSUtilities `ValueConverter` for standardized type conversions.

### 4.6 Separation of Concerns

- **Mapping Layer:** Handles reflection, caching, and code-property binding only.
- **I/O Layer:** Consumes mappings for reading/writing; uses standardized `GetValue()` and `SetValue()` interfaces.
- **Data Model Layer:** Entities and tables remain agnostic to serialization details; attributes are the only coupling point.

## 5. Code Reference Locations

### Core Mapping Files

- `src/ACadSharp/DxfMap.cs:34-96` — Type hierarchy traversal and subclass registration.
- `src/ACadSharp/DxfMapBase.cs:28-48` — Reflection-based property discovery and code mapping.
- `src/ACadSharp/DxfProperty.cs:37-60` — Property-code value getters/setters.
- `src/ACadSharp/DxfPropertyBase.cs:57-80+` — Generic value conversion and assignment.
- `src/ACadSharp/DxfClassMap.cs:53-86` — Subclass-level caching and initialization.

### Attribute Definitions

- `src/ACadSharp/Attributes/DxfCodeValueAttribute.cs` — Property-level code binding attribute.
- `src/ACadSharp/Attributes/DxfNameAttribute.cs` — Class-level DXF name attribute.
- `src/ACadSharp/Attributes/DxfSubClassAttribute.cs` — Subclass marker attribute.
- `src/ACadSharp/Attributes/DxfCollectionCodeValueAttribute.cs` — Collection property code binding.

### I/O Integration Points

- `src/ACadSharp/IO/DXF/DxfStreamReader/*Reader.cs` — Uses mappings during read phase.
- `src/ACadSharp/IO/DXF/DxfStreamWriter/*Writer.cs` — Uses mappings during write phase.

### Example Implementation

- `src/ACadSharp/Entities/Circle.cs` — Demonstrates attribute usage and property mapping (Center, Radius, etc.).

