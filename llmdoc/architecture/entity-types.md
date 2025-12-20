# Entity Types System Architecture

## 1. Identity

- **What it is:** Complete type system of CAD entities (144+ types) organized into 9 functional categories, with shared property system and attribute infrastructure.
- **Purpose:** Provides type-safe representation of all supported CAD graphical objects with standardized visual properties (color, layer, linetype, etc.) and DXF serialization.

## 2. Core Components

### Entity Base Infrastructure

- `src/ACadSharp/Entities/Entity.cs` (Entity, IEntity): Base class for all graphical entities with visual properties and transformation methods.
- `src/ACadSharp/Entities/IEntity.cs` (IEntity): Interface defining standard entity properties and methods.
- `src/ACadSharp/Types/ObjectType.cs` (ObjectType): Enum of all entity type codes (TEXT=1, CIRCLE=0x12, LINE=0x13, etc.).

### Basic Geometric Entities (8 types)

- `src/ACadSharp/Entities/Line.cs` (Line): Line segment with StartPoint, EndPoint, Normal, Thickness.
- `src/ACadSharp/Entities/Circle.cs` (Circle): Circle with Center, Radius, Normal, Thickness. Implements ICurve.
- `src/ACadSharp/Entities/Arc.cs` (Arc): Circular arc extending Circle, adding StartAngle, EndAngle, Sweep.
- `src/ACadSharp/Entities/Ellipse.cs` (Ellipse): Ellipse with Center, MajorAxisEndPoint, RadiusRatio, StartParameter, EndParameter. Implements ICurve.
- `src/ACadSharp/Entities/Point.cs` (Point): Point entity with Location, Thickness, Rotation.
- `src/ACadSharp/Entities/Ray.cs` (Ray): Ray with Origin, Direction.
- `src/ACadSharp/Entities/XLine.cs` (XLine): Infinite line with BasePoint, Direction.
- `src/ACadSharp/Entities/Face3D.cs` (Face3D): 3D face with Vertices.

### Polyline Entities (3 types)

- `src/ACadSharp/Entities/PolyLine.cs` (Polyline<T>): Generic polyline base class with Vertices (SeqendCollection<T>), Elevation, StartWidth, EndWidth, Normal, Thickness, PolylineFlags.
  - Derived: PolyLine2D, PolyLine3D
  - Contains Vertex objects (organized as SeqendCollection)
- `src/ACadSharp/Entities/LwPolyline.cs` (LwPolyline): Lightweight polyline (preferred) with Vertices (List<Vertex>), ConstantWidth, Elevation, Normal, Thickness, LwPolylineFlags.
- `src/ACadSharp/Entities/Vertex.cs` (Vertex): Polyline vertex with Location, BulgeAngle, StartWidth, EndWidth, Identifier, VertexFlags.

### Text and Dimension Entities (11 types)

#### Text Entities
- `src/ACadSharp/Entities/TextEntity.cs` (TextEntity): Single-line text with InsertPoint, Height, Rotation, ObliqueAngle, HorizontalAlignment, VerticalAlignment, AlignmentPoint, Style, Normal. Implements IText.
- `src/ACadSharp/Entities/MText.cs` (MText): Multi-line text with InsertPoint, AttachmentPoint, Width, Normal, Height, Rotation, LineSpacingFactor, LineSpacingStyle, Background, Style. Implements IText.
- `src/ACadSharp/Entities/AttributeEntity.cs` (AttributeEntity): Block attribute with Tag, TextString, InsertPoint, TextHeight, Rotation, TextStyle, Alignment, HorizontalAlignment, VerticalAlignment, Color, Layer, etc.

#### Dimension Entities
- `src/ACadSharp/Entities/Dimension.cs` (Dimension): Abstract base class for all dimensions with DefinitionPoint, InsertionPoint, Block reference, DimensionStyle, Flags, HorizontalDirection, AttachmentPoint, FlipArrow1, FlipArrow2, LineSpacingFactor, LineSpacingStyle.
  - `src/ACadSharp/Entities/DimensionLinear.cs`: Linear dimensions with two definition points.
  - `src/ACadSharp/Entities/DimensionAligned.cs`: Aligned dimensions parallel to geometry.
  - `src/ACadSharp/Entities/DimensionAngular2Line.cs`: Angle between two lines.
  - `src/ACadSharp/Entities/DimensionAngular3Pt.cs`: Angle at three points.
  - `src/ACadSharp/Entities/DimensionDiameter.cs`: Circle/arc diameter with leader.
  - `src/ACadSharp/Entities/DimensionRadius.cs`: Circle/arc radius with leader.
  - `src/ACadSharp/Entities/DimensionOrdinate.cs`: Ordinate (coordinate) dimensions.

### Block and Insert Entities (4 types)

- `src/ACadSharp/Entities/Insert.cs` (Insert): Block reference with Block reference, InsertPoint, Rotation, Normal, XScale, YScale, ZScale, RowCount, ColumnCount, RowSpacing, ColumnSpacing, Attributes (SeqendCollection<AttributeEntity>), SpatialFilter. Methods: ApplyTransform(), Clone(), Explode(), UpdateAttributes().
- `src/ACadSharp/Blocks/Block.cs` (Block): Block entity (container marker) with BlockOwner, BasePoint, Name, XRefPath, Comments, Flags (BlockTypeFlags).
- `src/ACadSharp/Blocks/BlockEnd.cs` (BlockEnd): Block end entity (terminator marker).
- `src/ACadSharp/Tables/BlockRecord.cs` (BlockRecord): Block metadata table entry with BlockEntity, BlockEnd, Entities collection, AttributeDefinitions, Layout, Source, EvaluationGraph, SortEntitiesTable. Inherits TableEntry.

### Advanced Entities (8+ types)

#### Curve and Surface
- `src/ACadSharp/Entities/Spline.cs` (Spline): Spline curve with ControlPoints (List<XYZ>), FitPoints (List<XYZ>), Degree, Knots, StartTangent, EndTangent, ControlPointTolerance, FitTolerance, SplineFlags. Implements ICurve.
- `src/ACadSharp/Entities/Solid.cs` (Solid): 3D solid/face with up to 4 vertices.
- `src/ACadSharp/Entities/Trace.cs` (Trace): Trace line (similar to solid but wireframe).

#### Filled Regions
- `src/ACadSharp/Entities/Hatch.cs` (Hatch): Hatch/fill pattern with Paths (List<BoundaryPath>), Pattern (HatchPattern), PatternAngle, PatternScale, Normal, Elevation, IsSolid, IsDouble, IsAssociative, GradientColor, AssociatedObjects.

#### Mesh and Surface
- `src/ACadSharp/Entities/Mesh.cs` (Mesh): Surface mesh with MeshVertices, MSize, NSize, MClosedFlag, NClosedFlag, MLevel, NLevel, SurfaceType.
- `src/ACadSharp/Entities/ModelerGeometry.cs` (ModelerGeometry): 3D solid geometry with SatData (binary representation).
- `src/ACadSharp/Entities/Region.cs` (Region): Region entity with SatData.

#### Special Entities
- `src/ACadSharp/Entities/Viewport.cs` (Viewport): Viewport entity with Center, Width, Height, ViewportID, ViewportStatus, ViewTarget, ViewDirection.
- `src/ACadSharp/Entities/MultiLeader.cs` (MultiLeader): Multi-leader (annotation line) with multiple leader lines and text.
- `src/ACadSharp/Entities/Image.cs` (Image/RasterImage): Raster image with ImageDefinition reference, InsertionPoint, UVector, VVector, ImageWidth, ImageHeight, Transparency, ClipBoundaryVertices.

## 3. Execution Flow (LLM Retrieval Map)

### 3.1 实体属性系统

#### 标准属性管理

- **1. 颜色系统 (Color)**:
  - 支持三种模式: RGB (0-255 per channel)、ACI (索引 0-255)、Book Color (命名颜色)
  - ByLayer: 默认值，继承所属 Layer 的颜色
  - ByBlock: 继承插入块的颜色
  - GetActiveColor(): 自动解析 ByLayer/ByBlock，返回实际颜色
  - 参考: `src/ACadSharp/Entities/Entity.cs:Color`

- **2. 图层系统 (Layer)**:
  - Layer 属性引用 Document.Layers 表中的 Layer 对象
  - 每个实体必须关联一个 Layer（默认"0"层）
  - Layer 控制: Color、LineType、LineWeight、Visibility、PrintFlag
  - 对象添加到文档时自动绑定到表中的 Layer 实例
  - 参考: `src/ACadSharp/Entities/Entity.cs:Layer`

- **3. 线型系统 (LineType)**:
  - LineType 属性引用 Document.LineTypes 表中的 LineType 对象
  - ByLayer: 继承 Layer.LineType
  - ByBlock: 继承块引用的线型
  - 默认值: "Continuous"
  - GetActiveLineType(): 解析实际线型
  - 支持复杂线型 (含线段、间隙、点、形状)
  - 参考: `src/ACadSharp/Entities/Entity.cs:LineType`

- **4. 线宽系统 (LineWeight)**:
  - LineWeightType 枚举值或指定毫米数
  - ByLayer: 继承 Layer.LineWeight
  - ByBlock: 继承块引用的线宽
  - GetActiveLineWeightType(): 解析实际线宽
  - 参考: `src/ACadSharp/Entities/Entity.cs:LineWeight`

- **5. 透明度系统 (Transparency)**:
  - Transparency 对象，值范围 0-255（0=完全透明，255=完全不透明）
  - ByLayer: 可选，继承 Layer 的透明度
  - 参考: `src/ACadSharp/Entities/Entity.cs:Transparency`

- **6. 材质系统 (Material)**:
  - 可选的 Material 对象引用
  - Material 定义表面属性（纹理、反射率等）
  - 参考: `src/ACadSharp/Entities/Entity.cs:Material`

### 3.2 实体类型的创建和使用模式

#### 基本几何实体使用

```
创建: entity = new Line()
初始化几何: entity.StartPoint = new XYZ(0,0,0); entity.EndPoint = new XYZ(10,10,0)
设置属性: entity.Color = Color.FromIndex(1); entity.Layer = layer
添加: document.Entities.Add(entity)
```

#### 多边形实体使用

```
轻量级 (推荐):
lwPolyline = new LwPolyline()
lwPolyline.Vertices.Add(new Vertex(0,0))
lwPolyline.Vertices.Add(new Vertex(10,0))
lwPolyline.Vertices.Add(new Vertex(10,10))

或通用:
polyline = new PolyLine2D()
polyline.Vertices.Add(new Vertex2D(0,0))
// Vertex 对象自动添加到 SeqendCollection，一个 Seqend 实体随之生成
```

#### 块引用使用

```
创建: insert = new Insert()
设置块: insert.Block = document.BlockRecords["BlockName"]
设置位置: insert.InsertPoint = new XYZ(10,10,0)
设置缩放: insert.XScale = 2.0; insert.YScale = 2.0
如果有属性: 更新 insert.Attributes 集合
添加: document.Entities.Add(insert)
```

#### 标注使用

```
创建: dimension = new DimensionLinear()
设置样式: dimension.Style = document.DimensionStyles["Standard"]
设置块: 系统自动创建维度块
设置定义点: dimension.DefinitionPoint = ...
设置文本属性: dimension.AttachmentPoint = DimensionTextAttachmentPoint.MiddleCenter
添加: document.Entities.Add(dimension)
// 维度块自动生成并添加到 document.BlockRecords
```

### 3.3 属性继承链

```
Entity
├─ 直接属性: Color, Layer, LineType, LineWeight, Transparency, Material
│
├─ ByLayer 值时的解析:
│  └─ 查询 Layer.Color / Layer.LineType / Layer.LineWeight / Layer.Transparency
│
├─ ByBlock 值时的解析 (仅在块属性中):
│  └─ 查询 Insert.Color / Insert.LineType 等
│
└─ 实际属性解析:
   └─ GetActiveColor() / GetActiveLineType() / GetActiveLineWeightType()
```

### 3.4 对象类型代码 (ObjectType)

- **1. 枚举映射**: ObjectType 枚举包含所有实体类型代码
  - LINE = 0x13
  - CIRCLE = 0x12
  - ARC = 0x11
  - TEXT = 0x01
  - DIMENSION = 0xA0 (多个子类)
  - 参考: `src/ACadSharp/Types/ObjectType.cs`

- **2. 动态类型查询**:
  ```
  entity.ObjectType  // 返回 ObjectType 值
  entity.ObjectName  // 返回字符串名称 (如 "LINE", "CIRCLE")
  entity.SubclassMarker  // 返回 DXF 子类标记
  ```
  - 参考: `src/ACadSharp/Entities/Entity.cs` 中的属性

### 3.5 DXF 序列化

- **1. 属性映射**: DxfMap 通过反射生成 Entity 属性到 DXF 代码的映射
  - [DxfCodeValue(10, 20, 30)] 标记属性的 DXF 代码
  - 多个代码表示多维属性 (如 XYZ 坐标使用代码 10,20,30)
  - 参考: `src/ACadSharp/DxfMap.cs`

- **2. 子类标记**: 每个实体类有 DXF 子类标记
  - [DxfSubClass("AcDbLine")] 等
  - 参考: `src/ACadSharp/Attributes/DxfSubClassAttribute.cs`

- **3. 序列化流程**:
  ```
  Entity → DxfMap.GetProperties() → DXF 代码值对 → 文件
  ```

## 4. Design Rationale

### 为什么使用分类系统？

实体分成 9 个功能类别提供了清晰的组织：

1. **基本几何**: Line、Circle、Arc、Ellipse 是最基础的构建块
2. **多边形**: Polyline、LwPolyline 用于复杂几何
3. **文本和标注**: TextEntity、MText、Dimension 用于注释
4. **块和插入**: Block、Insert 实现组件化和重用
5. **高级实体**: Spline、Hatch、Mesh 提供高级功能

### 为什么继承 Entity？

所有实体都继承 Entity 以获得：
- 统一的视觉属性系统（Color、Layer 等）
- 一致的生命周期管理（Handle、Owner、Document）
- 标准的变换方法（ApplyTransform 等）
- DXF 映射支持

### 为什么使用 SeqendCollection？

SeqendCollection 为多边形顶点和块属性提供了特殊支持：
- 顶点集合以 Seqend 实体结尾（DXF 格式要求）
- 自动管理 Seqend 的生命周期
- 参考: `src/ACadSharp/SeqendCollection.cs`

---

**Last Updated:** 2025-12-14
**Scope:** 144+ entity types, property systems, type codes, DXF serialization, usage patterns
