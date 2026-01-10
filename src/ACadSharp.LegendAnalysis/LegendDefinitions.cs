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
		LimitSwitch,            // 限位开关: 3-5个小圆
		CoolingUnit,            // 循环冷却机组: 外框+风机圆+W箱体
		AirHandlingUnit,        // 空调机组: 外框+风机圆+W箱体+串联菱形
		SplitCabinet,           // 分体柜机: 上下两个近似方框+内部箭头+连管
		Silencer,               // 消声器: 短矩形+3根垂直隔板
		AirCurtain,             // 热风幕: 竖长矩形+一侧多支平行箭头
		EjectorIsolationValve,   // 引射隔离阀: 对向双三角形+中心圆
		Fan,                    // 风机 (GQ): 单个大圆+内部3射线
		DirectCoolingUnit,      // 直冷机 (GZ): 矩形+内部(风机+M+风机)
		SplitUnit               // 分体机 (代替 SplitCabinet): 上下两矩形+连线
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
        public int Index { get; set; }
        public string? DebugInfo { get; set; }
    }

    public class LegendGroup
    {
        public int Index { get; set; }
        public BoundingBox BoundingBox { get; set; }
        public List<LegendCandidate> Members { get; set; } = new List<LegendCandidate>();
        public List<Entity> AdditionalEntities { get; set; } = new List<Entity>();
    }
}
