# ACadSharp IO 系统知识库

## OVERVIEW
CAD 文件（DXF/DWG）的核心 I/O 系统，负责位级流处理、多版本适配及基于模板的对象图重构。

## STRUCTURE
- `DXF/`: DXF 专用逻辑。包含 `DxfStreamReader/Writer` 处理 ASCII 和二进制流，按 Section（Header, Tables, Entities 等）切分读取逻辑。
- `DWG/`: DWG 专用逻辑。处理复杂的位级读取、LZ77 压缩/解压、Reed-Solomon 纠错及版本特定的 FileHeader 解析。
- `Templates/`: 模板系统。用于在读取过程中暂存对象数据，通过 Handle 记录引用，支持在文档构建阶段进行延迟解析。每个 `CadObject` 对应一个 `CadTemplate` 子类。
- `CadReaderBase.cs` / `ICadReader.cs`: 读写器基类与接口，定义了标准的 I/O 生命周期，包括流初始化和基础通知分发。
- `HugeMemoryStream.cs`: 专为处理超大 CAD 文件设计的流管理工具，优化了内存分配。

## WHERE TO LOOK
- `DxfReader` / `DwgReader`: I/O 入口点，负责版本检测、编码识别和驱动分段读取流程。
- `DxfDocumentBuilder` / `DwgDocumentBuilder`: 核心构建器，协调模板系统并在 `BuildDocument()` 中解析 Handle 引用和建立所有权。
- `Templates/CadTemplate`: 所有对象的中间态基类，存储未解析的 OwnerHandle、ReactorsHandles 和扩展数据。
- `DWG/DwgStreamReaders/`: 针对不同版本（AC1015-AC1032）的 DWG 数据流处理器。
- `DXF/DxfStreamReader/`: DXF 令牌解析核心，支持文本和二进制 DXF。

## CONVENTIONS
- **双阶段读取模式 (Two-Pass Read)**: 
  1. **提取阶段 (Extraction)**: 读取字节流，填充 `CadTemplate` 及其子类，将所有 Handle 存储为 `ulong`。
  2. **构建阶段 (Building)**: 调用 `Builder.BuildDocument()`，根据存储的 Handle 从 `Builder` 的对象缓存中检索真实的 `CadObject` 并完成赋值。
- **模板分层**: 每个实体类（如 `Line`）在 `Templates` 目录下都有对应的模板类（如 `CadEntityTemplate`），用于承载序列化中间值。
- **配置驱动**: 必须通过 `DxfReaderConfiguration` 或 `DwgReaderConfiguration` 控制读取行为（如编码、SummaryInfo 读取、自动缓存清理）。
- **位级读取一致性**: DWG 读取应使用 `IDwgStreamReader` 屏蔽版本差异，注意位读取顺序（Bit-level reading）和补零位。
- **通知机制**: 过程中的警告（如 Handle 丢失、不兼容的属性）应通过 `NotificationEventHandler` 冒泡，而非直接抛出异常。
- **编码处理**: AC1021 之前版本依赖 `$DWGCODEPAGE`，之后版本强制使用 UTF-8。

## ANTI-PATTERNS
- **立即引用解析**: 禁止在读取单个对象时尝试解析引用 Handle，必须交由 `CadDocumentBuilder` 统一处理以避免循环依赖。
- **硬编码版本偏移**: 严禁在 DWG 逻辑中硬编码固定偏移量，应始终通过 `DwgFileHeader` 的 `Records` 或 `Descriptors` 定位。
- **绕过 Builder 添加对象**: 禁止在 I/O 过程中绕过 Builder 直接向 `CadDocument` 添加实体，否则会破坏 Handle 种子管理。
- **缺失 CRC 校验**: 写入 DWG 时必须正确计算 CRC8/CRC32，否则会导致 AutoCAD 无法打开文件。
- **忽略内存流管理**: 处理大型 DWG 时应优先使用 `HugeMemoryStream` 以避免 LOH 碎片。
