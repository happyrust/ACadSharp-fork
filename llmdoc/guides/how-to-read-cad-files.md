# 如何读取 CAD 文件

读取 DXF 和 DWG 文件的完整指南，包括基本用法、配置选项和错误处理最佳实践。

## 1. 基础读取（快速开始）

### 1.1 读取 DWG 文件

```csharp
using ACadSharp;
using ACadSharp.IO;

// 最简单的方法
CadDocument doc = DwgReader.Read("file.dwg");

// 遍历实体
foreach (var entity in doc.Entities)
{
    Console.WriteLine($"{entity.ObjectName}: {entity.Handle}");
}
```

参考：`src/ACadSharp/IO/DWG/DwgReader.cs:Read()`

### 1.2 读取 DXF 文件

```csharp
// DXF 读取（支持 ASCII 和二进制格式自动检测）
CadDocument doc = DxfReader.Read("file.dxf");

// 获取所有层
foreach (var layer in doc.Layers)
{
    Console.WriteLine($"Layer: {layer.Name}, Color: {layer.Color}");
}
```

参考：`src/ACadSharp/IO/DXF/DxfReader.cs:Read()`

## 2. 读取器配置

### 2.1 DWG 读取器配置

```csharp
// 创建配置对象
var config = new DwgReaderConfiguration
{
    // 启用 CRC32 校验（默认 false，启用会减速）
    CrcCheck = false,

    // 跳过摘要信息以加快速度（默认 true）
    ReadSummaryInfo = true
};

// 使用配置读取
var reader = new DwgReader("file.dwg", config);
CadDocument doc = reader.Read();
```

关键配置选项：
- `CrcCheck`: 验证数据完整性，但显著增加读取时间（用于完整性验证时启用）
- `ReadSummaryInfo`: 跳过 AC1018+ 版本的摘要信息以提高速度（默认跳过）

参考：`src/ACadSharp/IO/DWG/DwgReaderConfiguration.cs`

### 2.2 DXF 读取器配置

```csharp
var config = new DxfReaderConfiguration();
var reader = new DxfReader("file.dxf", config);
CadDocument doc = reader.Read();
```

DXF 读取器支持 ASCII 和二进制格式的自动检测，无需特殊配置。

参考：`src/ACadSharp/IO/DXF/DxfReaderConfiguration.cs`

## 3. 通知系统的使用

### 3.1 监听读取过程中的通知

```csharp
var reader = new DwgReader("file.dwg");

// 订阅通知事件
reader.OnNotification += (sender, e) =>
{
    switch (e.NotificationType)
    {
        case NotificationType.NotImplemented:
            Console.WriteLine($"[未实现] {e.Message}");
            break;
        case NotificationType.Warning:
            Console.WriteLine($"[警告] {e.Message}");
            if (e.Exception != null)
                Console.WriteLine($"详情: {e.Exception.Message}");
            break;
        case NotificationType.Error:
            Console.WriteLine($"[错误] {e.Message}");
            break;
        case NotificationType.NotSupported:
            Console.WriteLine($"[不支持] {e.Message}");
            break;
    }
};

// 读取文件
CadDocument doc = reader.Read();
```

参考：`src/ACadSharp/IO/NotificationEventHandler.cs`

### 3.2 通知类型说明

| 类型 | 值 | 说明 | 处理 |
|------|-----|------|------|
| NotImplemented | -1 | 功能未在代码中实现 | 信息性，忽略 |
| None | 0 | 无通知 | 无 |
| NotSupported | 1 | CAD 文件中的功能不支持 | 信息性，忽略 |
| Warning | 2 | 警告信息（可能影响结果准确性） | 记录日志 |
| Error | 3 | 错误信息（严重问题） | 抛出异常 |

## 4. 部分读取

### 4.1 仅读取文件头

```csharp
var reader = new DwgReader("file.dwg");

// 仅读取文件头，快速获取版本和基本信息
var header = reader.ReadHeader();

Console.WriteLine($"CAD 版本: {header.Version}");
Console.WriteLine($"AutoCAD 版本: {header.AcadVer}");
```

参考：`src/ACadSharp/IO/ICadReader.cs:ReadHeader()`

### 4.2 跳过耗时操作

```csharp
// DWG: 跳过摘要信息
var config = new DwgReaderConfiguration
{
    ReadSummaryInfo = false
};
var reader = new DwgReader("large_file.dwg", config);
CadDocument doc = reader.Read();  // 更快

// DXF: 无特殊选项，但支持流式读取
```

## 5. 数据访问

### 5.1 访问文档的基本内容

```csharp
CadDocument doc = DwgReader.Read("file.dwg");

// 获取文档版本
Console.WriteLine($"版本: {doc.Header.Version}");

// 访问所有表
Console.WriteLine($"层数: {doc.Layers.Count}");
Console.WriteLine($"线型数: {doc.LineTypes.Count}");

// 访问实体
Console.WriteLine($"实体数: {doc.Entities.Count}");

// 访问块
Console.WriteLine($"块数: {doc.BlockRecords.Count}");
```

关键属性位置：
- Header：`src/ACadSharp/CadDocument.cs:Header`
- Entities：`src/ACadSharp/CadDocument.cs:ModelSpace.Entities`
- BlockRecords：`src/ACadSharp/CadDocument.cs:BlockRecords`

### 5.2 遍历实体

```csharp
CadDocument doc = DwgReader.Read("file.dwg");

// 遍历模型空间的所有实体
foreach (var entity in doc.Entities)
{
    if (entity is Line line)
    {
        Console.WriteLine($"Line: ({line.StartPoint}) to ({line.EndPoint})");
    }
    else if (entity is Circle circle)
    {
        Console.WriteLine($"Circle: Center={circle.Center}, Radius={circle.Radius}");
    }
}

// 按实体类型过滤
var circles = doc.Entities.OfType<Circle>();
var lines = doc.Entities.OfType<Line>();
```

参考：`src/ACadSharp/CadDocument.cs:Entities`

### 5.3 访问块内的实体

```csharp
CadDocument doc = DwgReader.Read("file.dwg");

// 遍历所有块（除了模型空间）
foreach (var blockRecord in doc.BlockRecords)
{
    if (blockRecord.Name.StartsWith("*"))
        continue;  // 跳过系统块

    Console.WriteLine($"块: {blockRecord.Name}");

    // 遍历块内的实体
    foreach (var entity in blockRecord.Entities)
    {
        Console.WriteLine($"  - {entity.ObjectName}");
    }
}
```

参考：`src/ACadSharp/Tables/BlockRecord.cs:Entities`

### 5.4 访问块属性和定义

```csharp
foreach (var blockRecord in doc.BlockRecords)
{
    // 获取块的物理实体对象
    Block blockEntity = blockRecord.BlockEntity;

    Console.WriteLine($"块基点: {blockEntity.BasePoint}");
    Console.WriteLine($"块标志: {blockEntity.Flags}");

    // 获取属性定义
    foreach (var attrDef in blockRecord.AttributeDefinitions)
    {
        Console.WriteLine($"属性: {attrDef.Tag}");
    }
}
```

参考：`src/ACadSharp/Blocks/Block.cs` 和 `src/ACadSharp/Tables/BlockRecord.cs`

## 6. 错误处理

### 6.1 常见错误和处理

```csharp
try
{
    CadDocument doc = DwgReader.Read("file.dwg");
}
catch (CadNotSupportedException)
{
    Console.WriteLine("不支持的 CAD 文件版本");
}
catch (FileNotFoundException)
{
    Console.WriteLine("文件不存在");
}
catch (IOException ex)
{
    Console.WriteLine($"文件读取错误: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"其他错误: {ex.Message}");
}
```

参考：`src/ACadSharp/Exceptions/`

### 6.2 通知系统作为错误检测机制

```csharp
var reader = new DxfReader("file.dxf");
var warnings = new List<string>();
var errors = new List<string>();

reader.OnNotification += (sender, e) =>
{
    if (e.NotificationType == NotificationType.Warning)
        warnings.Add(e.Message);
    else if (e.NotificationType == NotificationType.Error)
        errors.Add(e.Message);
};

CadDocument doc = reader.Read();

if (warnings.Count > 0)
    Console.WriteLine($"读取过程中出现 {warnings.Count} 个警告");
if (errors.Count > 0)
    Console.WriteLine($"读取过程中出现 {errors.Count} 个错误");
```

## 7. 版本相关的处理

### 7.1 检查文件版本

```csharp
CadDocument doc = DwgReader.Read("file.dwg");

// 检查 CAD 版本
ACadVersion version = doc.Header.Version;

if (version >= ACadVersion.AC1021)
{
    Console.WriteLine("R2007 或更新的版本（支持 UTF-8）");
}
else if (version >= ACadVersion.AC1018)
{
    Console.WriteLine("R2004-2006 版本（支持压缩）");
}
else if (version >= ACadVersion.AC1012)
{
    Console.WriteLine("R13 或更新版本（基础格式）");
}
```

参考：`src/ACadSharp/ACadVersion.cs`

### 7.2 版本兼容性

- **MC0_0 到 AC1009**: 仅支持 DXF 读取
- **AC1012 到 AC1032**: 支持 DXF 和 DWG 读取
- **AC1021+**: 自动使用 UTF-8 编码

## 8. 高级用法

### 8.1 使用流而不是文件路径

```csharp
using (FileStream stream = File.OpenRead("file.dwg"))
{
    var reader = new DwgReader(stream);
    CadDocument doc = reader.Read();
}

// 或使用内存流
MemoryStream memStream = new MemoryStream(fileBytes);
CadDocument doc = DwgReader.Read(memStream);
```

参考：`src/ACadSharp/IO/DWG/DwgReader.cs:Read(Stream)`

### 8.2 获取文件预览

```csharp
var reader = new DwgReader("file.dwg");

// 仅读取文件头以获取预览
var header = reader.ReadHeader();

// 获取预览图像（DWG 格式支持）
if (reader is DwgReader dwgReader)
{
    DwgPreview preview = dwgReader.GetPreview();
    if (preview?.ImageData != null)
    {
        // 保存预览图像
        File.WriteAllBytes("preview.bmp", preview.ImageData);
    }
}
```

参考：`src/ACadSharp/DwgPreview.cs` 和 `src/ACadSharp/IO/DWG/DwgReader.cs`

### 8.3 提取特定类型的实体

```csharp
CadDocument doc = DwgReader.Read("file.dwg");

// 查找所有文本实体
var texts = doc.Entities.OfType<Text>();
foreach (var text in texts)
{
    Console.WriteLine($"文本: {text.Value} @ {text.InsertPoint}");
}

// 查找所有 Insert (块引入)
var inserts = doc.Entities.OfType<Insert>();
foreach (var insert in inserts)
{
    Console.WriteLine($"块: {insert.Name} @ {insert.InsertPoint}");
}

// 查找特定图层的实体
var redEntities = doc.Entities.Where(e => e.Layer.Name == "Red");
```

## 9. 最佳实践

1. **总是处理通知**: 订阅 OnNotification 事件，捕获警告和错误。
2. **检查版本**: 在进行版本特定操作前检查 doc.Header.Version。
3. **使用 using 语句**: 处理流时使用 using，确保资源释放。
4. **处理异常**: 捕获 CadNotSupportedException 和 IOException。
5. **跳过耗时操作**: DWG 读取时考虑设置 ReadSummaryInfo = false。
6. **缓存 CadDocument**: 如果多次访问同一文件，缓存读取结果。
7. **验证数据**: 访问实体属性前检查是否为 null。

## 10. 常见场景示例

### 10.1 批量读取多个文件

```csharp
string[] files = Directory.GetFiles(".", "*.dwg");

foreach (var file in files)
{
    try
    {
        var doc = DwgReader.Read(file);
        Console.WriteLine($"{file}: {doc.Entities.Count} 个实体");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{file}: 读取失败 - {ex.Message}");
    }
}
```

### 10.2 统计文档内容

```csharp
CadDocument doc = DwgReader.Read("file.dwg");

// 统计实体类型
var entityCounts = doc.Entities
    .GroupBy(e => e.ObjectName)
    .ToDictionary(g => g.Key, g => g.Count());

foreach (var kvp in entityCounts)
{
    Console.WriteLine($"{kvp.Key}: {kvp.Value}");
}

// 统计图层
Console.WriteLine($"使用的图层: {doc.Layers.Count}");
foreach (var layer in doc.Layers)
{
    var layerEntities = doc.Entities.Where(e => e.Layer == layer).Count();
    Console.WriteLine($"  {layer.Name}: {layerEntities} 个实体");
}
```

### 10.3 导出基本信息

```csharp
CadDocument doc = DwgReader.Read("file.dwg");

var info = new
{
    Version = doc.Header.Version,
    EntityCount = doc.Entities.Count,
    LayerCount = doc.Layers.Count,
    BlockCount = doc.BlockRecords.Count,
    Author = doc.Header.Author,
    Created = doc.Header.CreateTime
};

var json = System.Text.Json.JsonSerializer.Serialize(info);
Console.WriteLine(json);
```

