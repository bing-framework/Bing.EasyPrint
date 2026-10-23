# EAN/UPC Composite 使用说明

EAN-8、EAN-13、UPC-A、UPC-E 现可作为 `PrintGs1CompositeElement` 和 V2/V3 `BpGs1CompositeElement` 的线性组件。CPCL、TSPL、ZPL、ESC/POS Raster 共用主机编码与图片路径；ESC/POS Native 和禁用图片的 Profile 明确拒绝。

## 输入与模式

| 线性组件 | 既有枚举值 | 主数据长度 | 二维列数 | 基础完整宽度（模块，不含附加码和静区） |
| --- | --- | --- | --- | --- |
| Ean8 | 4 | 7 位数据或 8 位含校验位 | 3 | CC-A 72，CC-B 82 |
| Ean13 | 5 | 12 位数据或 13 位含校验位 | 4 | 99 |
| UpcA | 6 | 11 位数据或 12 位含校验位 | 4 | 99 |
| UpcE | 7 | 6/7/8 位 | 2 | 55 |

纯主码仅允许 ASCII 数字。完整输入的校验位必须正确；少一位时自动计算，不截断或任意补零，也不接受括号 AI。Type 11 另支持严格的 `主码+附加码`：恰好一个 `+`，附加码只能为 2 或 5 位 ASCII 数字，不 trim 或补零；完整尺寸和输入约束见[附加码使用说明](gs1-composite-ean-upc-addons.md)。UPC-E 六位数据隐含号码系统 0；七位包含号码系统；八位另含校验位，号码系统只允许 0/1。六位压缩数据末位为 3 时第三位不得为 0/1/2，为 4 时第四位不得为 0，为 5–9 时第五位不得为 0。校验位按展开的 UPC-A 计算。

次数据使用共享解析器支持的括号式 GS1 AI。`Auto` 先尝试 CC-A，仅在容量不足时改用 CC-B；显式模式容量不足直接失败。EAN/UPC + CC-C 拒绝，GS1-128 的 Auto 仍选择 CC-C。此能力不改变普通 EAN/UPC 原生命令的输入约束。

## 尺寸与输出

设模块宽度为 X、二维组件行数为 R，`LinearBarHeight` 为 H 点，则旋转前高度为 `(2R + 6) × X + H`，再增加上下静区。二维行及三条分隔行各为 2X。左右静区增加在表中宽度之外。完整符号支持 0/90/180/270 度旋转，包含分隔、静区一起变换；坐标仍表示旋转后完整占位左上角。

H 必须大于零；调用方按应用标准选择条高和静区，库不自动添加行业静区或 HRI。需要可读文字时使用独立文字元素。预检失败不返回部分页面，取消沿既有令牌传播，临时编码预算在异常/取消后释放。

```csharp
using System;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;

var document = new PrintDocument();
document.AddPage(320, 180).Add(new PrintGs1CompositeElement(
    10, 10, BarcodeType.Ean13, "1234567890128", "(17)270101(10)LOT123",
    linearBarHeight: 80, moduleWidth: 2, mode: Gs1CompositeMode.Auto,
    quietZone: new PrintQuietZone(20, 8, 20, 8)));
var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);
var pages = provider.Render(document);
Console.WriteLine(pages[0].Length);
```

## 模板、Host 与设计器

Type 11 的 `Field` 绑定上述数字主数据，`SecondaryField` 绑定 AI 次数据。保留现有模板字段、枚举和 HTTP 路由。设计器可以选择四个零售组件、编辑条高/模块宽度/次数据、预览实际尺寸、导出指令并保存重载项目；非空主数据不会因切换制式而自动改写。

除基础 `gs1-composite` 外，EAN/UPC 需要 `gs1-composite-ean-upc` 特性和相应 `linearTypes`、Auto/CcA/CcB 能力；附加码另需 `gs1-composite-ean-upc-addons`，新 Host 的 `primaryFormats` 在 `ean-upc-digits` 后追加 `ean-upc-digits-with-addon`。`linearTypes` 共 12 项且 `modes` 不变。固定 `Field.Value` 附加码由设计器发送独立必需特性并校验响应；动态 `Field.Name` 值在 Host 完成绑定后严格校验，浏览器不会提前推断其值并协商；旧 Host 的纯数字预检会明确拒绝带加号的动态值。画布目前仍为不可扫描的布局占位，Host 返回编码尺寸，并非实际码图预览。

独立案例：[C# 驱动](../samples/Templates/Gs1CompositeSamples.cs)、[EAN-8 JSON](../samples/Templates/gs1-composite-ean8.json)、[EAN-13 JSON](../samples/Templates/gs1-composite-ean13.json)、[UPC-A JSON](../samples/Templates/gs1-composite-upca.json)、[UPC-E JSON](../samples/Templates/gs1-composite-upce.json)；附加码四制式 JSON 见[附加码使用说明](gs1-composite-ean-upc-addons.md#模板与-host-协商)。运行 `dotnet run --project samples/Templates/Templates.csproj -c Release -- artifacts/template-samples` 会验证四协议 C#/JSON/往返字节一致。

## 验证与边界

[19 组纯主码独立码图](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeEanUpcVectors.cs) 固定自 [Zint 2.15.0 的 test_composite.c](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/tests/test_composite.c)；另有 8 组附加码完整矩阵，见[附加码向量表](gs1-composite-ean-upc-addons.md#独立向量)。分隔与对齐参考同一提交的 [upcean.c](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/upcean.c) 和 [composite.c](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/composite.c)，许可保留于 [ThirdPartyNotices.txt](../src/Bing.EasyPrint/ThirdPartyNotices.txt)。运行时使用已有 ZXing 线性编码器和托管 Composite 实现，不启动外部编码程序。

后续阶段已补齐 [DataBar Truncated/StackedOmnidirectional Composite](gs1-composite-databar-completion.md)、[EAN/UPC 附加码](gs1-composite-ean-upc-addons.md)和[ExpandedStacked 自定义列数](expanded-stacked-columns.md)。完整 GS1 AI 字典仍未包含。软件自动化结果分别由[基础 EAN/UPC 报告](ean-upc-composite-verification-2026-10-22.md)和[附加码报告](ean-upc-addons-verification-2026-10-22.md)记录；历史报告保留原范围。目标设备打印及扫码验收仍需针对实际机型单独确认。
