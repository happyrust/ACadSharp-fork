# ACADSHARP 项目知识库

**生成时间**: 2026-01-02 12:38:18 AM
**提交哈希**: (当前分支)
**分支**: (当前分支)

## 概述
ACadSharp 是一个 C# .NET CAD 文件处理库，支持 DXF/DWG 格式的读写和转换。核心采用特性驱动的元数据映射系统，支持多框架（.NET 5.0-9.0, .NET Framework 4.8, .NET Standard 2.0/2.1）。

## 结构
```
ACadSharp/
├── src/                    # 核心源代码
│   ├── ACadSharp/         # 主类库
│   ├── ACadSharp.Tests/   # 单元测试
│   └── CSUtilities/       # 共享工具库
├── samples/               # 测试样本文件
├── docs/                  # 传统文档
├── llmdoc/               # 结构化文档系统
├── ACadSharp.WebApi/     # Web API (根目录，非标准)
├── ACadSharp.WebConverter/ # 转换逻辑 (根目录，非标准)
└── tools/                # 开发工具
```

## 查找位置
| 任务 | 位置 | 备注 |
|------|------|------|
| 核心实体类 | `src/ACadSharp/Entities/` | 144个实体文件 |
| IO读写逻辑 | `src/ACadSharp/IO/` | DXF/DWG处理核心 |
| 测试代码 | `src/ACadSharp.Tests/` | 镜像主项目结构 |
| 配置文件 | `.editorconfig`, `src/Directory.Build.props` | 代码风格和构建配置 |
| 样本数据 | `samples/` | 自动化测试数据源 |
| 文档系统 | `llmdoc/` | 结构化项目文档 |

## 约定
- **命名空间与文件夹路径必须匹配**
- **私有字段使用 `_camelCase` 前缀**
- **访问类成员必须使用 `this.` 限定符**
- **使用 Tab 缩进（4字符宽）**
- **特性驱动的 DXF 映射**: `[DxfName]`, `[DxfCodeValue]`

## 反模式 (本项目)
- **禁止删除系统表项**: 图层"0"、模型空间、图纸空间
- **禁止跨文档引用**: 实体和反应器必须在同一文档
- **禁止设置零缩放因子**: Insert 块的缩放因子不能为0
- **禁止省略访问修饰符**: 必须显式指定 public/private 等

## 独特风格
- **元数据驱动序列化**: 通过反射扫描特性生成 DxfMap
- **真实对象驱动测试**: 不使用 Mock 框架，使用真实 CAD 对象
- **多框架支持**: 同时支持 .NET Core 和 .NET Framework
- **程序集签名**: 使用 ACadSharp.snk 密钥文件

## 命令
```bash
# 构建
dotnet build src/ACadSharp.sln

# 测试
dotnet test src/ACadSharp.Tests/

# 子模块初始化
git submodule update --init --recursive

# 文档生成
dotnet netdocgen
```

## 注意事项
- 项目依赖外部子模块，构建前必须初始化
- 根目录下的 Web 项目位置非标准，建议移至 src/
- 存在文档冗余（docs/ 和 llmdoc/），优先使用 llmdoc/
- CI 使用自定义环境变量文件 github.env