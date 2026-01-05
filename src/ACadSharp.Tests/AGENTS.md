# ACADSHARP 测试架构知识库

**生成时间**: 2026-01-02 12:45:00 AM
**适用范围**: `src/ACadSharp.Tests/` 目录及其子模块

## OVERVIEW
ACadSharp.Tests 采用真实对象驱动与详尽数据驱动模式，确保 AutoCAD 复杂对象模型在 I/O 往返和内部克隆中的完整性。

## STRUCTURE
- **`Common/`**: 测试套件核心引擎。
  - `DataFactory.cs`: 使用反射自动发现程序集中所有实体类型进行覆盖测试。
  - `DocumentIntegrity.cs`: 验证对象图一致性（Handle、Owner、Document 引用）。
  - `IOTestsBase.cs`: I/O 测试基类，管理配置及拦截内部异常。
- **`Data/`**: 存储特定版本的 CAD 文档结构快照（JSON），用于跨版本回归测试。
- **`TestModels/`**: 测试专用数据模型。
  - `FileModel`: 包装测试文件路径，支持 XUnit 序列化以优化测试视图。
  - `CadDocumentTree`: 描述文档结构的树形模型，用于与快照对比。
- **`IO/`**: 深度镜像 `ACadSharp/IO` 结构，包含 DWG/DXF 的读取、写入及往返测试。

## WHERE TO LOOK
- **自动化类型发现**: 见 `Common/DataFactory.GetTypes<T>()`。
- **往返完整性测试**: 见 `IO/IOTests.cs` 中的 `DwgEntitiesToDwgFile` 等方法。
- **环境敏感开关**: 见 `TestVariables.cs`（控制 CI/CD 与本地环境的行为差异）。
- **拦截器逻辑**: 见 `IOTestsBase.onNotification`（将库的 Warning 提升为 Test Failure）。

## CONVENTIONS
- **Real-Object Driven**: 禁止使用 Mock 框架。由于 CAD 对象间存在深度的双向引用和文档上下文依赖，必须使用真实的 `CadObject` 实例。
- **Data-Driven Theory**: 广泛使用 `[Theory]`。通过 `samples/` 子模块扫描真实文件，或通过反射生成所有实体的测试矩阵。
- **Round-Trip Validation**: 核心逻辑为：读取 -> 修改/移动 -> 写入 -> 重新读取验证一致性。
- **Integrity Assertion**: 任何创建或移动对象的操作，必须调用 `DocumentIntegrity` 验证其在文档中的连接是否正确。

## ANTI-PATTERNS
- **禁止 Mocking**: 严禁 Mock `CadDocument` 或 `Entity` 等核心类。
- **禁止静默失败**: 不得忽略 `onNotification` 中的 Error 级通知，必须手动触发测试失败。
- **严禁硬编码路径**: 资源路径必须通过 `TestVariables` 获取，以保证跨平台兼容性。
- **禁止忽略子模块**: 本测试套件高度依赖 `samples/` 子模块，运行前必须初始化。
