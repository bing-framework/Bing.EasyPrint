using System.Text;
using Bing.EasyPrint.Templates;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = DesignerRequestProcessor.MaxRequestBytes);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("EASYPRINT_DESIGNER_API_URL") ?? "http://127.0.0.1:5187");

var app = builder.Build();
var protocolRegistry = DesignerProtocolRegistry.CreateDefault();
DesignerRequestProcessor.ProtocolRegistry = protocolRegistry;
var serverMaxWorkingBytes = DesignerRequestExecution.ReadServerLimit();
var admission = DesignerRequestAdmission.FromEnvironment(serverMaxWorkingBytes);
app.Use(async (context, next) =>
{
    DesignerContract.SetResponseHeaders(context.Response);
    await next(context);
});
app.MapGet("/api/designer/health", () => Results.Ok(new { status = "ready", contract = DesignerContract.Description }));
app.MapGet("/api/designer/samples", DesignerRequestProcessor.GetSamples);
app.MapGet("/api/designer/gs1-catalog", DesignerRequestProcessor.Gs1Catalog);
app.MapGet("/api/designer/protocol-registry", () => Results.Json(new
{
    contract = DesignerContract.Description,
    protocols = protocolRegistry.Descriptors.Select(descriptor => new
    {
        id = descriptor.Id,
        displayName = descriptor.DisplayName,
        isBuiltIn = descriptor.IsBuiltIn,
        configSchema = ParseJsonOrNull(descriptor.ConfigSchemaJson),
        constraints = ParseJsonOrNull(descriptor.ConstraintsJson),
        capabilities = descriptor.Capabilities is null
            ? ParseJsonOrNull(descriptor.CapabilitiesJson)
            : System.Text.Json.JsonSerializer.SerializeToElement(descriptor.Capabilities,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
    })
}, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
app.MapPost("/api/designer/capabilities", context => DesignerRequestExecution.ExecuteAsync(
    context, DesignerRequestProcessor.Capabilities, serverMaxWorkingBytes, admission));
app.MapPost("/api/designer/preview", context => DesignerRequestExecution.ExecuteAsync(
    context, DesignerRequestProcessor.Preview, serverMaxWorkingBytes, admission));
app.MapPost("/api/designer/preview-bundle", context => DesignerRequestExecution.ExecuteAsync(
    context, DesignerRequestProcessor.PreviewBundle, serverMaxWorkingBytes, admission));
app.MapPost("/api/designer/render", context => DesignerRequestExecution.ExecuteAsync(
    context, DesignerRequestProcessor.Render, serverMaxWorkingBytes, admission));
app.MapPost("/api/designer/batch", context => DesignerRequestExecution.ExecuteAsync(
    context, DesignerRequestProcessor.BatchRender, serverMaxWorkingBytes, admission));
app.MapPost("/api/designer/gs1-validate", context => DesignerRequestExecution.ExecuteAsync(
    context, DesignerRequestProcessor.Gs1Validate, serverMaxWorkingBytes, admission));
app.MapPost("/api/designer/gs1-convert", context => DesignerRequestExecution.ExecuteAsync(
    context, DesignerRequestProcessor.Gs1Convert, serverMaxWorkingBytes, admission));
app.MapPost("/api/designer/element-png", context => DesignerRequestExecution.ExecuteAsync(
    context, DesignerRequestProcessor.ElementPng, serverMaxWorkingBytes, admission));
app.MapPost("/api/designer/page-png", context => DesignerRequestExecution.ExecuteAsync(
    context, DesignerRequestProcessor.PagePng, serverMaxWorkingBytes, admission));
app.MapPost("/api/designer/page-pdf", context => DesignerRequestExecution.ExecuteAsync(
    context, DesignerRequestProcessor.PagePdf, serverMaxWorkingBytes, admission));
app.MapPost("/api/designer/element-svg", context => DesignerRequestExecution.ExecuteAsync(
    context, DesignerRequestProcessor.ElementSvg, serverMaxWorkingBytes, admission));
app.Run();

static System.Text.Json.JsonElement? ParseJsonOrNull(string json)
{
    if (string.IsNullOrWhiteSpace(json)) return null;
    using var document = System.Text.Json.JsonDocument.Parse(json);
    return document.RootElement.Clone();
}
public partial class Program { }
