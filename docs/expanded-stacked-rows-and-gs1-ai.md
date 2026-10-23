# ExpandedStacked 最大数据行数与完整 GS1 AI 字典

本页记录 2026-10-23 增量能力的公共契约、固定数据来源和验证边界。既有列数配置、默认两列输出及全部旧签名保持不变。

## ExpandedStacked 最大数据行数

普通 ESC/POS Raster `Type=4` / `BarcodeType=31` 与 Composite `Type=11` / `LinearType=31` 新增可选的 `ExpandedStackedRows` / `WithExpandedStackedRows` 和模板同名字段。

- 合法范围是 **2–11 条数据行**。上限只统计数据行，三条行间分隔行不计入；物理行数仍是 `4 × 数据行数 − 3`。
- `ExpandedStackedRows` 与 `ExpandedStackedColumns` **互斥**。未配置两者时沿用默认两列；同时配置会得到 `InvalidOperationException`，模板 JSON、Host 请求和设计器会给出明确错误，不会静默选取其中一个。
- 最大行数是上限而不是目标。编码器先按默认两列判断；只有两列无法满足上限时才增加列数，且不会降到两列以下。计算出的列数超过 11 或实际数据行仍超过上限时，编码前失败，不截断数据、不丢弃字段。
- 普通条码只影响 ESC/POS Raster；Composite 影响 CPCL、ESC/POS Raster、TSPL、ZPL 的图片输出。尺寸、旋转、静区、预检、模板布局、Host 预览和 Provider 输出共用实际编码结果。

### C# 入口

```csharp
var ordinary = new PrintBarcodeElement(0, 0, BarcodeType.Gs1DataBarExpandedStacked,
    "[91]1234567890123456789012345678901234", 35);
var rows = ordinary.WithExpandedStackedRows(2); // 返回副本，original 不持有行数

var size = EscPosRasterCanvas.MeasureExpandedStackedSizeByRows(
    "[91]1234567890123456789012345678901234", 35, 2, 2);
var canvas = new EscPosRasterCanvas(size.Width, size.Height);
canvas.DrawExpandedStackedBarcodeByRows(0, 0,
    "[91]1234567890123456789012345678901234", 35, 2);

var composite = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarExpandedStacked,
    "[91]1234567890123456789012345678901234", "[20]12", 35, 2,
    Gs1CompositeMode.CcA).WithExpandedStackedRows(2);
```

直接命令入口为 `EscPosPrintCommand.RasterExpandedStackedBarcodeByRows`，要求先开始 Raster 页。

### 模板、Host 和设计器

```json
{
  "Type": 11,
  "LinearType": 31,
  "ExpandedStackedRows": 2,
  "Mode": 1,
  "ModuleWidth": 2,
  "LinearBarHeight": 35,
  "Field": { "Value": "[91]1234567890123456789012345678901234" },
  "SecondaryField": { "Value": "[20]12" }
}
```

普通 `Type=4` 使用同名 `ExpandedStackedRows`、`BarcodeType=31` 和 `Height`。只接受整数 `2..11`；小数、字符串、布尔、对象、范围外值和与 `ExpandedStackedColumns` 并存都会失败。大小写别名重复声明会明确报错。

Host 新增 `gs1-databar-expanded-stacked-rows` 特性。普通 Raster 的顶层 `expandedStackedRows` 和 Composite 的 `gs1Composite.expandedStackedRows` 返回 `{minimum:2, maximum:11, default:null}`；不支持的协议/模式返回 `null`。旧 Host 未声明该特性时仍可使用缺省两列布局，显式行数会保留在项目中但阻止预览/输出，并显示缺失能力原因。

## 完整 GS1 AI 字典

### 固定版本与来源

- 字典：GS1 Barcode Syntax Dictionary，提交 `d1702ffb2fa5060f8460f7d581b01e377deaa1d6`（2025-02-09）。
- 来源：<https://github.com/gs1/gs1-syntax-dictionary>，文件 `gs1-syntax-dictionary.txt`。
- 许可：Apache License 2.0；完整声明见 `src/Bing.EasyPrint/ThirdPartyNotices.txt`。
- 该提交与 Zint 2.15.0（`4896136bdbebb1a0908a5cc419513efa5bf4bf70`）内置的 `gs1_lint.h` 生成源一致。
- 固定文件以嵌入资源发布；SHA-256 为 `8d01d6596c77dbbfbea25939b2ff4558854d36a4d528dc5195f692a4539e8921`。
- 当前版本展开范围后共 **536 个已定义 AI**，包括 2/3/4 位 AI 和 `3100–3165`、`3200–3295`、`3300–3695`、`3900–3955`、`4300–4333`、`7001–7041`、`710–716`、`7230–7259`、`8001–8030`、`8110–8112`、`8200`、`91–99` 等参数化族。

### 校验范围

括号 AI 输入现在按固定版本字典逐字段解析：

- 固定/变长、总长度与各组件长度；
- `N` 数字、`X` CSET 82、`Y` CSET 39、`Z` base64url 字符集；
- 固定长度 AI 不需要 FNC1；可变长度 AI 在后续元素前插入 FNC1（与既有规范化字节兼容）；
- `csum`、`csumalpha`、`key`、`keyoff1`、`yymmd0`、`yymmdd`、`yyyymmd0`、`yyyymmdd`、`hh`、`mi`、`ss`、`hhmi`、`iso3166`、`iso3166999`、`iso3166alpha2`、`iso4217`、`iso5218`、`yesno`、`nonzero`、`zero`、`winding`、`pieceoftotal`、`posinseqslash`、`hyphen`、`mediatype`、`importeridx`、`latitude`、`longitude`、`pcenc`、`iban`、`nozeroprefix`、`hasnondigit`、`couponcode`、`couponposoffer`、`packagetype` 等字典 Linter；
- 未知 AI、越界长度、非法字符、错误校验位和不完整字段在输出前失败，错误信息包含 AI 与具体字段原因。

仍然属于调用方或具体码制的边界：

- 单个历史括号展示值继续沿用既有单元素协议兼容路径；包含至少两个 AI、扩展 AI（253/254/255）或直接调用 `Normalize` / `NormalizeForProtocol` 时使用完整字典严格校验。该兼容路径不改变旧固件输入，也不声称对单元素裸括号值做完整字典复核。

- 字典中的 `req` / `ex` 跨 AI 关联属性被保留用于追溯，但不做全量业务配对推断；项目只保留既有明确规则（如 4300–4326 与 AI 00、254 与 414、地址第二行与第一行）。这不是 AI 语法缺口。
- Expanded 的 77 字符上限、DataBar Expanded 的 22 符号字符容量、Composite 的 CC-A/B/C 容量和物理尺寸仍由对应编码器校验。
- 本库不实现 GS1 Digital Link URI、EPCIS、行业业务规则或具体设备的扫描语义；原始已规范化输入入口继续按既有规则工作，不经括号字典重新猜测边界。

### 更新方法

1. 取得目标 GS1 Syntax Dictionary 提交的 `gs1-syntax-dictionary.txt`。
2. 替换 `src/Bing.EasyPrint/Protocols/Data/gs1-syntax-dictionary-<commit>.txt`，更新 `Gs1SyntaxDictionary.DictionaryCommit`、`DictionarySha256` 和资源后缀。
3. 如果新版本引入新 Linter，必须在 `Gs1SyntaxDictionary.KnownLinters` 与 `Gs1SyntaxAiDefinition.ValidateLinter` 中实现；加载时会拒绝未实现 Linter，不会静默接受。
4. 运行 `Gs1SyntaxDictionaryTests`、全部核心测试和五框架 XML 门禁。

### 案例

- [C# Composite 与普通行数案例](../samples/Templates/Gs1CompositeSamples.cs)
- [普通最大行数 JSON](../samples/Templates/expanded-stacked-rows.json)
- [Composite 最大行数 JSON](../samples/Templates/gs1-composite-expanded-stacked-rows.json)
- 运行：`dotnet run --project samples/Templates/Templates.csproj -c Release -- artifacts/expanded-stacked-rows-20261023/samples`
- 字典覆盖/正反例：`tests/Bing.EasyPrint.Tests/Gs1SyntaxDictionaryTests.cs`
- 行数全矩阵：`tests/Bing.EasyPrint.Tests/ExpandedStackedRowsTests.cs` 与 `Fixtures/ExpandedStackedRowVectors.cs`
