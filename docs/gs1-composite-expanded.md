# 单行 DataBar Expanded Composite

`PrintGs1CompositeElement` 和 V2/V3 的 `BpGs1CompositeElement` 支持 `BarcodeType.Gs1DataBarExpanded`（数值 28）与 CC-A/CC-B 组合。`Auto` 先尝试 A，容量不足时选择 B；显式 A 不会自动升级。DataBar Stacked Composite 现由[独立使用说明](gs1-composite-stacked.md)描述；ExpandedStacked Composite 现由[独立使用说明](gs1-composite-expanded-stacked.md)描述；DataBar 与 CC-C 配对仍不支持。

## 输入、尺寸与限制

主数据使用圆括号或方括号式 GS1 AI，使用固定版本完整 GS1 Syntax Dictionary 的括号式 AI 规则；例如 `(01)09506000134352(10)LOT-A`。开头 `(01)` 的 GTIN 校验位必须正确。裸 GTIN、原始 FNC1、混合括号、未知 AI、非法字符和超过 22 个符号字符的主数据均被拒绝。次数据同样使用严格括号 AI，并受 A/B 容量限制。输入错误通过 `primaryData`/`secondaryData` 参数名及模板字段路径报告。

编码器在当前实现的 Expanded 方法 1–14 子集中选择专用编码路径：方法 3–14 的压缩仅用于 GTIN-14 首位为 9 的输入；固定重量或日期方法只有在完整消费其要求的数据字段时才使用，后续仍有数据时回退到通用字段编码。普通 `PrintBarcodeElement` 的 Expanded 路径已修正历史上多出的 4 位数据；Composite 主数据的错误诊断也归属 `primaryData`。公开签名和枚举值不变，符合原有合法格式的输入仍可用。修正会改变部分码图和实际尺寸，已有模板及固定布局请重新预览并校准后再输出。需要线性与二维连接标志仍须使用 Composite 元素。

模块宽度、条高、静区均为 dot。二维组件每行高 2X，分隔行高 X，线性条高由调用方指定；完整符号含静区后再整体旋转。宽度随主数据增长，必须给页面和单元格留足空间，超界会明确失败。元素不内置 HRI，可另外添加文字。条高与静区是否符合使用场景的 GS1 要求仍需调用方和实机验收确认。

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
    linearType: BarcodeType.Gs1DataBarExpanded,
    primaryData: "(01)09506000134352(10)LOT-A",
    secondaryData: "(17)270101(10)LOT123",
    linearBarHeight: 68,
    moduleWidth: 2,
    mode: Gs1CompositeMode.Auto));
var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);
Console.WriteLine(provider.Render(document)[0].Length);
```

CPCL、TSPL、ZPL 和 ESC/POS Raster 复用主机光栅输出；ESC/POS Native 不支持。检查 `SupportsGs1Composite`，同时确认具体线性类型/模式配对。

## 模板和浏览器

沿用 `Type=11`、`Field` 主数据和 `SecondaryField` 次数据；`LinearType=28`、`Mode=0/1/2` 分别表达 Auto/A/B，既有 JSON 字段和枚举值不变。[JSON 案例](../samples/Templates/gs1-composite-expanded.json) 与 [C# 案例](../samples/Templates/Gs1CompositeSamples.cs) 会逐协议比较 C#/JSON/保存重载输出：

```powershell
dotnet run --project samples/Templates/Templates.csproj -c Release -- artifacts/expanded-samples
```

新 Host 追加 `gs1-composite-expanded` 特性和 `Gs1DataBarExpanded` 能力值。浏览器必须同时取得基础 `gs1-composite`、新特性和能力声明才启用此选项；旧 Host 仍可使用其已有制式。切换线性类型保留非空主数据，需要自行将旧 GTIN 改成括号 AI。

编码与参考矩阵来源为 [Zint 2.15.0 rss.c](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/rss.c) 和 [Composite 固定向量](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/tests/test_composite.c)。许可随源码和候选包保留；软件矩阵与解码测试不代替实机扫描。
