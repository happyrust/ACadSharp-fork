# CAD 对象层次结构

## 1. Identity

- **What it is:** The root object hierarchy that defines all CAD objects in ACadSharp, organized into three primary branches (Entity, NonGraphicalObject, TableEntry) descending from CadObject.
- **Purpose:** Provides a unified type system and lifecycle management for all CAD objects, including graphical entities, non-graphical objects, and table entries.

## 2. Core Components

- `src/ACadSharp/CadObject.cs` (CadObject, IHandledCadObject): Abstract base class for all CAD objects, defining Handle (unique identifier), Document reference, Owner, Reactors, XDictionary, and ExtendedData properties.
- `src/ACadSharp/Entities/Entity.cs` (Entity, IEntity): Base class for all graphical entities, adding visual properties (Color, Layer, LineType, LineWeight, Transparency, Material).
- `src/ACadSharp/Objects/NonGraphicalObject.cs` (NonGraphicalObject, INamedCadObject): Base class for non-graphical objects, implementing named object interface.
- `src/ACadSharp/Tables/TableEntry.cs` (TableEntry, INamedCadObject): Base class for all table entries, defining named entries in CAD tables.
- `src/ACadSharp/Tables/Collections/Table.cs` (Table<T>): Generic table collection implementing ICadCollection<T> and IObservableCadCollection<T>.

## 3. Execution Flow (LLM Retrieval Map)

### 3.1 CadObject 基础架构

CadObject 是所有对象的根类，定义了对象的核心生命周期和关系管理：

- **1. 对象创建**: CadObject 实例化，初始化为独立对象（Document=null, Owner=null, Handle=0）
  - 参考: `src/ACadSharp/CadObject.cs`（构造器）

- **2. 文档绑定**: 对象添加到集合时触发 `AssignDocument(CadDocument)` 方法
  - 自动分配 Handle（通过 Document.GetNextHandle()）
  - 更新 Document 和 Owner 引用
  - 注册到文档的对象查询系统

- **3. 所有权管理**: 每个对象最多有一个 Owner
  - Owner 可以是 CadDocument、BlockRecord、其他容器对象或集合
  - 添加到新集合时自动从旧 Owner 移除
  - 维护 `CadObjectCollection<T>` 的完整性约束

- **4. 反应器和依赖**: Reactors 属性存储依赖于此对象的其他对象列表
  - 参考: `src/ACadSharp/CadObject.cs:Reactors`
  - 用于追踪对象间的依赖关系

- **5. 扩展数据**: ExtendedDataDictionary 允许附加自定义数据
  - 按 AppId 分组存储 ExtendedData
  - 参考: `src/ACadSharp/XData/ExtendedDataDictionary.cs`

### 3.2 三大派生类型系统

#### Entity（实体）- 图形对象

Entity 继承 CadObject，添加视觉属性和几何变换能力：

- **1. 视觉属性初始化**: Entity 定义标准属性
  - Color: 支持 ByLayer、ByBlock、RGB、ACI 值
  - Layer: 引用 LayersTable 中的 Layer 对象
  - LineType: 引用 LineTypesTable 中的 LineType 对象
  - LineWeight: LineWeightType 枚举值（ByLayer、ByBlock、指定值）
  - Transparency: Transparency 对象（ByLayer 或指定值）
  - Material: 可选的材质对象引用
  - 参考: `src/ACadSharp/Entities/Entity.cs:1-100`

- **2. 属性解析**: GetActive* 方法自动解析 ByLayer/ByBlock 属性
  - GetActiveColor(): 返回实际颜色，如果是 ByLayer 则从 Layer.Color 查询
  - GetActiveLineType(): 返回实际线型，支持层级继承
  - GetActiveLineWeightType(): 返回实际线宽
  - 参考: `src/ACadSharp/Entities/Entity.cs:200-250`

- **3. 几何变换**: Entity 支持变换操作
  - ApplyTransform(Matrix3): 应用矩阵变换
  - ApplyRotation(point, angle): 旋转
  - ApplyScaling(point, scale): 缩放
  - ApplyTranslation(offset): 平移
  - 参考: `src/ACadSharp/Entities/Entity.cs` 中的变换方法

- **4. 属性匹配**: MatchProperties(Entity) 复制另一实体的属性
  - 参考: `src/ACadSharp/Entities/Entity.cs`

- **5. 边界框计算**: GetBoundingBox() 计算几何边界
  - 虚拟方法，由各具体实体实现

#### NonGraphicalObject（非图形对象）

NonGraphicalObject 继承 CadObject，代表配置和元数据对象：

- **1. 命名对象**: 实现 INamedCadObject 接口
  - 必须有唯一 Name 属性
  - 参考: `src/ACadSharp/Objects/NonGraphicalObject.cs`

- **2. 特殊对象类型**: CadDictionary、Group、Layout、ImageDefinition、Material、VisualStyle
  - 存储在 CadDocument.RootDictionary 或其他特殊集合
  - 参考: `src/ACadSharp/Objects/` 目录

#### TableEntry（表条目）

TableEntry 继承 CadObject，代表表中的项目：

- **1. 表注册**: 必须属于对应的表集合
  - Layer 属于 LayersTable
  - LineType 属于 LineTypesTable
  - BlockRecord 属于 BlockRecordsTable
  - 等等
  - 参考: `src/ACadSharp/Tables/TableEntry.cs`

- **2. 命名约束**: 实现 INamedCadObject，Name 必须唯一
  - 表自动维护 Name → TableEntry 的字典映射
  - 名称变更时自动更新映射

- **3. 默认条目**: 各表都有受保护的默认条目
  - LayersTable: ["0"]
  - LineTypesTable: ["Continuous", "ByBlock", "ByLayer"]
  - BlockRecordsTable: ["*Model_Space", "*Paper_Space"]
  - 参考: `src/ACadSharp/Tables/Collections/` 中各表的实现

### 3.3 接口系统

接口定义对象的能力，支持多层继承和多态：

- **IHandledCadObject**: 定义 Handle 属性（所有 CadObject 都实现）
  - 参考: `src/ACadSharp/IHandledCadObject.cs`

- **INamedCadObject**: 定义 Name 属性和 OnNameChanged 事件
  - 由 NonGraphicalObject、TableEntry 实现
  - 参考: `src/ACadSharp/INamedCadObject.cs`

- **IEntity**: 定义实体的标准属性和方法
  - Color、Layer、LineType、LineWeight、Transparency、Material
  - GetActiveColor()、GetActiveLineType() 等方法
  - 参考: `src/ACadSharp/Entities/IEntity.cs`

- **ICurve**: 定义曲线接口，支持参数化
  - 由 Circle、Arc、Ellipse、Spline 等实现
  - GetPointAt(parameter)、GetParameterAtPoint(point) 等方法

- **IText**: 定义文本属性接口
  - 由 TextEntity、MText 实现

### 3.4 对象生命周期

典型的对象生命周期流程：

- **1. 创建**: 使用 `new` 操作符创建实体或对象
  ```
  Entity entity = new Line();
  ```

- **2. 初始化属性**: 在添加到文档前设置属性
  ```
  entity.Color = Color.FromIndex(5);
  entity.Layer = /* layer reference */;
  ```

- **3. 添加到集合**:
  ```
  document.Entities.Add(entity);  // 或 blockRecord.Entities.Add(entity)
  ```

- **4. 文档绑定**: `AssignDocument()` 自动调用，Handle 分配完成
  - 此后 entity.Document != null，entity.Handle > 0

- **5. 修改和查询**: 对象可通过各种方式查询和修改
  - document.GetCadObject(handle)
  - entity.GetActiveColor() 等属性查询

- **6. 移除**: 从集合移除
  ```
  document.Entities.Remove(entity);
  ```

- **7. 文档解绑**: `UnassignDocument()` 自动调用

### 3.5 所有权和关联管理

- **单一所有权原则**: 每个对象最多有一个直接所有者
  - 参考: `src/ACadSharp/CadObjectCollection.cs:Add()` 实现强制唯一所有权

- **集合类型**:
  - CadObjectCollection<T>: 通用集合，维护对象的所有权转移
  - SeqendCollection<T>: 特殊集合，以 Seqend 实体结尾（多边形顶点、块属性）
  - Table<T>: 命名表集合，强制唯一命名（大小写不敏感）

- **跨引用管理**:
  - Entity.Layer 引用 Document.Layers 表中的 Layer
  - Entity.LineType 引用 Document.LineTypes 表中的 LineType
  - Insert.Block 引用 Document.BlockRecords 表中的 BlockRecord
  - 参考: `src/ACadSharp/CadDocument.cs` 中的表属性

## 4. Design Rationale

### 为什么三分法设计？

CadObject → {Entity, NonGraphicalObject, TableEntry} 的三分法设计反映了 CAD 文件格式的自然分层：

1. **Entity**: 代表可视化的图形对象（直线、圆、文本等）
   - 具有空间位置和几何属性
   - 可应用视觉属性（颜色、线型等）
   - 支持几何变换

2. **NonGraphicalObject**: 代表支持数据结构（字典、组、布局等）
   - 无空间位置或几何
   - 支持命名和扩展数据
   - 存储配置和元数据

3. **TableEntry**: 代表系统资源（图层、线型、样式等）
   - 必须唯一命名
   - 由系统表管理
   - 被其他对象引用

### 为什么使用接口系统？

接口提供了灵活的能力定义，避免深层继承：

- IEntity 定义所有实体的标准属性，不需要所有对象都有这些属性
- ICurve 定义曲线能力，只有参数化曲线实现它
- INamedCadObject 用于可命名对象，Entity 不一定需要它

### 为什么强制单一所有权？

单一所有权原则确保：
- 对象引用的完整性（不会有循环或多重所有权）
- 自动垃圾回收（对象被移除时可被清理）
- 一致的文档状态（不会有孤立对象）
- 参考: `src/ACadSharp/CadObjectCollection.cs` 中的实现

---

**Last Updated:** 2025-12-14
**Scope:** ACadSharp CadObject hierarchy, Entity/NonGraphicalObject/TableEntry branches, interfaces, lifecycle, and ownership model
