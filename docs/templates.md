# 多协议模板渲染

> 文档定位：本文件是模板版本、元素和兼容规则的详细参考。快速接入请先阅读[模板渲染指南](templates-guide.md)；当前模块关系见[架构与模块](architecture.md)。带 `Rxx` 的段落保留历史实现脉络。

> 当前范围：模板条码会通过共享 GS1 解析器使用协议文档 R152 已声明的 AI `253`、`254`、`255` 规则；四协议 `WriteRestrictedRaw` 属于直接命令层，不是模板 JSON 能力。协议新增轮次未必需要修改模板格式，因此本文件的最高 `Rxx` 不表示整个项目的最新轮次。

### R140：ESC/POS Raster GS1 DataBar Stacked

ESC/POS Raster 模板新增 `BarcodeType.Gs1DataBarStacked`，复用 13 位 ASCII 数字 payload 的 DataBar Omnidirectional 编码，生成 50X 宽的两行符号及分隔行；总条高至少为 13X，顶部、分隔和底部行按 5:1:7 输出。HRI 显示隐含 AI `(01)` 和 GTIN 校验位。Native、TSPL、ZPL 和 CPCL 不声明该类型能力，仍会严格拒绝。结构尺寸依据 [GS1 General Specifications](https://ref.gs1.org/standards/genspecs/12.0.0/)，分行和分隔模式参考 [Zint RSS encoder](https://github.com/zint/zint/blob/master/backend/rss.c)；具体设备的模块形状和扫描质量仍需实机验收。

### R138：按旋转角度校准 TSPL/ZPL 原生 HRI 边界

TSPL/ZPL 模板 Profile 可为条码旋转 90、180、270 度声明独立 HRI 页面四边外伸。每边使用页面坐标点数；角度配置中为 `null` 的边继承通用 Profile 值，显式 `0` 覆盖为零。该配置只参与模板预检，不移动字段或改变输出字节；真实机型 HRI 轮廓仍需手册或打印样本校准。

### R125：模板中的 GS1 金额/折扣 AI 子集

ESC/POS Native、TSPL、ZPL 模板输出现在接受原始 GS1 AI/FNC1 数据中的 `390n`、`391n`、`394n`、`395n`：分别校验 1–15 位金额、3 位数字货币码加 1–15 位金额、固定 4 位和固定 6 位数字。TSPL/ZPL 的宽度预检与 Provider 输出保留 `390n`/`391n` 后的 FNC1；Raster DataBar Expanded 中 GTIN 后的这些字段使用通用 Expanded 字段编码。`BpTemplate` 条码字段使用原始 AI/FNC1 格式，括号展示输入由直接协议命令解析器处理。

模板层只处理字段结构，不验证货币业务含义、小数换算、折扣范围或 AI 关联规则；真实固件与打印效果仍需设备验收。

### R124：ESC/POS Raster 模板 Code 49 完整外框

Code 49 Raster 模板现按完整符号外框布局：条码元素坐标是外框左上角，宽度包含 10X 左静区、70X 编码行和 1X 右静区，高度包含顶部、行间和底部 1X 分隔条。每行条高至少为 8X；模板预检、旋转、HRI（包括旋转后的预留范围）和显式静区占位与直接 Raster 输出共用该几何，内置左右静区会拒绝被其他元素覆盖。模板宽度估算仍保留保守上界。真实热敏/针式设备的打印与扫码质量需要目标设备验证。

### R123：ESC/POS Raster 模板 Code 49

R123 将 Code 49 编码接入 ESC/POS Raster 模板 Provider，模板宽度使用保守估算。完整外框几何与尺寸预检已在 R124 补齐，详见上文。

### R122：模板中的常用压缩 AI

统一模板的 ESC/POS Native、TSPL EAN128 和 ZPL GS1-128/DataBar Expanded 条码现在可以使用 `(310x)`/`[310x]`、`(320x)`/`[320x]`、`(3920)`–`(3923)` 和 `(3930)`–`(3933)` 子集。宽度预检和 Provider 输出共用共享解析器；变长价格字段在后续 AI 前保留 FNC1，模板失败仍带有条码元素路径。

该能力只负责跨协议输入规范化。Raster 的专用压缩 Method 3–6 编码和完整 GS1 业务规则不由本轮的原生协议 Provider 推导，未声明的压缩 AI 继续严格失败。

### R121：模板 Provider 的跨协议括号 AI 输入

模板中的 GS1-128/DataBar Expanded 多元素括号输入会沿目标 Provider 复用共享 AI 规范化：ESC/POS Native、TSPL EAN128 和 ZPL 原生条码在宽度预检与写入协议字节前统一插入变长字段的 FNC1，再按各自固件转义。单个括号展示值继续走历史原始输出路径，避免改变旧模板结果；未知 AI、混合括号或字段长度错误在对应元素路径处失败。

该轮只关闭当前常用 AI 子集的跨协议输入一致性；完整 GS1 字典、Stacked/Composite、Code 49 和设备固件行为仍由能力矩阵与外部验收覆盖。

### R120：ESC/POS Raster 模板 GTIN 加通用 AI 序列

统一模板在 ESC/POS Raster 模式下支持 `[01]GTIN[10]AB[20]12` 这类 GTIN 加常用 AI 序列。模板预检和 Provider 共用 GTIN 校验、括号样式、定长/变长字段和 FNC1 规范化；直接 Raster 与模板生成相同位流，HRI 保留结构化输入。压缩重量、价格和日期 AI 仍由既有 Expanded Method 3–14 入口处理。

未知 AI、校验位错误、字段长度错误和混用括号在元素路径处严格报错；完整 GS1 字典和业务关联规则仍未开放。

### R119：ESC/POS Raster 日期字段后的括号 AI 尾部

统一模板在日期 Method 7–14 后支持常用 GS1 AI 的圆括号/方括号尾部。模板预检和 Raster Provider 共用日期值、AI 字段长度、CSET 82、FNC1 插入和矩阵预算；`[10]AB[20]12` 与等价的原始 `10AB<FNC1>2012` 生成相同位图，HRI 保留方括号输入。

额外日期 AI、未知/压缩 AI、未闭合或混用括号和字段长度错误仍在元素路径处严格报错。该能力只覆盖已声明的常用子集，GS1 DataBar Expanded 的 AI/FNC1 规则参考 [GS1 General Specifications 24.0.0](https://ref.gs1.org/standards/genspecs/24.0.0/)。

### R118：ESC/POS Raster GS1 DataBar Expanded 括号 AI 到 FNC1 规范化

统一模板在 ESC/POS Raster 模式下支持常用 GS1 AI 的 `(AI)value` 与 `[AI]value` 输入。模板预检接受两种括号、按括号和 FNC1 转义后的字节长度计算上限；Provider 在矩阵预算前完成 AI、字段长度、数字/CSET 82 字符集和括号匹配校验，并为后续变长字段插入 FNC1。直接命令与模板对 `(10)ABC(17)240101`、`[10]ABC[17]240101` 生成相同符号，HRI 保留输入展示形式。

当前只开放 Provider 声明的常用 AI 子集；未知 AI、业务关联规则、链接标志和压缩 Method 3–14 的括号化组合仍严格报错。完整语义参考 [GS1 syntax dictionary](https://github.com/gs1/gs1-syntax-dictionary/blob/2026-01-27/gs1-syntax-dictionary.txt) 与 [GS1 General Specifications](https://ref.gs1.org/standards/genspecs/23.0.0/)。

### R117：ESC/POS Raster GS1 DataBar Expanded 日期后的通用字段

统一模板在 ESC/POS Raster 模式下现支持 Method 7/8、9–14 的日期字段后追加原始通用字段。模板预检和 Provider 共用日期压缩字段、通用字段状态机、动态符号宽度、HRI、页面边界、元素路径、预算和失败恢复；直接命令与模板使用同一规范化路径。日期后的尾部允许 ASCII 29/FNC1，但不能以 FNC1 结束或包含连续 FNC1。

本轮不把日期后的括号或方括号当作新的 AI 元数据；完整 AI 解析、跨协议括号规范化、链接标志、Stacked/Composite 和 Code 49 仍严格报错。算法参考 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)，真实固件、DPI、纸宽、HRI 字形和热敏/针式扫描质量仍需设备验收。

### R116：ESC/POS Raster GS1 DataBar Expanded Method 2 FNC1 数据

统一模板在 ESC/POS Raster 模式下现支持 Method 2 的原始 GS1 通用字段：输入以两位数字 AI 开头，使用 ASCII 29/FNC1 分隔字段，长度为 3–77 个字符。模板预检和 Provider 复用 `0000` 方法字段、通用字段状态机、动态符号宽度、HRI、页面边界、元素路径、预算和失败恢复；直接命令与模板使用同一规范化路径。

本轮只开放原始 FNC1 输入，不接受括号/方括号展示语法、末尾或连续 FNC1；完整 AI 元数据解析、链接标志、Stacked/Composite、Code 49 和跨协议 GS1 输入转换仍严格报错。规则依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 与 [libzint general_field.c](https://raw.githubusercontent.com/zint/zint/master/backend/general_field.c)。

### R115：ESC/POS Raster GS1 DataBar Expanded Method 9–14 日期字段

统一模板在 ESC/POS Raster 模式下现支持 `(310x)`/`[310x]`、`(320x)`/`[320x]` 后接 `(11)/(13)/(15)/(17)` 或方括号日期 AI 和六位日期。Provider 按日期 AI 选择 Method 7/8、9/10、11/12、13/14，编码 20 位重量和 16 位日期值；模板预检校验月 `01-12`、日 `00-31`，HRI 保留重量 AI、日期 AI 和日期文本。

本轮要求日期字段是结构化输入的最后一段，不接受日期后的通用字段或未实现日期 AI；页面边界、元素路径、能力快照、预算、直接命令和失败恢复继续复用统一 Raster 路径。算法依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)；目标固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描质量仍需设备验收。

### R114：ESC/POS Raster GS1 DataBar Expanded Method 7/8

统一模板在 ESC/POS Raster 模式下现支持没有日期后缀的 `(310x)`/`[310x]` 和 `(320x)`/`[320x]` 重量子范围。对 `000000-099999` 的 6 位重量，模板预检沿用 Method 3/4 的有效范围选择 Method 3/4，超出这些范围的输入选择 Method 7/8；Provider 将虚拟日期 `38400` 编入压缩字段，但 HRI 只显示原始 AI 和重量文本。页面边界、元素路径、能力快照、预算、直接命令和失败恢复继续复用统一 Raster 路径。

显式日期后缀、Method 9–14、其它结构化 AI、链接标志、Stacked/Composite 和 Code 49 在模板预检或 Provider 阶段严格报错。方法字段和虚拟日期依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)；目标固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描质量仍需设备验收。

### R113：ESC/POS Raster GS1 DataBar Expanded Method 5/6

统一模板在 ESC/POS Raster 模式下现支持 Expanded 的显式 `(392x)`/`[392x]` 本地价格和 `(393x)`/`[393x]` ISO 4217 货币+价格子范围，`x` 限制为 `0-3`。模板预检接受合法 `(01)`/`[01]` GTIN-14、Method 5 的非空 ASCII 数字价格，或 Method 6 的三位 ASCII 数字货币代码加非空 ASCII 数字价格；Provider 以对应压缩字段生成 `GS v 0`，并沿用统一宽度、页面边界、元素路径、能力快照、预算和失败恢复。直接命令和模板使用同一规范化/编码路径。

其它结构化 AI、Method 7–14、链接标志、Stacked/Composite 和 Code 49 在模板预检或 Provider 阶段严格报错。方法字段和货币压缩依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)；目标固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描质量仍需设备验收。

### R112：ESC/POS Raster GS1 DataBar Expanded Method 4

统一模板在 ESC/POS Raster 模式下现支持 Expanded 的显式 `(3202)`/`[3202]` 和 `(3203)`/`[3203]` 英制重量子范围。模板预检接受合法 `(01)`/`[01]` GTIN-14、对应 6 位重量和可选通用字段；`3202` 限制为 `000000-009999`，`3203` 限制为 `000000-022767`。Provider 以固定 Method 4 压缩字段生成 `GS v 0`，并沿用统一宽度、页面边界、元素路径、能力快照、预算和失败恢复。直接命令和模板使用同一规范化/编码路径。

其它 `(32xx)`、Method 5/6、Method 7–14、链接标志、Stacked/Composite 和 Code 49 在模板预检或 Provider 阶段严格报错。算法依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)；目标固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描质量仍需设备验收。

### R111：ESC/POS Raster GS1 DataBar Expanded Method 3

统一模板在 ESC/POS Raster 模式下现支持 Expanded 的显式 `(3103)`/`[3103]` 公斤重量子范围。模板预检接受 `(01)`/`[01]` 合法 GTIN-14、6 位 `000000-032767` 重量和可选通用字段；Provider 以固定 Method 3 压缩字段生成 `GS v 0`，并沿用统一宽度、页面边界、元素路径、能力快照、预算和失败恢复。直接命令和模板使用同一规范化/编码路径。

其它 `(31xx)`、Method 4/5–14、链接标志、Stacked/Composite 和 Code 49 在模板预检或 Provider 阶段严格报错。算法依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)；目标固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描质量仍需设备验收。

### R110：ESC/POS Raster GS1 DataBar Expanded 通用字段

统一模板在 ESC/POS Raster 模式下现支持 `BarcodeType.Gs1DataBarExpanded` 的 `(01)` GTIN-14 加通用字段字符集子集。模板预检接受 `(01)`、`[01]` 或裸 `01` 加 14 位合法 GTIN-14，以及数字、A-Z、a-z、`*,-./`、ISO/IEC 646 标点、空格和 ASCII 29 FNC1；Provider 使用同一 Numeric/Alphanumeric/ISO 状态机生成动态宽度 `GS v 0` 位图。HRI、页面边界、四方向旋转、元素路径、能力快照、预算和失败恢复沿既有 Raster 路径复用。

不在编码表中的字符、末尾 FNC1、其它 AI/Method、链接标志、Stacked/Composite 和 Code 49 在模板预检或 Provider 阶段严格报错。完整回归和多目标框架构建结果以本轮验证记录为准，真实固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描质量仍需设备验收。

### R109：ESC/POS Raster GS1 DataBar Expanded 数字通用字段

统一模板在 ESC/POS Raster 模式下支持 `BarcodeType.Gs1DataBarExpanded` 的 `(01)` GTIN-14 加纯数字通用字段子集。模板预检接受 `(01)`、`[01]` 或裸 `01` 加 14 位合法 GTIN-14 及数字后缀，按实际符号字符数测量线性宽度并生成 `GS v 0`；R108 的 GTIN-only 134 模块结果保持兼容。HRI、页面边界、四方向旋转、元素路径、能力快照、预算和失败恢复沿既有 Raster 路径复用。

字母/ISO 字段、ASCII 29/FNC1、其它 AI、Stacked/Composite 和 Code 49 仍在模板预检或 Provider 阶段严格报错。完整回归和多目标框架构建结果以本轮验证记录为准，真实固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描质量仍需设备验收。

### R108：ESC/POS Raster GS1 DataBar Expanded GTIN-only

统一模板在 ESC/POS Raster 模式下现支持 `BarcodeType.Gs1DataBarExpanded` 的 GTIN-only `(01)` 子集。模板输入可使用 `(01)`、`[01]` 或裸 `01` 加 14 位 GTIN-14；预检验证 ASCII 数字和 GTIN 校验位，主机生成 Method 1 Expanded 的四个数据字符、check/finder 结构和 134 模块 `GS v 0` 位图。宽度按实际 134 模块乘窄条点宽度测量，支持 HRI、四方向旋转、页面边界、模板元素路径、能力快照、资源预算和失败恢复。

`EscPosRasterGs1DataBarExpandedTests` 与 Native 能力回归在 `net6.0`、`netcoreapp3.1` 各通过 `30/30`；Core 完整回归各 `2858/2858`，Core/Templates 五目标框架构建 0 警告、0 错误。其它 AI/通用字段、Stacked/Composite、完整 Expanded 可变长度方法仍在模板能力预检阶段严格报错；编码表参考 [GS1 DataBar](https://www.gs1.org/standards/barcodes/databar)、[GS1 General Specifications](https://ref.gs1.org/standards/genspecs/11.0.0/)、[libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 和 [rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h)。真实固件、DPI、条高、纸宽、HRI 字形和热敏/针式扫描质量仍需设备验收。

### R107：ESC/POS Raster China Post（Datalogic 2 of 5）

统一模板在 ESC/POS Raster 模式下现支持 `BarcodeType.ChinaPost`。模板输入为非空 ASCII 数字，主机生成 Datalogic/China Post 的两窄条起始、宽条/窄条结束和字符间窄空白，固定 3:1 宽窄比后输出 `GS v 0` 位图；宽度按 `10n+9` 模块预检，支持四方向旋转、页面边界、元素路径、能力快照、资源预算和失败恢复。HRI 只显示调用方提供的数字，不追加可选校验位。

本轮 `EscPosRasterChinaPostTests` + `TsplChinaPostProviderTests` 在 `net6.0`、`netcoreapp3.1` 各通过 `30/30`。ESC/POS Native、TSPL、ZPL 和 CPCL 行为保持现有能力边界；TSPL CPOST 继续固定 1:1 原生输出。结构参考 [TCPDF DatalogicTwoOfFive](https://tcpdf.org/docs/srcdoc/tc-lib-barcode/classes-Com-Tecnick-Barcode-Type-Linear-DatalogicTwoOfFive/)，真实固件、DPI、宽窄比、条高、纸宽、HRI 和热敏/针式扫描质量仍需设备验收。

### R106：ESC/POS Raster GS1 DataBar Limited

统一模板在 ESC/POS Raster 模式下现支持 `BarcodeType.Gs1DataBarLimited`。模板输入为 13 位 ASCII 数字且首位为 0 或 1，主机生成固定 79 模块的 GS1 DataBar Limited 位图，使用统一模块宽度、四方向旋转、HRI `(01)`/GTIN 校验位、页面边界、元素路径、资源预算和失败恢复。14 位含校验输入、AI 前缀和复合链接字段尚未纳入跨协议输入契约。

`EscPosRasterGs1DataBarLimitedTests` 在 `net6.0`、`netcoreapp3.1` 各 `16/16` 通过；Core 完整回归各 `2829/2829`，Core/Templates 五目标框架构建 0 警告、0 错误。编码表参考 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)、[rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h)（BSD-3-Clause 归因）。Raster Expanded/Stacked/Composite、China Post 和 Code 49 仍在模板能力预检阶段严格报错，Native、TSPL、ZPL 的既有条码行为不因本轮改变；真实固件、DPI、条高、纸宽、HRI 和热敏/针式扫描质量仍需设备验收。

### R105：ESC/POS Raster GS1 DataBar Omnidirectional/Truncated

统一模板在 ESC/POS Raster 模式下现支持 `BarcodeType.Gs1DataBarOmnidirectional` 与 `BarcodeType.Gs1DataBarTruncated`。模板输入为 13 位 ASCII 数字 payload，主机按 GS1 DataBar/RSS-14 规则生成四个数据字符、finder pattern 和 Mod-79 校验，以固定 96 模块的 `GS v 0` 位图输出；开启 HRI 时显示隐含 `(01)`、13 位 payload 和 GTIN 校验位。宽度测量、模块缩放、旋转、HRI 预留、页面边界、元素路径、资源预算和失败恢复复用现有 Raster 条码路径。Omnidirectional 与 Truncated 共用横向串，模板仍分别保留 33X/13X 的最小条高语义。

本轮 `EscPosRasterGs1DataBarTests` 与 Native DataBar 能力回归在 `net6.0`、`netcoreapp3.1` 各 `30/30` 通过，Core 完整回归各 `2813/2813`；Core/Templates 五目标框架构建 0 警告、0 错误。编码表参考 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)、[rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h)（BSD-3-Clause 归因），结构尺寸参考 [GS1 General Specifications](https://ref.gs1.org/standards/genspecs/23.0.0/)。Raster Limited 已在 R106 接入，Expanded/Stacked/Composite、China Post 和 Code 49 仍在模板能力预检阶段严格报错；真实打印机固件、DPI、条高、纸宽、HRI 和热敏/针式扫描质量仍需设备验收。

当前 ESC/POS Raster 模板条码矩阵在原有 Code 11、Code 128、Code128Auto、Code 39、Code 93、EAN/UPC、Codabar、I2OF5/I2OF5C/I2OF5G、MSI、Plessey、UCC/EAN-128、POSTNET、PLANET、FIM A–E、Intelligent Mail、ITF-14、Telepen Alpha 和 Telepenn Numeric 基础上，新增 GS1 DataBar Omnidirectional、Truncated、Limited、China Post 与 Expanded `(01)` 通用字段字符集子集、Method 3 和 Method 4；Expanded 其它 AI/Method、链接标志、Stacked/Composite 和 Code 49 仍严格拒绝。

### R104：ESC/POS Raster Telepenn Numeric

统一模板在 ESC/POS Raster 模式下现支持 `BarcodeType.Telepenn`。模板输入为 1–60 位 ASCII 数字，奇数长度前补零后按两位压缩，主机生成 Telepen Numeric 的起止符和 Mod-127 校验符，以固定 16 模块/符号的 `GS v 0` 位图输出；宽度度量、旋转、页面边界、元素路径、资源预算和失败恢复沿 Telepen Alpha 的 Raster 条码路径处理。ESC/POS Native 原生 Telepenn 继续严格拒绝，统一语义不开放 `X` 扩展。

`EscPosRasterTelepennTests` 在 `net6.0`、`netcoreapp3.1` 各 `14/14` 通过；压缩规则参考 [libzint telepen.c](https://github.com/zint/zint/blob/master/backend/telepen.c)，真实打印机固件、DPI、条高、纸宽、HRI 和扫描质量仍需目标热敏/针式设备验收。

### R103：ESC/POS Raster Telepen Alpha

统一模板在 ESC/POS Raster 模式下现支持 `BarcodeType.Telepen`。模板输入为 1–30 个 7-bit ASCII 字符，主机生成 Telepen Alpha 的起止符和 Mod-127 校验符，以固定 16 模块/符号的 `GS v 0` 位图输出；宽度度量、模块缩放、HRI、旋转、页面边界、元素路径、资源预算和失败恢复沿现有 Raster 条码路径处理。`BarcodeType.Telepenn` Numeric 已在 R104 接入同一路径，ESC/POS Native 原生 Telepen 继续严格拒绝。

`EscPosRasterTelepenTests` 在 `net6.0`、`netcoreapp3.1` 各 `13/13` 通过；符号表参考 [libzint telepen.c](https://github.com/zint/zint/blob/master/backend/telepen.c)，真实打印机固件、DPI、条高、纸宽、HRI 和扫描质量仍需目标热敏/针式设备验收。

### R102：ESC/POS Native profile 矩阵

统一模板在 ESC/POS Native Rows 模式下现有设备 profile 组合回归：17 个 Function B 条码在完整 HRI allowlist 下均可输出；关闭原生条码或收窄 HRI 制式/位置 allowlist 时，Provider 在资源和字节输出前按元素路径严格失败。该证据锁定模板能力快照、profile 快照和 Native 预检的一致性，不代表所有厂商固件或业务 payload 已实机验收。

### R101：ESC/POS Raster Code 11

统一模板在 ESC/POS Raster 模式下现支持 `BarcodeType.Code11`。模板输入为 1–255 个 ASCII 数字或连字符，主机按 Code 11 规则追加 C/K 校验位并输出 `GS v 0` 位图；宽度度量、模块缩放、HRI、旋转、页面边界、元素路径、资源预算和失败恢复与其他 Raster 条码一致。该能力属于主机光栅输出，ESC/POS Native 的原生 GS k Code 11 仍在预检阶段严格拒绝；TSPL/ZPL 原生 Code 11 保持现有语义。

本轮 `EscPosRasterCode11Tests` 在 `net6.0`、`netcoreapp3.1` 各 `13/13` 通过，并覆盖直接命令、短/长 payload 校验位、非法输入、四向旋转、模板路径和能力传播。真实打印机的 Code 11 固件、HRI 字形、DPI、纸宽、打印和扫描质量仍需热敏/针式设备验收。

### R100：ESC/POS Native 数值边界证据

统一模板对应的 ESC/POS Native 条码能力已有直接命令端点回归：17 个条码类型覆盖模块宽度 2/6、条高 1/255、HRI 位置 0/1/2/3，并验证条高 0、模块宽度 1/7、HRI 位置 4 在元素输出前严格失败。该证据锁定能力快照与 GS k 参数校验的一致性，不代表静区、旋转、逐制式 payload、跨页布局或目标固件的完整笛卡尔组合。

### R99：ESC/POS Raster Plessey

统一模板在 ESC/POS Raster 模式下现支持 `BarcodeType.Plessey`。模板输入为非空 ASCII 数字，主机使用 ZXing PlesseyWriter 生成条码位序并裁剪其默认空白边界，再按模板模块宽度输出 `GS v 0` 位图；宽度度量、HRI、旋转、页面边界、元素路径、资源预算和失败恢复沿现有 Raster 条码路径处理。该能力属于主机光栅输出，不等价于 ESC/POS GS k 原生 Plessey 命令；实际打印/扫描仍需目标设备验收。

当前 ESC/POS Raster 模板条码矩阵包含 Code 11、Code 128、Code128Auto、Code 39、Code 93、EAN/UPC、Codabar、I2OF5/I2OF5C/I2OF5G、MSI、Plessey、UCC/EAN-128、POSTNET、PLANET、FIM A–E、Intelligent Mail、ITF-14、Telepen Alpha 和 Telepenn Numeric；GS1 DataBar、China Post、Code 49 等无可靠主机编码器的类型仍在模板预检阶段严格报错。

### R98：ESC/POS Raster ITF-14

统一模板在 ESC/POS Raster 模式下现支持 `BarcodeType.Itf14`。模板输入为恰好 13 位数字 payload，主机生成第 14 位 Mod10 校验位并以 ITF 位图输出；宽度度量、HRI、旋转、页面边界、元素路径、资源预算和失败恢复沿现有 Raster 条码路径处理。该路径不额外绘制 ITF-14 bearer bars，实际打印/扫描仍需目标设备验收。

Raster FIM 当前支持 `A`、`B`、`C`、`D`、`E` 五种模式；旧历史文字中的 D/E 拒绝描述不覆盖当前实现。

当前 ESC/POS Raster 模板条码矩阵包含 Code 128、Code128Auto、Code 39、Code 93、EAN/UPC、Codabar、I2OF5/I2OF5C/I2OF5G、UCC/EAN-128、MSI、POSTNET、PLANET、FIM A–E、Intelligent Mail 和 ITF-14；GS1 DataBar 及其他没有可靠主机编码器的变体仍在模板预检阶段严格报错。

### R97：ESC/POS Raster Code128 auto

统一模板在 ESC/POS Raster 模式下现支持 `BarcodeType.Code128Auto`。模板使用 1–255 个 7-bit ASCII 字符，主机按 Code 128 自动分组生成 `GS v 0` 位图；宽度度量、HRI、旋转、页面边界、资源预算和失败恢复与其他 Raster 条码一致。ESC/POS Native 的 Function B 79 原生入口仍可接收 `byte[]`，不与 Raster 字符串入口混用；空值、非 ASCII 或超长数据在元素路径处严格失败。该本地回归不代表目标热敏/针式设备对 Native Function B 79 或 Raster 条码的扫描质量已验收。

当前模板能力列表中的 Raster 条码集合应将 `Code128Auto` 与 Code 128 并列理解；后文历史集合文字只保留其对应轮次的范围，不覆盖本轮新增能力。

### R88 当前能力修正：TSPL/ZPL 机型 HRI 能力契约

TSPL/ZPL Profile 现在可通过 `supportsNativeBarcodeHumanReadable` 明确声明机型是否能输出原生条码可读文字。未填写时默认为支持以保持旧行为；关闭后，协议默认或显式开启 HRI 的模板会在 Provider 预检阶段按元素路径严格失败，显式关闭 HRI 仍可输出。该字段只表达机型级能力，不替代各条码制式的字体、位置、字高和真实设备校准。

### R87 当前能力修正：运行时矩阵与当前源码包

当前源码的 Core/Templates 包已在五个目标运行时（`net7.0`、`net6.0`、`net5.0`、`netcoreapp3.1`、`netcoreapp2.2`）通过完整 `2705/2705` 测试，并由独立包消费者验证默认 NuGet 资产的四协议工厂、模板输出、HRI、填充矩形、ZPL 延迟输出和图片旋转；`net5.0` 的显式 `netstandard2.1` 资产也确认两个输出 DLL 来自候选包。历史固定包落后于当前公开 API，不能用来判断当前模板实现。实际设备 DPI、字体、纸宽、切裁和打印扫描不由该证据推断。

### R86 当前能力修正：ESC/POS Native Sequential 旋转边界

ESC/POS Native Sequential 模板现在明确拒绝 Rotate90 条码和 Rotate90 文字，并返回对应元素路径。预检在非法元素后置图片资源打开前失败，保持 Native 的 Fail Fast 契约；需要旋转输出时应显式选择 Raster 或标签协议。该证据不代表其他协议的旋转视觉效果。

### R85 当前能力修正：ESC/POS Native Sequential 参数组合

ESC/POS Native Sequential 的 Version 2 模板现在有跨页 HRI 回归：Code39、I2OF5 与 `UCC/EAN-128` 在固定页眉、表头、重复明细和页脚中保留 `SourcePath`、实例索引、`GS H`、`GS h`、`GS w` 与对应 `GS k`。非零静区在模板资源处理前严格失败。该证据不代表所有 Native 条码组合、设备 HRI 字形或真实扫描效果。

### R84 当前能力修正：ESC/POS Native Rows 参数组合

ESC/POS Native Rows 模板现有回归覆盖 Code39、Code93、Codabar、I2OF5 与 `UCC/EAN-128` 的 HRI 组合。`HumanReadableReserveHeight` 会进入页面布局，输出保留 `GS H=2`、`GS w` 和对应 `GS k`；模块宽度从 2 调到 3 时布局宽度同步变化，低于设备范围的值在元素路径处严格失败。该证据不代表所有机型的 HRI 字形和完整模块宽度范围。

### R83 当前能力修正：ESC/POS Native Rows 条码矩阵

ESC/POS Native Rows 模板回归现覆盖 Code39、Code93、Codabar、I2OF5、I2OF5C 与 `UCC/EAN-128`，分别输出 Epson `GS k` 69、72、71、70、70、74。模板保留来源路径和布局尺寸，I2OF5C 会按现有统一校验规则将 `1234` 归一化为 `012348`；非法 Codabar 数据在模板路径处严格失败。该证据不代表所有 Native 参数组合、设备 HRI/条高或真实打印扫描结果。

### R82 当前能力修正：ESC/POS Native 扩展条码模板

ESC/POS Native Sequential 的 Version 2 模板现有回归覆盖 GS1 DataBar Omnidirectional、Truncated、Limited、Expanded 与 `CODE128 auto`，对应 Epson Function B `GS k` 75–79。跨页重复明细保留来源路径、实例索引和保守宽度，Expanded 的括号与 ASCII 29 FNC1 按设备要求转义；Native 不支持的 MSI 继续按模板元素路径严格报错。该证据不代表所有 Native 参数组合、目标固件的 HRI/条高或真实打印扫描结果。

### R81 当前能力修正：ESC/POS Native 条码参数边界

ESC/POS Native Rows 模板对 Code128、EAN13 的 HRI 预留高度会参与布局，并输出对应 `GS H`/`GS k` 指令。显式零静区保留原生输出，非零静区、旋转条码和旋转文字在资源处理前严格拒绝并报告模板路径。该证据覆盖代表性参数边界，不代表所有 Native 条码制式、设备 HRI 字形或真实扫描效果。

### R80 当前能力修正：ESC/POS Native 跨页条码

ESC/POS Native Sequential 的 Version 2 模板可以在页眉、表头、重复明细和页脚共同分页时输出 Code128、Code39、EAN13。回归按 `SourcePath` 与 `InstanceIndices` 验证条码实例，并检查 `GS k` 原生制式；Native 不支持的 MSI 会在模板元素路径处严格失败。该证据只覆盖代表性 Sequential 组合，不代表 Native Rows、全部条码制式、HRI/静区/旋转组合或真实设备固件。

### R79 当前能力修正：跨页条码复杂布局验证

Version 2 模板可以在固定页眉、表头、重复明细和页脚共同分页时携带 Code39、Code93、Codabar、I2OF5C。CPCL、TSPL、ZPL 和 ESC/POS Raster 的回归会按 `SourcePath` 与 `InstanceIndices` 验证每条明细跨页复制，并检查对应原生条码或整页 `GS v 0` 输出。该证据覆盖代表性非 Code128 条码的模板到 Provider 链路，不代表所有条码类型、ESC/POS Native 全矩阵、HRI/静区/旋转组合或真实设备扫描结果。

### R78 当前能力修正：图片长页资源边界

Version 2 模板的 `PageHeader`、`TableHeader`、重复明细和 `PageFooter` 可以共同包含图片；正文明细中的不同 `Crop` 会分别进入图片缓存键。长页渲染会复用固定分区图片，在每个正文块完成后释放非固定图片；有限 `MaxWorkingBytes` 超限时严格失败，下一次使用新会话和预算可以恢复。该回归证明资源生命周期和裁切隔离，不替代任意规模容量曲线或真实设备性能测量。

### R77 当前能力修正：图片结构化像素诊断

图片资源错误通过 `TemplateRenderException` 提供可空的源图尺寸、请求裁切区域和处理后输出尺寸。源图已识别但裁切越界时保留 `SourcePixelWidth`/`SourcePixelHeight` 与 `CropX`/`CropY`/`CropWidth`/`CropHeight`；损坏、缺失或尚未识别的资源保持尺寸为 `null`。Designer Host 会在错误 JSON 中透传这些字段，前端只在元数据完整时显示像素信息。字段描述的是模板解码和布局阶段的逻辑尺寸，不替代目标打印机 DPI、插值质量和真实设备验收。

### R75 当前能力修正：模板图片源区域裁切

`BpImageElement.Crop` 可选地声明源图片像素区域 `{ X, Y, Width, Height }`。坐标从源图左上角开始，`X/Y` 必须为非负整数，`Width/Height` 必须为正整数并完全落在源图边界内；服务端在读取源图后先执行裁切，再按模板 `Width`/`Height` 缩放。只设置一个目标尺寸时，比例依据裁切后的区域计算；省略或设置 `Crop: null` 继续使用完整源图。

裁切区域会参与图片缓存键，重复使用同一资源时不同区域不会串图。CPCL、TSPL、ZPL 和 ESC/POS Raster 共用该统一图片模型；ESC/POS Native 仍在能力预检阶段拒绝图片。设计器字段校验与源区域编辑正在同步，源区域越界不会被静默截断。真实设备的插值质量、DPI、纸宽、打印和扫描效果仍需硬件验收。

### R60 当前能力修正：ZPL GS1 DataBar

ZPL 模板现支持 GS1 DataBar Omnidirectional、Truncated、Limited 和 Expanded，输出 `^BR` 类型 1、2、5、6。统一模板支持四方向、1–10 点放大倍数和条高；Expanded 使用显式 2–22 偶数分段宽度。数字制式沿用 13 位输入与 Limited 首位 0/1 约束，Expanded 严格校验字段字符和 2–255 长度。模板能力快照、保守宽度/最小高度、页面边界、元素路径和 Profile 禁用均在 Provider 预检阶段处理。

`^BR` 标准语法没有 HRI 参数，因此 ZPL DataBar 的 `HumanReadable=true` 会明确报错；需要可读内容时追加独立文字元素，未指定值不额外改写设备行为。参数依据 Zebra [`^BR`](https://docs.zebra.com/us/en/printers/software/zpl-pg/zpl-commands/%5Ebr.html)，真实固件、GS1 AI/FNC1 业务语义、堆叠布局、DPI、纸宽、打印和扫描仍需设备验收。其他协议不因该实现而放宽能力矩阵。

### R58 当前能力修正：TSPL/ZPL 文字样式显式位图降级

模板中的 TSPL/ZPL 粗体、下划线和旋转下划线默认在能力预检阶段严格拒绝。`TsplMediaOptions.AllowRasterizedTextStyles` 或 `ZplMediaOptions.AllowRasterizedTextStyles` 显式开启后，统一模板使用主机字体生成裁剪后的单色位图，并由对应 Provider 输出 `BITMAP` 或 `^GFA`；四个旋转角度、样式墨迹和行高与模板布局保持一致。Designer 介质面板可保存该开关，但真实设备内置字体效果、图片能力和扫描结果仍需硬件验收。

### R56 当前能力修正：ESC/POS Native DataBar 与 Code128 auto

R57 字符契约修正：GS1 DataBar Expanded 官方允许的裸 `{` 原样保留；逻辑括号和 ASCII 29 FNC1 仍按 GS k 78 转义为 `{(`、`{)`、`{1`，统一模板度量与命令输出保持一致。

ESC/POS Native 模板现支持 BarcodeType.Gs1DataBarOmnidirectional、Gs1DataBarTruncated、Gs1DataBarLimited、Gs1DataBarExpanded 和 Code128Auto，输出 Epson GS k Function B 75–79。75–77 只接受 13 位数字，Limited 首位必须为 0 或 1；Expanded 接受官方字符集，并把括号及 ASCII 29 FNC1 转成 Epson 要求的转义字节；Code128 auto 的统一模板入口使用 7-bit ASCII，独立命令另提供原始字节入口。模板能力快照、元素路径、页面边界、Native 原生参数和失败恢复沿 Provider 同一路径处理。

这些条码只属于 ESC/POS Native；Raster、CPCL、TSPL、ZPL 在能力预检阶段严格报错。Provider 对 DataBar 75/76/77/78 的设备最小条高和保守横向模块数进行占用预检，避免统一布局低估输出；设备是否实现对应 Function B、HRI、DPI、纸宽和最终扫描结果仍需目标机验收。规范依据 [Epson GS k 参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_lk.html)。

### R55/R134 当前能力：ZPL Code 49

ZPL 模板现支持 `BarcodeType.Code49`，输出 Zebra 原生 `^B4`。模板限制为 1–81 个可安全嵌入 ZPL 字段的 ASCII 字符，单行目标条高为 1–32000 点、模块宽度为 1–10 点，四方向旋转使用 N/R/I/B；可读文字使用 `f=B`，起始模式默认 `A`。Provider 将目标点高向上换算为 `^B4` 所需的模块宽度倍率，宽度按本地编码器的 70 模块行宽估算，高度按本地编码行数、实际行高、分隔预留和 HRI 高度估算；Profile 还可声明原生 HRI 最小横向总宽及页面坐标四边外伸。旋转条码的 HRI 预留高度会计入旋转后的横向边界；逐角度固件 footprint 仍需单独校准。页面外伸配置只扩大主机预检边界，不移动字段或选择设备对齐。模板能力快照、页面边界、元素路径、Profile 禁用、失败恢复和 golden-byte 回归均沿同一 Provider 路径处理。参数依据 [Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)；`^`、`~`、引号和控制字符严格报错，真实固件、HRI、DPI、纸宽和打印扫描仍需目标设备验收。CPCL、ESC/POS 对该类型保持严格拒绝。

### R54 当前能力修正：ZPL Plessey

ZPL 模板现支持 `BarcodeType.Plessey`，输出 Zebra 原生 `^BP`。模板只接受非空 ASCII 数字，条高为 1–32000 点、窄条宽度为 1–10 点，四方向旋转使用 N/R/I/B；`^BY` 默认 3:1，`e=N` 不重复请求设备打印校验位。模板能力快照、保守宽度、页面边界、元素路径、Profile 禁用、失败恢复和 golden-byte 回归均沿同一 Provider 路径处理。参数依据 [Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)；真实固件、HRI、DPI、纸宽和打印扫描仍需目标设备验收。ZPL 其他未映射类型在模板预检阶段严格报错。

### R51 当前能力修正：TSPL Code 49

TSPL 模板现支持 `BarcodeType.Code49`，输出原生 `BARCODE "CODE49"`。模板限制为 1–81 个可安全嵌入 TSPL 字段的 ASCII 字符，窄宽比只允许 1:1、1:2、1:3、2:5、3:7；NUL、ESC、CR/LF、引号等 framing 冲突字符在模板预检阶段严格报错。统一宽度按数据长度、起止槽位和 49 单元/内容符号保守预留，能力快照、四方向旋转、页面边界、元素路径、Profile 禁用、Designer 选择器和失败恢复沿同一 Provider 路径处理。Code 49 堆叠行数和最终模块布局交给目标固件，不宣称本地主机宽度是任意设备的精确扫描宽度。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf)；实际固件、条高、DPI、纸宽和打印扫描仍需目标设备验收。CPCL、ESC/POS 对该类型保持严格拒绝，ZPL 已通过 `^B4` 支持。

### R50 当前能力修正：TSPL Telepen

TSPL 模板现支持 `BarcodeType.Telepen` 与 `BarcodeType.Telepenn`，输出原生 `BARCODE "TELEPEN"`/`"TELEPENN"`。模板分别限制为 1–30 个可安全嵌入 TSPL 字段的 ASCII 字符和 1–60 位 ASCII 数字，宽窄比只允许 1:1、1:2、1:3、2:5、3:7；NUL、ESC、CR/LF、引号等 framing 冲突字符在模板预检阶段严格报错。统一宽度按 16 单元/符号及起止符保守预留，能力快照、四方向旋转、页面边界、元素路径、Profile 禁用、Designer 选择器和失败恢复沿同一 Provider 路径处理。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf)；真实固件、条高、DPI、纸宽、HRI 和打印扫描仍需目标设备验收。CPCL、ESC/POS 和 ZPL 对这两个类型保持严格拒绝。

### R49 当前能力修正：TSPL China Post

TSPL 模板现支持 `BarcodeType.ChinaPost`，输出原生 `BARCODE "CPOST"`。模板只接受非空 ASCII 数字，固定使用 1:1 窄宽比；省略 `WideModuleWidth` 时由模板适配层自动使用窄条点宽，显式不等宽条在预检阶段严格报错。模板能力快照、四方向旋转、保守宽度、页面边界、元素路径、Profile 禁用、Designer 选择器和失败恢复均已接入。CPOST 的固件可用性、最终编码和真实打印扫描仍需目标设备验收。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf)；其他协议对该类型保持严格拒绝。

### R37 当前能力修正：TSPL EAN14

TSPL 模板现支持 `BarcodeType.Ean14`，输出原生 `BARCODE "EAN14"`。模板严格接受 13 位 ASCII 数字 payload，使用 1:1 窄宽比并按完整 GS1-128 符号计算占用宽度；设备负责最终校验位。模板能力快照、四方向旋转、页面边界、元素路径、Profile 禁用、比例校验和失败恢复均有回归。其他协议对该类型在能力预检阶段严格报错。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)；GTIN/AI `01` 业务语义和真实设备验收不由模板层自动推断。

### R36 当前能力修正

TSPL 模板现支持 `BarcodeType.Itf14`，输出原生 `BARCODE "ITF14"`。模板严格接受 13 位 ASCII 数字 payload，并按完整 14 位交叉二五符号计算占用宽度；设备负责最终校验位。模板能力快照、四方向旋转、页面边界、元素路径、Profile 禁用和失败恢复均有回归。其他协议对该类型在能力预检阶段严格报错。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。


### R35 当前能力修正

TSPL 模板现支持 `BarcodeType.Plessey`，输出原生 `BARCODE "PLESSEY"`。模板只接受非空 ASCII 数字并按 1:2 窄宽比计算 Plessey 占用宽度；设备负责最终校验和条形编码。模板能力快照、四方向旋转、页面边界、元素路径、Profile 禁用和失败恢复均有回归。CPCL 与 ESC/POS Native 对该类型在能力预检阶段严格报错，ESC/POS Raster 已由 R99 增加主机光栅路径，ZPL 使用原生 `^BP`。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。

### R34 当前能力修正

TSPL 模板现支持 `BarcodeType.Postnet`，输出原生 `BARCODE "POST"`。模板统一接受 5、9 或 11 位邮政数字并移除连字符，设备生成最终校验位；四方向旋转会按 TSPL 原生坐标和实际占用宽度计算，模板预检在输出前检查能力、元素路径、页面边界和 Profile 禁用。独立命令、Provider、失败恢复和跨协议能力边界均有回归。依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。

### R33 当前能力修正

TSPL 模板现支持 `BarcodeType.I2OF5G`。按 TSC 手册，11/12 位统一输入选择 `BARCODE "DPI"` 并发送 11 位 payload，13/14 位选择 `BARCODE "DPL"` 并发送 13 位 payload；主机按统一 German Post 4/9 规则生成或验证完整校验值。Provider、模板能力快照、四方向、页边界、失败恢复和 Profile 禁用均已覆盖，实际打印和扫描仍需目标 TSPL 固件验收。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。

### R32 当前能力修正

ZPL 模板现支持 `BarcodeType.I2OF5G`。模板数据按 CPCL German Post 4/9 规则由主机生成或校验校验位，再通过 ZPL `^B2` 输出完整 ITF 数据；输出关闭 ZPL 自身 Mod10 选项，避免算法混用。模板覆盖 N/R/I/B 方向、页面边界、元素路径和失败恢复。ZPL 的 `^B2` 语义见 [ZPL 条码说明](https://docs.zebra.com/us/en/printers/software/zpl-pg/fonts-and-barcodes/barcodes.html)。

### R31 当前能力修正

TSPL 模板现支持 `BarcodeType.UccEan128`。模板沿用以数字应用标识开头的 GS1 元素串；ASCII 29 在 `BARCODE "EAN128"` 输出中转换为 `!102` FNC1 控制码，字面 `!` 因 TSPL 控制码歧义而严格拒绝。条码支持四方向、页面边界、模板元素路径和严格失败恢复。TSPL 规范见 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。应用标识、业务字段长度和真实设备扫描结果仍由调用方及目标机型负责。

### R28 当前能力修正

ESC/POS Raster 模板已支持 `BarcodeType.IntelligentMail`/IMb。模板数据允许连字符分组，但归一化后必须为 20/25/29/31 位 ASCII 数字；主机按 USPS-B-3200 生成 65 根四状态条，不追加校验位。宽度、四角旋转、模板静区、页面边界、HRI、元素路径和预算恢复沿同一 Raster Provider 路径处理。ESC/POS Native 继续严格拒绝；FIM D/E 和厂商私有邮政变体仍严格报错。

### R29 当前能力修正

模板 `BarcodeType.Fim` 现支持 A/B/C/D/E 五种模式。FIM D 使用 `111010111`，FIM E 使用 `101000101`；两者与 A/B/C 一样保留九槽位宽度、旋转、页边界、HRI 和预算失败恢复。ESC/POS Raster 负责主机光栅化，CPCL 保留原生 `FIM` 指令，ESC/POS Native 继续严格拒绝。

### R30 当前能力修正

ZPL 模板现支持 `BarcodeType.UccEan128`。模板沿用数字应用标识开头的 GS1 元素串；ASCII 29 分隔符在 ZPL `^BC` 输出中转换为 FNC1 调用码，条码支持四方向、HRI、页面边界和元素路径校验。ZPL 官方 `^BC` 规则见 [Zebra ^BC](https://docs.zebra.com/us/en/printers/software/zpl-pg/c-zpl-zpl-commands/r-zpl-bc.html)。TSPL 对该类型仍在模板预检阶段严格报错。

使用 `TemplateRenderOptions.Job = new PrintJobOptions { Copies = 2, CopyOrder = PrintCopyOrder.Collated }` 可显式按完整份编排分页模板。任务级进纸和切纸分别设置 `FeedScope`/`CutScope`；输出页带零基逻辑页、份数和收尾标志。`MaxPages` 在任务模式统计实际输出页数，包含重复份数。旧页面设置不与任务策略叠加，未提供 Job 时继续使用原行为。详见 [任务策略](protocols.md#显式任务级份数与收尾)；可执行四协议示例为 `samples/Templates/PrintJobSamples.cs`。

ESC/POS Native 混合收据可通过 `EscPosDevice = new EscPosDeviceOptions { NativeFlow = EscPosNativeFlowMode.Sequential }` 显式启用。模板仍使用统一模型，码图的自动尺寸进入顺序游标校验，支持文字、明细、条码、二维码及页尾；完整示例为 `samples/Templates/native-mixed-receipt.json`，运行 Templates 示例可生成 `.escpos` 文件。默认 Rows 保留旧行为。Sequential 坐标表达逻辑顺序和分页占位，设备自行执行原生码图进纸；不承诺精确物理坐标，非零静区、图形及旋转仍需 Raster。详见 [协议说明](protocols.md#native-混合收据)。

`Bing.EasyPrint.Templates` 将模板和业务数据转换为目标打印协议的逐页字节。默认目标协议为 CPCL，也可通过 `TemplateRenderOptions.Protocol` 选择 ESC/POS、TSPL 或 ZPL；ESC/POS 还可通过 `EscPosLayout` 选择 Raster 或 Native。它不连接打印机，也不读取模板指定的文件或 URL。目标框架保持 netstandard2.0/2.1、.NET Core 3.1、.NET 6/7。

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

公开类型位于 `Bing.EasyPrint.Templates`；原有模型仍位于 `Bing.EasyPrint.Model` 和 `Bing.EasyPrint.Elements`。`SaveJson(template)` 保留传入模板的 V1、V2 或 V3 版本。未显式设置编码时，CPCL、ESC/POS 和 TSPL 使用 GBK，ZPL 使用 UTF-8；也可设置 `TemplateRenderOptions.Encoding`，编码必须与打印机配置一致。

### 协议输出

R33 补充：TSPL 模板的 `I2OF5G` 使用原生 `DPI`/`DPL`。11/12 位输入输出 11 位 `DPI` payload，13/14 位输入输出 13 位 `DPL` payload；主机先按统一 4/9 规则生成或验证完整校验位。

TSPL 统一模板的 Code93 条码输出为 `BARCODE ...,"93",...`，接受非空可打印 ASCII；I2OF5/I2OF5C 分别输出 `BARCODE ...,"25",...`/`"25C"`，只接受数字，25 类型的奇数输入会补一个前导 `0`，25C 类型的偶数输入会补一个前导 `0`，由设备生成 25C 校验位；MSI 条码输出为 `BARCODE ...,"MSI",...`，只接受 1–13 位数字且不自动追加校验位。需要设备的 `MSIC` 校验变体时，应通过受信任的原始 TSPL 指令入口显式使用。TSPL 其余统一条码集合和不支持能力仍按 Provider 严格预检。

ZPL 统一模板还支持 POSTNET、PLANET 和 Intelligent Mail：分别输出 `^BZ`、`^B5` 与 `^BZ` 类型 3，POSTNET 接受 5/9/11 位数字，PLANET 接受 12/14 位并校验数字总和，IMb 接受 20/25/29/31 位数字且不追加校验位；三者都会移除输入中的连字符并支持 0/90/180/270 度。I2OF5G 使用 `^B2` 的 `e=N`，校验位由主机按 4/9 规则生成或验证。IMb 的 20 位跟踪码和最多 11 位路由码由调用方按 USPS 业务规则提供。

R33 补充：TSPL 统一模板还支持 I2OF5G，11/12 位输入输出 `BARCODE "DPI"` 的 11 位 payload，13/14 位输入输出 `BARCODE "DPL"` 的 13 位 payload；主机按统一 German Post 4/9 规则生成或验证校验位。下方历史 TSPL 集合文字未列出 I2OF5G 的部分由本补充覆盖。

模板布局先转换为协议无关的页面元素，再由目标 Provider 生成字节。CPCL 保持旧的默认输出；TSPL 的统一模板支持 Code128、Code39、Code93、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、I2OF5C 和 MSI，并生成坐标式标签指令；25/25C 规则依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。TSPL 和 ZPL 生成坐标式标签指令，斜线位图需在 `TsplMediaOptions` 或 `ZplMediaOptions` 中显式设置 `AllowRasterizedLines = true`。ESC/POS 默认 Native；显式选择 Raster 才会将整页合成为单色位图，适合坐标、线框、Code 128/Code 39/Code 93/EAN/UPC/Codabar/I2OF5、二维码和图片；Native 生成顺序文字、进纸以及原生 Code 128/GS1-128/Code 39/Code 93/EAN8/EAN13/UPC-A/UPC-E/Codabar/I2OF5 命令，GS1-128 使用 `GS k 74`，统一模板保留数字应用标识开头的 GS1 元素串，独立命令可按 Epson 约定传入 7-bit ASCII；I2OF5C/I2OF5G 在主机补齐或校验对应校验位后复用 ITF，不能表达通用坐标图形。ZPL 统一模板支持 Code 128、Code 39、Code 93、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、I2OF5C、I2OF5G、MSI、POSTNET、PLANET 和 Intelligent Mail，分别输出 `^BC`、`^B3`、`^BA`、`^B8`、`^BE`、`^BU`、`^B9`、`^BK`、`^B2`、`^BM`、`^BZ`、`^B5` 和 `^BZ` 类型 3；I2OF5C 偶数输入补前导 `0` 后由 `^B2` 的 `e=Y` 生成 Mod10 校验位，I2OF5G 由主机按 4/9 规则处理并以 `e=N` 输出，统一 MSI 使用 `^BMA` 无校验位，直接命令可显式选择 A-D 校验方案，IMb 严格校验 20/25/29/31 位输入。目标协议不支持的元素、样式或页面设置会在输出前明确报错，并包含模板节点路径。

文字可以用 `Font` 明确选择字体来源：`{ "Kind": 0 }` 使用协议默认字体，`{ "Kind": 1, "Name": "1" }` 使用设备字体，`{ "Kind": 2, "Name": "Microsoft YaHei" }` 使用主机字体。`Kind` 必须是整数 0、1 或 2；默认字体不能写 `Name`，设备/主机字体必须提供非空字符串名称，`Font` 对象只允许这两个字段。CPCL 设备字体接受 `24`、`55`，并要求字号与原有内置字库匹配；TSPL 接受 `0`–`8`，ZPL 接受单个数字或大写字母；主机字体仅适用于 ESC/POS Raster。ESC/POS Native 当前使用协议默认字体，不能将 TSPL/ZPL/CPCL 的设备标识传入。

未指定 `Font` 时，旧 `FontFamily`、`DeviceFontName` 和 CPCL `FontName` 保留原有行为。显式 `Font` 不能与非零 `FontName`、非空 `FontFamily` 或旧 `DeviceFontName` 混用；JSON 中请省略旧字段，而不是保留一个 `DeviceFontName: null`。非 CPCL 输出不能直接使用旧 `FontName=24/55`：引擎会在读取业务字段和资源前明确拒绝，请改用目标协议的语义字体或设置 `FontName=0` 使用默认字体。粗体和下划线继续由 `Bold`/`FontStyle` 表达，并受目标 Provider 能力约束。保存和重新加载不会改写旧模板字体字段。

ESC/POS Raster 可通过 `TemplateRenderOptions.EscPosDevice.RasterFonts` 固定主机字体字节、资源版本及有序替代策略；此时 `Font.Kind=2` 的名称和旧 `FontFamily` 引用资源别名，留空使用目录默认资源。未声明别名、缺少所需常规/粗体且没有可用替代样式会在图片读取和业务绑定前拒绝；实际文字的字形覆盖在度量与绘制时继续检查。固定资源同时用于换行、字号适配、墨迹边界和最终输出，不自动改用系统字体。使用方式见 [打印指令集](protocols.md)。

编辑或生成模板前可调用 `TemplateProtocolCapabilities.Create(options)` 查询该输出配置支持的元素、语义字体、字号、旋转角度、样式和码图参数。`SupportsFont(font, size)` 同时检查 CPCL 字体/字号对应关系、Native 的 24 点限制及 TSPL 放大倍率边界；查询不会证明目标固件已安装字库。`DeviceFonts[*].Sizes` 和 `TextSizes` 为 null 时使用整数范围，非 null 时须同时满足离散字号。能力是独立快照，模板静态校验使用同一来源，最终内容与布局仍需渲染验证。

#### ESC/POS 设备配置

ESC/POS 模板应按目标设备手册配置 `TemplateRenderOptions.EscPosDevice.Profile`。Profile 用于选择 GS `v 0` 或 ESC `*` 图片指令，声明 8 点/24 点点阵模式、水平/垂直 DPI、进纸运动单位、可打印纸宽、单条图片数据预算，以及 Native 模式的切刀和原生条码/二维码/文字倍数能力。ESC `*` 使用按列的 8 点或 24 点垂直带，GS `v 0` 使用按行数据；两种指令不能仅凭相同的宽高点数互换。ESC `*` 的非等方 DPI 会按垂直 DPI 重采样输入位图并按运动单位累计进纸，GS `v 0` 要求横纵 DPI 相同。水平分块每个垂直带只写一次图像控制 `LF`，不会产生模板额外进纸或切纸；页面 `FeedLines` 仍单独控制页级进纸。

`EscPosDeviceOptions.Dpi` 省略时，有 Profile 就默认取 `Profile.HorizontalDpi`，无 Profile 时为 203；显式值必须与 Profile 的水平 DPI 一致。Profile 禁用原生能力只影响 Native，Raster 会使用主机位图绘制；GS `v 0` 配置非默认 ESC `*` 模式或单向打印会明确拒绝。编码结束会写入 `ESC 2`，启用单向打印时再写入 `ESC U 0`，恢复协议默认行距和双向打印状态，不保存调用前的打印机状态。

```csharp
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Protocols;

var directDevice = new EscPosDeviceOptions
{
    Profile = EscPosPrinterProfile.EightDotImpact(360)
};
var directBytes = new EscPosPrintCommand(
        Encoding.ASCII, EscPosLayoutMode.Raster, directDevice)
    .Initialize()
    .RasterPage(360, 120, canvas => canvas.DrawLine(0, 0, 359, 0))
    .Build()
    .GetBytes();

var renderOptions = new TemplateRenderOptions
{
    Protocol = PrintProtocol.EscPos,
    EscPosLayout = EscPosLayoutMode.Raster,
    EscPosDevice = new EscPosDeviceOptions
    {
        // 未设置 Dpi，默认使用 Profile.HorizontalDpi（此示例为 180）。
        Profile = EscPosPrinterProfile.EightDotImpact(360)
    },
    FeedLines = 0,
    CutAfterPrint = false,
    Encoding = Encoding.ASCII
};
```

`EightDotImpact(360)` 只是通用的 8 点针式设备配置示例，不代表任何品牌或型号已经通过验收。只支持 ESC/P 或厂商私有指令的针式设备不能用 ESC/POS 结果替代；必须依据对应手册实现或选择相应 Provider。Epson 的 [ESC `*` 点阵图参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/esc_asterisk.html) 和 [ESC `3` 行距参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/esc_3.html) 仅用于核对命令字段。

运行 `dotnet run --project samples/Templates -- artifacts/template-samples` 可生成固定标签、分页小票和价签的默认 CPCL 文件。PM0025、PM0034 的可编辑新版模板也分别保存在 `samples/Templates/price-label-pm0025.json` 和 `samples/Templates/price-label-pm0034.json`；示例程序会直接加载这两份文件，并核对与 C# 构造的逐页 CPCL 字节一致。两者按新模型独立重制，参考 ERP 样例的纸张尺寸、字段和二维码位置（PM0025 旧样例不含二维码），不会自动导入 ERP 旧 JSON。程序会检查 PM0025 的常规/促销分支，以及 PM0034 的常规、促销、会员和促销+会员分支。示例不会连接或打印到设备。

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

文字和一维条码可设置 `Rotation: 0 | 90 | 180 | 270`，模板中的角度统一按顺时针解释，默认 `0`。`StartCoordinate` 和 `EndCoordinate` 描述旋转后的占位区域；布局轨迹的 `Width`、`Height` 已经是旋转后的尺寸，并单独返回 `Rotation`。旋转前的字宽、行高和条码静区仍按原有度量规则计算，90/270 度交换占位宽高，分页不会拆开旋转文字块或条码。

ZPL POSTNET 使用 `BarcodeType.Postnet`，PLANET 使用 `BarcodeType.Planet`，Intelligent Mail 使用 `BarcodeType.IntelligentMail`；模板会在 Provider 阶段严格校验各自的数字长度、连字符归一化后的内容、PLANET 校验位、IMb 的 20/25/29/31 位路由长度、条高和模块宽度；不支持的邮政变体不会被猜测或静默转换。

文字新增 `Width`，表示旋转前单行的排版宽度（打印点）。零表示自动：0/180 度使用区域水平宽度，90/270 度使用结束坐标限定的可用高度，没有结束坐标时使用单页高度上限。建议多列旋转文字显式设置 `Width` 控制换行；`TextAlignment` 仍在旋转前逐行对齐。整个旋转文字块横向放不下或高度超过单页时会明确失败。条码 `Width`、`Height` 继续表示旋转前占位，`HumanReadableReserveHeight` 也随条码一起旋转。

CPCL 文字支持四个角度，模板适配层负责换算原生逆时针文字命令；旋转文字下划线会被拒绝。CPCL 模板条码支持 0 和顺时针 270 度（原生 VB 方向），不把 90/180 度静默映射成其他方向；直接构造 `PrintBarcodeElement` 时同样会对 90/180 度严格报错，0/270 保持输出。TSPL 文字和 Code128、Code39、Code93、EAN/UPC、Codabar、I2OF5、I2OF5C、MSI 条码支持四个角度；ZPL 文字、Code128、Code39、Code93、EAN/UPC、Codabar、I2OF5、I2OF5C、I2OF5G、MSI、POSTNET、PLANET 和 Intelligent Mail 条码支持四个角度，I2OF5C 偶数输入补前导 `0` 后使用 `^B2` 的 `e=Y`，I2OF5G 由主机按 4/9 规则生成或验证并使用 `e=N`，统一 MSI 使用 `^BMA` 无校验位，IMb 使用 `^BZ` 类型 3。ESC/POS Native 支持 Code 128/GS1-128/Code 39/Code 93/EAN8/EAN13/UPC-A/UPC-E/Codabar/I2OF5/I2OF5C/I2OF5G，但仍严格拒绝非零文字、条码和二维码旋转；GS1-128 的 AI、FNC1 和业务校验由调用方按目标 Epson 固件约定处理，I2OF5C/I2OF5G 的校验位由主机按对应语义处理后复用 ITF。ESC/POS Raster 支持主机文字、Code 128/Code 39/Code 93/EAN/UPC/Codabar/I2OF5 条码和二维码的 0/90/180/270 度旋转；固定字体临时掩码会连同下划线一起旋转，条码可读文字和预留区随条码旋转，模板行高及码图占位按旋转后范围预留，静区校验也按旋转后的占位计算。ESC/POS Raster 的独立命令页可通过带字号、样式和角度的 `Text`、`RasterBarcode` 和 `RasterQrCode` 绘制主机内容。现有 CPCL 命令 API 的角度和字节行为保持兼容。

## 模板和布局

- `Version` 为 1、2 或 3。V1 保持原有 JSON 行为；V2 增加变量、页面分区与通用组合元素；V3 增加流式组合、实例上下文和参数。未知类型、未知属性、重复键和类型元数据会被拒绝。
- 所有尺寸为打印点，但各协议使用自己的 DPI 语义：CPCL 沿用历史固定 200 DPI；TSPL、ZPL 默认使用 203 DPI 元数据，也可显式设置实际设备值；ESC/POS 无 Profile 时默认 203，有 Profile 时默认使用其 `HorizontalDpi`。DPI 只参与对应协议的能力描述和布局换算，不会改变打印头的硬件分辨率，必须与目标设备手册一致。`Width` 必须为正；固定页 `Height` 必须为正。`AutoHeight=true` 时按内容缩短页高，`Height` 为分页上限；为零则使用 `MaxPageHeight`（默认 4096）。
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

### V3 流式组合

V3 的组合实例和组件定义均可包含 `Elements` 或 `Rows`，两者互斥。实例使用 `ComponentId` 时不能再携带内联内容。`Elements` 适合固定坐标的价签或图形组，默认 `KeepTogether`；`Rows` 适合列表及表格，默认 `Split`。`TemplateTableFactory.CreateGroup(...)` 可直接生成包含表头和重复明细的组合。内联组合、定义和实例内容都参与深度、行数、页数及输出大小限制；缺失引用、循环引用和内容源冲突会报节点路径。

子元素位置相对于组合原点；移动实例不改定义。未设置 `StartCoordinate` 的组合随前文实际高度排列，后续流式内容自动下移；绝对定位元素仍保持指定位置。宽度按实例、定义、父容器依次取值；高度为零时按内容计算，正高度约束内容，超限报错。`Split` 允许文字行和行列边界跨页，图片、码图和内部整体组合不可拆；组合内表格仅在所属明细的续页重绘表头，空明细不输出孤立表头。

实例 `DataPath` 从当前外层数据选择组件上下文；普通字段路径随后从该对象解析，`$root` 始终指向整份数据。组件参数先从外层上下文求值，再切换 `DataPath`；实例 `Parameters` 可传 `{ "Path": "Labels.Title" }` 或 `{ "Value": "商品" }`。实例值优先于定义的 `DefaultValue`，缺少 `Required` 参数报错。组件内部通过 `$param.title` 读取，嵌套组件必须显式传参。既有 `$item`、`$index` 与根数据引用保持原义。JSON 参数是数据，不执行脚本，也不隐式把字符串转成数字。

完整 C# 与 JSON 对照见 `samples/Templates/V3Samples.cs` 和 `samples/Templates/v3-flowing-receipt.json`。示例顶部固定、中间明细循环、底部自动下移，并校验两种模板逐页 CPCL 字节一致。V1/V2 模板无需升级；只有使用 V3 属性时才应设置 `Version: 3`。布局轨迹为每个片段提供 `SourcePath`、`DefinitionPath`、`ComponentInstancePath` 和 `FragmentIndex`，设计器可据此定位实例与定义。

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

V2/V3 的矩形（`Type: 7`）可设置布尔值 `Filled`，省略或 `false` 保留原有轮廓输出。`Filled: true` 按 `Width × Height` 绘制实心黑色区域，其占位和分页使用相同宽高；`LineWidth` 不改变填充面积，但仍须满足原有参数校验。CPCL、TSPL、ZPL 和 ESC/POS Raster 支持实心填充，ESC/POS Native 严格拒绝。TSPL/ZPL 使用原生图形命令，因此机型禁用图片但允许图形时仍可使用；禁用图形则在资源读取前拒绝。ZPL 填充宽高为 1–32000 点，超限在模板静态预检时报告元素路径。

V2 另支持 `Type: 6` 组合、`7` 矩形、`8` 椭圆轮廓、`9` 单色图标、`10` 二维点阵码。椭圆和图标在通用布局中展开为线段；CPCL 和 ESC/POS Raster 会按各自 Provider 输出，TSPL/ZPL 需显式启用斜线位图，ESC/POS Native 对坐标图形、图标和点阵图片会严格拒绝。图标仅有 `check/cross/info` 三种，不加载外部路径。`Type: 10` 的 `Format` 为 `DataMatrix` 或 `PDF417`，使用 ZXing.Net 编码为黑白点阵再调用图片指令；码图尺寸须足以容纳完整符号，不会压缩条纹。`Width/Height`、单图像素与输出字节均受现有选项限制。

文字默认字号 24；CPCL 支持库中 `FontSize` 的内置字号，以及粗体、下划线和反白。`FontName=0` 自动选择本库使用的 24/55 点阵字库：24 的基础半角/全角字距为 12/24 点、高 24 点；55 为 8/16 点、高 16 点。布局与 CPCL 输出使用相同整数缩放倍率。ESC/POS Raster 使用显式固定字体配置或既有系统字体策略，支持粗体和下划线但严格拒绝反白；ESC/POS Native 支持 24 点顺序文字、粗体、单线下划线和反白，模板分别使用 `FontStyle=4` 与 `Reverse=true`；TSPL/ZPL 使用设备内置字体且当前不支持粗体、下划线或反白。目标协议不支持的字号、字体编号或样式会明确报错。

代码页编码按字符实际的单/双字节选择半角/全角字距；UTF 编码区分拉丁字符、半角片假名、CJK、全角形式及补充平面宽字符，组合附加符不独占字距。CPCL 的度量针对上述固定点阵字库；TSPL/ZPL 可通过 `TemplateRenderOptions.DeviceHalfCellWidthAt24` 和 `DeviceFullCellWidthAt24` 分别校准半角、全角字宽，默认全角为半角的两倍，两个值都会按字号缩放并影响中英混排的换行和分页。相关选项只用于布局，不负责下载字体或为打印机补充缺失字形；设备须配置与输出编码匹配的字库。`NeedFeedLine` 控制自动换行，显式 CR/LF 转换为布局换行。`Fit` 优先选择能单行容纳的较小字号：CPCL 从内置字号中选择，其他协议可缩小到正整数点数；随后按换行设置处理。斜体等未实现样式报错。

需要让 TSPL/ZPL 模板在输出前拒绝未经设备测量的原生占位时，可设置 `TemplateRenderOptions.RequireDeclaredNativeBounds = true`。严格模式只适用于 TSPL/ZPL；对于无法由主机确定字距的文字，可在 `DeviceFontCalibrations` 中按 `DeviceFontName` 配置半角宽、全角宽和行高，未显式选择字体时使用键 `"0"`。匹配字体的校准优先于全局 `DeviceHalfCellWidthAt24`、`DeviceFullCellWidthAt24`、`DeviceLineHeightAt24`；未匹配时仍使用三个全局值作为兼容回退。每组测量值都必须是打印点单位的有限正数。`TextElements` 可为单个 Unicode 文本单元覆盖 24 点下的前进宽度，并可选声明相对设备文字命令原点的墨迹矩形；这也可为组合附加符组成的文本单元单独校准字距。前进宽度允许为零；墨迹矩形四边必须同时提供，允许负向 bearing，参与换行、分页以及旋转后的页面边界检查。未配置单元时继续采用半角/全角回退。TSPL 固定字体 `1` 至 `8` 的可打印 ASCII 文本属于已知点阵例外；逐字体测量值以该字体设为 24 点时的实际输出为基准，其他字号按设备实际整数放大倍率换算，半角字宽和行高不会低于已知点阵尺寸。同一字体中的非 ASCII 文本、TSPL 字体 `0` 以及 ZPL 设备字体仍需提供校准值。严格模式下每个输出的 `BpBarcodeElement` 还必须设置正的 `Width`；默认或显式开启可读文字时，还必须设置正的 `HumanReadableReserveHeight`，显式关闭则无需预留。

```csharp
var options = new TemplateRenderOptions
{
    Protocol = PrintProtocol.Tspl,
    RequireDeclaredNativeBounds = true,
    DeviceHalfCellWidthAt24 = 12,
    DeviceFullCellWidthAt24 = 24,
    DeviceLineHeightAt24 = 24
};
options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration>
{
    ["1"] = new DeviceFontCalibration
    {
        HalfCellWidthAt24 = 16,
        FullCellWidthAt24 = 32,
        LineHeightAt24 = 24
    }
};
```

单个字符或组合文本单元可增加精细度量；边界按 24 点字号下相对设备文字命令原点的打印点填写，左右/上下值必须成对完整提供：

```csharp
options.DeviceFontCalibrations["1"].TextElements = new Dictionary<string, DeviceTextElementCalibration>
{
    ["e\u0301"] = new DeviceTextElementCalibration
    {
        AdvanceAt24 = 11,
        InkLeftAt24 = -1,
        InkTopAt24 = -3,
        InkRightAt24 = 10,
        InkBottomAt24 = 22
    }
};
```

未开启严格模式时保持现有自动估算行为，默认值为 `false`，因此旧模板和 CPCL 输出不变。严格模式中的声明应来自目标型号的实际测量；它只能加强主机侧布局检查，不能替代设备手册、打印头可用区域、固件条码编码差异和最终实机验收。校准数据缺失或条码宽度未声明时，渲染会在输出前抛出包含协议和模板元素路径的明确错误。

图片的字段值是资源键。实现 `ITemplateResourceProvider.OpenRead` 提供流；返回流归渲染器释放。宽高均为零时使用原尺寸，只给一个尺寸时按比例缩放。同一布局块可复用资源，正文处理后释放缓存；多帧 GIF/TIFF 仅解码首帧。CPCL 模式下 `IsCompress=true` 使用 CG 二进制图形，默认使用 EG 十六进制图形；其他协议按各自 Provider 输出图片。必须直接传输页字节，不能通过字符串往返转换二进制数据。

二维码使用 Model 2，默认模块宽 4、纠错 M。按 [DENSO WAVE 容量表](https://www.qrcode.com/en/about/version.html) 计算全部 1–40 版本及 L/M/Q/H 纠错等级，包含模式位、随版本变化的长度位及四边静区。纯数字、纯字母数字分别计算压缩容量；其他内容按实际输出编码的字节数预留，打印机自动分段编码时可能使用更小的区域。空内容和超过版本 40 的数据报错。`Width/Height` 可增加预留区域，不能缩小到计算值以下。

一维条码默认 Code128、`LineWidth=2`、`Ratio=2`，须设置正的 `Height`。参数遵循 [Zebra CPCL Link-OS 手册 6.2.1](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/cpcl-link-os-pg-en.pdf)：实际窄条点宽为 `LineWidth + 1`；`Ratio=0..4` 对应 1.5、2、2.5、3、3.5，`20..30` 对应 2.0 至 3.0。POSTNET 和 PLANET 的 Ratio 为条间距参数，实际间距加一。非整数宽条向上取整预留。各制式分别计算符号、起止符、校验符和静区，不能用 Ratio 直接乘整个条码宽度。TSPL/ZPL/ESC-POS 可通过 `NarrowModuleWidth` 显式指定窄条点宽，TSPL 还可通过 `WideModuleWidth` 指定宽条点宽；CPCL 不接受这两个新属性，ZPL/ESC-POS 不接受显式宽条宽度。未设置时保留旧模板映射。

| 制式 | 模板输入及尺寸规则 |
|---|---|
| EAN13 / EAN8 / UPCA | 固定模块数；接受完整号码或缺一位校验码的号码，补齐或校验 mod-10。EAN8 也接受 6 位并补前导 0 |
| UPCE | 6 位数据或 7 位含号码系统的数据，号码系统为 0/1；6 位补前导 0 |
| Code39 | 基本字符集大写字母、数字及 `-. $/+%`，起止符由打印机添加 |
| Code93 | 可打印 ASCII，扩展字符按两个符号，计入两个校验符 |
| Code128 | 可打印 ASCII；纯偶数数字按子集 C，混合内容按全 B 的安全上界，不依赖固件的自动切换策略 |
| UCCEAN128 | 统一模板要求以数字应用标识开头的 GS1 元素串，计入 FNC1；不接受带括号的展示文本。ESC/POS Native 独立入口接受 2–255 字节 7-bit ASCII，并按 Epson `GS k 74` 的特殊字符规则传入；应用标识、业务字段长度及 GS1 业务校验由调用方负责 |
| Codabar | A/B/C/D 起止符，内部为数字及 `-$:/.+` |
| I2OF5 / I2OF5C | 数字；必要时补前导 0，使包含校验位后的长度为偶数；CPCL 由打印机添加校验位，ESC/POS Native 由主机按 3/1 规则添加后输出 ITF |
| I2OF5G | 11–14 位数字；按完整 12/14 位符号预留，指令使用字母 O 的 `I2OF5G` |
| MSI | 1–13 位数字，无额外校验位 |
| POSTNET / PLANET / Intelligent Mail / FIM | POSTNET 为 5/9/11 位数字，可用连字符分组，计入自动校验位；PLANET 为 12/14 位数字，必须包含使数字总和对 10 取模为 0 的校验位；Intelligent Mail 为 20/25/29/31 位数字，不追加校验位；三者均可用连字符分组；FIM 仅 A/B/C |

数据规范化发生在本次渲染内部，不修改模板字段。`Width` 可扩大预留空间；不足时明确失败。模板结构化入口不支持 Code128 二进制功能字符替代码；此类高级指令可由可信的基础指令入口生成。

## 码图静区

二维码和一维条码可设置 `QuietZone: { Left, Top, Right, Bottom }`，各边为非负整数打印点，未写出的边按零处理。省略或 `null` 保留旧留白：二维码四边各四模块，一维条码按制式计算；显式 `{}` 表示四边零留白。模板坐标指向包含静区的外部占位起点。二维码 `Width/Height`、条码 `Width` 包含静区；条码 `Height` 仍表示条高，布局高度另加可读文字预留和上下静区。旋转时一起换算静区和占位，布局轨迹、单元格边界及分页均使用外部占位。

包含非零静区的页会在输出前校验整页边界及相邻元素占位，禁止文字、线框、图片等侵入留白。TSPL/ZPL 默认启用或显式启用条码可读文字时，必须设置正的 `HumanReadableReserveHeight`，也可显式关闭可读文字。ESC/POS Native 在资源加载前拒绝非零静区；显式零值可用于原点码图。JSON 加载器拒绝静区的负数、浮点数、字符串、未知边名及非对象值。该选项不自动选择码制所需的最小静区，数值应由调用方按业务码制和目标机型确定。

ESC/POS Raster 显式开启条码可读文字时必须声明正的 `HumanReadableReserveHeight`，Provider 会按 `EscPosDeviceOptions.RasterBarcodeHumanReadableFontSize`（省略时 12 点）对应的主机字体实际墨迹检查页边界；CPCL 显式开启 HRI 时通过 `BT` 输出设备文字，预留高度参与模板分页。

`LoadJsonWithPaths` 对非法静区返回带完整元素或四边字段路径的 `TemplateRenderException`，Host 将其保留为 `sourcePath`；此时尚未分页，页面和输出元素索引为 null。普通 `LoadJson` 保持原有 `JsonSerializationException` 契约。

## 资源与失败语义

| 选项 | 默认值 |
|---|---:|
| MaxJsonBytes | 1 MiB |
| MaxDepth | 16 |
| MaxDetailRows | 10,000 |
| MaxPages | 100 |
| MaxOutputBytes | 32 MiB |
| MaxWorkingBytes | null（未启用累计工作资源预算） |
| MaxImageBytes | 10 MiB / 单图 |
| MaxImagePixels | 16,000,000 / 单图 |
| MaxPageHeight | 4096 点 |

这些值可由应用调整。字段、图片打印数据及缓存也按输出预算进行提前检查；页数和最终字节数在生成时检查。限额是拒绝边界，不是吞吐或峰值内存保证。渲染器在遍历、分页和资源流读取之间检查取消；同步解码和应用 getter 无法在执行中强制中断。

`MaxWorkingBytes` 是可选的显式资源估计预算，与 `MaxOutputBytes` 分别检查。它覆盖已接入的编码缓存、解码/缩放 RGBA 图、单色画布、回滚副本、命令缓冲及输出复制；扩容期间旧、新缓冲同时计入。超限抛出 `PrintResourceLimitException`，提供资源名称、本次请求字节、已预留字节及限制。未设置该值时仍可通过外层 `PrintResourceScope` 限制渲染；已有单图、页数和输出限制继续生效。

内部渲染失败会归还本次操作的预留；成功返回时，若存在外层资源作用域，结果预留转移到外层并继续计入预算。没有外层作用域时，内部预算只覆盖这次渲染，返回后不追踪数组的 GC 生命周期。第三方库内部缓存、解析对象和工作缓冲尚不能用此值精确计量，因此它不表示进程实际峰值内存上限。设计器 Host 已接入单请求预算与全局准入，配置见[设计器 API 预算](designer.md#api-预算输入与验证)。

无效模板或资源超限抛出 `ArgumentException`（或其派生类型）；格式错误可抛出 `JsonException`；取消抛出 `OperationCanceledException`。默认 CPCL 普通 `Render` 保留图像解码的原异常类型；布局诊断及其他协议会包装已知图片输入错误并保留内部异常，资源提供器的其他错误仍透传。设计器可使用 `LoadJsonWithPaths` 和 `RenderWithLayout`，在可定位的结构或布局错误中取得 `TemplateRenderException.SourcePath`。共享组件内部的渲染错误还会提供 `DefinitionPath`、`ComponentInstancePath` 和 `InstanceIndices`；嵌套组件保留最内层已知归属，`SourcePath` 继续表示实际展开的实例节点。Provider 阶段错误的 `PageIndex` 与 `ElementIndex` 保留实际页面及页内打印元素的零基序号，静态预检或页级错误没有相应位置时为 null；模板逐页输出时会将 Provider 局部页号换算为整个文档的页号。`ElementIndex` 不等于包含行、单元格及组合节点的公开布局数组下标。原 `LoadJson`/`Render` 接口保持原有调用方式。失败不返回部分页。调用方输入不被修改；同一渲染器可供独立并发调用，但调用期间不得修改模板、数据或选项。

`TemplateRenderException.FieldPath` 表示失败的业务字段绑定路径，例如 `Customer.Name`；`ResourceName` 表示已知图片输入错误对应的资源键，例如 `logo`。不属于对应类别的错误返回 null。这两项元数据沿内部异常链保留，在共享组件补充归属时也继续可用，调用方无需从本地化文案解析字段或资源名称。

## 基础指令与验证

结构化文本、条码和二维码入口拒绝协议控制字符，避免外部输入插入命令。`WriteRaw`、`WriteRawLine` 以及明确的 Raw 方法仍只适用于可信打印指令，不应直接接收外部输入。

CPCL 文字、条码和二维码组件在加入命令时校验文本及协议标记，并固定这些内容；重复 `Build()` 直接输出已校验的组件。分割线字符与当前位置相对于页宽的检查仍在 `Build()` 执行，因此构建前修改页宽仍可能使构建失败。

重复 `Build()` 不再追加之前的结果。图像转换不修改调用方图片，图像限制还可通过基础库的 `ImageProcessingOptions` 配置。

自动回归：`dotnet test tests/Bing.EasyPrint.Tests -f net6.0`。真实打印仅在 `EASYPRINT_ENABLE_HARDWARE_PRINT=1` 时启用，且必须用 `EASYPRINT_PRINTER_NAME` 指定队列；默认自动测试不会提交打印作业。性能工具位于 `tools/PerformanceProbe`，对固定文字和图片输出检查确定性并报告耗时、进程分配量；使用 `IncludeTemplates=true` 增加新引擎场景。

`DefineFormat` 的格式正文也是可信原始指令入口。EG/VEG 数据必须为长度匹配的十六进制字符串；旧 CG/VCG 字符串接口要求编码后的字节数与声明尺寸一致，新代码优先使用 `DrawCompressedImage`。
R22 ESC/POS Raster 条码补充：`I2OF5C`/`I2OF5G` 现在支持四方向主机光栅化，分别按 3/1、4/9 规则补齐或验证校验位后编码为 ITF；页面宽度、HRI 和旋转占位按归一化数据计算。没有可靠主机编码器的 GS1-128、MSI、邮政码等仍按能力矩阵严格拒绝。

R23 能力修正：ESC/POS Raster 模板条码已支持 `MSI` 主机光栅化。`BarcodeType.Msi` 只接受 1-13 位数字，不追加校验位；TemplateGraph、Provider 页边界、宽度测量、HRI 和旋转占位使用相同数据语义，失败保留 `SourcePath`/元素路径并支持后续重试。ESC/POS Native MSI 继续严格报错，未映射邮政码和 GS1 类型继续按能力矩阵拒绝。Raster 旋转下划线模板已验证 0/90/180/270 度墨迹与占位边界；设备字高和扫码结果仍需实机校准。

R24 能力修正：ESC/POS Raster 模板条码已支持 `BarcodeType.Postnet` 主机光栅化。输入接受 5/9/11 位数字和连字符分组，渲染前计算 POSTNET 校验位并绘制全高/半高条；模板的宽度、旋转、页面边界、HRI 和元素路径错误沿同一 Raster Provider 路径处理。Raster 的 POSTNET 不代表 Native GS `k` 命令支持，其他协议仍按能力矩阵严格拒绝未声明的邮政码。

R25 能力修正：ESC/POS Raster 模板条码已支持 `BarcodeType.Planet` 主机光栅化。模板输入接受 12/14 位数字和连字符分组，渲染前验证数字和校验并使用 POSTNET 反相的全高/半高条模式；宽度、四角旋转、HRI、页面边界和元素路径错误沿同一 Provider 路径处理。Raster PLANET 不代表 Native ESC/POS `GS k` 命令支持，未映射的 GS1-128、Intelligent Mail/IMb、FIM 等仍严格拒绝。

R26 能力修正：ESC/POS Raster 模板条码已支持统一模型的 `BarcodeType.Fim` A/B/C 模式。模板渲染按 USPS 九位模式生成条/空槽位，宽度、四角旋转、HRI、模板静区、页面边界和元素路径错误沿同一 Raster Provider 路径处理；FIM D/E、GS1-128、Intelligent Mail/IMb 和厂商私有邮政变体继续严格拒绝。

R27 能力修正：ESC/POS Raster 模板条码已支持 `BarcodeType.UccEan128`/GS1-128。模板输入要求数字应用标识开头，可使用 ASCII 29 表示 FNC1 字段分隔；主机启用 ZXing `GS1_FORMAT` 生成 Code 128 光栅，HRI 会省略不可打印的分隔符。宽度、四角旋转、模板静区、页面边界、元素路径和预算恢复沿同一 Raster Provider 路径处理；Native `GS k 74` 保持原有行为，AI 业务语义和实机扫描仍需调用方及目标设备确认。Intelligent Mail/IMb、FIM D/E 和厂商私有邮政变体继续严格报错。
