# 如何写入 CAD 文件

写入 DXF 和 DWG 文件的完整指南，包括创建文档、配置选项和版本选择。

## 1. 基础写入（快速开始）

### 1.1 创建并保存 DWG 文件

```csharp
using ACadSharp;
using ACadSharp.IO;
using CSMath;

// 创建新的 CAD 文档
CadDocument doc = new CadDocument();

// 创建一条直线
var line = new Line
{
    StartPoint = new XYZ(0, 0, 0),
    EndPoint = new XYZ(10, 10, 0)
};

// 添加到模型空间
doc.Entities.Add(line);

// 保存为 DWG 文件
DwgWriter.Write("output.dwg", doc);
```

参考：`src/ACadSharp/IO/DWG/DwgWriter.cs:Write()`

### 1.2 创建并保存 DXF 文件

```csharp
// 创建新的 CAD 文档
CadDocument doc = new CadDocument();

// 创建圆
var circle = new Circle
{
    Center = new XYZ(5, 5, 0),
    Radius = 2.5
};

doc.Entities.Add(circle);

// 保存为 DXF 文件（自动使用 ASCII 格式）
DxfWriter.Write("output.dxf", doc);
```

参考：`src/ACadSharp/IO/DXF/DxfWriter.cs:Write()`

## 2. 版本选择

### 2.1 指定输出版本

```csharp
CadDocument doc = new CadDocument();

// 添加实体...

// 指定版本写入 DWG
doc.Header.Version = ACadVersion.AC1032;  // R2018+
DwgWriter.Write("output_2018.dwg", doc);

// 其他常见版本：
// ACadVersion.AC1027  // R2013-2017
// ACadVersion.AC1024  // R2010-2012
// ACadVersion.AC1021  // R2007-2009
// ACadVersion.AC1018  // R2004-2006
// ACadVersion.AC1015  // R2000
// ACadVersion.AC1014  // R14
// ACadVersion.AC1012  // R13
```

关键版本：
- **AC1012 (R13)**: 最早的 DWG 读写版本
- **AC1015 (R2000)**: 推荐用于广泛兼容性
- **AC1018 (R2004-2006)**: 支持压缩
- **AC1021 (R2007-2009)**: UTF-8 支持，但写入不完全支持
- **AC1024 (R2010-2012)** 和 **AC1027 (R2013-2017)**: 现代格式，推荐
- **AC1032 (R2018+)**: 最新格式

参考：`src/ACadSharp/ACadVersion.cs`

### 2.2 版本兼容性矩阵

| 版本 | 名称 | DXF 读 | DXF 写 | DWG 读 | DWG 写 |
|------|------|:----:|:----:|:----:|:----:|
| AC1012 | R13 | ✓ | ✓ | ✓ | ✓ |
| AC1014 | R14 | ✓ | ✓ | ✓ | ✓ |
| AC1015 | R2000 | ✓ | ✓ | ✓ | ✓ |
| AC1018 | R2004-2006 | ✓ | ✓ | ✓ | ✓ |
| AC1021 | R2007-2009 | ✓ | ✓ | ✓ | ✗ |
| AC1024 | R2010-2012 | ✓ | ✓ | ✓ | ✓ |
| AC1027 | R2013-2017 | ✓ | ✓ | ✓ | ✓ |
| AC1032 | R2018+ | ✓ | ✓ | ✓ | ✓ |

## 3. 写入器配置

### 3.1 DXF 写入器配置

```csharp
CadDocument doc = new CadDocument();
// 添加实体...

var config = new DxfWriterConfiguration
{
    // 是否写入可选值（某些 DXF 代码对于某些对象是可选的）
    WriteOptionalValues = true,

    // 自定义头变量集合（高级用法）
    // HeaderVariables = new List<string> { ... }
};

// 使用配置写入
var writer = new DxfWriter("output.dxf", config);
writer.Write(doc);
```

关键配置：
- `WriteOptionalValues`: 控制是否写入 DXF 代码值（默认 true）。某些情况下设置为 false 可减小文件大小。

参考：`src/ACadSharp/IO/DXF/DxfWriterConfiguration.cs`

### 3.2 DWG 写入器配置

```csharp
CadDocument doc = new CadDocument();
// 添加实体...

var config = new DwgWriterConfiguration
{
    // 由于继承自 CadWriterConfiguration，支持以下选项：
};

// 使用配置写入
var writer = new DwgWriter("output.dwg", config);
writer.Write(doc);
```

参考：`src/ACadSharp/IO/DWG/DwgWriterConfiguration.cs`

### 3.3 通用写入器配置

```csharp
var config = new DwgWriterConfiguration
{
    // 重置 DXF 类并更新计数
    ResetDxfClasses = true,

    // 更新模型空间的标注
    UpdateDimensionsInModel = true,

    // 更新块中的标注
    UpdateDimensionsInBlocks = true,

    // 写入后关闭流
    CloseStream = true,

    // 写入前进行文档验证
    ValidateOnWrite = true
};
```

参考：`src/ACadSharp/IO/CadWriterConfiguration.cs`

## 4. 输出格式选择

### 4.1 DXF 格式选择（ASCII 或二进制）

```csharp
CadDocument doc = new CadDocument();
// 添加实体...

// 方法 1: 自动选择（默认 ASCII）
DxfWriter.Write("output.dxf", doc);

// 方法 2: 显式指定流和配置
using (var stream = File.Create("output_binary.dxf"))
{
    var writer = new DxfWriter(stream);
    writer.Write(doc);
}

// 注意：DxfWriter 自动检测并使用 ASCII 格式（除非特殊配置）
```

关键点：
- **ASCII DXF**: 人类可读，文件较大，兼容性最好
- **二进制 DXF**: 文件较小，但不是所有软件都支持

### 4.2 DWG 格式（自动处理压缩和编码）

```csharp
CadDocument doc = new CadDocument();
doc.Header.Version = ACadVersion.AC1032;

// DWG 写入器自动处理：
// - AC1018+: 自动 LZ77 压缩
// - AC1021+: 自动 Reed-Solomon 编码
// - 编码: 自动检测并使用合适的编码

DwgWriter.Write("output.dwg", doc);

// 所有复杂的格式化和压缩由库自动处理
```

## 5. 添加实体

### 5.1 基本实体创建

```csharp
CadDocument doc = new CadDocument();

// 创建直线
var line = new Line
{
    StartPoint = new XYZ(0, 0, 0),
    EndPoint = new XYZ(10, 10, 0),
    Color = Color.Red,
    Layer = doc.Layers[0]  // 默认层
};
doc.Entities.Add(line);

// 创建圆
var circle = new Circle
{
    Center = new XYZ(5, 5, 0),
    Radius = 2.5,
    Color = Color.Green
};
doc.Entities.Add(circle);

// 创建文本
var text = new Text
{
    Value = "Hello CAD",
    InsertPoint = new XYZ(0, 0, 0),
    Height = 1.0
};
doc.Entities.Add(text);
```

### 5.2 添加更复杂的实体

```csharp
CadDocument doc = new CadDocument();

// 多段线（Polyline）
var polyline = new Polyline();
polyline.Vertexes.Add(new Vertex(new XYZ(0, 0, 0)));
polyline.Vertexes.Add(new Vertex(new XYZ(5, 0, 0)));
polyline.Vertexes.Add(new Vertex(new XYZ(5, 5, 0)));
doc.Entities.Add(polyline);

// 弧
var arc = new Arc
{
    Center = new XYZ(0, 0, 0),
    Radius = 3,
    StartAngle = 0,
    EndAngle = 180
};
doc.Entities.Add(arc);

// 填充
var hatch = new Hatch
{
    PatternName = "SOLID",
    Color = Color.Blue
};
// 添加边界（需要闭合路径）
doc.Entities.Add(hatch);
```

## 6. 使用图层和样式

### 6.1 创建和使用图层

```csharp
CadDocument doc = new CadDocument();

// 创建新图层
var layer = new Layer
{
    Name = "MyLayer",
    Color = Color.Yellow,
    LineType = doc.LineTypes["Continuous"]
};
doc.Layers.Add(layer);

// 使用图层
var entity = new Line
{
    StartPoint = new XYZ(0, 0, 0),
    EndPoint = new XYZ(10, 10, 0),
    Layer = layer
};
doc.Entities.Add(entity);
```

关键点：
- 所有实体必须分配一个图层
- 默认图层是 "0"（不能删除）
- 图层支持颜色、线型、线宽等属性

### 6.2 使用线型

```csharp
// 获取或创建线型
LineType dashed;
if (!doc.LineTypes.TryGetValue("Dashed", out dashed))
{
    dashed = new LineType
    {
        Name = "Dashed",
        Description = "Dashed line",
        PatternLength = 0.5
        // 段信息（如有）
    };
    doc.LineTypes.Add(dashed);
}

// 使用线型
var entity = new Line
{
    LineType = dashed,
    StartPoint = new XYZ(0, 0, 0),
    EndPoint = new XYZ(10, 0, 0)
};
doc.Entities.Add(entity);
```

### 6.3 使用文本样式

```csharp
// 获取或创建文本样式
TextStyle style;
if (!doc.TextStyles.TryGetValue("MyStyle", out style))
{
    style = new TextStyle
    {
        Name = "MyStyle",
        FontFileName = "arial.ttf",
        Height = 0  // 0 表示浮动高度
    };
    doc.TextStyles.Add(style);
}

// 使用样式
var text = new Text
{
    Value = "Styled Text",
    Style = style,
    Height = 2.5,
    InsertPoint = new XYZ(0, 0, 0)
};
doc.Entities.Add(text);
```

## 7. 创建块和块插入

### 7.1 创建块定义

```csharp
CadDocument doc = new CadDocument();

// 创建块记录
var blockRecord = new BlockRecord
{
    Name = "MyBlock"
};

// 创建块实体（物理容器）
var blockEntity = new Block
{
    Name = "MyBlock",
    BasePoint = new XYZ(0, 0, 0),
    BlockOwner = blockRecord
};
blockRecord.BlockEntity = blockEntity;

// 为块添加实体
var line = new Line
{
    StartPoint = new XYZ(0, 0, 0),
    EndPoint = new XYZ(5, 5, 0)
};
blockRecord.Entities.Add(line);

// 添加到文档
doc.BlockRecords.Add(blockRecord);
```

### 7.2 插入块

```csharp
// 获取或创建块...
BlockRecord blockRecord = doc.BlockRecords["MyBlock"];

// 创建块插入
var insert = new Insert
{
    Name = "MyBlock",
    InsertPoint = new XYZ(10, 10, 0),
    Scale = new XYZ(1, 1, 1),
    Rotation = 0
};

// 添加到模型空间
doc.Entities.Add(insert);
```

## 8. 通知和错误处理

### 8.1 监听写入过程中的通知

```csharp
CadDocument doc = new CadDocument();
// 添加实体...

var writer = new DwgWriter("output.dwg");

// 订阅通知事件
writer.OnNotification += (sender, e) =>
{
    if (e.NotificationType == NotificationType.Warning)
    {
        Console.WriteLine($"[警告] {e.Message}");
    }
    else if (e.NotificationType == NotificationType.Error)
    {
        Console.WriteLine($"[错误] {e.Message}");
    }
};

try
{
    writer.Write(doc);
}
catch (Exception ex)
{
    Console.WriteLine($"写入失败: {ex.Message}");
}
```

### 8.2 文档验证

```csharp
CadDocument doc = new CadDocument();
// 添加实体...

var config = new DwgWriterConfiguration
{
    ValidateOnWrite = true  // 启用验证
};

var writer = new DwgWriter("output.dwg", config);

try
{
    writer.Write(doc);  // 如果验证失败，会在写入前抛出异常
}
catch (Exception ex)
{
    Console.WriteLine($"文档验证或写入失败: {ex.Message}");
}
```

## 9. 使用流和内存

### 9.1 写入到流

```csharp
CadDocument doc = new CadDocument();
// 添加实体...

// 写入内存流
using (MemoryStream stream = new MemoryStream())
{
    DwgWriter.Write(stream, doc);
    byte[] bytes = stream.ToArray();
    File.WriteAllBytes("output.dwg", bytes);
}

// 或使用文件流
using (FileStream stream = File.Create("output.dwg"))
{
    DwgWriter.Write(stream, doc);
}
```

### 9.2 使用 WriteOptionalValues 优化大小

```csharp
CadDocument doc = new CadDocument();
// 添加实体...

var config = new DxfWriterConfiguration
{
    WriteOptionalValues = false  // 省略可选值，减小文件大小
};

var writer = new DxfWriter("output_compact.dxf", config);
writer.Write(doc);
```

## 10. DXF 到 DWG 转换

### 10.1 从 DXF 转换到 DWG

```csharp
// 读取 DXF
CadDocument doc = DxfReader.Read("input.dxf");

// 可选：更新版本
doc.Header.Version = ACadVersion.AC1032;

// 写入为 DWG
DwgWriter.Write("output.dwg", doc);
```

### 10.2 从 DWG 转换到 DXF

```csharp
// 读取 DWG
CadDocument doc = DwgReader.Read("input.dwg");

// 可选：更新版本
doc.Header.Version = ACadVersion.AC1015;

// 写入为 DXF
DxfWriter.Write("output.dxf", doc);
```

## 11. 设置文档元数据

### 11.1 更新文档信息

```csharp
CadDocument doc = new CadDocument();

// 设置头部属性
doc.Header.Author = "Your Name";
doc.Header.Comments = "Created by ACadSharp";
doc.Header.CreateTime = DateTime.Now;
doc.Header.UpdateTime = DateTime.Now;
doc.Header.DrawingUnits = MeasurementUnits.Millimeters;

// 添加实体...

DwgWriter.Write("output.dwg", doc);
```

### 11.2 设置摘要信息

```csharp
CadDocument doc = new CadDocument();

// 设置摘要信息（对于 DWG）
if (doc.SummaryInfo != null)
{
    doc.SummaryInfo.Author = "Your Company";
    doc.SummaryInfo.Subject = "Drawing Subject";
    doc.SummaryInfo.Comments = "Any additional information";
}

DwgWriter.Write("output.dwg", doc);
```

## 12. 最佳实践

1. **设置正确的版本**: 根据目标 AutoCAD 版本选择合适的 ACadVersion。
2. **验证文档**: 启用 ValidateOnWrite 检查文档完整性。
3. **处理通知**: 订阅 OnNotification 事件以捕获写入过程中的问题。
4. **使用图层和样式**: 充分利用 CAD 的组织和视觉属性。
5. **清理资源**: 使用 using 语句或显式关闭流。
6. **测试兼容性**: 在目标 CAD 软件中测试生成的文件。
7. **处理异常**: 捕获 Exception 并妥善处理错误。
8. **减小文件大小**: 对于 DXF，考虑 WriteOptionalValues = false。

## 13. 常见场景示例

### 13.1 创建简单的设计

```csharp
CadDocument doc = new CadDocument();
doc.Header.Version = ACadVersion.AC1032;

// 创建图层
var layer = new Layer { Name = "Design", Color = Color.Blue };
doc.Layers.Add(layer);

// 绘制几何图形
var rect1 = new Line { StartPoint = XYZ.Zero, EndPoint = new XYZ(10, 0, 0), Layer = layer };
var rect2 = new Line { StartPoint = new XYZ(10, 0, 0), EndPoint = new XYZ(10, 5, 0), Layer = layer };
var rect3 = new Line { StartPoint = new XYZ(10, 5, 0), EndPoint = new XYZ(0, 5, 0), Layer = layer };
var rect4 = new Line { StartPoint = new XYZ(0, 5, 0), EndPoint = XYZ.Zero, Layer = layer };

doc.Entities.Add(rect1);
doc.Entities.Add(rect2);
doc.Entities.Add(rect3);
doc.Entities.Add(rect4);

// 添加文本注释
var text = new Text { Value = "Rectangle", InsertPoint = new XYZ(5, 2.5, 0), Height = 0.5 };
doc.Entities.Add(text);

DwgWriter.Write("rectangle.dwg", doc);
```

### 13.2 导入 DXF 并修改

```csharp
// 读取现有 DXF
CadDocument doc = DxfReader.Read("template.dxf");

// 修改文档
foreach (var entity in doc.Entities)
{
    entity.Color = Color.Red;
}

// 添加新实体
var newLine = new Line { StartPoint = XYZ.Zero, EndPoint = new XYZ(5, 5, 0) };
doc.Entities.Add(newLine);

// 更新版本并保存
doc.Header.Version = ACadVersion.AC1032;
DwgWriter.Write("modified.dwg", doc);
```

### 13.3 批量创建和保存

```csharp
for (int i = 0; i < 10; i++)
{
    CadDocument doc = new CadDocument();

    var circle = new Circle
    {
        Center = new XYZ(i * 10, 0, 0),
        Radius = 2.5,
        Color = (Color)(i % 256)
    };
    doc.Entities.Add(circle);

    DwgWriter.Write($"circle_{i:D2}.dwg", doc);
}
```

