namespace ACadSharp.IO.DWG
{
	internal class Dwg21CompressedMetadata
	{
		/// <summary> 头部大小 (通常为 0x70) </summary>
		public ulong HeaderSize { get; set; } = 0x70;
		/// <summary> 文件总大小 </summary>
		public ulong FileSize { get; set; }
		/// <summary> 压缩状态下页面映射表的 CRC </summary>
		public ulong PagesMapCrcCompressed { get; set; }
		/// <summary> 页面映射表纠错因子 (Correction Factor) </summary>
		public ulong PagesMapCorrectionFactor { get; set; }
		/// <summary> 页面映射表 CRC 种子 </summary>
		public ulong PagesMapCrcSeed { get; set; }
		/// <summary> 二级页面映射表偏移 (相对于数据页起始位置 0x480) </summary>
		public ulong Map2Offset { get; set; }
		/// <summary> 二级页面映射表页面 ID </summary>
		public ulong Map2Id { get; set; }
		/// <summary> 页面映射表在流中的相对偏移 (相对于 0x480) </summary>
		public ulong PagesMapOffset { get; set; }
		/// <summary> 第二个头部偏移 (相对于 0x480) </summary>
		public ulong Header2offset { get; set; }
		/// <summary> 压缩后页面映射表大小 </summary>
		public ulong PagesMapSizeCompressed { get; set; }
		/// <summary> 解压后页面映射表大小 </summary>
		public ulong PagesMapSizeUncompressed { get; set; }
		/// <summary> 文件中的总页面数量 </summary>
		public ulong PagesAmount { get; set; }
		/// <summary> 页面 ID 的最大值 </summary>
		public ulong PagesMaxId { get; set; }
		/// <summary> 段映射表二级标识 ID </summary>
		public ulong SectionsMap2Id { get; set; }
		/// <summary> 页面映射表页面 ID </summary>
		public ulong PagesMapId { get; set; }
		/// <summary> 未知字段 (通常为 32) </summary>
		public ulong Unknow0x20 { get; set; } = 32;
		/// <summary> 未知字段 (通常为 64) </summary>
		public ulong Unknow0x40 { get; set; } = 64;
		/// <summary> 解压后页面映射表的 CRC </summary>
		public ulong PagesMapCrcUncompressed { get; set; }
		/// <summary> 未知字段 (通常为 0xF800/63488) </summary>
		public ulong Unknown0xF800 { get; set; } = 0xF800;
		/// <summary> 未知字段 (通常为 4) </summary>
		public ulong Unknown4 { get; set; } = 4;
		/// <summary> 未知字段 (通常为 1) </summary>
		public ulong Unknown1 { get; set; } = 1;
		/// <summary> 逻辑段总数 (number of sections + 1) </summary>
		public ulong SectionsAmount { get; set; }
		/// <summary> 解压后段映射表的 CRC </summary>
		public ulong SectionsMapCrcUncompressed { get; set; }
		/// <summary> 压缩后段映射表大小 </summary>
		public ulong SectionsMapSizeCompressed { get; set; }
		/// <summary> 段映射表主页面 ID </summary>
		public ulong SectionsMapId { get; set; }
		/// <summary> 解压后段映射表大小 </summary>
		public ulong SectionsMapSizeUncompressed { get; set; }
		/// <summary> 压缩状态下段映射表的 CRC </summary>
		public ulong SectionsMapCrcCompressed { get; set; }
		/// <summary> 段映射纠错因子 </summary>
		public ulong SectionsMapCorrectionFactor { get; set; }
		/// <summary> 段映射 CRC 种子 </summary>
		public ulong SectionsMapCrcSeed { get; set; }
		/// <summary> 流版本号 (通常为 0x60100) </summary>
		public ulong StreamVersion { get; set; } = 393472;
		/// <summary> CRC 种子 </summary>
		public ulong CrcSeed { get; set; }
		/// <summary> 编码后的 CRC 种子 </summary>
		public ulong CrcSeedEncoded { get; set; }
		/// <summary> 随机种子 </summary>
		public ulong RandomSeed { get; set; }
		/// <summary> 整个元数据块的 CRC64 校验码 </summary>
		public ulong HeaderCRC64 { get; set; }
	}
}
