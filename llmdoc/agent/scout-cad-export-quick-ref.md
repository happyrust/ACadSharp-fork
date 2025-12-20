# ACadSharp CAD 数据导出 - 快速参考指南

## 核心 API 速查

### 1. 读取 CAD 文件并访问实体

```csharp
// 读取文件
CadDocument doc = DwgReader.Read("drawing.dwg");
// 或
CadDocument doc = DxfReader.Read("drawing.dxf");

// 遍历所有实体
foreach (var entity in doc.Entities)
{
    Console.WriteLine($"实体: {entity.ObjectName}, 图层: {entity.Layer.Name}");
}

// 按类型过滤
var lines = doc.Entities.OfType<Line>();
var circles = doc.Entities.OfType<Circle>();

// 获取特定实体属性
var color = entity.Color;           // 颜色（ByLayer、ByBlock、RGB、ACI）
var activeColor = entity.GetActiveColor();  // 解析后的实际颜色
var layer = entity.Layer;           // 图层引用
var lineType = entity.LineType;     // 线型引用
var weight = entity.LineWeight;     // 线宽
```

### 2. 访问表和系统资源

```csharp
// 访问 9 个核心表
var layers = doc.Layers;            // 图层表
var lineTypes = doc.LineTypes;      // 线型表
var textStyles = doc.TextStyles;    // 文本样式表
var blocks = doc.BlockRecords;      // 块记录表
var dimStyles = doc.DimensionStyles;  // 标注样式表
var appIds = doc.AppIds;            // 应用 ID 表
var ucs = doc.UCSs;                 // 坐标系表
var views = doc.Views;              // 视图表
var vports = doc.VPorts;            // 视口表

// 可选集合
var colors = doc.Colors;            // 命名颜色
var groups = doc.Groups;            // 分组
var layouts = doc.Layouts;          // 版面
var materials = doc.Materials;      // 材质

// 查询表项
if (doc.Layers.TryGetValue("LayerName", out var layer))
{
    Console.WriteLine($"图层颜色: {layer.Color}");
    Console.WriteLine($"图层线型: {layer.LineType?.Name}");
}

// 遍历表项
foreach (var layer in doc.Layers)
{
    Console.WriteLine($"{layer.Name}: Color={layer.Color}");
}
```

### 3. 访问几何属性

```csharp
// Line（直线）
if (entity is Line line)
{
    var start = line.StartPoint;    // XYZ
    var end = line.EndPoint;        // XYZ
    var normal = line.Normal;       // 法向量
}

// Circle（圆）
if (entity is Circle circle)
{
    var center = circle.Center;     // XYZ
    var radius = circle.Radius;     // double
}

// LwPolyline（多边形）
if (entity is LwPolyline poly)
{
    foreach (var vertex in poly.Vertices)
    {
        var pt = vertex.Location;   // XYZ
        var bulge = vertex.BulgeAngle;
    }
}

// Insert（块引入）
if (entity is Insert insert)
{
    var blockName = insert.Block.Name;
    var insertPoint = insert.InsertPoint;
    var scale = insert.XScale;      // 缩放因子

    // 访问块属性
    foreach (var attr in insert.Attributes)
    {
        var tag = attr.Tag;
        var value = attr.TextString;
    }
}

// Dimension（标注）
if (entity is Dimension dim)
{
    var defPoint = dim.DefinitionPoint;
    var insertPoint = dim.InsertionPoint;
}
```

### 4. 访问块中的实体

```csharp
// 访问模型空间（主要绘图区域）
var modelSpace = doc.ModelSpace;
foreach (var entity in modelSpace.Entities)
{
    // 处理实体
}

// 访问特定块
var block = doc.BlockRecords["BlockName"];
foreach (var entity in block.Entities)
{
    // 处理块内实体
}

// 访问块属性定义
foreach (var attrDef in block.AttributeDefinitions)
{
    var tag = attrDef.Tag;
}
```

### 5. 导出为文件

```csharp
// 导出为 DXF（二进制）
var config = new DxfWriterConfiguration
{
    Version = ACadVersion.AC1027,  // R2013-2017
    IsBinary = true
};
DxfWriter.Write("output.dxf", doc, config);

// 导出为 DXF ASCII（可读文本）
config.IsBinary = false;
DxfWriter.Write("output.dxf", doc, config);

// 导出为 DWG
DwgWriter.Write("output.dwg", doc);

// 使用流导出
using (FileStream stream = File.Create("output.dxf"))
{
    DxfWriter.Write(stream, doc, config);
}
```

### 6. 导出为 JSON（手动序列化）

```csharp
// 导出文档元数据和实体列表
var export = new
{
    Version = doc.Header.Version,
    Author = doc.Header.Author,
    Created = doc.Header.CreateTime,

    // 导出所有图层
    Layers = doc.Layers.Select(l => new
    {
        l.Name,
        Color = l.Color.Index ?? 0,
        LineType = l.LineType?.Name ?? "Continuous",
        IsOn = !l.IsFrozen
    }).ToList(),

    // 导出所有实体
    Entities = doc.Entities.Select(e => new
    {
        e.Handle,
        e.ObjectName,
        LayerName = e.Layer.Name,
        Color = e.Color.Index,
        LineWeight = (int)e.LineWeight,

        // 根据类型提取几何属性
        Geometry = e switch
        {
            Line line => new { Type = "Line", Start = line.StartPoint, End = line.EndPoint },
            Circle circle => new { Type = "Circle", Center = circle.Center, Radius = circle.Radius },
            LwPolyline poly => new { Type = "Polyline", VertexCount = poly.Vertices.Count },
            Insert insert => new { Type = "Insert", Block = insert.Block.Name, Point = insert.InsertPoint },
            _ => new { Type = e.ObjectName }
        }
    }).ToList()
};

string json = System.Text.Json.JsonSerializer.Serialize(export, new System.Text.Json.JsonSerializerOptions
{
    WriteIndented = true,
    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
});

File.WriteAllText("output.json", json);
```

## 常见场景示例

### 统计文档内容

```csharp
var doc = DwgReader.Read("drawing.dwg");

// 统计实体类型
var stats = doc.Entities
    .GroupBy(e => e.ObjectName)
    .ToDictionary(g => g.Key, g => g.Count());

foreach (var kvp in stats)
{
    Console.WriteLine($"{kvp.Key}: {kvp.Value}");
}

// 统计图层使用情况
var layerStats = doc.Layers.Select(l => new
{
    LayerName = l.Name,
    EntityCount = doc.Entities.Count(e => e.Layer == l),
    Color = l.Color
});

foreach (var stat in layerStats)
{
    Console.WriteLine($"{stat.LayerName}: {stat.EntityCount} 个实体");
}
```

### 按图层提取实体

```csharp
var targetLayer = "Layer1";
var layerEntities = doc.Entities.Where(e => e.Layer.Name == targetLayer).ToList();

Console.WriteLine($"图层 '{targetLayer}' 中有 {layerEntities.Count} 个实体");

foreach (var entity in layerEntities)
{
    // 处理实体
}
```

### 访问块引入及其属性

```csharp
var inserts = doc.Entities.OfType<Insert>();

foreach (var insert in inserts)
{
    Console.WriteLine($"块: {insert.Block.Name}");
    Console.WriteLine($"位置: {insert.InsertPoint}");
    Console.WriteLine($"缩放: X={insert.XScale}, Y={insert.YScale}");

    // 如果块有属性
    if (insert.Block.HasAttributes)
    {
        foreach (var attr in insert.Attributes)
        {
            Console.WriteLine($"  属性 {attr.Tag}: {attr.TextString}");
        }
    }
}
```

### 创建新 CAD 文档并导出

```csharp
// 创建新文档
var doc = new CadDocument();

// 添加图层
var layer = new Layer { Name = "MyLayer", Color = Color.FromIndex(1) };
doc.Layers.Add(layer);

// 添加实体
var line = new Line
{
    StartPoint = new XYZ(0, 0, 0),
    EndPoint = new XYZ(10, 10, 0),
    Layer = layer
};
doc.Entities.Add(line);

// 导出
DwgWriter.Write("new_drawing.dwg", doc);
```

---

## 属性速查表

| 属性 | 类型 | 说明 | 访问模式 |
|------|------|------|---------|
| Color | Color | 颜色（ByLayer/ByBlock/RGB/ACI） | entity.Color / entity.GetActiveColor() |
| Layer | Layer | 图层引用 | entity.Layer |
| LineType | LineType | 线型引用 | entity.LineType |
| LineWeight | LineWeightType | 线宽 | entity.LineWeight |
| Transparency | Transparency | 透明度 (0-255) | entity.Transparency |
| Handle | ulong | 唯一 ID | entity.Handle |
| Owner | CadObject | 所有者 | entity.Owner |
| Document | CadDocument | 所属文档 | entity.Document |

---

**版本**：ACadSharp 3.3.13
**最后更新**：2025-12-14
