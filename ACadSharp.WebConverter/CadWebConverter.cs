using System;
using System.IO;
using System.Threading.Tasks;
using ACadSharp;
using ACadSharp.IO;

namespace ACadSharp.WebConverter
{
    /// <summary>
    /// CAD 文件转换器，用于将 DWG/DXF 文件转换为 Web 可查看的格式
    /// </summary>
    public class CadWebConverter
    {
        /// <summary>
        /// 转换结果
        /// </summary>
        public class ConversionResult
        {
            /// <summary>
            /// 是否成功
            /// </summary>
            public bool Success { get; set; }

            /// <summary>
            /// 转换后的数据
            /// </summary>
            public byte[]? Data { get; set; }

            /// <summary>
            /// 错误消息
            /// </summary>
            public string? ErrorMessage { get; set; }

            /// <summary>
            /// 文件名
            /// </summary>
            public string? FileName { get; set; }

            /// <summary>
            /// MIME 类型
            /// </summary>
            public string MimeType { get; set; } = "application/dxf";
        }

        /// <summary>
        /// 转换选项
        /// </summary>
        public class ConversionOptions
        {
            /// <summary>
            /// 输出格式 (DXF 或 DWG)
            /// </summary>
            public OutputFormat Format { get; set; } = OutputFormat.DXF;

            /// <summary>
            /// DXF 是否使用二进制格式
            /// </summary>
            public bool DxfBinary { get; set; } = false;

            /// <summary>
            /// DWG 输出版本
            /// </summary>
            public ACadVersion DwgVersion { get; set; } = ACadVersion.AC1027;
        }

        /// <summary>
        /// 输出格式
        /// </summary>
        public enum OutputFormat
        {
            /// <summary>
            /// DXF 格式 (推荐用于 Web)
            /// </summary>
            DXF,

            /// <summary>
            /// DWG 格式
            /// </summary>
            DWG
        }

        /// <summary>
        /// 从文件流转换 CAD 文件为 Web 可查看格式
        /// </summary>
        /// <param name="inputStream">输入文件流</param>
        /// <param name="fileName">文件名(用于判断文件类型)</param>
        /// <param name="options">转换选项</param>
        /// <returns>转换结果</returns>
        public async Task<ConversionResult> ConvertAsync(
            Stream inputStream,
            string fileName,
            ConversionOptions? options = null)
        {
            options ??= new ConversionOptions();

            try
            {
                // 读取 CAD 文档
                var doc = await ReadDocumentAsync(inputStream, fileName);

                // 转换为目标格式
                var outputData = await ConvertDocumentAsync(doc, options);

                return new ConversionResult
                {
                    Success = true,
                    Data = outputData,
                    FileName = GetOutputFileName(fileName, options.Format),
                    MimeType = GetMimeType(options.Format)
                };
            }
            catch (Exception ex)
            {
                return new ConversionResult
                {
                    Success = false,
                    ErrorMessage = $"转换失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 从字节数组转换 CAD 文件
        /// </summary>
        public async Task<ConversionResult> ConvertAsync(
            byte[] inputData,
            string fileName,
            ConversionOptions? options = null)
        {
            using var stream = new MemoryStream(inputData);
            return await ConvertAsync(stream, fileName, options);
        }

        /// <summary>
        /// 读取 CAD 文档
        /// </summary>
        private async Task<CadDocument> ReadDocumentAsync(Stream stream, string fileName)
        {
            return await Task.Run(() =>
            {
                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                return extension switch
                {
                    ".dwg" => DwgReader.Read(stream),
                    ".dxf" => DxfReader.Read(stream),
                    _ => throw new NotSupportedException($"不支持的文件类型: {extension}")
                };
            });
        }

        /// <summary>
        /// 转换文档为目标格式
        /// </summary>
        private async Task<byte[]> ConvertDocumentAsync(
            CadDocument doc,
            ConversionOptions options)
        {
            return await Task.Run(() =>
            {
                using var outputStream = new MemoryStream();

                switch (options.Format)
                {
                    case OutputFormat.DXF:
                        // DXF 写入 (ASCII 格式推荐用于 Web)
                        using (var writer = new DxfWriter(outputStream, doc, options.DxfBinary))
                        {
                            writer.Write();
                        }
                        break;

                    case OutputFormat.DWG:
                        // DWG 写入 (版本由文档的 Header.Version 控制)
                        // 如果需要指定版本，需要在转换前设置 doc.Header.Version
                        if (options.DwgVersion != doc.Header.Version)
                        {
                            doc.Header.Version = options.DwgVersion;
                        }
                        using (var writer = new DwgWriter(outputStream, doc))
                        {
                            writer.Write();
                        }
                        break;

                    default:
                        throw new NotSupportedException($"不支持的输出格式: {options.Format}");
                }

                return outputStream.ToArray();
            });
        }

        /// <summary>
        /// 获取输出文件名
        /// </summary>
        private string GetOutputFileName(string originalFileName, OutputFormat format)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
            var extension = format == OutputFormat.DXF ? ".dxf" : ".dwg";
            return $"{nameWithoutExt}{extension}";
        }

        /// <summary>
        /// 获取 MIME 类型
        /// </summary>
        private string GetMimeType(OutputFormat format)
        {
            return format switch
            {
                OutputFormat.DXF => "application/dxf",
                OutputFormat.DWG => "application/dwg",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// 获取文档统计信息
        /// </summary>
        public static string GetDocumentInfo(CadDocument doc)
        {
            return $@"CAD 文档信息:
- 版本: {doc.Header.Version}
- 实体数量: {doc.Entities.Count()}
- 图层数量: {doc.Layers.Count()}
- 块数量: {doc.BlockRecords.Count()}";
        }
    }
}
