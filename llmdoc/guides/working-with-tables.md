# How to Work with Tables

Step-by-step instructions for common table operations in ACadSharp.

## 1. Access Existing Table Entries

**Retrieve a Layer:**
```csharp
if (document.Layers.TryGetValue("MyLayer", out var layer))
{
    Console.WriteLine($"Layer found: {layer.Name}");
}
```

**Get all Layers:**
```csharp
foreach (var layer in document.Layers)
{
    Console.WriteLine(layer.Name);
}
```

**Check if entry exists:**
```csharp
bool exists = document.Layers.Contains("0");  // True
```

## 2. Create and Add Table Entries

**Create a new Layer:**
```csharp
var newLayer = new Layer("Walls")
{
    Color = Color.Red,
    LineType = document.LineTypes["Dashed"],
    LineWeight = LineWeightType.W20
};
document.Layers.Add(newLayer);
```

**Create a new LineType:**
```csharp
var lineType = new LineType("DotDash")
{
    Description = "Dot-dash pattern",
    PatternLength = 2.5
};
lineType.Segments.Add(new Segment(1.0));      // Line segment
lineType.Segments.Add(new Segment(0.5, 0));   // Gap
document.LineTypes.Add(lineType);
```

**Create a new TextStyle:**
```csharp
var style = new TextStyle("MyFont")
{
    FontFileName = "arial.ttf",
    TextHeight = 2.5,
    WidthFactor = 0.8
};
document.TextStyles.Add(style);
```

## 3. Remove Table Entries

**Delete a Layer (if not default):**
```csharp
var removed = document.Layers.Remove("UnusedLayer");
if (removed != null)
{
    Console.WriteLine("Layer removed successfully");
}
// Layer "0" cannot be removed (returns null)
```

**Handle removal event:**
```csharp
document.Layers.OnRemove += (sender, args) =>
{
    Console.WriteLine($"Removed: {args.Item.Name}");
};
```

## 4. Modify Table Entries

**Change Layer Properties:**
```csharp
var layer = document.Layers["MyLayer"];
layer.Color = Color.Blue;
layer.IsOn = true;  // Make visible
```

**Rename an Entry:**
```csharp
var style = document.TextStyles["OldName"];
style.Name = "NewName";  // Dictionary key automatically updated
```

## 5. Create AppIds for XData

**Add Application ID:**
```csharp
var appId = new AppId("MyCustomApp");
document.AppIds.Add(appId);

// Later, attach XData using this AppId
var xdata = new ExtendedData();
xdata.AddControlStrings();
xdata.Records.Add(new ExtendedDataString("Custom value"));
entity.ExtendedData.Add(appId, xdata);
```

## 6. Query Multiple Entries

**Find entries by prefix:**
```csharp
var customLayers = document.Layers
    .Where(l => l.Name.StartsWith("Custom"))
    .ToList();
```

**Count entries:**
```csharp
Console.WriteLine($"Total layers: {document.Layers.Count}");
```

## 7. Handle Default Entries

**Access default Layer:**
```csharp
var defaultLayer = document.Layers["0"];  // Always exists
```

**Verify entry is not default before deletion:**
```csharp
string[] defaultLayers = { "0", "defpoints" };
if (!defaultLayers.Contains(entry.Name))
{
    document.Layers.Remove(entry.Name);
}
```

**Verification:** Run `document.Layers.Count` and confirm entry is gone.
