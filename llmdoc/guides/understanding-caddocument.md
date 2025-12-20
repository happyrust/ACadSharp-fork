# How to Understand and Use CadDocument

A practical guide for working with the central CAD document structure in ACadSharp.

## Step 1: Create a New Document

Initialize a CadDocument from scratch with default settings:

```csharp
// Create a new document (defaults to R2000/AC1015)
var document = new CadDocument();

// Verify default structure is created
Assert.NotNull(document.Header);
Assert.NotNull(document.Layers);
Assert.NotNull(document.LineTypes);
Assert.NotNull(document.Entities);  // ModelSpace entities
Assert.NotNull(document.BlockRecords);

// Access model space directly
var modelSpace = document.ModelSpace;
Assert.NotNull(modelSpace);

// Check for default layers and tables
Assert.True(document.Layers.Contains("0"));
Assert.True(document.LineTypes.Contains("Continuous"));
```

Reference: `src/ACadSharp/CadDocument.cs` (constructor), `src/ACadSharp/CadDocument.cs:CreateDefaults()`

## Step 2: Access Document Structure

Explore the hierarchical organization of tables and collections:

```csharp
// Access 9 main tables
var layers = document.Layers;           // LayersTable
var lineTypes = document.LineTypes;     // LineTypesTable
var blockRecords = document.BlockRecords;  // BlockRecordsTable
var textStyles = document.TextStyles;   // TextStylesTable
var dimensionStyles = document.DimensionStyles;  // DimensionStylesTable
var appIds = document.AppIds;           // AppIdsTable
var ucs = document.UCSs;                // UCSTable
var views = document.Views;             // ViewsTable
var vports = document.VPorts;           // VPortsTable

// Access optional collections from RootDictionary
var colors = document.Colors;           // BookColor collection
var groups = document.Groups;           // Group collection
var layouts = document.Layouts;         // Layout collection
var materials = document.Materials;     // Material collection
var mLineStyles = document.MLineStyles; // MLine style collection

// Access header variables
var cadVersion = document.Header.Version;  // AutoCAD version
var units = document.Header.MeasurementUnits;  // Drawing units
```

Reference: `src/ACadSharp/CadDocument.cs` (property definitions), `src/ACadSharp/Header/CadHeader.cs`

## Step 3: Read a CAD File

Load an existing DWG or DXF file:

```csharp
// Read DWG file (auto-detects format)
var document = DwgReader.Read("drawing.dwg");
Console.WriteLine($"Version: {document.Header.Version}");
Console.WriteLine($"Entities: {document.Entities.Count}");

// OR read DXF file (auto-detects ASCII/Binary)
var document = DxfReader.Read("drawing.dxf");

// Both methods return CadDocument with all structure populated

// Verify document integrity
Assert.NotNull(document.Header);
Assert.True(document.Entities.Count >= 0);
foreach (var entity in document.Entities)
{
    Assert.NotNull(entity.Document);
    Assert.True(entity.Handle > 0);
}
```

Reference: `src/ACadSharp/IO/DXF/DxfReader.cs:Read()`, `src/ACadSharp/IO/DWG/DwgReader.cs:Read()`

## Step 4: Browse the Document Tree

Navigate tables and blocks in hierarchical order:

```csharp
// List all layers
Console.WriteLine("Layers:");
foreach (var layer in document.Layers)
{
    Console.WriteLine($"  {layer.Name}: Color={layer.Color}, LineType={layer.LineType?.Name}");
}

// List all line types
Console.WriteLine("LineTypes:");
foreach (var lineType in document.LineTypes)
{
    Console.WriteLine($"  {lineType.Name}: PatternLength={lineType.PatternLength}");
}

// List all blocks
Console.WriteLine("Blocks:");
foreach (var blockRecord in document.BlockRecords)
{
    Console.WriteLine($"  {blockRecord.Name}: Entities={blockRecord.Entities.Count}");

    // List entities in each block
    foreach (var entity in blockRecord.Entities)
    {
        Console.WriteLine($"    - {entity.ObjectName} (Handle={entity.Handle})");
    }
}

// Access special blocks
var modelSpace = document.BlockRecords["*Model_Space"];
var paperSpace = document.BlockRecords["*Paper_Space"];

Console.WriteLine($"ModelSpace entities: {modelSpace.Entities.Count}");
Console.WriteLine($"PaperSpace entities: {paperSpace.Entities.Count}");
```

Reference: `src/ACadSharp/Tables/Collections/Table.cs:GetEnumerator()`, `src/ACadSharp/CadDocument.cs:ModelSpace,PaperSpace`

## Step 5: Query Entities by Handle

Retrieve objects by their unique handle identifier:

```csharp
// Get object by handle (returns CadObject or null)
ulong targetHandle = 256;  // Some handle value
if (document.TryGetCadObject(targetHandle, out var obj))
{
    Console.WriteLine($"Found: {obj.ObjectName} (Handle={obj.Handle})");
}

// Get object by handle with type checking
if (document.TryGetCadObject<Entity>(targetHandle, out var entity))
{
    Console.WriteLine($"Found entity: {entity.ObjectName}");
}

// Get all handles in document (after reading)
document.RestoreHandles();  // Rebuild handle map if corrupted

// Query objects of specific type
var lines = document.Entities.OfType<Line>();
foreach (var line in lines)
{
    Console.WriteLine($"Line: {line.Handle}");
}
```

Reference: `src/ACadSharp/CadDocument.cs:GetCadObject(),TryGetCadObject(),RestoreHandles()`

## Step 6: Add Entities to Model Space

Insert new entities into the drawing:

```csharp
// Create a layer first (optional, uses default "0" if not specified)
var myLayer = new Layer { Name = "MyLayer", Color = Color.FromIndex(3) };
if (!document.Layers.Contains("MyLayer"))
{
    document.Layers.Add(myLayer);
}

// Create and add entities to model space
var line = new Line
{
    StartPoint = new XYZ(0, 0, 0),
    EndPoint = new XYZ(10, 10, 0),
    Layer = myLayer
};
document.Entities.Add(line);

var circle = new Circle
{
    Center = new XYZ(5, 5, 0),
    Radius = 3,
    Color = Color.FromIndex(5),
    Layer = myLayer
};
document.Entities.Add(circle);

// Verify entities are in model space
Assert.Equal(2, document.Entities.Count);
foreach (var entity in document.Entities)
{
    Assert.Equal(document.ModelSpace, entity.Owner);
}
```

Reference: `src/ACadSharp/CadDocument.cs:Entities`, `src/ACadSharp/CadObjectCollection.cs:Add()`

## Step 7: Create New Layers and Manage Styles

Add custom layers and table entries:

```csharp
// Create a new layer with custom properties
var newLayer = new Layer
{
    Name = "ConstructionLines",
    Color = Color.FromIndex(7),  // White
    IsOn = true,
    PlotFlag = true
};

// Add to layers table
document.Layers.Add(newLayer);

// Create a new text style
var newStyle = new TextStyle
{
    Name = "MyStyle",
    FontFile = "arial.ttf"
};
document.TextStyles.Add(newStyle);

// Create a new dimension style (copy from standard)
var stdDimStyle = document.DimensionStyles["Standard"];
var newDimStyle = (DimensionStyle)stdDimStyle.Clone();
newDimStyle.Name = "MyDimStyle";
newDimStyle.TextHeight = 2.5;
document.DimensionStyles.Add(newDimStyle);

// Use the new layer for entities
var entity = new Line { Layer = newLayer, /* ... */ };
document.Entities.Add(entity);
```

Reference: `src/ACadSharp/Tables/Layer.cs`, `src/ACadSharp/Tables/TextStyle.cs`, `src/ACadSharp/Tables/DimensionStyle.cs`

## Step 8: Work with Blocks

Define blocks and insert them multiple times:

```csharp
// Create a block definition
var blockDef = new BlockRecord
{
    Name = "ElectricSymbol"
};
document.BlockRecords.Add(blockDef);

// Add geometry to the block
var blockLine = new Line
{
    StartPoint = new XYZ(0, 0, 0),
    EndPoint = new XYZ(5, 0, 0)
};
blockDef.Entities.Add(blockLine);

var blockCircle = new Circle
{
    Center = new XYZ(2.5, 0, 0),
    Radius = 0.5
};
blockDef.Entities.Add(blockCircle);

// Insert the block multiple times in different locations
var insert1 = new Insert
{
    Block = blockDef,
    InsertPoint = new XYZ(0, 0, 0)
};
document.Entities.Add(insert1);

var insert2 = new Insert
{
    Block = blockDef,
    InsertPoint = new XYZ(10, 0, 0),
    XScale = 1.5
};
document.Entities.Add(insert2);

Assert.Equal(2, document.Entities.OfType<Insert>().Count());
```

Reference: `src/ACadSharp/Tables/BlockRecord.cs:Entities`, `src/ACadSharp/Entities/Insert.cs`

## Step 9: Write the Document to File

Save the document in different formats:

```csharp
// Write as DWG (current version)
DwgWriter.Write("output.dwg", document);

// Write as DXF (binary format)
var dxfConfig = new DxfWriterConfiguration();
dxfConfig.Version = ACadVersion.AC1027;  // R2013-2017
DxfWriter.Write("output.dxf", document, dxfConfig);

// Write as DXF ASCII (human-readable)
dxfConfig.IsBinary = false;
DxfWriter.Write("output_ascii.dxf", document, dxfConfig);

// Verify the written file
var readBack = DwgReader.Read("output.dwg");
Assert.Equal(document.Entities.Count, readBack.Entities.Count);
```

Reference: `src/ACadSharp/IO/DWG/DwgWriter.cs:Write()`, `src/ACadSharp/IO/DXF/DxfWriter.cs:Write()`

## Step 10: Format Conversion and Version Compatibility

Convert between formats and CAD versions:

```csharp
// Read from any format
var document = DxfReader.Read("input.dxf");

// Check current version
Console.WriteLine($"Current version: {document.Header.Version}");

// Update version before writing
document.Header.Version = ACadVersion.AC1032;  // R2018+

// Write to DWG with specific version
var dwgConfig = new CadWriterConfiguration();
// Version is set in header
DwgWriter.Write("output_2018.dwg", document);

// Different format: DXF to DWG
var dxfDoc = DxfReader.Read("input.dxf");
DwgWriter.Write("converted.dwg", dxfDoc);

// DWG to DXF
var dwgDoc = DwgReader.Read("input.dwg");
DxfWriter.Write("converted.dxf", dwgDoc);
```

Reference: `src/ACadSharp/Header/CadHeader.cs:Version`, `src/ACadSharp/ACadVersion.cs`

**Verification:** Run examples with `dotnet run` in the ACadSharp.Examples project, or test with sample files in `samples/` directory. Use unit tests in `src/ACadSharp.Tests/` to verify all document operations.

---

**Last Updated:** 2025-12-14
**Focus:** Core document operations, table management, entity navigation, file I/O, and format conversion
