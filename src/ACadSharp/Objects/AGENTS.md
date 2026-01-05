# ACadSharp Objects 核心知识库

## OVERVIEW
ACadSharp 对象系统核心，负责非图形对象（Non-Graphical Objects）的定义、层次结构管理及基于反应器（Reactor）的依赖跟踪机制。

## WHERE TO LOOK
- **基类与基础**:
	- `NonGraphicalObject.cs`: 所有非图形对象的抽象基类，支持命名（`INamedCadObject`）和事件通知。
	- `CadDictionary.cs`: 通用键值对容器，用于存储布局、样式和自定义对象，是 CAD 扩展机制的核心。
- **核心对象实现**:
	- `Group.cs`: 实现实体成组功能，是理解 Reactor 模式在依赖跟踪中应用的示例。
	- `Layout.cs`: 定义打印布局和页面设置，链接块记录与打印参数。
	- `XRecord.cs`: 通用数据存储对象（文件名存在拼写错误 `XRecrod.cs`），用于保存任意应用程序元数据。
- **功能模块**:
	- `Collections/`: 强类型集合实现（如 `LayoutCollection`），负责对象的所有权管理和事件冒泡。
	- `Evaluations/`: 包含动态块（Dynamic Blocks）的图评估逻辑，处理块属性的动态计算。

## CONVENTIONS
- **继承准则**: 纯逻辑或元数据对象应继承 `NonGraphicalObject`；若涉及图形表现，则应继承 `Entity`。
- **元数据驱动映射**:
	- 类级别：必须标注 `[DxfName]` 指定 DXF 对象名称，`[DxfSubClass]` 指定子类标记。
	- 属性级别：使用 `[DxfCodeValue]` 映射 DXF 组码，引用类型需配合 `DxfReferenceType.Handle`。
- **所有权模型 (Ownership)**:
	- 严格单一所有权：每个对象必须且只能有一个 `Owner`（通常为 `CadDictionary` 或集合）。
	- 自动绑定：在 `Add()` 操作中应自动设置 `value.Owner = this`，并处理 `AssignDocument` 逻辑。
- **反应器模式 (Reactor Pattern)**:
	- 用于建立反向引用（如从 Entity 指向所属的 Group）。
	- 使用 `target.AddReactor(this)` 注册依赖，确保在删除或移动对象时能同步感知。
	- 文档隔离：反应器链接不能跨越 `CadDocument` 边界。
- **克隆策略**:
	- 必须重写 `Clone()`，调用 `base.Clone()` 后重置 `Handle`、`Document` 和 `Owner`。
	- 内部集合（如 `_entries`）必须重新实例化，防止多个克隆体共享同一个状态。

## ANTI-PATTERNS
- **跨文档反应器**: 禁止在属于不同 `CadDocument` 的对象之间建立 Reactor 链接，否则会导致序列化错误和内存泄漏。
- **手动 Handle 操作**: 禁止在业务逻辑中直接修改 `Handle`；该属性仅由文档在注册对象时通过 `HandleControl` 自动管理。
- **匿名字典项**: 严禁将 `Name` 为空或仅包含空白字符的 `NonGraphicalObject` 添加到 `CadDictionary` 中。
- **硬编码 Entry 名称**: 访问系统标准字典项时，必须使用 `CadDictionary` 中的常量（如 `AcadLayout`）而非字符串字面量。
- **不完整的资源清理**: 在 `UnassignDocument` 过程中未调用 `CleanReactors()` 或未解绑事件监听器，导致对象无法被 GC 回收。
