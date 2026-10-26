# DemoProtocol 示例

该程序集演示 O4 新协议注册，不修改 `PrintProtocol` 枚举、内置 Provider 分发或设计器静态列表。协议 ID 为 `demo.protocol`。

## 支持范围

- 元素：文字 `1`、二维码 `3`、一维码 `4`、线段 `5`、矩形 `7`。
- 一维码：`Code128`、`Code39`、`Ean13`；二维码：QR。
- 可声明任务收尾，输出独立控制文件。
- 不声明图片、GS1 Composite、完整页面 PNG；示例不冒充真实打印机固件。

## 配置

```json
{
  "characterWidth": 6,
  "lineHeight": 24,
  "maxPageHeight": 600,
  "supportsFinishing": true,
  "barcodeModuleWidth": 2,
  "qrModuleWidth": 4,
  "dpi": 203
}
```

`qrModuleWidth` 或 `barcodeModuleWidth` 改变会改变实际布局宽度；`maxPageHeight` 参与自动页高分页；`dpi` 只接受 `203/300/600` 并进入能力快照；最大文字字号为 512，513 在编码前拒绝；`supportsFinishing=false` 时任何收尾请求在编码前拒绝。示例不声明 GS1 QR、图片和完整页面 PNG。

## 运行案例

`demo-project.json` 是设计器 v2 项目，包含文字、QR、收尾配置和编辑器状态。可在仓库根目录运行 Host 与设计器，然后通过“导入”加载该文件，或直接使用：

```powershell
dotnet run --project tools/DesignerHost/DesignerHost.csproj
cd web/Bing.EasyPrint.Designer
npm run dev
```

C# 集成案例见 `tests/Bing.EasyPrint.Tests/ProtocolRegistryTests.cs`、`tests/Bing.EasyPrint.Tests/TemplateCustomProtocolTests.cs` 和 `tests/DesignerHost.Tests/ProtocolRegistryHostTests.cs`；真实浏览器案例见 `web/Bing.EasyPrint.Designer/e2e/customProtocol.spec.ts`。
