using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text;
using ACadSharp;
using ACadSharp.Objects;
using ACadSharp.IO;

Console.OutputEncoding = Encoding.UTF8;

string defaultPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "test-files", "2244原理图图例.dwg"));
bool verbose = false;
bool exportSvg = false;
bool exportDxf = false;
bool exportDwg = false;
bool dxfBinary = false;
string? svgOutputPath = null;
string? dxfOutputPath = null;
string? dwgOutputPath = null;
ACadVersion? dwgVersion = null;
string? layoutName = null;
string? providedPath = null;

for (int i = 0; i < args.Length; i++)
{
	string arg = args[i];

	if (arg is "--verbose" or "-v")
	{
		verbose = true;
		continue;
	}

	if (arg == "--svg")
	{
		exportSvg = true;
		continue;
	}

	if (arg.StartsWith("--svg=", StringComparison.Ordinal))
	{
		exportSvg = true;
		svgOutputPath = arg["--svg=".Length..].Trim();
		continue;
	}

	if (arg == "--svg-out" && i + 1 < args.Length)
	{
		exportSvg = true;
		svgOutputPath = args[++i];
		continue;
	}

	if (arg.StartsWith("--svg-out=", StringComparison.Ordinal))
	{
		exportSvg = true;
		svgOutputPath = arg["--svg-out=".Length..].Trim();
		continue;
	}

	if (arg == "--dxf")
	{
		exportDxf = true;
		continue;
	}

	if (arg.StartsWith("--dxf=", StringComparison.Ordinal))
	{
		exportDxf = true;
		dxfOutputPath = arg["--dxf=".Length..].Trim();
		continue;
	}

	if (arg == "--dxf-out" && i + 1 < args.Length)
	{
		exportDxf = true;
		dxfOutputPath = args[++i];
		continue;
	}

	if (arg.StartsWith("--dxf-out=", StringComparison.Ordinal))
	{
		exportDxf = true;
		dxfOutputPath = arg["--dxf-out=".Length..].Trim();
		continue;
	}

	if (arg is "--dxf-binary" or "--dxf-bin")
	{
		exportDxf = true;
		dxfBinary = true;
		continue;
	}

	if (arg == "--dwg")
	{
		exportDwg = true;
		continue;
	}

	if (arg.StartsWith("--dwg=", StringComparison.Ordinal))
	{
		exportDwg = true;
		dwgOutputPath = arg["--dwg=".Length..].Trim();
		continue;
	}

	if (arg == "--dwg-out" && i + 1 < args.Length)
	{
		exportDwg = true;
		dwgOutputPath = args[++i];
		continue;
	}

	if (arg.StartsWith("--dwg-out=", StringComparison.Ordinal))
	{
		exportDwg = true;
		dwgOutputPath = arg["--dwg-out=".Length..].Trim();
		continue;
	}

	if (arg == "--dwg-version" && i + 1 < args.Length)
	{
		string v = args[++i];
		if (Enum.TryParse<ACadVersion>(v, ignoreCase: true, out var parsed))
		{
			dwgVersion = parsed;
		}
		else
		{
			Console.Error.WriteLine($"无法解析 --dwg-version: {v}");
			return 2;
		}
		continue;
	}

	if (arg.StartsWith("--dwg-version=", StringComparison.Ordinal))
	{
		string v = arg["--dwg-version=".Length..].Trim();
		if (Enum.TryParse<ACadVersion>(v, ignoreCase: true, out var parsed))
		{
			dwgVersion = parsed;
		}
		else
		{
			Console.Error.WriteLine($"无法解析 --dwg-version: {v}");
			return 2;
		}
		continue;
	}

	if (arg == "--layout" && i + 1 < args.Length)
	{
		layoutName = args[++i];
		continue;
	}

	if (arg.StartsWith("--layout=", StringComparison.Ordinal))
	{
		layoutName = arg["--layout=".Length..].Trim();
		continue;
	}

	if (arg.StartsWith("-", StringComparison.Ordinal))
	{
		Console.Error.WriteLine($"未知参数：{arg}");
		return 2;
	}

	if (providedPath is null)
	{
		providedPath = arg;
		continue;
	}

	Console.Error.WriteLine($"多余的位置参数：{arg}");
	return 2;
}

string filePath = providedPath ?? defaultPath;

if (args.Length == 0 || providedPath is null)
{
	Console.WriteLine($"未提供参数，默认使用：{filePath}");
	Console.WriteLine("用法：dotnet run --project tools/DwgParse/DwgParse.csproj -- <dwg/dxf文件路径> [--svg[=<输出.svg>]|--svg-out <输出.svg>] [--dxf[=<输出.dxf>]|--dxf-out <输出.dxf>] [--dxf-binary] [--dwg[=<输出.dwg>]|--dwg-out <输出.dwg>] [--dwg-version <ACadVersion>] [--layout <布局名>] [--verbose|-v]");
	Console.WriteLine();
}

if (!File.Exists(filePath))
{
	Console.Error.WriteLine($"找不到文件：{filePath}");
	return 2;
}

Console.WriteLine($"开始读取 CAD：{filePath}");
Console.WriteLine($"文件大小：{new FileInfo(filePath).Length} bytes");

int printedOtherMessages = 0;
int printedWarningMessages = 0;
int maxOtherMessagesToPrint = verbose ? int.MaxValue : 20;
int maxWarningMessagesToPrint = verbose ? int.MaxValue : 50;
var notificationCounts = new Dictionary<NotificationType, int>();

static string NormalizeMessage(string message)
{
	if (string.IsNullOrWhiteSpace(message))
	{
		return string.Empty;
	}

	return Regex.Replace(message.Trim(), "\\d+", "#");
}

var notificationPatterns = new Dictionary<(NotificationType Type, string Pattern), (int Count, string Sample)>();

void OnNotification(object sender, NotificationEventArgs e)
{
	notificationCounts.TryGetValue(e.NotificationType, out int current);
	notificationCounts[e.NotificationType] = current + 1;

	string pattern = NormalizeMessage(e.Message);
	var patternKey = (e.NotificationType, pattern);
	if (notificationPatterns.TryGetValue(patternKey, out var existing))
	{
		notificationPatterns[patternKey] = (existing.Count + 1, existing.Sample);
	}
	else
	{
		notificationPatterns[patternKey] = (1, e.Message);
	}

	bool isError = e.NotificationType is NotificationType.Error;
	bool isWarning = e.NotificationType is NotificationType.Warning;

	bool canPrintWarning = isWarning && printedWarningMessages < maxWarningMessagesToPrint;
	bool canPrintOther = !isWarning && !isError && printedOtherMessages < maxOtherMessagesToPrint;

	if (isError || canPrintWarning || canPrintOther)
	{
		if (isWarning)
		{
			printedWarningMessages++;
		}
		else if (!isError)
		{
			printedOtherMessages++;
		}

		Console.Write($"[{e.NotificationType}] {e.Message}");
		if (e.Exception is not null)
		{
			Console.Write($" | Exception: {e.Exception.GetType().Name}: {e.Exception.Message}");
		}
		Console.WriteLine();
	}
}

var stopwatch = Stopwatch.StartNew();
CadDocument? document = null;

static bool IsSupportedDwgWriteVersion(ACadVersion version)
{
	return version is ACadVersion.AC1014
		or ACadVersion.AC1015
		or ACadVersion.AC1018
		or ACadVersion.AC1024
		or ACadVersion.AC1027
		or ACadVersion.AC1032;
}

try
{
	string ext = Path.GetExtension(filePath).ToLowerInvariant();
	if (ext == ".dxf")
	{
		document = DxfReader.Read(filePath, new DxfReaderConfiguration(), OnNotification);
	}
	else
	{
		document = DwgReader.Read(filePath, new DwgReaderConfiguration(), OnNotification);
	}
}
catch (Exception ex)
{
	stopwatch.Stop();
	Console.Error.WriteLine($"读取失败（用时 {stopwatch.Elapsed}）：{ex.GetType().Name}: {ex.Message}");
	Console.Error.WriteLine(ex.ToString());

	return 1;
}

stopwatch.Stop();

Console.WriteLine();
Console.WriteLine($"读取成功，用时：{stopwatch.Elapsed}");
Console.WriteLine($"DWG 版本：{document.Header?.Version}");
Console.WriteLine($"代码页：{document.Header?.CodePage}");
Console.WriteLine($"实体数量（ModelSpace）：{document.Entities?.Count ?? 0}");
Console.WriteLine($"块记录：{document.BlockRecords?.Count ?? 0}");
Console.WriteLine($"图层：{document.Layers?.Count ?? 0}");
Console.WriteLine($"线型：{document.LineTypes?.Count ?? 0}");
Console.WriteLine($"文字样式：{document.TextStyles?.Count ?? 0}");
Console.WriteLine($"标注样式：{document.DimensionStyles?.Count ?? 0}");
Console.WriteLine($"UCS：{document.UCSs?.Count ?? 0}");
Console.WriteLine($"视图：{document.Views?.Count ?? 0}");
Console.WriteLine($"视口：{document.VPorts?.Count ?? 0}");
Console.WriteLine($"注册应用（AppId）：{document.AppIds?.Count ?? 0}");

if (document.Entities is not null && document.Entities.Count > 0)
{
	Console.WriteLine();
	Console.WriteLine("实体类型统计（Top 20）：");
	foreach (var group in document.Entities
		.GroupBy(e => e.GetType().Name, StringComparer.Ordinal)
		.OrderByDescending(g => g.Count())
		.ThenBy(g => g.Key, StringComparer.Ordinal)
		.Take(20))
	{
		Console.WriteLine($"- {group.Key}: {group.Count()}");
	}
}

Console.WriteLine();
Console.WriteLine("通知统计：");
foreach (var kvp in notificationCounts.OrderBy(k => (int)k.Key))
{
	Console.WriteLine($"- {kvp.Key}: {kvp.Value}");
}

if (notificationPatterns.Count > 0)
{
	Console.WriteLine();
	Console.WriteLine("通知模式统计（Top 10，已做数字归一化）：");
	foreach (var kvp in notificationPatterns
		.OrderByDescending(kvp => kvp.Value.Count)
		.ThenBy(kvp => (int)kvp.Key.Type)
		.ThenBy(kvp => kvp.Key.Pattern, StringComparer.Ordinal)
		.Take(10))
	{
		Console.WriteLine($"- {kvp.Key.Type} x{kvp.Value.Count}: {kvp.Value.Sample}");
	}
}

if (exportSvg)
{
	string resolvedSvgPath = svgOutputPath ?? string.Empty;
	if (string.IsNullOrWhiteSpace(resolvedSvgPath))
	{
		resolvedSvgPath = Path.ChangeExtension(filePath, ".svg");
	}

	resolvedSvgPath = Path.GetFullPath(resolvedSvgPath);
	string? dir = Path.GetDirectoryName(resolvedSvgPath);
	if (!string.IsNullOrWhiteSpace(dir))
	{
		Directory.CreateDirectory(dir);
	}

	Console.WriteLine();
	Console.WriteLine($"开始导出 SVG：{resolvedSvgPath}");

	try
	{
		using var writer = new SvgWriter(resolvedSvgPath, document);
		writer.OnNotification += OnNotification;

		if (!string.IsNullOrWhiteSpace(layoutName))
		{
			if (document.Layouts is null || !document.Layouts.TryGetValue(layoutName, out var layout))
			{
				Console.Error.WriteLine($"找不到布局：{layoutName}");
				return 2;
			}

			writer.Write(layout);
		}
		else
		{
			writer.Write();
		}
	}
	catch (Exception ex)
	{
		Console.Error.WriteLine($"导出 SVG 失败：{ex.GetType().Name}: {ex.Message}");
		Console.Error.WriteLine(ex.ToString());
		return 1;
	}

	Console.WriteLine($"SVG 已生成：{resolvedSvgPath}（{new FileInfo(resolvedSvgPath).Length} bytes）");
}

if (exportDxf)
{
	string resolvedDxfPath = dxfOutputPath ?? string.Empty;
	if (string.IsNullOrWhiteSpace(resolvedDxfPath))
	{
		resolvedDxfPath = Path.ChangeExtension(filePath, ".dxf");
	}

	resolvedDxfPath = Path.GetFullPath(resolvedDxfPath);
	string? dir = Path.GetDirectoryName(resolvedDxfPath);
	if (!string.IsNullOrWhiteSpace(dir))
	{
		Directory.CreateDirectory(dir);
	}

	Console.WriteLine();
	Console.WriteLine($"开始导出 DXF{(dxfBinary ? "（二进制）" : "（ASCII）")}：{resolvedDxfPath}");

	try
	{
		DxfWriter.Write(resolvedDxfPath, document, binary: dxfBinary, configuration: null, notification: OnNotification);
	}
	catch (Exception ex)
	{
		Console.Error.WriteLine($"导出 DXF 失败：{ex.GetType().Name}: {ex.Message}");
		Console.Error.WriteLine(ex.ToString());
		return 1;
	}

	Console.WriteLine($"DXF 已生成：{resolvedDxfPath}（{new FileInfo(resolvedDxfPath).Length} bytes）");
}

if (exportDwg)
{
	string resolvedDwgPath = dwgOutputPath ?? string.Empty;
	if (string.IsNullOrWhiteSpace(resolvedDwgPath))
	{
		resolvedDwgPath = Path.ChangeExtension(filePath, ".dwg");
	}

	resolvedDwgPath = Path.GetFullPath(resolvedDwgPath);
	string? dir = Path.GetDirectoryName(resolvedDwgPath);
	if (!string.IsNullOrWhiteSpace(dir))
	{
		Directory.CreateDirectory(dir);
	}

	Console.WriteLine();
	Console.WriteLine($"开始导出 DWG：{resolvedDwgPath}");

	try
	{
		ACadVersion desired = dwgVersion ?? document.Header?.Version ?? ACadVersion.AC1027;
		if (!IsSupportedDwgWriteVersion(desired))
		{
			desired = ACadVersion.AC1027;
		}

		if (document.Header is null)
		{
			document.CreateDefaults();
		}

		// Ensure required defaults exist for DWG header writer.
		// Some DXF files don't include the default MLineStyle ("Standard"), which is required by CurrentMLineStyle.
		document.UpdateCollections(true);
		if (document.MLineStyles is not null && !document.MLineStyles.ContainsKey(MLineStyle.DefaultName))
		{
			document.MLineStyles.Add(MLineStyle.Default);
		}
		if (document.Header is not null)
		{
			// Point header to a valid style name.
			document.Header.CurrentMLineStyleName = MLineStyle.DefaultName;
		}

		if (!IsSupportedDwgWriteVersion(document.Header.Version) || document.Header.Version != desired)
		{
			document.Header.Version = desired;
		}

		DwgWriter.Write(resolvedDwgPath, document, configuration: null, notification: OnNotification);
	}
	catch (Exception ex)
	{
		Console.Error.WriteLine($"导出 DWG 失败：{ex.GetType().Name}: {ex.Message}");
		Console.Error.WriteLine(ex.ToString());
		return 1;
	}

	Console.WriteLine($"DWG 已生成：{resolvedDwgPath}（{new FileInfo(resolvedDwgPath).Length} bytes）");
}

return 0;
