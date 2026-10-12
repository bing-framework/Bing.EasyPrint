# 打印指令集

> 文档定位：本文件保存协议能力、设备 Profile 和各轮兼容细节。首次接入请先阅读[核心打印与协议](core-printing.md)；未完成和设备验收项见[待实现与待确认](status.md)。带 `Rxx` 的段落是对应迭代的记录，不等同于当前分支验证结论。

### R152：GS1 GDTI、GLN 扩展与 GCN

共享括号 AI 解析器新增 AI `253`、`254`、`255`。AI `253` 接受含有效 GS1 校验位的 13 位 GDTI 键及可选 1–17 位 CSET 82 序列组件；AI `255` 接受含有效校验位的 13 位 GCN 键及可选 1–12 位数字序列组件；AI `254` 接受 1–20 位 CSET 82 GLN 扩展组件，并要求同一元素串包含含有效校验位的 AI `414` 物理位置 GLN。GS1 的 GLN+扩展示例按 AI `414` 后接 AI `254` 排列。变量字段后续还有元素时会插入 FNC1。单个新 AI 也进入严格解析；其他历史单元素括号输入兼容行为保持不变。

ESC/POS Native/Raster、TSPL 和 ZPL 的 GS1 结构化入口共享这些规则。Raster DataBar Expanded 保留括号输入作为 HRI 文本。此处只声明以上字段子集，不代表完整 GS1 AI 字典或业务关系校验。字段定义和校验位规则依据 [GS1 General Specifications Release 26.0](https://ref.gs1.org/standards/genspecs/)；AI `254` 与 AI `414` 的配对要求亦见 [GS1 General Specifications 24.0](https://ref.gs1.org/standards/genspecs/24.0.0/)。

### R141：四协议受限 Raw 校验

`WriteRaw`、`WriteRawLine` 和 `WriteRawBytes` 继续作为可信调用方的兼容逃生口，保留原有字节输出且不解析内容。需要校验文本指令时，使用 `IRestrictedRawPrintCommand<TCommand>.WriteRestrictedRaw(string)`；该入口仅接受各协议明确支持且能完成参数、能力和几何校验的语法子集。成功后原文按当前命令编码写入，不规范化空格、大小写或换行。未知/厂商私有命令、下载、状态查询、不可解析的二进制载荷和未闭合块会以包含协议、命令及行号或字节偏移的 `ArgumentException` 拒绝。整批先在临时状态中解析；任何语法、编码、能力或边界失败都不会追加 Raw 或改变后续命令状态。

CPCL 命令实例只承载一个包含任务边界的完整受限 Raw 程序。写入后再次写入 Raw 或结构化内容会在追加时抛出 `InvalidOperationException`，原程序及后续 `Build()` 输出保持不变；`SetPage` 或 `InitCommand` 会重置实例，之后可开始新的结构化任务。需要生成多个独立完整任务时，分别创建命令实例。

CPCL 仅含页面正文的受限 Raw 必须以 CRLF 结束，因为构建时还会自动追加 `PRINT`。TSPL 受限 Raw 可将无换行的末行作为最终批次；若紧接着再次调用 `WriteRestrictedRaw`，前一批必须以 CR/LF 结束，或当前批次必须以 CR/LF 开始。校验不会补写或规范化 Raw 文本；不满足分隔条件时整批拒绝且既有输出不变。

受限几何检查范围如下：

| 协议 | 受限 Raw 的范围和几何前置条件 |
| --- | --- |
| CPCL | 完整任务必须包含任务头 `!`、`PAGE-WIDTH` 和最后的 `PRINT`；仅含正文时复用现有结构化页面尺寸。文字/文字块、线框、条码、QR/PDF417/MaxiCode/Aztec 和长度可核验 EG/CG 图形检查页面范围。`B RSS` 复合命令不在受限子集内，因为其固件分段和 footprint 不能由当前跨机型契约可靠确定。 |
| ESC/POS | 只接受 Native 文本和白名单控制、进纸、切纸、原生条码/二维码序列；Raster 图片和任意二进制内容仍使用相应结构化或可信 Raw 入口。需要横向范围判断的内容必须能从 `GS W`、Profile 或明确宽度配置得到可打印宽度。 |
| TSPL | 绘制前必须有 `SIZE`；受限 Raw 接受介质/方向/参考点、文字、线框/填充、条码、二维码、进纸/打印/切纸及自检命令。`BITMAP` 线上载荷是二进制，因此 `WriteRestrictedRaw(string)` 拒绝该命令；使用结构化 `Bitmap(..., asciiHex)` 会把 HEX 解码为二进制，或使用可信 `WriteRawBytes` 写入完整命令。参见 TSC [TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf) 的 BITMAP 命令示例。 |
| ZPL | 绘制字段必须处于已打开的格式状态中，并在绘制前声明 `^PW` 和 `^LL`。受限 Raw 可打开格式后由后续受限 Raw 或结构化命令续写；最终输出必须由 `^XZ` 或 `End()` 闭合，未闭合时 `Build()`/`BuildOutput()` 拒绝。接受字段文字、线框、当前支持的条码/二维码、ASCII 十六进制 `^GFA`、打印数量及已配置 Profile 允许的切纸命令。 |

设备字体、HRI 和固件条码使用保守 footprint 估算，相关越界错误会注明估算；这不代替目标机型的字形、墨迹和扫描实测。严格入口不改变旧 Raw 方法的既有行为。

活动 `PrintResourceScope` 时，四协议受限 Raw 会在解析前按 `512 + 128 × UTF-16 字符数` 预留临时 tokenizer 工作集；ESC/POS 编码缓冲继续单独计入预算。估算覆盖解析期间的引擎临时数据，不等于进程实际堆用量。预算不足会抛出 `PrintResourceLimitException`，且不追加输出或提交候选状态；成功返回和失败退出都会释放临时预留。

ESC/POS Native 受限 Raw 会在同一行累计文字、条码、二维码和制表位的估算宽度；`ESC a` 对齐设置不会清除已累计宽度，水平制表符推进后立即检查打印区域边界。此检查依赖 `GS W`、机型 Profile 或显式宽度/制表位配置；估算失败不会提交本批 Raw 或其状态。

### R140：ESC/POS Raster GS1 DataBar Stacked

`EscPosPrintCommand` 的 Raster 页面和统一打印 Provider 支持 `BarcodeType.Gs1DataBarStacked`。输入为 13 位 ASCII 数字 payload；主机复用 DataBar Omnidirectional 数据字符编码，生成 50X 宽、顶部 5X、1X 分隔和底部 7X 的两行码图。`height` 表示总符号高度，至少为 `13 * moduleWidth`；HRI 使用隐含 `(01)` 与生成的 GTIN-14 校验位。ESC/POS Native、TSPL、ZPL 和 CPCL 均不声明该能力。尺寸和分行规则依据 [GS1 General Specifications](https://ref.gs1.org/standards/genspecs/12.0.0/) 与 [Zint RSS encoder](https://github.com/zint/zint/blob/master/backend/rss.c)；输出能力不代表已在具体打印机上完成扫描验收。

### R138：按旋转角度配置 TSPL/ZPL 原生 HRI 边界

TSPL/ZPL Profile 可分别为条码旋转 90、180、270 度设置 `NativeBarcodeHumanReadableOverhang`。边距按输出页面坐标解释，只影响 HRI 启用时的主机边界预检，不改变条码字段或协议字节。覆盖结构中的某边为 `null` 时继承 Profile 通用四边值；显式 `0` 会清除该边的通用外伸。未设置角度覆盖时完全沿用原通用边界。配置值用于匹配已知机型，不代替真实字体、HRI 对齐或墨迹测量。

### R137：TSPL/ZPL 原生 HRI 四边页面包围盒

TSPL/ZPL Profile 在原有左侧外伸上新增 `NativeBarcodeHumanReadableTopOverhangDots`、`NativeBarcodeHumanReadableRightOverhangDots` 和 `NativeBarcodeHumanReadableBottomOverhangDots`。四个非负值表示条码当前估算页面矩形之外的额外边距，仅在 HRI 启用时参与 direct 与模板 Provider 的边界预检；右侧外伸与旧 `NativeBarcodeHumanReadableReserveWidthDots` 的最小总宽相加，后者语义不变。条码旋转后的主体尺寸先按既有逻辑换轴，再按页面坐标叠加四边边距；模板 Provider 现在也把 HRI 预留高度随 90/270 度条码转入横向范围。配置只影响主机预检，不移动字段、不改指令字节，也不设置设备字体或对齐。各方向应填写可覆盖目标机型输出的保守值；单一 Profile 不表达逐角度不同测量值。

### R136：TSPL/ZPL 原生 HRI 左侧边界校准

`TsplPrinterProfile` 与 `ZplPrinterProfile` 新增可选 `NativeBarcodeHumanReadableLeftOverhangDots`。填写非负点数后，HRI 启用时 direct 命令和模板 Provider 会把该值计入条码字段坐标左侧的页面 X 轴边界；未设置或为零时保持原预检行为，HRI 关闭时忽略。该属性只扩大主机预检边界，不移动字段、不改变 HRI 对齐或输出字节。它不是字体度量，也不自动适配旋转方向；目标设备应按对应输出方向校准保守值。

### R135：TSPL/ZPL 原生 HRI 横向占位校准

`TsplPrinterProfile` 和 `ZplPrinterProfile` 新增 `NativeBarcodeHumanReadableReserveWidthDots`。填写正数后，只在启用 HRI 时将 direct 命令及模板 Provider 的条码横向边界至少扩到该宽度；未配置时沿用条码主体估算，关闭 HRI 时忽略该值。该配置只影响主机边界预检，不改变协议字节或设备字体。宽度从条码字段原点向右计量；若设备将 HRI 居中且文字向左超出字段原点，调用方仍须自行留出左侧边距。实际 HRI 字形、对齐和墨迹仍需机型资料或实测校准。

### R134：ZPL Code 49 `^B4` 行高与页面 footprint

`ZplPrintCommand.BarcodeCode49(..., height, ..., moduleWidth)` 的 `height` 表示期望的单行高度（dots）。ZPL `^B4` 的 `h` 参数是相对 `^BY` 模块宽度的整数倍率；Provider 会将 `height` 向上换算为倍率，因此不能被 `moduleWidth` 整除时，实际单行高度会上调到最近的模块宽度整数倍。Code 49 页面宽度按本地编码器的 70 模块行宽估算，页面高度按本地编码行数、换算后的行高、分隔条和 HRI 预留估算；此高度仍取决于打印机固件的堆叠布局。超过两行时 interpretation line 可能超出条码主体宽度，HRI 横向占位尚未校准。依据：[Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)。真实 Zebra 固件、HRI、DPI、纸宽、打印和扫描仍需目标设备验收。

### R133：按 TSPL/ZPL Profile 校准 direct 条码 HRI 预留高度

`TsplPrinterProfile` 与 `ZplPrinterProfile` 新增 `NativeBarcodeHumanReadableReserveHeightDots`，用于 direct 结构化条码命令的页面边界预检。填写正数时，该点数用于启用 HRI 的纵向 footprint；未填写时沿用兼容性 24 点主机估值。该属性只影响预检，不改变输出命令，也不控制打印机字体或字号。TSPL 手册没有规定通用 HRI 行高；ZPL `^BC` 可读文字使用当前或先前命令选择的字体，因此 24 点并非设备标准保证值。建议依据目标机型手册或实测填写保守值：[TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)、[Zebra `^BC` command](https://docs.zebra.com/us/en/printers/software/zpl-pg/c-zpl-zpl-commands/r-zpl-bc.html)。HRI 横向字宽、对齐和实际墨迹仍未由该属性校准。

### R132：TSPL/ZPL 补充 Unicode 文本边界估算

直接使用 TSPL 或 ZPL 的原生 `Text` 命令时，页面边界估算按 Unicode scalar 计数。TSPL 保留 ASCII 一格、非 ASCII 两格的主机估算；ZPL 按字段宽度乘 scalar 数。一个 supplementary-plane 字符现在按一个 scalar 处理，避免 UTF-16 代理对被当成两个字符而误拒贴边文本。此修正只影响主机页边界预检，不证明打印机字体的实际 advance、组合字形或墨迹边界；这些仍需具体设备资料或实测校准。

### R131：限制 ZPL 整页方向到标准参数

ZPL `^PO` 整页方向只接受 `N`（正常）和 `I`（反向），因此 `ZplMediaOptions.Orientation` 仅允许 `None` 与 `Down`。`Left`/`Right` 在选项校验阶段抛出 `ArgumentOutOfRangeException`，不会生成错误的 `^POB`/`^POR` 字节。元素自身的 90/270 度旋转仍由各 ZPL 字段命令表达，不受此整页选项约束。依据：[Zebra ^PO 命令说明](https://support.zebra.com/cpws/docs/zpl/PO_Command.pdf)。

### R130：TSPL/ZPL 直接命令页面边界预检

直接使用 `TsplPrintCommand.Size(...)` 或 `ZplPrintCommand.Label(...)` 设置标签尺寸后，文字、条码、二维码、线、线框、填充矩形和位图/图形字段会在写入前检查页面范围。拒绝越界元素时不会追加对应输出。TSPL 的 `mm` 页面尺寸按命令的 DPI 元数据换算成 dots；`REFERENCE`、`DIRECTION` 与 ZPL 页面方向参与预检变换。

Bitmap/GraphicField、线和矩形使用输出尺寸检查。内置字体、原生二维码和条码使用 host-side footprint 估算，超界异常标记“估算”；字体墨迹、HRI、固件条码模块和打印方向仍需机型实测。Code39/Code93 等没有专用原生宽度公式的 direct 条码使用主机编码宽度作估算，该回退不会改变模板排版度量。未设置页尺寸时 direct 命令保持现有无边界行为。模板输出沿用模板 Provider 自己的布局与声明占位预检；`WriteRaw` 和任意原始指令注入不参与结构化页面边界检查。

R130 页面边界预检曾沿用整页方向枚举中的四种值；R131 已将 ZPL 配置收紧为 `^PO` 实际支持的 `None`/`Down`，并在选项校验阶段拒绝 `Left`/`Right`。TSPL 仍可按自身 `DIRECTION` 命令支持的方向设置工作。

### R129：GS1 Scan4Transport AI 子集

GS1 结构化 AI 解析器现在支持 Scan4Transport AI `4300`–`4326`：按 AI 元数据检查固定/可变长度、数字或 CSET 82 字符，并在后续元素前为可变长字段插入 ASCII 29/FNC1。输入同时包含任一 `4300`–`4326` 字段时必须包含 SSCC AI `00`；AI `4303` 地址第二行要求 `4302`，AI `4313` 回运地址第二行要求 `4312`。

ESC/POS Native 与 Raster GS1-128、TSPL EAN128、ZPL GS1-128 使用相同的规范化 payload。AI `4307`/`4317` 只验证两个大写 ASCII 字母的格式，不查询注册表；AI `4324`–`4326` 只验证数字字段长度，不校验日期/时间的日历意义。解析器也不校验机型固件容量、业务地址含义或实物扫描质量。`BpTemplate` 的条码字段仍使用原始 AI/FNC1 数据。

字段和关联规则参考 [GS1 General Specifications 26.0](https://ref.gs1.org/standards/genspecs/)、[GS1 Application Identifiers](https://ref.gs1.org/ai/)及 [GSCN 25-306](https://ref.gs1.org/standards/genspecs/gscn/2025/GSCN-25-306-MandAssoc03.pdf)。本地测试覆盖解析及协议字节；目标热敏/针式打印机仍需机型级实物验收。

### R128：按 Profile 声明值预检 GS1 payload 容量

ESC/POS、TSPL、ZPL 的设备 Profile 可声明 GS1-128 payload 字符上限；ESC/POS 与 ZPL 另可声明 GS1 DataBar Expanded 上限。上限需来自具体设备手册或已验证配置，不配置时库不猜测固件容量。校验统计括号 AI 规范化后的 payload 字符，包含 ASCII 29/FNC1 分隔符、不包含协议 framing；直接命令在追加输出前检查，Raster Expanded 在画布修改前检查，模板 Provider 在输出前检查并报告元素路径。Profile 值会复制进命令快照与协议能力。

此字符上限不是模块宽度、纸宽、打印质量或可扫码保证。Raster Expanded 仍受其既有 77 字符输入上限约束，原生结构化 AI 输入仍受既有协议长度规则约束；未配置机型容量不会放宽这些既有限制。

### R127：括号式 GS1 字段中的字面左括号

直接协议命令的括号式 GS1 AI 输入现在用 `\(` 表示字段值中的字面 `(`，例如 `(91)AB\(CD(17)250101` 会规范化为 `91AB(CD`、ASCII 29/FNC1、`17250101`。圆括号与方括号 AI 展示形式均支持；只有 `\(` 是有效反斜杠转义，未知转义会严格失败，字段长度按解码后的数据计算。值中的 `)` 是 CSET82 字符，无需转义。

ESC/POS Native GS1-128、Raster GS1-128/DataBar Expanded、TSPL EAN128 和 ZPL GS1-128 在输出前共用该规范化行为。Raster DataBar Expanded 通用字段允许原始 CSET82 圆括号，HRI 显示解码后的字面字符，不显示输入转义符；原始数据中不属于 CSET82 的方括号仍拒绝。该括号转义仅适用于直接命令结构化输入，`BpTemplate` 条码字段继续使用原始 AI/FNC1 数据。

字符集参考 [GS1 General Specifications 24.0 CSET82](https://ref.gs1.org/standards/genspecs/24.0.0/)。本地验证证明解析、协议字节和 Raster 字段编码，不代表具体固件容量或真实设备打印/扫码结果。

### R126：GS1 公司内部 AI 长度与 CSET82 边界

共享 GS1 AI 解析器按标准限制 AI `90` 为 1–30 个 CSET82 字符、AI `91`–`99` 为 1–90 个字符；这些是变量字段，后面还有 AI 时会插入 ASCII 29/FNC1。Native GS1-128、TSPL EAN128、ZPL GS1-128/DataBar Expanded 共用字段元数据；Raster DataBar Expanded 仍有 77 字符总输入上限。CSET82 不含空格，因此空格会被拒绝。

当前括号输入语法会把值中的 `(` 识别为下一个 AI 的起点，暂不能在括号形式中表示这个虽属 CSET82 的字符。字段长度合法也不保证目标打印机的 GS1 符号容量足够；原生固件容量和真实打印/扫描需按具体设备验收。字段长度参考 [GS1 General Specifications 26.0](https://ref.gs1.org/standards/genspecs/)，CSET82 字符集表参考 [GS1 General Specifications 24.0](https://ref.gs1.org/standards/genspecs/24.0.0/)，空格排除说明见 [GS1 General Specifications 21.0.1](https://ref.gs1.org/standards/genspecs/21.0.1/)。

### R125：GS1 常见金额/折扣 AI 子集

共享 AI 元数据解析器现支持 `390n`、`391n`、`394n`、`395n`，其中 `n` 为 `0`–`9`：`390n` 是应付金额，接受 1–15 位数字；`391n` 是带 ISO 货币代码的应付金额，接受 3 位数字货币代码加 1–15 位数字金额；`394n` 是百分比折扣，固定 4 位数字；`395n` 是单位价格，固定 6 位数字。ESC/POS Native、TSPL、ZPL 的括号输入会在变长字段后按需插入 FNC1；Raster DataBar Expanded 的 GTIN 加这些 AI 走通用字段路径，不会误用 `392x`/`393x` 专用压缩方法。

本轮只校验字段字符集和结构长度，不校验 `391n` 的货币代码是否属于 ISO 4217，不按 `n` 执行金额小数换算，也不校验 `394n` 的业务数值范围或字段组合。完整语义参考 [GS1 General Specifications 23.0.0](https://ref.gs1.org/standards/genspecs/23.0.0/)。

### R124：ESC/POS Raster Code 49 完整符号

ESC/POS Raster Code 49 现在按 USS-49 输出完整符号：81X 总宽由 10X 左静区、70X 条空区和 1X 右静区组成；上、下各绘制 1X 边界分隔条，行间仍为 1X。总高按 `rows * barHeight + (rows + 1) * X` 预检，`barHeight` 表示单行条高且至少为 8X。Raster Code 49 的条码元素坐标现在指向完整外框左上角；旋转、HRI 居中和旋转后的 HRI 预留页宽、显式静区占位及页面边界校验遵循该外框，内置左右静区即使未声明额外 `PrintQuietZone` 也会参与重叠校验。标准结构与尺寸参考 [ANSI/AIM BC6-2000 USS-49](https://expresscorp.com/wp-content/uploads/2023/02/USS-49.pdf)。本地测试验证主机生成字节，不代表真实打印质量或扫码验收。

### R123：ESC/POS Raster Code 49 主机编码

ESC/POS Raster 现在可在主机侧编码 Code 49：接受 7-bit ASCII，支持数字压缩、2–8 行、行/符号校验，并将 70 模块宽度的矩阵绘制为光栅数据。每行条高至少为 8 个模块，行间分隔条为 1X；页面预检、旋转、HRI 和模板输出沿用 Raster Provider 路径。

R123 引入了 Code 49 编码、内部行分隔和 Raster Provider 接入；当轮暂未覆盖的完整边界几何已在 R124 补齐。编码表参考 [libzint Code 49 实现](https://github.com/zint/zint/blob/master/backend/code49.c)。

### R122：跨协议常用压缩 AI 规范化

共享 GS1 解析器现在额外接受当前 Raster 已有编码路径使用的 `310x`/`320x`（六位数字重量）、`3920`–`3923`（非空数字价格）和 `3930`–`3933`（三位数字货币代码加非空数字价格）AI。ESC/POS Native GS1-128/DataBar Expanded、TSPL EAN128、ZPL GS1-128/DataBar Expanded 的括号输入会先统一转换为原始 AI；变长 `392x`/`393x` 字段在后续 AI 前插入 ASCII 29/FNC1，再分别编码为 Epson、TSPL 或 ZPL 的协议形式。

本轮只扩展已验证的压缩 AI 语法和长度边界，不在 Native/TSPL/ZPL 中模拟 Raster 的 Method 3–6 压缩位流，也不宣称已经支持其它压缩 AI、完整小数业务规则、链接标志或完整 GS1 字典。单元素历史括号输入继续保持旧兼容路径。

### R121：Native/TSPL/ZPL 共享括号 AI 规范化

已声明的 GS1 AI 子集现在可在 ESC/POS Native 的 GS1-128/DataBar Expanded、TSPL EAN128 和 ZPL GS1-128/DataBar Expanded 之间复用。输入包含至少两个 `(AI)value` 或 `[AI]value` 元素时，协议层先调用共享解析器，校验括号样式、AI 字段长度、数字/CSET 82 字符集，并在变长字段后续元素前插入 ASCII 29/FNC1；最后分别转换为 Epson `{1`、TSPL `!102` 或 ZPL `>8`。

单个历史括号展示值继续保留旧协议字节输出，以兼容原有固件输入；未知 AI、混合括号和字段错误只在多元素结构化输入中严格失败。该能力仍只覆盖当前声明的常用 AI 子集，不代表完整 GS1 字典、业务关联规则或厂商扩展。

### R120：ESC/POS Raster GS1 DataBar Expanded GTIN 加通用 AI 序列

ESC/POS Raster 现支持 `(01)GTIN` 或 `[01]GTIN` 后接常用括号 AI 序列，例如 `(10)AB(20)12`。解析器校验 GTIN-14、定长/变长字段和括号样式，并将变长字段在后续 AI 前规范化为 ASCII 29/FNC1；输出复用 Method 1 的既有通用字段状态机。`(310x)`、`(320x)`、`(392x)` 和 `(393x)` 首个压缩 AI 继续由原有 Method 3–14 路径处理。

未知 AI、GTIN 校验位错误、字段长度错误和括号样式混用在矩阵预算前失败。该入口扩展的是当前常用 AI 子集，不代表完整 GS1 业务序列或跨协议统一字典。

### R119：ESC/POS Raster GS1 DataBar Expanded 日期后的括号 AI 尾部

日期 Method 7–14 现在支持日期值后的常用括号 AI 尾部。例如 `(10)AB(20)12` 会规范化为 `10AB`、ASCII 29/FNC1、`2012`，并复用 R117 的通用字段状态机。圆括号和方括号都支持，HRI 保留完整的结构化输入文本；定长字段不插入 FNC1，变长字段在后续元素前插入 FNC1。

额外日期 AI、未知 AI、压缩重量/价格 AI、字段长度错误和混用括号在矩阵预算前严格失败。日期后的括号语义仍限于当前常用子集，不代表完整 GS1 业务关联规则；GS1 DataBar Expanded 的 AI Element String、FNC1 和补充 AI 选择参考 [GS1 General Specifications 24.0.0](https://ref.gs1.org/standards/genspecs/24.0.0/)。

### R118：ESC/POS Raster GS1 DataBar Expanded 括号 AI 到 FNC1 规范化

ESC/POS Raster 现支持当前声明的常用 GS1 AI 子集使用圆括号或方括号输入。解析器校验定长/变长字段、数字与 CSET 82 字符集，并在后续元素前为变长字段插入 ASCII 29/FNC1；定长字段直接拼接。括号和方括号输入会与等价的原始 FNC1 数据生成相同 Method 2 位流，Human Readable 保留用户的 AI 展示形式。

未知 AI、未闭合或混用括号、字段长度/字符集错误在矩阵预算前严格失败。当前子集不是完整 GS1 字典，也不实现链接标志、业务关联规则或括号化压缩 Method 3–14；完整 AI 语义以 [GS1 syntax dictionary](https://github.com/gs1/gs1-syntax-dictionary/blob/2026-01-27/gs1-syntax-dictionary.txt) 和 [GS1 General Specifications](https://ref.gs1.org/standards/genspecs/23.0.0/) 为准。模板预检同步允许方括号并按转义后的长度计算上限。

### R117：ESC/POS Raster GS1 DataBar Expanded 日期后的通用字段

ESC/POS Raster 现允许 Method 7/8、9–14 的日期压缩字段后继续追加原始通用字段。日期 AI 和六位日期仍使用结构化 `(11)/(13)/(15)/(17)` 或方括号形式，日期后的尾部从编码索引 34 开始按 Numeric、Alphanumeric、ISO/IEC 646 和 ASCII 29/FNC1 状态机编码；尾部不能以 FNC1 结束或包含连续 FNC1。HRI 会保留日期字段和通用尾部。

本轮只扩展日期后的原始通用字段，不把尾部括号解释为新的 AI 元数据；完整 AI 元数据解析、括号到 FNC1 的跨协议规范化、链接标志、Stacked/Composite 和 Code 49 仍严格拒绝。位流继续复用 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 的 Expanded 压缩字段和通用字段布局；本地 golden 不替代真实固件、DPI、纸宽和热敏/针式扫描验收。

### R116：ESC/POS Raster GS1 DataBar Expanded Method 2 FNC1 数据

ESC/POS Raster 在保留 GTIN-14 Method 1/3–14 路径的前提下，增加 Method 2 的原始 GS1 通用字段输入。输入必须以两位数字 AI 开头、使用 ASCII 29/FNC1 分隔变长字段，长度限制为 3–77 个字符；主机写入规范的 `0000` 方法字段，并复用 Numeric、Alphanumeric、ISO/IEC 646 和 FNC1 通用字段状态机、动态 check/finder、宽度测量、HRI、Raster 命令与模板 Provider。HRI 返回原始 FNC1 数据，绘制可读文字时仍移除控制分隔符。

Method 2 当前明确使用原始 FNC1 契约，不接受括号/方括号展示语法、末尾 FNC1 或连续 FNC1；旧的裸 14 位非 GTIN 输入继续严格拒绝。完整 AI 元数据解析、括号到 FNC1 的跨协议规范化、链接标志、Stacked/Composite 和 Code 49 仍未开放。方法字段和通用字段规则依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 与 [libzint general_field.c](https://raw.githubusercontent.com/zint/zint/master/backend/general_field.c)；本地 golden 不替代真实固件、DPI、纸宽和热敏/针式扫描验收。

### R115：ESC/POS Raster GS1 DataBar Expanded Method 9–14 日期字段

ESC/POS Raster 在保留 R114 Method 7/8 和既有 Expanded 压缩路径的前提下，现支持显式 `(310x)`/`[310x]` 或 `(320x)`/`[320x]` 后接日期 AI `(11)`、`(13)`、`(15)`、`(17)` 及六位日期。四组日期分别映射到 Method 7/8、9/10、11/12、13/14；主机写入对应 `0111000`–`0111111` 方法字段、20 位重量和 16 位日期值 `yy*384+(mm-1)*32+dd`。月范围为 `01-12`，日范围为 `00-31`，其中日 `00` 保留 GS1 的月末语义；HRI 重建原始重量 AI、日期 AI 和日期文本。

日期输入必须使用显式括号或方括号 AI，且本轮只接受单一日期字段，不接受日期后的通用字段。方法字段、日期值和 AI 映射参考 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)；本地测试不替代打印机固件、DPI、纸宽、HRI 字形和热敏/针式扫描验收。

### R114：ESC/POS Raster GS1 DataBar Expanded Method 7/8

ESC/POS Raster 在保留 R113 Method 5/6、R112 Method 4、R111 Method 3 和 R110 通用字段的前提下，现支持显式 `(310x)`/`[310x]` 与 `(320x)`/`[320x]` 的 Method 7/8 子范围。对没有日期后缀且重量为 `000000-099999` 的输入，`(3103)` 超过 Method 3 上限或其它 `(310x)` 选择 Method 7，`(3202)/(3203)` 超过 Method 4 上限或其它 `(320x)` 选择 Method 8。主机编码写入 `0111000`/`0111001` 方法字段、20 位重量和规范要求的固定日期值 `38400`，不把虚拟日期追加到 HRI；之后复用 check/finder、动态宽度和 `GS v 0` 位图路径。

Method 9–14、显式日期后缀、其它结构化 AI、链接标志、Stacked/Composite 和 Code 49 仍严格拒绝。方法位、重量字段和虚拟日期参考 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)；本地测试不替代打印机固件、DPI、纸宽、HRI 字形和热敏/针式扫描验收。

### R113：ESC/POS Raster GS1 DataBar Expanded Method 5/6

ESC/POS Raster 在保留 R112 Method 4、R111 Method 3、R110 Method 1 通用字段和既有 golden 的前提下，现支持 GS1 DataBar Expanded 的显式 Method 5/6 子范围。输入可使用 `(01)`/`[01]` 加合法 GTIN-14，再接 `(392x)`/`[392x]` 本地价格或 `(393x)`/`[393x]` 三位 ASCII ISO 4217 货币代码和价格，其中 `x` 为 `0-3`；Method 5 价格从 AI 后直接开始，Method 6 价格从三位货币代码后开始，价格必须为非空 ASCII 数字。主机编码分别写入 `01100xx`/`01101xx` 方法字段，Method 6 货币代码写入 10 位字段，再复用通用字段、check/finder、动态宽度和 `GS v 0` 位图路径。HRI 会保留对应 AI、货币代码和价格文本。

Method 7–14、其它结构化 AI、链接标志、Stacked/Composite 和 Code 49 仍严格拒绝。Method 5/6 的方法选择、字段起点和货币压缩参考 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)；本地测试不替代打印机固件、DPI、纸宽、HRI 字形和热敏/针式扫描验收。

### R112：ESC/POS Raster GS1 DataBar Expanded Method 4

ESC/POS Raster 在保留 R111 Method 3、R110 Method 1 通用字段和既有 golden 的前提下，现支持 GS1 DataBar Expanded 的显式 Method 4 子范围。输入可使用 `(01)`/`[01]` 加合法 GTIN-14，再接 `(3202)`/`[3202]` 或 `(3203)`/`[3203]` 和 6 位英制重量；`3202` 范围为 `000000-009999`，`3203` 范围为 `000000-022767`。主机编码固定 `0101` 方法字段，`3203` 重量按规范加 `10000` 后写入 15 位字段，再复用通用字段、check/finder、动态宽度和 `GS v 0` 位图路径。HRI 会保留对应 AI 和重量文本，裸数字后缀仍按既有 Method 1 通用字段处理。

Method 5/6、Method 7–14、其它 `(32xx)`/日期/价格 AI、链接标志、Stacked/Composite 和 Code 49 仍严格拒绝。方法选择、重量范围和 `3203` 偏移参考 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)；本地测试不替代打印机固件、DPI、纸宽、HRI 字形和热敏/针式扫描验收。

### R111：ESC/POS Raster GS1 DataBar Expanded Method 3

ESC/POS Raster 在保留 R110 Method 1 通用字段和既有数字 golden 的前提下，现支持 GS1 DataBar Expanded 的显式 Method 3 子范围。输入可使用 `(01)`/`[01]` 加 14 位合法 GTIN-14，再接 `(3103)`/`[3103]` 和 6 位公斤重量 `000000-032767`；主机编码固定 `0100` 方法字段和 15 位重量字段，然后复用通用字段、check/finder、动态宽度和 `GS v 0` 位图路径。HRI 会保留 `(01)`、`(3103)` 和重量文本，裸数字 `3103...` 仍按既有 Method 1 通用字段处理。

Method 4、其它 `(31xx)`/日期/价格 AI、链接标志、Stacked/Composite 和 Code 49 仍严格拒绝。方法选择和重量范围参考 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)；本地测试不替代打印机固件、DPI、纸宽、HRI 字形和热敏/针式扫描验收。

### R110：ESC/POS Raster GS1 DataBar Expanded 通用字段

ESC/POS Raster 在保留 R109 数字字段和 R108 五符号 134 模块 golden 的前提下，现支持 `BarcodeType.Gs1DataBarExpanded` Method 1 的 Numeric、Alphanumeric、ISO/IEC 646 通用字段状态机。输入可使用 `(01)`、`[01]` 或裸 `01` 加 14 位合法 GTIN-14，再接数字、A-Z、a-z、`*,-./`、ISO/IEC 646 标点、空格和 ASCII 29 FNC1；数字成对压缩、模式 latch、末位数字和 `00100` padding 均由主机编码。HRI 绘制前移除 FNC1 控制字符，宽度和矩阵预算按实际符号字符数计算。

不在当前编码表中的 `#`、`$`、末尾 FNC1、其它 AI/压缩方法、链接标志、Stacked/Composite 和 Code 49 仍严格拒绝。实现状态机参考 [libzint general_field.c](https://raw.githubusercontent.com/zint/zint/master/backend/general_field.c) 与 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)；目标固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描质量仍需设备验收。

### R109：ESC/POS Raster GS1 DataBar Expanded 数字通用字段

ESC/POS Raster 在保留 R108 五符号 GTIN-only 134 模块字节结果的前提下，扩展 `BarcodeType.Gs1DataBarExpanded` 的 Method 1 主机编码。输入可使用 `(01)`、`[01]` 或裸 `01` 加 14 位合法 GTIN-14，并可继续追加纯 ASCII 数字通用字段；数字字段按 7-bit 成对压缩，末位数字按规范选择 4/7-bit 编码，随后执行 Numeric terminator、`00100` padding、动态 check/finder 表和变量符号宽度。矩阵预算与宽度测量按实际符号字符数计算，HRI 会保留 `(01)` 和数字通用字段。

字母、ISO 646、ASCII 29/FNC1、其它 AI、链接标志、Stacked/Composite 仍严格拒绝；这些能力需要完整 CSET 82/ISO 编码和堆叠/复合布局后再开放。相关结构参考 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)、[general_field.c](https://raw.githubusercontent.com/zint/zint/master/backend/general_field.c) 和 [rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h)。

### R108：ESC/POS Raster GS1 DataBar Expanded GTIN-only

ESC/POS Raster 现支持统一模型的 `BarcodeType.Gs1DataBarExpanded` 严格 GTIN-only 主机编码。输入可使用 `(01)`、`[01]` 或裸 `01` 加 14 位 GTIN-14，主机验证数字和 GTIN 校验位后按 GS1 DataBar Expanded Method 1 生成四个数据字符、check character 和 finder pattern；五个符号字符展开为固定 134 模块，再输出 `GS v 0` 位图。宽度测量、HRI `(01)`、模块缩放、四方向旋转、模板页面边界、元素路径、能力快照、资源预算和失败恢复复用 Raster 条码路径。

该能力属于主机光栅输出，不改变 ESC/POS Native Function B 78、TSPL、ZPL 或 CPCL 的既有行为。Raster 当前明确拒绝其它 AI/通用字段、Composite、Stacked 和完整可变长度 Expanded 方法；完整 GS1 DataBar 输入契约仍待后续跨协议设计。编码流程和表格参考 [GS1 DataBar](https://www.gs1.org/standards/barcodes/databar)、[GS1 General Specifications](https://ref.gs1.org/standards/genspecs/11.0.0/)、[libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 和 [rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h)，目标固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描质量仍需设备验收。

### R107：ESC/POS Raster China Post（Datalogic 2 of 5）

ESC/POS Raster 现支持统一模型的 `BarcodeType.ChinaPost` 主机编码。输入严格限制为非空 ASCII 数字，数字字符沿用 Matrix 2 of 5；主机使用 Datalogic/China Post 的两窄条起始、宽条/窄条结束和字符间窄空白，并固定 3:1 宽窄比，输出 `GS v 0` 位图。宽度按 `10n+9` 模块计算，支持模块缩放、HRI、四方向旋转、模板页面边界和资源预算。

该能力属于主机光栅输出，不改变 ESC/POS Native、TSPL CPOST、ZPL 或 CPCL 的既有行为。TSPL 仍使用原生 `BARCODE "CPOST"` 并固定 1:1 窄宽参数；Raster 不追加可选校验位，也不声明目标固件的长度规则。结构参考 [TCPDF DatalogicTwoOfFive](https://tcpdf.org/docs/srcdoc/tc-lib-barcode/classes-Com-Tecnick-Barcode-Type-Linear-DatalogicTwoOfFive/)、[Seagull Barcode Guide](https://barcodeguide.seagullscientific.com/Content/Symbologies/Datalogic_2_of_5.htm)，宽窄比范围参考 [Seagull Datalogic options](https://help.seagullscientific.com/12.0/en/Content/symb_Datalogic2of5.htm)。目标固件、DPI、条高、纸宽、HRI 和扫描质量仍需热敏/针式设备验收。

### R106：ESC/POS Raster GS1 DataBar Limited

ESC/POS Raster 现支持统一模型的 `BarcodeType.Gs1DataBarLimited` 主机编码。输入严格限制为 13 位 ASCII 数字且首位为 0 或 1；主机按两组数据 pair、7 元素组合宽度、Mod-89 校验和 Annex C finder 表生成固定 79 模块位图，再进入 `GS v 0`。开启 HRI 时显示 `(01)`、payload 和 GTIN 校验位。宽度测量、模块缩放、四方向旋转、页面边界、模板元素路径、资源预算和失败恢复复用现有 Raster 路径。

该能力属于主机光栅输出，不改变 ESC/POS Native Function B 77、TSPL 或 ZPL 的既有行为；14 位含校验输入、AI `(01)` 前缀、复合链接字段以及 Expanded/Stacked/Composite/China Post/Code 49 仍严格按能力矩阵处理。算法表格参考 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)、[rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h)（BSD-3-Clause 归因），结构尺寸参考 [GS1 General Specifications](https://ref.gs1.org/standards/genspecs/23.0.0/)；目标固件、DPI、条高、纸宽、HRI 和扫描质量仍需热敏/针式设备验收。

### R105：ESC/POS Raster GS1 DataBar Omnidirectional/Truncated

ESC/POS Raster 现支持统一模型的 `BarcodeType.Gs1DataBarOmnidirectional` 与 `BarcodeType.Gs1DataBarTruncated` 主机编码。输入严格限制为 13 位 ASCII 数字 payload，主机按 GS1 DataBar/RSS-14 的四个数据字符、组合宽度、finder pattern 和 Mod-79 校验生成固定 96 模块的一维位图，再进入 `GS v 0`。开启 HRI 时显示隐含 `(01)`、13 位 payload 和 GTIN 校验位。两个变体共用横向编码结构，Raster 按调用方条高绘制；模板/Provider 继续按 Omnidirectional 33X、Truncated 13X 的最小高度做布局语义校验。

这属于主机光栅输出，不等价于 ESC/POS GS k 原生 DataBar；既有 Native Function B 75/76 能力不变。Raster 的 `Limited` 已在 R106 接入，`Expanded`、`Stacked/Composite`、`ChinaPost` 和 `Code49` 仍在能力预检阶段严格拒绝。数据字符和表格参考 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)、[rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h)（BSD-3-Clause 归因），结构尺寸参考 [GS1 General Specifications](https://ref.gs1.org/standards/genspecs/23.0.0/)；目标固件、DPI、条高合规、纸宽、HRI 和扫描质量仍需热敏/针式设备验收。

当前 ESC/POS Raster 条码能力矩阵在原有 Code 11、Code 128、Code128Auto、Code 39、Code 93、EAN/UPC、Codabar、I2OF5/I2OF5C/I2OF5G、MSI、Plessey、UCC/EAN-128、POSTNET、PLANET、FIM A–E、Intelligent Mail、ITF-14、Telepen Alpha 和 Telepenn Numeric 基础上，新增 GS1 DataBar Omnidirectional、Truncated、Limited、China Post 与 Expanded `(01)` 通用字段字符集子集、Method 3、Method 4、Method 5、Method 6、Method 7–14；Expanded 其它 AI/Method、链接标志、Stacked/Composite 和 Code 49 仍严格拒绝。

### R104：ESC/POS Raster Telepenn Numeric

ESC/POS Raster 现支持统一模型的 `BarcodeType.Telepenn` 主机编码。输入严格限制为 1–60 位 ASCII 数字，奇数长度前补零，每两位压缩为一个 Telepen Numeric glyph，按压缩 glyph 求 Mod-127 校验位，再输出起始 `_`、payload、校验和结束 `z`；每个符号展开为 16 个模块后输出 `GS v 0` 位图。宽度测量、旋转、HRI、页面边界、模板能力快照、资源预算和失败恢复复用 Telepen Alpha 的 Raster 路径。

该能力不等价于 ESC/POS GS k 原生 Telepenn；`SupportsCommandBarcodeType(BarcodeType.Telepenn)` 仍为 false。为保持现有统一 TSPL 语义，本项目不开放 Telepen Numeric 的 `X` 扩展。压缩规则参考 [libzint telepen.c](https://github.com/zint/zint/blob/master/backend/telepen.c)，真实固件、DPI、条高、纸宽、HRI 和扫描质量仍需目标热敏/针式设备验收。

### R103：ESC/POS Raster Telepen Alpha

ESC/POS Raster 现支持统一模型的 `BarcodeType.Telepen` 主机编码。输入严格限制为 1–30 个 7-bit ASCII 字符，主机按 Telepen Alpha 规则生成起始 `_`、payload、Mod-127 校验符和结束 `z`，每个符号展开为 16 个模块后输出 `GS v 0` 位图。宽度测量、旋转、HRI、页面边界、模板能力快照、资源预算和失败恢复复用同一 Raster 路径。

该能力不等价于 ESC/POS GS k 原生 Telepen；`SupportsCommandBarcodeType(BarcodeType.Telepen)` 仍为 false。`BarcodeType.Telepenn` 数值压缩模式已在 R104 接入 Raster，但不推断为 ESC/POS Native 原生能力。符号表参考 [libzint telepen.c](https://github.com/zint/zint/blob/master/backend/telepen.c)，真实固件、DPI、条高、纸宽、HRI 和扫描质量仍需目标热敏/针式设备验收。

### R102：ESC/POS Native profile 矩阵

ESC/POS Native 的 17 个 Function B 65–79 条码制式现有 profile 组合回归。完整 HRI 制式/位置 allowlist 可以逐项通过；`SupportsNativeBarcode=false`、不在 HRI 制式 allowlist 或不在 HRI 位置 allowlist 的模板会在输出前严格失败并保留元素路径。该验证补齐能力声明与模板预检的一致性，不替代具体打印机固件、HRI 字形、DPI 和扫描验收。

### R101：ESC/POS Raster Code 11 主机编码

ESC/POS Raster 现支持统一模型的 `BarcodeType.Code11`。主机接受 1–255 个 ASCII 数字或连字符，按 Code 11 规则生成 C 校验位；payload 超过 10 个字符时追加 K 校验位，再以完整起止星号和窄空白模块串生成 `GS v 0` 位图。Raster 的宽度测量、模块缩放、旋转、HRI、页面边界、能力快照、模板 Provider、资源预算和失败恢复沿现有主机条码路径处理。

该能力是主机光栅输出，`SupportsCommandBarcodeType(BarcodeType.Code11)` 在 ESC/POS Raster 模式保持 false；ESC/POS Native 仍严格拒绝 Code 11。TSPL/ZPL 的原生 Code 11 输出不受影响。Code 11 的字符模式和校验语义参考 [Code 11](https://en.wikipedia.org/wiki/Code_11)，真实固件、DPI、条高、纸宽、HRI 字形和扫描质量仍需目标热敏/针式设备验收。

### R100：ESC/POS Native 数值边界证据

Native Function B 65–79 的直接命令现在有端点矩阵回归：17 个统一条码类型分别验证模块宽度 2/6、条高 1/255、HRI 位置 0/1/2/3 的合法字节组合，以及非法数值不污染已有命令。该轮只锁定 `CommandBarcodeModuleWidth=2..6`、`CommandBarcodeHeight=1..255` 与实际校验一致，不改变 Native 输出；静区、旋转、逐制式 payload 及设备固件组合仍需后续分层验证和实机验收。

### R99：ESC/POS Raster Plessey

ESC/POS Raster 现支持统一模型的 `BarcodeType.Plessey`。主机使用 ZXing.Net 0.16.11 的 `PlesseyWriter` 生成完整 Plessey 位序，裁剪编码器自带空白边界后按统一模块宽度绘制 `GS v 0` 位图；宽度测量、旋转、HRI、页面边界、资源预算和失败恢复沿现有 Raster 条码路径处理。输入严格限制为非空 ASCII 数字，与 TSPL/ZPL 统一模型保持一致。

`EscPosPrintCommand.RasterBarcode` 和模板 Provider 共用该路径；Raster 的能力快照会开放 `Plessey`，但仍按主机光栅条码约定不报告 ESC/POS GS k 原生 Plessey 命令能力。真实打印机的 Plessey 扫描、DPI、条高、纸宽和 HRI 墨迹仍需设备验收。

当前 ESC/POS Raster 条码能力矩阵包含 Code 11、Code 128、Code128Auto、Code 39、Code 93、EAN/UPC、Codabar、I2OF5/I2OF5C/I2OF5G、MSI、Plessey、UCC/EAN-128、POSTNET、PLANET、FIM A–E、Intelligent Mail、ITF-14、Telepen Alpha 和 Telepenn Numeric；GS1 DataBar、China Post、Code 49 等没有可靠主机编码器的变体继续严格拒绝。

### R98：ESC/POS Raster ITF-14

ESC/POS Raster 现支持统一模型的 `BarcodeType.Itf14`。主机侧接受恰好 13 位数字 payload，按 Mod10 生成第 14 位校验位，再使用 ITF 矩阵生成 `GS v 0` 位图；宽度、HRI、旋转、页面边界、预算和失败恢复沿现有 Raster 条码路径执行。Raster 输出不伪造 ITF-14 bearer bars，真实条高、静区和扫描质量仍需目标设备验收。TSPL 原生 `BARCODE "ITF14"` 语义保持不变，其他协议继续严格按能力矩阵处理。

FIM `A`–`E` 五种 USPS 模式已由 Raster 主机编码支持；历史说明中把 `D/E` 写成拒绝的段落仅保留为历史记录，不代表当前能力。

当前 ESC/POS Raster 条码能力矩阵包含 Code 128、Code128Auto、Code 39、Code 93、EAN/UPC、Codabar、I2OF5/I2OF5C/I2OF5G、UCC/EAN-128、MSI、POSTNET、PLANET、FIM A–E、Intelligent Mail 和 ITF-14；GS1 DataBar 及其他没有可靠主机编码器的变体仍严格拒绝。

### R97：ESC/POS Raster Code128 auto

ESC/POS Raster 现支持统一模型的 `BarcodeType.Code128Auto`。主机使用与 Code 128 相同的 ZXing 自动分组矩阵绘制位图，宽度、旋转、HRI 预留、页面边界和资源预算沿现有 Raster 条码路径处理；输入严格限制为 1–255 个 7-bit ASCII 字符。该能力属于主机光栅输出，`SupportsCommandBarcodeType(BarcodeType.Code128Auto)` 在 Raster 模式仍为 false；需要 ESC/POS Function B 79 的设备原生字节时使用 Native `BarcodeCode128Auto` 入口。任意 8-bit 原始数据不通过 Raster 字符串入口推断，继续保持 Native `byte[]` API 的边界。

当前能力列表中的 Raster 条码集合应将 `Code128Auto` 与 Code 128 并列理解；后文旧版“Raster 支持基础 I2OF5”的概括只描述早期子集，不覆盖本轮新增能力。

### R60 当前能力修正：ZPL GS1 DataBar

ZPL 统一模型现支持 `BarcodeType.Gs1DataBarOmnidirectional`、`Gs1DataBarTruncated`、`Gs1DataBarLimited` 和 `Gs1DataBarExpanded`，分别映射到 Zebra 原生 `^BR` 类型 1、2、5、6。`ZplPrintCommand` 提供四种命名入口，支持 N/R/I/B 四方向、1–10 点放大倍数、1–32000 点条高；Expanded 额外支持 2–22 的偶数分段宽度。统一模型沿用 DataBar 数字制式的 13 位输入和 Limited 首位 0/1 约束，Expanded 接受 2–255 个安全嵌入 ZPL 字段的字符并严格拒绝控制/字段分隔冲突。

ZPL `^BR` 标准参数没有普通一维条码的 HRI 开关，因此 Provider 对显式 `HumanReadable=true` 返回能力错误；需要可读内容时应追加独立文字元素，未指定值保留设备原生默认。模板能力快照、宽度/最小高度保守预检、元素路径错误和失败恢复沿同一 Provider 路径处理。参数依据 Zebra [`^BR`](https://docs.zebra.com/us/en/printers/software/zpl-pg/zpl-commands/%5Ebr.html)；设备固件、GS1 AI/FNC1 业务语义、堆叠布局、DPI、纸宽、打印和扫描仍需目标 Zebra 设备验收。CPCL、TSPL 和 ESC/POS Raster 对这些类型保持严格拒绝，ESC/POS Native 的既有 Function B 75–78 能力不变。

### R58 当前能力修正：TSPL/ZPL 文字样式显式位图降级

TSPL 与 ZPL 原生 `TEXT` 默认不宣称统一的粗体、下划线或旋转下划线能力，模板和 Provider 会严格报错。显式设置 `TsplMediaOptions.AllowRasterizedTextStyles` 或 `ZplMediaOptions.AllowRasterizedTextStyles` 后，主机字体会被渲染为单色位图，分别使用 TSPL `BITMAP` 和 ZPL `^GFA` 输出，支持 0/90/180/270 度旋转；该选项也可在 Designer 的 TSPL/ZPL 介质设置中保存。位图降级要求 Profile 允许图片，且不等价于打印机固件内置字体效果；默认关闭以避免静默改变字体语义。

### R56 当前能力修正：ESC/POS Native Function B 75–79

R57 字符契约修正：GS1 DataBar Expanded 官方允许的裸 `{` 原样保留；逻辑括号和 ASCII 29 FNC1 仍按 GS k 78 转义为 `{(`、`{)`、`{1`。

ESC/POS Native 现支持 Epson GS k Function B 的 GS1 DataBar Omnidirectional、Truncated、Limited、Expanded（75–78）以及 Code128 auto（79）。新增 BarcodeType.Gs1DataBarOmnidirectional、Gs1DataBarTruncated、Gs1DataBarLimited、Gs1DataBarExpanded、Code128Auto，独立命令提供命名入口；统一模板 Provider 使用同一条码映射、输入校验、页面占用预检和失败恢复路径。75–77 严格要求 13 位数字，Limited 首位为 0 或 1；Expanded 接受 Epson 声明的字符集，括号和 ASCII 29 FNC1 在主机侧按 GS k 78 规则转义；Code128 auto 额外提供原始 byte[] 入口以保留 0–255 字节。

新类型只开放给 ESC/POS Native，Raster、CPCL、TSPL、ZPL 仍严格拒绝。Epson 参考还声明 75/76/77/78 的设备最小条高分别为 33/13/10/34 倍模块宽度，Provider 按保守模块数和最小高度做页面边界检查；具体机型是否实现这些 Function B 类型、HRI、纸宽、DPI 和扫描结果仍需外部设备验收。命令格式和数据范围依据 [Epson GS k 参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_lk.html)。

### R55/R134 当前能力：ZPL Code 49

ZPL 统一模型支持 `BarcodeType.Code49`，映射到 Zebra 原生 `^B4`。数据为 1–81 个可安全嵌入 ZPL 字段的 ASCII 字符，单行目标条高为 1–32000 点，模块宽度为 1–10 点，旋转为 N/R/I/B；可读文字使用 `f=B`，起始模式默认为 `A`，也可指定 `A` 或 `0`–`5`。R55 初版宽度估算使用 49 模块并直接把点数作为 `^B4` 高度参数；R134 已修正为 70 模块行宽，并按 `^B4` 的 `h` 行高倍率换算目标点数，页面高度按本地编码器的堆叠行数和保守分隔预留估算。设备固件仍决定实际堆叠和 HRI footprint。`^`、`~`、引号和控制字符明确拒绝，不静默启用 `^FH` 转义。参数依据 [Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)；实际固件、HRI、DPI、纸宽、打印和扫描仍需目标设备验收。CPCL、ESC/POS 对该类型保持严格拒绝。

### R54 当前能力修正：ZPL Plessey

ZPL 统一模型现支持 `BarcodeType.Plessey`，映射到 Zebra 原生 `^BP`。`ZplPrintCommand.BarcodePlessey`、Provider、模板能力快照和页面边界预检使用同一契约：非空 ASCII 数字、1–32000 点条高、1–10 点窄条宽度，旋转为 N/R/I/B，`e=N` 不重复请求设备打印校验位；`^BY` 默认 3:1，符合 Plessey 2:1–3:1 比例范围。输入、Profile 能力和页面占用在写入前严格检查，失败不会追加动作。参数依据 [Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)；实际固件、HRI、DPI、纸宽、打印和扫描仍需目标设备验收。ZPL 其他未映射条码继续严格拒绝。

### R53 当前能力修正：ZPL Code 11

ZPL 统一模型现支持 `BarcodeType.Code11`，映射到 Zebra 原生 `^B1`。`ZplPrintCommand.BarcodeCode11`、Provider、模板能力快照和页面边界预检使用同一契约：数据为非空 ASCII 数字/连字符，条高为 1–32000 点，窄条宽度为 1–10 点，旋转为 N/R/I/B，设备使用 `e=N` 生成两个 Modulo-11 校验位。输入、Profile 能力和页面占用在写入前严格检查，失败不会追加动作。参数依据 [Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)；实际固件、HRI、DPI、纸宽、打印和扫描仍需目标设备验收。ZPL Code 49 已在 R55 接入，其他未映射条码继续严格拒绝。

### R52 当前能力修正：TSPL 条码比例编辑器

Designer 对 `TELEPEN`、`TELEPENN` 和 `CODE49` 提供 TSPL 专用五档窄条:宽条比例选择：`1:1`、`1:2`、`1:3`、`2:5`、`3:7`。选择后按当前设备 `barcodeModuleWidth` 能力自动写入合法整数 `NarrowModuleWidth`/`WideModuleWidth`；超出范围的比例禁用，手动输入不合法组合时保留原值并在预检阶段报告。Provider 的严格校验和旧 `Ratio` 兼容语义保持不变。

### R51 当前能力修正：TSPL Code 49

TSPL 统一模型现支持 `BarcodeType.Code49`，映射到 TSC 原生 `BARCODE "CODE49"`。`TsplPrintCommand.BarcodeCode49`、通用 `Barcode` 入口、Provider、模板能力快照和四方向页面边界使用同一契约：CODE49 接受 1–81 个可安全嵌入 TSPL 字段的 ASCII 字符，窄宽比限制为 1:1、1:2、1:3、2:5 或 3:7。NUL、ESC、CR/LF、引号等会破坏 TSPL `BARCODE` framing 的字符明确拒绝；宽度按数据长度加起止槽位和 49 单元/内容符号保守预留。Code 49 的堆叠行数、最终模块结构和扫描结果由目标固件决定。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf)，TSC 手册标注 CODE49 自 V6.89EZ 起可用，实际固件、HRI、条高、DPI、纸宽和扫描结果仍需目标设备验收。CPCL、ESC/POS 对该类型保持严格拒绝，ZPL 已通过 `^B4` 支持。

### R50 当前能力修正：TSPL Telepen

TSPL 统一模型现支持 `BarcodeType.Telepen` 和 `BarcodeType.Telepenn`，分别映射到 TSC 原生 `BARCODE "TELEPEN"` 与 `"TELEPENN"`。`TsplPrintCommand.BarcodeTelepen`/`BarcodeTelepenn`、通用 `Barcode` 入口、Provider、模板能力快照、四方向页面边界和 Designer 选项使用同一契约：TELEPEN 接受 1–30 个可安全嵌入 TSPL 字段的 ASCII 字符，TELEPENN 接受 1–60 位 ASCII 数字，窄宽比限制为 1:1、1:2、1:3、2:5 或 3:7。NUL、ESC、CR/LF、引号等会破坏 TSPL `BARCODE` framing 的字符明确拒绝；宽度按 16 单元/符号及起止符保守预留。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf)，TSC 手册标注两类条码自 V6.89EZ 起可用，实际固件、HRI、条高、DPI、纸宽和扫描结果仍需目标设备验收。CPCL、ESC/POS 和 ZPL 对这两个类型保持严格拒绝。

### R49 当前能力修正：TSPL China Post

TSPL 统一模型现支持 `BarcodeType.ChinaPost`，映射到 TSC 原生 `BARCODE "CPOST"`。`TsplPrintCommand.BarcodeChinaPost`、通用 `Barcode` 入口和模板 Provider 只接受非空 ASCII 数字，固定使用 1:1 窄宽比，不由主机追加校验位；Provider 按数据符号和起止槽位保守计算宽度，支持四方向、页面边界、Profile 禁用和失败恢复。Designer 选择器值为 `21`，省略 `WideModuleWidth` 时自动收敛为与窄条相同的点宽。CPOST 的固件可用性、最终编码、条高、DPI、纸宽和扫描结果仍需目标 TSC 设备验收。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf)；CPCL、ESC/POS Native 和 ZPL 对该类型保持严格拒绝，ESC/POS Raster 已在 R107 接入主机编码。

### R37 当前能力修正：TSPL EAN14

TSPL 统一模型现支持 `BarcodeType.Ean14`，映射到 TSC 原生 `BARCODE "EAN14"`。`TsplPrintCommand.BarcodeEan14` 和 Provider 接受恰好 13 位 ASCII 数字 payload，严格要求手册声明的 1:1 窄宽比，默认命令入口使用推荐的 8 点窄条宽度，并按完整 GS1-128 符号预留宽度，设备负责最终校验位。能力快照、通用原始类型入口、四方向、原点/占用宽度、页边界、模板元素路径、Profile 禁用、比例校验和失败恢复均已接入。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)；EAN-14 在 GS1 中通常承载 GTIN-14/AI `01` 语义，业务字段解析和真实打印扫描仍需调用方与目标设备确认。CPCL、ESC/POS 和 ZPL 当前对 EAN14 保持严格拒绝。

### R36 当前能力修正

TSPL 统一模型现支持 `BarcodeType.Itf14`，映射到 TSC 原生 `BARCODE "ITF14"`。`TsplPrintCommand.BarcodeItf14` 和 Provider 接受恰好 13 位 ASCII 数字 payload，按完整 14 位 ITF-14 符号预留宽度，设备负责最终校验位。能力快照、通用原始类型入口、四方向、原点/占用宽度、页边界、模板元素路径、Profile 禁用和失败恢复均已接入。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)；CPCL、ESC/POS 和 ZPL 当前对 ITF14 保持严格拒绝。


### R35 当前能力修正

TSPL 统一模型现支持 `BarcodeType.Plessey`，映射到 TSC 原生 `BARCODE "PLESSEY"`。`TsplPrintCommand.BarcodePlessey` 和 Provider 接受非空 ASCII 数字，严格要求窄条:宽条为 1:2；设备负责最终条形编码和校验。能力快照、通用原始类型入口、四方向、原点/占用宽度、页边界、模板元素路径、Profile 禁用和失败恢复均已接入。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)；真实打印、扫描和固件差异仍需设备验收。CPCL 与 ESC/POS Native 保持严格拒绝，ESC/POS Raster 已由 R99 增加主机光栅路径，ZPL 使用原生 `^BP`。

### R34 当前能力修正

TSPL 统一模型现支持 `BarcodeType.Postnet`，映射到 TSC 原生 `BARCODE "POST"`。`TsplPrintCommand.BarcodePostnet` 和 Provider 接受 5、9 或 11 位 ASCII 邮政数字，移除连字符后发送数据位；TSPL 设备负责生成 POSTNET 最终校验位。能力快照、`POST` 原始入口、四方向、原点/占用宽度、页边界、模板元素路径、Profile 禁用和失败恢复均已接入。定向及跨协议回归 net6.0/netcoreapp3.1 各 `61/61`，Core 全量回归三种可用运行时各 `2336/2336`，Core/Templates 五个生产目标框架构建 0 warnings、0 errors。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)；真实打印、扫描和固件差异仍需设备验收。

### R33 当前能力修正

TSPL 统一模型现支持 `BarcodeType.I2OF5G`。TSC TSPL 手册将 `DPI` 定义为 11 位 Deutsche Post Identcode、`DPL` 定义为 13 位 Deutsche Post Leitcode；`TsplPrintCommand.BarcodeI2OF5G` 在主机侧按统一 German Post 4/9 规则生成或验证 12/14 位完整值，再按长度选择 `DPI`/`DPL` 并发送 11/13 位 payload。Provider、模板、能力快照、N/R/I/B 方向、页边界、失败恢复和 Profile 禁用均已接入；实际固件生成的校验位、打印和扫描仍需设备验收。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。

### R32 当前能力修正

ZPL 统一模型现支持 `BarcodeType.I2OF5G`。ZPL 原生 `^B2` 只表达基础 Interleaved 2 of 5，因此 `ZplPrintCommand.BarcodeI2OF5G` 在主机侧按 CPCL German Post 4/9 规则为 11/13 位输入生成校验位、为 12/14 位输入校验既有校验位，然后以关闭 ZPL Mod10 的 `^B2` 输出完整数据。支持 N/R/I/B 方向、Provider/模板路径、页边界、失败恢复和设备 Profile。Zebra 官方资料将 `^B2` 列为 ZPL II 的 Interleaved 2 of 5 命令，见 [ZPL 条码说明](https://docs.zebra.com/us/en/printers/software/zpl-pg/fonts-and-barcodes/barcodes.html)。该映射不代表目标固件原生理解 German Post 4/9，实际扫描仍需设备验收。

### R31 当前能力修正

TSPL 统一模型现支持 `BarcodeType.UccEan128`，映射到 TSC 原生 `BARCODE "EAN128"`。输入以数字应用标识开头，ASCII 29 分隔符转换为 TSPL `!102` FNC1 控制码；TSPL 的 `EAN128` 类型负责 GS1-128 的首个 FNC1。为避免控制码歧义，字面 `!` 严格拒绝。独立 `TsplPrintCommand.BarcodeUccEan128`、Provider、模板能力快照、四方向、页边界和失败恢复已接入。语义依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)；AI 业务校验、设备固件支持和真实扫描仍需调用方与目标设备确认。

### R28 当前能力修正

ESC/POS Raster 已支持统一模型的 Intelligent Mail/IMb。主机按 USPS-B-3200 生成 65 根四状态条，接受 20 位跟踪码以及 0/5/9/11 位路由码组合；输入允许连字符分组，归一化后必须为 20/25/29/31 位 ASCII 数字，不追加校验位。宽度测量、HRI、四角旋转、模板静区、页边界和预算恢复沿同一 Raster 路径处理。ESC/POS Native 继续严格拒绝 IMb；FIM D/E 和厂商邮政变体也继续严格拒绝。规范依据 [USPS-B-3200 Intelligent Mail Barcode](https://postalpro.usps.com/storages/2017-08/2190_USPSB3200IntelligentMailBarcode4State_0.pdf)，MID、STID、条高/DPI 和实际扫描结果仍需调用方与目标设备确认。

### R29 当前能力修正

统一 `BarcodeType.Fim` 现接受 A、B、C、D、E 五种 USPS FIM 模式。ESC/POS Raster 主机按 USPS DMM 202 生成 D=`111010111`、E=`101000101` 的九槽位图形；模板度量、页边界、旋转、HRI 和预算恢复与 A/B/C 共用同一路径。CPCL 模板也接受 D/E 输入并保留原有 `BARCODE FIM` 输出；ESC/POS Native 仍无 FIM 原生命令。模式定义依据 [USPS DMM 202](https://pe.usps.com/cpim/ftp/manuals/DMM300/202.pdf)，实际条高、条宽、位置、DPI 和业务适用场景仍需目标设备验收。

### R30 当前能力修正

ZPL 统一模型现支持 `BarcodeType.UccEan128`。Provider 和独立 `ZplPrintCommand.BarcodeUccEan128` 使用 `^BC`：输出前缀 `>;>8` 选择 Code 128 子集 C 并插入首个 FNC1；输入中的 ASCII 29 分隔符转换为后续 `>8`，字面 `>` 转换为 `>0`，避免被误解析为 ZPL 调用码。支持 N/R/I/B 四方向、可读文字、模板能力快照、尺寸边界、严格输入校验和失败恢复。语义依据 Zebra 官方 [`^BC`](https://docs.zebra.com/us/en/printers/software/zpl-pg/c-zpl-zpl-commands/r-zpl-bc.html) 文档；应用标识、分隔结构、业务校验和真实扫描仍由调用方及目标设备确认。TSPL、其他尚未映射的 GS1 变体继续严格拒绝。

`Bing.EasyPrint` 保留现有 CPCL API，并提供独立的 ESC/POS、TSPL 和 ZPL 命令构建器。命令构建器只负责生成打印机字节，不负责连接端口或提交打印任务。

```csharp
var receipt = new DefaultEasyPrint().CreateEscPosCommand()
    .Initialize()
    .Text("Hello")
    .Feed(2)
    .Cut()
    .Build()
    .GetBytes();

var label = new DefaultEasyPrint().CreateTsplCommand()
    .Size(60, 40)
    .Text(20, 20, "Hello")
    .Print()
    .Build()
    .GetBytes();

var zpl = new DefaultEasyPrint().CreateZplCommand()
    .Start()
    .Label(400, 300)
    .Text(20, 20, "Hello")
    .End()
    .Build()
    .GetBytes();
```

ESC/POS 支持 `Native` 和 `Raster` 两种构造模式。`Native` 适合顺序文字、进纸、裁切和原生条码/二维码；`WriteText`、`Tab`、`NewLine` 可以组合制表行，它不提供通用的绝对坐标绘图。`Raster` 会先在主机端合成整页单色位图，再通过 ESC/POS 光栅命令输出。

统一 `PrintDocument` 的 ESC/POS Native Provider 支持原点顺序文字、Code 128、GS1-128（UCC/EAN-128）、Code 39、Code 93、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、I2OF5C、I2OF5G 和二维码；Raster 在主机端光栅化 Code 128、GS1-128（UCC/EAN-128）、Code 39、Code 93、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、I2OF5C、I2OF5G、MSI、POSTNET、PLANET 和 FIM A/B/C；Intelligent Mail/IMb、FIM D/E 等仍严格拒绝。GS1-128 Native 使用 Epson Function B `GS k 74`，Raster 使用 ZXing `GS1_FORMAT`；统一模板沿用以数字应用标识开头的 GS1 元素串，ASCII 29 表示 FNC1，独立 `EscPosPrintCommand.BarcodeUccEan128` 仍接受 2–255 字节 7-bit ASCII。AI、分隔符和可选校验标记由调用方按目标固件约定提供，设备补齐起始符、FNC1、校验和停止符。I2OF5C 使用 CPCL 的 Mod10 规则（权重 3/1）补齐校验位，I2OF5G 使用 German Post Code 的 Mod10 规则（权重 4/9）补齐或校验校验位，最终均复用 ESC/POS 的 ITF `GS k 70`。需要坐标、线框或图片时显式选择 Raster。TSPL 统一模板还支持原生 `BARCODE` 类型 `25`/`25C` 的 I2OF5/I2OF5C；25 要求偶数数字，25C 要求奇数数字并由设备生成校验位，统一输入的另一种奇偶长度会补前导 `0`，规则依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。TSPL `BITMAP` 在命令头后写原始二进制位图；十六进制字符串重载会先解码，设备收到的仍是二进制数据。TSPL/ZPL 的水平和垂直线使用原生命令；斜线和由斜线组成的图标、椭圆需要显式设置介质选项的 `AllowRasterizedLines = true`，否则会报错。

`PrintPage.Add(element, sourcePath)` 为每次追加创建独立的归属副本，不修改传入元素。相同元素可以在多个页面、文档或同一页多次复用，各处 `SourcePath` 独立；页面 `Elements` 中的对象与输入对象不保证引用相同。标准元素的打印数据不可变；自定义元素的额外引用状态需由实现者保持不可变。`PrintImageElement` 在构造时复制输入位图，`Raster` 每次返回副本，修改这些数组不会改变后续输出。页面份数、进纸和切纸设置仍可显式修改；渲染期间不得修改文档结构或页面设置。

ESC/POS Raster 对 FIM D/E 和厂商邮政变体继续严格拒绝；GS1-128、MSI、POSTNET、PLANET、FIM A/B/C 和 Intelligent Mail/IMb 已提供主机光栅路径，其他协议仍按各自能力矩阵处理。

TSPL `Size(width, height, unit)` 使用 `SIZE width unit,height unit` 格式，随后按文本编码写入 `CODEPAGE` 和 `CLS`。当前映射为 GBK/936、UTF-8、ASCII/USA；其他编码在设置标签尺寸时明确报错。直接使用默认命令时编码为 GBK，需要调用方注册 `CodePagesEncodingProvider`。

ZPL 使用 UTF-8 编码时会在 `^XA` 后写入 `^CI28`；模板选择 ZPL 且未显式设置编码时默认使用 UTF-8。打印机仍需具备相应字符的字体字形。
ZPL 目前只接受 UTF-8 或 ASCII；其他 .NET 编码没有对应的设备字符集映射，会在 `Start()` 时失败。
ZPL 可通过 `ZplMediaOptions` 设置 `^PR` 打印速度、`^PO` 页面方向、`^MN` 介质跟踪和 `^MD` 打印浓度；这些设置会在 `Label` 中按选项快照输出，并在写入前校验范围。

Provider 的 `PrintCapabilities.SupportsEncoding` 可用于渲染前预检：TSPL 明确支持 ASCII、UTF-8 和代码页 936，ZPL 支持 ASCII 和 UTF-8。ESC/POS 命令和 Provider 要求传入编码对 ASCII 字符以及 `CR/LF` 保持单字节表达，也就是编码 `A\r\n` 后必须得到 `0x41 0x0D 0x0A`；ASCII 和 UTF-8 满足此条件，UTF-16、UTF-32 等宽字节编码会在构造时明确拒绝。该约束用于保护 ESC/POS 的控制序列和换行边界，不代表设备一定支持对应字符集。

ESC/POS 的代码页编号仍由具体设备型号决定。未配置文字契约时，`EscPosDeviceOptions.CodePageIndex` 保留既有手工 `ESC t n` 行为，`SupportsEncoding` 只校验主机侧字节布局。配置 `EscPosPrinterProfile.Text` 后，Native 会进一步检查主机编码、设备索引和声明字库的一致性，不推断品牌或固件映射。
`PrintCapabilities` 还分别描述文字旋转、二维码旋转、条码旋转、填充矩形、页级份数和额外进纸。TSPL/ZPL 的二维码命令支持 0/90/180/270 度；ZPL 的 Code 128、Code 39、Code 93、Code 11、Codabar、I2OF5、MSI、POSTNET、PLANET、Intelligent Mail 和 EAN/UPC 原生条码也支持四个方向；ESC/POS Raster 的文字、上述主机条码和二维码支持 0/90/180/270 度，独立命令可在 Raster 页直接绘制带字号、样式和角度的文字、条码及二维码；ESC/POS Native 仍严格拒绝坐标旋转，I2OF5C/I2OF5G 继续在 Native 顺序布局中使用，GS1-128 同时可在 Raster 由主机编码；Provider 会在编码前完成输入或校验位规范化；CPCL 的显式静区组合按能力校验。Raster 旋转文字和码图的静区联合检查使用旋转后的占位范围，条码可读文字预留区会随条码一起旋转。`IPrintProtocolProvider.Check(document)` 返回是否通过及首个问题的路径、原因和能力/数据分类；原有 `Validate(document)` 仍在失败时抛出明确异常。

ESC/POS 的 `Initialize()` 只输出打印机初始化指令。字符列数取决于具体设备的字体与纸宽，`Initialize(columns)` 因此会明确报错。需要设置设备打印区域宽度时使用 `SetPrintAreaWidth(widthUnits)`，参数单位为打印机的水平运动单位，不是字符列数。Native 模板若需要同一行的制表列，可设置 `EscPosDeviceOptions.NativeTabWidthDots` 为实测的相邻默认制表位逻辑点距；仅接受按此点距对齐、互不重叠的 ASCII 列文字，未配置时仍拒绝非零 X 坐标。该选项只校准布局，不向设备重设制表位。

需要声明设备代码页时，可传入 `EscPosDeviceOptions` 的 `CodePageIndex`；命令会在初始化后写入 `ESC t n`。索引由具体机型定义，应与传入的 .NET `Encoding` 一起按设备手册配置。`PrintAreaWidthUnits` 会在初始化后写入 `GS W`。两项均未指定时，初始化字节保持原样。模板可通过 `TemplateRenderOptions.EscPosDevice` 使用这些设置。

### ESC/POS 原生文字契约

`EscPosPrinterProfile.Text` 是可选的 `EscPosTextProfile`。它声明当前默认设备字库的 .NET `EncodingCodePage`、可选设备 `CodePageIndex`、`CharacterMode` 和非空 `CharacterRanges`。字符区间使用 Unicode 标量的闭区间，不能包含 UTF-16 代理项；字库声明不等于自动探测或设备验收。

```csharp
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var profile = new EscPosPrinterProfile
{
    Text = new EscPosTextProfile
    {
        EncodingCodePage = 936,
        CharacterMode = EscPosCharacterMode.FsKanji,
        CharacterRanges = new[]
        {
            new PrintCharacterRange(0x20, 0x7e),
            new PrintCharacterRange(0x4e00, 0x9fff)
        },
        HalfCellWidthDots = 12,
        FullCellWidthDots = 24,
        LineHeightDots = 26
    }
};
var bytes = new EscPosPrintCommand(Encoding.GetEncoding(936), EscPosLayoutMode.Native,
    new EscPosDeviceOptions { Profile = profile }).Initialize().Text("中文 ABC").Build().GetBytes();
```

此示例需用目标设备确认过的字库区间和字距替换。`DeviceDefault` 不追加字符模式指令；`SingleByte` 追加 `FS .`，只接受单字节编码；`FsKanji` 追加 `FS &`，当前接受显式声明的 GBK、GB18030、Big5 或韩文代码页。UTF-8 设备须使用经确认的默认字符模式；日文代码系统和厂商私有模式需另建指令契约。命令语义依据 Epson 的 [FS & 参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/fs_ampersand.html) 与 [FS . 参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/fs_period.html)，具体模式仍受机型限制。

初始化顺序为 `ESC @`、可选 `ESC t n`、可选字符模式、可选 `GS W`。显式旧 `CodePageIndex` 与文字契约冲突时拒绝；绑定契约后不能通过 `SelectCodePage` 或 `SetCharacterMode` 切到未声明状态。`Text` / `WriteText` 在追加前检查编码、字库覆盖和非法 Unicode；失败不会污染已记录输出。受信任的 `WriteRaw` / `WriteRawBytes` 仍是显式绕过结构化文字检查的出口。

Native 模板和文档使用声明的半角/全角字宽，模板文字字形逻辑高度仍为 24 点，行推进可声明 24 或 26 点。这些值是初始化后设备默认字体/行距的实测契约，不会向打印机重设字库、字体或行距；渲染器拒绝不能按声明整行推进的布局。`NativeTextProfile` 能力查询返回独立副本。未指定文字契约时旧默认布局和字节保持。

Raster 的主机文字不受 Native 编码或字库范围限制，直接调用原生 `Text` / `WriteText` 仍检查契约。Raster 度量和绘制会检查实际字体样式的每个 Unicode 标量字形，缺字时明确报错；未配置固定字体时，默认字体选择只使用能覆盖整段文字的系统字体，显式系统字体不静默改用其他字体。布局换行/制表不要求可见字形。

Host/设计器的显式 `encoding: "Device"` 仅用于声明了文字契约的 ESC/POS Native，按 `EncodingCodePage` 选择主机编码。未选择编码时模板仍默认 GBK，独立 ESC/POS 命令仍默认 GB18030；契约不会静默改写这些默认值。

### Raster 固定字体与显式替代

`EscPosDeviceOptions.RasterFonts` 接受不可变的 `RasterFontCatalog`，仅用于 ESC/POS Raster。每个 `RasterFontResource` 包含资源别名、调用方声明的版本、常规字体字节及可选的同族粗体字节。加载单个 TTF/OTF 文件，不自动读取系统字体、访问文件路径或安装字体；使用字体前应确认其嵌入及分发许可。

```csharp
var bodyFont = new RasterFontResource("receipt-body", "2026-10",
    File.ReadAllBytes("receipt-regular.ttf"), File.ReadAllBytes("receipt-bold.ttf"));
var fonts = new RasterFontCatalog(new[] { bodyFont }, "receipt-body");
var options = new EscPosDeviceOptions { RasterFonts = fonts };
var command = new EscPosPrintCommand(Encoding.UTF8, EscPosLayoutMode.Raster, options);
command.Initialize().RasterPage(384, 200, canvas =>
    canvas.DrawText(0, 0, "Receipt", 24, fontFamily: "receipt-body"));
var bytes = command.Build().GetBytes();
```

资源别名区分大小写，和字体内部名称无关。`PrintFontKind.Host` 的名称、旧 `FontFamily` 字段及画布字体参数在固定配置中都引用资源别名；未指定字体时使用 `DefaultFont`。名称重复、未知默认/替代引用、无效字体及伪装为常规或粗体的资源在创建时拒绝；未知的显式别名直接拒绝，不掩盖拼写错误。

`fallbackFonts` 在 C# 构造器中是可选的有序资源别名列表。空列表严格拒绝主字体的缺字或缺少粗体；提供列表后只依次尝试能覆盖整段文字且具有所需样式的资源，不拼接不同字体的字形，不合成粗体，也不扫描系统字体。模板在换行、缩放和绘制时复用选定别名，Provider 边界检查和最终画布使用相同配置。相同字体字节及相同渲染库/选项不受机器上系统字体安装情况影响；资源版本是来源标记，不代替实际字体字节或库版本。

字体资源在构造时复制；字节属性每次返回副本，配置资源和替代列表只读。命令、Provider、能力查询可安全共用该不可变配置；修改输入数组、资源数组或选项属性不改变已创建命令和 Provider 的字体配置。逐字形检查不证明复杂文字的组合、连字或双向布局在设备上相同，Native 仍依赖声明字库和实机验收。

Core 与 Host 共用 `RasterFontLimits`：每个资源及目录的常规、粗体原始数据合计最多 32 MiB，目录最多 64 个资源，替代列表最多 64 项。单资源超限在复制和解析前拒绝；目录有界枚举并检查累计容量，不再次复制资源数据。`RasterFontResource(name, version, regularData, boldData, cancellationToken)` 与 `RasterFontCatalog(fonts, defaultFont, fallbackFonts, cancellationToken)` 提供可取消入口，旧构造器使用 `CancellationToken.None`。字体复制或解析失败会归还本次预算，预算超限保留 `PrintResourceLimitException`；第三方字体解析只能在调用边界响应取消。

Host 的 `escPosDevice.rasterFonts` 使用 `{ defaultFont, fallbackFonts, fonts: [{ name, version, regularData, boldData? }] }`；字体数据为 Base64。最多 64 个资源，常规及粗体解码数据合计不超过 32 MiB；配置只接受 ESC/POS Raster。能力响应 `rasterFonts` 仅返回默认/替代别名及资源名称、版本、是否具有粗体，不返回字体数据。设计器项目和模板包保留字体字节，裸模板 JSON 仍只保存协议无关模板。

模板包继续使用既有的 20 MiB 压缩包、32 MiB 解压总量限制；该总量包含模板、图片及保存了 Base64 字体的清单。字体导入的 32 MiB 限制不等于可保存同样大小的字体包，超过归档总量时会明确拒绝。含字体的项目使用模板包保存，避免普通项目 JSON 的 1 MiB 上限；也不静默丢弃字体资源。

Templates 样例可在输出目录后追加常规字体路径和可选粗体路径，例如 `dotnet run --project samples/Templates -- artifacts/fixed-font-samples receipt-regular.ttf receipt-bold.ttf`。额外生成 `fixed-font-receipt.escpos` 并比较重复渲染字节；不提供字体参数时原样例行为保留。样例版本标记 `sample-input` 应在实际应用中替换为真实资源版本。

### ESC/POS 机型 Profile 与 DPI

ESC/POS 的图片指令、点阵方向和物理纸宽不是跨机型统一能力。需要按目标设备手册显式填写 `EscPosPrinterProfile`，并把它放入 `EscPosDeviceOptions.Profile`。Profile 可以声明图片指令（`RasterGsV0` 或 `BitImageEscStar`）、ESC `*` 的 8 点或 24 点模式、水平/垂直 DPI、进纸运动单位、可打印宽度、单条图片数据预算，以及切刀和原生条码/二维码/文字倍数能力。`EightDotImpact(360)` 是一个通用的 8 点针式设备起始配置示例，不代表任何品牌或型号已经兼容；只支持 ESC/P 或厂商私有指令的针式设备不能据此当作 ESC/POS 设备。

未显式设置 `EscPosDeviceOptions.Dpi` 时，有 Profile 就使用其 `HorizontalDpi`，没有 Profile 时使用 203；显式设置时必须与 Profile 的水平 DPI 一致。水平 DPI 用于模板和输入位图的横向点数，ESC `*` 会根据 `VerticalDpi` 对纵向采样进行转换，以保持相同的物理高度；`MotionUnitsPerInch` 用于 ESC `3`/ESC `J` 的带间进纸。GS `v 0` 要求横纵 DPI 相同，非等方 DPI 应选择 ESC `*`。

GS `v 0` 按行排列数据，`MaxImageBlockBytes` 会按完整行分块；ESC `*` 按垂直带和列排列数据，8 点模式每列一个字节，24 点模式每列三个字节，预算则按列块分割。ESC `*` 的水平分块只改变图片指令数量：每个垂直带末尾写一次图片控制换行 `LF`，不会因为同一带有多个水平块而额外进纸或切纸。带间的 `ESC 3`/必要时 `ESC J` 是图片编码所需的垂直推进，和模板页面的 `FeedLines` 独立；GS `v 0` 分块本身不添加进纸或切纸。编码结束写入 `ESC 2`，启用单向点阵时再写入 `ESC U 0`，表示恢复协议默认行距和双向打印状态；这不是保存并恢复调用前打印机状态的机制。

GS `v 0` Profile 使用非默认 `BitImageMode` 或启用 `UnidirectionalBitImage` 会在构造时明确拒绝。Profile 禁用原生条码、二维码或文字倍数时，只影响 Native 命令和 Native Provider；Raster 模式会把内容绘制到主机位图，不依赖这些原生能力。纸宽和图片预算仍会在 Provider 预检阶段严格检查，超出时按页面或元素路径报错。

直接构造命令时可以这样绑定机型配置：

```csharp
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Protocols;

var profile = EscPosPrinterProfile.EightDotImpact(360);
var device = new EscPosDeviceOptions { Profile = profile };
// 未设置 Dpi，device.Dpi 默认等于 profile.HorizontalDpi（此示例为 180）。
var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster, device)
    .Initialize()
    .RasterPage(360, 120, canvas => canvas.DrawLine(0, 0, 359, 0))
    .Build()
    .GetBytes();
```

Epson 的 [ESC `*` 点阵图指令参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/esc_asterisk.html) 和 [ESC `3` 行距指令参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/esc_3.html) 可用于核对命令字段和设备手册；它们只是协议语法参考，不构成对具体品牌或型号的支持声明。最终兼容性必须通过目标设备手册和实机验收确认。

TSPL 可通过 `TsplMediaOptions` 配置 `GAP`、`DIRECTION`、`REFERENCE`、`SPEED` 和 `DENSITY`，模板入口为 `TemplateRenderOptions.TsplMedia`。介质命令在 `SIZE`、`CODEPAGE`、`CLS` 后输出。TSPL 的 `Dpi` 默认 203，也可以显式设置为目标设备值；它是能力描述和调用方点数换算用的元数据，不会向打印机发送改变分辨率的命令。

ZPL 的 `ZplMediaOptions.Dpi` 同样默认 203，也应按目标设备显式设置。CPCL 继续保留历史固定 200 DPI 的能力描述和布局语义；各协议的 DPI 不能互相替代，最终点数和纸宽应以目标设备手册为准。

### TSPL/ZPL 机型 Profile 与能力边界

R55/R54/R53 补充：下方历史能力列表中如未列出 Code 11、Plessey 或 Code 49，以本节为准。ZPL `BarcodeType.Code11` 使用 `^B1`，由 `ZplPrintCommand.BarcodeCode11` 和统一 Provider 输出；Plessey 使用 `^BP`，由 `ZplPrintCommand.BarcodePlessey` 和统一 Provider 输出；Code 49 使用 `^B4`，由 `ZplPrintCommand.BarcodeCode49` 和统一 Provider 输出。三者均支持 N/R/I/B 四方向，数据、条高、模块宽度和页面占用严格预检；ZPL 其他未映射类型仍拒绝。

R33 补充：TSPL 统一能力还包含 `I2OF5G`，Provider 使用 TSC 原生 `DPI`/`DPL`，分别发送 11/13 位 payload；主机按统一 4/9 规则生成或验证 12/14 位完整输入。下方历史段落中未包含 I2OF5G 的集合描述由本补充覆盖。

TSPL 统一能力包含 Code93、I2OF5、I2OF5C 和 `MSI`：Provider 分别输出 `BARCODE` 的 `93`、`25`、`25C` 与 `MSI` 类型，Code93 接受非空可打印 ASCII，25/25C 只接受数字并按 TSC 手册要求分别归一化为偶数/奇数位，MSI 接受 1–13 位数字并保持无校验位语义。`MSIC` 是设备手册中的独立校验变体，未纳入统一 `BarcodeType.Msi`，需要时通过 `WriteRaw`/`WriteRawBytes` 显式注入并自行承担固件兼容性。ZPL 统一能力包含 I2OF5、I2OF5C、I2OF5G、POSTNET、PLANET 和 Intelligent Mail：I2OF5/I2OF5C/I2OF5G 使用 `^B2`，I2OF5 要求偶数位数字，I2OF5C 在发送前将偶数位输入补前导 `0` 并以 `e=Y` 交给设备计算 Mod10，I2OF5G 则由主机按 4/9 规则生成或验证校验位并使用 `e=N`；Provider 分别输出 `^B2`、`^BZ`、`^B5` 与 `^BZ` 类型 3，POSTNET 接受 5、9 或 11 位数字，PLANET 接受 12 或 14 位且要求数字和校验位对 10 取模为 0，IMb 接受 20、25、29 或 31 位数字；这些类型都支持 N/R/I/B 四方向。三类邮政码允许连字符分组并在编码前归一化，IMb 不含校验位，条码本身由 65 根四状态条组成，数据由 20 位跟踪码和最多 11 位路由码组成；这些 USPS 业务字段仍由调用方负责。

ZPL I2OF5/I2OF5C/I2OF5G 的直接入口分别为 `ZplPrintCommand.BarcodeI2OF5`、`ZplPrintCommand.BarcodeI2OF5C` 与 `ZplPrintCommand.BarcodeI2OF5G`；I2OF5C 的偶数输入会补前导 `0`，再由 `^B2` 的 `e=Y` 计算 Mod10，I2OF5G 按 4/9 规则由主机生成或验证完整数据并使用 `e=N`。三者高度范围均为 1–32000，窄条模块宽度为 1–10，支持 N/R/I/B；统一模板会在 Provider 校验阶段报告非法长度、字符、校验位或元素路径，ZPL Profile 关闭原生条码时也会严格拒绝。`^B2` 的方向、偶数配对和 Mod10 参数依据 [Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)。ZPL POSTNET、PLANET 和 Intelligent Mail 的直接入口分别为 `ZplPrintCommand.BarcodePostnet`、`ZplPrintCommand.BarcodePlanet` 与 `ZplPrintCommand.BarcodeIntelligentMail`，其长度/校验规则和官方依据保持不变。

`TsplMediaOptions.Profile` 接受可选的 `TsplPrinterProfile`，`ZplMediaOptions.Profile` 接受可选的 `ZplPrinterProfile`。Profile 是调用方依据目标设备手册填写的声明，不探测设备、不下载字体，也不自动推断品牌或固件兼容性；Profile 为 null 时，TSPL/ZPL 保留现有通用能力、默认编码和输出字节。

两个 Profile 共同提供 `Name`、`FirmwareVersion`、`PrintableWidthDots`、`MaxLabelHeightDots` 和 `MaxImageDataBytes`。名称和固件版本只是能力快照的来源记录；宽度、高度和单条单色图片字节上限均为可选的正数，单位分别是当前 DPI 的打印点、打印点和字节。`SupportsImages`、`SupportsGraphics`、`SupportsNativeBarcode`、`SupportsNativeBarcodeHumanReadable`、`SupportsNativeQrCode` 和 `SupportsRotation` 分别收窄图片、线框、原生一维条码、原生条码可读文字、原生二维码及文字/条码旋转能力；斜线仍须显式设置对应介质选项的 `AllowRasterizedLines = true`，并同时受到图片能力和图片预算约束。

`DeviceFontNames` 只接受协议内置字体标识：TSPL 为 `"0"` 至 `"8"`，ZPL 为 `"0"` 至 `"9"` 或 `"A"` 至 `"Z"`。null 表示不增加字体限制，空数组表示禁用文字；提供列表后，未列出的字体会被拒绝。`EncodingCodePages` 只能从当前已映射编码中收窄：ASCII 为 20127、UTF-8 为 65001，TSPL 另有 936；ZPL 不接受 936。null 保留当前映射，非 null 列表必须是非空且不重复的子集，不能借此新增设备字符集。`CharacterRanges` 可选，表示所列设备字体共同覆盖的 Unicode 标量闭区间；非 null 列表必须非空、不含 null，且区间互不重叠，null 表示不声明范围。区间仅用于主机侧预检，不证明目标固件的实际字库覆盖。

ZPL 另有 `SupportsCut`；设为 false 时，页面或任务切纸会提前拒绝，不能同时配置 `Cutter`。Host 的设计器 API 与 Vue 设计器已经接通 `tsplMedia.profile`/`zplMedia.profile`：媒体对象和 Profile 的未知字段严格拒绝，能力响应按需返回 `labelProfile`；未配置 Profile 时保留旧行为。设计器可以创建、清除和编辑 Profile，旧 Host 若提交 Profile 后能力响应缺少 `labelProfile`，会明确提示更新 Host。TSPL/ZPL 均可另声明下面的 `Cutter` 契约启用受控裁切。

命令、Provider 和 `TemplateProtocolCapabilities.Create(options)` 建立能力快照时会深复制 Profile 及其数组；之后修改原 Profile、媒体选项或数组，不改变已经创建的命令、Provider 或能力查询。`PrintCapabilities` 可查询机型名称、固件记录、可打印宽度/标签高度、图片预算、设备字体、字符范围及各项 `Supports*` 能力，模板能力快照还会据此过滤元素、字体和字号。能力查询仍只是声明的主机侧边界，不能替代目标机型的手册和实机验收。

模板渲染会在读取业务数据、绑定字段和打开图片资源前静态拒绝 Profile 已明确不支持的元素、设备字体和固定页面宽高；依赖实际字段内容的字符范围检查在绑定后继续按模板路径校验。`AutoHeight` 不会把模板高度或 `MaxPageHeight` 布局上限当作实际页高提前拒绝；分页结束后按每个实际页的高度再次校验标签高度上限。`MaxImageDataBytes` 按单色位图每行填充后的宽度计算，即 `ceil(width / 8) * height`；ZPL 的十六进制膨胀和命令头不计入该字段。TSPL/ZPL 的斜线转位图会在分配位图画布前检查该预算。

TSPL 已写入 `Size` 或其他结构化输出后，`ApplyMediaOptions` 不能再添加、移除或替换 Profile；仍未绑定 Profile 时，只有介质设置的旧 `ApplyMediaOptions` 行为保持不变。`WriteRaw`/`WriteRawBytes` 仍是受信任的原始指令出口，会绕过这些结构化 Profile 检查，调用方负责保证目标协议和设备约束。

```csharp
var tsplOptions = new TsplMediaOptions
{
    Profile = new TsplPrinterProfile
    {
        Name = "Example-TSPL",
        FirmwareVersion = "manual-record",
        PrintableWidthDots = 384,
        MaxLabelHeightDots = 1024,
        MaxImageDataBytes = 4096,
        DeviceFontNames = new[] { "0", "1" },
        EncodingCodePages = new[] { 20127, 65001, 936 },
        CharacterRanges = new[] { new PrintCharacterRange(0x20, 0x7e) }
    }
};
var tsplBytes = new TsplPrintCommand(Encoding.ASCII, tsplOptions)
    .Size(384, 200, "dot")
    .Text(8, 8, "0", 0, 1, 1, "Label")
    .Print()
    .Build().GetBytes();

var zplOptions = new ZplMediaOptions
{
    Profile = new ZplPrinterProfile
    {
        Name = "Example-ZPL",
        SupportsCut = false,
        DeviceFontNames = new[] { "0", "A" },
        EncodingCodePages = new[] { 20127, 65001 }
    }
};
```

直接使用 `EscPosRasterCanvas` 时，Raster 支持：

- `DrawText`：使用配置的固定字体，未配置时使用系统字体，并支持粗体和下划线；
- `DrawLine`：绘制任意角度的线段并指定线宽；
- `DrawRectangle`：绘制描边或填充矩形；
- `DrawBarcode`：将 Code 128、Code 39、Code 93、EAN8、EAN13、UPC-A、UPC-E、Codabar 或基础 I2OF5 绘制为可指定模块宽度的单色条码；按 `BarcodeType` 重载选择制式，缺少可靠主机编码器的类型会严格报错；
- `DrawQrCode`：将二维码按模块尺寸和纠错等级绘制到单色画布；
- `DrawRaster`：复制 MSB 优先排列的单色位图，图片尺寸必须与数据长度匹配。

例如：

```csharp
var bytes = new EscPosPrintCommand(layoutMode: EscPosLayoutMode.Raster)
    .Initialize()
    .RasterPage(576, 160, canvas =>
    {
        canvas.DrawText(16, 8, "Hello", 24, bold: true);
        canvas.DrawLine(16, 48, 560, 48, 2);
        canvas.DrawRectangle(16, 64, 544, 72, 2);
    })
    .Feed(1)
    .Cut()
    .Build()
    .GetBytes();
```

也可以在命令类中显式开始和结束一张光栅页，再使用坐标 `Text`、`Line` 和 `Box` 命令。页未结束时 `Build()` 会报错，原生和原始指令也不能插入页内；坐标必须位于已开始的页内。

```csharp
var bytes = new EscPosPrintCommand(layoutMode: EscPosLayoutMode.Raster)
    .BeginRasterPage(384, 120)
    .Line(8, 8, 375, 8)
    .Box(8, 20, 375, 110)
    .EndRasterPage()
    .Build()
    .GetBytes();
```

`PrintCapabilities` 的 Native/Raster 布局标志按当前模式报告；Raster 模式还报告主机字体支持及 ESC/POS 光栅命令的行字节数、高度上限。这些是协议字段上限，不代表某个型号的物理纸宽或可用内存。

`SupportsBarcode`、`SupportsQrCode` 和 `SupportsBarcodeType` 描述统一模型的输出能力；`SupportsCommandBarcode`、`SupportsCommandQrCode` 和 `SupportsCommandBarcodeType` 描述独立命令的设备原生编码能力。ESC/POS Profile 禁用原生条码或二维码时，Raster Provider 仍能由主机绘制码图，但直接调用 `Barcode` / `QrCode` 会拒绝，原生命令能力查询也会返回不支持。

`PrintCapabilities` 还提供统一文本模型的 `FontKinds`、`DeviceFontNames`、`GetTextSizeRange(font)` 和 `SupportsFont(font, size)`，以及 `QrModuleSize`、`BarcodeModuleWidth`、`BarcodeHeight`。范围包含最小值和最大值；最大值为 null 只表示未声明额外的参数上限，不能据此推断设备纸宽、字形或内存容量。TSPL 固定字体按现有整数倍率取整规则计算允许的请求字号；字体 `0` 根据当前 DPI 转换后的 `TEXT` 点数上限计算范围。

直接调用独立命令的原生码图 API 时使用 `CommandQrModuleSize`、`CommandBarcodeModuleWidth`、`CommandBarcodeHeight`。例如 ESC/POS Raster 模型可以在主机上生成更大的码图，但独立 `QrCode`/`Barcode` 方法仍编码设备原生命令，并受原生参数范围和 Profile 约束。

模板编辑器应使用 `TemplateProtocolCapabilities.Create(options)` 查询模板层的能力快照，获得元素类型、设备字体与离散字号、文字/条码角度、模块范围和样式支持。模板层对 CPCL 字号、条码 0/270 度及二维码模块上限有额外约束，不能只依据独立命令的通用能力。该快照不读取业务数据和图片资源，后续修改选项或 Profile 不改变已有快照；实际字体字形、内容、坐标、旋转占位和资源预算仍由渲染器校验。

`RasterPage` 会在分配画布和执行绘图回调前检查布局模式、未结束页面、尺寸字段、Profile 纸宽、图片分块预算、倍宽/倍高标志和 ESC * 重采样范围。无效配置不会执行回调；绘图回调抛出异常时不追加该页图片。合法页面仍使用与 `RasterImage` 相同的编码路径。

Provider 的 `Check(document).FirstIssue` 保留 `PageIndex` 和 `ElementIndex`，均为零基序号；页级问题的 `ElementIndex` 为 null。多个页面或重复元素可以有相同的 `ElementPath`，应结合位置字段定位，不能只选择路径的第一个匹配。
`SupportsBarcodeType` 查询统一 `PrintDocument` Provider 可表达的条码类型；`SupportsCommandBarcodeType` 查询独立命令类可编码的条码类型。ESC/POS 独立命令支持 Function B 的多种制式；统一 Provider 在 Native 模式支持 Code 128、GS1-128、Code 39、Code 93、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、I2OF5C 和 I2OF5G，Raster 模式支持主机 Code 128、Code 39、Code 93、EAN8、EAN13、UPC-A、UPC-E、Codabar 和基础 I2OF5。GS1-128 使用 Function B `m=74`，独立入口保留 Epson 7-bit ASCII 数据，不替调用方生成 AI、FNC1 或业务校验；统一模板继续要求数字应用标识开头。Code 39 使用各协议实现允许的非空大写字符集，Code 93 使用非空 ASCII，EAN/UPC 支持缺少校验位时由模板层补齐，I2OF5 的奇数模板输入补前导 0；I2OF5C 按 3/1 规则补齐校验位，I2OF5G 按 4/9 规则补齐或验证校验位；Codabar 要求 A-D 起止符。ZPL Provider 和命令支持 Code 128、Code 39、Code 93、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、I2OF5C、I2OF5G、MSI、POSTNET、PLANET 和 Intelligent Mail，分别使用原生 `^BC`、`^B3`、`^BA`、`^B8`、`^BE`、`^BU`、`^B9`、`^BK`、`^B2`、`^BM`、`^BZ`、`^B5` 和 `^BZ` 类型 3；Code 39 自动生成起止星号并默认关闭 Mod-43 校验，Code 93 默认关闭可读文字中的校验位显示。EAN/UPC 结构化入口校验数字和已有校验位，写入 ZPL 时只发送设备要求的数据部分；UPC-E 将统一模型的 6/7 位压缩数据展开为 `^B9` 要求的 10 位输入，并只接受号码系统 0。ZPL Codabar 要求完整 A-D 起止符，Provider 将起止符拆到 `^BK` 参数并只把正文写入 `^FD`；ZPL I2OF5 要求至少 2 位偶数数字，I2OF5C 由 `e=Y` 交给设备计算 Mod10，I2OF5G 由主机按 4/9 规则生成或验证并使用 `e=N`；统一 MSI 使用 `^BMA` 保留输入数字且不追加校验位，PLANET 要求 12/14 位数字和校验位，IMb 要求 20/25/29/31 位数字且不追加校验位，直接 `BarcodeMsi`、`BarcodePlanet` 和 `BarcodeIntelligentMail` 可分别选择 A-D 或 N/R/I/B 方案。非法字符、错误校验位、不完整的起止符和不符合 IMb 路由长度的数据在输出前拒绝。

ZPL 还支持统一 POSTNET（`BarcodeType.Postnet`）、PLANET（`BarcodeType.Planet`）和 Intelligent Mail（`BarcodeType.IntelligentMail`）输出；POSTNET 使用 `^BZ`，PLANET 使用 `^B5`，IMb 使用 `^BZ` 的类型 3。输入分别只接受 5/9/11、12/14 和 20/25/29/31 位数字；PLANET 校验数字和，IMb 不追加校验位。三者都允许连字符用于分组且会被移除，方向使用 N/R/I/B。
ZPL 结构化 Code 128 数据仅接受非空可打印 ASCII，并拒绝 `^`、`~` 和 `>`；`>` 在 Zebra 的 `^BC` 指令中是调用码前缀，若需主动使用子集切换或功能字符，请通过受信任的原始命令入口自行编码。Code 39 使用 `^B3o,e,h,f,g`，Code 93 使用 `^BAo,h,f,g,e`，EAN-8/EAN-13 使用 `^B8o,h,f,g`/`^BEo,h,f,g`，UPC-A/UPC-E 使用 `^BUo,h,f,g,e`/`^B9o,h,f,g,e`，I2OF5/I2OF5C/I2OF5G 使用 `^B2o,h,f,g,e`；I2OF5G 的 4/9 校验位由主机处理且 `e=N`。结构化入口拒绝控制字符和协议分隔符，需要完整转义时请使用受信任的原始命令入口。
`SupportsBarcodeHumanReadable` 表示统一 Provider 是否能开启条码下方的可读文字。`PrintBarcodeElement.HumanReadable` 和模板的 `BpBarcodeElement.HumanReadable` 可显式开关；不设置时保持旧行为：TSPL/ZPL 开启，CPCL 和 ESC/POS Native/Raster 关闭。CPCL 显式开启会输出 `BT 0 24 0` 包围条码并在之后恢复 `BT OFF`；ESC/POS Native 使用原生 HRI，Raster 使用 `EscPosDeviceOptions.RasterBarcodeHumanReadableFontSize` 指定的主机字号（省略时为 12 点）绘制并按实际墨迹做页边界校验。设备文字高度取决于固件或主机字体，布局仍需按目标机型预留空间；Raster 要求显式开启时声明正数 `HumanReadableReserveHeight`，以免文字落入下一元素或页外。
可将目标机实测的字高写入 `HumanReadableReserveHeight`（打印点）：模板会把它计入流式高度和分页，统一文档会据此检查页边界。默认零保持旧版布局；关闭可读文字或目标协议不支持 HRI 时，非零预留值会被拒绝。

协议命令的 `Build()` 可重复调用，构建失败不会清除已经记录的命令。`WriteRaw` 是显式的原始指令出口，调用方负责保证内容符合目标协议。ZPL `BarcodeI2OF5C` 是独立的校验位语义入口，保留旧 `BarcodeI2OF5` 的参数与输出契约。

模板渲染默认生成 CPCL，也可以通过 `TemplateRenderOptions.Protocol` 选择 TSPL、ZPL 或 ESC/POS；ESC/POS 布局默认 Native。TSPL/ZPL 支持坐标文字、线框、条码、二维码和单色图片。TSPL 统一模板的 I2OF5/I2OF5C、MSI、POSTNET、PLANET、PLESSEY、ITF14、EAN14、Code11 和 China Post 分别输出对应的原生 `BARCODE` 类型，并在命令层严格处理数字字符、固定比例和设备校验语义；CPOST 固定使用 1:1 窄宽比，Code 11/China Post 的最终校验和符号编码由目标固件完成。ESC/POS Raster 支持模板文字、线段、边框矩形、主机 Code 128/Code 39/Code 93/EAN8/EAN13/UPC-A/UPC-E/Codabar/I2OF5/China Post、二维码和单色图片；China Post 使用固定 3:1 宽窄比的 Datalogic 2 of 5 主机模式，HRI 不追加校验位。模板图片会先按亮度阈值转换为黑白位图。Raster 文字、上述主机条码和二维码均支持主机侧 0/90/180/270 度旋转；条码可读文字与其预留区一起旋转，文字下划线随文字旋转，反白仍严格拒绝。Raster 也提供 `EscPosPrintCommand.RasterBarcode` 和 `RasterQrCode` 坐标入口。ZPL 模板支持 Code 128、Code 39、Code 93、Code 11、Plessey、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、I2OF5C、I2OF5G、MSI、POSTNET、PLANET 和 Intelligent Mail，并按原生命令输出四方向条码；统一 MSI 使用 `^BMA` 无校验位，I2OF5G 使用主机 4/9 校验位与 `^B2` 的 `e=N`，Plessey 使用 `^BP` 的默认 3:1 比例，直接命令可选择 A-D 校验方案，PLANET 使用 `^B5`，IMb 使用 `^BZ` 类型 3 并校验 20/25/29/31 位数据长度。ESC/POS Native 模式支持从左侧原点开始、24 点、无旋转的顺序文字行，并可通过 `ESC - n` 输出单线下划线、通过 `GS B n` 输出反白；Native 条码还支持 GS1-128 `GS k 74` 和 I2OF5C/I2OF5G，GS1-128 的 AI、分隔符与业务校验由调用方负责，I2OF5C/I2OF5G 分别由主机补齐或校验 3/1、4/9 校验位后输出 ITF。连续文字行可按模板的 24 或 26 点逻辑行距输出，整行空白转换为额外换行。粗体、下划线和反白会在每段文字前后显式切换，非 24 点字号和坐标旋转仍严格拒绝。无法换算为整行的坐标布局会报错；需要精确点坐标或 Raster 旋转时显式使用 Raster 模式。
ZPL 统一模板中的 I2OF5C 使用 `^B2` 的 `e=Y`，偶数输入在设备计算校验位前补前导 `0`；I2OF5G 由主机按 4/9 规则生成或验证完整数据并使用 `e=N`；旧 I2OF5、其他协议和未映射变体仍按各自能力边界执行。
非 CPCL 模板图片先将透明像素合成到白纸，再按 `TemplateRenderOptions.ImageThreshold` 二值化；默认阈值 128，可设置为 0 至 255。CPCL 的既有图片处理与输出字节不受此选项影响。

四种模板协议共用 `PrintDocument` 页面模型和 Provider 校验。CPCL 图片沿用 Floyd-Steinberg 二值化；模板的压缩图片会保留为二进制 `CG` 指令，普通图片仍输出 `EG`，避免改变旧模板的字节。旧模板条码 `Ratio` 仍是 CPCL 比例码；输出 TSPL、ZPL 或 ESC/POS 时，模板适配器会将它换算为实际条宽点数。非 CPCL 模板可用 `NarrowModuleWidth` 显式指定窄条点宽，TSPL 还可用 `WideModuleWidth` 指定宽条点宽；不支持的协议会拒绝显式宽条配置。

模板页可以通过 `TemplateRenderOptions.Copies`、`FeedLines` 和 `CutAfterPrint` 配置份数、额外进纸和切纸。未指定时 ESC/POS 保持一行进纸并切纸，其他协议不额外进纸或切纸；显式设置 `FeedLines = 0` 可以关闭 ESC/POS 进纸。不支持的页设置会在输出前报错，协议能力和数据校验错误包含页面或元素路径。

模板分页已按协议选择度量策略：CPCL 使用既有字库和枚举字号，ESC/POS Raster 使用所选固定字体或未配置固定字体时的系统字体测量，ZPL 与 TSPL 字体 `0` 使用保守单元宽度估算，TSPL 固定字体使用基础点阵尺寸和实际放大倍率。非 CPCL 模板的 `Fit` 可按目标协议缩小到整数点数。ESC/POS Raster 的模板文字可通过 `BpFontElement.FontFamily` 指定固定字体配置中的资源别名；直接使用 `PrintDocument` 时可通过 `PrintTextElement.FontFamily` 指定。配置了 `RasterFonts` 时名称按固定字体别名解析，未配置时按系统字体族解析；其他协议和 ESC/POS Native 会在校验时拒绝非空字体族。TSPL/ZPL 可通过模板或文档文字元素的 `DeviceFontName` 选择内置字体，TSPL 接受 `0` 至 `8`，ZPL 接受单个大写字母或数字；未指定时为 `0`。`TemplateRenderOptions.DeviceFontCalibrations` 可按字体标识配置半角宽、全角宽和行高，匹配时优先于全局 `DeviceHalfCellWidthAt24`、`DeviceFullCellWidthAt24`、`DeviceLineHeightAt24`；其 `TextElements` 可进一步按 Unicode 文本单元校准前进宽度和可选墨迹矩形，度量随字号/TSPL 整数放大倍率缩放，并用于换行、分页、旋转布局和边界预检。墨迹矩形以设备文字命令原点为基准，四边必须同时给出，可包含负向 bearing。对 TSPL 固定字体，未指定时使用该字体的基础点阵宽度；其他字体未设置全角字宽时默认为半角的两倍。这些值只适用于 TSPL/ZPL，不会向设备发送字体命令。涉及严格对齐或长文本分页时，应使用目标型号验证实际字距。
Raster 文字另按实际光栅化墨迹检查四边界，模板会为字体负向 bearing 自动留出空间。直接使用 `PrintDocument` 时，坐标须容纳完整字形；例如某些字体在 `x=0` 绘制时会产生负向像素，预检会带元素路径拒绝该裁剪。`EscPosRasterCanvas.MeasureTextInkBounds` 可用于计算所选字体和字号的实际范围。

模板先检查无需业务数据即可判定的协议能力与页面设置，再读取图片资源、展开明细和分页。不支持的静态元素会在资源读取前报出协议和模板元素路径；依赖实际内容或坐标的能力会在收集全部逻辑页后继续校验，最后才逐页生成字节和检查输出大小。

## 显式条码与二维码静区

`PrintQuietZone` 用旋转前的逻辑打印点声明左、上、右、下四边留白。统一文档中的码图坐标仍指向编码区起点，`reservedWidth` 或二维码的 `width/height` 是编码区占位，静区向外扩展。未传入静区时保留旧行为；`PrintQuietZone.Uniform(0)` 表示显式不增加静区。留白数值由调用方按码制和目标设备确定，组件不会自动把显式零值改成最小值。

```csharp
var document = new PrintDocument();
var page = document.AddPage(400, 200);
page.Add(new PrintQrCodeElement(16, 16, "A-100", 4, QrCodeCorrectionLevel.M,
    PrintQuietZone.Uniform(16)), "Receipt.QrCode");
var bytes = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.UTF8)
    .Render(document)[0];
```

坐标 Provider 在输出前检查码图连同静区是否完全位于页内，并检查其他元素的占位是否侵入静区，与元素追加顺序无关。空白静区之间可重叠。原生文字、图片、条码按声明的整个占位保守检查；原生图片行宽按发送的整字节点数计算，包含填充位。线段按笔画范围、矩形按四边及实际原生命令端点检查，斜线使用保守包围区域。包含非零静区的页面中，无法测量的原生文字必须声明宽高，CPCL 条码必须声明编码区预留宽度，CPCL 二维码必须声明编码区宽高。TSPL/ZPL 启用或默认启用条码可读文字时，必须声明 `HumanReadableReserveHeight` 或显式关闭可读文字。设备声明尺寸的准确性仍由调用方按机型确认。

ESC/POS Native 会明确拒绝非零静区；空静区可保留原点原生输出。CPCL 的显式静区二维码只接受无旋转输出；CPCL 条码静区沿用原生命令的逆时针锚点，模板适配器负责顺时针布局转换。其他已有旋转限制不变。

直接画布可调用 `DrawBarcode(x, y, data, height, quietZone, moduleWidth)` 或 `DrawQrCode(x, y, data, moduleSize, errorLevel, quietZone)`。画布依据实际编码区尺寸保留空白，拒绝静区内已有黑点，也拒绝后续写入黑点；不清除原图。带静区保护的复合绘图失败会恢复像素及新增静区记录。画布的像素检查与公共模型的保守占位检查分别适用于它们各自的尺寸声明。

## Native 混合收据

`EscPosDeviceOptions.NativeFlow = EscPosNativeFlowMode.Sequential` 显式启用文字、Code 128、二维码和页尾的顺序混排。默认 `Rows` 保留旧文字行与原点码图行为；Raster 不接受 Sequential。能力快照的 `NativeFlow` 表示当前模式，非 Native 为 null。

Sequential 仍使用统一页面和元素模型。文字按既有 24/26 点逻辑行排列，同一行的 ASCII 列必须对齐配置的制表位。输出码图前关闭文字行；原生码图指令在标准模式自行打印并进纸，因此码图之后不重复添加换行。逻辑游标按码图声明的 `Y + Height` 推进，后续元素不得倒退或重叠；额外空白只能换算为整行。每个新页面从原点重新开始。码图必须声明正数占位宽高，开启条码 HRI 时必须声明 `HumanReadableReserveHeight`；模板根据自身码图尺寸生成声明。

这里的坐标用于顺序、占位及分页校验。纸上的实际文字行距、码图尺寸和 HRI 字高由设备固件决定，不能把声明高度当作设备定位命令。二维码的默认逻辑留白也不自动转换为设备静区；非零显式静区仍严格拒绝。需要精确点坐标、保证留白或图形时选择 Raster。码图必须从 X=0 开始，不支持旋转；设备 Profile 禁用的原生码图仍提前拒绝。

例如，直接文档可在 Y=0 输出标题、Y=26 输出明细、Y=52 输出高度 60 的条码、Y=112 输出高度 84 的二维码，最后在 Y=196 输出页尾。以下设置只改变 Provider 的页面布局规则，不改变直接 `EscPosPrintCommand.Text/Barcode/QrCode` 的顺序调用语义：

```csharp
var options = new EscPosDeviceOptions { NativeFlow = EscPosNativeFlowMode.Sequential };
var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos,
    Encoding.ASCII, EscPosLayoutMode.Native, options);
```

协议依据：Epson [GS k 条码参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_lk.html) 和 [QR Print 参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_lparen_lk_fn181.html)。这些命令要求行首或空打印缓冲区，并由设备执行码图进纸；目标型号是否实现这些行为仍须实机确认。

## 显式任务级份数与收尾

`PrintJobOptions` 编排整个文档的份数和收尾。直接文档通过 `PrintJobRenderer.Render(provider, document, job)` 输出；模板通过 `TemplateRenderOptions.Job` 启用。未提供 `Job` 时，现有页面 `Copies`、`FeedLines`、`CutAfterPrint` 和模板默认字节保持原行为。

```csharp
var options = new TemplateRenderOptions
{
    Protocol = PrintProtocol.EscPos,
    Encoding = Encoding.ASCII,
    Job = new PrintJobOptions
    {
        Copies = 2,
        CopyOrder = PrintCopyOrder.Collated,
        FeedScope = PrintFinishScope.EachCopy,
        FeedUnit = PrintFeedUnit.Lines,
        FeedAmount = 2,
        CutScope = PrintFinishScope.EachCopy
    }
};
var result = new TemplateRenderer().Render(template, data, options: options);
foreach (var page in result.Pages)
{
    // 按列表顺序把 page.Data 原样交给调用方的传输实现。
    // SourcePageIndex 和 CopyIndex 是零基逻辑页与份数归属。
}
```

`Collated` 按完整份输出：A0、B0、A1、B1；`Uncollated` 按逻辑页输出全部份数：A0、A1、B0、B1。每个返回项是一个实际输出页，设备页内份数为一；`SourcePageIndex`、`CopyIndex` 和收尾标志记录该项归属。旧页面模式的模板归属与收尾标志为 null。

进纸和切纸可分别选择 `None`、`EachPage`、`EachCopy` 或 `JobEnd`，对应不执行、每个实际页后、每份完整文档后和整个任务末尾。相同边界先进纸再切纸。多页多份的 `Uncollated` 不能形成连续的完整份，因此拒绝 `EachCopy`；单页或单份允许。模板在分页确定第二个逻辑页时检查该组合，也会按逻辑页数乘以任务份数提前检查 `MaxPages`。依赖分页结果的限制无法在读取任何业务数据之前全部判定。

| 协议 | 任务进纸单位 | 任务切纸 |
| --- | --- | --- |
| CPCL | 不支持额外任务进纸 | 不支持 |
| ESC/POS | Lines、MotionUnits；显式 Profile 另支持 Dots | 按 Profile 的切刀能力检查 |
| TSPL | Dots | 显式 Cutter 的 Batch/Immediate；未配置时拒绝 |
| ZPL | 不支持额外任务进纸 | 使用既有 `^CN`，实际设备能力须确认 |

`Lines` 使用设备当前行距，`MotionUnits` 使用设备纵向运动单位，两者不能当作相同长度。ESC/POS `Dots` 根据 Profile 的纵向 DPI 和每英寸运动单位向上取整换算；Profile 只声明元数据，不重设设备运动单位。ESC/POS 大进纸量按最多 255 单位拆分；TSPL 使用 `FEED` 打印点距离。任务单位必须匹配目标能力，不能静默改用其他单位。省略显式裁切选项时沿用原有页尾指令；需要全切、半切或切刀位置补偿时使用下面的裁切契约。

任务策略独占份数和收尾。文档页面必须为一份、零额外进纸且不切纸；模板的旧 `Copies` 必须为一，`FeedLines` 必须省略或为零，`CutAfterPrint` 必须省略或为 false。非法负数仍拒绝。正数 `FeedAmount` 必须有非 None 的 `FeedScope`；零数量不产生进纸。任务按实际输出项计算输出预算，在展开重复份数前检查，预算并不等同于全过程的峰值内存限制。

样例 `samples/Templates/PrintJobSamples.cs` 将同一模板在四协议下生成两页、两份，并保存有序页面及 `job.json`。调用方必须按返回顺序发送整个页面字节，不能先按逻辑页重新排序；ZIP 清单也采用实际输出顺序。这些文件仅为指令产物，设备接受效果仍需实机验收。

## 显式裁切形状与切刀位置补偿

`PrintCutOptions` 可放在 `PrintPage.CutOptions`、`TemplateRenderOptions.CutOptions` 或 `PrintJobOptions.CutOptions`。默认 `Mode = Full`、`FeedToCutPosition = false`、`FeedBeforeCutAmount = 0`、`FeedBeforeCutUnit = MotionUnits`。页面选项必须启用页面切纸；任务选项必须有非 `None` 的 `CutScope`。模板页面选项不能与 `Job` 同时使用。省略新选项时旧 API 和默认字节保持不变。

ESC/POS 的显式映射在 Native 和 Raster 都可使用。`Full` / `Partial` 分别编码 GS V Function A 的 `m=0/1`。机型可用 `EscPosPrinterProfile.SupportedCutModes` 收窄模式；null 保留既有入口，空数组禁用切刀，未知或重复值拒绝。请求不在白名单内的模式会提前失败，不自动改变裁切形状。旧 `Cut(bool partial = false)` 同样受白名单限制。TSPL 使用下文独立的机型策略。

`FeedToCutPosition = true` 使用 GS V Function B：设备先将纸送到切刀位置，再增加指定补偿距离并执行 `m=65/66` 裁切。它必须有显式 Profile 的 `SupportsFeedAndCut = true`。补偿量只接受 `MotionUnits` 或 `Dots`；点数按 Profile 的纵向 DPI 和运动单位向上取整。换算结果必须为 0–255；零也可请求设备进纸到切刀位置。正补偿量配合 `FeedToCutPosition = false` 会拒绝。不能按普通进纸方式拆分超限补偿，因为多次 Function B 会产生多次切纸。任务普通进纸与切刀位置补偿独立，同一边界先普通进纸，再执行一次裁切命令。设备实际切法和运动单位须按手册确认，参见 [Epson GS V 官方参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_cv.html)。

```csharp
var options = new TemplateRenderOptions
{
    Protocol = PrintProtocol.EscPos,
    Encoding = Encoding.ASCII,
    EscPosDevice = new EscPosDeviceOptions
    {
        Profile = new EscPosPrinterProfile
        {
            Name = "Confirmed cutter",
            SupportedCutModes = new[] { PrintCutMode.Full, PrintCutMode.Partial },
            SupportsFeedAndCut = true
        }
    },
    Job = new PrintJobOptions
    {
        Copies = 2,
        CutScope = PrintFinishScope.EachCopy,
        CutOptions = new PrintCutOptions
        {
            Mode = PrintCutMode.Partial,
            FeedToCutPosition = true,
            FeedBeforeCutAmount = 6,
            FeedBeforeCutUnit = PrintFeedUnit.MotionUnits
        }
    }
};
```

能力快照提供 `CutModes`、`CutFeedUnits`、ESC/POS 的 `CutFeedMotionUnits` 以及 TSPL 的 `CutFeedDots`。它们描述显式裁切选项的编码支持；旧 `SupportsCut` 继续保留。ZPL 的显式切法由可选 `Cutter` 契约声明，不支持通用切前进纸补偿。既有 ZPL 裸 `^CN` 仅为兼容输出入口：[Zebra 官方手册](https://cpws.zebra.com/cpws/docs/zpl/zpl-zbi2-pm-en.pdf) 给出的 `^CN` 要求 Kiosk 模式及有效参数，不能用旧入口字节测试证明实机已裁切。

TSPL 裁切采用下面的显式机型契约。[TSC 官方手册](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf) 将 `CUT` 定义为立即裁切，`EOJ` 是固件 V6.39 EZ 起的完成同步命令；`SET CUTTER` 与 `SET PARTIAL_CUTTER` 会持久保存。固件版本记录不会自动启用这些能力。

### TSPL 受控裁切

`TsplPrinterProfile.Cutter` 接受可选 `TsplCutterProfile`。未配置时原有默认字节与无结构化切纸能力保持。配置时必须显式确认 `SupportsEndOfJob = true`，并提供非空、不重复的 `SupportedCutModes`；默认只声明 Full。配置、数组和能力返回均采用独立副本。

`Batch` 使用 `SET CUTTER BATCH` 或 `SET PARTIAL_CUTTER BATCH`，在每个 `PRINT` 批次后切一次。`PrintWithCut(options, sets, copies)` 先等待之前命令完成，关闭已声明的自动裁切，启用请求的批次切法，再执行 PRINT；EOJ 完成后关闭自动裁切。页面份数属于同一个 PRINT 批次，因此只在该批次末尾切一次；需要每份裁切时使用 Job 的 EachPage/EachCopy 编排。任务在指定边界页的 PRINT 内启用裁切，其他页不启用。

`Immediate` 只允许声明一种 CUT 的实际固定形状；CUT 没有选择全切/半切的参数。此策略表示调用方已经按目标机型确认关闭自动裁切后仍可执行 CUT，不从协议名称推断这一硬件行为。`PrintWithCut` 等待 PRINT 完成后执行 CUT，独立 `Cut(options)` 先执行 EOJ。配置了切刀契约的普通 `Print()` 也会同步并关闭已声明的自动裁切，以免沿用前一任务的自动切刀状态；成功完成的批次不会把自动裁切留为启用。关闭操作表示受控关闭，不是恢复未知的原设备设置；断电、取消传输或原始指令绕过后的状态仍须由发送方管理。

立即策略可声明 `CutPositionFeedDots`，表示从 PRINT 完成后介质位置到切刀的实测纵向点数，范围 0–9999。启用 `FeedToCutPosition` 时仅接受 Dots，并输出实测位置加 `FeedBeforeCutAmount` 的 FEED；合计不得超过 9999，零距离不输出 FEED。没有实测位置时补偿请求严格拒绝。Batch 自动裁切不能在 PRINT 完成和自动切纸之间插入补偿，因此拒绝补偿以及同时具有正数进纸和裁切的 Job；Immediate Job 仍保持先进纸再裁切。

```csharp
var media = new TsplMediaOptions
{
    Profile = new TsplPrinterProfile
    {
        Name = "Verified TSPL cutter",
        Cutter = new TsplCutterProfile
        {
            Strategy = TsplCutStrategy.Batch,
            SupportsEndOfJob = true,
            SupportedCutModes = new[] { PrintCutMode.Full, PrintCutMode.Partial }
        }
    }
};
var bytes = new TsplPrintCommand(Encoding.ASCII, media)
    .Size(320, 80, "dot").Text(8, 8, "Label")
    .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Partial })
    .Build().GetBytes();
```

模板使用 `TsplMedia = media`，页面显式设置 `CutAfterPrint = true` 和可选 `CutOptions`，或通过 Job 指定 CutScope/CutOptions。Host/设计器保存 `tsplMedia.profile.cutter`，能力返回 `tsplCutter`、`cutModes` 及立即策略的 `cutFeedDots`。这些值表示调用方确认的能力，样例参数必须按具体型号替换并完成实机验收。

### ZPL 受控裁切与独立文件

`ZplPrinterProfile.Cutter` 接受 `ZplCutterProfile`。调用方必须明确确认 `SupportsTearOff = true`，并声明非空、不重复的 `SupportedCutModes`。未配置时旧 `Print` 和裸 `Cut` 字节保留；配置后使用 `Start().Label(...).PrintWithCut(options, quantity).End()`，一个格式只声明一次打印。普通 `Print`，以及未显式 Print 的 `End`，会写入 `^MMT` 和份数，以关闭继承的自动裁切模式。每个格式明确选择模式，不保存或恢复设备未知状态；取消传输、断电和 `WriteRaw` 绕过后的状态由发送方管理。

- `Cutter`：输出 `^MMC` 和 `^PQq,q,0,Y`，在一个打印批次末尾裁切一次且不暂停；只映射 Full。份数逐份裁切通过 Job 的边界编排表达。[打印数量参考](https://docs.zebra.com/content/tcm/us/en/printers/software/zpl-pg/zpl-commands/%5Epq.html)
- `Delayed`：页面输出 `^MMD` 和 `^PQq`，随后独立输出 `~JK` 控制文件；只映射 Full。发送方必须按顺序保留文件边界，并保证设备已处于延迟模式且有待裁切标签；这不是设备完成确认机制。[打印模式参考](https://docs.zebra.com/us/en/printers/software/zpl-pg/zpl-commands/%5Emm.html)
- `Kiosk`：仅用于明确确认支持该模式的设备，输出 `^MMK`、`^KV`、`^PQ` 和带有效参数的 `^CN`。必须设置 2–9 mm 的 `KioskCutMarginMillimeters`；声明 Partial 时还须设置 10–60 mm 的 `KioskPartialUncutMillimeters`，表示留下未切介质的长度。Full 使用 `^KV0,b`/`^CN1`，Partial 使用 `^KVa,b`/`^CN0`；不改写 presenter 类型、超时或 loop length。这些毫米参数不是打印点，也不映射 `FeedToCutPosition`。支持范围依据 [Zebra 官方手册的 ^KV/^CN](https://cpws.zebra.com/cpws/docs/zpl/zpl-zbi2-pm-en.pdf)，不能从协议名称推断通用 Kiosk 支持。

三个策略都拒绝通用切前补偿及 ZPL 额外任务进纸。数量范围为 1–99,999,999。配置、数组和能力读回均为副本；错误在记录动作或读取模板资源前拒绝。受控格式尚未 `End` 时不输出部分页面，修正后可以继续构建。

`WriteRestrictedRaw` 与结构化调用共享每个格式的裁切状态。未显式声明 `^MM` 时，校验按 Cutter Profile 的策略解释 `^PQ`；Cutter 模式要求结构化输出形状 `^PQq,q,0,Y`，Delayed/Kiosk 仅接受单数量参数。Delayed 可在同一格式的打印数量之前用 `^MMT` 清除延迟裁切状态。Kiosk 必须按 `^MMK`、与 Profile 匹配的 `^KV`、`^PQ`、与 `^KV` 全切/半切值一致的 `^CN` 顺序闭合；`^XZ` 或结构化 `End()` 均执行同一配对校验。Raw 与结构化状态可跨调用续写，成功后仍按原文输出，失败批次不提交状态或字节。

```csharp
var media = new ZplMediaOptions { Profile = new ZplPrinterProfile {
    Name = "Confirmed delayed cutter",
    Cutter = new ZplCutterProfile { Strategy = ZplCutStrategy.Delayed, SupportsTearOff = true }
} };
var output = new ZplPrintCommand(Encoding.ASCII, media)
    .Start().Label(320, 80).Text(8, 8, "Label")
    .PrintWithCut(new PrintCutOptions()).End().BuildOutput();
for (var index = 0; index < output.Segments.Count; index++)
    File.WriteAllBytes($"segment-{index + 1:000}.zpl", output.Segments[index].Data);
```

`PrintPageOutput.Segments` 表达独立文件，而不是可任意合并的传输缓冲块。`ByteLength` 为所有文件的总字节数。`BuildOutput`、Provider 的 `RenderOutput`、Job 和模板结果保留边界；旧 Provider 通过可选接口/扩展适配为单文件，不增加原接口的必实现成员。需要多个文件时，旧命令 `Build`、ZPL Provider `Render` 或页面 `Data` 会明确拒绝并提示使用新入口，不返回少了裁切的字节。单文件页面的 `Data` 保持原字节。

页面或 Job 请求受控裁切而省略 `CutOptions` 时，默认请求 `Full`。仅声明 `Partial` 的 Kiosk 必须显式设置 `CutOptions.Mode = PrintCutMode.Partial`；不支持默认切法时模板在业务绑定与图片资源读取前拒绝，Job 错误定位到 `Job.CutOptions`。未配置 Cutter 的旧 ZPL 任务保留兼容裁切路径。

Host/设计器保存 `zplMedia.profile.cutter`，能力响应返回 `zplCutter` 与 `supportsIndependentOutputSegments`；直接 preview/render API 也核对配置，缺字段或返回策略不匹配时提示更新 Host。多文件 ZIP 首个页面文件仍为 `page-001.zpl`，后续如 `page-001-segment-002.zpl`；自动生成 `job.json`，`outputVersion: 2`、`preserveFileBoundaries: true` 和有序 `files` 列出文件名、零基页面/文件序号与用途。发送顺序以清单为准，不能仅排序 ZIP 文件或把控制文件拼入页面。没有独立文件的旧 ZIP/Job 清单保持原结构。

## 累计资源预算与取消

直接命令、Provider 和 Job 可放在调用方建立的 `PrintResourceScope` 中；不建立作用域时不增加默认累计限制。模板还可使用 `TemplateRenderOptions.MaxWorkingBytes` 为单次渲染设置预算。预算覆盖显式缓冲长度、RGBA 像素和引擎自有记录/集合的保守估计，`MaxOutputBytes` 则限制最终输出，两者用途不同。

```csharp
using var scope = PrintResourceScope.Begin(applicationWorkingByteLimit, cancellationToken);
var pages = provider.Render(document, cancellationToken);
var estimatedPeakBytes = scope.PeakBytes;
```

预留在分配前检查所有父级限制。缓冲扩容将旧、新数组同时计入；临时读流、图像处理及绘图回滚的预留在使用结束后归还。成功命令/页面结果的预留保留到调用方作用域结束，内部构建或渲染失败会归还本次操作的预留，便于在同一作用域内重试。作用域支持正常执行上下文流下的异步子任务和嵌套预算；须等待活动子作用域结束后再释放父级。显式抑制 `ExecutionContext` 流的后台任务不继承该预算。

`CurrentBytes`/`PeakBytes` 描述显式估计，不追踪 GC，也不等于进程实际内存。第三方解码、缩放、字体解析和码图编码的内部缓存/工作缓冲仍有覆盖边界；调用方在作用域外创建并持有的其他对象也不自动计入。资源超限通过 `PrintResourceLimitException` 的 `ResourceName`、`RequestedBytes`、`CurrentBytes` 和 `MaxBytes` 定位。

下表列出当前第三方路径的计量边界。已计费项是引擎能够在分配前显式估计并纳入作用域的存储；未完整计入项仍可能占用内存，不应将这些估计解释为完整堆上限。

| 路径 | 已计费的显式存储 | 未完整计入或不可观察的边界 |
| --- | --- | --- |
| ImageSharp 图片解码、单色化与缩放 | 解码后的 RGBA 像素、单色缓冲和 resize 结果存储 | 解码元数据，以及 `CloneAs`/`Mutate` 内部产生的 scratch 缓冲 |
| SixLabors.Fonts 主机字体与文字图像 | 复制的原始字体字节和文字图像存储 | `FontCollection`、字形缓存、`GetGlyphs` 对象和集合等库内部对象 |
| ZXing.Net 码图（固定 0.16.11 参数） | 按固定参数计算的保守矩阵上界及其输出矩阵存储 | 全部对象头、集合、编码过程的其他 scratch 和库内部工作缓冲 |

命令缓冲在保留文本片段时按 UTF-16 字符数乘二预留 `Command.SourceText`，编码失败归还该片段，`Clear()` 释放文本预留并保留字节缓冲容量。命令追加、文档页/元素、输出段和模板布局还计入记录、列表槽及保留文本的估计；同一文本每次记录分别计费，不按引用去重。工作预算应在创建命令/文档和追加内容前建立，作用域外已经存在的模型不追溯计费。

列表扩容先预留新旧两份槽容量，成功后归还旧槽；追加失败不发布部分记录。当前内部列表按容器 96 字节、引用槽 8 字节及各类型的记录成本估计，文本采用 24 字节加 UTF-16 长度乘二。资源索引按容器 128 字节、每次新键插入 256 字节及保留键/字符串估计，包含哈希扩容和租约记录的保守余量；移除/清空不会立即退还索引估计，保留至所属会话或调用作用域结束。此为可审计的记账规则，不是 CLR 对象布局或完整堆内存保证。

模板的块、绑定、缓存索引、换行和临时文档估计由渲染会话持有并在结束时释放；返回页数据、布局轨迹和输出段继续计入调用方作用域。失败与取消归还未提交操作的预算，资源超限的必要诊断与清理保留出口，不再次要求工作额度。第三方图片/字体解析、绘图与码图编码内部对象，以及 GC 和框架传输缓冲，仍不是该显式估计的完整覆盖范围。

Raster 主机条码/QR 度量和绘制，以及模板 DataMatrix/PDF417，会在编码前按当前 ZXing.Net 默认参数预留矩阵存储上界。Code128 包含 pattern 数据与输出位矩阵；QR/DataMatrix 包含字节矩阵与输出位矩阵；PDF417 包含编码行、放大及旋转过程的数据区。预算保留到本次度量或绘制完成，编码失败和取消同样释放。请求尺寸不足时编码器可能扩大符号，因此预留不只按请求宽高计算。此估计未包含编码器所有内部对象和工作缓冲，增加尺寸、边距或编码 hint 时须同步更新估计。

四协议命令支持 `Build(CancellationToken)`，ZPL 独立文件使用 `BuildOutput(CancellationToken)`；旧 `Build()` 保持 `CancellationToken.None` 行为。工厂返回的 `IProtocolPrintCommand` 可通过扩展方法使用取消入口，可选 `ICancelableProtocolPrintCommand` 保持旧自定义实现兼容。Provider 的可取消预检同样采用可选接口，旧自定义实现只能在调用前后检查取消。

内置实现会在元素遍历、文字覆盖/度量、像素、编码分块和字节复制循环之间检查取消。第三方同步解码、缩放、字体及符号处理、调用方 getter/同步 `Stream.Read` 只能在调用前后检查，不能强制中断正在执行的同步调用。取消抛出 `OperationCanceledException`，构建失败不返回部分输出，已记录命令可使用新的令牌重新构建。

`Build(token)` 返回的缓冲写入器仍使用该令牌；之后取消会使 `GetBytes()` 抛出取消异常。需要保留输出时，在操作完成前调用 `GetBytes()` 保存独立字节数组；已经取得的数组不受后续取消影响。使用 `Build()` 返回的写入器保持旧行为。

## 图片旋转

统一模型和模板图片支持顺时针 `0/90/180/270` 度旋转。图片 `Width`、`Height` 和 MSB 优先位图表示旋转前尺寸，`X`、`Y` 表示旋转后占位的左上角；90/270 度交换占位宽高。模板 `StartCoordinate`、`EndCoordinate`、单元格可用宽度和分页使用旋转后占位，布局轨迹返回实际占位及 `Rotation`。图片作为整体分页，不拆成多个旋转片段。

```csharp
var image = new PrintImageElement(20, 10, 3, 2,
    new byte[] { 0xA0, 0xC0 }, RotationAngle.Rotate90);
// 原始 101 / 110 点阵顺时针旋转为 11 / 10 / 01，输出占位为 2×3。
```

旧六参数构造器及默认 `Rotation = 0` 保持原方向和输出字节。新的构造器保留原始位图快照，Provider 在主机端旋转有效像素，再输出普通图片命令；非零角度会清零输出行尾填充位，输入行尾填充位不会变成有效像素。CPCL 的 `IsCompress` 仍决定 EG/CG 编码，TSPL/ZPL 和 ESC/POS Raster 保留各自图片指令和黑白极性。

`BpImageElement.Rotation` 使用同一角度约定；缩放先按旋转前宽高完成，再旋转单色位图。CPCL、TSPL、ZPL 和 ESC/POS Raster 支持此能力，ESC/POS Native 继续严格拒绝图片。`PrintCapabilities.SupportsImageRotation` 和 `TemplateProtocolCapabilities.ImageRotations` 独立描述主机图片旋转，不依赖机型的原生文字/条码旋转标志；禁用图片的机型不会提供图片旋转能力。Host 返回 `imageRotations`，旧 Host 缺失该字段时设计器只允许 0 度。

临时旋转位图在分配前预留 `Image.RotatedMonochrome` 预算，图片复制或绘制完成后释放；旋转循环响应取消，不修改调用方位图。非零角度图片会按旋转后的宽高预检页边界，包括 CPCL；默认 CPCL 未旋转图片继续沿用旧校验行为。实际打印方向、纸宽和设备边界仍需实机确认。

## 实心填充矩形

独立标签命令提供 `TsplPrintCommand.FillRectangle(x, y, width, height)` 和 `ZplPrintCommand.FillRectangle(x, y, width, height)`，另有取消令牌重载。区域为半开范围 `[x, x + width) × [y, y + height)`，与旧 `Box` 的右下端点参数不同。TSPL 输出 `BAR x,y,width,height`；ZPL 输出 `^FOx,y^GBwidth,height,min(width,height),B,0^FS`，宽高限定为 1–32000 点。坐标、正尺寸、溢出、机型图形能力、设备边界、记录预算和取消在追加前校验；失败不会污染之前的命令记录。

统一模型使用 `PrintRectangleElement.CreateFilled(x, y, width, height)` 表达精确实心黑色填充，与模板 V2/V3 的 `Type: 7, Filled: true` 一致。CPCL 适配为水平 `L` 及填充高度，TSPL/ZPL 使用原生图形命令，ESC/POS Raster 绘制单色位图；Native 严格拒绝。旧 `PrintRectangleElement(..., filled: true)` 在 CPCL 中继续保持 IL 反色及端点行为，需要统一实心语义的新代码应使用工厂。`Filled` 默认 false 的模板及旧 CPCL 调用字节保持兼容。机型禁用图片但允许图形时，两种标签协议的填充仍可使用。

指令范围依据 [TSC TSPL/TSPL2 手册](https://fs.chinatsc.cn/system/files/tspl_tspl2_programming_2018_5_17_scjiantizhongwen.pdf) 和 [Zebra ZPL ^GB 参考](https://docs.zebra.com/content/tcm/us/en/printers/software/zpl-pg/zpl-commands/%5Egb.html)。大面积黑色填充和连续打印负载须按实际打印机的打印密度与工作负载限制进行验收，本地字节测试不证明其连续打印能力。

## 实机验收

本地 golden-byte 测试只能确认输出字节，不能证明设备固件会按预期打印。对每种目标机型分别记录：型号、固件版本、实际支持的指令集、DPI、纸宽、设备代码页、传输方式、所用模板/数据、输出文件及打印结果。将 `TemplateRenderResult.Pages[*].Segments` 按顺序独立发送，单文件页面仍可使用 `Data`，不要经字符串解码再编码。针式机若只支持 ESC/P 或厂商私有指令，不能用 ESC/POS 测试结果替代；需取得其协议资料并另行实现 Provider。

- CPCL、TSPL、ZPL：各打印一张包含中英混排文字、线框、Code 128、二维码和单色图片的标签；核对页宽、元素位置、条码/二维码可扫描性、图片方向与黑白极性，并按目标机型校准字宽。再验证份数及设备支持的介质、进纸或裁切选项。
- ESC/POS Native：打印中英混排的连续文本行、Code 128、二维码，并检查配置的代码页、换行、制表列、进纸与裁切。只在设备手册明确支持的代码页和切刀能力下启用相应选项。
- ESC/POS Raster：打印同一收据的坐标文字、线框、Code 128/Code 39/Code 93/EAN/UPC/Codabar/I2OF5、二维码和单色图片；核对纸宽、裁切边界、文字清晰度、条码/二维码可扫描性及进纸与裁切。

每项记录“通过 / 不通过 / 设备不支持”和实际测量或故障现象；设备不支持的能力应保留严格拒绝，不以静默省略指令作为通过。实机验收完成前，不把本地测试通过表述为该机型已兼容。

TSPL 字体 `0` 的 `TEXT` 宽高参数按点数计算；固定字体 `1` 至 `8` 则按各自基础点阵高度选择整数放大倍率，模板字宽和行高按实际倍率计算。固定字体无法缩小到小于基础点阵；不同机型仍需校准实际字距。公共模型的 `FeedLines` 表示行数，而 TSPL `FEED` 接受打印点距离，因此 TSPL Provider 对正数 `FeedLines` 严格报错；需要按点进纸时直接使用 `TsplPrintCommand.Feed(distance)`。ESC/POS Provider 的 `Copies` 会在同一个逻辑页面输出中重复整份页面指令，每份分别执行该页的进纸和切纸设置。

统一文本模型可以显式传入不可变的 `PrintFont`，例如 `new PrintTextElement(0, 0, "ABC", 24, new PrintFont(PrintFontKind.Device, "1"))` 用于 TSPL 固定字体；`PrintFont.Default` 使用目标协议默认字体，`PrintFontKind.Host` 只允许 ESC/POS Raster。CPCL 的语义设备字体为 `24` 或 `55`，必须与原有字号匹配，沿用相同的内置文字字节。原 `PrintTextElement` 构造器签名和字体字段保持兼容；独立命令保留既有调用，ESC/POS Raster 另提供带字号、样式和角度的坐标 `Text` 重载。字体标识只表达选择意图，设备实际字形和字距仍由机型决定。

统一模型的 `Width`/`Height` 表示旋转前的占位；90/270 度文字和条码会交换宽高进行页边界预检。CPCL 的统一 `PrintDocument` 条码直连只接受 0 和顺时针 270 度，90/180 度会在 Provider 预检阶段按元素路径严格报错；旧 `CPCLPrintCommand` 原始角度 API 保持原有字节行为。TSPL/ZPL 二维码即使未声明占位，也会按编码后的符号尺寸检查右边和下边。TSPL 固定字体的可打印 ASCII 文本按内置点阵估算占位；TSPL/ZPL Code 128 按主机编码后的条码宽度预检，ZPL Code 39 的未知固件宽度仍应使用 `Width` 声明并以实机校准为准。其他设备字体和条码，以及固件选择不同 Code 128 编码方式的情况，仍不能仅靠主机估算证明物理边界；严格对齐时应提供目标机型实测占位并进行实机核对。

TSPL Provider 的旋转文字/条码以统一模型 `X`/`Y` 的占位左上角定位，再按旋转角度换算原生锚点：90 度在 X 加旋转前高度，180 度在 X/Y 分别加宽度/高度，270 度在 Y 加宽度。旋转的未知设备字体必须声明 `Width`/`Height`；已知固定 ASCII 字体可由点阵推导。旋转条码必须声明预留宽度，或使用可推导宽度的 Code128。原始 `TsplPrintCommand.Text/Barcode` 继续接收原生命令锚点，字节不变。ZPL Provider 和 `ZplPrintCommand.Barcode`/`Barcode39`/`Barcode93`/`BarcodeEan8`/`BarcodeEan13`/`BarcodeUpcA`/`BarcodeUpcE`/`BarcodeCodabar`/`BarcodeI2OF5`/`BarcodeMsi`/`BarcodePostnet`/`BarcodePlanet` 支持 Code128、Code39、Code93、EAN/UPC、Codabar、I2OF5、MSI、POSTNET、PLANET 的 N/R/I/B 四方向；MSI 统一模型固定使用 A 方案无校验位，直接命令可选择 A-D，PLANET 校验 12/14 位数字和，UPC-E 会先把统一模型压缩数据转换为 ZPL 的 10 位字段，Codabar 会把 A-D 起止符拆到 `^BK` 参数，I2OF5 保持偶数数字输入，原有 Code128 六参数签名继续生成 N 方向。

POSTNET、PLANET 和 Intelligent Mail 是上述 ZPL 旋转条码集合中的独立 `BarcodePostnet`、`BarcodePlanet` 与 `BarcodeIntelligentMail` 入口，分别使用 `^BZ`、`^B5` 和 `^BZ` 类型 3；IMb 输入严格限定为 20/25/29/31 位数字，不自动追加校验位。

### 可选严格原生占位预检

TSPL/ZPL 可按目标设备启用 `requireDeclaredNativeBounds: true`，让 Provider 在渲染前要求非空原生文字声明 `PrintTextElement` 的 `width` 和 `height`，条码声明 `reservedWidth`。TSPL 固定字体 `1` 至 `8` 的可打印 ASCII 文本属于已知点阵例外，仍会按字体点阵和放大倍率自动计算；字体 `0`、ZPL 字体及其他非 ASCII 文本必须显式声明。严格模式下，如果条码的可读文字按协议默认值生效（TSPL/ZPL 的 `HumanReadable` 为 `null`）或显式设置为 `true`，还必须提供正数 `HumanReadableReserveHeight`；显式设置 `HumanReadable = false` 时不要求该高度。默认值为 `false`，因此现有直接调用 `PrintDocument` 的代码保持原有行为。

```csharp
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;

var document = new PrintDocument();
var page = document.AddPage(640, 400);
page.Add(new PrintTextElement(
    x: 20, y: 20, text: "商品 A", fontSize: 32,
    width: 160, height: 40, deviceFontName: "0"));
page.Add(new PrintBarcodeElement(
    x: 20, y: 90, type: BarcodeType.Code128, data: "A-100",
    barHeight: 100, narrowWidth: 2, reservedWidth: 220));

var provider = PrintProtocolProviderFactory.Create(
    PrintProtocol.Tspl, Encoding.UTF8,
    requireDeclaredNativeBounds: true);
provider.Validate(document);
var pageBytes = provider.Render(document)[0];
```

严格预检只校验主机侧声明和协议布局边界，不能替代设备手册或实机测量。设备字体的实际字距、固件对条码的编码方式、缩放策略和打印头可用区域可能与主机估算不同；应按具体型号填写实测占位并完成打印验收。该选项只适用于 TSPL/ZPL，传给 CPCL 或 ESC/POS 会直接报参数错误。

模板渲染使用 `TemplateRenderOptions.RequireDeclaredNativeBounds = true` 启用同一原则。模板选项会将目标型号在 24 点参考字号下的半角字宽、全角字宽和行高分别作为 `DeviceHalfCellWidthAt24`、`DeviceFullCellWidthAt24`、`DeviceLineHeightAt24`，用于在换行、分页和输出前的元素占位检查；严格模式还要求每个实际输出的 `BpBarcodeElement.Width` 为正数。条码可读文字按协议默认值生效或显式启用时，还必须设置正数 `HumanReadableReserveHeight`；显式关闭可读文字时不要求该高度。TSPL 固定字体 `1` 至 `8` 的可打印 ASCII 文本仍可使用已知点阵度量，TSPL 字体 `0`、ZPL 字体以及非 ASCII 文本需要设备校准值。默认值为 `false`，旧模板行为不变；该设置只用于主机侧布局和拒绝不完整声明，不代表设备已经通过实机验收。
R21 能力汇总修正：上文统一 ZPL 条码集合中的 I2OF5C 与 `BarcodeI2OF5C` 直接入口均已实现；偶数输入补前导 `0` 后使用 `^B2` 的 `e=Y`，Raster 与其他未声明能力仍严格拒绝。

R22 能力修正：ESC/POS Raster 现支持 `I2OF5C`/`I2OF5G`。Provider 在主机侧按 3/1、4/9 Mod10 规则补齐或验证校验位，再复用 ZXing ITF 光栅编码；可读文字、宽度预检和旋转占位均使用归一化数据。GS1-128、MSI、POSTNET、PLANET、Intelligent Mail 等没有可靠主机编码器的类型仍严格拒绝。本文前文 R18 的“Raster 严格拒绝 I2OF5C/I2OF5G”表述由本修正覆盖。

R23 能力修正：ESC/POS Raster 现支持 `MSI`，使用 ZXing MSI 主机光栅编码；统一输入严格接受 1-13 位 ASCII 数字，不自动追加校验位。宽度测量、模板页边界、HRI 数据和旋转占位复用同一归一化路径，校验失败在输出前以元素路径报告并可安全重试。ESC/POS Native 仍严格拒绝 MSI；Raster 仍严格拒绝 GS1-128、POSTNET、PLANET、Intelligent Mail 等没有可靠主机编码器的类型。实际打印、扫描、DPI 和固件校验语义仍需目标设备验收。

R24 能力修正：ESC/POS Raster 现支持 `POSTNET`，主机按 5/9/11 位 ASCII 数字和连字符分组校验输入，自动计算校验位并生成 USPS 的全高/半高条码模式；连字符不会进入条码数据。宽度测量、四角旋转、HRI 数据、模板页边界和失败恢复使用同一规范化路径。ESC/POS Native、TSPL、ZPL 的 POSTNET 能力保持原有边界；Raster 的 `SupportsCommandBarcodeType` 仍为 false，因为该能力属于画布主机编码而非独立原生命令。条高、间距、DPI、打印和扫描质量仍需目标设备验收。

R25 能力修正：ESC/POS Raster 现支持 `PLANET`，接受 12/14 位 ASCII 数字和连字符分组，校验数字和必须为 10 的倍数；主机使用 POSTNET 的反相五条模式，保留全高起止框条并按基线绘制半高条。宽度测量、四角旋转、HRI、模板页面边界和失败恢复沿既有 Raster 路径执行。该能力仍只属于主机光栅画布，`SupportsCommandBarcodeType(BarcodeType.Planet)` 保持 false；GS1-128、Intelligent Mail/IMb、FIM 等没有可靠主机编码器的类型继续按能力矩阵严格拒绝。PLANET 条高、间距、DPI、纸宽、打印和扫描质量需目标设备验收。

R26 能力修正：ESC/POS Raster 现支持统一模型已有的 `FIM` A/B/C 模式。主机按 USPS 九位二进制模式绘制条/空槽位，宽度、四角旋转、HRI、模板静区和失败恢复沿既有 Raster 路径执行；`SupportsCommandBarcodeType(BarcodeType.Fim)` 保持 false。FIM D/E、GS1-128、Intelligent Mail/IMb 和厂商私有邮政扩展继续严格拒绝。FIM 模式定义依据 [USPS DMM 202](https://pe.usps.com/TEXT/dmm300/202.htm)，实际条高、间距、DPI、纸宽和扫描质量需目标设备验收。

R27 能力修正：ESC/POS Raster 现支持统一模型的 `UccEan128`/GS1-128。主机复用 ZXing Code 128 并启用 `GS1_FORMAT`；输入首两个字符必须是数字应用标识开头，ASCII 29 作为字段分隔符编码为 FNC1，其他内容限制为 7-bit ASCII。Raster 的宽度测量、HRI、四角旋转、模板静区和资源预算沿同一路径处理；Native 的 `GS k 74` 原生命令保持不变，AI 语义、字段长度、业务校验和固件扫描结果仍需调用方与目标设备确认。Intelligent Mail/IMb、FIM D/E 和厂商邮政变体继续严格拒绝。
