# ExpandedStacked 列数配置

普通 ESC/POS Raster 条码与四协议 Composite 均支持配置 ExpandedStacked 每行的字符对数上限。省略或 `null` 使用两列，原有构造函数、方法和默认字节输出保持兼容。

| 入口 | 配置范围 | 输出协议 |
| --- | --- | --- |
| `PrintBarcodeElement` / Type=4、BarcodeType=31 | 1–11 | ESC/POS Raster |
| `PrintGs1CompositeElement` / Type=11、LinearType=31 | 2–11 | CPCL、ESC/POS Raster、TSPL、ZPL |

“列”指每行的字符对数上限，与打印点宽度、CC-A/B 的二维组件列数不同。Composite 首行至少容纳四个符号字符，因此单列被明确拒绝。普通 CPCL、TSPL、ZPL 和 ESC/POS Native 不支持此条码制式。

## C# 入口

```csharp
using System;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;

const string primary = "[91]1234567890123456789012345678901234";
var size = EscPosRasterCanvas.MeasureExpandedStackedSize(primary, 35, 2, 3);
Console.WriteLine($"普通三列编码区：{size.Width}×{size.Height} dot"); // 302×76

var original = new PrintGs1CompositeElement(0, 0,
    BarcodeType.Gs1DataBarExpandedStacked, primary, "[20]12",
    linearBarHeight: 35, moduleWidth: 2, mode: Gs1CompositeMode.CcA);
var threeColumns = original.WithExpandedStackedColumns(3); // 返回副本，original 仍为缺省两列
var document = new PrintDocument();
document.AddPage(302, 90).Add(threeColumns);
var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);
Console.WriteLine(provider.Render(document)[0].Length);
```

普通模型同样使用 `.WithExpandedStackedColumns(columns)`。直接光栅入口为 `EscPosRasterCanvas.DrawExpandedStackedBarcode(x, y, data, height, columns, ...)` 和 `EscPosPrintCommand.RasterExpandedStackedBarcode(x, y, data, height, columns, ...)`；后者要求先开始 Raster 页。模块宽默认 2 dot，可读文字默认关闭，旋转默认 0。可读文字、字体、预留高度和旋转参数沿用普通 Raster 条码契约。

测量入口 `MeasureExpandedStackedSize(data, rowHeight, moduleWidth, columns, cancellationToken)` 返回旋转前编码区，不含显式静区或可读文字。测量临时资源在返回或异常时释放；绘制观察画布/光栅页取消令牌，失败时回滚本次绘制。模型副本不持有外部资源，保留坐标、尺寸、静区、旋转和诊断路径，并按现有资源作用域记录元数据。

## 模板和尺寸

V2/V3 Composite 元素示例：

```json
{
  "Type": 11,
  "LinearType": 31,
  "ExpandedStackedColumns": 3,
  "Mode": 1,
  "ModuleWidth": 2,
  "LinearBarHeight": 35,
  "Field": { "Value": "[91]1234567890123456789012345678901234" },
  "SecondaryField": { "Value": "[20]12" }
}
```

普通 Type=4 使用同名字段、`BarcodeType=31`、`Height` 和 `NarrowModuleWidth`。列数只允许整数；`0`、`12`、`2.5`、`"3"`、布尔值和其他制式上的非空列数均失败。.NET 模板加载器继续兼容属性名大小写，但同一元素不允许以大小写别名重复声明列数。设计器导入和浏览器请求要求规范名称 `ExpandedStackedColumns`；其他大小写会明确报错，避免漏掉旧 Host 的特性协商。省略或 `null` 不要求新能力，保存重载保留显式配置。列数不是数据绑定表达式。

每条数据行高度 H 至少为模块宽度 X，建议 34X。普通高度为 `N×H + 3×(N−1)×X`；Composite 再加 `2×CC行数×X + X`，随后加静区并旋转。短末行按标准补齐，不强制填满列数上限。内容、补齐与列数共同决定实际行数和宽度，不能把宽度一律写成 `49×列数+4`。

以上主数据、次数据、CC-A、H=35、X=2、无静区、不旋转时：

| 列数 | 普通编码区 dot | Composite dot |
| --- | --- | --- |
| 2 | 204×117 | 204×131 |
| 3 | 302×76 | 302×90 |
| 4 | 400×76 | 400×90 |
| 11 | 596×35 | 596×49 |

统一模型、模板布局、Host 预览、旋转与静区预检都使用实际编码尺寸。超出页面或占用静区时在输出前报错；非法列数不会静默回退为两列。

## Host 和设计器

新特性为 `gs1-databar-expanded-stacked-columns`。Host 顶层 `expandedStackedColumns` 在支持普通 Type=31 的 Raster 模式返回 `{minimum:1, maximum:11, default:2}`，其他模式为 `null`；支持 Composite 时 `gs1Composite.expandedStackedColumns` 返回 `{minimum:2, maximum:11, default:2}`。CC-A/B 组件仍使用四列。

设计器仅为 ExpandedStacked 显示列数选项。显式设置（包括 2）要求该协商特性和对应范围；旧 Host 可继续处理缺省两列，显式配置会显示不支持原因并阻止预览/导出，用户数据不会被删除。切换到其他制式是主动编辑操作，会移除不适用的列数。

真实 Host 验收覆盖编辑、实际尺寸、导出、项目保存导入和旧 Host 降级。画布在新 Host 下显示真实码图 PNG；旧 Host 仍显示不可扫描的布局占位。复杂字体排版不属于本阶段。最大数据行数、完整 GS1 AI 字典见[最大行数与 AI 字典](expanded-stacked-rows-and-gs1-ai.md)。

## 案例与验证

- [C# 案例](../samples/Templates/Gs1CompositeSamples.cs)、[普通单列 JSON](../samples/Templates/expanded-stacked-columns.json)、[Composite 三列 JSON](../samples/Templates/gs1-composite-expanded-stacked-columns.json)：`dotnet run --project samples/Templates/Templates.csproj -c Release -- artifacts/expanded-stacked-columns-samples`。
- [核心测试](../tests/Bing.EasyPrint.Tests/ExpandedStackedColumnsTests.cs)：普通 28 组独立全矩阵；Composite 40 组独立全矩阵，覆盖 2–11 列、短/长主数据、CC-A/B。
- 普通矩阵摘自固定 [Zint test_rss.c](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/tests/test_rss.c)。其中 6 组涉及额外 AI 17，使用现有已规范化原始输入入口；不扩大括号 AI 解析子集。
- Composite 矩阵由相同固定版本 Zint 编译后生成，命令和中间输出记录于 `artifacts/expanded-stacked-columns-20261023/oracle-manifest.json`，结果固化在[测试向量](../tests/Bing.EasyPrint.Tests/Fixtures/ExpandedStackedCompositeColumnVectors.cs)。运行测试无需安装 Zint。
- 最终命令、框架、数量和首次失败说明见[阶段验收报告](expanded-stacked-columns-verification-2026-10-23.md)。本阶段不发布包，不执行实机验收。
