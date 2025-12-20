# How to Work with Blocks and Inserts

Step-by-step instructions for creating, managing, and using blocks in ACadSharp.

## 1. Create a Block Definition

**Create a simple block:**
```csharp
// Create block record
var blockRecord = new BlockRecord("Door");
document.BlockRecords.Add(blockRecord);

// Add entities to block (they become owned by blockRecord)
var line = new Line(XYZ.Zero, new XYZ(1, 0, 0));
blockRecord.Entities.Add(line);
```

**Verify creation:**
```csharp
bool exists = document.BlockRecords.Contains("Door");
Console.WriteLine(exists);  // True
```

## 2. Add Entities to Blocks

**Add multiple entities:**
```csharp
var block = document.BlockRecords["MyBlock"];
block.Entities.Add(new Line(...));
block.Entities.Add(new Circle(...));
block.Entities.Add(new TextEntity(...));

// Verify
Console.WriteLine($"Block has {block.Entities.Count} entities");
```

**Access block base point:**
```csharp
var basePoint = block.BlockEntity.BasePoint;  // Typically XYZ.Zero
```

## 3. Create Block References (Inserts)

**Basic Insert:**
```csharp
var blockRecord = document.BlockRecords["Door"];
var insert = new Insert(blockRecord)
{
    InsertPoint = new XYZ(10, 5, 0),
    XScale = 2.0,
    Rotation = Math.PI / 4  // 45 degrees in radians
};
document.Entities.Add(insert);
```

**Insert with properties:**
```csharp
var insert = new Insert(blockRecord)
{
    InsertPoint = new XYZ(0, 0, 0),
    XScale = 1.0,
    YScale = 1.0,
    ZScale = 1.0,
    Normal = XYZ.AxisZ,
    Layer = document.Layers["InsertLayer"]
};
document.Entities.Add(insert);
```

## 4. Array Insertions

**Create array of block references:**
```csharp
var insert = new Insert(blockRecord)
{
    InsertPoint = new XYZ(0, 0, 0),
    RowCount = 3,
    ColumnCount = 4,
    RowSpacing = 5.0,
    ColumnSpacing = 10.0
};
document.Entities.Add(insert);
```

**Check if multiple:**
```csharp
if (insert.IsMultiple)  // true if RowCount > 1 or ColumnCount > 1
{
    Console.WriteLine("This is an array insert");
}
```

## 5. Work with Attributes

**Define attributes in block:**
```csharp
var attrDef = new AttributeDefinition("TAG1")
{
    DefaultValue = "Default Text",
    InsertPoint = new XYZ(0, 0, 0),
    Height = 2.5,
    Alignment = TextAlignment.MiddleCenter
};
block.Entities.Add(attrDef);
```

**Get attribute definitions:**
```csharp
var blockRecord = document.BlockRecords["Door"];
foreach (var attrDef in blockRecord.AttributeDefinitions)
{
    Console.WriteLine($"Attribute: {attrDef.Tag}");
}
```

**Update insert attributes:**
```csharp
var insert = new Insert(blockRecord);
insert.UpdateAttributes();  // Create attribute entities from definitions

// Set attribute values
foreach (var attr in insert.Attributes)
{
    if (attr.Tag == "DOOR_TYPE")
    {
        attr.Value = "SinglePane";
    }
}
```

## 6. Access Insert Attributes

**Read attribute values:**
```csharp
var insert = document.Entities.OfType<Insert>().FirstOrDefault();
foreach (var attr in insert.Attributes)
{
    Console.WriteLine($"{attr.Tag}: {attr.Value}");
}
```

**Find specific attribute:**
```csharp
var doorType = insert.Attributes
    .FirstOrDefault(a => a.Tag == "DOOR_TYPE");
if (doorType != null)
{
    Console.WriteLine($"Door Type: {doorType.Value}");
}
```

## 7. XRef Block References

**Create XRef block:**
```csharp
var xrefRecord = new BlockRecord("ExternalRef")
{
    // Set XRef flags
};
var xrefBlock = xrefRecord.BlockEntity;
xrefBlock.XRefPath = "C:\\CAD\\external.dwg";
xrefBlock.Flags |= BlockTypeFlags.XRef;
document.BlockRecords.Add(xrefRecord);
```

**Check if block is XRef:**
```csharp
var block = document.BlockRecords["ExternalRef"];
if ((block.BlockEntity.Flags & BlockTypeFlags.XRef) != 0)
{
    Console.WriteLine($"XRef path: {block.BlockEntity.XRefPath}");
}
```

## 8. Anonymous Blocks

**Access unnamed block:**
```csharp
// System automatically generates name like "*A1"
var anonymousBlock = document.BlockRecords["*A1"];
Console.WriteLine($"Anonymous block: {anonymousBlock.Name}");
```

## 9. Model and Paper Space

**Add entity to Model Space:**
```csharp
var line = new Line(XYZ.Zero, new XYZ(10, 10, 0));
document.ModelSpace.Entities.Add(line);
// or simply: document.Entities.Add(line);
```

**Access Paper Space:**
```csharp
var text = new TextEntity { Value = "Title" };
document.PaperSpace.Entities.Add(text);
```

**Verify current space:**
```csharp
Console.WriteLine($"Model space has {document.ModelSpace.Entities.Count} entities");
Console.WriteLine($"Paper space has {document.PaperSpace.Entities.Count} entities");
```

## 10. Verify Block Operations

**Confirm block created:**
```csharp
if (document.BlockRecords.TryGetValue("Door", out var block))
{
    Console.WriteLine($"Block exists with {block.Entities.Count} entities");
}
```

**Check insert references correct block:**
```csharp
var insert = document.Entities.OfType<Insert>().First();
Console.WriteLine($"Insert references: {insert.Block.Name}");
```
