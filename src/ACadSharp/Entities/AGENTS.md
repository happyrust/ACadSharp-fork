# ACadSharp 实体系统 (Entities)

## OVERVIEW
提供 144+ 种 CAD 图形实体的类型安全实现，通过特性驱动的元数据映射支持 DXF/DWG 读写。

## WHERE TO LOOK
| 类别 | 关键文件/模式 | 说明 |
|------|--------------|------|
| **核心基类** | `Entity.cs` | 所有图形实体的抽象基类，管理颜色、图层等通用属性 |
| **基础几何** | `Line.cs`, `Circle.cs`, `Arc.cs` | 核心 2D/3D 几何图形实现，依赖 `CSMath.XYZ` |
| **多段线家族** | `LwPolyline.cs`, `Polyline2D.cs` | 包含轻量级和传统多段线，涉及复杂的顶点管理 |
| **复杂填充** | `Hatch.cs`, `Hatch.*.cs` | 填充实体，通过 partial class 拆分边界路径和图案定义 |
| **注解系统** | `TextEntity.cs`, `MText.cs` | 单行和多行文本，支持样式引用和富文本属性 |
| **标注系统** | `Dimension.cs`, `Dimension*.cs` | 对齐、径向、角度等 7 种标注，继承自 `Dimension` |
| **智能引线** | `MultiLeader.cs`, `Leader.cs` | 包含样式覆盖、多重引线和块属性嵌入 |
| **块与引用** | `Insert.cs`, `Block.cs` | 处理块定义 (BlockRecord) 与块插入之间的引用关系 |
| **表格实体** | `TableEntity.cs`, `TableEntity.*.cs` | 极其复杂的表格实现，涉及单元格、行、列的递归组织 |

## CONVENTIONS
- **特性驱动映射**: 类级必须标注 `[DxfName]`。继承链中的 `[DxfSubClass]` 会按顺序累加生成 DXF 映射。
- **精确组码关联**: 属性通过 `[DxfCodeValue]` 映射组码。注意 10/20/30 (XYZ) 和 11/21/31 (方向) 的区分。
- **严格成员访问**: 必须显式使用 `this.` 限定符。私有字段使用 `_camelCase`，公共成员使用 `PascalCase`。
- **引用一致性**: 在 Setter 中修改 `Layer` 或 `LineType` 等引用时，必须调用 `updateCollection` 确保对象已入库。
- **嵌套类组织**: 复杂的内部数据结构（如 Hatch 路径）应定义为嵌套类，并放入对应的 `.Component.cs` 文件。
- **属性延迟计算**: 诸如 `GetActiveColor()` 这种方法应用于解析 `ByLayer` 和 `ByBlock` 的最终视觉状态。

## ANTI-PATTERNS
- **隐式访问控制**: 严禁省略 `public`/`private`/`protected` 修饰符，必须代码显式化。
- **魔术字符串**: 严禁在代码中直接写 "AcDbEntity" 等字符串，必须引用 `DxfSubclassMarker` 常量。
- **零缩放因子**: `Insert` 实体的缩放系数严禁为 0，否则会导致 CAD 软件渲染崩溃或文件损坏。
- **非法跨文档引用**: 严禁将属于 A 文档的图层或块记录直接赋值给 B 文档的实体。
- **空值不一致**: 禁止为 `Layer` 或 `LineType` 等核心属性设置 `null`，必须始终有默认值（如 `Layer.Default`）。
