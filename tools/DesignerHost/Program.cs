using System.Text;
using Bing.EasyPrint.Templates;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = DesignerRequestProcessor.MaxRequestBytes);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("EASYPRINT_DESIGNER_API_URL") ?? "http://127.0.0.1:5187");

var app = builder.Build();
app.MapGet("/api/designer/health", () => Results.Ok(new { status = "ready" }));
app.MapGet("/api/designer/samples", DesignerRequestProcessor.GetSamples);
app.MapPost("/api/designer/preview", DesignerRequestProcessor.Preview);
app.MapPost("/api/designer/render", DesignerRequestProcessor.Render);
app.Run();

public partial class Program { }
