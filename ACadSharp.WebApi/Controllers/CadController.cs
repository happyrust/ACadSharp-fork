using Microsoft.AspNetCore.Mvc;
using ACadSharp.WebConverter;
using System.ComponentModel.DataAnnotations;

namespace ACadSharp.WebApi.Controllers
{
    /// <summary>
    /// CAD 文件转换 API 控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CadController : ControllerBase
    {
        private readonly ILogger<CadController> _logger;
        private readonly CadWebConverter _converter;

        // 允许的文件扩展名
        private static readonly string[] AllowedExtensions = { ".dwg", ".dxf" };

        // 最大文件大小 (50MB)
        private const long MaxFileSize = 50 * 1024 * 1024;

        public CadController(ILogger<CadController> logger)
        {
            _logger = logger;
            _converter = new CadWebConverter();
        }

        /// <summary>
        /// 转换 CAD 文件为 Web 可查看格式
        /// </summary>
        /// <param name="file">CAD 文件 (DWG 或 DXF)</param>
        /// <param name="format">输出格式 (dxf 或 dwg，默认 dxf)</param>
        /// <param name="binary">DXF 是否使用二进制格式 (默认 false)</param>
        /// <returns>转换后的文件</returns>
        [HttpPost("convert")]
        [RequestSizeLimit(MaxFileSize)]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConvertFile(
            [Required] IFormFile file,
            [FromQuery] string format = "dxf",
            [FromQuery] bool binary = false)
        {
            try
            {
                // 验证文件
                var validationResult = ValidateFile(file);
                if (validationResult != null)
                    return validationResult;

                _logger.LogInformation(
                    "开始转换文件: {FileName}, 大小: {FileSize} bytes, 输出格式: {Format}",
                    file.FileName, file.Length, format);

                // 配置转换选项
                var options = new CadWebConverter.ConversionOptions
                {
                    Format = format.ToLowerInvariant() == "dwg"
                        ? CadWebConverter.OutputFormat.DWG
                        : CadWebConverter.OutputFormat.DXF,
                    DxfBinary = binary
                };

                // 执行转换
                using var stream = file.OpenReadStream();
                var result = await _converter.ConvertAsync(stream, file.FileName, options);

                if (!result.Success)
                {
                    _logger.LogError("转换失败: {ErrorMessage}", result.ErrorMessage);
                    return Problem(
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "转换失败");
                }

                _logger.LogInformation(
                    "转换成功: {FileName}, 输出大小: {OutputSize} bytes",
                    result.FileName, result.Data!.Length);

                // 返回转换后的文件
                return File(result.Data!, result.MimeType, result.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "转换过程中发生异常: {Message}", ex.Message);
                return Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "服务器错误");
            }
        }

        /// <summary>
        /// 获取 CAD 文件的信息（不进行转换）
        /// </summary>
        /// <param name="file">CAD 文件 (DWG 或 DXF)</param>
        /// <returns>文件信息</returns>
        [HttpPost("info")]
        [RequestSizeLimit(MaxFileSize)]
        [ProducesResponseType(typeof(CadFileInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetFileInfo([Required] IFormFile file)
        {
            try
            {
                // 验证文件
                var validationResult = ValidateFile(file);
                if (validationResult != null)
                    return validationResult;

                _logger.LogInformation("获取文件信息: {FileName}", file.FileName);

                // 读取文档
                using var stream = file.OpenReadStream();
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                CadDocument doc = extension switch
                {
                    ".dwg" => await Task.Run(() => IO.DwgReader.Read(stream)),
                    ".dxf" => await Task.Run(() => IO.DxfReader.Read(stream)),
                    _ => throw new NotSupportedException($"不支持的文件类型: {extension}")
                };

                var info = new CadFileInfo
                {
                    FileName = file.FileName,
                    FileSize = file.Length,
                    Version = doc.Header.Version.ToString(),
                    EntityCount = doc.Entities.Count(),
                    LayerCount = doc.Layers.Count(),
                    BlockCount = doc.BlockRecords.Count(),
                    Units = doc.Header.InsUnits.ToString()
                };

                _logger.LogInformation(
                    "文件信息获取成功: {FileName}, 版本: {Version}, 实体数: {EntityCount}",
                    file.FileName, info.Version, info.EntityCount);

                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取文件信息失败: {Message}", ex.Message);
                return Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "获取文件信息失败");
            }
        }

        /// <summary>
        /// 健康检查
        /// </summary>
        [HttpGet("health")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = "CAD Converter API"
            });
        }

        /// <summary>
        /// 验证上传的文件
        /// </summary>
        private IActionResult? ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "文件无效",
                    Detail = "未提供文件或文件为空",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // 检查文件扩展名
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "文件类型不支持",
                    Detail = $"只支持 {string.Join(", ", AllowedExtensions)} 文件",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // 检查文件大小
            if (file.Length > MaxFileSize)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "文件过大",
                    Detail = $"文件大小不能超过 {MaxFileSize / 1024 / 1024} MB",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return null;
        }
    }

    /// <summary>
    /// CAD 文件信息响应模型
    /// </summary>
    public class CadFileInfo
    {
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// 文件大小 (字节)
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// CAD 版本
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 实体数量
        /// </summary>
        public int EntityCount { get; set; }

        /// <summary>
        /// 图层数量
        /// </summary>
        public int LayerCount { get; set; }

        /// <summary>
        /// 块数量
        /// </summary>
        public int BlockCount { get; set; }

        /// <summary>
        /// 单位
        /// </summary>
        public string Units { get; set; } = string.Empty;
    }
}
