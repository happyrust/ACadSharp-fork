# How to Work with Entities

A practical guide for creating, querying, and modifying CAD entities in ACadSharp.

## Step 1: Create and Add a Simple Entity

Create a Line entity and add it to the document's model space:

```csharp
// Create a line
var line = new Line
{
    StartPoint = new XYZ(0, 0, 0),
    EndPoint = new XYZ(10, 10, 0)
};

// Add to document (automatically assigns Handle and Document reference)
document.Entities.Add(line);

// Verify: line.Document != null and line.Handle > 0
Assert.NotNull(line.Document);
Assert.True(line.Handle > 0);
```

Reference: `src/ACadSharp/Entities/Line.cs`, `src/ACadSharp/CadDocument.cs:Entities`

## Step 2: Set Visual Properties (Color, Layer, LineType)

Control the appearance of entities using standard visual properties:

```csharp
var circle = new Circle
{
    Center = new XYZ(5, 5, 0),
    Radius = 3
};

// Set color (by index, 1=red)
circle.Color = Color.FromIndex(1);

// Set layer (reference from document's layer table)
if (document.Layers.TryGetValue("MyLayer", out var layer))
{
    circle.Layer = layer;
}

// Set linetype (reference from document's linetype table)
if (document.LineTypes.TryGetValue("Dashed", out var linetype))
{
    circle.LineType = linetype;
}

// Set lineweight
circle.LineWeight = LineWeightType.W013;  // 0.13mm

// Set transparency (0=transparent, 255=opaque)
circle.Transparency = new Transparency(200);

document.Entities.Add(circle);
```

Reference: `src/ACadSharp/Entities/Entity.cs:Color,Layer,LineType`, `src/ACadSharp/Color.cs`

## Step 3: Resolve ByLayer/ByBlock Properties

Use GetActive* methods to automatically resolve inherited properties:

```csharp
// Assume entity has ByLayer color (default)
Assert.Equal(Color.ByLayer, entity.Color);

// Get the actual color from the layer
var actualColor = entity.GetActiveColor();
// Returns the layer's color, or RGB/ACI if entity overrides

// Similar for other properties
var actualLineType = entity.GetActiveLineType();
var actualLineWeight = entity.GetActiveLineWeightType();
```

Reference: `src/ACadSharp/Entities/Entity.cs:GetActiveColor(),GetActiveLineType()`

## Step 4: Work with Polylines

Add vertices to lightweight polylines (recommended):

```csharp
var polyline = new LwPolyline
{
    Elevation = 0,
    ConstantWidth = 0
};

// Add vertices
polyline.Vertices.Add(new Vertex(0, 0));
polyline.Vertices.Add(new Vertex(10, 0));
polyline.Vertices.Add(new Vertex(10, 10));
polyline.Vertices.Add(new Vertex(0, 10));
polyline.Vertices.Add(new Vertex(0, 0));  // close

// Optional: set close flag
polyline.IsClosed = true;

document.Entities.Add(polyline);
// Note: A Seqend entity is automatically added when the collection becomes non-empty
```

Reference: `src/ACadSharp/Entities/LwPolyline.cs:Vertices`, `src/ACadSharp/SeqendCollection.cs`

## Step 5: Use Block Inserts with Attributes

Create a block reference and populate its attributes:

```csharp
// Assume block "MyBlock" exists in document
if (!document.BlockRecords.TryGetValue("MyBlock", out var blockDef))
{
    blockDef = new BlockRecord { Name = "MyBlock" };
    document.BlockRecords.Add(blockDef);
}

// Create insert
var insert = new Insert
{
    Block = blockDef,
    InsertPoint = new XYZ(10, 10, 0),
    XScale = 1.0,
    YScale = 1.0,
    ZScale = 1.0
};

// Add attributes (if block has attribute definitions)
if (blockDef.HasAttributes)
{
    foreach (var attrDef in blockDef.AttributeDefinitions)
    {
        var attribute = new AttributeEntity
        {
            Tag = attrDef.Tag,
            TextString = "Value"
        };
        insert.Attributes.Add(attribute);
    }
}

document.Entities.Add(insert);
```

Reference: `src/ACadSharp/Entities/Insert.cs:Block,Attributes`, `src/ACadSharp/Tables/BlockRecord.cs`

## Step 6: Query and Iterate Entities

Different ways to find and process entities:

```csharp
// Iterate all entities in model space
foreach (var entity in document.Entities)
{
    Console.WriteLine($"Entity: {entity.ObjectName}, Handle: {entity.Handle}");
}

// Get entity by handle
if (document.TryGetCadObject<Entity>(myHandle, out var entity))
{
    Console.WriteLine($"Found: {entity.ObjectName}");
}

// Filter by type
var lines = document.Entities.OfType<Line>();
var circles = document.Entities.OfType<Circle>();

// Filter by property
var redEntities = document.Entities.Where(e => e.Color.Index == 1);

// Get all entities in a specific block
var blockRecord = document.BlockRecords["MyBlock"];
foreach (var entity in blockRecord.Entities)
{
    // Process block entities
}
```

Reference: `src/ACadSharp/CadDocument.cs:Entities,GetCadObject()`, `src/ACadSharp/Tables/BlockRecord.cs:Entities`

## Step 7: Apply Transformations

Modify entity position, rotation, and scale:

```csharp
var line = new Line
{
    StartPoint = new XYZ(0, 0, 0),
    EndPoint = new XYZ(10, 0, 0)
};

// Translate
line.ApplyTranslation(new XYZ(5, 5, 0));  // Moves by offset

// Rotate
var rotationCenter = new XYZ(0, 0, 0);
line.ApplyRotation(rotationCenter, 45 * Math.PI / 180);  // 45 degrees in radians

// Scale
var scaleCenter = new XYZ(0, 0, 0);
line.ApplyScaling(scaleCenter, 2.0);  // 2x scale

// Custom matrix transform
var matrix = new Matrix3(/* transformation matrix */);
line.ApplyTransform(matrix);
```

Reference: `src/ACadSharp/Entities/Entity.cs:ApplyTransform(),ApplyTranslation(),ApplyRotation(),ApplyScaling()`

## Step 8: Clone and Match Properties

Duplicate entities or copy properties:

```csharp
// Clone an entity (deep copy)
var originalLine = new Line { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(10, 10, 0) };
var clonedLine = (Line)originalLine.Clone();
// clonedLine has same geometry but no Handle or Document reference

// Copy properties from another entity
var sourceEntity = new Line { Color = Color.FromIndex(5), Layer = layer };
var targetEntity = new Circle();
targetEntity.MatchProperties(sourceEntity);
// Now targetEntity has Color=5 and same Layer as sourceEntity
```

Reference: `src/ACadSharp/CadObject.cs:Clone()`, `src/ACadSharp/Entities/Entity.cs:MatchProperties()`

## Step 9: Work with Dimension Entities

Create dimension annotations:

```csharp
// Linear dimension
var dimension = new DimensionLinear
{
    DefinitionPoint = new XYZ(0, 0, 0),      // First point
    SecondDefinitionPoint = new XYZ(10, 0, 0),  // Second point
    InsertionPoint = new XYZ(5, 2, 0),       // Text placement
    Style = document.DimensionStyles["Standard"]
};

// Optional: customize appearance
dimension.Color = Color.FromIndex(3);
dimension.AttachmentPoint = DimensionTextAttachmentPoint.MiddleCenter;
dimension.TextHeight = 2.5;

document.Entities.Add(dimension);
// Note: A dimension block is automatically created in the document's block table
```

Reference: `src/ACadSharp/Entities/Dimension.cs`, `src/ACadSharp/Entities/DimensionLinear.cs`

## Step 10: Handle Collections in Blocks

Add and manage entities within block definitions:

```csharp
// Create a block record
var blockRecord = new BlockRecord { Name = "MyComponentBlock" };
document.BlockRecords.Add(blockRecord);

// Add entities to the block
var blockLine = new Line
{
    StartPoint = new XYZ(0, 0, 0),
    EndPoint = new XYZ(5, 0, 0)
};
blockRecord.Entities.Add(blockLine);  // Adds to block, not model space

var blockCircle = new Circle
{
    Center = new XYZ(5, 0, 0),
    Radius = 1
};
blockRecord.Entities.Add(blockCircle);

// Now insert the block in model space
var insert = new Insert
{
    Block = blockRecord,
    InsertPoint = new XYZ(0, 0, 0)
};
document.Entities.Add(insert);
```

Reference: `src/ACadSharp/Tables/BlockRecord.cs:Entities`, `src/ACadSharp/CadObjectCollection.cs`

**Verification:** Run the examples with `dotnet run` in ACadSharp.Examples project, or use unit tests in `src/ACadSharp.Tests/Entities/` directory to verify all entity operations work as expected.

---

**Last Updated:** 2025-12-14
**Focus:** Practical patterns for entity manipulation, properties, transformations, and collection management
