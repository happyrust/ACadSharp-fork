### Code Sections (The Evidence)

#### 核心读写类
- `src/ACadSharp/IO/DWG/DwgReader.cs` (DwgReader): DWG 读取主入口类，继承自 CadReaderBase<DwgReaderConfiguration>，负责版本检测和读取流程编排。
- `src/ACadSharp/IO/DWG/DwgWriter.cs` (DwgWriter): DWG 写入主入口类，继承自 CadWriterBase<DwgWriterConfiguration>，协调各章节写入和文件生成。
- `src/ACadSharp/IO/DWG/DwgDocumentBuilder.cs` (DwgDocumentBuilder): DWG 文档构建器，实现双阶段读取模式，负责句柄解析和对象图重构。

#### 流处理和压缩系统
- `src/ACadSharp/IO/DWG/DwgStreamReaders/DwgStreamReaderBase.cs` (DwgStreamReaderBase): DWG 位流读取基类，实现位级数据读取逻辑。
- `src/ACadSharp/IO/DWG/DwgStreamReaders/DwgLZ77AC18Decompressor.cs` (DwgLZ77AC18Decompressor): AC1018+ 版本专用的 LZ77 解压缩算法实现。
- `src/ACadSharp/IO/DWG/DwgStreamWriters/DwgLZ77AC18Compressor.cs` (DwgLZ77AC18Compressor): AC1018+ 版本专用的 LZ77 压缩算法实现。

#### 文件头和版本处理
- `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeader.cs` (DwgFileHeader): DWG 文件头抽象基类，包含版本检测的工厂方法 CreateFileHeader()。
- `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeaderAC15.cs` (DwgFileHeaderAC15): AC1012-AC1015 版本（R13-R2000）的文件头实现。
- `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeaderAC18.cs` (DwgFileHeaderAC18): AC1018-AC1020 版本（R2004-2006）的文件头实现，支持压缩。
- `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeaderAC21.cs` (DwgFileHeaderAC21): AC1021+ 版本（R2007+）的文件头实现，支持 UTF-8。

#### 数据读取器
- `src/ACadSharp/IO/DWG/DwgStreamReaders/DwgHeaderReader.cs` (DwgHeaderReader): DWG 系统变量章节读取器。
- `src/ACadSharp/IO/DWG/DwgStreamReaders/DwgObjectReader.cs` (DwgObjectReader): DWG 对象数据读取器，处理实体和非图形对象。
- `src/ACadSharp/IO/DWG/DwgStreamReaders/DwgHandleReader.cs` (DwgHandleReader): DWG 句柄表读取器，构建句柄到对象偏移的映射。
- `src/ACadSharp/IO/DWG/DwgStreamReaders/DwgClassesReader.cs` (DwgClassesReader): DWG 类定义读取器。

#### 配置和工具类
- `src/ACadSharp/IO/DWG/DwgReaderConfiguration.cs` (DwgReaderConfiguration): DWG 读取器配置类，包含 CRC 校验和摘要信息读取选项。
- `src/ACadSharp/IO/DWG/DwgWriterConfiguration.cs` (DwgWriterConfiguration): DWG 写入器配置类。
- `src/ACadSharp/IO/DWG/DwgCheckSumCalculator.cs` (DwgCheckSumCalculator): DWG 校验和计算器。
- `src/ACadSharp/IO/DWG/CRC32StreamHandler.cs` (CRC32StreamHandler): CRC32 校验处理器。

#### 测试代码
- `src/ACadSharp.Tests/IO/DWG/DwgReaderTests.cs` (DwgReaderTests): DWG 读取功能测试，覆盖基础读取、文件头、预览图和 CRC 校验。
- `src/ACadSharp.Tests/IO/DWG/DwgWriterTests.cs` (DwgWriterTests): DWG 写入功能测试，包含版本兼容性和实体写入测试。
- `src/ACadSharp.Tests/IO/DWG/DwgWriterSingleObjectTests.cs` (DwgWriterSingleObjectTests): 单个对象的 DWG 序列化保真度测试。
- `src/ACadSharp.Tests/Internal/DwgFileHeaderExploration.cs` (DwgFileHeaderExploration): DWG 版本兼容性探索测试。

#### 共享基础架构
- `src/ACadSharp/IO/CadReaderBase.cs` (CadReaderBase): 所有读取器的基类，管理通知事件、编码和配置。
- `src/ACadSharp/IO/CadWriterBase.cs` (CadWriterBase): 所有写入器的基类，管理通知事件、编码和文档验证。
- `src/ACadSharp/IO/ICadReader.cs` (ICadReader): CAD 读取器通用接口，定义 Read() 和 ReadHeader() 方法。
- `src/ACadSharp/IO/ICadWriter.cs` (ICadWriter): CAD 写入器通用接口，定义 Write() 方法。

### Report (The Answers)

#### result

1. **DWG 读写核心类和实现文件位置**

**主入口类：**
- `src/ACadSharp/IO/DWG/DwgReader.cs` - DWG 读取主入口
- `src/ACadSharp/IO/DWG/DwgWriter.cs` - DWG 写入主入口

**核心架构类：**
- `src/ACadSharp/IO/DWG/DwgDocumentBuilder.cs` - 文档构建器，实现双阶段读取
- `src/ACadSharp/IO/DWG/FileHeaders/` - 文件头处理（AC15/AC18/AC21 版本）
- `src/ACadSharp/IO/DWG/DwgStreamReaders/` - 流读取器集合
- `src/ACadSharp/IO/DWG/DwgStreamWriters/` - 流写入器集合

2. **DWG 格式特有的数据结构和处理逻辑**

**压缩算法：**
- LZ77 压缩：`src/ACadSharp/IO/DWG/DwgStreamReaders/DwgLZ77AC18Decompressor.cs`
- AC1018+ 版本专用压缩实现：`src/ACadSharp/IO/DWG/DwgStreamWriters/DwgLZ77AC18Compressor.cs`

**句柄系统：**
- 句柄读取：`src/ACadSharp/IO/DWG/DwgStreamReaders/DwgHandleReader.cs`
- 句柄集合管理：`src/ACadSharp/IO/DWG/DwgHeaderHandlesCollection.cs`

**版本特定处理：**
- 工厂模式版本识别：`src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeader.cs:CreateFileHeader()`
- 位级流处理：`src/ACadSharp/IO/DWG/DwgStreamReaders/DwgStreamReaderBase.cs`

3. **与 DXF 功能的共享代码和独立代码**

**共享基础架构：**
- 基础接口：`ICadReader`, `ICadWriter`
- 基类抽象：`CadReaderBase<T>`, `CadWriterBase<T>`
- 通知系统：`src/ACadSharp/IO/NotificationEventHandler.cs`
- 配置基类：`CadReaderConfiguration`, `CadWriterConfiguration`

**DXF 特有实现：**
- `src/ACadSharp/IO/DXF/DxfReader.cs` - DXF 读取入口
- `src/ACadSharp/IO/DXF/DxfStreamReader/` - DXF 流处理（文本/二进制）
- `src/ACadSharp/IO/DXF/DxfDocumentBuilder.cs` - DXF 文档构建

**DWG 特有实现：**
- 位级流处理（DwgStreamReaderBase）
- 压缩算法（LZ77）
- 句柄表系统
- 版本特定文件头

4. **DWG 读写的测试用例和示例**

**核心测试文件：**
- `src/ACadSharp.Tests/IO/DWG/DwgReaderTests.cs` - 读取功能测试
- `src/ACadSharp.Tests/IO/DWG/DwgWriterTests.cs` - 写入功能测试
- `src/ACadSharp.Tests/IO/DWG/DwgWriterSingleObjectTests.cs` - 单对象序列化测试
- `src/ACadSharp.Tests/Internal/DwgFileHeaderExploration.cs` - 版本兼容性探索

**测试覆盖范围：**
- 版本兼容性：AC1012 到 AC1032 的完整测试矩阵
- 往返完整性：读取 → 修改 → 写入 → 重新读取的完整性验证
- 性能测试：使用 Stopwatch 测量大型文件处理时间
- CRC 校验：数据完整性验证

5. **关键的算法和数据结构**

**双阶段读取架构：**
1. **提取阶段**：`DwgObjectReader` 读取字节流并填充 `CadTemplate`，对象引用存储为句柄
2. **构建阶段**：`DwgDocumentBuilder.BuildDocument()` 解析句柄引用，重构对象图

**位级数据处理：**
- `DwgStreamReaderBase` 提供位读取能力（Bit、BitShort、ModularChar 等）
- 支持可变长度整数和字符串编码

**版本适配机制：**
- 工厂模式：`DwgFileHeader.CreateFileHeader()` 根据版本创建相应处理器
- 版本特定的压缩和编码处理

#### conclusions

1. **分层架构设计**：ACadSharp 的 DWG 系统采用清晰的分层架构，将位流处理、版本特定逻辑和高层次对象操作分离，提高了代码的可维护性。

2. **版本兼容性策略**：通过工厂模式和版本特定的文件头类，实现了从 R13（AC1012）到 R2024（AC1032）的广泛版本支持。

3. **性能优化机制**：
   - 双阶段读取模式避免重复解析
   - LZ77 压缩减少文件大小
   - 配置驱动的选择性读取（如跳过摘要信息）

4. **数据完整性保障**：
   - CRC32 校验确保数据完整性
   - 往返测试验证序列化保真度
   - 句柄系统维护对象引用关系

5. **测试驱动开发**：项目采用真实对象驱动的测试策略，确保复杂对象模型在 I/O 往返中的完整性。

#### relations

1. **DwgReader → DwgDocumentBuilder**：DwgReader 使用 DwgDocumentBuilder 实现双阶段读取，将原始字节流转换为完整的 CadDocument 对象图。

2. **DwgFileHeader → 版本特定实现**：通过工厂模式创建版本特定的文件头处理器（AC15/AC18/AC21），每个版本处理不同的压缩和编码。

3. **DwgStreamReaderBase → 具体读取器**：基类提供位级读取能力，具体读取器（DwgHeaderReader、DwgObjectReader 等）实现特定章节的数据解析。

4. **CadReaderBase → DwgReader/DxfReader**：共享基类提供通用功能（通知管理、编码处理），具体读取器实现格式特定的逻辑。

5. **DwgObjectReader → CadTemplate**：对象读取器将 DWG 数据填充到模板对象中，为后续的对象构建阶段准备数据。

6. **模板系统统一性**：`src/ACadSharp/IO/Templates/CadTemplate.cs` 是 DWG 和 DXF 共享的中间态数据容器，屏蔽了底层格式的解析顺序差异，支持双阶段读取模式。

7. **映射系统共享**：`src/ACadSharp/DxfMap.cs` 和相关特性系统（`[DxfName]`, `[DxfCodeValue]`）定义了统一的属性语义，虽然名称带有"Dxf"但为整个项目服务。

8. **格式转换链**：DwgReader → CadDocument → DxfWriter 实现格式转换，CadDocument 作为统一的中间表示层，无需专门的转换器类。

9. **压缩系统集成**：LZ77 压缩器和解压器与版本特定的文件头紧密集成，AC1018+ 自动启用压缩，早期版本跳过压缩处理。

10. **测试类与实现类的关系**：测试类紧密对应实现类，例如 DwgReaderTests 测试 DwgReader 的所有主要功能，确保代码质量和回归测试覆盖。