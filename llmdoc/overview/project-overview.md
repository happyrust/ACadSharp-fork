# ACadSharp 项目概览

## 1. 项目身份

**ACadSharp** 是一个纯 C# 库，用于读取和写入 CAD 文件（DXF 和 DWG 格式），提供完整的文档操作、实体管理和格式转换能力。项目采用元数据驱动架构，支持多个 .NET 框架版本，当前版本为 3.3.13。

## 2. 核心目的

- **读写 CAD 文件**: 支持 DXF（ASCII/二进制）和 DWG（多版本）格式的完整读写
- **文档操作**: 创建、修改、查询和转换 CAD 文档
- **实体管理**: 支持 144+ 种 CAD 实体类型（几何、文本、标注、高级实体等）
- **表和块管理**: 管理图层、线型、样式、块记录等数据组织结构
- **扩展数据**: 通过 XData 为任何对象附加自定义数据
- **格式转换**: 支持 DXF/DWG 互相转换及 SVG 导出

## 3. 主要功能模块

### 3.1 读写功能
- **DXF 读取**: DxfReader 支持 ASCII 和二进制格式（包括 AC1009 特殊格式）
- **DXF 写入**: DxfWriter 支持二进制和 ASCII 输出
- **DWG 读取**: DwgReader 支持多版本（AC1012-AC1032）
- **DWG 写入**: DwgWriter 支持多数版本的写入（AC1021 除外）
- **通知系统**: 内置事件驱动的错误和警告通知机制

### 3.2 对象模型
- **CadObject 层次**: CadObject → {Entity, NonGraphicalObject, TableEntry}
- **实体系统**: 144+ 种实体包括直线、圆、弧、样条、多边形、文本、标注、填充等
- **表管理**: 9 个主要表（AppIds, BlockRecords, Layers, LineTypes, TextStyles, DimensionStyles, UCSs, Views, VPorts）
- **集合系统**: 通过 RootDictionary 管理块、组、布局、材质、线型样式等

### 3.3 属性系统
- **视觉属性**: 颜色、图层、线型、线宽、透明度、材质管理
- **属性继承**: 支持 ByLayer/ByBlock 属性解析
- **扩展数据**: 15 种 ExtendedDataRecord 类型支持任意数据附加

### 3.4 块和插入
- **块定义**: BlockRecord 代表块元数据，Block/BlockEnd 代表物理结构
- **块插入**: Insert 实体支持单个和数组插入
- **块属性**: 支持属性定义和块属性管理
- **XRef 支持**: 外部参考管理
- **动态块**: 通过 EvaluationGraph 和 XData 支持动态块

## 4. 技术栈

### 4.1 语言和框架
- **语言**: C# (.NET 多版本支持)
- **框架支持**: .NET 5.0, 6.0, 7.0, 8.0, 9.0, .NET Framework 4.8, netstandard 2.0/2.1
- **依赖**: CSMath (向量、矩阵、几何运算)、CSUtilities (工具函数)

### 4.2 核心设计模式
- **元数据驱动**: 通过 DxfName/DxfSubClass/DxfCodeValue 特性自动生成 DXF 映射
- **工厂模式**: DwgFileHeader.CreateFileHeader()、DxfMap.Create() 等版本相关工厂
- **模板模式**: 74+ 个 ICadTemplate 实现支持类型化序列化
- **事件驱动**: NotificationEventHandler 通知系统贯穿 I/O 过程
- **可观察集合**: IObservableCadCollection 支持集合变化监听

### 4.3 文件格式规范
- **DXF 章节结构**: HEADER → CLASSES → TABLES → BLOCKS → ENTITIES → OBJECTS
- **DWG 章节结构**: Header(文件头) → SUMMARY → HEADER → CLASSES → HANDLES → Objects
- **版本差异处理**:
  - AC1009: 特殊二进制格式
  - AC1012-AC1015: 基础结构，无压缩
  - AC1018-AC1020: LZ77 压缩、CRC32 校验
  - AC1021+: LZ77 压缩、Reed-Solomon 编码

## 5. 支持的 CAD 版本兼容性矩阵

| 版本 | 名称 | DXF读 | DXF写 | DWG读 | DWG写 |
|------|------|:----:|:----:|:----:|:----:|
| AC1009 | R11/R12 | ✓ | ✗ | ✗ | ✗ |
| AC1012 | R13 | ✓ | ✓ | ✓ | ✓ |
| AC1014 | R14 | ✓ | ✓ | ✓ | ✓ |
| AC1015 | R2000 | ✓ | ✓ | ✓ | ✓ |
| AC1018 | R2004-2006 | ✓ | ✓ | ✓ | ✓ |
| AC1021 | R2007-2009 | ✓ | ✓ | ✓ | ✗ |
| AC1024 | R2010-2012 | ✓ | ✓ | ✓ | ✓ |
| AC1027 | R2013-2017 | ✓ | ✓ | ✓ | ✓ |
| AC1032 | R2018+ | ✓ | ✓ | ✓ | ✓ |

## 6. 核心架构组件

### 6.1 I/O 系统
- **DxfReader/DxfWriter** (`src/ACadSharp/IO/DXF/`): DXF 格式的读写入口
  - 支持 ASCII 和二进制格式自动检测
  - 章节级别的部分读取能力
  - 配置化的头变量写入集合
- **DwgReader/DwgWriter** (`src/ACadSharp/IO/DWG/`): DWG 格式的读写入口
  - 多个版本相关的文件头结构（AC15/AC18/AC21）
  - 位级别的数据读写操作
  - 支持 LZ77 压缩和 Reed-Solomon 编码

### 6.2 对象模型
- **CadDocument** (`src/ACadSharp/CadDocument.cs`): 文档中心，包含所有表、集合、头部和实体
- **CadObject** (`src/ACadSharp/CadObject.cs`): 所有对象的基类，提供 Handle、Owner、Document、XDictionary
- **Entity** (`src/ACadSharp/Entities/Entity.cs`): 图形实体基类，添加视觉属性（颜色、图层、线型等）

### 6.3 Tables 和 Blocks
- **Table<T>** (`src/ACadSharp/Tables/Collections/Table.cs`): 通用表实现，管理命名的表项集合
  - LayersTable、LineTypesTable、BlockRecordsTable 等 9 个表
  - 强制唯一命名和默认条目保护
- **BlockRecord** (`src/ACadSharp/Tables/BlockRecord.cs`): 块元数据，包含实体集合、属性定义、排序表
- **Block/BlockEnd**: 块实体对象，标记块定义的开始和结束

### 6.4 DXF 映射系统
- **DxfMap** (`src/ACadSharp/DxfMap.cs`): 元数据驱动的映射生成器，通过反射动态创建 DXF 代码与 C# 属性的双向映射
- **DxfProperty** (`src/ACadSharp/DxfProperty.cs`): 单个 DXF 属性与对象属性的关联
- **特性系统** (`src/ACadSharp/Attributes/`): DxfName、DxfSubClass、DxfCodeValue 等标注属性

### 6.5 文档构建
- **DxfDocumentBuilder** (`src/ACadSharp/IO/DXF/DxfDocumentBuilder.cs`): 整合 DXF 读取各部分数据
- **DwgDocumentBuilder** (`src/ACadSharp/IO/DWG/DwgDocumentBuilder.cs`): 整合 DWG 读取各部分数据，处理句柄关系

## 7. 项目结构

```
src/ACadSharp/
├── Entities/               (144 文件) - 图形实体类
├── Objects/                (71 文件)  - 非图形对象
│   ├── Collections/        - 对象集合
│   └── Evaluations/        - 评估对象（动态块）
├── Tables/                 (36 文件) - 表项类
│   └── Collections/        - 表集合实现
├── IO/
│   ├── DXF/                - DXF 读写（主类 + 流处理器）
│   ├── DWG/                - DWG 读写（主类 + 流处理器 + 文件头）
│   ├── SVG/                - SVG 导出
│   └── Templates/          (74+ 个) - 泛型对象模板
├── Header/                 - 文件头变量定义
├── Blocks/                 - 块管理
├── Classes/                - DXF 类定义
├── XData/                  - 扩展数据系统
├── Attributes/             - 元数据特性
├── Extensions/             - 扩展方法
├── Exceptions/             - 自定义异常
├── DxfMap.cs               - DXF 映射系统
├── DxfCode.cs              - DXF 代码枚举
├── DxfFileToken.cs         - DXF 文件令牌
├── DxfSubclassMarker.cs    - DXF 子类标记
└── CadDocument.cs          - 核心文档类

src/ACadSharp.Tests/        (94 个测试文件)
├── IO/                     - I/O 测试（读写、格式转换）
├── Entities/               - 实体测试（24 个）
├── Tables/                 - 表和表项测试
├── Objects/                - 对象测试
├── Common/                 - 测试工具和工厂
├── TestModels/             - 测试数据模型
└── Data/                   - 参考 JSON 数据

src/ACadSharp.Examples/     (407 行代码)
├── Program.cs              - 主程序（读写演示）
├── ReaderExamples.cs       - 读取器示例
├── WriterExamples.cs       - 写入器示例
├── DocumentExamples.cs     - 文档操作示例
└── Entities/               - 实体创建示例

samples/                    - 27 个测试数据文件
├── *.dwg (AC1009-AC1032)   - DWG 格式文件
├── *_ascii.dxf             - ASCII DXF 文件
├── *_binary.dxf            - 二进制 DXF 文件
└── patterns/, geolocation/ - 数据资源
```

## 8. 关键技术亮点

1. **元数据驱动的序列化**: 通过反射和特性自动生成 DXF 映射，支持自动化读写逻辑
2. **多格式支持**: DXF(ASCII/Binary)、DWG(8 个版本)、SVG 导出，统一的文档模型
3. **版本适应**: 自动版本检测、版本相关的文件头处理、编码自适应（UTF-8 for AC1021+）
4. **完整的错误处理**: 三级通知系统（NotImplemented/Warning/Error），大多数 DXF 错误非致命允许部分恢复
5. **高效的位级读写**: DWG 采用位级数据操作，支持压缩和编码验证
6. **完善的集合管理**: 强制单一所有权、支持可观察集合、自动文档绑定
7. **灵活的属性系统**: ByLayer/ByBlock 属性解析、XData 扩展、反应器追踪

## 9. 测试和质量保证

- **测试覆盖**: 94 个测试文件，涵盖 8 个 CAD 版本、24 种实体类型、完整 I/O 功能
- **测试框架**: xUnit，支持参数化测试（Theory）和固定场景（Fact）
- **多框架测试**: 并行测试 .NET 9.0 和 .NET Framework 4.8
- **代码覆盖**: Coverlet LCOV 格式集成，自动上传 Coveralls
- **CI/CD 自动化**: GitHub Actions 工作流自动编译、测试、覆盖率收集
- **样本数据**: 27 个 DWG/DXF 文件，JSON 参考数据用于树结构验证

## 10. 快速开始示例

### 读取 DWG 文件
```csharp
CadDocument doc = DwgReader.Read("file.dwg");
foreach (var entity in doc.Entities)
{
    Console.WriteLine($"{entity.ObjectName}: {entity.Handle}");
}
```

### 创建和写入
```csharp
CadDocument doc = new CadDocument();
var line = new Line { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(10, 10, 0) };
doc.Entities.Add(line);
DwgWriter.Write("output.dwg", doc);
```

### 文件格式转换
```csharp
CadDocument doc = DxfReader.Read("file.dxf");
DwgWriter.Write("output.dwg", doc);  // DXF 转 DWG
```
