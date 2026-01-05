using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;
using System.Collections.Generic;

namespace ACadSharp.LegendAnalysis
{
    public enum LegendType
    {
        Unknown,
        ElectricIsolationValve, // 电动隔离阀 (QM): 矩形+ZoneTop(圆+M)
        ManualIsolationValve,   // 手动隔离阀 (QM): 矩形+ZoneTop(T型)
        CheckValve,             // 止回阀 (RM): 矩形+无Top组件
        GravityReliefValve,     // 重力卸压阀 (RM): 矩形+ZoneTop(L型)
        BlastValve,             // 防爆波阀 (FL): 矩形+ZoneTop(弹簧)+内部W
        FireDamper,             // 防火阀 (FM): 矩形+中心F
        SmokeFireDamper,        // 排烟防火阀 (FM): 矩形+中心E
        FreshAirLouver,         // 新风口 (WP): 细长+箭头向内
        ExhaustLouver,          // 排风口 (WP): 细长+箭头向外
        LimitSwitch             // 限位开关: 3-5个小圆
    }

    public class LegendCandidate
    {
        public List<Entity> Entities { get; set; } = new List<Entity>();

        // Entities attached for context (e.g., nearby text labels).
        public List<Entity> ContextEntities { get; set; } = new List<Entity>();
        
        // Bounding Box of the entire cluster
        public BoundingBox BoundingBox { get; set; }

        public Entity? AnchorRect { get; set; }
        
        public LegendType DetectedType { get; set; } = LegendType.Unknown;
        public string? DebugInfo { get; set; }
    }
}
