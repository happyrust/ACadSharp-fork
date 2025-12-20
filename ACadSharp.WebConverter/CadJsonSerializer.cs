using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using ACadSharp.WebConverter.Models;

namespace ACadSharp.WebConverter
{
    /// <summary>
    /// CAD 文档到 JSON 的序列化器
    /// </summary>
    public class CadJsonSerializer
    {
        private readonly JsonSerializerOptions _jsonOptions;

        public CadJsonSerializer()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false, // 紧凑格式以减小体积
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// 将 CadDocument 转换为 JSON 字符串
        /// </summary>
        public async Task<string> SerializeToJsonAsync(CadDocument document)
        {
            var jsonDoc = await ConvertToJsonModelAsync(document);
            return JsonSerializer.Serialize(jsonDoc, _jsonOptions);
        }

        /// <summary>
        /// 将 CadDocument 转换为 JSON 字节数组
        /// </summary>
        public async Task<byte[]> SerializeToJsonBytesAsync(CadDocument document)
        {
            var json = await SerializeToJsonAsync(document);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// 转换为 JSON 模型
        /// </summary>
        private async Task<CadJsonDocument> ConvertToJsonModelAsync(CadDocument document)
        {
            return await Task.Run(() =>
            {
                var jsonDoc = new CadJsonDocument
                {
                    Version = "1.0",
                    Document = CreateDocumentMetadata(document),
                    Layers = ConvertLayers(document),
                    Entities = ConvertEntities(document.Entities),
                    Blocks = ConvertBlocks(document),
                    Statistics = CreateStatistics(document)
                };

                return jsonDoc;
            });
        }

        /// <summary>
        /// 创建文档元数据
        /// </summary>
        private DocumentMetadata CreateDocumentMetadata(CadDocument document)
        {
            var entities = document.Entities.ToList();
            BoundsData? bounds = null;

            if (entities.Any())
            {
                // 计算边界框
                double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

                foreach (var entity in entities)
                {
                    var points = GetEntityPoints(entity);
                    foreach (var pt in points)
                    {
                        minX = Math.Min(minX, pt.X);
                        minY = Math.Min(minY, pt.Y);
                        minZ = Math.Min(minZ, pt.Z);
                        maxX = Math.Max(maxX, pt.X);
                        maxY = Math.Max(maxY, pt.Y);
                        maxZ = Math.Max(maxZ, pt.Z);
                    }
                }

                bounds = new BoundsData
                {
                    Min = new[] { minX, minY, minZ },
                    Max = new[] { maxX, maxY, maxZ }
                };
            }

            return new DocumentMetadata
            {
                Version = document.Header.Version.ToString(),
                Units = document.Header.InsUnits.ToString(),
                Bounds = bounds
            };
        }

        /// <summary>
        /// 转换图层
        /// </summary>
        private List<LayerData> ConvertLayers(CadDocument document)
        {
            var layers = new List<LayerData>();

            foreach (var layer in document.Layers)
            {
                var color = layer.Color;
                layers.Add(new LayerData
                {
                    Name = layer.Name,
                    Color = new[] { color.R, color.G, color.B },
                    IsVisible = layer.IsOn,
                    IsLocked = layer.IsFrozen,
                    LineType = layer.LineType?.Name ?? "Continuous"
                });
            }

            return layers;
        }

        /// <summary>
        /// 转换实体列表
        /// </summary>
        private List<EntityData> ConvertEntities(IEnumerable<Entity> entities)
        {
            var result = new List<EntityData>();

            foreach (var entity in entities)
            {
                var entityData = ConvertEntity(entity);
                if (entityData != null)
                {
                    result.Add(entityData);
                }
            }

            return result;
        }

        /// <summary>
        /// 转换单个实体
        /// </summary>
        private EntityData? ConvertEntity(Entity entity)
        {
            var data = new EntityData
            {
                Handle = entity.Handle.ToString("X"),
                Layer = entity.Layer?.Name ?? "0"
            };

            // 获取实际颜色
            var color = entity.Color.IsByLayer && entity.Layer != null ? entity.Layer.Color : entity.Color;
            if (color != null && !color.IsByLayer && !color.IsByBlock)
            {
                data.Color = new[] { color.R, color.G, color.B };
            }

            // 获取线宽
            if (entity.LineWeight != LineWeightType.ByLayer && entity.LineWeight != LineWeightType.ByBlock)
            {
                data.LineWeight = (int)entity.LineWeight / 100.0; // 转换为毫米
            }

            // 根据实体类型提取几何数据
            data.Type = entity.ObjectName;
            data.Geometry = ExtractGeometry(entity);

            return data;
        }

        /// <summary>
        /// 提取实体的几何数据
        /// </summary>
        private Dictionary<string, object> ExtractGeometry(Entity entity)
        {
            var geometry = new Dictionary<string, object>();

            switch (entity)
            {
                case Line line:
                    geometry["start"] = new[] { line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z };
                    geometry["end"] = new[] { line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z };
                    break;

                case Circle circle:
                    geometry["center"] = new[] { circle.Center.X, circle.Center.Y, circle.Center.Z };
                    geometry["radius"] = circle.Radius;
                    geometry["normal"] = new[] { circle.Normal.X, circle.Normal.Y, circle.Normal.Z };
                    break;

                case Arc arc:
                    geometry["center"] = new[] { arc.Center.X, arc.Center.Y, arc.Center.Z };
                    geometry["radius"] = arc.Radius;
                    geometry["startAngle"] = arc.StartAngle;
                    geometry["endAngle"] = arc.EndAngle;
                    geometry["normal"] = new[] { arc.Normal.X, arc.Normal.Y, arc.Normal.Z };
                    break;

                case LwPolyline lwPolyline:
                    var lwVertices = lwPolyline.Vertices.Select(v =>
                        new[] { v.Location.X, v.Location.Y, 0.0 }
                    ).ToList();
                    geometry["vertices"] = lwVertices;
                    geometry["closed"] = lwPolyline.Flags.HasFlag(LwPolylineFlags.Closed);
                    break;

                case Polyline2D polyline2d:
                    var poly2dVertices = polyline2d.Vertices.Select(v =>
                        new[] { v.Location.X, v.Location.Y, v.Location.Z }
                    ).ToList();
                    geometry["vertices"] = poly2dVertices;
                    geometry["closed"] = polyline2d.Flags.HasFlag(PolylineFlags.ClosedPolylineOrClosedPolygonMeshInM);
                    break;

                case TextEntity text:
                    geometry["insertPoint"] = new[] { text.InsertPoint.X, text.InsertPoint.Y, text.InsertPoint.Z };
                    geometry["value"] = text.Value;
                    geometry["height"] = text.Height;
                    geometry["rotation"] = text.Rotation;
                    break;

                case MText mtext:
                    geometry["insertPoint"] = new[] { mtext.InsertPoint.X, mtext.InsertPoint.Y, mtext.InsertPoint.Z };
                    geometry["value"] = mtext.Value;
                    geometry["height"] = mtext.Height;
                    geometry["width"] = mtext.RectangleWidth;
                    break;

                case Insert insert:
                    geometry["insertPoint"] = new[] { insert.InsertPoint.X, insert.InsertPoint.Y, insert.InsertPoint.Z };
                    geometry["blockName"] = insert.Block?.Name ?? "";
                    geometry["scale"] = new[] { insert.XScale, insert.YScale, insert.ZScale };
                    geometry["rotation"] = insert.Rotation;
                    break;

                default:
                    // 其他实体类型，尝试获取基本点
                    var points = GetEntityPoints(entity);
                    if (points.Any())
                    {
                        geometry["points"] = points.Select(p => new[] { p.X, p.Y, p.Z }).ToList();
                    }
                    break;
            }

            return geometry;
        }

        /// <summary>
        /// 获取实体的关键点
        /// </summary>
        private List<CSMath.XYZ> GetEntityPoints(Entity entity)
        {
            var points = new List<CSMath.XYZ>();

            switch (entity)
            {
                case Line line:
                    points.Add(line.StartPoint);
                    points.Add(line.EndPoint);
                    break;
                case Circle circle:
                    points.Add(circle.Center);
                    break;
                case Arc arc:
                    points.Add(arc.Center);
                    break;
                case LwPolyline lwPolyline:
                    points.AddRange(lwPolyline.Vertices.Select(v => new CSMath.XYZ(v.Location.X, v.Location.Y, 0)));
                    break;
                case Polyline2D polyline2d:
                    points.AddRange(polyline2d.Vertices.Select(v => v.Location));
                    break;
                case TextEntity text:
                    points.Add(text.InsertPoint);
                    break;
                case MText mtext:
                    points.Add(mtext.InsertPoint);
                    break;
                case Insert insert:
                    points.Add(insert.InsertPoint);
                    break;
            }

            return points;
        }

        /// <summary>
        /// 转换块定义
        /// </summary>
        private List<BlockData> ConvertBlocks(CadDocument document)
        {
            var blocks = new List<BlockData>();

            foreach (var blockRecord in document.BlockRecords)
            {
                // 跳过模型空间和纸张空间
                if (blockRecord.Name.StartsWith("*"))
                    continue;

                blocks.Add(new BlockData
                {
                    Name = blockRecord.Name,
                    BasePoint = new[] {
                        blockRecord.BlockEntity?.BasePoint.X ?? 0,
                        blockRecord.BlockEntity?.BasePoint.Y ?? 0,
                        blockRecord.BlockEntity?.BasePoint.Z ?? 0
                    },
                    Entities = ConvertEntities(blockRecord.Entities)
                });
            }

            return blocks;
        }

        /// <summary>
        /// 创建统计信息
        /// </summary>
        private StatisticsData CreateStatistics(CadDocument document)
        {
            var entities = document.Entities.ToList();
            var typeCount = entities
                .GroupBy(e => e.ObjectName)
                .ToDictionary(g => g.Key, g => g.Count());

            return new StatisticsData
            {
                TotalEntities = entities.Count,
                EntitiesByType = typeCount,
                LayerCount = document.Layers.Count(),
                BlockCount = document.BlockRecords.Count(b => !b.Name.StartsWith("*"))
            };
        }
    }
}
