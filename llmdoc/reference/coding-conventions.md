# ACadSharp 编码规范

## 1. 核心概述

ACadSharp 项目遵循 .NET 标准编码约定，由 `.editorconfig` 配置强制执行。规范涵盖命名、代码风格、项目组织和 DXF 映射特有的约定。

---

## 2. 命名约定

### 私有字段
- **格式**: `_camelCase` (下划线前缀 + 驼峰命名)
- **规则**: `dotnet_naming_rule.private_field_should_be_begins_with__`
- **示例**: `_document`, `_entityHandle`, `_layerData`

### 私有/受保护方法
- **格式**: `camelCase` (无前缀)
- **规则**: `dotnet_naming_rule.private_method_should_be_camelcase`
- **示例**: `parseEntity()`, `validateProperty()`, `setupCollections()`

### 接口
- **格式**: `IPascalCase` (I 前缀)
- **规则**: `dotnet_naming_rule.interface_should_be_begins_with_i`
- **示例**: `ICadCollection<T>`, `IEntity`, `ICadReader`

### 类、结构体、枚举
- **格式**: `PascalCase`
- **规则**: `dotnet_naming_rule.types_should_be_pascal_case`
- **示例**: `Entity`, `Circle`, `DimensionStyle`

### 公共成员 (属性、事件、方法)
- **格式**: `PascalCase`
- **规则**: `dotnet_naming_rule.non_field_members_should_be_pascal_case`
- **示例**: `public Color Color { get; set; }`, `public void AddEntity(...)`

---

## 3. 代码风格

### 缩进与行尾
- **制表符**: 使用 Tab (相当于 4 个空格) — `indent_style = tab`, `tab_width = 4`
- **行尾**: CRLF (`end_of_line = crlf`)

### 表达式体成员
- **方法**: `false` — 不使用表达式体 (`csharp_style_expression_bodied_methods = false`)
- **构造函数**: `false` — 不使用表达式体 (`csharp_style_expression_bodied_constructors = false`)
- **属性**: `true` — 优先使用表达式体 (`csharp_style_expression_bodied_properties = true`)
  - 例: `public int Value => _value;`
- **索引器**: `true` — 优先使用表达式体
- **访问器**: `when_on_single_line` — 单行时使用表达式体
- **Lambda**: `true` — 优先使用表达式体

### 括号与空格
- **使用括号**: `csharp_prefer_braces = true` — 始终用括号包围代码块
- **二元运算符前后**: `before_and_after` — `a + b` (不是 `a +b` 或 `a+ b`)
- **逗号后**: 一个空格 (`csharp_space_after_comma = true`) — `method(a, b, c)`

### Using 指令与命名空间
- **Placement**: `outside_namespace` — Using 指令放在命名空间外
- **简化 using**: `true` — 使用简化 using 语句 (`csharp_prefer_simple_using_statement = true`)
- **块作用域**: `block_scoped` — 使用块作用域命名空间 (`csharp_style_namespace_declarations = block_scoped`)

### 现代 C# 特性
- **Null 检查**: 优先使用 `is null` 检查 (`dotnet_style_prefer_is_null_check_over_reference_equality_method = true`)
- **对象初始化器**: 优先使用 (`dotnet_style_object_initializer = true`)
- **集合初始化器**: 优先使用 (`dotnet_style_collection_initializer = true`)
- **元组**: 优先使用显式元组名称 (`dotnet_style_explicit_tuple_names = true`)
- **模式匹配**: 优先使用 (`csharp_style_prefer_pattern_matching = true`)
- **Switch 表达式**: 优先使用 (`csharp_style_prefer_switch_expression = true`)

---

## 4. 项目组织

### 命名空间与文件夹
- **原则**: 命名空间路径必须与文件夹路径匹配
- **规则**: `dotnet_style_namespace_match_folder = true`
- **示例**:
  - 文件 `src/ACadSharp/Entities/Circle.cs` 使用 `namespace ACadSharp.Entities { }`
  - 文件 `src/ACadSharp/IO/DXF/DxfReader.cs` 使用 `namespace ACadSharp.IO.DXF { }`

### 文件组织
- **一个类一个文件**: 每个公共类型必须单独存放在对应文件中
- **嵌套类**: 可在同一文件中定义（仅在逻辑相关时）
- **示例**: `GeoData.cs` 包含 `GeoData` 类及其嵌套类 `GeoMeshPoint`、`GeoMeshFace`

### 访问修饰符
- **原则**: 明确指定访问修饰符 (`dotnet_style_require_accessibility_modifiers = for_non_interface_members`)
- **适用**: 除接口成员外的所有成员必须显式标注 `public`、`private`、`protected` 等

---

## 5. 特性系统与元数据 (DXF 映射)

### DXF 特性标注
ACadSharp 采用**特性-驱动映射**系统，自动关联 C# 属性与 DXF 代码。

#### 类级特性
- **`[DxfName("CLASS_NAME")]`** — 定义 DXF 实体/类的名称
  - 示例: `[DxfName("CIRCLE")]` 用于圆形实体

- **`[DxfSubClass("AcDbCircle")]`** — 定义 DXF 子类标记
  - 示例: `[DxfSubClass("AcDbEntity")]` 用于基础实体类

#### 属性级特性
- **`[DxfCodeValue(10, 20, 30)]`** — 属性对应的 DXF 代码（可接收多个代码值）
  - 示例: `[DxfCodeValue(40)] public double Radius { get; set; }`
  - 多代码: `[DxfCodeValue(10, 20, 30)] public XYZ Center { get; set; }`

- **`[DxfCollectionCodeValueAttribute]`** — 用于集合属性的特性
  - 示例: 在包含多个子实体的属性上使用

#### 映射机制
1. **DxfMap.Create(Type)** — 通过反射扫描类型树
2. **属性收集** — 查找所有带 `[DxfCodeValue]` 的属性
3. **映射构建** — 为每个代码创建 `DxfProperty` 实例，组织成 `DxfMap > SubClasses > DxfClassMap`
4. **缓存策略** — 使用 `ConcurrentDictionary` 缓存已生成的映射，调用 `ClearCache()` 清理

### 使用示例
```csharp
[DxfName("CIRCLE")]
[DxfSubClass("AcDbCircle")]
public class Circle : Entity
{
	[DxfCodeValue(10, 20, 30)]
	public XYZ Center { get; set; }

	[DxfCodeValue(40)]
	public double Radius { get; set; }
}
```

---

## 6. XML 文档注释

### 禁用 CS1591 警告
- **规则**: `dotnet_diagnostic.CS1591.severity = none`
- **说明**: XML 文档注释警告被禁用
- **影响**: 公开的类型/成员无需强制添加 `///` 注释

---

## 7. 属性与字段管理

### Readonly 字段
- **偏好**: 使用 readonly 字段而非可变字段 (`dotnet_style_readonly_field = true`)

### 自动属性
- **偏好**: 优先使用自动属性 (`dotnet_style_prefer_auto_properties = true`)
- **示例**: `public string Name { get; set; }` 而非手工实现

### 字段限定符
- **使用**: 字段访问时显式使用 `this.` 限定符 (`dotnet_style_qualification_for_field = true`)
- **示例**: `this._document = doc;` 而非 `_document = doc;`

---

## 8. 多框架支持

### 目标框架
ACadSharp 支持多个 .NET 版本（见 `ACadSharp.csproj`）：
- `net5.0`, `net6.0`, `net7.0`, `net8.0`, `net9.0`
- `net48` (Framework)
- `netstandard2.0`, `netstandard2.1`

### 条件编译与依赖
- 共享项目 `CSMath` 和 `CSUtilities` 通过 `Import Project` 指令集成
- 条件包: `System.Memory`, `System.Text.Encoding.CodePages`

---

## 9. 关键约定总结

| 类别 | 约定 | 示例 |
|------|------|------|
| 私有字段 | `_camelCase` | `_handle`, `_owner` |
| 私有方法 | `camelCase` | `parseEntity()` |
| 接口 | `IPascalCase` | `IEntity`, `ICadCollection` |
| 类/结构体 | `PascalCase` | `Entity`, `Circle` |
| 公共成员 | `PascalCase` | `LayerName`, `GetBounds()` |
| DXF 特性 | `[DxfName]`, `[DxfCodeValue]` | 映射元数据 |
| 缩进 | Tab (4 空格) | 一致性风格 |
| 行尾 | CRLF | Windows 兼容 |

---

## 10. 源代码位置

- **EditorConfig**: `/.editorconfig` — 强制执行规范
- **项目配置**: `/src/ACadSharp/ACadSharp.csproj` — 多框架配置
- **代码结构**: `/llmdoc/agent/scout-code-structure.md` — 详细的组织模式
