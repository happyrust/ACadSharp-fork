# Guide: Understanding Table Entry Types

Deep-dive into each table entry type and its purpose in CAD documents.

## 1. Layer - Visual Organization and Control

**Purpose:** Layers organize drawing objects by function and control visibility, color, line type, and line weight for all entities on that layer.

**Key Properties:**
- `Name`: Unique identifier (case-insensitive)
- `Color`: RGB or indexed color (0-255), ByLayer default
- `LineType`: Reference to LineType table entry (e.g., "Continuous", "Dashed")
- `LineWeight`: Physical line thickness in millimeters (ByLayer, ByBlock, or fixed value)
- `Material`: Optional material reference for 3D rendering
- `IsOn`: Visibility toggle (true = visible, false = hidden)
- `PlotFlag`: Whether layer prints when document is printed

**Default Layers:**
- `"0"`: Always exists, cannot be deleted, objects default to this layer
- `"defpoints"`: Special layer for dimension definition points, automatically hidden

**Creating and Using:**
```csharp
var wallsLayer = new Layer("Walls") { Color = Color.Red };
var windowsLayer = new Layer("Windows") { LineType = document.LineTypes["Dashed"] };
document.Layers.Add(wallsLayer);
document.Layers.Add(windowsLayer);

// Assign entity to layer
line.Layer = document.Layers["Walls"];
```

## 2. LineType - Pattern Definition for Lines

**Purpose:** LineTypes define visual patterns for lines (solid, dashed, dotted, complex with shapes/text).

**Key Properties:**
- `Name`: Unique identifier (predefined: "Continuous", "ByBlock", "ByLayer")
- `Description`: Human-readable explanation
- `Segments`: Collection of Segment objects defining the pattern
- `PatternLength`: Total length of one repeating pattern cycle
- `HasShapes`: Boolean indicating if pattern includes embedded shapes or text
- `IsComplex`: Boolean for complex patterns (with shapes, text, or curves)

**Segment Types:**
- **Solid segment** (positive length): Drawn line portion
- **Gap segment** (zero or negative length): Invisible portion

**Built-in LineTypes:**
- `"Continuous"`: Single solid line (no gaps)
- `"ByLayer"`: Inherits from entity's layer
- `"ByBlock"`: Inherits from block's properties

**Creating Custom LineType:**
```csharp
var dashedLine = new LineType("MyDashed")
{
    Description = "Custom dashed pattern",
    PatternLength = 3.0
};
dashedLine.Segments.Add(new Segment(1.5));  // 1.5 unit dash
dashedLine.Segments.Add(new Segment(1.5, 0)); // 1.5 unit gap
document.LineTypes.Add(dashedLine);

// Use in layer
myLayer.LineType = document.LineTypes["MyDashed"];
```

## 3. TextStyle - Font and Text Formatting

**Purpose:** TextStyles define font files, sizing, width, obliquity, and mirroring for text entities.

**Key Properties:**
- `Name`: Unique identifier (default: "Standard")
- `FontFileName`: TrueType (.ttf) or SHX font file path (e.g., "arial.ttf", "txt.shx")
- `BigFontFileName`: Optional CJK (Chinese, Japanese, Korean) font file
- `TextHeight`: Default text height (0 = no default, set at entity level)
- `WidthFactor`: Width scaling ratio (0.5 = 50% width, 2.0 = double width)
- `ObliqueAngle`: Italic slant in radians
- `IsBackward`: Mirror horizontally
- `IsUpsideDown`: Mirror vertically
- `IsTrueType`: Boolean for TrueType vs. SHX font

**Usage:**
```csharp
var narrowFont = new TextStyle("Narrow")
{
    FontFileName = "arial.ttf",
    WidthFactor = 0.7  // 70% width
};
document.TextStyles.Add(narrowFont);

// Apply to text entity
textEntity.Style = document.TextStyles["Narrow"];
```

## 4. BlockRecord - Block Definition Metadata

**Purpose:** BlockRecords represent block definitions (blueprints for reusable geometry). Each BlockRecord references its Block entity, BlockEnd terminator, and entity collection.

**Key Properties:**
- `Name`: Block identifier (special: "*Model_Space", "*Paper_Space", "*A1", etc.)
- `Entities`: CadObjectCollection<Entity> containing all block entities
- `BlockEntity`: The Block marker entity (defines base point, flags)
- `BlockEnd`: The BlockEnd marker entity (block termination)
- `AttributeDefinitions`: IEnumerable of AttributeDefinition entities
- `CanScale`: Boolean for uniform vs. non-uniform scaling support
- `EvaluationGraph`: Dynamic block metadata (if dynamic)
- `Source`: Source block reference for dynamic blocks (via XData)

**Special Blocks:**
- `*Model_Space`: Always exists, primary drawing space
- `*Paper_Space`: Always exists, layout/print space
- `*A1`, `*A2`, etc.: Anonymous blocks (system-generated)

**Usage:**
```csharp
var doorBlock = new BlockRecord("Door");
var doorGeometry = new Line(XYZ.Zero, new XYZ(1, 0, 0));
doorBlock.Entities.Add(doorGeometry);
document.BlockRecords.Add(doorBlock);

// Check attributes
foreach (var attrDef in doorBlock.AttributeDefinitions)
{
    Console.WriteLine($"Attribute: {attrDef.Tag}");
}
```

## 5. AppId - Application Identifier

**Purpose:** AppIds register application names for attaching extended data (XData) to CAD objects without polluting the core structure.

**Key Properties:**
- `Name`: Application name (default: "ACAD" for AutoCAD)
- Built-in: "AcDbBlockRepBTag", "AcDbBlockRepETag" (dynamic blocks)

**Usage:**
```csharp
// Register custom app
var myAppId = new AppId("MyCustomApplication");
document.AppIds.Add(myAppId);

// Attach XData with this ID
var xdata = new ExtendedData();
xdata.AddControlStrings();
xdata.Records.Add(new ExtendedDataString("CustomMetadata"));
entity.ExtendedData.Add(myAppId, xdata);
```

## 6. UCS - User Coordinate System

**Purpose:** UCS defines alternative coordinate reference frames for user work.

**Key Properties:**
- `Name`: UCS identifier
- `Origin`: WCS coordinates of UCS origin (XYZ)
- `XAxis`: Unit direction vector for UCS X-axis
- `YAxis`: Unit direction vector for UCS Y-axis
- `OrthographicViewType`: Enumeration (Top, Bottom, Front, Back, Left, Right, IsometricTopLeft, etc.)
- `Elevation`: UCS elevation value

**Usage:**
```csharp
var customUcs = new UCS("MyUCS")
{
    Origin = new XYZ(10, 10, 0),
    XAxis = new XYZ(1, 0, 0),
    YAxis = new XYZ(0, 1, 0)
};
document.UCSs.Add(customUcs);
```

## 7. View - Named View Configuration

**Purpose:** Views save named viewport configurations for quick navigation.

**Key Properties:**
- `Name`: View identifier
- `ViewCenter`: 2D center point of view
- `ViewHeight`: Height of view window
- `ViewWidth`: Width of view window
- `LensLength`: Camera lens focal length (perspective)
- `FrontClippingPlane`: Front clipping distance
- `BackClippingPlane`: Back clipping distance
- `ViewRotation`: Rotation angle in radians
- `ViewMode`: Orthographic or perspective mode
- `UCS`: Associated UCS name (if any)
- `IsViewOrthographic`: Boolean for orthographic projection

**Usage:**
```csharp
var detailView = new View("DetailView")
{
    ViewCenter = new XY(50, 50),
    ViewHeight = 10,
    ViewWidth = 10
};
document.Views.Add(detailView);
```

## 8. VPort - Viewport Configuration

**Purpose:** VPorts define viewport settings for active drawing area (display parameters).

**Key Properties:**
- `Name`: Viewport identifier (default: "*Active")
- `LowerLeftCorner`: Lower-left corner XY coordinates
- `UpperRightCorner`: Upper-right corner XY coordinates
- `ViewCenter`: Center of viewport view
- `SnapBasePoint`: Snap grid base point
- `SnapSpacing`: Grid snap spacing (X, Y)
- `GridSpacing`: Grid display spacing
- `ViewportAspectRatio`: Width/height ratio
- `LineTypeScale`: Viewport-specific line type scaling

**Usage:**
```csharp
var activeVPort = document.VPorts["*Active"];
activeVPort.SnapSpacing = new XY(0.5, 0.5);  // 0.5 unit snap grid
activeVPort.GridSpacing = new XY(1.0, 1.0);   // 1.0 unit grid
```

## 9. DimensionStyle - Annotation Appearance Control

**Purpose:** DimensionStyles define all aspects of dimension appearance: lines, arrows, text, tolerances, units, prefixes/suffixes.

**Key Properties (40+ properties including):**
- `Name`: Style identifier (default: "Standard")
- `DimensionLineColor`: Color for dimension and extension lines
- `DimensionLineWeight`: Line weight for dimension lines
- `ExtensionLineColor`: Color for extension lines (perpendicular to dimension)
- `ExtensionLineWeight`: Line weight for extension lines
- `ArrowBlockName`: Block reference for arrow heads
- `ArrowSize`: Arrow size in drawing units
- `TextStyle`: TextStyle reference for dimension text
- `TextHeight`: Dimension text height
- `TextColor`: Text color
- `Tolerance`: Tolerance display type (None, Symmetric, Deviation)
- `ToleranceHeight`: Tolerance text height
- `Prefix`: String prepended to dimension value
- `Suffix`: String appended to dimension value
- `DecimalPlaces`: Number of decimal places for dimension values
- `UnitFormat`: Dimension unit format (decimal, scientific, engineering, etc.)

**Usage:**
```csharp
var customDimStyle = new DimensionStyle("CustomDim")
{
    TextHeight = 2.5,
    ArrowSize = 1.5,
    DecimalPlaces = 2,
    Prefix = "DIM: ",
    Suffix = " mm"
};
document.DimensionStyles.Add(customDimStyle);

// Apply to dimension
dimension.DimensionStyle = document.DimensionStyles["CustomDim"];
```

## 10. Quick Reference

| Entry Type | Default Entry | Primary Purpose | Key Reference |
|-----------|---------------|-----------------|----------------|
| Layer | "0" | Visual control, organization | Color, LineType, Visibility |
| LineType | "Continuous", "ByLayer", "ByBlock" | Line pattern definition | Segments, PatternLength |
| TextStyle | "Standard" | Font and text formatting | FontFileName, WidthFactor |
| BlockRecord | "*Model_Space", "*Paper_Space" | Block geometry container | Entities, AttributeDefinitions |
| AppId | "ACAD" | XData application identifier | ExtendedData storage |
| UCS | None | User coordinate system | Origin, XAxis, YAxis |
| View | None | Named viewport configuration | ViewCenter, ViewHeight |
| VPort | "*Active" | Active viewport settings | SnapSpacing, GridSpacing |
| DimensionStyle | "Standard" | Dimension appearance | TextHeight, ArrowSize, Format |
