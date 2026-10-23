# EAN/UPC Composite 附加码使用说明

EAN-8、EAN-13、UPC-A、UPC-E Composite 现支持附加 2 位或 5 位数字码。该能力扩展 Type 11 GS1 Composite 的主数据格式，不改变普通 Type 4 EAN/UPC 条码，也不表示完整 GS1 Composite 制式均已实现。基础零售 Composite 的主码和 Host 接入说明见 [EAN/UPC Composite 使用说明](gs1-composite-ean-upc.md)。

## 输入与模式

主字段采用 `主码+附加码`，中间必须恰好一个 ASCII `+`。附加码只能是恰好 2 位或 5 位 ASCII 数字；不接受空白、不执行 trim、不补前导零，也不接受多个加号。主码仍按既有 EAN/UPC 规则校验：可省略末位校验位由库计算，也可提供正确校验位；提供错误校验位会拒绝。

| 线性组件 | 主码输入长度 | 附加码长度 |
| --- | --- | --- |
| EAN-8 | 7 位数据或 8 位含校验位 | 2 或 5 位 |
| EAN-13 | 12 位数据或 13 位含校验位 | 2 或 5 位 |
| UPC-A | 11 位数据或 12 位含校验位 | 2 或 5 位 |
| UPC-E | 6、7 或 8 位 | 2 或 5 位 |

UPC-E 的六位主码隐含号码系统 0，七位包含号码系统，八位还包含校验位；号码系统仍只允许 0 或 1，零压缩和校验位规则保持原有约束。加号只分隔主码和附加码，不属于零售主码。

四种线性组件支持 `Auto`、`CcA`、`CcB`，不支持 `CcC`。`Auto` 先尝试 CC-A，容量不足时选择 CC-B；显式 A/B 容量不足会报错。次字段仍使用当前支持的括号式 GS1 AI 子集，业务层负责校验 AI 之间的关系。

## 尺寸与输出

下表是 X=1 时的完整符号宽度，包含二维组件和附加码，但不含调用方显式添加的静区。EAN-8 在 A/B 模式下宽度不同；其他三种线性组件的线性/附加码宽度决定完整宽度。

| 线性组件 | CC-A：2 位 / 5 位 | CC-B：2 位 / 5 位 |
| --- | ---: | ---: |
| EAN-8 | 98 / 125 | 108 / 135 |
| EAN-13 | 125 / 152 | 125 / 152 |
| UPC-A | 127 / 154 | 127 / 154 |
| UPC-E | 81 / 108 | 81 / 108 |

设模块宽度为 X、二维组件行数为 R，线性条高为 H 点，旋转前高度为 `(2R + 6) × X + H`，再加上下静区。三条分隔行各高 `2X`，只围住零售主码；附加码在主码右侧，UPC-A 主码与附加码之间留 `9X`，其他三种留 `7X`。Composite 不含 HRI；需要可读文字时另放文字元素。

```csharp
using System;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;

var document = new PrintDocument();
document.AddPage(400, 300).Add(new PrintGs1CompositeElement(
    10, 10, BarcodeType.Ean13, "1234567890128+51234", "(17)270101(10)LOT123",
    linearBarHeight: 80, moduleWidth: 2, mode: Gs1CompositeMode.CcB,
    quietZone: new PrintQuietZone(20, 8, 20, 8)));
var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);
var pages = provider.Render(document);
Console.WriteLine(pages[0].Length);
```

该例 X=2，EAN-13 五位附加码在 CC-B 下宽 152 模块，即码图宽 304 点；左右各 20 点静区后完整占位宽 344 点，从 x=10 开始到 354 点，400 点页宽可容纳。替换数据、模式、模块宽或静区后，应重新预览并确认已有布局边界。

CPCL、TSPL、ZPL、ESC/POS Raster 通过主机图片路径输出；ESC/POS Native 和禁用图片的 Profile 明确不支持 Composite。码图按 Raster 语义输出，不提供内置 HRI。目标固件表现和真实扫码结果仍需在对应设备上验证，本指南不声明实机验收完成。

## 模板与 Host 协商

V2/V3 Type 11 的 `Field` 绑定 `主码+附加码`，`SecondaryField` 绑定 GS1 AI 次数据。无需新增元素类型或枚举；`LinearType` 仍是既有 EAN-8/EAN-13/UPC-A/UPC-E 数值，`Mode` 使用 Auto/A/B。

新 Host 公布 `gs1-composite-ean-upc-addons` 特性，并在 `gs1Composite.primaryFormats` 末尾追加 `ean-upc-digits-with-addon`；原有 `ean-upc-digits` 及 `linearTypes`（12 项）、`modes` 保持不变。固定 `Field.Value` 含附加码时，设计器会发送独立的必需特性并检查 Host 响应。`Field.Name` 动态绑定值在 Host 完成绑定后进行严格输入校验，浏览器不会提前推断其值并协商；旧 Host 的纯数字预检会明确拒绝带加号的动态值。缺少新特性的旧 Host 仍可处理不含附加码的 EAN/UPC Composite。

设计器画布仍显示不可扫描的布局占位，Host 在线预览使用实际编码尺寸。示例 JSON：[EAN-8](../samples/Templates/gs1-composite-ean8-addon.json)、[EAN-13](../samples/Templates/gs1-composite-ean13-addon.json)、[UPC-A](../samples/Templates/gs1-composite-upca-addon.json)、[UPC-E](../samples/Templates/gs1-composite-upce-addon.json)；C# 四协议字节往返驱动见 [Gs1CompositeSamples.cs](../samples/Templates/Gs1CompositeSamples.cs)。运行 `dotnet run --project samples/Templates/Templates.csproj -c Release -- artifacts/template-samples` 可比较 C#、JSON 与 JSON 往返输出。

## 独立向量

以下 8 组完整矩阵固定自 Zint 2.15.0 commit `4896136bdbebb1a0908a5cc419513efa5bf4bf70` 的 `backend/tests/test_composite.c` cases 71–78。它们覆盖四种主码、2/5 位附加码和 CC-A/CC-B；来源实现见 [Zint upcean.c](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/upcean.c)。运行时不启动外部编码程序，许可见 [ThirdPartyNotices.txt](../src/Bing.EasyPrint/ThirdPartyNotices.txt)。

| 案例 | 线性组件 | 模式 | 输入 | 固定矩阵 |
| --- | --- | --- | --- | --- |
| 71 | EAN-13 | CC-A | `123456789012+12` | [fixture](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeEanUpcAddonVectors.cs#L40) |
| 72 | EAN-13 | CC-A | `123456789012+54321` | [fixture](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeEanUpcAddonVectors.cs#L51) |
| 73 | UPC-A | CC-A | `12345678901+12` | [fixture](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeEanUpcAddonVectors.cs#L61) |
| 74 | UPC-A | CC-B | `12345678901+12121` | [fixture](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeEanUpcAddonVectors.cs#L71) |
| 75 | UPC-E | CC-A | `0654321+89` | [fixture](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeEanUpcAddonVectors.cs#L82) |
| 76 | UPC-E | CC-B | `1876543+56789` | [fixture](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeEanUpcAddonVectors.cs#L94) |
| 77 | EAN-8 | CC-A | `9876543+65` | [fixture](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeEanUpcAddonVectors.cs#L109) |
| 78 | EAN-8 | CC-B | `9876543+74083` | [fixture](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeEanUpcAddonVectors.cs#L120) |

本功能是 EAN/UPC 主码右侧的附加组件，不是 DataBar Stacked 的上下线性数据行，也不是 GS1 DataBar ExpandedStacked 的多行扩展组件。相关功能分别见 [Stacked Composite](gs1-composite-stacked.md) 与 [ExpandedStacked](gs1-composite-expanded-stacked.md)。本阶段自动化验证结果由[阶段验收报告](ean-upc-addons-verification-2026-10-22.md)记录。
