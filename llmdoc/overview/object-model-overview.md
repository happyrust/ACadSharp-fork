# CAD Object Model Overview

## 1. Identity

**ACadSharp's CAD Object Model** is a comprehensive, type-safe system for representing and manipulating all CAD objects (geometrical, structural, and metadata) as a unified object hierarchy rooted in CadObject.

**Purpose**: Provides a flexible, extensible framework for programmatic access to CAD data structures, supporting DXF/DWG file reading/writing while maintaining AutoCAD semantic compatibility.

## 2. High-Level Description

The CAD Object Model organizes thousands of potential CAD objects into a three-branch hierarchy:

### Core Hierarchy
```
CadObject (abstract root)
├── Entity (graphical objects: 144+ types)
│   ├── Basic Geometry: Line, Circle, Arc, Ellipse, Point, Ray, XLine, Face3D
│   ├── Polylines: LwPolyline, Polyline2D, Polyline3D (vertices as Vertex objects)
│   ├── Text & Annotations: TextEntity, MText, Dimension (7 subtypes)
│   ├── Blocks & References: Insert, Block, BlockEnd
│   └── Advanced: Spline, Hatch, Mesh, Solid, Viewport, MultiLeader, Image
│
├── NonGraphicalObject (metadata objects)
│   ├── CadDictionary (key-value store)
│   ├── Group (entity collection)
│   ├── Layout (drawing layout)
│   ├── ImageDefinition, Material, VisualStyle, BookColor
│   └── Special evaluations for dynamic blocks
│
└── TableEntry (system resources)
    ├── Layer (visibility, color, linetype control)
    ├── LineType (line pattern definition)
    ├── TextStyle (font and text properties)
    ├── BlockRecord (block metadata and entities)
    ├── DimensionStyle (dimension appearance)
    ├── AppId, UCS, View, VPort
    └── Managed in 9 system tables within CadDocument
```

### CadDocument: The Central Hub

CadDocument is the document container that:

- **Manages 9 core tables**: Layers, LineTypes, TextStyles, BlockRecords, DimensionStyles, AppIds, UCSs, Views, VPorts
- **Maintains optional collections**: Colors, Groups, Layouts, Materials, MLineStyles, MLeaderStyles, Scales
- **Stores CAD header**: Version, units, scale, insbase, and 100+ system variables
- **Indexes all objects**: By Handle (unique 64-bit ID) for fast lookup
- **Provides model/paper space**: Predefined blocks for entities
- **Manages serialization**: Integrates with DXF/DWG readers and writers

### Key Design Principles

1. **Unified Object Identity**: Every CadObject has a Handle (unique ID), Document reference, and Owner (container).
2. **Strict Single Ownership**: Each object belongs to exactly one owner; moving to a new collection automatically removes from the old one.
3. **Property Inheritance**: Entities inherit visual properties (Color, LineType, LineWeight) from their Layer, with ByLayer/ByBlock resolution.
4. **Type-Safe Collections**: Generic collections (CadObjectCollection<T>, Table<T>) with type constraints and event notifications.
5. **Metadata-Driven Serialization**: DxfMap uses reflection and attributes to auto-generate DXF code mappings for all object types.
6. **Extensibility**: XDictionary and ExtendedData allow arbitrary data attachment to any object.

### Object Lifecycle Example

```
1. Create: var line = new Line() [no Document, no Handle]
2. Set properties: line.StartPoint, line.EndPoint, line.Color, etc.
3. Add to collection: document.Entities.Add(line)
   → Triggers AssignDocument()
   → Allocates Handle
   → Sets Document and Owner references
4. Modify: Adjust properties, apply transformations
5. Save: DwgWriter.Write() or DxfWriter.Write()
   → Uses DxfMap to serialize to DXF codes
6. Remove: document.Entities.Remove(line)
   → Triggers UnassignDocument()
   → Handle released, references cleared
```

### Property System Architecture

```
Entity Visual Properties
├── Color: ByLayer | ByBlock | RGB | ACI (index)
│   └─ GetActiveColor() resolves ByLayer → queries Layer.Color
├── Layer: Reference to Document.Layers[name]
│   └─ Controls visibility, default color, linetype, lineweight
├── LineType: Reference to Document.LineTypes[name]
│   └─ Defines line pattern (Continuous, Dashed, etc.)
├── LineWeight: ByLayer | ByBlock | W013-W200 (mm)
├── Transparency: 0-255 (0=transparent, 255=opaque)
├── Material: Reference to Document.Materials[name]
└── LineTypeScale: Scaling factor for line pattern
```

### Entity Type Organization

**144+ entity types** organized by functional category:

| Category | Count | Examples | Purpose |
|----------|-------|----------|---------|
| Basic Geometry | 8 | Line, Circle, Arc, Ellipse | Fundamental building blocks |
| Polylines | 3 | LwPolyline, Polyline2D, Vertex | Complex paths and chains |
| Text/Annotations | 11 | TextEntity, MText, Dimension | Labeling and dimensioning |
| Blocks/References | 4 | Insert, Block, BlockEnd | Reusable components |
| Advanced | 8+ | Spline, Hatch, Mesh, Viewport | Specialized geometry |

### Table and Collection System

```
CadDocument
├── 9 Mandatory Tables (inheriting from Table<T>)
│   ├── LayersTable: Layer objects with color/linetype/visibility
│   ├── LineTypesTable: LineType objects with segment definitions
│   ├── TextStylesTable: TextStyle objects with font info
│   ├── BlockRecordsTable: BlockRecord objects (block metadata)
│   ├── DimensionStylesTable: DimensionStyle objects (40+ properties)
│   ├── AppIdsTable: AppId objects (for XData registration)
│   ├── UCSTable: Coordinate systems
│   ├── ViewsTable: Named views
│   └── VPortsTable: Viewport configurations
│
└── Optional Collections (in RootDictionary)
    ├── Colors: BookColor objects (named colors)
    ├── Groups: Group objects (entity collections)
    ├── Layouts: Layout objects (page setups)
    ├── Materials: Material objects (surface properties)
    └── MLineStyles, MLeaderStyles, Scales
```

### Extension Mechanisms

1. **ExtendedData (XData)**: Attach arbitrary data to any object, organized by AppId
   - 15 data types: String, Integer16/32, Real, Coordinate, WorldCoord, Displacement, Direction, Distance, Scale, Handle, BinaryChunk, Layer, Reference
   - Supports multi-application tagging

2. **XDictionary**: Store named non-graphical objects within any CadObject
   - Used for block sorting tables, dynamic block properties, custom data

3. **Reactors**: Track dependencies between objects
   - CleanReactors() manages cross-document references

### Integration Points

- **DXF Serialization**: DxfMap maps properties to DXF group codes; DxfReader/DxfWriter use this for I/O
- **DWG Serialization**: DwgReader/DwgWriter handle binary format with version-specific headers
- **Document Building**: DxfDocumentBuilder, DwgDocumentBuilder assemble parsed data into CadDocument
- **Event System**: Collectionchanged, OnNameChanged events for reactivity

## 3. Architectural Strengths

1. **Semantic Consistency**: Follows AutoCAD object model naming and relationships
2. **Type Safety**: Generic constraints prevent mixing incompatible objects
3. **Performance**: O(1) handle-based lookup; efficient collection management
4. **Extensibility**: XData, XDictionary, and Reactors support custom data
5. **Format Agnostic**: Single object model supports DXF, DWG, and potential other formats
6. **Multi-Version Support**: Header and version-specific readers handle 8 AutoCAD versions (AC1009-AC1032)

---

**Last Updated:** 2025-12-14
**Scope:** High-level CAD object model overview, including hierarchy, lifecycle, properties, tables, collections, and extension mechanisms
