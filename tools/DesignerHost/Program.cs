using System.Text;
using Bing.EasyPrint.Templates;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = DesignerRequestProcessor.MaxRequestBytes);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("EASYPRINT_DESIGNER_API_URL") ?? "http://127.0.0.1:5187");

var app = builder.Build();
var serverMaxWorkingBytes = DesignerRequestExecution.ReadServerLimit();
var admission = DesignerRequestAdmission.FromEnvironment(serverMaxWorkingBytes);
app.Use(async (context, next) =>
{
    DesignerContract.SetResponseHeaders(context.Response);
    await next(context);
});
app.MapGet("/api/designer/health", () => Results.Ok(new { status = "ready", contract = DesignerContract.Description }));
app.MapGet("/api/designer/samples", DesignerRequestProcessor.GetSamples);
app.MapPost("/api/designer/capabilities", context => DesignerRequestExecution.ExecuteAsync(
    context, DesignerRequestProcessor.Capabilities, serverMaxWorkingBytes, admission));
app.MapPost("/api/designer/preview", context => DesignerRequestExecution.ExecuteAsync(
    context, DesignerRequestProcessor.Preview, serverMaxWorkingBytes, admission));
app.MapPost("/api/designer/render", context => DesignerRequestExecution.ExecuteAsync(
    context, DesignerRequestProcessor.Render, serverMaxWorkingBytes, admission));
app.Run();

public partial class Program { }
