# GS1-128 + CC-A/CC-B Composite

`PrintGs1CompositeElement` 和 V2/V3 的 Type 11 `BpGs1CompositeElement` 支持 `BarcodeType.UccEan128`（11）与显式 `Gs1CompositeMode.CcA`（1）、`CcB`（2）配对。GS1-128 的 `Auto`（0）仍直接选择 CC-C，以保持已有项目的输出兼容。显式 A/B 超容量时返回输入错误，不自动升级模式。

## 输入和尺寸

主数据和次数据均使用当前共享解析器支持的括号式 GS1 AI，例如 `(01)12345678901231` 与 `(17)270101(10)LOT123`；方括号形式也可用。主数据规范化后最多 48 个字符，字段分隔 FNC1 计入长度。此入口不校验完整 AI 业务关系，也不额外保证 GS1-128 主数据的 GTIN 校验位正确。

CC-A/B 使用四列二维组件，各行高 `2 × ModuleWidth`；与线性组件之间的分隔行高 `ModuleWidth`。线性条高仍为 `LinearBarHeight`，单位为打印点。对齐依据实际 Code 128 码字数计算，因此整体宽度不能直接套用 CC-C 的七模块偏移。编码尺寸、显式四边静区和整体旋转共同决定布局占位，页面越界会在输出前拒绝。

CPCL、TSPL、ZPL、ESC/POS Raster 通过共享图片路径输出；ESC/POS Native 和禁止图片的 Profile 不支持。Composite 不内置 HRI，需要可读文字时另加文字元素。

## C# 示例

```csharp
using System;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;

var document = new PrintDocument();
document.AddPage(600, 300).Add(new PrintGs1CompositeElement(
    x: 20, y: 20,
    linearType: BarcodeType.UccEan128,
    primaryData: "(01)12345678901231",
    secondaryData: "(17)270101(10)LOT123",
    linearBarHeight: 40, moduleWidth: 2,
    mode: Gs1CompositeMode.CcA));
var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);
byte[] pageBytes = provider.Render(document)[0];
Console.WriteLine(pageBytes.Length);
```

需要 CC-B 时显式改为 `Gs1CompositeMode.CcB`。可执行 [C# 案例](../samples/Templates/Gs1CompositeSamples.cs)、[CC-A JSON](../samples/Templates/gs1-composite-gs1-128-cc-a.json) 和 [CC-B JSON](../samples/Templates/gs1-composite-gs1-128-cc-b.json) 使用相同主次数据绑定，比较四协议的 C#、JSON 和保存/重载输出：

```powershell
dotnet run --project samples/Templates/Templates.csproj -c Release -- artifacts/template-samples
```

## 设计器和 Host

基础 `gs1-composite` 与可选特性 `gs1-composite-gs1-128-ab`、`UccEan128` 线性能力及 `CcA`/`CcB` 模式能力共同启用此组合。HTTP 路由和能力响应 JSON 结构不变；已有类型/模式数组不能单独表达组合支持，调用方必须检查新增特性。

旧 Host 缺少该特性时，A/B 选项不可用，并显示原因；已协商的 Auto/CC-C 仍可使用。项目导入保留 Mode，不静默替换。预览和导出请求要求新增特性，并校验响应特性，防止被旧 Host 当成其他模式处理。设计器画布在新 Host 下显示真实码图 PNG；旧 Host 仍显示布局占位。Host 返回编码后的实际尺寸。

本阶段自动化验收范围与结果见[GS1-128 A/B 阶段报告](gs1-128-ab-verification-2026-10-22.md)。后续已补齐 [ExpandedStacked](gs1-composite-expanded-stacked.md) 与 [EAN/UPC Composite](gs1-composite-ean-upc.md)，各阶段证据分别记录。
