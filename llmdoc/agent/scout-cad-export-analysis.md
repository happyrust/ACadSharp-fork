# ACadSharp CAD 数据导出机制 - 深度侦察报告

## 调查目标

深入分析 ACadSharp 项目如何导出 CAD 数据，包括：
1. CadDocument 对象模型和 API
2. 遍历和访问实体的方式
3. 实体属性的访问模式
4. 表系统（图层、线型等）的数据结构
5. JSON 序列化和数据导出机制

**调查时间**：2025-12-14
**项目版本**：ACadSharp 3.3.13

---

## 代码部分（证据）

### CadDocument 核心结构

- `src/ACadSharp/CadDocument.cs` (CadDocument): 中央文档容器，管理 9 个核心表、可选集合、头部变量、所有实体和对象的索引。主要属性：Entities（模型空间实体）、Header（头部变量）、Layers/LineTypes/TextStyles/BlockRecords/DimensionStyles/AppIds/UCSs/Views/VPorts（9个核心表）、Colors/Groups/Layouts/Materials/MLineStyles/MLeaderStyles（可选集合）、ModelSpace/PaperSpace（特殊块）。

### 实体基础设施

- `src/ACadSharp/Entities/Entity.cs` (Entity): 所有图形实体的抽象基类，继承 CadObject，包含标准视觉属性（Color、Layer、LineType、LineWeight、Transparency、Material）和变换方法（ApplyTransform、ApplyTranslation、ApplyRotation、ApplyScaling）。支持 DXF 映射通过 [DxfCodeValue] 特性。

- `src/ACadSharp/Entities/IEntity.cs` (IEntity): Entity 接口定义，规范实体的标准属性和方法。

### 实体属性访问

- `src/ACadSharp/Entities/Entity.cs:Color` - [DxfCodeValue(62, 420)]: 颜色属性，支持 ByLayer、ByBlock、RGB、ACI 模式。

- `src/ACadSharp/Entities/Entity.cs:Layer` - [DxfCodeValue(DxfReferenceType.Name, 8)]: 图层引用，自动绑定到 document.Layers 表。

- `src/ACadSharp/Entities/Entity.cs:LineType` - [DxfCodeValue(DxfReferenceType.Name, 6)]: 线型引用，自动绑定到 document.LineTypes 表。

- `src/ACadSharp/Entities/Entity.cs:LineWeight` - [DxfCodeValue(370)]: 线宽属性，支持 ByLayer/ByBlock 或毫米值。

- `src/ACadSharp/Entities/Entity.cs:Transparency` - [DxfCodeValue(440)]: 透明度，值范围 0-255。

- `src/ACadSharp/Entities/Entity.cs:Material` - [DxfCodeValue(DxfReferenceType.Handle, 347)]: 材质引用。

- `src/ACadSharp/Entities/Entity.cs:LineTypeScale` - [DxfCodeValue(48)]: 线型缩放因子。

### 几何实体类型（144+ 种）

#### 基本几何实体

- `src/ACadSharp/Entities/Line.cs` (Line): 直线，包含 StartPoint、EndPoint、Normal、Thickness。

- `src/ACadSharp/Entities/Circle.cs` (Circle): 圆，包含 Center、Radius、Normal、Thickness。

- `src/ACadSharp/Entities/Arc.cs` (Arc): 圆弧，继承 Circle，添加 StartAngle、EndAngle、Sweep。

- `src/ACadSharp/Entities/Ellipse.cs` (Ellipse): 椭圆，包含 Center、MajorAxisEndPoint、RadiusRatio、StartParameter、EndParameter。

- `src/ACadSharp/Entities/Point.cs` (Point): 点，包含 Location、Thickness、Rotation。

- `src/ACadSharp/Entities/Ray.cs` (Ray): 射线，包含 Origin、Direction。

- `src/ACadSharp/Entities/XLine.cs` (XLine): 无限线，包含 BasePoint、Direction。

- `src/ACadSharp/Entities/Face3D.cs` (Face3D): 3D 面，包含 Vertices。

#### 多边形实体

- `src/ACadSharp/Entities/LwPolyline.cs` (LwPolyline): 轻量级多边形（推荐），包含 Vertices（List<Vertex>）、ConstantWidth、Elevation、Normal、Thickness、LwPolylineFlags。

- `src/ACadSharp/Entities/PolyLine.cs` (Polyline<T>): 通用多边形基类，包含 Vertices（SeqendCollection<T>）、Elevation、StartWidth、EndWidth、Normal、Thickness、PolylineFlags。衍生类：PolyLine2D、PolyLine3D。

- `src/ACadSharp/Entities/Vertex.cs` (Vertex): 多边形顶点，包含 Location、BulgeAngle、StartWidth、EndWidth、Identifier、VertexFlags。

#### 文本和标注实体

- `src/ACadSharp/Entities/TextEntity.cs` (TextEntity): 单行文本，包含 InsertPoint、Height、Rotation、ObliqueAngle、HorizontalAlignment、VerticalAlignment、AlignmentPoint、Style、Normal。

- `src/ACadSharp/Entities/MText.cs` (MText): 多行文本，包含 InsertPoint、AttachmentPoint、Width、Normal、Height、Rotation、LineSpacingFactor、LineSpacingStyle、Background、Style。

- `src/ACadSharp/Entities/Dimension.cs` (Dimension): 标注基类，包含 DefinitionPoint、InsertionPoint、Block reference、DimensionStyle、HorizontalDirection、AttachmentPoint、FlipArrow1/2、LineSpacingFactor/Style。衍生类：DimensionLinear、DimensionAligned、DimensionAngular2Line、DimensionAngular3Pt、DimensionDiameter、DimensionRadius、DimensionOrdinate。

- `src/ACadSharp/Entities/AttributeEntity.cs` (AttributeEntity): 块属性，包含 Tag、TextString、InsertPoint、TextHeight、Rotation、TextStyle、Alignment。

#### 块和引入实体

- `src/ACadSharp/Entities/Insert.cs` (Insert): 块引用，包含 Block reference、InsertPoint、Rotation、Normal、XScale/YScale/ZScale、RowCount/ColumnCount、RowSpacing/ColumnSpacing、Attributes（SeqendCollection<AttributeEntity>）、SpatialFilter。方法：ApplyTransform()、Clone()、Explode()、UpdateAttributes()。

- `src/ACadSharp/Blocks/Block.cs` (Block): 块实体（定义标记），包含 BlockOwner、BasePoint、Name、XRefPath、Comments、Flags（BlockTypeFlags）。

- `src/ACadSharp/Blocks/BlockEnd.cs` (BlockEnd): 块终止实体（结束标记）。

#### 高级实体

- `src/ACadSharp/Entities/Spline.cs` (Spline): 样条曲线，包含 ControlPoints（List<XYZ>）、FitPoints、Degree、Knots、StartTangent、EndTangent、ControlPointTolerance、FitTolerance、SplineFlags。

- `src/ACadSharp/Entities/Hatch.cs` (Hatch): 填充图案，包含 Paths（List<BoundaryPath>）、Pattern（HatchPattern）、PatternAngle、PatternScale、Normal、Elevation、IsSolid、IsDouble、IsAssociative、GradientColor、AssociatedObjects。

- `src/ACadSharp/Entities/Mesh.cs` (Mesh): 网格，包含 MeshVertices、MSize、NSize、MClosedFlag、NClosedFlag、MLevel、NLevel、SurfaceType。

- `src/ACadSharp/Entities/ModelerGeometry.cs` (ModelerGeometry): 3D 实体，包含 SatData。

- `src/ACadSharp/Entities/Viewport.cs` (Viewport): 视口，包含 Center、Width、Height、ViewportID、ViewportStatus、ViewTarget、ViewDirection。

- `src/ACadSharp/Entities/MultiLeader.cs` (MultiLeader): 多引线，包含多条引线和文本。

- `src/ACadSharp/Entities/RasterImage.cs` (RasterImage): 光栅图像，包含 ImageDefinition reference、InsertionPoint、UVector、VVector、ImageWidth、ImageHeight、Transparency、ClipBoundaryVertices。

### 表系统

- `src/ACadSharp/Tables/Collections/Table.cs` (Table<T>): 通用表基类，实现 ICadCollection<T> 和 IObservableCadCollection<T>，使用字典管理条目（不区分大小写）。支持 Add、Remove、TryGetValue、Contains、GetEnumerator 等操作。

- `src/ACadSharp/Tables/TableEntry.cs` (TableEntry): 表项基类，继承 CadObject，实现 INamedCadObject（Name 和 Flags 属性）。

- `src/ACadSharp/Tables/Layer.cs` (Layer): 图层表项，包含 Color、LineType、LineWeight、Material、IsOn、PlotFlag。

- `src/ACadSharp/Tables/LineType.cs` (LineType): 线型表项，包含 Segments collection、PatternLength、IsComplex、IsShape flags。

- `src/ACadSharp/Tables/TextStyle.cs` (TextStyle): 文本样式表项，包含 FontFile、TextHeight、WidthFactor、ObliqueAngle、BackwardsFlag、UpsideDownFlag、IsShapeFile。

- `src/ACadSharp/Tables/BlockRecord.cs` (BlockRecord): 块记录表项，包含 BlockEntity、BlockEnd、Entities collection（CadObjectCollection<Entity>）、AttributeDefinitions、Layout、EvaluationGraph、SortEntitiesTable。

- `src/ACadSharp/Tables/DimensionStyle.cs` (DimensionStyle): 标注样式表项，包含 40+ 属性控制标注外观（DimensionLineColor、TextHeight、TextColor、ArrowSize 等）。

- `src/ACadSharp/Tables/AppId.cs` (AppId): 应用 ID 表项，用于 XData 关联和追踪。

- `src/ACadSharp/Tables/UCS.cs` (UCS): 用户坐标系表项，包含 Origin、XAxisDirection、YAxisDirection。

- `src/ACadSharp/Tables/View.cs` (View): 命名视图表项，包含 Center、Width、Height、ClipingPlanes。

- `src/ACadSharp/Tables/VPort.cs` (VPort): 视口表项，包含 Coordinates、SnapBasePoint、GridSpacing、ViewDirection。

- `src/ACadSharp/Tables/Collections/LayersTable.cs` (LayersTable): 图层表具体实现。

- `src/ACadSharp/Tables/Collections/BlockRecordsTable.cs` (BlockRecordsTable): 块记录表具体实现，处理匿名块命名。

### DXF 映射系统

- `src/ACadSharp/DxfMap.cs` (DxfMap): 元数据驱动的 DXF 映射生成器，通过反射扫描特性生成属性到 DXF 代码的映射。缓存机制通过 ConcurrentDictionary。支持子类标记和多值属性映射。

- `src/ACadSharp/DxfProperty.cs` (DxfProperty): DXF 属性映射，包含 DxfCode、PropertyName、Name、Units、ReferenceType。

- `src/ACadSharp/Attributes/DxfCodeValueAttribute.cs`: 特性标记属性与 DXF 代码的关联。

- `src/ACadSharp/Attributes/DxfNameAttribute.cs`: 特性标记对象的 DXF 名称。

- `src/ACadSharp/Attributes/DxfSubClassAttribute.cs`: 特性标记 DXF 子类名称。

### 写入和序列化

- `src/ACadSharp/IO/DWG/DwgWriter.cs` (DwgWriter): DWG 格式写入器，静态方法 Write(path, document)、Write(stream, document)，支持版本选择。

- `src/ACadSharp/IO/DXF/DxfWriter.cs` (DxfWriter): DXF 格式写入器，静态方法 Write(path, document, config)、Write(stream, document, config)，支持 ASCII 和二进制格式。

- `src/ACadSharp/IO/CadWriterBase.cs` (CadWriterBase): 写入器基类，定义通用接口 Write(CadDocument)。

- `src/ACadSharp/IO/DXF/DxfWriterConfiguration.cs`: DXF 写入配置，包含 Version、IsBinary、WriteXData 等选项。

- `src/ACadSharp/IO/DWG/DwgWriterConfiguration.cs`: DWG 写入配置。

### CadObject 基础

- `src/ACadSharp/CadObject.cs` (CadObject): 所有 CAD 对象的抽象基类，包含 Handle（唯一 ID）、Owner、Document、XDictionary、XData、Reactors。支持 Clone() 和对象生命周期管理。

### 访问模式

- `src/ACadSharp/CadDocument.cs:TryGetCadObject<T>()`: 通过 Handle 获取特定类型的对象。

- `src/ACadSharp/CadDocument.cs:GetCadObject<T>()`: 通过 Handle 获取对象，抛出异常如果不存在。

- `src/ACadSharp/CadDocument.cs:RestoreHandles()`: 重建 Handle 映射。

- `src/ACadSharp/Tables/Collections/Table.cs:TryGetValue()`: 通过名称查询表项（不区分大小写）。

- `src/ACadSharp/Tables/Collections/Table.cs:Contains()`: 检查表项是否存在。

---

## 调查报告

### result

#### 1. CadDocument 对象模型

**CadDocument** 是 ACadSharp 的核心容器，包含：

- **9 个核心表**：Layers、LineTypes、TextStyles、BlockRecords、DimensionStyles、AppIds、UCSs、Views、VPorts。所有表都继承 `Table<T>` 基类，支持类型安全的集合操作。

- **可选集合**（通过 RootDictionary）：Colors、Groups、Layouts、Materials、MLineStyles、MLeaderStyles、Scales。

- **特殊块**：ModelSpace（*Model_Space）包含绘图的主体实体，PaperSpace（*Paper_Space）是默认的纸张空间。

- **头部变量**：CadHeader 包含版本（Version）、单位、比例、创建时间、作者等 100+ 个系统变量。

- **全局索引**：通过 Handle（64 位唯一 ID）快速查找任何对象，O(1) 性能。

#### 2. 实体遍历和访问

**遍历所有实体** - 通过 document.Entities（实际是 ModelSpace.Entities 的快捷方式）：

```csharp
foreach (var entity in document.Entities)
{
    // entity 是 Entity 类型（Line、Circle、Arc 等）
    string type = entity.ObjectName;  // "LINE", "CIRCLE" 等
    ulong handle = entity.Handle;
    Layer layer = entity.Layer;
}
```

**按类型过滤** - 使用 LINQ：

```csharp
var lines = document.Entities.OfType<Line>();
var circles = document.Entities.OfType<Circle>();
var inserts = document.Entities.OfType<Insert>();
```

**按属性过滤**：

```csharp
var layerEntities = document.Entities.Where(e => e.Layer.Name == "LayerName");
var coloredEntities = document.Entities.Where(e => e.Color.Index == 1);
```

**通过 Handle 查询**：

```csharp
if (document.TryGetCadObject<Entity>(handle, out var entity))
{
    // 找到对象
}
```

**访问块内的实体**：

```csharp
foreach (var blockRecord in document.BlockRecords)
{
    foreach (var entity in blockRecord.Entities)
    {
        // 处理块内的实体
    }
}
```

#### 3. 实体属性访问模式

**标准视觉属性** - 所有 Entity 都有：

```csharp
Color color = entity.Color;           // ByLayer、ByBlock、RGB(0-255)、ACI(0-255)
Layer layer = entity.Layer;           // 引用到 document.Layers[name]
LineType linetype = entity.LineType;  // 引用到 document.LineTypes[name]
LineWeightType weight = entity.LineWeight;  // ByLayer、ByBlock 或 W013-W200
Transparency trans = entity.Transparency;  // 0-255
Material mat = entity.Material;       // 可选材质引用
double scale = entity.LineTypeScale;  // 线型缩放因子
```

**获取实际属性**（解析 ByLayer/ByBlock）：

```csharp
Color activeColor = entity.GetActiveColor();        // 自动解析继承
LineType activeLineType = entity.GetActiveLineType();
LineWeightType activeWeight = entity.GetActiveLineWeightType();
```

**几何属性** - 因实体类型而异：

```csharp
if (entity is Line line)
{
    XYZ startPt = line.StartPoint;
    XYZ endPt = line.EndPoint;
    XYZ normal = line.Normal;
    double thickness = line.Thickness;
}
else if (entity is Circle circle)
{
    XYZ center = circle.Center;
    double radius = circle.Radius;
    XYZ normal = circle.Normal;
}
else if (entity is LwPolyline poly)
{
    foreach (var vertex in poly.Vertices)
    {
        XYZ location = vertex.Location;
        double bulge = vertex.BulgeAngle;
    }
}
```

**扩展数据** - 通过 XData 和 XDictionary：

```csharp
XDataCollection xdata = entity.XData;      // ExtendedData 15 种类型
CadDictionary xdict = entity.XDictionary;  // 非图形对象容器
```

#### 4. 表系统的数据结构和访问

**表的通用接口**（所有 9 个表都支持）：

```csharp
// 添加表项
var newLayer = new Layer { Name = "MyLayer", Color = Color.FromIndex(5) };
document.Layers.Add(newLayer);

// 按名称查询（不区分大小写）
if (document.Layers.TryGetValue("MyLayer", out var layer))
{
    // 找到图层
}

// 检查是否存在
bool exists = document.Layers.Contains("MyLayer");

// 遍历表项
foreach (var layer in document.Layers)
{
    string name = layer.Name;
    Color color = layer.Color;
}

// 删除表项（默认项受保护）
document.Layers.Remove("MyLayer");
```

**图层表（LayersTable）** - 特定访问模式：

```csharp
// 创建和添加图层
var layer = new Layer
{
    Name = "MyLayer",
    Color = Color.FromIndex(3),      // 颜色索引
    LineType = document.LineTypes["Dashed"],  // 线型引用
    LineWeight = LineWeightType.W025,  // 0.25mm
    IsOn = true,           // 可见性
    IsFrozen = false,      // 冻结状态
    PlotFlag = true        // 打印标志
};
document.Layers.Add(layer);

// 访问图层属性
foreach (var layer in document.Layers)
{
    Console.WriteLine($"{layer.Name}: Color={layer.Color}, LineType={layer.LineType?.Name}");
}

// 为实体指定图层
entity.Layer = document.Layers["MyLayer"];
```

**线型表（LineTypesTable）** - 线型定义：

```csharp
// 访问线型
foreach (var lineType in document.LineTypes)
{
    string name = lineType.Name;           // "Continuous", "Dashed" 等
    double patternLength = lineType.PatternLength;
    bool isComplex = lineType.IsComplex;

    // 线型段集合
    foreach (var segment in lineType.Segments)
    {
        // 段长度、文本、形状等
    }
}
```

**文本样式表（TextStylesTable）**：

```csharp
// 创建文本样式
var style = new TextStyle
{
    Name = "MyStyle",
    FontFile = "arial.ttf",
    TextHeight = 2.5,
    WidthFactor = 1.0,
    ObliqueAngle = 0
};
document.TextStyles.Add(style);

// 访问文本样式
foreach (var style in document.TextStyles)
{
    var font = style.FontFile;
}
```

**块记录表（BlockRecordsTable）** - 块定义和实体容器：

```csharp
// 创建块定义
var blockRec = new BlockRecord { Name = "MyBlock" };
document.BlockRecords.Add(blockRec);

// 添加实体到块
var line = new Line { StartPoint = new XYZ(0,0,0), EndPoint = new XYZ(5,0,0) };
blockRec.Entities.Add(line);  // 添加到块，而不是模型空间

// 访问块中的实体
foreach (var entity in blockRec.Entities)
{
    // 处理块内实体
}

// 访问块属性定义
foreach (var attrDef in blockRec.AttributeDefinitions)
{
    string tag = attrDef.Tag;
}

// 插入块引用
var insert = new Insert
{
    Block = blockRec,
    InsertPoint = new XYZ(10, 10, 0),
    XScale = 1.5,
    YScale = 1.5,
    ZScale = 1.0
};
document.Entities.Add(insert);

// 访问特殊块
var modelSpace = document.ModelSpace;
var paperSpace = document.PaperSpace;
```

**标注样式表（DimensionStylesTable）** - 标注控制：

```csharp
// 访问标注样式
var dimStyle = document.DimensionStyles["Standard"];

// 标注样式有 40+ 属性控制外观
double textHeight = dimStyle.TextHeight;
Color dimLineColor = dimStyle.DimensionLineColor;
Color textColor = dimStyle.TextColor;
double arrowSize = dimStyle.ArrowSize;
```

#### 5. 属性继承链（ByLayer/ByBlock 解析）

实体的视觉属性遵循继承链：

```
实体属性 → ByLayer 解析 → Layer 属性 → 实际值
          ↓
        ByBlock 解析（仅在块属性中）→ Insert 的属性

实体属性访问：
- entity.Color：直接设置的值，可能是 ByLayer/ByBlock/RGB/ACI
- entity.GetActiveColor()：自动解析后的实际颜色

类似地：
- entity.LineType → entity.GetActiveLineType()
- entity.LineWeight → entity.GetActiveLineWeightType()
```

#### 6. 序列化和数据导出

**DXF 映射系统** - 自动序列化：

所有实体和表项通过 `DxfMap` 自动映射到 DXF 代码：

```csharp
// 内部工作原理（对用户透明）
DxfMap map = DxfMap.Create<Line>();
// map.SubClasses 包含 "AcDbEntity" 和 "AcDbLine" 子类
// 每个子类有属性映射到 DXF 代码

// 属性映射示例：
// [DxfCodeValue(10, 20, 30)] public XYZ StartPoint  → DXF codes 10,20,30
// [DxfCodeValue(11, 21, 31)] public XYZ EndPoint    → DXF codes 11,21,31
// [DxfCodeValue(DxfReferenceType.Name, 8)] public Layer Layer → DXF code 8 (layer name)
```

**写入 DXF 文件**：

```csharp
// 基本写入
var config = new DxfWriterConfiguration { Version = ACadVersion.AC1027 };
DxfWriter.Write("output.dxf", document, config);

// ASCII DXF（人可读）
config.IsBinary = false;
DxfWriter.Write("output_ascii.dxf", document, config);

// 包括 XData
config.WriteXData = true;
```

**写入 DWG 文件**：

```csharp
// 基本写入（自动使用 document.Header.Version）
DwgWriter.Write("output.dwg", document);

// 更新版本后写入
document.Header.Version = ACadVersion.AC1032;  // R2018+
DwgWriter.Write("output_2018.dwg", document);
```

**手动数据导出为 JSON**：

```csharp
var exportData = new
{
    Version = document.Header.Version,
    Author = document.Header.Author,
    Created = document.Header.CreateTime,
    EntityCount = document.Entities.Count,
    Layers = document.Layers.Select(l => new
    {
        l.Name,
        Color = l.Color.Index,
        LineType = l.LineType?.Name
    }).ToList(),
    Entities = document.Entities.Select(e => new
    {
        e.Handle,
        e.ObjectName,
        e.Layer.Name,
        e.Color,
        e.LineWeight
    }).ToList()
};

string json = System.Text.Json.JsonSerializer.Serialize(exportData);
```

---

### conclusions

**关键结论**：

1. **统一的对象模型**：CadDocument 通过 9 个表和可选集合管理所有 CAD 对象，提供单一的访问入口。

2. **高效的遍历机制**：document.Entities 提供快速的 LINQ 查询能力，支持按类型、属性、Handle 过滤。

3. **属性继承系统**：实体属性支持 ByLayer/ByBlock 继承，通过 GetActive* 方法自动解析。

4. **元数据驱动序列化**：DxfMap 通过反射和特性系统自动映射属性到 DXF 代码，无需手动代码。

5. **多种导出方式**：
   - DXF 写入（ASCII 和二进制）
   - DWG 写入
   - 手动 JSON 序列化（通过反射访问属性）
   - Stream 支持（内存优化）

6. **表系统的一致性**：所有 9 个表使用相同接口（Add、Remove、TryGetValue、Contains），支持不区分大小写的查询。

7. **块和引入的分离设计**：
   - BlockRecord（表项）= 块定义和实体容器
   - Insert（实体）= 块引用（轻量级）
   - Block/BlockEnd（实体）= 定义的标记

8. **扩展机制**：XData 和 XDictionary 支持附加任意数据到对象，无需修改核心结构。

---

### relations

**关键代码关联**：

- `CadDocument.cs` 依赖所有 9 个表（Layers、LineTypes、TextStyles 等），通过构造函数创建默认实例。

- `CadDocument.cs:Entities` 是 `ModelSpace.Entities` 的快捷方式，后者是 `BlockRecord.Entities` 的一个实例。

- `Entity.cs` 的所有视觉属性（Color、Layer、LineType）通过 setter 更新到 document 中的表实例（通过 updateCollection 方法）。

- `DxfWriter.cs` 和 `DwgWriter.cs` 都依赖 `DxfMap.Create<T>()` 来获取属性映射，用于序列化。

- `Table<T>` 基类被所有 9 个表继承，提供统一的集合接口（Add、Remove、TryGetValue 等）。

- `BlockRecord.cs:Entities` 是 `CadObjectCollection<Entity>`，与 `document.Entities`（ModelSpace.Entities）完全相同的结构。

- `Insert.cs:Block` 属性引用 `BlockRecord`，Insert 的 Attributes 集合驱动自 BlockRecord.AttributeDefinitions。

- `DxfMap.cs` 扫描 Entity 和 TableEntry 的 DXF 特性，生成属性到代码的映射，缓存在 ConcurrentDictionary 中。

- `CadObject.cs` 是 Entity、NonGraphicalObject、TableEntry 的共同基类，提供 Handle、Owner、Document、XData、XDictionary 支持。

---

## 数据访问模式总结

| 访问方式 | 代码位置 | 用途 |
|---------|---------|------|
| **遍历所有实体** | `CadDocument.Entities` | 访问模型空间的所有图形对象 |
| **按类型过滤** | `document.Entities.OfType<T>()` | 获取特定类型的实体（如所有 Line） |
| **按属性过滤** | `document.Entities.Where(e => e.Layer.Name == "X")` | 按图层、颜色等属性筛选 |
| **通过 Handle 查询** | `document.TryGetCadObject<T>(handle)` | 快速查找特定对象 |
| **访问表项** | `document.Layers.TryGetValue("name")` | 查询图层、线型等表资源 |
| **遍历表项** | `foreach (var layer in document.Layers)` | 枚举所有表条目 |
| **访问块内实体** | `blockRecord.Entities` | 访问块定义中的实体 |
| **解析继承属性** | `entity.GetActiveColor()` | 自动解析 ByLayer/ByBlock |
| **访问头部变量** | `document.Header.Version/Author` | 获取文档元数据 |
| **序列化为 DXF** | `DxfWriter.Write()` | 导出为 DXF 文件 |
| **序列化为 DWG** | `DwgWriter.Write()` | 导出为 DWG 文件 |
| **手动序列化** | 反射 + JsonSerializer | 导出为 JSON 或其他格式 |

---

**调查完成日期**：2025-12-14
**可用性**：此报告为 LLM 代理检索映射，包含完整的代码位置和 API 访问模式。
