# GS1 DataBar ExpandedStacked

`BarcodeType.Gs1DataBarExpandedStacked`（数值 31）支持普通 ESC/POS Raster 条码，以及 CPCL、ESC/POS Raster、TSPL、ZPL 的 Composite CC-A/CC-B。已有枚举值和成员签名保持不变。普通 CPCL、TSPL、ZPL 和 ESC/POS Native 不声明此条码能力。

## 输入与布局

普通条码沿用 [Expanded 输入规则](gs1-composite-expanded.md)对应的现有普通条码规范化入口；Composite 主数据必须使用圆括号或方括号 AI，例如 `(01)09501101530003(17)140704(10)AB-123`。Composite 开头 `(01)` 的 GTIN 校验位会校验；未知 AI、混合括号及容量超限会被拒绝。共享 AI 解析器仍是已声明的子集，不保证完整 AI 字典或字段间业务关系。

布局固定为每行两个字符对，即最多四个符号字符。编码器根据内容生成 1–6 条数据行，完整线性宽度为 102 模块；每两条数据行之间有三个 1X 分隔行。最后一行不足一对时按标准补位；反向行、短末行、保护条及定位符分隔规则在编码器内处理。当前不提供列数或最大行数配置。

新类型的普通 `BarHeight`/模板 `Height` 和 Composite `LinearBarHeight` **均为每条数据行的高度，单位为 dot**，至少为模块宽度 1X，建议至少 34X。行间分隔行不包含在该高度内。现有 `Gs1DataBarStacked` 的上下行总高语义保持不变。

若数据行数为 N、每行条高为 H、模块宽为 X，则普通符号高度为 `N × H + 3 × (N - 1) × X`；Composite 另加 `2 × CC行数 × X + X`，再加四边静区并整体旋转。上述主数据在 X=2、H=35 时，普通符号为 204×117 dot。尺寸不足会在输出前失败，模板布局与 Host 预览使用实际多行尺寸。

Composite 的 CC-A/B 二维组件采用四列。`Auto` 优先 CC-A，容量不足时使用 CC-B；显式 A/B 不会升级，CC-C 配对被拒绝。Composite 不内置 HRI，可添加独立文字元素。

## C# 与模板案例

```csharp
using System;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;

var document = new PrintDocument();
document.AddPage(300, 300).Add(new PrintGs1CompositeElement(
    10, 10, BarcodeType.Gs1DataBarExpandedStacked,
    "(01)09501101530003(17)140704(10)AB-123", "(21)12345678",
    linearBarHeight: 68, moduleWidth: 2, mode: Gs1CompositeMode.Auto));
var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);
Console.WriteLine(provider.Render(document)[0].Length);
```

普通条码使用 `PrintBarcodeElement` 或 `EscPosPrintCommand.RasterBarcode`，制式为同一枚举；须选择 ESC/POS Raster。模板普通条码使用 `Type=4`、`BarcodeType=31`；Composite 使用 V2/V3 `Type=11`、`LinearType=31`，通过 `Field`/`SecondaryField` 绑定主次数据。

[C# 案例](../samples/Templates/Gs1CompositeSamples.cs)与 [JSON 案例](../samples/Templates/gs1-composite-expanded-stacked.json)比较四协议 C#/JSON/保存重载的输出字节：

```powershell
dotnet run --project samples/Templates/Templates.csproj -c Release -- artifacts/expanded-stacked-samples
```

## Host 与设计器

Host 在原有 Composite 线性能力数组末尾追加 `Gs1DataBarExpandedStacked`，并声明 `gs1-composite-expanded-stacked`。设计器要求基础 `gs1-composite`、该新特性、对应线性能力和 A/B 模式能力；旧 Host 缺少特性时明确禁用该 Composite 选项。普通条码按 `barcodeTypes` 的真实能力开放。

选择新制式后可以编辑模块宽度、每行条高、主次数据、旋转和静区，并保存、重载或导出。切换制式保留非空原数据，不会将裸 GTIN 静默转换为括号 AI。在线预览使用 Host 返回的真实尺寸；当前画布仍显示不可扫描的布局占位，实际条码位图预览属于后续阶段。

布局和固定矩阵来源为 [Zint 2.15.0 rss.c](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/rss.c)、[普通条码向量](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/tests/test_rss.c)和 [Composite 向量](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/tests/test_composite.c)。许可保留在源码与包中；本阶段按用户要求不执行实机验收。
