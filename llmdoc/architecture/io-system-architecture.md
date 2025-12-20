# DXF/DWG I/O 系统架构

## 1. 身份

- **What it is:** ACadSharp 的核心 I/O 系统，负责 DXF (ASCII/Binary)、DWG (多版本) 文件的读写和格式转换。
- **Purpose:** 提供统一的、可扩展的 CAD 文件读写框架，支持多个版本、多种格式和部分加载能力。

## 2. 核心组件

### 2.1 基础接口和基类

- `src/ACadSharp/IO/ICadReader.cs` (ICadReader): CAD 读取器的通用接口，定义 Read() 和 ReadHeader() 方法。
- `src/ACadSharp/IO/ICadWriter.cs` (ICadWriter): CAD 写入器的通用接口，定义 Write() 方法。
- `src/ACadSharp/IO/CadReaderBase.cs` (CadReaderBase, CadReaderBase<T>): 所有读取器的基类，管理通知事件、编码和配置。
- `src/ACadSharp/IO/CadWriterBase.cs` (CadWriterBase, CadWriterBase<T>): 所有写入器的基类，管理通知事件、编码和文档验证。

### 2.2 配置系统

- `src/ACadSharp/IO/CadReaderConfiguration.cs` (CadReaderConfiguration): 读取器通用配置基础类。
- `src/ACadSharp/IO/CadWriterConfiguration.cs` (CadWriterConfiguration): 写入器通用配置基础类，支持验证和清流选项。
- `src/ACadSharp/IO/DXF/DxfReaderConfiguration.cs` (DxfReaderConfiguration): DXF 读取器专用配置。
- `src/ACadSharp/IO/DXF/DxfWriterConfiguration.cs` (DxfWriterConfiguration): DXF 写入器专用配置，定义头变量写入集合。
- `src/ACadSharp/IO/DWG/DwgReaderConfiguration.cs` (DwgReaderConfiguration): DWG 读取器专用配置，包括 CRC 校验和摘要信息读取选项。
- `src/ACadSharp/IO/DWG/DwgWriterConfiguration.cs` (DwgWriterConfiguration): DWG 写入器专用配置。

### 2.3 DXF 读取架构

- `src/ACadSharp/IO/DXF/DxfReader.cs` (DxfReader): 主入口类，继承自 CadReaderBase<DxfReaderConfiguration>。支持二进制和 ASCII 文本格式，按顺序读取 HEADER、CLASSES、TABLES、BLOCKS、ENTITIES、OBJECTS 等 DXF 章节。
- `src/ACadSharp/IO/DXF/DxfStreamReader/IDxfStreamReader.cs` (IDxfStreamReader): DXF 流读取的通用接口，定义了代码和值的读取操作。
- `src/ACadSharp/IO/DXF/DxfStreamReader/DxfBinaryReader.cs` (DxfBinaryReader): 二进制 DXF 文件读取实现（R14 及更新版本）。
- `src/ACadSharp/IO/DXF/DxfStreamReader/DxfBinaryReaderAC1009.cs` (DxfBinaryReaderAC1009): AC1009 格式专用的二进制读取器。
- `src/ACadSharp/IO/DXF/DxfStreamReader/DxfTextReader.cs` (DxfTextReader): ASCII 文本 DXF 文件读取实现。
- `src/ACadSharp/IO/DXF/DxfDocumentBuilder.cs` (DxfDocumentBuilder): DXF 文档构建器，整合读取的数据到 CadDocument 中。
- 章节读取器（DxfTablesSectionReader、DxfBlockSectionReader、DxfEntitiesSectionReader、DxfObjectsSectionReader）: 各自负责对应 DXF 章节的读取。

### 2.4 DXF 写入架构

- `src/ACadSharp/IO/DXF/DxfWriter.cs` (DxfWriter): 主入口类，继承自 CadWriterBase<DxfWriterConfiguration>。支持二进制和 ASCII 格式输出。
- `src/ACadSharp/IO/DXF/DxfStreamWriter/IDxfStreamWriter.cs` (IDxfStreamWriter): DXF 流写入的通用接口。
- `src/ACadSharp/IO/DXF/DxfStreamWriter/DxfBinaryWriter.cs` (DxfBinaryWriter): 二进制 DXF 写入实现。
- `src/ACadSharp/IO/DXF/DxfStreamWriter/DxfAsciiWriter.cs` (DxfAsciiWriter): ASCII 文本 DXF 写入实现。
- 章节写入器（DxfHeaderSectionWriter、DxfTablesSectionWriter、DxfBlocksSectionWriter、DxfEntitiesSectionWriter、DxfObjectsSectionWriter）: 各自负责对应 DXF 章节的写入。

### 2.5 DWG 读取架构

- `src/ACadSharp/IO/DWG/DwgReader.cs` (DwgReader): 主入口类，继承自 CadReaderBase<DwgReaderConfiguration>。支持 AC1012-AC1032 版本（R13-R2020）。
- `src/ACadSharp/IO/DWG/DwgStreamReaders/IDwgStreamReader.cs` (IDwgStreamReader): DWG 流读取的通用接口。
- `src/ACadSharp/IO/DWG/DwgStreamReaders/DwgStreamReaderBase.cs` (DwgStreamReaderBase): DWG 流读取的基础类，实现位级别的数据读取。
- `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeader.cs` (DwgFileHeader): DWG 文件头的基础抽象类，工厂方法用于创建版本相应的文件头对象。
- 版本相关文件头类：DwgFileHeaderAC15、DwgFileHeaderAC18、DwgFileHeaderAC21 - 处理不同版本的文件头结构差异。
- 流读取器（DwgHeaderReader、DwgClassesReader、DwgObjectReader、DwgHandleReader）: 各自负责特定数据章节的读取。
- `src/ACadSharp/IO/DWG/DwgDocumentBuilder.cs` (DwgDocumentBuilder): DWG 文档构建器，整合读取数据并处理句柄引用。

### 2.6 DWG 写入架构

- `src/ACadSharp/IO/DWG/DwgWriter.cs` (DwgWriter): 主入口类，继承自 CadWriterBase<DwgWriterConfiguration>。
- `src/ACadSharp/IO/DWG/DwgStreamWriters/IDwgFileHeaderWriter.cs` (IDwgFileHeaderWriter): 文件头写入的通用接口。
- `src/ACadSharp/IO/DWG/DwgStreamWriters/IDwgStreamWriter.cs` (IDwgStreamWriter): DWG 流写入的通用接口。
- 版本相关文件头写入器：DwgFileHeaderWriterAC15、DwgFileHeaderWriterAC18、DwgFileHeaderWriterAC21。
- 流写入器（DwgHeaderWriter、DwgClassesWriter、DwgObjectWriter、DwgHandleWriter）: 各自负责特定数据章节的写入。

### 2.7 辅助系统

- `src/ACadSharp/IO/NotificationEventHandler.cs` (NotificationEventHandler): 通知系统，定义了 NotificationEventArgs 和 NotificationType 枚举。
- `src/ACadSharp/IO/CadObjectHolder.cs` (CadObjectHolder): 对象持有器，用于追踪需要写入的对象队列。
- `src/ACadSharp/IO/Templates/ICadTemplate.cs` 和其 74+ 个实现类: 泛型模板系统，支持各种 CAD 对象的类型化序列化。
- 版本支持：`src/ACadSharp/ACadVersion.cs` (ACadVersion) 定义了从 MC0_0 到 AC1032 的所有支持版本。

## 3. 执行流（LLM 检索地图）

### 3.1 DXF 读取流程

```
1. 初始化阶段
   - DxfReader.Read(stream) 入口
   - getReader() 检测格式 (IsBinary) 和版本 (AC1009)
   - 创建对应的流读取器 (DxfBinaryReader/DxfTextReader/DxfBinaryReaderAC1009)

2. 文档构建阶段
   - DxfDocumentBuilder 初始化
   - 按顺序读取各章节:
     a. readHeader() - 使用 CadHeader.GetHeaderMap() 解析变量
     b. readClasses() - 逐个解析类定义
     c. readTables() - 使用 DxfTablesSectionReader
     d. readBlocks() - 使用 DxfBlockSectionReader
     e. readEntities() - 使用 DxfEntitiesSectionReader
     f. readObjects() - 使用 DxfObjectsSectionReader

3. 整合阶段
   - DxfDocumentBuilder.BuildDocument()
   - 返回 CadDocument 实例

4. 部分读取支持
   - 仅读取 HEADER (ReadHeader())
   - 仅读取 ENTITIES 和 OBJECTS
   - 跳过失败部分继续读取
```

关键代码位置：
- 格式检测：`src/ACadSharp/IO/DXF/DxfReader.cs:getReader()`
- 版本识别：`src/ACadSharp/CadUtils.cs:GetVersionFromName()`

### 3.2 DWG 读取流程

```
1. 文件头读取阶段
   - DwgReader.Read(stream) 入口
   - readFileHeader() - 识别版本（前6字节 "ACXXXX"）
   - 根据版本调用 readFileHeaderAC15/AC18/AC21
   - 获取各章节的位置和大小信息

2. 数据章节读取阶段
   - DwgDocumentBuilder 初始化
   - ReadSummaryInfo() - AC1018+ 版本
   - ReadHeader() → DwgHeaderReader
   - readClasses() → DwgClassesReader
   - readHandles() → DwgHandleReader (获取句柄表)
   - readObjects() → DwgObjectReader (读取所有对象)

3. 对象关系构建阶段
   - 通过句柄表构建对象关系
   - 解压缩（AC1018+ 使用 LZ77）
   - 处理 CRC 校验（可选）

4. 整合阶段
   - DwgDocumentBuilder.BuildDocument()
   - 返回 CadDocument 实例
```

关键代码位置：
- 版本识别：`src/ACadSharp/IO/DWG/DwgReader.cs:readFileHeader()`
- 文件头工厂：`src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeader.cs:CreateFileHeader()`

### 3.3 DXF 写入流程

```
1. 准备阶段
   - DxfWriter.Write(stream, document) 入口
   - createStreamWriter() - 创建二进制或文本写入器
   - 将 RootDictionary 加入写入队列

2. 章节写入阶段
   - 依次写入各章节：
     a. writeHeader() → DxfHeaderSectionWriter
     b. writeDxfClasses()
     c. writeTables()
     d. writeBlocks()
     e. writeEntities()
     f. writeObjects()
     g. writeACDSData()
     h. 写入 EOF 标记

3. 对象队列处理
   - 使用 CadObjectHolder 追踪所有需要写入的对象
   - 各 SectionWriter 调用 CadObjectHolder 进行对象处理

4. 完成阶段
   - 刷新/关闭流
```

关键代码位置：
- 写入入口：`src/ACadSharp/IO/DXF/DxfWriter.cs:Write()`
- 对象队列：`src/ACadSharp/IO/CadObjectHolder.cs`

### 3.4 DWG 写入流程

```
1. 准备阶段
   - DwgWriter.Write(stream, document) 入口
   - getFileHeaderWriter() - 创建版本相应的写入器
   - 初始化各章节缓冲区

2. 章节写入阶段
   - 逐个写入各章节（保持特定顺序）：
     a. writeHeader()
     b. writeClasses()
     c. writeSummaryInfo()
     d. writePreview()
     e. writeAppInfo()
     f. writeFileDepList()
     g. writeRevHistory()
     h. writeAuxHeader()
     i. writeObjects()
     j. 其他元数据章节

3. 文件头最终化
   - DwgWriter.WriteFile() - 写入文件头和完整性检查
   - 处理 CRC 校验（版本相关）
   - 处理压缩和编码（AC1018+ 版本）

4. 完成阶段
   - 关闭/刷新流
```

关键代码位置：
- 写入入口：`src/ACadSharp/IO/DWG/DwgWriter.cs:Write()`
- 文件完成：`src/ACadSharp/IO/DWG/DwgWriter.cs:WriteFile()`

## 4. 版本特定处理

### 4.1 DXF 版本差异

| 版本范围 | 特点 | 处理方式 |
|---------|------|--------|
| MC0_0 - AC1009 | 仅读取 | 特殊的 AC1009 二进制读取器，不支持写入 |
| AC1012 - AC1015 | 基础格式 | 标准 DXF 读写，无特殊处理 |
| AC1018 - AC1020 | 新格式 | 支持二进制和 ASCII |
| AC1021+ | UTF-8 支持 | 自动启用 UTF-8 编码 |

### 4.2 DWG 版本差异

- **AC1012-AC1015 (R13-R2000)**: 基础结构，直接读写，无压缩。使用 DwgFileHeaderAC15。
- **AC1018-AC1020 (R2004-2006)**: LZ77 压缩、CRC32 校验、分页结构。使用 DwgFileHeaderAC18。
- **AC1021+ (R2007+)**: LZ77 压缩、Reed-Solomon (255,239) 编码、增强的页面映射。使用 DwgFileHeaderAC21。

关键位置：
- 版本检测：`src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeader.cs:CreateFileHeader()`
- 压缩处理：`src/ACadSharp/IO/DWG/Decompressors/DwgLZ77AC18Decompressor.cs` 和 `DwgLZ77AC21Decompressor.cs`

## 5. 错误处理和通知系统

### 5.1 通知系统架构

```csharp
delegate: NotificationEventHandler(object sender, NotificationEventArgs e)

enum NotificationType:
  - NotImplemented (-1): 功能未实现
  - None (0): 无通知
  - NotSupported (1): 功能不支持
  - Warning (2): 警告信息
  - Error (3): 错误
```

关键位置：`src/ACadSharp/IO/NotificationEventHandler.cs`

### 5.2 错误处理策略

1. **DXF 读取**:
   - 未知头变量: 记录 NotImplemented 通知，跳过
   - 无效的头值: 发送 Warning 通知，使用默认值
   - 无法找到 HEADER 章节: Warning，使用泛型读取器
   - 不支持的版本: 抛出 CadNotSupportedException

2. **DWG 读取**:
   - 不支持的版本: 抛出 CadNotSupportedException
   - 章节不存在: 返回 null，读取继续
   - 文件头验证失败: Warning，记录实际 ID

3. **编码处理**:
   - 尝试使用指定的编码
   - 失败时回退到 Windows-1252
   - AC1021+ 自动使用 UTF-8

### 5.3 通知订阅和处理

读取器/写入器通过 `OnNotification` 事件暴露通知：
- 订阅者可以记录日志、更新 UI、收集统计信息
- 大多数 DXF 错误是非致命的，读取继续进行
- DWG 严重错误导致异常，部分读取支持允许恢复

## 6. 配置选项总结

### 6.1 DxfReaderConfiguration
- 基础配置（继承自 CadReaderConfiguration）

### 6.2 DxfWriterConfiguration
- WriteOptionalValues: 是否写入可选值
- 头变量写入集合定制

### 6.3 DwgReaderConfiguration
- CrcCheck: 启用 CRC32 校验（影响性能）
- ReadSummaryInfo: 跳过摘要信息以加快速度

### 6.4 DwgWriterConfiguration
- 基础配置（继承自 CadWriterBase 的通用选项）

### 6.5 CadWriterConfiguration (通用)
- ResetDxfClasses: 重置 DXF 类并更新计数
- UpdateDimensionsInModel: 更新模型空间的标注
- UpdateDimensionsInBlocks: 更新块中的标注
- CloseStream: 写入后关闭流
- ValidateOnWrite: 写入前进行文档验证

## 7. 章节处理机制

### 7.1 DXF 章节依赖关系

```
HEADER (必需) → 定义基础参数
CLASSES → 定义自定义类
TABLES → 定义层、线型、样式等资源
BLOCKS → 定义块定义
ENTITIES → 定义模型空间实体
OBJECTS → 定义字典和其他非图形对象
```

### 7.2 DWG 章节依赖关系

```
Header (文件头) → 定义元数据和分页位置
SUMMARY INFO → 文档属性
HEADER (数据章节) → 系统变量
CLASSES → 类定义
HANDLES → 句柄表（对象索引）
AcDbObjects → 所有对象和实体数据
```

## 8. 设计亮点

1. **元数据驱动映射**: 通过 DxfMap 和特性系统自动生成 DXF 代码与 C# 属性的双向映射。
2. **版本透明处理**: 版本检测自动化，版本相关代码通过工厂方法隔离。
3. **流处理架构**: 支持大文件，使用 HugeMemoryStream 处理内存压力。
4. **事件驱动错误处理**: 通知系统贯穿整个读写过程，支持细粒度的错误监控。
5. **可扩展的模板系统**: 74+ 个 ICadTemplate 实现支持灵活的对象序列化。
6. **部分加载支持**: DXF 支持章节级别的部分读取，DWG 支持跳过某些章节。
7. **多格式支持**: 统一接口支持 DXF(ASCII/Binary)、DWG(8个版本)、SVG 导出。

