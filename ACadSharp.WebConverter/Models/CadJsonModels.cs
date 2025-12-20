using System.Collections.Generic;

namespace ACadSharp.WebConverter.Models
{
    /// <summary>
    /// CAD 文档的 JSON 表示 - 根对象
    /// </summary>
    public class CadJsonDocument
    {
        /// <summary>
        /// JSON 格式版本
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// 文档元数据
        /// </summary>
        public DocumentMetadata Document { get; set; } = new();

        /// <summary>
        /// 图层列表
        /// </summary>
        public List<LayerData> Layers { get; set; } = new();

        /// <summary>
        /// 实体列表
        /// </summary>
        public List<EntityData> Entities { get; set; } = new();

        /// <summary>
        /// 块定义列表
        /// </summary>
        public List<BlockData> Blocks { get; set; } = new();

        /// <summary>
        /// 统计信息
        /// </summary>
        public StatisticsData Statistics { get; set; } = new();
    }

    /// <summary>
    /// 文档元数据
    /// </summary>
    public class DocumentMetadata
    {
        /// <summary>
        /// CAD 版本
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 单位
        /// </summary>
        public string Units { get; set; } = string.Empty;

        /// <summary>
        /// 边界框
        /// </summary>
        public BoundsData? Bounds { get; set; }
    }

    /// <summary>
    /// 边界框
    /// </summary>
    public class BoundsData
    {
        /// <summary>
        /// 最小点 [x, y, z]
        /// </summary>
        public double[] Min { get; set; } = new double[3];

        /// <summary>
        /// 最大点 [x, y, z]
        /// </summary>
        public double[] Max { get; set; } = new double[3];
    }

    /// <summary>
    /// 图层数据
    /// </summary>
    public class LayerData
    {
        /// <summary>
        /// 图层名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 颜色 (RGB 数组 [r, g, b])
        /// </summary>
        public int[] Color { get; set; } = new int[3];

        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// 是否锁定
        /// </summary>
        public bool IsLocked { get; set; } = false;

        /// <summary>
        /// 线型名称
        /// </summary>
        public string LineType { get; set; } = "Continuous";
    }

    /// <summary>
    /// 实体数据 - 基类
    /// </summary>
    public class EntityData
    {
        /// <summary>
        /// 实体类型 (Line, Circle, Arc, etc.)
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 句柄 (唯一标识)
        /// </summary>
        public string Handle { get; set; } = string.Empty;

        /// <summary>
        /// 图层名称
        /// </summary>
        public string Layer { get; set; } = string.Empty;

        /// <summary>
        /// 颜色 [r, g, b]
        /// </summary>
        public int[]? Color { get; set; }

        /// <summary>
        /// 线宽
        /// </summary>
        public double? LineWeight { get; set; }

        /// <summary>
        /// 几何数据 (根据类型不同而不同)
        /// </summary>
        public Dictionary<string, object> Geometry { get; set; } = new();
    }

    /// <summary>
    /// 块定义数据
    /// </summary>
    public class BlockData
    {
        /// <summary>
        /// 块名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 基点 [x, y, z]
        /// </summary>
        public double[] BasePoint { get; set; } = new double[3];

        /// <summary>
        /// 块内的实体列表
        /// </summary>
        public List<EntityData> Entities { get; set; } = new();
    }

    /// <summary>
    /// 统计信息
    /// </summary>
    public class StatisticsData
    {
        /// <summary>
        /// 总实体数
        /// </summary>
        public int TotalEntities { get; set; }

        /// <summary>
        /// 按类型统计
        /// </summary>
        public Dictionary<string, int> EntitiesByType { get; set; } = new();

        /// <summary>
        /// 图层数
        /// </summary>
        public int LayerCount { get; set; }

        /// <summary>
        /// 块数
        /// </summary>
        public int BlockCount { get; set; }
    }
}
