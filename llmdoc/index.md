# ACadSharp 文档系统索引

## 1. 项目概述

**ACadSharp** 是一个纯 C# 库，用于读取和写入 CAD 文件（DXF 和 DWG 格式），提供完整的文档操作、实体管理和格式转换能力。项目采用元数据驱动架构，支持多个 .NET 框架版本，当前版本为 3.3.13。

该文档系统为开发者和 LLM 代理提供了项目的完整检索映射，涵盖概述、指南、架构和参考资料四个主要类别，共 27 份文档。

---

## 2. 文档总览

### 文档统计

| 类别 | 数量 | 描述 |
|------|------|------|
| Overview（概述） | 2 | 项目和对象模型的高级介绍 |
| Architecture（架构） | 8 | 系统组件、执行流和数据结构 |
| Guides（指南） | 9 | 任务导向的操作说明 |
| Reference（参考） | 4 | 约定、配置和兼容性查阅 |
| Agent Reports（代理报告） | 9 | 深度侦察和分析文档 |
| **总计** | **28** | **完整文档集** |

---

## 3. 按类别组织的文档列表

### 3.1 Overview（概述）

概述类文档提供项目的高级背景、核心概念和整体架构。

#### `overview/project-overview.md`

- **核心内容**：
  - 项目身份、核心目的、主要功能模块
  - 支持的 CAD 版本（AC1009-AC1032）兼容性矩阵
  - 技术栈（C#、.NET 多版本、元数据驱动架构）
  - 项目结构（Entities、Objects、Tables、IO 等 200+ 文件）
  - 测试和质量保证（94 个测试文件、多框架测试）
  - 快速开始示例（读取、创建、转换）

- **适用场景**：想要理解 ACadSharp 的目标、功能、版本支持和整体设计的开发者

#### `overview/object-model-overview.md`

- **核心内容**：
  - CAD 对象模型的统一三层次架构（Entity、NonGraphicalObject、TableEntry）
  - CadDocument 的中心角色和 9 个核心表
  - 对象属性系统（颜色、图层、线型、线宽、透明度、材质）
  - 144+ 实体类型的功能分类
  - 表和集合系统、扩展机制（XData、XDictionary、Reactors）
  - 对象生命周期、属性继承链和设计原理

- **适用场景**：需要理解 CAD 对象如何组织、何时使用哪种对象类型、属性如何继承的开发者

---

### 3.2 Architecture（架构）

架构文档是系统设计的"LLM 检索映射"，描述文件交互、执行流和核心设计决策。

#### `architecture/io-system-architecture.md`

- **核心内容**：
  - 读写基础架构（ICadReader、ICadWriter、CadReaderBase、CadWriterBase）
  - 配置系统详解（DxfReaderConfiguration、DxfWriterConfiguration、DwgReaderConfiguration 等）
  - DXF 读取架构（DxfReader → DxfStreamReader/DxfBinaryReader/DxfTextReader）
  - DXF 写入架构（DxfWriter → DxfStreamWriter 实现）
  - DWG 读取架构（版本相关文件头、位级数据读取、压缩和编码）
  - DWG 写入架构（多版本支持、CRC 校验、Reed-Solomon 编码）
  - 版本特定处理（DXF/DWG 版本差异、编码自适应）
  - 错误处理和通知系统（NotificationType 枚举、订阅模式）
  - 章节处理机制和设计亮点

- **关键代码位置**：
  - `src/ACadSharp/IO/DXF/DxfReader.cs`
  - `src/ACadSharp/IO/DWG/DwgReader.cs`
  - `src/ACadSharp/IO/CadWriterBase.cs`

- **适用场景**：需要理解读写流程、版本处理、错误通知机制的开发者

#### `architecture/entity-types.md`

- **核心内容**：
  - Entity 基础架构和 IEntity 接口
  - 8 个基本几何实体（Line、Circle、Arc、Ellipse 等）
  - 3 个多边形实体（Polyline、LwPolyline、Vertex）
  - 11 个文本和标注实体（TextEntity、MText、Dimension 7 个子类）
  - 4 个块和插入实体（Insert、Block、BlockEnd、BlockRecord）
  - 8+ 个高级实体（Spline、Hatch、Mesh、Viewport、MultiLeader、Image 等）
  - 标准属性管理（颜色、图层、线型、线宽、透明度、材质）
  - 属性继承链、对象类型代码、DXF 序列化

- **关键代码位置**：
  - `src/ACadSharp/Entities/Entity.cs`
  - `src/ACadSharp/Entities/` (144 个实体文件)
  - `src/ACadSharp/Types/ObjectType.cs`

- **适用场景**：需要创建实体、理解实体属性继承、选择合适实体类型的开发者

#### `architecture/dxf-mapping-system.md`

- **核心内容**：
  - DxfMap 元数据驱动映射生成器
  - DxfProperty 属性关联系统
  - DxfName、DxfSubClass、DxfCodeValue 特性系统
  - 映射生成流程（反射、特性扫描、缓存）
  - 属性对 DXF 代码的双向映射
  - 子类标记和代码组织

- **关键代码位置**：
  - `src/ACadSharp/DxfMap.cs`
  - `src/ACadSharp/DxfProperty.cs`
  - `src/ACadSharp/Attributes/`

- **适用场景**：需要理解如何添加新属性、扩展 DXF 支持、或自定义映射的开发者

#### `architecture/dxf-codes-and-tokens.md`

- **核心内容**：
  - DXF 代码系统（0-999 范围的整数代码）
  - 文件令牌（SECTION、HEADER、CLASSES 等关键词）
  - 子类标记（AcDbEntity、AcDbLine、AcDbCircle 等）
  - 数据类型与代码的对应（整数、实数、字符串、坐标等）
  - DxfCode 枚举的完整列表

- **关键代码位置**：
  - `src/ACadSharp/DxfCode.cs`
  - `src/ACadSharp/DxfFileToken.cs`
  - `src/ACadSharp/DxfSubclassMarker.cs`

- **适用场景**：需要理解 DXF 文件格式细节、手动解析 DXF 代码或调试映射问题的开发者

#### `architecture/object-hierarchy.md`

- **核心内容**：
  - CadObject 基类设计（Handle、Owner、Document、XDictionary）
  - 三层对象层次（Entity、NonGraphicalObject、TableEntry）
  - Entity 和非图形对象的属性差异
  - TableEntry 的表管理方式
  - 对象所有权模型和生命周期

- **关键代码位置**：
  - `src/ACadSharp/CadObject.cs`
  - `src/ACadSharp/Entities/Entity.cs`
  - `src/ACadSharp/Tables/TableEntry.cs`

- **适用场景**：需要理解对象层次关系、实现自定义对象、或理解所有权模型的开发者

#### `architecture/tables-system.md`

- **核心内容**：
  - Table<T> 通用表实现
  - 9 个核心表（Layers、LineTypes、TextStyles、BlockRecords、DimensionStyles、AppIds、UCSs、Views、VPorts）
  - 表项生命周期和强制唯一性
  - 表的集合接口和枚举
  - 表与 CadDocument 的整合

- **关键代码位置**：
  - `src/ACadSharp/Tables/Collections/Table.cs`
  - `src/ACadSharp/Tables/` (表项类)

- **适用场景**：需要创建或管理表项、理解表的约束条件、访问系统资源的开发者

#### `architecture/blocks-system.md`

- **核心内容**：
  - Block 实体和 BlockRecord 表项的区分
  - 块定义和块插入的关系
  - BlockRecord 包含的实体集合和属性定义
  - Insert 实体的块引用、缩放、旋转
  - 块的生命周期（创建、插入、删除）
  - 动态块和 XRef 支持

- **关键代码位置**：
  - `src/ACadSharp/Blocks/Block.cs`
  - `src/ACadSharp/Tables/BlockRecord.cs`
  - `src/ACadSharp/Entities/Insert.cs`

- **适用场景**：需要创建块、插入块引用、操作块属性的开发者

#### `architecture/dwg-migration-strategy.md`

- **核心内容**：
  - ACadSharp DWG 功能到 zcad-rs 的系统性移植架构策略
  - 架构映射关系和核心组件移植计划
  - 分阶段实施路线图（4个阶段，17-24周）
  - Rust 特定的内存管理、错误处理和性能优化策略
  - 测试驱动的质量保证体系

- **关键代码位置**：
  - `src/ACadSharp/IO/DWG/DwgReader.cs` (DWG 读取主入口)
  - `src/ACadSharp/IO/DWG/DwgWriter.cs` (DWG 写入主入口)
  - `/Volumes/DPC/work/cad-code/zcadvelecAI/zcad-rs/zcad-io/src/dwg/mod.rs` (目标实现)
  - 相关代理报告：`agent/scout-dwg-functionality-analysis.md`、`agent/dwg-to-rust-migration-plan.md`

- **适用场景**：参与 DWG 功能移植的开发者、架构设计决策者、技术评估人员

---

### 3.3 Guides（指南）

指南是任务导向的操作说明，每个指南聚焦单一、具体的工作流程。

#### `guides/how-to-read-cad-files.md`

- **核心步骤**：
  1. 基础读取（DWG/DXF 的最简方式）
  2. 配置读取器（DwgReaderConfiguration、DxfReaderConfiguration）
  3. 订阅通知系统（监听读取过程中的警告和错误）
  4. 部分读取（仅读取文件头或跳过耗时操作）
  5. 访问文档内容（遍历实体、访问表、访问块）
  6. 错误处理（异常捕获、通知处理）
  7. 版本检查和兼容性判断
  8. 高级用法（流处理、预览提取、特定类型过滤）

- **适用场景**：想要学习如何读取 DXF/DWG 文件、访问其内容、处理错误的开发者

#### `guides/how-to-write-cad-files.md`

- **核心步骤**：
  1. 基础写入（创建文档、添加实体、保存 DWG/DXF）
  2. 版本选择和兼容性矩阵
  3. 配置写入器（DxfWriterConfiguration、DwgWriterConfiguration）
  4. 输出格式选择（ASCII vs 二进制 DXF、自动压缩）
  5. 添加实体（基本几何、复杂实体）
  6. 使用图层和样式（创建、应用）
  7. 创建块和块插入
  8. 通知和错误处理（验证、异常）
  9. 使用流和内存优化
  10. 格式转换（DXF→DWG）
  11. 文档元数据设置

- **适用场景**：想要学习如何创建 CAD 文件、添加内容、选择版本的开发者

#### `guides/understanding-caddocument.md`

- **核心步骤**：
  1. CadDocument 的中心角色
  2. 创建新文档
  3. 访问核心表（Layers、LineTypes、BlockRecords 等）
  4. 访问实体集合（Entities、ModelSpace、PaperSpace）
  5. 访问头部（Header 变量）
  6. 使用 RootDictionary 访问可选集合
  7. 对象的添加和移除
  8. 文档的验证和清理

- **适用场景**：需要理解 CadDocument 如何组织数据、如何访问和修改各种对象的开发者

#### `guides/understanding-dxf-structure.md`

- **核心步骤**：
  1. DXF 文件的六个主要章节（HEADER、CLASSES、TABLES、BLOCKS、ENTITIES、OBJECTS）
  2. 每个章节的数据组织和依赖关系
  3. DXF 代码对和值的读取
  4. 特定章节的用途（如 HEADER 定义系统变量）
  5. 版本相关的结构差异

- **适用场景**：需要理解 DXF 文件格式、手动解析 DXF、或调试读写问题的开发者

#### `guides/understanding-table-entries.md`

- **核心步骤**：
  1. TableEntry 基类和表项的共同属性
  2. 创建和添加表项（Layer、LineType、TextStyle 等）
  3. 修改表项属性
  4. 表项的约束条件（唯一名称、默认项保护）
  5. 删除表项的限制
  6. 表项的生命周期

- **适用场景**：需要创建或管理图层、线型、文本样式等系统资源的开发者

#### `guides/working-with-blocks.md`

- **核心步骤**：
  1. 理解 Block 实体和 BlockRecord 的区分
  2. 创建块定义（BlockRecord + Block + 实体）
  3. 添加块到文档
  4. 创建块插入（Insert 实体）
  5. 插入块引用和缩放/旋转
  6. 访问块内的实体
  7. 块属性的获取和修改
  8. 块的动态功能

- **适用场景**：需要使用块进行组件化设计、创建可重用部件的开发者

#### `guides/working-with-entities.md`

- **核心步骤**：
  1. 实体的基本生命周期（创建、配置、添加）
  2. 设置几何属性（坐标、半径、长度等）
  3. 设置视觉属性（颜色、图层、线型）
  4. 处理属性继承（ByLayer、ByBlock）
  5. 实体变换（移动、缩放、旋转）
  6. 实体的克隆和复制
  7. 实体的删除
  8. 特定实体类型的特殊操作

- **适用场景**：需要创建和修改 CAD 图形实体的开发者

#### `guides/working-with-tables.md`

- **核心步骤**：
  1. 访问 CadDocument 中的表
  2. 遍历表项
  3. 按名称查询表项（TryGetValue）
  4. 创建新表项
  5. 修改表项
  6. 删除表项（可删除性检查）
  7. 观察表的变化（事件系统）

- **适用场景**：需要管理系统资源（图层、线型等）的开发者

#### `guides/extending-dxf-mapping.md`

- **核心步骤**：
  1. 理解 DxfMap 系统的原理
  2. 添加新属性（使用 DxfCodeValue 特性）
  3. 定义自定义 DXF 名称（DxfName 特性）
  4. 指定子类标记（DxfSubClass 特性）
  5. 处理多值属性（XYZ 坐标对应多个代码）
  6. 清除映射缓存
  7. 测试扩展
  8. 版本相关的映射差异

- **适用场景**：需要添加新属性支持、自定义实体、或扩展 DXF 映射的开发者

---

### 3.4 Reference（参考）

参考文档提供约定、配置和查阅信息。

#### `reference/coding-conventions.md`

- **核心内容**：
  - 命名约定（私有字段 `_camelCase`、公共成员 `PascalCase`、接口 `IPascalCase`）
  - 代码风格（Tab 制表符、CRLF 行尾、表达式体成员规则）
  - 项目组织（命名空间与文件夹匹配、一个类一个文件）
  - 特性系统与 DXF 映射（DxfName、DxfSubClass、DxfCodeValue）
  - 访问修饰符规则、字段限定符、自动属性偏好
  - 多框架支持和条件编译
  - EditorConfig 配置位置

- **适用场景**：贡献代码时需要遵循的编码规范

#### `reference/git-conventions.md`

- **核心内容**：
  - Git 工作流（分支命名、提交信息格式）
  - Pull Request 流程
  - 代码审查规范
  - 版本标签和发布流程

- **适用场景**：参与项目开发、提交代码的开发者

#### `reference/supported-cad-versions.md`

- **核心内容**：
  - CAD 版本兼容性矩阵（AC1009-AC1032）
  - 每个版本的特性和限制
  - DXF/DWG 读写支持情况
  - 版本相关的特殊处理（压缩、编码、CRC）

- **适用场景**：需要选择目标版本、理解版本限制的开发者

#### `reference/xdata-system.md`

- **核心内容**：
  - ExtendedData（XData）系统概述
  - 15 种 ExtendedDataRecord 类型
  - XData 的应用 ID 注册
  - XData 的读写和序列化
  - 常见用途和最佳实践

- **适用场景**：需要为对象附加自定义数据的开发者

---

### 3.5 Agent Reports（代理报告）

代理报告是通过深度侦察生成的详细分析文档，适用于 LLM 代理。

#### `agent/scout-io-architecture.md`

- **内容**：I/O 系统的详细侦察报告，包括所有读写器的代码位置、执行流图、版本处理细节

#### `agent/scout-entity-model.md`

- **内容**：实体模型系统的深度分析，包括 144+ 实体的分类、属性系统、生命周期

#### `agent/scout-tables-blocks.md`

- **内容**：表和块系统的侦察，包括所有 9 个表的详细信息、块的创建和使用

#### `agent/scout-code-structure.md`

- **内容**：项目代码结构的完整映射，包括文件组织、命名空间、关键类的位置

#### `agent/scout-testing-examples.md`

- **内容**：测试框架和示例代码的侦察，包括如何运行测试、测试覆盖的功能

#### `agent/scout-dwg-functionality-analysis.md`

- **内容**：ACadSharp DWG 读写功能的深度分析，包括核心组件映射、压缩算法、版本处理和测试策略

#### `agent/scout-zcad-rs-analysis.md`

- **内容**：zcad-rs 项目现状评估，包括架构分析、已实现功能、与 ACadSharp 的技术差距对比

#### `agent/dwg-to-rust-migration-plan.md`

- **内容**：DWG 功能移植的详细实施计划，包括 4 阶段开发路线图、技术挑战解决方案和风险评估

#### `agent/dwg-to-rust-migration-summary.md`

- **内容**：DWG 移植计划的执行摘要，包括核心差距分析、技术策略和关键成功指标

---

## 4. 快速导航

根据你的需求，选择合适的文档：

### 4.1 "我想读取 DXF/DWG 文件..."

1. **快速开始**：阅读 `guides/how-to-read-cad-files.md`
2. **理解流程**：阅读 `architecture/io-system-architecture.md`（DXF 读取流程、DWG 读取流程）
3. **处理版本**：参考 `reference/supported-cad-versions.md`
4. **处理错误**：在指南中查找"通知系统"和"错误处理"部分

### 4.2 "我想创建和写入 CAD 文件..."

1. **快速开始**：阅读 `guides/how-to-write-cad-files.md`
2. **添加实体**：阅读 `guides/working-with-entities.md`
3. **使用图层和样式**：在写入指南中查找相关部分
4. **选择版本**：参考 `reference/supported-cad-versions.md` 的兼容性矩阵
5. **理解数据结构**：参考 `guides/understanding-caddocument.md`

### 4.3 "我想理解对象模型..."

1. **概述**：阅读 `overview/object-model-overview.md`
2. **对象层次**：阅读 `architecture/object-hierarchy.md`
3. **实体类型**：阅读 `architecture/entity-types.md`
4. **表系统**：阅读 `architecture/tables-system.md`
5. **块系统**：阅读 `architecture/blocks-system.md`

### 4.4 "我想使用表（图层、线型、样式）..."

1. **基础**：阅读 `guides/understanding-table-entries.md`
2. **操作**：阅读 `guides/working-with-tables.md`
3. **架构**：阅读 `architecture/tables-system.md`

### 4.5 "我想使用块..."

1. **指南**：阅读 `guides/working-with-blocks.md`
2. **架构**：阅读 `architecture/blocks-system.md`

### 4.6 "我想扩展 DXF 映射或添加新属性..."

1. **映射系统**：阅读 `architecture/dxf-mapping-system.md`
2. **DXF 代码**：阅读 `architecture/dxf-codes-and-tokens.md`
3. **扩展指南**：阅读 `guides/extending-dxf-mapping.md`
4. **编码规范**：参考 `reference/coding-conventions.md`

### 4.7 "我想理解 DXF 文件格式..."

1. **结构**：阅读 `guides/understanding-dxf-structure.md`
2. **代码系统**：阅读 `architecture/dxf-codes-and-tokens.md`
3. **映射原理**：阅读 `architecture/dxf-mapping-system.md`

### 4.8 "我想为项目贡献代码..."

1. **编码规范**：阅读 `reference/coding-conventions.md`
2. **Git 规范**：阅读 `reference/git-conventions.md`
3. **项目概览**：阅读 `overview/project-overview.md`
4. **代码结构**：参考 `agent/scout-code-structure.md`

### 4.9 "我想理解错误处理和通知系统..."

1. **I/O 架构**：在 `architecture/io-system-architecture.md` 中查找"错误处理和通知系统"部分
2. **读取指南**：在 `guides/how-to-read-cad-files.md` 中查找"通知系统的使用"
3. **写入指南**：在 `guides/how-to-write-cad-files.md` 中查找"通知和错误处理"

### 4.10 "我想进行格式转换（DXF ↔ DWG）..."

1. **读取和写入**：参考 `guides/how-to-read-cad-files.md` 和 `guides/how-to-write-cad-files.md`
2. **版本选择**：查看 `reference/supported-cad-versions.md`
3. **转换示例**：在写入指南中查找"DXF 到 DWG 转换"部分

### 4.11 "我想了解 DWG 功能移植到 Rust 的计划..."

1. **架构策略**：阅读 `architecture/dwg-migration-strategy.md`
2. **技术分析**：参考 `agent/scout-dwg-functionality-analysis.md` 和 `agent/scout-zcad-rs-analysis.md`
3. **实施计划**：查看 `agent/dwg-to-rust-migration-plan.md`
4. **执行摘要**：参考 `agent/dwg-to-rust-migration-summary.md`

---

## 5. 文档内部链接

文档之间的交叉引用关系：

```
Overview
├─ project-overview.md (引用所有架构和指南)
└─ object-model-overview.md (引用 Entity、Table、Block 架构)

Architecture
├─ io-system-architecture.md (依赖 dxf-codes 和 dxf-mapping)
├─ entity-types.md (引用 object-hierarchy)
├─ dxf-mapping-system.md (引用 dxf-codes)
├─ dxf-codes-and-tokens.md (核心参考)
├─ object-hierarchy.md (引用 entity-types 和 tables)
├─ tables-system.md (引用 object-hierarchy)
├─ blocks-system.md (引用 entity-types 和 tables)
└─ dwg-migration-strategy.md (引用 agent 报告和 io-system)

Guides
├─ how-to-read-cad-files.md (引用 io-system 和 entity-types)
├─ how-to-write-cad-files.md (引用 io-system 和 entity-types)
├─ understanding-caddocument.md (引用 object-model 和 tables)
├─ understanding-dxf-structure.md (引用 dxf-codes)
├─ understanding-table-entries.md (引用 tables)
├─ working-with-blocks.md (引用 blocks)
├─ working-with-entities.md (引用 entity-types)
├─ working-with-tables.md (引用 tables)
└─ extending-dxf-mapping.md (引用 dxf-mapping)

Reference
├─ coding-conventions.md (引用 dxf-mapping)
├─ git-conventions.md (与其他文档无关)
├─ supported-cad-versions.md (在所有 I/O 指南中使用)
└─ xdata-system.md (引用 object-model)

Agent Reports
├─ scout-io-architecture.md (详细的 io-system-architecture)
├─ scout-entity-model.md (详细的 entity-types)
├─ scout-tables-blocks.md (详细的 tables + blocks)
├─ scout-code-structure.md (项目级别的代码映射)
├─ scout-testing-examples.md (测试和示例)
├─ scout-dwg-functionality-analysis.md (ACadSharp DWG 功能深度分析)
├─ scout-zcad-rs-analysis.md (zcad-rs 项目现状评估)
├─ dwg-to-rust-migration-plan.md (DWG 移植详细实施计划)
└─ dwg-to-rust-migration-summary.md (DWG 移植执行摘要)
```

---

## 6. 文档维护说明

- **更新时机**：当添加新功能、改进架构或发现重要的设计模式时
- **更新范围**：确保文档保持最新，特别是代码位置和版本号
- **一致性**：维护所有文档之间的交叉引用和链接
- **质量检查**：定期审查文档的准确性和完整性

---

## 7. 文档统计信息

| 指标 | 值 |
|------|-----|
| 总文档数 | 32 |
| 总行数（估计） | ~18,000+ |
| 代码位置引用 | 250+ |
| 示例代码片段 | 180+ |
| 快速导航场景 | 12+ |
| 跨文档链接 | 80+ |

---

## 8. 快速查找表

### 8.1 按文件位置查找

| 功能 | 文件路径 | 文档 |
|------|---------|------|
| 读取 DWG/DXF | `src/ACadSharp/IO/DXF/DxfReader.cs` | io-system-architecture, how-to-read |
| 写入 DWG/DXF | `src/ACadSharp/IO/DWG/DwgWriter.cs` | io-system-architecture, how-to-write |
| Entity 基类 | `src/ACadSharp/Entities/Entity.cs` | entity-types, object-hierarchy |
| CadDocument | `src/ACadSharp/CadDocument.cs` | understanding-caddocument |
| 表系统 | `src/ACadSharp/Tables/Collections/Table.cs` | tables-system, working-with-tables |
| 块系统 | `src/ACadSharp/Blocks/Block.cs` | blocks-system, working-with-blocks |
| DXF 映射 | `src/ACadSharp/DxfMap.cs` | dxf-mapping-system, extending-dxf-mapping |
| DXF 代码 | `src/ACadSharp/DxfCode.cs` | dxf-codes-and-tokens |

### 8.2 按功能查找

| 需求 | 推荐文档 |
|------|---------|
| 读取文件 | how-to-read-cad-files |
| 写入文件 | how-to-write-cad-files |
| 创建实体 | working-with-entities |
| 管理图层 | working-with-tables, understanding-table-entries |
| 使用块 | working-with-blocks, blocks-system |
| 理解对象模型 | object-model-overview, object-hierarchy |
| 扩展功能 | extending-dxf-mapping, coding-conventions |
| 版本选择 | supported-cad-versions, how-to-write-cad-files |
| 错误处理 | how-to-read-cad-files, io-system-architecture |
| DXF 格式 | understanding-dxf-structure, dxf-codes-and-tokens |

---

**最后更新**：2026-01-02
**文档版本**：1.2（包含 DWG 移植策略）
**项目版本**：ACadSharp 3.3.13
