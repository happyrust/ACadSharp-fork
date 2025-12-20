# 支持的 CAD 版本参考

ACadSharp 支持的 CAD 文件版本详细说明及其读写能力矩阵。

## 1. 核心总结

ACadSharp 支持从最早的 AutoCAD R1.1（MC0_0）到最新的 AutoCAD R2024（AC1032）的 CAD 文件格式。对于 DXF 格式，所有版本都支持读取；对于 DWG 格式，从 R13（AC1012）开始支持读写。库采用自动版本检测机制，无需手动指定版本即可读取文件。

## 2. 版本支持矩阵

### 2.1 完整版本支持表

| 版本代码 | 版本名称 | AutoCAD 版本 | DXF 读 | DXF 写 | DWG 读 | DWG 写 | 说明 |
|---------|---------|-------------|:----:|:----:|:----:|:----:|------|
| MC0_0 | R1.1 | AutoCAD 1.1 | ✓ | ✗ | ✗ | ✗ | 历史版本，仅读取 |
| AC1_2 | R1.2 | AutoCAD 1.2 | ✓ | ✗ | ✗ | ✗ | 历史版本，仅读取 |
| AC1_4 | R1.4 | AutoCAD 1.4 | ✓ | ✗ | ✗ | ✗ | 历史版本，仅读取 |
| AC1_50 | R2.0 | AutoCAD 2.0 | ✓ | ✗ | ✗ | ✗ | 历史版本，仅读取 |
| AC2_10 | R2.10 | AutoCAD 2.10 | ✓ | ✗ | ✗ | ✗ | 历史版本，仅读取 |
| AC1002 | R2.5 | AutoCAD 2.5 | ✓ | ✗ | ✗ | ✗ | 历史版本，仅读取 |
| AC1003 | R2.6 | AutoCAD 2.6 | ✓ | ✗ | ✗ | ✗ | 历史版本，仅读取 |
| AC1004 | R9 | AutoCAD R9 | ✓ | ✗ | ✗ | ✗ | 历史版本，仅读取 |
| AC1006 | R10 | AutoCAD R10 | ✓ | ✗ | ✗ | ✗ | 历史版本，仅读取 |
| **AC1009** | **R11/R12** | **AutoCAD R11/R12** | **✓** | **✗** | **✗** | **✗** | **AC1009 特殊二进制格式，仅读取** |
| **AC1012** | **R13** | **AutoCAD R13** | **✓** | **✓** | **✓** | **✓** | **最早的完全支持版本** |
| **AC1014** | **R14** | **AutoCAD R14** | **✓** | **✓** | **✓** | **✓** | **基础功能，高兼容性** |
| **AC1015** | **R2000** | **AutoCAD 2000** | **✓** | **✓** | **✓** | **✓** | **推荐用于广泛兼容性** |
| **AC1018** | **R2004-2006** | **AutoCAD 2004/2005/2006** | **✓** | **✓** | **✓** | **✓** | **支持 LZ77 压缩** |
| **AC1021** | **R2007-2009** | **AutoCAD 2007/2008/2009** | **✓** | **✓** | **✓** | **✗** | **UTF-8 支持，DWG 写入不完整** |
| **AC1024** | **R2010-2012** | **AutoCAD 2010/2011/2012** | **✓** | **✓** | **✓** | **✓** | **完全支持** |
| **AC1027** | **R2013-2017** | **AutoCAD 2013/2014/2015/2016/2017** | **✓** | **✓** | **✓** | **✓** | **完全支持** |
| **AC1032** | **R2018+** | **AutoCAD 2018/2019/2020/2021/2022/2023/2024** | **✓** | **✓** | **✓** | **✓** | **最新格式，完全支持** |

### 2.2 推荐版本选择

**用途** | **推荐版本** | **替代版本** | **说明** |
|--------|----------|----------|--------|
| 最大兼容性 | AC1015 (R2000) | AC1014 (R14) | 支持大多数 AutoCAD 版本 |
| 现代格式 | AC1032 (R2018+) | AC1027 (R2013-2017) | 最新特性支持 |
| 压缩文件 | AC1018 (R2004-2006) | AC1024 (R2010-2012) | LZ77 压缩，较小文件 |
| 国际化 | AC1021 (R2007-2009) 或更新 | AC1024+ | UTF-8 编码支持 |
| DWG 写入 | AC1024+ | 避免 AC1021 | AC1021 写入支持有限 |

## 3. 版本特定的限制和注意事项

### 3.1 AC1009 (R11/R12) 特殊处理

**特点:**
- 采用特殊的二进制格式，不同于后续版本
- 仅支持读取，不支持写入
- 使用 `DxfBinaryReaderAC1009` 专用读取器

**限制:**
- 许多新型实体和特性不存在
- 块、属性、引入功能基础
- 尺寸标注功能有限

**使用场景:**
- 读取旧的 R12 数据进行迁移
- 需要导出到更新版本时使用

参考：`src/ACadSharp/IO/DXF/DxfStreamReader/DxfBinaryReaderAC1009.cs`

### 3.2 AC1012-AC1015 (R13-R2000) 基础版本

**特点:**
- 完整支持读写
- 无压缩
- 直接的二进制 DWG 结构

**优势:**
- 最佳的向后兼容性
- 文件解析最直接

**限制:**
- 不支持某些现代特性（如动态块、表格实体等）
- 文件可能较大（无压缩）

**推荐场景:**
- 需要与 R13-R2000 版本的 AutoCAD 互操作
- 需要最大的兼容性范围

参考：`src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeaderAC15.cs`

### 3.3 AC1018-AC1020 (R2004-2006) 压缩版本

**新特性:**
- LZ77 压缩算法支持
- CRC32 校验
- 分页结构

**优势:**
- 文件大小显著减小（通常 30-50% 压缩率）
- 完整功能支持
- 良好的兼容性

**处理开销:**
- 压缩/解压缩增加计算时间
- CRC 校验可选但会影响读取速度

**DWG 读取配置:**
```csharp
var config = new DwgReaderConfiguration
{
    CrcCheck = false,  // 禁用 CRC 以加快速度
    ReadSummaryInfo = false  // 跳过摘要信息以加快速度
};
```

参考：
- `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeaderAC18.cs`
- `src/ACadSharp/IO/DWG/Decompressors/DwgLZ77AC18Decompressor.cs`

### 3.4 AC1021 (R2007-2009) UTF-8 过渡版本

**新特性:**
- UTF-8 编码支持（自动启用）
- Reed-Solomon (255,239) 编码
- 增强的页面映射

**编码自动处理:**
```csharp
// AC1021+ 自动使用 UTF-8
CadDocument doc = DwgReader.Read("file_2007.dwg");
// 编码自动处理，无需手动指定
```

**限制和注意事项:**
- **DWG 写入不完整**: AC1021 DWG 写入支持有限，建议使用 AC1024 或更新版本
- **DXF 读写完全支持**: DXF 格式的 AC1021 完全支持
- UTF-8 自动启用，但某些特殊字符可能需要特殊处理

**版本选择建议:**
```csharp
// 避免：
doc.Header.Version = ACadVersion.AC1021;
DwgWriter.Write("output.dwg", doc);  // 不完整的 DWG 写入

// 改为：
doc.Header.Version = ACadVersion.AC1024;  // 使用 R2010 格式
DwgWriter.Write("output.dwg", doc);
```

参考：`src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeaderAC21.cs`

### 3.5 AC1024+ (R2010+) 现代版本

**特点:**
- 完整的 DWG 读写支持
- Reed-Solomon 编码（AC1021+）
- 支持现代实体和特性（表格、多线段、动态块等）
- 优秀的性能和兼容性平衡

**版本特性:**
- **AC1024 (R2010-2012)**: 3D 实体、参数化约束基础
- **AC1027 (R2013-2017)**: 云线、网格、变量属性块
- **AC1032 (R2018+)**: 最新的 API、高级块功能

**推荐用于:**
- 新的 CAD 应用开发
- 需要现代特性支持
- 大多数现代 AutoCAD 版本兼容

参考：
- `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeaderAC21.cs` (AC1021+)
- `src/ACadSharp/IO/DWG/Decompressors/DwgLZ77AC21Decompressor.cs`

## 4. 编码和国际化

### 4.1 版本相关的编码处理

| 版本范围 | 默认编码 | UTF-8 支持 | 代码页检测 | 说明 |
|---------|--------|---------|----------|------|
| MC0_0 - AC1006 | ASCII | 无 | 无 | ASCII 文本 |
| AC1009 - AC1015 | Windows-1252 | 无 | 有（$DWGCODEPAGE） | 支持西方字符集 |
| AC1018 - AC1020 | Windows-1252 | 否 | 有 | 支持扩展字符 |
| AC1021 - AC1032 | UTF-8 | **是** | 自动 | 完整的 Unicode 支持 |

### 4.2 自动编码降级

库采用自动编码降级策略：
1. 尝试使用指定的编码（从 $DWGCODEPAGE 或版本推荐）
2. 如果失败，降级到 Windows-1252
3. 发送 Warning 通知，记录编码错误

```csharp
reader.OnNotification += (sender, e) =>
{
    if (e.NotificationType == NotificationType.Warning &&
        e.Message.Contains("Encoding"))
    {
        Console.WriteLine("编码转换降级，某些字符可能丢失");
    }
};
```

参考：`src/ACadSharp/IO/CadReaderBase.cs:getEncoding()`

## 5. 版本识别机制

### 5.1 DXF 版本识别

**DXF 版本存储位置:**
- 头部变量 `$ACADVER` 中，格式为字符串（如 "AC1032"）

**自动识别流程:**
```
1. DxfReader.getReader() 检测格式（二进制或 ASCII）
2. 读取 HEADER 章节
3. 查找 $ACADVER 变量
4. CadUtils.GetVersionFromName() 转换为 ACadVersion 枚举
5. 选择合适的读取器实现
```

参考：
- `src/ACadSharp/IO/DXF/DxfReader.cs:getReader()`
- `src/ACadSharp/CadUtils.cs:GetVersionFromName()`

### 5.2 DWG 版本识别

**DWG 版本存储位置:**
- 文件前 6 个字节，格式为 ASCII 字符串（如 "AC1032"）

**自动识别流程:**
```
1. DwgReader.readFileHeader() 打开文件
2. 读取前 6 字节（版本字符串）
3. 自动识别版本
4. DwgFileHeader.CreateFileHeader() 工厂方法创建对应版本的文件头对象
5. 根据版本调用 readFileHeaderAC15/AC18/AC21 方法
```

参考：
- `src/ACadSharp/IO/DWG/DwgReader.cs:readFileHeader()`
- `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeader.cs:CreateFileHeader()`

## 6. 源代码位置

### 6.1 版本定义和支持

- **版本枚举**: `src/ACadSharp/ACadVersion.cs` - 定义所有支持的版本常量
- **DXF 映射**: `src/ACadSharp/DxfCode.cs` - DXF 代码定义（版本中立）
- **版本工厂**: `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeader.cs:CreateFileHeader()` - 版本相关的文件头创建

### 6.2 版本相关的读取器

- **AC1009**: `src/ACadSharp/IO/DXF/DxfStreamReader/DxfBinaryReaderAC1009.cs`
- **AC1012-AC1015**: `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeaderAC15.cs`
- **AC1018-AC1020**: `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeaderAC18.cs`
- **AC1021+**: `src/ACadSharp/IO/DWG/FileHeaders/DwgFileHeaderAC21.cs`

### 6.3 版本相关的写入器

- **DWG AC1012-AC1015**: `src/ACadSharp/IO/DWG/DwgStreamWriters/DwgFileHeaderWriterAC15.cs`
- **DWG AC1018**: `src/ACadSharp/IO/DWG/DwgStreamWriters/DwgFileHeaderWriterAC18.cs`
- **DWG AC1021+**: `src/ACadSharp/IO/DWG/DwgStreamWriters/DwgFileHeaderWriterAC21.cs`

### 6.4 压缩和编码

- **LZ77 AC18 解压**: `src/ACadSharp/IO/DWG/Decompressors/DwgLZ77AC18Decompressor.cs`
- **LZ77 AC21 解压**: `src/ACadSharp/IO/DWG/Decompressors/DwgLZ77AC21Decompressor.cs`
- **CRC 校验**: `src/ACadSharp/IO/DWG/CRC32StreamHandler.cs`

## 7. 相关架构文档

- **/llmdoc/architecture/io-system-architecture.md** - 完整的 I/O 系统架构，包括版本差异处理
- **/llmdoc/guides/how-to-read-cad-files.md** - 读取文件的版本选择指南
- **/llmdoc/guides/how-to-write-cad-files.md** - 写入文件的版本选择和配置

## 8. 外部参考

- **AutoCAD DXF Reference**: https://help.autodesk.com/view/AUTOCAD/latest/ENU/ - 官方 DXF 格式规范
- **ACadSharp GitHub**: https://github.com/DomCR/ACadSharp - 项目源代码和 Issue 追踪
- **AutoCAD 版本历史**: https://en.wikipedia.org/wiki/AutoCAD#Release_history - 版本时间线

