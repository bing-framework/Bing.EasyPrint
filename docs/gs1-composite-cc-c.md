# GS1-128 + CC-C Composite

本页说明当前 GS1 Composite 中 GS1-128 线性组件与 CC-C 二维组件的使用方式。Composite 是一个不可拆分的符号；CC-C 输出仅在目标 Provider 能提供坐标和图片能力时可用，ESC/POS Native 不支持。

当前验证范围与结果见[2026-10-22 阶段报告](gs1-composite-cc-c-verification-2026-10-22.md)。

## 支持组合

| 线性组件 | Auto | CC-A | CC-B | CC-C |
|---|---:|---:|---:|---:|
| GS1 DataBar Omnidirectional | 是 | 是 | 是 | 否 |
| GS1 DataBar Limited | 是 | 是 | 是 | 否 |
| 单行 DataBar Expanded | A→B | 支持 | 支持 | 不支持 |
| DataBar Stacked | A→B | 支持 | 支持 | 不支持 |
| GS1-128 (`UccEan128`) | 是 | 是 | 是 | 是 |

GS1-128 的 `Auto` 直接选择 CC-C。DataBar 的 `Auto` 保持原策略：先尝试 CC-A，容量不足时选择 CC-B。DataBar+CC-C 和 EAN/UPC+CC-C 仍是非法组合；构造、模板导入验证、预览和渲染都会拒绝。GS1-128 显式 CC-A/CC-B 需要新 Host 特性 `gs1-composite-gs1-128-ab`，详见[GS1-128 A/B 使用说明](gs1-composite-gs1-128-ab.md)。单行 Expanded 和 DataBar Stacked 均支持 A/B，分别见[Expanded 使用说明](gs1-composite-expanded.md)和[Stacked 使用说明](gs1-composite-stacked.md)。ExpandedStacked 和 EAN/UPC Composite（包括 2/5 位附加码）已分别接入，见[ExpandedStacked 指南](gs1-composite-expanded-stacked.md)、[EAN/UPC 指南](gs1-composite-ean-upc.md)及[附加码指南](gs1-composite-ean-upc-addons.md)。

## 数据和尺寸

GS1-128 主数据必须使用括号式应用标识，例如 `(01)12345678901231`。共享解析器会规范化括号 AI 输入并要求规范化结果不超过 48 个字符；它不验证所有 AI 之间的业务关系，也不保证 GTIN 校验位正确。调用方仍应按业务要求验证完整性。次数据使用共享解析器固定版本完整 GS1 Syntax Dictionary（提交 d1702ffb2fa5060f8460f7d581b01e377deaa1d6）的完整 AI 规则；跨 AI 业务关联仍由业务层校验。

`ModuleWidth`、`LinearBarHeight`、坐标和静区均以打印点为单位。CC-C 行高为 `3 × ModuleWidth`，分隔行为 `ModuleWidth`；完整符号尺寸由编码结果确定。Composite 不内置 HRI，需要可读文本时另加文字元素。静区和旋转作用于完整符号。

## C# 模型

以下示例生成 CPCL 页数据；页宽 600 点、页高 300 点，模块宽度为 2 点：

```csharp
using System;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;

var document = new PrintDocument();
document.AddPage(600, 300).Add(new PrintGs1CompositeElement(
    x: 20,
    y: 20,
    linearType: BarcodeType.UccEan128,
    primaryData: "(01)12345678901231",
    secondaryData: "(17)251231(10)LOT123",
    linearBarHeight: 40,
    moduleWidth: 2,
    mode: Gs1CompositeMode.CcC));

var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);
byte[] pageBytes = provider.Render(document)[0];
Console.WriteLine(pageBytes.Length);
```

`mode` 可省略为 `Auto`；在线性类型为 `UccEan128` 时，`Auto` 与显式 `CcC` 使用相同模式。

## 模板 JSON

以下 V3 模板使用常量主次字段；也可将 `Field` 和 `SecondaryField` 的 `Value` 换为业务数据绑定的 `Name`：

```json
{
  "Version": 3,
  "Width": 600,
  "Height": 300,
  "Rows": [
    {
      "Cells": [
        {
          "Elements": [
            {
              "Type": 11,
              "LinearType": 11,
              "Mode": 3,
              "ModuleWidth": 2,
              "LinearBarHeight": 40,
              "StartCoordinate": { "X": 20, "Y": 20 },
              "Field": { "Value": "(01)12345678901231" },
              "SecondaryField": { "Value": "(17)251231(10)LOT123" }
            }
          ]
        }
      ]
    }
  ]
}
```

`Type: 11` 是 GS1 Composite；`LinearType: 11` 是既有 `UccEan128` 条码枚举值；`Mode: 3` 是 `Gs1CompositeMode.CcC`。模板模型和 JSON 属性不因 CC-C 增加新字段。

仓库还提供了可执行的 [C# 模板构造案例](../samples/Templates/Gs1CompositeSamples.cs) 和 [V3 JSON 案例](../samples/Templates/gs1-composite-cc-c.json)。运行 `dotnet run --project samples/Templates/Templates.csproj -- artifacts/template-samples` 会对 CPCL、ESC/POS Raster、TSPL 和 ZPL 比较 C# 构造、JSON 加载和保存/重载的逐页输出；输出不一致时示例失败。

## 设计器和旧 Host

新 Host 在保留 `gs1-composite` 的同时声明 `gs1-composite-cc-c`，能力数组提供 `UccEan128` 和 `CcC`。旧 Host 若只声明 `gs1-composite`，既有 DataBar Composite 继续可用，但设计器会禁用 GS1-128/CC-C 选项。包含该组合的 preview/render 请求要求新特性；旧 Host 会返回明确的功能缺失错误，不会静默替换编码模式。显式 CC-A/CC-B 另外要求 `gs1-composite-gs1-128-ab` 和 `CcA`/`CcB` 模式能力，旧 Host 缺少该特性时保留已声明的 Auto/CC-C 组合，不提供 A/B。

设计器属性面板切换到 GS1-128 时，Auto 仍选择 CC-C；支持新特性的 Host 也可显式选择 CC-A/CC-B。主数据可填入括号式 `(01)12345678901231`。导入或修改为不支持的线性类型和模式组合时会报错。项目保存和重新导入保留既有 Type 11 字段。

设计器画布在新 Host 下按元素显示真实码图 PNG；旧 Host 只显示布局占位。在线预览尺寸来自 Host 编码。真实打印机的模块宽度、行高、纸张适配、打印质量和扫码结果仍需目标机型验收。
