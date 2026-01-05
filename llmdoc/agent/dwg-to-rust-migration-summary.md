# DWG 到 Rust 移植计划执行摘要

## 项目概述
基于 ACadSharp 成熟的 DWG 实现和 zcad-rs 的优秀架构，制定系统性移植计划，将完整的 DWG 读写功能迁移到 Rust 生态。

## 核心差距分析
| 维度 | ACadSharp | zcad-rs | 差距 |
|------|-----------|----------|------|
| **DWG 读写** | 完整读写 | 仅读取 | 写入功能缺失 |
| **版本支持** | AC1012-AC1032 (R13-R2024) | AC1018/AC1021 | 6个主要版本缺失 |
| **实体覆盖** | 144+ 实体文件 | 基础实体 | 完整度不足 |
| **压缩算法** | LZ77, Reed-Solomon | 无 | 核心技术缺失 |
| **代码规模** | 成熟项目 | 80,720行 | 架构基础好 |

## 技术移植策略

### 架构映射
- **DwgReader/DwgWriter** → 扩展现有 `DwgFacade`
- **BitStream 系统** → 扩展现有 `bitstream.rs`
- **句柄管理** → 利用 Rust 的类型系统
- **内存模型** → Arena 分配器 + EntityId

### 关键挑战
1. **位级数据处理**: DWG 的核心是精确的位操作
2. **版本差异**: 12个主要版本，架构差异巨大
3. **压缩算法**: LZ77 + Reed-Solomon 实现复杂
4. **引用循环**: Rust 借用检查器 vs CAD 对象模型

## 分阶段实施计划

### 阶段 1: 基础架构 (4-6周)
- BitStream Writer 扩展
- CRC/校验系统
- 基础文件头写入
- 简单实体序列化

### 阶段 2: 核心功能 (6-8周)
- 句柄系统实现
- LZ77 压缩算法
- 完整对象序列化
- 分页系统

### 阶段 3: 版本扩展 (4-6周)
- 早期版本支持 (AC1012-AC1015)
- 现代版本支持 (AC1024-AC1032)
- 版本工厂优化

### 阶段 4: 质量保证 (3-4周)
- 全面测试套件
- 性能优化
- 文档完善

**总时间: 17-24周 (4-6个月)**

## 优先级排序

### 高优先级 (必须)
1. BitStream Writer
2. CRC 校验系统
3. 句柄管理
4. 基础对象序列化

### 中优先级 (核心)
1. LZ77 压缩
2. 分页系统
3. 版本适配
4. 实体扩展

### 低优先级 (增强)
1. 性能优化
2. 高级特性
3. 工具集成

## Rust 特定解决方案

### 内存管理
```rust
// Arena 分配器 + 类型安全引用
pub struct EntityArena {
    entities: Arena<Entity>,
    handles: HashMap<Handle, EntityId>,
}

pub struct EntityId<T: EntityType> {
    index: usize,
    phantom: PhantomData<T>,
}
```

### 错误处理
```rust
use thiserror::Error;

#[derive(Error, Debug)]
pub enum DwgWriteError {
    #[error("Bit alignment error at position {position}")]
    BitAlignment { position: u64 },
    
    #[error("CRC mismatch: expected {expected}, got {actual}")]
    CrcMismatch { expected: u32, actual: u32 },
}
```

## 测试策略

### 测试金字塔
```
        /\
       /  \  集成测试 (往返测试)
      /    \
     /      \ 黄金样例测试
    /________\
   /            \ 单元测试 (每个组件)
```

### 关键测试
- 30+ 黄金样例往返测试
- 性能基准对比
- 大文件处理验证
- 跨版本兼容性

## 风险缓解

| 风险 | 缓解措施 |
|------|----------|
| 格式复杂性 | 参考ACadSharp源码，渐进实现 |
| 版本兼容性 | 版本化设计，分阶段支持 |
| 性能要求 | 基准测试，持续优化 |
| 团队经验 | 技术调研，代码审查 |

## 资源需求

### 人员配置
- 资深 Rust 工程师: 1-2人
- CAD 格式专家: 1人  
- 测试工程师: 0.5-1人
- 文档工程师: 0.5人

### 技术资源
- 测试数据: 各版本 DWG 文件 (500MB-1GB)
- 开发环境: Rust 1.70+, cross-compilation tools
- CI/CD: GitHub Actions

## 成功指标

- ✅ 支持所有 DWG 版本 (AC1012-AC1032)
- ✅ 完整读写能力 (往返兼容)
- ✅ 测试覆盖率 > 90%
- ✅ 性能达到 ACadSharp 的 80%+
- ✅ 与 AutoCAD 完全兼容

## 下一步行动

1. **立即启动** (1周): 技术调研，环境准备
2. **原型验证** (2周): 最小可行原型
3. **团队组建** (并行): 确定成员，明确分工
4. **里程碑设置** (1周): 详细计划，风险评估

## 预期成果

完成后，zcad-rs 将成为 Rust 生态系统中最完整的 CAD 文件处理库，为：
- CAD 软件开发提供 Rust 解决方案
- 工程设计工具的现代化改造
- Rust 在工业软件领域的应用奠定基础

**总投资**: 17-24周时间 + 3-4人团队
**回报**: 为 Rust 生态填补关键空白，推动工业软件现代化