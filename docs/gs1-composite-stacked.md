# DataBar Stacked Composite

`PrintGs1CompositeElement` 和 V2/V3 的 Type 11 `BpGs1CompositeElement` 支持 `BarcodeType.Gs1DataBarStacked`（数值 30）与 CC-A/CC-B 组合。此处的 Stacked 是两行 DataBar 线性组件连接 CC-A 或 CC-B，不表示所有 GS1 Composite 制式均已实现。

## 支持范围

Auto、CC-A 和 CC-B 分别对应 `Gs1CompositeMode.Auto`、`CcA`、`CcB`，以及模板 `Mode` 数值 0、1、2。Auto 先尝试 CC-A，容量不足时选择 CC-B；显式 CC-A 容量不足时不会自动改为 CC-B。CC-C 不支持此 DataBar 组合。

Composite 在 CPCL、TSPL、ZPL 和 ESC/POS Raster 输出中由主机生成图形；ESC/POS Native 不支持 Composite。该元素不内置 HRI，需要可读文字时另加文字元素。自动化向量和软件验证不能代替目标固件的打印质量和实机扫描验收。

主数据可使用 13 位 GTIN、校验位正确的 14 位 GTIN，或带括号 AI 的 `(01)` 与有效 GTIN-14，例如 `(01)09506000134352`。GTIN 校验位会校验。次数据必须是共享解析器当前支持的括号式 GS1 AI 子集；这不代表完整 AI 字典，也不校验所有 AI 之间的业务关系。

## 尺寸

Stacked 使用两列 CC-A/CC-B。线性 DataBar 为 50 个模块宽；两列二维组件加横向对齐后，完整复合码固定为 56 个模块宽（不含显式静区），高度随次数据容量变化。`ModuleWidth`（记为 X）与 `LinearBarHeight` 均以打印点为单位。Stacked 的 `LinearBarHeight` 是上下两条线性数据行的总高，不包含行间的内部一模块分隔行，并且必须满足 `LinearBarHeight >= 12 × ModuleWidth`：上行高度向下取整为 `floor(5 × LinearBarHeight / 12)`，下行使用余量。二维组件与线性组件之间的外部分隔行也为一模块高。比如模块宽 2、总条高 40 时，上行 16 点、下行 24 点，两条数据行之间另有 2 点内部间隔。

## C# 示例

```csharp
using System;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;

var document = new PrintDocument();
document.AddPage(600, 300).Add(new PrintGs1CompositeElement(
    x: 20,
    y: 20,
    linearType: BarcodeType.Gs1DataBarStacked,
    primaryData: "(01)09506000134352",
    secondaryData: "(10)LOT-A(17)270101",
    linearBarHeight: 40,
    moduleWidth: 2,
    mode: Gs1CompositeMode.Auto));

var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);
byte[] pageBytes = provider.Render(document)[0];
Console.WriteLine(pageBytes.Length);
```

可将 Provider 改为 CPCL、TSPL、ZPL 或 ESC/POS Raster 的目标设置；ESC/POS Native 会拒绝该 Composite。

## 模板 JSON

[`samples/Templates/gs1-composite-stacked.json`](../samples/Templates/gs1-composite-stacked.json) 是 V3 Type 11 模板示例。它使用 `LinearType: 30`、`Mode: 0`、模块宽 2、总条高 40，并通过 `Field.Name` 与 `SecondaryField.Name` 绑定业务数据：

```json
{
  "Primary": "(01)09506000134352",
  "Secondary": "(10)LOT-A(17)270101"
}
```

使用模板时，主次数据字段分别映射到 Composite 的主数据和次数据。模板只支持 V2/V3；导入时会校验线性制式、模式、字段和条高约束。

## 设计器与 Host

Host 通过 `gs1-composite-stacked` 特性和 `gs1Composite.linearTypes` 中的 `Gs1DataBarStacked` 声明协商此能力。新 Host 在现有 Omnidirectional、Limited、UccEan128、Expanded 能力值之后追加 Stacked，`modes` 保持 `Auto`、`CcA`、`CcB`、`CcC`。旧 Host 缺少特性或线性类型数组时，设计器禁用 Stacked 并提示该 Host 不支持；直接提交的预览或渲染请求会被拒绝，其他已声明的旧组合继续可用。

本页支持的是标准两行 DataBar Stacked 作为 Composite 线性组件；它不同于单行 DataBar Expanded，ExpandedStacked 已在[独立阶段](gs1-composite-expanded-stacked.md)实现，EAN/UPC 已在[后续阶段](gs1-composite-ean-upc.md)实现。当前验证范围见[Stacked 阶段报告](gs1-stacked-verification-2026-10-22.md)。

GS1-128 显式 A/B 已在后续阶段补齐，见[使用说明](gs1-composite-gs1-128-ab.md)。
