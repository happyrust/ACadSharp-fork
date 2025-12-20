var builder = WebApplication.CreateBuilder(args);

// 添加服务
builder.Services.AddControllers();

// 配置 CORS - 允许前端访问
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 配置文件上传大小限制
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100MB
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB
});

var app = builder.Build();

// 配置 HTTP 请求管道
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// 添加欢迎页面
app.MapGet("/", () => Results.Json(new
{
    message = "ACadSharp Web Converter API",
    version = "1.0.0",
    endpoints = new[]
    {
        "POST /api/cad/convert - 转换 CAD 文件",
        "POST /api/cad/info - 获取文件信息",
        "GET /api/cad/health - 健康检查"
    }
}));

Console.WriteLine("🚀 API 已启动!");
Console.WriteLine("📍 地址: http://localhost:5000");
Console.WriteLine("💚 健康检查: http://localhost:5000/api/cad/health");

app.Run();
