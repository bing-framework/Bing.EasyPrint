# DataBar Truncated 与 Stacked Omnidirectional

`Gs1DataBarTruncated=26` 现在可用于 Composite；新增 `Gs1DataBarStackedOmnidirectional=32` 支持普通 ESC/POS Raster 条码和 Composite。原枚举 0–31 不变。两种 Composite 均由 CPCL、TSPL、ZPL 和 ESC/POS Raster 的图片路径输出，ESC/POS Native 或禁用图片的 Profile 拒绝。

## 输入与布局

Composite 主数据接受 13 位 ASCII 数字 payload、校验位正确的 14 位 GTIN，或仅含该 GTIN 的括号 AI01。次数据沿用固定版本完整 GS1 Syntax Dictionary 的 AI 规则。`Auto` 优先 CC-A，容量不足时使用 CC-B；显式 A/B 超容量失败，CC-C 配对拒绝。普通 Type 32 只接受 13 位数字 payload，自动计算校验位；HRI 为 `(01)` 加 14 位 GTIN。

设 X 为模块宽度，H 为条高，R 为二维组件行数；下表尺寸均为旋转前且不含静区：

| 类型 | 宽度 | 高度 | 条高含义 |
| --- | --- | --- | --- |
| Truncated Composite | 100X | H+(2R+1)X | H 至少 13X |
| Stacked Omnidirectional 普通 Raster | 50X | 2H+3X | H 为每条数据行高度，至少 33X |
| Stacked Omnidirectional Composite | 56X | 2H+(2R+4)X | H 为每条数据行高度，至少 33X |

Stacked Omnidirectional 使用两个等高数据行，中间有三个 1X 分隔行；Composite 另有一个 1X 外部分隔行。已有 `Gs1DataBarStacked=30` 仍按总条高 5:7 分配两行、使用一个内部分隔行，两个制式不可互换。Truncated 与 Omnidirectional 使用同形码图和连接位，但允许较低条高。

支持完整符号的 0/90/180/270 度旋转；显式静区和普通条码的 HRI 一起旋转。Composite 不自动添加静区或可读文字，业务方按应用规范配置。预检拒绝页面越界及非法条高，失败不返回部分页面；取消沿令牌传播，失败与取消后释放临时预算。

```csharp
using System;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;

var document = new PrintDocument();
document.AddPage(180, 220).Add(new PrintGs1CompositeElement(
    10, 10, BarcodeType.Gs1DataBarStackedOmnidirectional,
    "(01)12345678901231", "(17)270101(10)LOT123",
    linearBarHeight: 68, moduleWidth: 2, mode: Gs1CompositeMode.Auto,
    quietZone: new PrintQuietZone(8, 4, 8, 4)));
var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);
var pages = provider.Render(document);
Console.WriteLine(pages[0].Length);
```

## 模板、Host 和设计器

V2/V3 的 Type 11 保留原字段；`LinearType` 为 26 或 32，`LinearBarHeight` 遵循上表。普通 Type 4 的 `BarcodeType=32` 仅用于 ESC/POS Raster，`Height` 是每条数据行高度。模板布局使用完整编码高度，不能将总高度直接回填为条高。普通条码模板沿用左右各10X默认静区（例如X=2时完整宽140点）；显式 QuietZone 可覆盖。开启普通HRI须声明足以容纳实际文字墨迹的 HumanReadableReserveHeight，该高度另加到2H+3X之外。

Host 在已有十个 `gs1Composite.linearTypes` 项后追加 `Gs1DataBarTruncated` 和 `Gs1DataBarStackedOmnidirectional`。两者分别要求基础 `gs1-composite` 加 `gs1-composite-truncated` 或 `gs1-composite-stacked-omnidirectional`，以及相应类型和 Auto/CcA/CcB 能力。设计器支持编辑、预览尺寸、导出及项目保存重载；切换制式会将过低条高提高到最小值。旧 Host 缺少对应特性时禁用并显示原因，强制请求也必须通过特性协商。画布在新 Host 下显示真实码图 PNG；旧 Host 仍为不可扫描的布局占位。

案例：[C# 驱动](../samples/Templates/Gs1CompositeSamples.cs)、[Truncated JSON](../samples/Templates/gs1-composite-truncated.json)、[Stacked Omnidirectional JSON](../samples/Templates/gs1-composite-stacked-omnidirectional.json)。执行 `dotnet run --project samples/Templates/Templates.csproj -c Release -- artifacts/template-samples` 验证四协议 C#/JSON/往返一致。

## 验证与边界

[固定向量](../tests/Bing.EasyPrint.Tests/Fixtures/DataBarCompletionVectors.cs) 来自 [Zint 2.15.0 test_rss.c](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/tests/test_rss.c) 和同提交的 [test_composite.c](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/tests/test_composite.c)，覆盖 7 组普通条码（包括 finder=3 特殊分隔）及 5 组 Composite 的连接位、完整二维组件和对齐。Truncated 复用既有独立 Omnidirectional 向量验证。布局参考 [rss.c](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/rss.c)，许可保留在 [ThirdPartyNotices.txt](../src/Bing.EasyPrint/ThirdPartyNotices.txt)。

这些 DataBar 能力与另行接入的 [EAN/UPC 2/5 位附加码](gs1-composite-ean-upc-addons.md)相互独立；ExpandedStacked 自定义列数见[独立配置指南](expanded-stacked-columns.md)。完整 GS1 AI 字典或设计器真实码图预览仍不在本指南范围内。五目标框架保持不变；图片依赖大版本迁移继续延期，现有依赖告警仍需单独处理。目标设备打印与扫码仍需实机确认。[本轮验收报告](databar-completion-verification-2026-10-22.md)记录自动化证据。
