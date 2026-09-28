# CPCL 模板渲染

`Bing.EasyPrint.Templates` 将模板和业务数据转换为逐页 CPCL 字节。它不连接打印机，也不读取模板指定的文件或 URL。目标框架保持 netstandard2.0/2.1、.NET Core 3.1、.NET 6/7。

## 调用

```csharp
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var loader = new TemplateLoader();
var template = loader.LoadJson(json);
var result = new TemplateRenderer().Render(template, order,
    resources: resourceProvider,
    options: new TemplateRenderOptions(),
    cancellationToken: cancellationToken);
foreach (var page in result.Pages)
{
    // 将 page.Data 交给应用自身的打印传输层。
}
```

公开类型位于 `Bing.EasyPrint.Templates`；原有模型仍位于 `Bing.EasyPrint.Model` 和 `Bing.EasyPrint.Elements`。`SaveJson(template)` 保留传入模板的 V1 或 V2 版本。默认使用 GBK；也可显式设置 `TemplateRenderOptions.Encoding`，编码必须与打印机配置一致。

运行 `dotnet run --project samples/Templates -- artifacts/template-samples` 可生成固定标签、分页小票和价签的 CPCL 文件。PM0025、PM0034 的可编辑新版模板也分别保存在 `samples/Templates/price-label-pm0025.json` 和 `samples/Templates/price-label-pm0034.json`；示例程序会直接加载这两份文件，并核对与 C# 构造的逐页 CPCL 字节一致。两者按新模型独立重制，参考 ERP 样例的纸张尺寸、字段和二维码位置（PM0025 旧样例不含二维码），不会自动导入 ERP 旧 JSON。程序会检查 PM0025 的常规/促销分支，以及 PM0034 的常规、促销、会员和促销+会员分支。示例不会连接或打印到设备。

## 价签组合

`PriceLabelTemplateFactory.Create(PriceLabelPreset.Mm15|Mm70|Mm90)` 返回普通 `BpTemplate`，可以继续编辑或使用 `TemplateLoader.SaveJson` 保存。`PriceLabelRenderer.PrepareData` 将价签业务输入转换为通用字段对象；`PriceLabelRenderer.Render` 直接输出逐页字节，不连接打印机。

```csharp
var template = PriceLabelTemplateFactory.Create(PriceLabelPreset.Mm90);
var data = new PriceLabelData
{
    Name = "示例商品", Barcode = "6901234567890", QrCode = "https://example.test/p/1",
    OriginPrice = 300m, SalePrice = 250m, MemberPrice = 200.03m,
    Unit = "袋", Specification = "250ml*16", Origin = "杭州"
};
var result = new PriceLabelRenderer().Render(template, data);
var json = new TemplateLoader().SaveJson(template);
```

价签金额按固定两位小数、远离零方向舍入；负价被拒绝。原价和售价不相等时显示促销分支，会员价大于零时显示会员分支；促销日期只在开始与结束时间都存在时显示。空白二维码和条码不输出。15/70/90mm 预设分别使用 159x159、540x300、671x320 打印点；70/90mm 会员价位于二维码下方的独立区域，不与底部条码相交，监管电话及可读商品条码排在底部一维条码上方。程序样例会输出 `price-label-pm0025.json`、`price-label-pm0034.json` 及对应 CPCL：PM0025 为 671x360 点并保留 ERP 原商品全名、规格、产地、监管电话及促销日期字段；PM0034 为 600x320 点，使用 ERP 三全商品、条码、规格、产地和 300/300、300.02/250.01、会员价 200.03 的实际测试输入，展示旧模板二维码锚点、原价划线、售价和可选会员价。PM0034 样例标题使用 24 点字，使完整商品名可在固定标题区换行容纳；两者是明确重制的 C# 模板，不能把旧格式 JSON 直接传给 `TemplateLoader`。

文字元素的 `TextAlignment` 可设为 `Left`、`Center`、`Right`，默认仍为左对齐，换行后的每一行在元素宽度内分别对齐。线段使用 JSON `Type: 5`，从 `StartCoordinate` 绘制到 `LineEndCoordinate`；两个端点不可重合，方向不限，线宽范围为 1–16 点。两类元素都会参与区域和分页检查。

## 模板和布局

- `Version` 为 1 或 2。V1 保持原有 JSON 行为；V2 增加变量、页面分区与通用组合元素。未知类型、未知属性、重复键和类型元数据会被拒绝。
- 所有尺寸为打印点，指令使用 200 DPI。`Width` 必须为正；固定页 `Height` 必须为正。`AutoHeight=true` 时按内容缩短页高，`Height` 为分页上限；为零则使用 `MaxPageHeight`（默认 4096）。
- 行从上到下排列；`Height` 为最小高度。单元格从左到右排列，正宽度固定，零宽度均分剩余空间。内容不能横向越界。
- 单元格先排列嵌套 `Rows`，再排列 `Elements`。元素未指定 `StartCoordinate` 时向下排列；指定时以单元格左上角为原点。`Margin` 加入间距；`EndCoordinate` 可约束元素矩形。
- `IsHeader` 只用于顶层连续起始行，每页重复；不支持表头自身的 `RepeatSource`。表头必须给正文留下空间。
- 文字按行跨页；同一行的所有单元格共用切分线。图片、条码和二维码不可切分，放不下时移到下一页；单个元素或互相重叠的元素组超过页高则报错。空白高度也计入分页。
- 边框样式 0 无、1 实线、2 虚线。分页后的片段重新闭合边框。打印为单色，背景仅支持白色；不支持的彩色配置明确报错。
- C# 使用 `Elements`。JSON 的旧 `ElementAlls` 在 `Elements` 为空时转换；有 `Elements` 时以后者为准。加载后清除旧字段，避免重复输出。

### V2 页面分区与组件

V2 顶层行可指定 `Section`：`PageHeader` 在每页顶部，`DocumentHeader` 只在首页页眉之后，`Body` 按数据流分页，`DocumentFooter` 始终在正文之后，`PageFooter` 在每页底部。`TableHeader` 只与紧随其后的 `Body` 重复行配对，在该表格明细跨页时重绘，并保证表头与首条明细位于同页；空明细不输出表头，后续普通正文页也不重复该表头。页脚高度从正文可用高度中扣除；动态页高包含页脚。V1 的 `IsHeader` 保持原语义。页眉、页脚和表格表头不能重复数据源。

```json
{
  "Version": 2,
  "Width": 320,
  "Height": 180,
  "AutoHeight": true,
  "Variables": { "shop": "Store.Name" },
  "Rows": [
    { "Section": "PageHeader", "Cells": [{ "Elements": [{ "Type": 1, "FontSize": 24, "Field": { "Name": "$var.shop" } }] }] },
    { "Section": "Body", "RepeatSource": "Items", "Cells": [{ "Elements": [{ "Type": 1, "FontSize": 24, "Field": { "Name": "Name" } }] }] },
    { "Section": "PageFooter", "Cells": [{ "Elements": [{ "Type": 1, "FontSize": 16, "Field": { "Value": "感谢惠顾" } }] }] }
  ]
}
```

`Variables` 将名称映射为根数据字段路径，使用 `$var.shop` 读取；变量也可出现在条件与重复源中。缺失路径报错，不执行表达式。`Components` 保存 `BpComponentDefinition` 列表，包含唯一 `Id`、打印点宽高和相对坐标的 `Elements`。`Type: 6` 组合可内联 `Elements` 或通过 `ComponentId` 引用定义；组合整体跨页移动，子元素可编辑。组件引用循环、无效尺寸及越界内容都会失败。

价签是业务组件示例，不在设计器通用组件库中。ERP 可将 `PriceLabelTemplateFactory.Create(...)` 产生的单元格元素保存为自己的 `BpComponentDefinition`，将 `PriceLabelRenderer.PrepareData(...)` 的结果作为渲染数据，并自行决定模板存储、权限及选用规则。PM0025/PM0034 仍是业务样例，不会被隐式转换成通用内置组件。

`TemplateTableFactory.Create(repeatSource, columns, ...)` 把表格转为可编辑行：启用重复表头时生成 `TableHeader`，明细行使用 `RepeatSource`。设计器中的“动态表格”使用同样的行/列结构，可配置列标题、字段、宽度和对齐；“文字转表格”解析 CSV/TSV 后生成静态表格行。表格的单元格边框、字段和行高仍可分别调整。

## 字段与条件

`BgField` 的取值优先级为 `ValueFunc`、`Name` 字段路径、`Value` 静态值。委托仅允许在可信 C# 代码中设置，包含委托的模板不可序列化。委托和属性 getter 的执行时间由应用负责。

字段支持公开实例属性、字典、嵌套路径及 Newtonsoft `JObject` 数据。字段名区分大小写，不支持索引器或方法调用。不存在的字段报错；已存在的 null 字段显示为空。数值格式使用不随系统区域变化的格式。

`RepeatSource` 引用可枚举集合；在重复行中，普通字段路径和 `$item` 指向当前项，`$root` 指向根数据，`$index` 是从零开始的当前序号。序号包含被条件隐藏的项。集合为 null 时不输出明细；每个枚举项都会计入行数限制。

行和元素均可设置 `Condition`：

```json
{
  "Operator": "and",
  "Conditions": [
    { "Path": "Quantity", "Operator": "gt", "Value": 0 },
    { "Path": "Name", "Operator": "notNull" }
  ]
}
```

支持 `eq/ne/gt/ge/lt/le/isNull/notNull/and/or`。字符串按序比较，数值之间可比较，不将字符串隐式转换为数字。模板不支持 C#、脚本、反射类型声明或算术表达式。

## 元素

V2 另支持 `Type: 6` 组合、`7` 矩形、`8` 椭圆轮廓、`9` 单色图标、`10` 二维点阵码。椭圆以 CPCL 线段近似；图标仅有 `check/cross/info` 三种，不加载外部路径。`Type: 10` 的 `Format` 为 `DataMatrix` 或 `PDF417`，使用 ZXing.Net 编码为黑白点阵再调用现有图片指令；码图尺寸须足以容纳完整符号，不会压缩条纹。`Width/Height`、单图像素与输出字节均受现有选项限制。

文字默认字号 24，支持库中 `FontSize` 的全部内置字号，以及粗体、下划线。`FontName=0` 自动选择本库使用的 24/55 点阵字库：24 的基础半角/全角字距为 12/24 点、高 24 点；55 为 8/16 点、高 16 点。布局与输出使用相同整数缩放倍率；不支持的字号、字体编号或不匹配组合明确报错。

代码页编码按字符实际的单/双字节选择半角/全角字距；UTF 编码区分拉丁字符、半角片假名、CJK、全角形式及补充平面宽字符，组合附加符不独占字距。换行保留文本元素及代理对。此度量针对上述固定点阵字库，不负责下载字体或为打印机补充缺失字形；设备须配置与输出编码匹配的字库。`NeedFeedLine` 控制自动换行，显式 CR/LF 转换为布局换行。`Fit` 优先选择能单行容纳的较小内置字号，随后按换行设置处理。斜体等未实现样式报错。

图片的字段值是资源键。实现 `ITemplateResourceProvider.OpenRead` 提供流；返回流归渲染器释放。宽高均为零时使用原尺寸，只给一个尺寸时按比例缩放。同一布局块可复用资源，正文处理后释放缓存；多帧 GIF/TIFF 仅解码首帧。`IsCompress=true` 使用 CG 二进制图形，默认使用 EG 十六进制图形；必须直接传输页字节，不能通过字符串往返转换二进制数据。

二维码使用 Model 2，默认模块宽 4、纠错 M。按 [DENSO WAVE 容量表](https://www.qrcode.com/en/about/version.html) 计算全部 1–40 版本及 L/M/Q/H 纠错等级，包含模式位、随版本变化的长度位及四边静区。纯数字、纯字母数字分别计算压缩容量；其他内容按实际输出编码的字节数预留，打印机自动分段编码时可能使用更小的区域。空内容和超过版本 40 的数据报错。`Width/Height` 可增加预留区域，不能缩小到计算值以下。

一维条码默认 Code128、`LineWidth=2`、`Ratio=2`，须设置正的 `Height`。参数遵循 [Zebra CPCL Link-OS 手册 6.2.1](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/cpcl-link-os-pg-en.pdf)：实际窄条点宽为 `LineWidth + 1`；`Ratio=0..4` 对应 1.5、2、2.5、3、3.5，`20..30` 对应 2.0 至 3.0。POSTNET 的 Ratio 为条间距参数，实际间距加一。非整数宽条向上取整预留。各制式分别计算符号、起止符、校验符和静区，不能用 Ratio 直接乘整个条码宽度。

| 制式 | 模板输入及尺寸规则 |
|---|---|
| EAN13 / EAN8 / UPCA | 固定模块数；接受完整号码或缺一位校验码的号码，补齐或校验 mod-10。EAN8 也接受 6 位并补前导 0 |
| UPCE | 6 位数据或 7 位含号码系统的数据，号码系统为 0/1；6 位补前导 0 |
| Code39 | 基本字符集大写字母、数字及 `-. $/+%`，起止符由打印机添加 |
| Code93 | 可打印 ASCII，扩展字符按两个符号，计入两个校验符 |
| Code128 | 可打印 ASCII；纯偶数数字按子集 C，混合内容按全 B 的安全上界，不依赖固件的自动切换策略 |
| UCCEAN128 | 以数字应用标识开头的 GS1 元素串，计入 FNC1；不接受带括号的展示文本。应用标识、业务字段长度及 GS1 业务校验由调用方负责 |
| Codabar | A/B/C/D 起止符，内部为数字及 `-$:/.+` |
| I2OF5 / I2OF5C | 数字；必要时补前导 0，使包含校验位后的长度为偶数；C 由打印机添加校验位 |
| I2OF5G | 11–14 位数字；按完整 12/14 位符号预留，指令使用字母 O 的 `I2OF5G` |
| MSI | 1–13 位数字，无额外校验位 |
| POSTNET / FIM | POSTNET 为 5/9/11 位数字，可用连字符分组，计入自动校验位；FIM 仅 A/B/C |

数据规范化发生在本次渲染内部，不修改模板字段。`Width` 可扩大预留空间；不足时明确失败。模板结构化入口不支持 Code128 二进制功能字符替代码；此类高级指令可由可信的基础指令入口生成。

## 资源与失败语义

| 选项 | 默认值 |
|---|---:|
| MaxJsonBytes | 1 MiB |
| MaxDepth | 16 |
| MaxDetailRows | 10,000 |
| MaxPages | 100 |
| MaxOutputBytes | 32 MiB |
| MaxImageBytes | 10 MiB / 单图 |
| MaxImagePixels | 16,000,000 / 单图 |
| MaxPageHeight | 4096 点 |

这些值可由应用调整。字段、图片打印数据及缓存也按输出预算进行提前检查；页数和最终字节数在生成时检查。限额是拒绝边界，不是吞吐或峰值内存保证。渲染器在遍历、分页和资源流读取之间检查取消；同步解码和应用 getter 无法在执行中强制中断。

无效模板或资源超限抛出 `ArgumentException`（或其派生类型）；格式错误可抛出 `JsonException`；取消抛出 `OperationCanceledException`，图像解码和资源提供器错误保留原异常。设计器可使用 `LoadJsonWithPaths` 和 `RenderWithLayout`，在可定位的结构或布局错误中取得 `TemplateRenderException.SourcePath`；原 `LoadJson`/`Render` 接口保持原有调用方式。失败不返回部分页。调用方输入不被修改；同一渲染器可供独立并发调用，但调用期间不得修改模板、数据或选项。

## 基础指令与验证

结构化文本、条码和二维码入口拒绝协议控制字符，避免外部输入插入命令。`WriteRaw`、`WriteRawLine` 以及明确的 Raw 方法仍只适用于可信打印指令，不应直接接收外部输入。

CPCL 文字、条码和二维码组件在加入命令时校验文本及协议标记，并固定这些内容；重复 `Build()` 直接输出已校验的组件。分割线字符与当前位置相对于页宽的检查仍在 `Build()` 执行，因此构建前修改页宽仍可能使构建失败。

重复 `Build()` 不再追加之前的结果。图像转换不修改调用方图片，图像限制还可通过基础库的 `ImageProcessingOptions` 配置。

自动回归：`dotnet test tests/Bing.EasyPrint.Tests -f net6.0`。真实打印仅在 `EASYPRINT_ENABLE_HARDWARE_PRINT=1` 时启用，且必须用 `EASYPRINT_PRINTER_NAME` 指定队列；默认自动测试不会提交打印作业。性能工具位于 `tools/PerformanceProbe`，对固定文字和图片输出检查确定性并报告耗时、进程分配量；使用 `IncludeTemplates=true` 增加新引擎场景。

`DefineFormat` 的格式正文也是可信原始指令入口。EG/VEG 数据必须为长度匹配的十六进制字符串；旧 CG/VCG 字符串接口要求编码后的字节数与声明尺寸一致，新代码优先使用 `DrawCompressedImage`。
