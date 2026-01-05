# DWG 功能移植到 Rust 的架构策略

## 1. Identity

- **What it is:** 将 ACadSharp 成熟的 DWG 读写功能系统性地移植到 zcad-rs 项目的架构策略文档。
- **Purpose:** 为 Rust 生态系统提供完整的 CAD 文件处理能力，填补关键的技术空白。

## 2. Core Components

### 2.1 架构映射核心文件
- `src/ACadSharp/IO/DWG/DwgReader.cs` (DwgReader): DWG 读取主入口，双阶段读取模式参考
- `src/ACadSharp/IO/DWG/DwgWriter.cs` (DwgWriter): DWG 写入主入口，需要完整移植
- `src/ACadSharp/IO/DWG/DwgDocumentBuilder.cs` (DwgDocumentBuilder): 文档构建器，对象图重构核心
- `src/ACadSharp/IO/DWG/DwgStreamReaders/DwgStreamReaderBase.cs` (DwgStreamReaderBase): 位级流处理基础
- `/Volumes/DPC/work/cad-code/zcadvelecAI/zcad-rs/zcad-io/src/dwg/mod.rs`: zcad-rs 现有 DWG 读取实现
- `/Volumes/DPC/work/cad-code/zcadvelecAI/zcad-rs/zcad-io/src/dwg/ac18_entities.rs`: 最大 DWG 实现文件 (2419行)

### 2.2 压缩和校验系统
- `src/ACadSharp/IO/DWG/DwgStreamReaders/DwgLZ77AC18Decompressor.cs`: AC1018+ LZ77 解压缩
- `src/ACadSharp/IO/DWG/DwgStreamWriters/DwgLZ77AC18Compressor.cs`: AC1018+ LZ77 压缩器
- `src/ACadSharp/IO/DWG/CRC32StreamHandler.cs`: CRC32 校验处理器
- `src/ACadSharp/IO/DWG/DwgCheckSumCalculator.cs`: 校验和计算器

### 2.3 版本处理系统
- `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeader.cs`: 版本检测工厂方法
- `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeaderAC15.cs`: AC1012-AC1015 支持
- `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeaderAC18.cs`: AC1018-AC1020 支持
- `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeaderAC21.cs`: AC1021+ 支持

### 2.4 测试和验证
- `src/ACadSharp.Tests/IO/DWG/DwgReaderTests.cs`: 读取功能测试
- `src/ACadSharp.Tests/IO/DWG/DwgWriterTests.cs`: 写入功能测试
- `src/ACadSharp.Tests/IO/DWG/DwgWriterSingleObjectTests.cs`: 序列化保真度测试

### 2.5 相关分析文档
- `/llmdoc/agent/scout-dwg-functionality-analysis.md`: ACadSharp DWG 功能深度分析
- `/llmdoc/agent/scout-zcad-rs-analysis.md`: zcad-rs 项目现状评估
- `/llmdoc/agent/dwg-to-rust-migration-plan.md`: 详细实施计划
- `/llmdoc/agent/dwg-to-rust-migration-summary.md`: 执行摘要

## 3. Execution Flow (LLM Retrieval Map)

### 3.1 移植架构决策流程

- **1. 阶段性评估**: 从 `/llmdoc/agent/dwg-to-rust-migration-plan.md` 获取完整的分阶段计划
- **2. 技术映射**: 参考 `/llmdoc/agent/scout-dwg-functionality-analysis.md` 中的核心组件映射关系
- **3. 目标架构适配**: 基于 `/llmdoc/agent/scout-zcad-rs-analysis.md` 的现有架构进行适配
- **4. 实施路径**: 按照 `/llmdoc/agent/dwg-to-rust-migration-plan.md` 中的 4 阶段实施计划执行

### 3.2 核心移植流程

- **1. 基础架构移植** (4-6周): 扩展 zcad-rs 的 BitStream 系统，实现基础写入能力
- **2. 核心功能实现** (6-8周): 移植句柄系统、压缩算法、对象序列化
- **3. 版本扩展** (4-6周): 从 AC1018/AC1021 扩展到 AC1012-AC1032 全版本支持
- **4. 质量保证** (3-4周): 全面测试、性能优化、文档完善

### 3.3 Rust 特定适配

- **内存管理**: 使用 Arena 分配器 + EntityId 类型安全引用系统
- **错误处理**: 基于 thiserror 的结构化错误处理
- **性能优化**: 零拷贝解析、边界检查消除、可选 SIMD 优化

## 4. Design Rationale

### 4.1 为什么选择渐进式移植

- **风险控制**: 分阶段实施降低技术风险，每个阶段都有可验证的成果
- **质量保证**: 测试驱动开发确保每个组件的可靠性
- **团队效率**: 并行开发不同模块，最大化资源利用

### 4.2 架构适配策略

- **保留优势**: 充分利用 zcad-rs 的模块化架构和 Rust 的类型安全特性
- **最小侵入**: 扩展现有 DwgFacade 而非重写，保持向后兼容
- **标准化**: 遵循 Rust 生态的最佳实践和编码规范

### 4.3 测试驱动的质量保证

- **三层测试金字塔**: 单元测试 → 集成测试 → 黄金样例测试
- **往返兼容性**: Read-Write-Read 完整流程验证
- **性能基准**: 与 ACadSharp 进行定量性能对比