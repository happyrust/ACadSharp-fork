# CAD 文件查看器 - 集成示例

ACadSharp + cad-viewer 的完整集成示例

## 🎯 功能特性

- ✅ 文件上传（点击或拖拽）
- ✅ 支持 DWG 和 DXF 格式
- ✅ 文件信息查看
- ✅ 格式转换（DWG/DXF → DXF）
- ✅ 转换后的文件可直接在 cad-viewer 中查看

## 🚀 快速开始

### 1. 启动后端 API

```bash
cd /Volumes/DPC/work/cad-code/ACadSharp/ACadSharp.WebApi
dotnet run
```

API 将运行在 http://localhost:5000

### 2. 打开前端页面

直接在浏览器中打开 `index.html` 文件，或使用本地服务器：

```bash
# 使用 Python
python3 -m http.server 8000

# 使用 Node.js (http-server)
npx http-server -p 8000

# 使用 PHP
php -S localhost:8000
```

然后访问: http://localhost:8000

## 📖 使用流程

### 方式 1: 通过 Web API 转换

1. **上传文件**: 点击或拖拽 DWG/DXF 文件到上传区域
2. **查看信息**: 系统会自动获取并显示文件信息（版本、实体数量等）
3. **转换文件**: 点击 "转换并下载" 按钮
4. **下载结果**: 转换后的 DXF 文件会自动下载
5. **在 cad-viewer 中查看**: 打开 cad-viewer，加载下载的 DXF 文件

### 方式 2: 直接使用 ACadSharp

如果你想在服务器端处理：

```csharp
using ACadSharp;
using ACadSharp.WebConverter;

// 读取文件
var doc = DwgReader.Read("file.dwg");

// 转换为 DXF
var converter = new CadWebConverter();
var result = await converter.ConvertAsync(
    fileStream,
    "file.dwg",
    new CadWebConverter.ConversionOptions
    {
        Format = CadWebConverter.OutputFormat.DXF,
        DxfBinary = false // ASCII 格式推荐用于 Web
    }
);

// 保存或返回给客户端
File.WriteAllBytes("output.dxf", result.Data);
```

## 🔌 API 接口

### 1. 转换文件

```
POST /api/cad/convert?format=dxf&binary=false
Content-Type: multipart/form-data

file: [CAD 文件]
```

响应: 转换后的文件 (application/dxf)

### 2. 获取文件信息

```
POST /api/cad/info
Content-Type: multipart/form-data

file: [CAD 文件]
```

响应:
```json
{
    "fileName": "drawing.dwg",
    "fileSize": 1024000,
    "version": "AC1027",
    "entityCount": 150,
    "layerCount": 5,
    "blockCount": 3,
    "units": "Millimeters"
}
```

### 3. 健康检查

```
GET /api/cad/health
```

响应:
```json
{
    "status": "healthy",
    "timestamp": "2025-12-14T10:00:00Z",
    "service": "CAD Converter API"
}
```

## 🔗 集成到 cad-viewer

转换后的 DXF 文件可以直接加载到 cad-viewer:

```typescript
import { AcApDocManager } from '@mlightcad/cad-simple-viewer';
import { AcDbOpenDatabaseOptions } from '@mlightcad/data-model';

// 从 API 获取转换后的文件
const response = await fetch('http://localhost:5000/api/cad/convert', {
    method: 'POST',
    body: formData
});

const dxfContent = await response.arrayBuffer();

// 加载到 cad-viewer
const options: AcDbOpenDatabaseOptions = {
    minimumChunkSize: 1000,
    readOnly: true
};

await AcApDocManager.instance.openDocument(
    'converted.dxf',
    dxfContent,
    options
);
```

## 🛠 技术栈

**后端:**
- .NET 9.0
- ASP.NET Core Web API
- ACadSharp (CAD 文件处理)
- Swashbuckle (Swagger/OpenAPI)

**前端:**
- 原生 HTML/CSS/JavaScript
- Fetch API
- 拖拽上传

**CAD 查看器:**
- @mlightcad/cad-simple-viewer
- @mlightcad/data-model
- THREE.js (渲染引擎)

## 📝 注意事项

1. **文件大小限制**: 默认最大 50MB，可在 API 配置中调整
2. **支持的版本**: AC1012-AC1032 (R13-R2018+)
3. **输出格式**: 推荐使用 DXF ASCII 格式，兼容性最好
4. **CORS 配置**: 开发环境已配置 CORS，生产环境请根据需要调整
5. **性能**: 大文件转换可能需要几秒钟，请耐心等待

## 🔍 故障排除

### API 无法访问

检查后端是否正在运行:
```bash
curl http://localhost:5000/api/cad/health
```

### CORS 错误

确保 API 的 CORS 配置包含您的前端地址。

### 转换失败

- 检查文件格式是否正确（.dwg 或 .dxf）
- 查看 API 日志获取详细错误信息
- 确认文件未损坏

## 📚 相关文档

- [ACadSharp 文档](/Volumes/DPC/work/cad-code/ACadSharp/llmdoc/)
- [cad-viewer 文档](https://github.com/mlightcad/cad-viewer)
- [ASP.NET Core 文档](https://docs.microsoft.com/aspnet/core)

## 📄 许可证

MIT
