# 多协议实现与当前候选验证

## R139 验证补充：TSPL/ZPL 文本单元字体度量

逐字体校准现在可按 `StringInfo` 文本单元覆盖 24 点下的 advance，并可选提供相对命令原点的墨迹四边。该配置参与 TSPL/ZPL 换行、分页、Fit、旋转布局和边界预检；DesignerHost 的严格 JSON DTO 接受 `textElements` 并把值映射到同一模板渲染选项。配置只承载调用方实测数据，本轮没有目标机型测量值。

| 门禁 | 结果 |
| --- | --- |
| L0 静态检查 | `git diff --check` 通过；仅有既有 CRLF 转换提示 |
| L1 字体校准定向测试 | Core.Tests `net6.0` `31/31`，零失败、零跳过 |
| L2 Core.Tests 完整测试 | `net6.0` `3138/3138`，零失败、零跳过 |
| Host 校准请求冒烟 | DesignerHost.Tests `net8.0` `1/1` 通过；Host.Tests 全量 `120/122` |
| Host.Tests 剩余失败 | TSPL/ZPL `Label_profile_qr_rotation_allowlist_is_forwarded_to_host_capabilities` 两项期望 200、实际 400；与本轮字体请求字段无关 |
| Core 与 Templates 多目标框架构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部通过，零警告、零错误 |
| Host 构建环境 | Web SDK 默认内容 glob 捕获工作区现有嵌套 `tools/DesignerHost/artifacts`；以 `EnableDefaultContentItems=false` 命令行覆盖运行 Host 测试，无仓库配置改动 |
| NuGet 漏洞索引 | Core.Tests 报 `NU1900`，当前环境不能加载索引；测试结果不受影响 |
| 设备验证 | 未执行；逐机型字体、墨迹和 HRI 数据仍需手册或实物样本 |

覆盖包括 TSPL/ZPL 字体选择隔离、组合文本单元、24 点缩放、负向 bearing、四个旋转角、ZPL `^FO` 布局坐标及 Host JSON 映射。没有新增依赖、CI job 或测试矩阵。

## R138 验证补充：TSPL/ZPL 原生 HRI 逐角度边界

TSPL/ZPL Profile 增加 90/180/270 度条码旋转的 HRI 四边覆盖。覆盖中的 null 边逐边继承通用 Profile 边距，0 明确覆盖为零。Direct 命令和模板 Provider 共用同一个不可变设备契约解析结果；未配置角度覆盖的边界与 R137 相同，指令字节不变。

| 门禁 | 结果 |
| --- | --- |
| L0 静态检查 | `git diff --check` 通过；本轮源码和文档行尾空白扫描无命中 |
| L1 角度边界定向测试 | Core.Tests `net6.0` `42/42`，零失败、零跳过 |
| L2 Core.Tests 完整测试 | `net6.0` `3120/3120`，零失败、零跳过 |
| Core 与 Templates 多目标框架构建 | 两项目的 `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部通过，零警告、零错误 |
| NuGet 漏洞索引 | 测试出现 `NU1900`；当前环境无法加载 `https://api.nuget.org/v3/index.json`，未影响测试结果 |
| 覆盖范围 | Profile 值非负校验、零值、快照、90/180/270 度选择、逐边继承、direct/template exact-edge 与 overflow、direct 命令字节不变 |
| 设备验证 | 未执行；配置需要目标型号手册或打印样本给出实测值 |

首次定向运行曾因测试夹具的 HRI 预留高度及角度坐标假设不一致而失败；修正夹具并覆盖全部三个非零角度后，定向集和完整 Core.Tests 均通过，未改生产边界语义。

## R137 验证补充：TSPL/ZPL 原生 HRI 四边页面包围盒

TSPL/ZPL Profile 增加上、右、下 HRI 外伸配置，与 R136 左侧值组成四边页面坐标边距。Direct 命令和模板 Provider 都只在 HRI 启用时检查外伸；旋转条码先换轴主体范围，右侧边距仍附加于旧的最小总宽之外。模板 Provider 的旋转 footprint 现在也包含换轴后的 HRI 预留高度。打印指令字节不因此改变。

| 门禁 | 结果 |
| --- | --- |
| L0 差异与静态检查 | `git diff --check` 通过；Profile 创建点和能力快照参数已核对；R137 触及文件无行尾空白 |
| L1 HRI/页面边界/旋转定向测试 | `net6.0` `138/138`，零失败、零跳过 |
| L2 Core 全量单元测试 | `net6.0` `3111/3111`，零失败、零跳过 |
| Core 与 Templates 多目标框架构建 | 两项目的 `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部通过，零警告、零错误 |
| NuGet 漏洞索引 | 测试出现 `NU1900`；当前环境无法加载 `https://api.nuget.org/v3/index.json`，未影响测试结果 |
| 覆盖范围 | 四边配置负值拒绝和快照、direct/模板四侧贴边与越界、旋转 footprint、HRI 关闭兼容、模板元素路径 |
| 设备验证 | 未执行；真实 HRI 字体外伸和各方向墨迹范围仍需目标机型手册或打印样本 |

首次定向运行有 4 个旋转边界断言因测试页高漏计已配置的最小 HRI 宽度而失败；按 Profile 的既有宽度语义修正测试几何后，定向集和全量 Core.Tests 均通过，生产边界实现未作回退。

## R136 验证补充：TSPL/ZPL 原生 HRI 左侧边界校准

TSPL/ZPL Profile 新增非负 `NativeBarcodeHumanReadableLeftOverhangDots`，以页面 X 轴字段坐标左侧距离计入 direct 与模板条码边界。该值只影响 HRI 开启时的主机预检，不移动字段或改变协议字节；HRI 关闭时沿用原边界行为。旋转输出需要设备方向专属校准。

| 门禁 | 结果 |
| --- | --- |
| L0 差异与调用点检查 | `git diff --check` 通过；新增调用点均已对齐；本轮文件行尾空白扫描无命中 |
| L1 HRI Profile/direct/Provider 定向测试 | `net6.0` `52/52`，零失败、零跳过 |
| L2 Core 全量单元测试 | `net6.0` `3103/3103`，零失败、零跳过 |
| 受影响项目构建 | Core 与 Templates 的 `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部通过，零警告、零错误；Core.Tests `net6.0` 构建通过 |
| NuGet 漏洞索引 | 出现 `NU1900`；当前环境无法加载 `https://api.nuget.org/v3/index.json`，不影响上述测试通过 |
| 覆盖范围 | Profile 非负校验与快照、direct/模板左边界贴合、越界失败原子性、元素路径、关闭 HRI 后忽略外伸值 |
| 设备验证 | 未执行；真实 HRI 外伸、字宽、对齐和旋转方向仍需目标机型手册或实测 |

首次精准集运行时还暴露一条 R134 Code 49 测试对通用边界异常文案的旧断言不匹配；仅修正了测试断言，最终精准集和 Core 全量回归均通过，未改运行时错误文案。

## R135 验证补充：TSPL/ZPL 原生 HRI 横向占位

TSPL/ZPL Profile 新增可选的 `NativeBarcodeHumanReadableReserveWidthDots`，用于校准启用原生 HRI 时的最小横向页面占位。宽度从条码字段原点向右计量，只影响主机预检，不更改打印字节；HRI 关闭时忽略该值。居中 HRI 的左侧外伸和真实字宽仍需单独校准。

| 门禁 | 结果 |
| --- | --- |
| L0 差异与空白检查 | `git diff --check` 通过；本轮触及文件行尾空白扫描无命中 |
| L1 HRI Profile/direct/Provider 定向测试 | 未启动；编译器 `CS0016` 无法写入 `src/Bing.EasyPrint/Bing.EasyPrint.xml`，返回 `Access denied` |
| L2 Core `net6.0` 全量测试 | 未运行；L1 在编译阶段被阻断 |
| 多目标框架构建 | 未运行；未重试替代输出目录 |
| NuGet 漏洞索引 | 测试命令报告 `NU1900`，当前环境无法加载 `https://api.nuget.org/v3/index.json` |
| 覆盖范围 | 新增用例覆盖正数校验、Profile 快照、HRI 启用/关闭、贴边/越界原子性及模板 Provider 元素路径；用例尚未执行 |
| 设备验证 | 未执行；居中 HRI 左侧外伸、固件字体和实际墨迹仍需目标机型手册或实测 |

本轮实现和测试代码已通过静态复核，但由于编译器无法写入 XML 文档输出，不能将编译或测试记为通过。恢复输出文件写入访问后，需先运行该定向测试，再运行 Core `net6.0` 回归。

## R134 验证补充：ZPL Code 49 `^B4` 行高与 footprint

ZPL Code 49 的公共 `height` 以 dots 表达，`^B4` 的 `h` 则是相对 `^BY` 模块宽度的倍率。本轮新增换算、实际行高边界估算、70 模块宽度预检及对应回归用例；有效调用的 ZPL 参数字节会按 API 声明的点数语义修正。

| 门禁 | 结果 |
| --- | --- |
| L0 静态差异检查 | `git diff --check` 通过，新增/未跟踪文件的行尾空白扫描无命中；Core 编译未能完成 |
| L1 ZPL Code 49 / direct 页面边界测试 | 未运行；编译器 `CS2012` 无法写入 `Bing.EasyPrint.dll`，返回 `Access denied` |
| L2 Core `net6.0` 全量测试 | 未运行；L1 在构建阶段被阻断 |
| 多目标框架构建 | 未运行；常规项目输出和本轮隔离输出目录均遭遇同类写入拒绝 |
| NuGet 漏洞索引 | 该次测试还报告既有 `NU1900`；不能访问 `https://api.nuget.org/v3/index.json` |
| 外部设备 | 未执行；目标固件行数、HRI 墨迹、纸宽/DPI、打印与扫描需要具体机型 |

回归用例覆盖 `height` 到倍率的换算与向上取整、70 模块宽度、编码器行数高度估算、旋转后的页面边界、贴边/越界和拒绝后输出原子性，但因编译受阻目前只是已编写、未执行。`^B4` 的堆叠布局与超过两行时可能越过主体宽度的 HRI，仍需设备确认。

## R133 验证补充：TSPL/ZPL direct HRI 预留高度校准

TSPL/ZPL Profile 新增 `NativeBarcodeHumanReadableReserveHeightDots`。该值必须为正数，只影响启用 HRI 时 direct 原生条码的主机页边界预估；未设置时保留既有 24 点估值，协议输出字节不变。Profile 在命令创建时进入不可变快照。

| 门禁 | 结果 |
| --- | --- |
| TSPL/ZPL HRI 页面边界与 Profile 定向测试 | `net6.0` `37/37`，零失败、零跳过 |
| Core 全量单元测试 | `net6.0` `3083/3083`，零失败、零跳过 |
| Core 多目标框架构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部通过，零警告、零错误 |
| NuGet 漏洞索引 | 测试报告既有 `NU1900`；当前环境无法访问索引，测试通过 |
| 覆盖范围 | 正值校验、Profile 快照、配置高度贴边/越界、失败原子性、无配置时旧 24 点行为、关闭 HRI、TSPL 与 ZPL direct 条码 |
| 设备验证 | 未执行；TSPL 未规定通用 HRI 行高，ZPL HRI 字体受当前字体命令影响，真实字高/字宽和打印质量需机型手册或实测 |

本轮只校准 direct HRI 的纵向预留。未配置 Profile 时继续使用旧 24 点估值以保持兼容，但它不代表设备标准高度；HRI 横向字宽与对齐仍未纳入边界模型。

## R132 验证补充：TSPL/ZPL 补充 Unicode 文本边界估算

TSPL/ZPL direct 原生文字边界预检按 Unicode scalar 而不是 UTF-16 code unit 计数。新增 supplementary-plane 字符用例覆盖精确贴右边界、越界一格和拒绝后输出保持不变；有效协议命令字节未改变。

| 门禁 | 结果 |
| --- | --- |
| TSPL/ZPL 页面边界定向测试 | `net6.0` `20/20`，零失败、零跳过 |
| Core 多目标框架构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部通过，零警告、零错误 |
| Core 全量单元测试 | 本轮未运行；本轮变更范围由 L1 边界测试覆盖，R130 全量结果仅为历史证据 |
| NuGet 漏洞索引 | 测试报告既有 `NU1900`；当前环境无法访问索引，测试通过 |
| 覆盖范围 | TSPL 固定字体非 BMP 字符、ZPL 请求字段宽度、边界贴合和失败原子性 |
| 设备验证 | 未执行；字体实际字距、组合字形和 HRI 仍需目标型号手册或实机校准 |

## R131 验证补充：ZPL 整页方向标准校验

ZPL `^PO` 表示整页反转 180 度，标准参数只有 `N`（正常）与 `I`（反向）。媒体选项现在只允许 `None`/`Down`；`Left`/`Right` 和未知枚举在创建命令时拒绝，避免输出无效的 `^POB/^POR`。ZPL 字段自身的旋转命令保持可用。依据：[Zebra ^PO 命令说明](https://support.zebra.com/cpws/docs/zpl/PO_Command.pdf)。

| 门禁 | 结果 |
| --- | --- |
| ZPL 媒体选项与页面边界定向回归 | `net6.0` `27/27`，零失败、零跳过 |
| Core 多目标框架构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部通过，零警告、零错误 |
| NuGet 漏洞索引 | 测试报告既有 `NU1900`；当前环境无法访问索引，测试通过 |
| 默认与有效方向 | 默认字节不变；`Down` 输出 `^POI`；`None` 保留标准正常方向 |
| 设备验证 | 未执行；具体型号整页方向、边距及输出位置仍需热敏标签设备验收 |

## R130 验证补充：TSPL/ZPL 直接命令页面边界预检

TSPL `Size` 与 ZPL `Label` 记录成功设置的页面尺寸。direct 结构化绘制在追加命令前校验文字、条码、二维码、线、线框、填充矩形及图像尺寸；错误路径保持命令输出不变。TSPL `mm` 尺寸按当前 DPI 转点，`REFERENCE`/`DIRECTION` 和 ZPL 方向设置参与范围变换。模板 Provider 仍使用模板布局与声明边界检查，避免 Native 字体估算改变既有模板输出。

| 门禁 | 结果 |
| --- | --- |
| 页面边界、字体、Provider 定向回归 | `net6.0` `175/175`，零失败、零跳过 |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `3068/3068`，零失败、零跳过 |
| Core 多目标框架构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部通过，零警告、零错误 |
| NuGet 漏洞索引 | 出现 `NU1900`；当前环境无法访问 NuGet 索引，不影响构建/测试结果 |
| 覆盖范围 | exact-edge/overflow 原子性、TSPL DPI 与 font `0` point 换算、Reference/Direction、ZPL orientation、native 估算、Raster 尺寸、no-size 兼容 |
| 设备验证 | 未执行；Native footprint、固件方向/参考点、实际打印和扫描率需要机型级验收 |

Native 字体、HRI 和条码边界是 host-side estimate。ZPL `^PO` 与 `Left`/`Right` 的映射问题已在 R131 收紧为严格拒绝；`WriteRaw` 仍无法进行结构化边界检查。

## R129 验证补充：GS1 Scan4Transport AI 子集

共享 GS1 解析器增加 AI `4300`–`4326` 的字段定义和 FNC1 规范化，并执行 SSCC、收货地址行与回运地址行的关联检查。规范依据：[GS1 General Specifications 26.0](https://ref.gs1.org/standards/genspecs/)、[GS1 Application Identifiers](https://ref.gs1.org/ai/)及 [GSCN 25-306](https://ref.gs1.org/standards/genspecs/gscn/2025/GSCN-25-306-MandAssoc03.pdf)。

| 门禁 | 结果 |
| --- | --- |
| GS1 Scan4Transport 定向回归 | `net6.0` `146/146`，零失败、零跳过 |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `3050/3050`，零失败、零跳过 |
| Core/Templates 多目标框架构建 | 五目标框架全部成功，零警告、零错误 |
| NuGet 审计数据源 | 测试构建出现既有 `NU1900`；当前环境无法访问漏洞索引，测试通过 |
| 覆盖范围 | AI 字段形状、FNC1、SSCC/地址行关联、Native/Raster/TSPL/ZPL 编码 |
| 设备验证 | 未执行；未验证固件输出、纸宽/DPI 校准、真实打印或扫描率 |

本轮对国家代码只做字母形状验证，对日期/时间只做字段长度验证。结构化括号输入不扩展 `BpTemplate` 的原始 AI/FNC1 字段契约。

## R128 验证补充：GS1 设备 payload 容量预检

本轮为 ESC/POS、TSPL、ZPL Profile 增加可选 GS1-128 payload 字符上限，为 ESC/POS 与 ZPL 增加独立 DataBar Expanded 上限。直接命令、Raster 绘制与共享模板 Provider 在生成输出前使用规范化数据进行检查；ASCII 29/FNC1 计入字符数，协议 framing 不计。未配置 Profile 上限时不会推断固件容量。

| 门禁 | 结果 |
| --- | --- |
| 定向测试 | `net6.0` `229/229`，零失败、零跳过 |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `3014/3014`，零失败、零跳过 |
| Core/Templates 多目标框架构建 | 五目标框架全部成功，零警告、零错误 |
| NuGet 审计数据源 | 测试构建出现既有 `NU1900`；当前环境无法访问漏洞索引，测试与构建通过 |
| 覆盖范围 | Profile 正数验证和快照、ESC/POS Native/Raster 超限拒绝、TSPL 模板元素路径、ZPL DataBar Expanded 拒绝 |
| 设备验证 | 未执行；需要机型手册/固件资料及真实打印和扫码验收 |

字符数 Profile 是调用方配置的设备约束，不代表模块宽度、物理页面适配或扫码成功。Raster Expanded 保留 77 字符输入边界。

## R127 验证补充：括号式 GS1 字段中的字面左括号

统一括号 AI 解析器新增 `\(` 字段值转义，解码后再校验 CSET82/字段长度；变量字段后仍插入 FNC1。ESC/POS Native、ESC/POS Raster、TSPL、ZPL 命令路径输出解码字符；Raster Expanded HRI 也去除输入转义符。原始数据仍拒绝方括号，未知反斜杠转义拒绝。

| 门禁 | 结果 |
| --- | --- |
| GS1/Native/TSPL/ZPL/Raster 定向回归 | `net6.0` `190/190`，零失败、零跳过 |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `3007/3007`，零失败、零跳过 |
| Core/Templates L0 构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部成功，零警告、零错误 |
| NuGet 审计数据源 | 测试显示既有 `NU1900`，当前环境无法访问 NuGet 漏洞索引；测试通过 |
| 设备验证 | 未执行；未验证各打印机固件容量、实际打印与读取率 |

`BpTemplate` 条码数据契约仍为原始 AI/FNC1；本轮没有扩展模板字段为括号展示语法。

## R126 验证补充：GS1 公司内部 AI 长度与字符集

共享 GS1 解析器现在将 AI `90` 限制为最多 30 个字符、AI `91`–`99` 限制为最多 90 个字符，并拒绝 CSET82 不支持的空格。变量字段后的 FNC1 行为和 Raster 的 77 字符总输入上限保持不变。规范依据：[GS1 General Specifications 26.0](https://ref.gs1.org/standards/genspecs/)、[24.0 CSET82 表](https://ref.gs1.org/standards/genspecs/24.0.0/)及[21.0.1 空格排除说明](https://ref.gs1.org/standards/genspecs/21.0.1/)。

| 门禁 | 结果 |
| --- | --- |
| GS1/Native/TSPL/ZPL/Raster 定向回归 | `net6.0` `175/175`，零失败、零跳过 |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2992/2992`，零失败、零跳过 |
| Core/Templates L0 构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部成功，零警告、零错误 |
| NuGet 审计数据源 | 测试显示既有 `NU1900`，当前环境无法访问 NuGet 漏洞索引；测试通过 |
| 设备验证 | 未执行；字段通过解析不代表打印机能容纳符号或具备对应固件支持 |

当前括号 AI 文本不能无歧义表达值中的 `(`；GS1 AI 的完整字典、载体容量联动预检和真实设备验收仍属 TODO。

## R125 验证补充：GS1 金额/折扣 AI 子集

本轮共享 GS1 解析器增加 `390n`、`391n`、`394n`、`395n`，并验证 Native ESC/POS、TSPL、ZPL 命令与模板字段，以及 Raster DataBar Expanded GTIN 通用字段路径。GS1 语法依据固定版本 [GS1 General Specifications 23.0.0](https://ref.gs1.org/standards/genspecs/23.0.0/)；没有验证金额的小数业务语义或货币代码清单。

| 门禁 | 结果 |
| --- | --- |
| GS1/Native/模板定向回归 | `net6.0` `175/175`，零失败、零跳过 |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2981/2981`，零失败、零跳过 |
| Core/Templates L0 构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部成功，零警告、零错误 |
| 构建数据源 | 测试命令出现既有 `NU1900`，因为当前环境无法访问 NuGet 漏洞索引；无测试失败 |
| 设备验证 | 未执行；未验证固件对货币/小数语义、HRI、打印质量或扫码读取率的行为 |

本轮关闭四类常见金额/折扣 AI 的结构化字段解析与本地输出子范围。其它 GS1 AI、货币业务验证、AI 关联规则、其它压缩族和真实设备验证仍属 TODO。

## R124 验证补充：ESC/POS Raster Code 49 完整符号几何

本轮补齐 USS-49 完整符号 footprint：81X 总宽、上下边界分隔条、包含外框的输出高度，以及按真实条纹宽度居中的 HRI。旋转、显式静区占用和页边界预检共用完整尺寸；Raster 模板沿用同一 Provider 几何。

| 门禁 | 结果 |
| --- | --- |
| Code 49 Raster 专项 | `net6.0` `15/15`，覆盖矩阵、完整符号 golden、最小行高、页边界、模板输出、非法 ASCII、90/180/270 度静区重叠与旋转 HRI 预留 |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2972/2972`，零失败、零跳过 |
| Core/Templates L0 构建 | 五目标框架均成功，零警告、零错误 |
| 设备验证 | 未执行；本地字节与布局结果不代表目标固件支持、打印质量或扫码读取率 |

本轮关闭 Code 49 Raster 的完整符号几何缺口；GS1 完整语法、ESC/POS Native 固件差异和真实设备验证仍是 TODO。

## R123 验证补充：ESC/POS Raster Code 49

本轮新增 Code 49 主机编码器，支持完整 7-bit ASCII 映射、数字压缩、2–8 行、70X 矩阵、行/符号校验；ESC/POS Raster 按 8X 最小行高和 1X 内部分隔条绘制，并通过模板 Provider 与页面预检输出。

| 门禁 | 结果 |
| --- | --- |
| Code 49 与既有 TSPL/ZPL 回归 | `net6.0` `72/72` |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2966/2966`，零失败、零跳过 |
| Core/Templates L0 构建 | 五目标框架均成功；构建仅有 NuGet 漏洞数据源不可达的 `NU1900` 提示 |
| 覆盖范围 | 参考矩阵、81 位数字容量、最小行高拒绝、ESC/POS Raster 字节 golden、模板输出和非法 ASCII |
| 设备验证 | 未执行；当时缺少的 USS-49 边界几何已在 R124 补齐，真实热敏/针式打印与扫描仍需设备验收 |

本轮关闭 Code 49 主机编码与 Raster 内部分隔绘制子范围；完整 USS-49 边界/静区随后在 R124 完成，完整 GS1 语法及设备验证仍属 TODO。

## R122 验证补充：跨协议常用压缩 AI

本轮将 Raster 已有的 `310x`/`320x`、`3920`–`3923`、`3930`–`3933` 输入形态接入共享解析器，验证 ESC/POS Native、TSPL EAN128、ZPL GS1-128/DataBar Expanded 的直接命令、模板宽度预检、FNC1 转义和失败恢复。该轮只开放语法归一化，不把原生协议输出描述为 Raster 压缩位流。

| 门禁 | 结果 |
| --- | --- |
| Native/TSPL/ZPL 定向回归 | 四个协议测试类：`net6.0`、`netcoreapp3.1` 各 `97/97` |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2957/2957`，零失败、零跳过 |
| Core/Templates L0 构建 | 五目标框架构建通过，0 警告、0 错误 |
| 覆盖范围 | 固定六位重量、价格/货币长度、变长字段 FNC1、Native/TSPL/ZPL 转义、模板宽度预检和非法输入无污染 |
| 设备验证 | 未执行；AI 业务解释、固件压缩语义、HRI、DPI 和热敏/针式扫描仍需设备验收 |

本轮关闭“已声明压缩 AI 子集的跨协议输入规范化”本地子范围；其它压缩 AI、完整 GS1 字典及 Raster Stacked/Composite/Code 49 仍按 TODO 处理。

## R121 验证补充：多协议括号 AI 规范化

本轮将当前声明的 GS1 AI 子集接入 ESC/POS Native、TSPL EAN128 和 ZPL GS1-128/DataBar Expanded。至少两个括号 AI 元素的输入由共享解析器校验并转换为原始 AI/FNC1，随后由目标协议分别编码；单元素历史括号值保持旧输出。

| 门禁 | 结果 |
| --- | --- |
| Native/TSPL/ZPL 定向回归 | `EscPosNativeDataBarTests`、`TsplUccEan128ProviderTests`、`ZplUccEan128ProviderTests`、`ZplDataBarProviderTests`：`net6.0` 合计 `83/83` |
| 覆盖范围 | 圆括号多元素、变长字段 FNC1、Epson/TSPL/ZPL 转义、未知 AI 严格失败、单括号兼容回归 |
| 共享校验 | AI 字段长度、数字/CSET 82、括号样式和 255 字节原生 payload 上限 |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2943/2943`，零失败、零跳过 |
| Core/Templates L0 构建 | 五目标框架构建通过，0 警告、0 错误 |
| 设备验证 | 未执行；不同固件对 FNC1、HRI 和括号展示的解释仍需热敏/针式设备验收 |

本轮关闭“已声明常用 AI 子集的跨协议输入规范化”本地子范围；完整 GS1 字典、业务关联/链接标志、Stacked/Composite 和 Code 49 仍按 TODO 处理。

## R120 验证补充：GTIN 加常用括号 AI 序列

本轮开放 `(01)`/`[01]` GTIN 后的常用 AI 序列。变长字段在后续 AI 前插入 ASCII 29/FNC1，定长字段直接拼接；压缩 AI 仍走既有 Method 3–14 识别。GTIN 校验位、未知 AI、字段长度和括号样式错误在矩阵预算前失败。

| 门禁 | 结果 |
| --- | --- |
| Expanded GTIN AI 精准回归 | `EscPosRasterGs1DataBarExpandedTests`：`net6.0`、`netcoreapp3.1` 各 `91/91` |
| 覆盖范围 | 圆括号/方括号等价位流、变长/定长 AI、GTIN 校验、HRI、直接 Raster、模板和预算前非法输入 |
| 规范参考 | [GS1 General Specifications 24.0.0](https://ref.gs1.org/standards/genspecs/24.0.0/) |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2934/2934`，零失败、零跳过 |
| Core/Templates 构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误 |
| 设备验证 | 未执行；完整 GS1 字典、业务关联、链接标志和真实热敏/针式扫描仍需后续验收 |

本轮关闭 Raster Expanded GTIN 加常用 AI 的本地规范化子范围；完整跨协议输入契约和其他 Raster 变体继续按 TODO 处理。

## R119 验证补充：日期后的括号 AI 尾部

本轮开放日期 Method 7–14 后的常用括号 AI 尾部。变长 AI 在后续元素前插入 ASCII 29/FNC1，定长 AI 直接拼接；额外日期 AI、未知 AI、压缩 AI、长度错误和混用括号在矩阵预算前失败。直接 Raster、HRI、宽度和模板均复用同一规范化路径。

| 门禁 | 结果 |
| --- | --- |
| Expanded 日期尾部精准回归 | `EscPosRasterGs1DataBarExpandedTests`：`net6.0`、`netcoreapp3.1` 各 `85/85` |
| 覆盖范围 | Method 7–14 原始尾部回归、圆括号/方括号 AI 等价位流、FNC1、HRI、宽度、直接 Raster、模板和预算前非法输入 |
| 规范参考 | [GS1 General Specifications 24.0.0](https://ref.gs1.org/standards/genspecs/24.0.0/) |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2928/2928`，零失败、零跳过 |
| Core/Templates 构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误 |
| 设备验证 | 未执行；完整 AI 业务关联、链接标志、Stacked/Composite 和真实热敏/针式扫描仍需后续验收 |

本轮关闭日期尾部常用括号 AI 的本地编码子范围；完整 GS1 字典、多日期业务组合、跨协议统一输入契约和外部设备证据继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R118 验证补充：ESC/POS Raster GS1 DataBar Expanded 括号 AI 规范化

本轮增加常用 GS1 AI 的圆括号/方括号解析。变长 AI 在后续元素前规范化为 ASCII 29/FNC1，定长 AI 直接拼接；未知 AI、定长/变长长度错误、数字或 CSET 82 字符错误、括号不匹配均在矩阵预算前失败。直接 Raster 和模板使用相同规范化结果，HRI 保留括号展示文本。

| 门禁 | 结果 |
| --- | --- |
| Expanded 括号 AI 精准回归 | `EscPosRasterGs1DataBarExpandedTests`：`net6.0`、`netcoreapp3.1` 各 `77/77` |
| 计划覆盖 | Method 2 括号/方括号等价 golden、固定/变长末字段、HRI、宽度、直接 Raster、模板和预算前非法输入 |
| 规范参考 | [GS1 syntax dictionary](https://github.com/gs1/gs1-syntax-dictionary/blob/2026-01-27/gs1-syntax-dictionary.txt)、[GS1 General Specifications](https://ref.gs1.org/standards/genspecs/23.0.0/) |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2920/2920`，零失败、零跳过 |
| Core/Templates 构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误 |
| 设备验证 | 未执行；当前子集不代表完整 GS1 AI 字典、链接标志或真实热敏/针式扫描验收 |

本轮关闭 Raster Expanded 常用括号 AI 到 FNC1 的本地子范围；完整字典、业务关联、多协议统一输入契约、Stacked/Composite、Code 49、Native 全业务 payload、DesignerHost 隔离重建和性能容量仍待后续 TODO。

## R117 验证补充：ESC/POS Raster GS1 DataBar Expanded 日期后的通用字段

本轮开放 Method 7/8、9–14 日期压缩字段后的原始通用字段。日期后的尾部从压缩数据索引 34 开始复用通用字段状态机、FNC1 分隔、动态宽度、HRI、直接 Raster 和模板路径；尾部结构化方括号、末尾或连续 FNC1 仍严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| 日期尾部精准回归 | `EscPosRasterGs1DataBarExpandedTests`：`net6.0`、`netcoreapp3.1` 各 `68/68` |
| 覆盖范围 | Method 7、Method 10 golden、括号/方括号等价输入、FNC1 尾部、HRI、宽度、直接 Raster、模板和非法尾部 |
| 规范参考 | [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2911/2911`，零失败、零跳过 |
| Core/Templates 构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误 |
| 真实设备 | 未执行；固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描仍需后续验收 |

本轮关闭日期压缩字段后追加原始通用字段的本地子范围；完整 AI 元数据解析、跨协议括号规范化、链接标志、Stacked/Composite、Code 49、Native 全业务 payload、DesignerHost 隔离重建和性能容量继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R116 验证补充：ESC/POS Raster GS1 DataBar Expanded Method 2 FNC1 数据

本轮增加 Method 2 的原始 GS1 通用字段输入。输入以两位数字 AI 开头并包含 ASCII 29/FNC1 分隔符，主机写入 `0000` 方法字段，再复用 Numeric、Alphanumeric、ISO/IEC 646 和 FNC1 状态机、动态宽度、HRI、Raster 页面、直接命令和统一模板路径。括号/方括号展示语法、末尾或连续 FNC1、旧的裸 14 位非 GTIN 输入继续严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| Expanded Method 2 精准回归 | `EscPosRasterGs1DataBarExpandedTests`：`net6.0`、`netcoreapp3.1` 各 `64/64` |
| 覆盖范围 | Method 2 golden、FNC1 输入校验、HRI、宽度、直接 Raster 命令和模板页面 |
| 规范参考 | [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)、[libzint general_field.c](https://raw.githubusercontent.com/zint/zint/master/backend/general_field.c) |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2907/2907`，零失败、零跳过 |
| Core/Templates 构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误 |
| 真实设备 | 未执行；固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描仍需后续验收 |

本轮关闭 Raster Expanded Method 2 的显式 FNC1 原始输入子范围；完整 AI 元数据解析、括号到 FNC1 的跨协议规范化、链接标志、Stacked/Composite、Code 49、Native 全业务 payload、DesignerHost 隔离重建和性能容量继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R115 验证补充：ESC/POS Raster GS1 DataBar Expanded Method 9–14 日期字段

本轮增加 `(310x)`/`[310x]`、`(320x)`/`[320x]` 后接 `(11)/(13)/(15)/(17)` 日期 AI 的 Method 9–14 编码。日期 AI 映射到 `0111000`–`0111111` 方法位，压缩字段使用 20 位重量和 16 位 `yy*384+(mm-1)*32+dd` 日期值；日 `00` 保留 GS1 月末语义，日期后的额外字段和未知两位 AI 严格拒绝。HRI、动态符号宽度、矩阵预算、Raster 页面、直接命令和模板路径复用既有 Expanded 实现。

| 门禁 | 结果 |
| --- | --- |
| Expanded Method 9–14 精准回归 | `EscPosRasterGs1DataBarExpandedTests`：`net6.0`、`netcoreapp3.1` 各 `57/57` |
| 覆盖范围 | 八个方法 golden、括号/方括号等价输入、日期 AI 映射、月份/日期边界、HRI、宽度、直接 Raster 命令和模板页面 |
| 规范参考 | [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2900/2900`，零失败、零跳过 |
| Core/Templates 构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误 |
| 真实设备 | 未执行；固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描仍需后续验收 |

本轮关闭 Raster Expanded Method 9–14 的显式日期字段；日期后的通用字段、其它结构化 AI、链接标志、Stacked/Composite、Code 49、跨协议 GS1 输入契约、Native 全业务 payload、DesignerHost 隔离重建和性能容量继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R114 验证补充：ESC/POS Raster GS1 DataBar Expanded Method 7/8

本轮增加没有日期后缀的 `(310x)`/`[310x]` 与 `(320x)`/`[320x]` Method 7/8 主机编码。`(3103)` 超出 `032767` 或其它 `(310x)` 选择 Method 7，`(3202)/(3203)` 超出 Method 4 范围或其它 `(320x)` 选择 Method 8；压缩字段使用 `0111000`/`0111001` 方法位、20 位重量和固定 `38400` 日期。HRI、动态符号宽度、矩阵预算、Raster 页面、直接命令和模板路径复用既有 Expanded 实现，显式日期后缀、Method 9–14、Stacked/Composite 和 Code 49 仍严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| Expanded Method 7/8 精准回归 | `EscPosRasterGs1DataBarExpandedTests`：`net6.0`、`netcoreapp3.1` 各 `49/49` |
| 覆盖范围 | Method 7/8 golden、`[01]/[310x]/[320x]` 等价输入、Method 3/4 选择边界、重量上限、HRI、宽度、直接 Raster 命令和模板页面 |
| 规范参考 | [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2892/2892`，零失败、零跳过 |
| Core/Templates 构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误 |
| 真实设备 | 未执行；固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描仍需后续验收 |

本轮关闭 Raster Expanded Method 7/8 的无日期重量子范围；Method 9–14、显式日期输入、Stacked/Composite、Code 49、跨协议 GS1 输入契约、Native 全业务 payload、DesignerHost 隔离重建和性能容量继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R113 验证补充：ESC/POS Raster GS1 DataBar Expanded Method 5/6

本轮增加显式 `(392x)`/`[392x]` 本地价格和 `(393x)`/`[393x]` ISO 4217 货币+价格的 Method 5/6 主机编码，`x` 为 `0-3`。压缩字段分别使用 `01100xx`/`01101xx` 方法位；Method 6 追加 10 位货币代码，价格按本轮严格输入契约要求为非空 ASCII 数字。HRI、动态符号宽度、矩阵预算、Raster 页面、直接命令和模板路径复用既有 Expanded 实现，其它 Method 7–14、结构化 AI、Stacked/Composite 和 Code 49 仍严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| Expanded Method 5/6 精准回归 | `EscPosRasterGs1DataBarExpandedTests`：`net6.0`、`netcoreapp3.1` 各 `49/49` |
| 覆盖范围 | Method 5/6 golden、`[01]/[392x]/[393x]` 等价输入、货币/价格边界、非法结构化 AI、HRI、宽度、直接 Raster 命令和模板页面 |
| 规范参考 | [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2892/2892`，零失败、零跳过 |
| Core/Templates 构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误 |
| 真实设备 | 未执行；固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描仍需后续验收 |

本轮关闭 Raster Expanded Method 5/6 本地价格和货币+价格子范围；其它 Expanded 方法、Stacked/Composite、Code 49、跨协议 GS1 输入契约、Native 全业务 payload、DesignerHost 隔离重建和性能容量继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R112 验证补充：ESC/POS Raster GS1 DataBar Expanded Method 4

本轮增加显式 `(3202)`/`[3202]` 和 `(3203)`/`[3203]` 英制重量的 Method 4 主机编码。压缩字段使用固定 `0101` 方法位、GTIN triplet 和 15 位重量，`3203` 按规范加 `10000`；通用字段、动态符号宽度、HRI、矩阵预算、Raster 页面和模板路径复用 R111/R110 实现，其它重量/日期/价格 AI、Method 5–14、Stacked/Composite 和 Code 49 仍严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| Expanded Method 4 精准回归 | `EscPosRasterGs1DataBarExpandedTests`：`net6.0`、`netcoreapp3.1` 各 `41/41` |
| 覆盖范围 | Method 4 golden、`[01]/[3202]/[3203]` 等价输入、重量上下限、非法重量、未开放结构化 AI 拒绝、通用字段、HRI、宽度、直接命令、Raster 页面和模板页面 |
| 规范参考 | [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2884/2884`，零失败、零跳过 |
| Core/Templates 构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误 |
| 真实设备 | 未执行；固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描仍需后续验收 |

本轮关闭 Raster Expanded Method 4 英制重量子范围；其它 Expanded 方法、Stacked/Composite、Code 49、跨协议 GS1 输入契约、Native 全业务 payload、DesignerHost 隔离重建和性能容量继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R111 验证补充：ESC/POS Raster GS1 DataBar Expanded Method 3

本轮增加显式 `(3103)`/`[3103]` 公斤重量的 Method 3 主机编码。压缩字段使用固定 `0100` 方法位、GTIN triplet 和 15 位重量，通用字段、动态符号宽度、HRI、矩阵预算、Raster 页面和模板路径复用 R110 实现；其它重量/日期/价格 AI、Method 4–14、Stacked/Composite 和 Code 49 仍严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| Expanded Method 3 精准回归 | `EscPosRasterGs1DataBarExpandedTests`：`net6.0`、`netcoreapp3.1` 各 `31/31` |
| 覆盖范围 | Method 3 golden、`[01]/[3103]` 等价输入、15 位重量范围、非法重量、HRI、宽度、直接 Raster 和模板页面 |
| 规范参考 | [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2874/2874`，零失败、零跳过 |
| Core/Templates 构建 | `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误 |
| 真实设备 | 未执行；固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描仍需后续验收 |

本轮关闭 Raster Expanded Method 3 公斤重量子范围；其它 Expanded 方法、Stacked/Composite、Code 49、跨协议 GS1 输入契约、Native 全业务 payload、DesignerHost 隔离重建和性能容量继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R110 验证补充：ESC/POS Raster GS1 DataBar Expanded 通用字段

本轮在保留 R109 数字字段和 R108 五符号 GTIN-only 134 模块串的前提下，增加 Numeric、Alphanumeric、ISO/IEC 646 通用字段状态机以及 FNC1 转换。实现覆盖数字成对压缩、模式 latch、末位数字、动态 checksum/finder 表、HRI 控制符清理、预算和模板路径；其它 AI/压缩方法、Stacked/Composite 和 Code 49 仍严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| Expanded 精准回归 | `EscPosRasterGs1DataBarExpandedTests`：`net6.0`、`netcoreapp3.1` 各 `26/26` |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2869/2869`，零失败、零跳过 |
| Core 构建 | `netcoreapp3.1`、`net6.0`、`net7.0`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误 |
| Templates 构建 | `netcoreapp3.1`、`net6.0`、`net7.0`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误 |
| 覆盖范围 | 134/151 模块数字 golden、200 模块字母数字 golden、249 模块 ISO/FNC1 golden、canonical 输入、HRI、动态宽度、预算回滚、直接命令、四方向 Provider、模板、非法字段 |
| 规范参考 | [libzint general_field.c](https://raw.githubusercontent.com/zint/zint/master/backend/general_field.c)、[libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)、[libzint rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h) |
| 真实设备 | 未执行；完整 CSET/其它 AI、Stacked/Composite、固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描仍需后续验收 |

本轮关闭 Raster Expanded 的通用字段字符集子范围；完整 Expanded、Stacked/Composite、Code 49、跨协议 GS1 输入契约、Native 全业务 payload、DesignerHost 隔离重建和性能容量仍按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R109 验证补充：ESC/POS Raster GS1 DataBar Expanded 数字通用字段

本轮在保留 R108 五符号 GTIN-only 134 模块串的前提下，增加 `(01)` GTIN-14 后纯数字通用字段的 Method 1 主机编码。实现覆盖 7-bit 数字成对压缩、末位数字分支、Numeric padding、动态 checksum/finder 表、变量符号宽度、预算和模板路径；字母、ISO、ASCII 29/FNC1、其它 AI、Stacked/Composite 仍严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| Expanded 数字字段精准回归 | `EscPosRasterGs1DataBarExpandedTests`：`net6.0`、`netcoreapp3.1` 各 `21/21` |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2864/2864`，零失败、零跳过 |
| Core/Templates 构建 | 五个目标框架，0 警告、0 错误 |
| 覆盖范围 | 134 模块兼容 golden、151 模块数字字段 golden、canonical 输入、HRI、动态宽度、预算回滚、直接命令、非法字段 |
| 规范参考 | [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)、[general_field.c](https://raw.githubusercontent.com/zint/zint/master/backend/general_field.c)、[rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h) |
| 真实设备 | 未执行；完整 CSET/其它 AI、Stacked/Composite、固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描仍需后续验收 |

本轮关闭 Expanded 数字通用字段子范围；完整 Expanded、Stacked/Composite、Code 49、跨协议 GS1 输入契约、Native 全业务 payload、DesignerHost 隔离重建和性能容量仍按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R108 验证补充：ESC/POS Raster GS1 DataBar Expanded GTIN-only

本轮将 `BarcodeType.Gs1DataBarExpanded` 接入 ESC/POS Raster 主机编码的严格 GTIN-only 子集。输入接受 `(01)`、`[01]` 或裸 `01` 加 14 位 GTIN-14；主机按 Method 1 生成压缩字段、check character、finder pattern 和 134 模块线性位图，再输出 `GS v 0`。

| 门禁 | 结果 |
| --- | --- |
| Expanded 精准回归 | `EscPosRasterGs1DataBarExpandedTests` + `EscPosNativeDataBarTests`：`net6.0`、`netcoreapp3.1` 各 `30/30` |
| Core 全量回归 | `net6.0`、`netcoreapp3.1` 各 `2858/2858`，零失败、零跳过 |
| Core/Templates 构建 | 五个目标框架，0 警告、0 错误 |
| 覆盖范围 | golden pattern、三种 canonical 输入、GTIN 校验、非法 AI、预算回滚、直接命令、四方向、HRI、模板、页面边界和能力快照 |
| 规范参考 | [GS1 DataBar](https://www.gs1.org/standards/barcodes/databar)、[GS1 General Specifications](https://ref.gs1.org/standards/genspecs/11.0.0/)、[libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)、[rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h) |
| 真实设备 | 未执行；Expanded 其它 AI/通用字段、Stacked/Composite、固件、DPI、纸宽、条高、HRI 字形和热敏/针式扫描质量仍需后续验收 |

本轮关闭 Raster Expanded 的 GTIN-only 主机编码子范围；完整 Expanded、Stacked/Composite、Code 49、统一 GS1 输入契约、Native 全业务 payload、DesignerHost 隔离重建、性能容量和真实硬件继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R107 验证补充：ESC/POS Raster China Post

本轮将 `BarcodeType.ChinaPost` 接入 ESC/POS Raster 主机编码。输入严格为非空 ASCII 数字；主机沿 Matrix 2 of 5 数字模式输出 Datalogic/China Post 的两窄条起始、宽条/窄条结束、字符间窄空白和固定 3:1 宽窄比，不追加可选校验位，再输出 `GS v 0` 位图。TSPL 原生 CPOST 的既有 1:1 设备语义保持不变。

| 门禁 | 结果 |
| --- | --- |
| Raster/TSPL China Post 精准回归 | `EscPosRasterChinaPostTests` + `TsplChinaPostProviderTests`：`net6.0`、`netcoreapp3.1` 各 `30/30` |
| 覆盖范围 | 起止符/数字表 golden pattern、1/10/20 位长度、非法输入、预算恢复、直接命令、四方向、模板、页面边界和能力快照 |
| 规范参考 | [TCPDF DatalogicTwoOfFive](https://tcpdf.org/docs/srcdoc/tc-lib-barcode/classes-Com-Tecnick-Barcode-Type-Linear-DatalogicTwoOfFive/)、[Seagull Barcode Guide](https://barcodeguide.seagullscientific.com/Content/Symbologies/Datalogic_2_of_5.htm)、[Seagull ratio options](https://help.seagullscientific.com/12.0/en/Content/symb_Datalogic2of5.htm) |
| 真实设备 | 未执行；固件、DPI、宽窄比、条高、纸宽、HRI 字形和热敏/针式扫描质量仍需设备验收 |

本轮关闭 Raster China Post 主机编码子范围；Expanded、Stacked/Composite、Code 49、GS1 14 位/AI/复合输入契约、Native 全业务 payload、DesignerHost 隔离重建、性能容量和真实硬件继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R106 验证补充：ESC/POS Raster GS1 DataBar Limited

本轮将 `BarcodeType.Gs1DataBarLimited` 接入 ESC/POS Raster 主机编码。输入严格沿统一模型限制为 13 位 ASCII 数字且首位为 0 或 1；主机按 GS1 DataBar Limited 的两组数据 pair、7 元素宽度、Mod-89 校验和 89 个 finder 模式生成固定 79 模块一维串，再输出 `GS v 0` 位图。开启 HRI 时统一显示 `(01)`、13 位 payload 和 GTIN 校验位。14 位含校验输入、AI 前缀和复合链接字段继续严格不开放。

| 门禁 | 结果 |
| --- | --- |
| Raster Limited 精准回归 | `EscPosRasterGs1DataBarLimitedTests`：`net6.0` `16/16`、`netcoreapp3.1` `16/16` |
| 既有 DataBar 组合回归 | `EscPosRasterGs1DataBarTests`、`EscPosNativeDataBarTests` 与 Limited 合并筛选：`net6.0` `46/46` |
| Core 完整回归 | `net6.0` `2829/2829`、`netcoreapp3.1` `2829/2829`，零失败、零跳过 |
| Core/Templates 构建 | 五个目标框架，0 警告、0 错误；测试构建阶段仅有既有 `NU1900` 漏洞源不可达警告 |
| 覆盖范围 | 79 模块 golden pattern、边界 payload、首位/长度/字符非法输入、预算恢复、直接命令、四方向、模板、页面布局和能力快照 |
| 规范参考 | [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)、[rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h)（BSD-3-Clause 归因）；结构尺寸参考 [GS1 General Specifications](https://ref.gs1.org/standards/genspecs/23.0.0/) |
| 真实设备 | 未执行；固件、DPI、条高合规、纸宽、HRI 字形和热敏/针式扫描质量仍需设备验收 |

本轮关闭 Raster GS1 DataBar Limited 主机编码子范围；Expanded、Stacked/Composite、China Post、Code 49、14 位/AI 输入契约、Native 全业务 payload、DesignerHost 隔离重建、性能容量和真实硬件继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R105 验证补充：ESC/POS Raster GS1 DataBar Omnidirectional/Truncated

本轮将 `BarcodeType.Gs1DataBarOmnidirectional` 与 `BarcodeType.Gs1DataBarTruncated` 接入 ESC/POS Raster 主机编码。输入严格沿统一模型限制为 13 位 ASCII 数字 payload；主机按 GS1 DataBar/RSS-14 的数据字符组合、finder pattern 和 Mod-79 校验生成固定 96 模块一维串，再输出 `GS v 0` 位图。开启 HRI 时统一显示隐含 `(01)`、13 位 payload 和 GTIN 校验位。两个变体横向结构相同，条高语义继续由 Provider/模板布局表达；Limited、Expanded、Stacked/Composite、China Post 和 Code 49 仍严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| Raster DataBar 精准回归 | `EscPosRasterGs1DataBarTests` 与 Native DataBar 能力回归：`net6.0` `30/30`、`netcoreapp3.1` `30/30` |
| Core 完整回归 | `net6.0` `2813/2813`、`netcoreapp3.1` `2813/2813`，零失败、零跳过 |
| Core/Templates 构建 | 五个目标框架，0 警告、0 错误 |
| 覆盖范围 | 96 模块 golden pattern、边界 payload、非法输入、预算恢复、直接命令、四方向、模板、页面边界和能力快照 |
| 规范参考 | [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c)、[rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h)（BSD-3-Clause 归因）；结构尺寸参考 [GS1 General Specifications](https://ref.gs1.org/standards/genspecs/23.0.0/) |
| 真实设备 | 未执行；固件、DPI、条高合规、纸宽、HRI 字形和热敏/针式扫描质量仍需设备验收 |

本轮关闭 Raster GS1 DataBar Omnidirectional/Truncated 主机编码子范围；其他 DataBar 变体、China Post、Code 49、Native 全业务 payload、DesignerHost 隔离重建、性能容量和真实硬件继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R104 验证补充：ESC/POS Raster Telepenn Numeric

本轮将 `BarcodeType.Telepenn` 接入 ESC/POS Raster 主机编码。输入沿现有统一模型限制为 1–60 位 ASCII 数字，奇数位前补零后按两位压缩为一个 Telepen Numeric glyph，按 glyph 求 Mod-127 校验并输出固定 16 模块/符号的 `GS v 0` 位图；ESC/POS Native 仍严格拒绝原生 Telepenn。

| 门禁 | 结果 |
| --- | --- |
| Raster Telepenn 精准回归 | `EscPosRasterTelepennTests`：`net6.0` `14/14`、`netcoreapp3.1` `14/14` |
| TSPL Telepen 既有回归 | `TsplTelepenProviderTests`：`net6.0` `37/37`、`netcoreapp3.1` `37/37` |
| Core 完整回归 | `net6.0` `2798/2798`、`netcoreapp3.1` `2798/2798`，零失败、零跳过 |
| Core/Templates 构建 | 五个目标框架，0 警告、0 错误 |
| 覆盖范围 | 奇数位补零、00–99 glyph、Mod-127 校验、60 位上限、非法输入、预算恢复、直接命令、四方向、模板和能力快照 |
| 规范参考 | [libzint telepen.c](https://github.com/zint/zint/blob/master/backend/telepen.c)；本项目保持既有纯数字边界 |
| 真实设备 | 未执行；固件 Telepenn、HRI 字形、DPI、条高、纸宽和扫描质量仍需热敏/针式设备验收 |

本轮关闭 Raster Telepenn Numeric 主机编码子范围；GS1 DataBar、China Post、Code 49、Native 未映射制式、DesignerHost、性能容量和真实硬件继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R103 验证补充：ESC/POS Raster Telepen Alpha

本轮将 `BarcodeType.Telepen` 接入 ESC/POS Raster 主机编码。输入为 1–30 个 7-bit ASCII 字符，按 Telepen Alpha 的起始符、payload、Mod-127 校验符和结束符生成固定 16 模块/符号的 `GS v 0` 位图；Telepenn Numeric 已在 R104 接入同一路径，ESC/POS Native 仍严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| Raster Telepen 精准回归 | `EscPosRasterTelepenTests`：`net6.0` `13/13`、`netcoreapp3.1` `13/13` |
| TSPL Telepen 既有回归 | `TsplTelepenProviderTests`：`net6.0` `37/37`、`netcoreapp3.1` `37/37` |
| Core 完整回归 | `net6.0` `2784/2784`、`netcoreapp3.1` `2784/2784`，零失败、零跳过 |
| 覆盖范围 | Mod-127 校验、起止符、30 字符上限、非法输入、预算恢复、直接命令、四方向、模板和能力快照 |
| 规范参考 | [libzint telepen.c](https://github.com/zint/zint/blob/master/backend/telepen.c)；表格归属 BSD-3-Clause |
| 真实设备 | 未执行；固件 Telepen、HRI 字形、DPI、条高、纸宽和扫描质量仍需热敏/针式设备验收 |

本轮关闭 Raster Telepen Alpha 主机编码子范围；Telepenn Numeric 已在 R104 完成，GS1 DataBar、China Post、Code 49、Native 未映射制式、DesignerHost、性能容量和真实硬件继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R102 验证补充：ESC/POS Native profile 矩阵

本轮把 17 个 ESC/POS Native Function B 65–79 条码制式与设备 profile 约束组合验证。完整 HRI allowlist profile 下，Rows 模板逐项输出对应 GS k；关闭原生条码、收窄 HRI 条码制式或收窄 HRI 位置时，模板在输出前按元素路径严格失败。

| 门禁 | 结果 |
| --- | --- |
| Native profile 矩阵精准回归 | `TemplateEscPosNativeFullMatrixTests`：`net6.0` `12/12`、`netcoreapp3.1` `12/12` |
| Core 完整回归 | `net6.0` `2771/2771`、`netcoreapp3.1` `2771/2771`，零失败、零跳过 |
| 覆盖范围 | 17 个原生一维条码、完整 allowlist、原生条码总开关、HRI 制式 allowlist、HRI 位置 allowlist |
| 真实设备 | 未执行；profile 只描述目标设备，不证明固件、HRI 字形、DPI、条高或扫描质量 |

本轮关闭 Native profile 组合的本地验证缺口；所有业务 payload/固件的无限笛卡尔组合、真实设备和性能容量仍按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R101 验证补充：ESC/POS Raster Code 11 主机编码

本轮把 `BarcodeType.Code11` 接入 ESC/POS Raster 主机编码路径。输入限制为 1–255 个 ASCII 数字或连字符，主机按 payload 长度追加 Code 11 C/K 校验位，再以完整起止星号和窄空白模块串输出 `GS v 0`；独立 Raster 命令、模板 Provider、能力快照、宽度测量、旋转、HRI、页面边界和失败恢复共用同一路径。ESC/POS Native 的 GS k 原生命令仍保持严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| Raster Code 11 精准回归 | `EscPosRasterCode11Tests`：`net6.0` `13/13`、`netcoreapp3.1` `13/13` |
| Core 完整回归 | `net6.0` `2768/2768`、`netcoreapp3.1` `2768/2768`，零失败、零跳过 |
| Core/Templates L0 构建 | 五个目标框架构建通过，0 警告、0 错误 |
| 输入与失败恢复 | 短 payload 单 C、长 payload C+K、非法/超长输入、矩阵预算释放和模板元素路径通过 |
| 能力范围 | ESC/POS Raster `SupportsBarcodeType=true`、`SupportsCommandBarcodeType=false`；ESC/POS Native 仍为 false |
| 真实设备 | 未执行；固件 Code 11、HRI 字形、DPI、条高、纸宽、打印和扫描质量仍需热敏/针式设备验收 |

本轮关闭 Raster Code 11 主机编码子范围；GS1 DataBar、China Post、Code 49 等仍因缺少可靠主机编码器保持严格拒绝，Telepen Alpha/Numeric 已在 R103/R104 完成。Native 完整静区/旋转/逐制式 payload/跨页/profile 笛卡尔矩阵、DesignerHost 干净隔离重建、性能容量、发送重试和真实硬件继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R100 验证补充：ESC/POS Native 数值边界矩阵

本轮新增 `EscPosNativeNumericBoundaryMatrixTests`，把模板能力矩阵中的 17 个 ESC/POS Native 一维条码类型映射到 Function B 65–79；I2OF5、I2OF5C、I2OF5G 共享 70，但分别验证其直接命令载荷。每种类型覆盖模块宽度 `2`/`6`、条高 `1`/`255`、HRI 位置 `0`/`1`/`2`/`3` 的 4 组数值端点，并检查 GS H、GS h、GS w、GS k 顺序、长度字段和重复构建；条高 0、模块宽度 1/7、HRI 位置 4 均验证失败后原缓冲区不变。

| 门禁 | 结果 |
| --- | --- |
| Native 数值边界精准回归 | `EscPosNativeNumericBoundaryMatrixTests`：`net6.0` `3/3`、`netcoreapp3.1` `3/3`；包含 68 个合法端点组合和 68 个非法恢复场景 |
| Core 完整回归 | `net6.0` `2755/2755`、`netcoreapp3.1` `2755/2755`，零失败、零跳过 |
| 能力与命令范围一致性 | `CommandBarcodeModuleWidth=2..6`、`CommandBarcodeHeight=1..255`，17 个条码类型逐项通过能力断言 |
| 生产代码范围 | 本轮只增加测试证据，未改变 Native GS k 输出、公开 API、目标框架或包配置 |
| 真实设备 | 未执行；固件条码实现、HRI 字形、DPI、条高、纸宽、扫描质量和针式打印机兼容性仍需设备验收 |

本轮关闭 Native 数值端点和失败恢复的证据子范围；完整静区/旋转/逐制式 payload/跨页/profile 笛卡尔矩阵、HRI 字体样式、未映射 Raster 类型、性能容量、发送重试和真实硬件仍按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R99 验证补充：ESC/POS Raster Plessey 主机编码子范围

本轮将 `BarcodeType.Plessey` 接入 ESC/POS Raster 主机编码路径。ZXing.Net `PlesseyWriter` 生成的位序在主机侧去除默认空白边界，再由统一 Raster 画布按模块宽度、旋转和 HRI 预留输出；独立命令与模板 Provider 共用同一条规范化路径。

| 门禁 | 结果 |
| --- | --- |
| Plessey/Raster 精准回归 | `EscPosRasterBarcodeTypeTests`、`TsplPlesseyProviderTests`、`ZplPlesseyProviderTests`：`net6.0` `83/83`、`netcoreapp3.1` `83/83` |
| Core 完整回归 | `net6.0` `2752/2752`、`netcoreapp3.1` `2752/2752`，零失败、零跳过 |
| Core/Templates L0 构建 | `Bing.EasyPrint` 与 `Bing.EasyPrint.Templates` 五目标框架构建通过，0 警告、0 错误 |
| 输入与失败恢复 | 非空 ASCII 数字校验、模块宽度缩放、直接命令、模板元素路径和能力快照通过；无效输入在矩阵变更前失败 |
| 真实设备 | 未执行；Plessey 扫描、DPI、条高、纸宽、HRI 和热敏/针式打印质量仍需设备验收 |

本轮关闭 Raster Plessey 主机编码子范围；不把该能力扩展为 ESC/POS GS k 原生 Plessey 证明，也不宣称 Native 数值笛卡尔矩阵、未映射变体、性能容量、发送重试或真实硬件已完成。

## R98 验证补充：ESC/POS Raster ITF-14 主机编码子范围

本轮将 `BarcodeType.Itf14` 接入 ESC/POS Raster 主机编码路径。输入为 13 位数字 payload，主机按既有 Mod10 规则生成第 14 位校验位，再复用 ITF 位图；独立 Raster 命令、模板 Provider、能力快照和页面布局共用该规范化路径。

| 门禁 | 结果 |
| --- | --- |
| Raster 条码精准回归 | `EscPosRasterBarcodeTypeTests`：`net6.0` `38/38`、`netcoreapp3.1` `38/38` |
| Core 完整回归 | `net6.0` `2747/2747`、`netcoreapp3.1` `2747/2747`，零失败、零跳过 |
| Core/Templates L0 构建 | `Bing.EasyPrint` 与 `Bing.EasyPrint.Templates` 五目标框架构建通过，0 警告、0 错误 |
| 输入与失败恢复 | 13 位 payload 生成的 14 位 ITF 与基础 I2OF5 宽度/位图一致；空值、长度错误和非数字输入在编码前失败 |
| 真实设备 | 未执行；ITF-14 bearer bars、DPI、条高、静区和扫描质量仍需热敏/针式设备验收 |

FIM D/E 已在既有 Raster 主机编码和测试中支持；后文旧 R26 历史段落中的“D/E 严格拒绝”不代表当前能力矩阵。

## R97 验证补充：ESC/POS Raster Code128 auto 主机光栅子范围

本轮将 `BarcodeType.Code128Auto` 接入 ESC/POS Raster 主机编码路径。Raster 复用 Code 128 自动分组矩阵，统一模板、能力快照和独立 `RasterBarcode` 入口共享 1–255 字节 7-bit ASCII 校验；Native 的 Function B 79 原始 `byte[]` 入口保持原语义。

| 门禁 | 结果 |
| --- | --- |
| Raster 条码精准回归 | `EscPosRasterBarcodeTypeTests`：`net6.0` `31/31`、`netcoreapp3.1` `31/31` |
| Core 完整回归 | `net6.0` `2740/2740`、`netcoreapp3.1` `2740/2740`，零失败、零跳过 |
| Core/Templates L0 构建 | `Bing.EasyPrint` 与 `Bing.EasyPrint.Templates` 五目标框架构建通过，0 警告、0 错误 |
| 生产代码范围 | `EscPosRasterCanvas` 支持集合、Code 128 格式映射、输入规范化和能力传播；未修改版本、TFM、包配置 |
| 失败恢复 | 空值/非 ASCII 输入在画布变更前失败；结束空白 Raster 页后输出与无条码基线一致 |
| 真实设备 | 未执行；Raster 墨迹、DPI、纸宽、条高和扫描质量仍需热敏/针式设备验收 |

本轮关闭 Raster Code128 auto 的主机矩阵、宽度、能力快照、模板/独立入口和失败恢复子范围；不把它扩展为 Native 任意二进制 Raster 化证明，也不宣称 Native 条码全部数值笛卡尔矩阵、HRI 字体样式、未映射类型、性能容量或真实设备已完成。

## R96 验证补充：TSPL/ZPL 原生二维码旋转白名单

本轮把 TSPL/ZPL 原生二维码方向从协议默认能力细化为可选机型 profile 白名单，并贯通 `PrintCapabilities`、模板能力快照、Provider 预检、TSPL `QRCODE`、ZPL `^BQ`、DesignerHost DTO/能力 JSON 和 Designer 项目编辑器。`null` 不收窄，空数组禁用非零旋转，`None` 不作为白名单项；没有 profile 的旧输出保持四角度兼容。

| 门禁 | 结果 |
| --- | --- |
| 二维码 profile/直接命令/模板精准回归 | `LabelQrCodeRotationProfileTests`：`net6.0` `9/9`、`netcoreapp3.1` `9/9` |
| Core 完整回归 | `net6.0` `2735/2735`；`netcoreapp3.1` `2735/2735` |
| Core/Templates L0 构建 | 五个目标框架构建通过，0 编译错误 |
| Designer 前端 | Vitest `19` 个文件、`250/250`；`vue-tsc -b` 与 Vite 生产构建通过 |
| Host 应用源码 | `CoreCompile` 通过；Host 测试源码新增二维码能力断言，但完整新输出构建仍受旧静态资源路径、嵌套中间目录和运行中的 `DesignerHost.dll` 锁阻塞 |
| 真实设备 | 未执行；TSPL/ZPL 固件对二维码方向、模块尺寸、定位原点及扫描质量仍需热敏/针式设备验收 |

本轮关闭 TSPL/ZPL 原生二维码旋转 profile、能力描述、直接命令和模板严格预检子范围；不把白名单当作固件兼容证明，也不宣称全部 Native 数值笛卡尔矩阵、HRI 字体样式、未映射变体、性能容量、发送重试或真实硬件验收已完成。上述内容继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R95 验证补充：ESC/POS Native HRI 位置能力

本轮把 ESC/POS Native 条码 HRI 位置从隐含的单一默认值细化为 `None`、`Above`、`Below`、`Both`，并贯通 Profile、能力快照、独立命令、模板预检、Designer Host 契约和浏览器项目模型。旧 `byte textPosition` API 保持兼容；模板默认位置为 `Below`，能力不允许时在生成字节前严格失败。ESC/POS Raster 的 HRI 仍由主机绘制，不受 Native 位置 allowlist 限制。

| 门禁 | 结果 |
| --- | --- |
| HRI 位置 Profile/命令/模板精准回归 | `EscPosBarcodeTextPositionProfileTests` 与模板位置用例：`net6.0` `5/5`、`netcoreapp3.1` `5/5` |
| 受影响 Core 精准回归 | `net6.0` `68/68`，零失败、零跳过 |
| Core 完整回归 | `net6.0` `2726/2726`；`netcoreapp3.1` `2726/2726` |
| Designer 前端 | Vitest `19` 个文件、`249/249`；Vite 生产构建通过 |
| 项目编译与质量 | Core/模板 `net6.0` 编译通过；`git diff --check` 通过 |
| Designer Host 源码/新构建 | `CoreCompile` 通过；完整输出构建未闭合，仍受旧静态资源清单、运行中的 `DesignerHost.exe` 锁和隔离多目标中间目录影响；未使用旧二进制冒充新证据 |
| 真实设备 | 未执行；HRI 字形、位置、DPI、字高、纸宽、切裁、进纸和扫描质量仍需热敏/针式设备验收 |

本轮关闭 ESC/POS Native HRI 位置 allowlist、默认模板位置预检和 Designer 契约子范围；不宣称 HRI 字体样式、二维码方向、固件版本差异或全部数值笛卡尔矩阵已完成。Host 干净隔离重建、未映射协议变体、主机字体校准、性能容量、发送重试和真实硬件继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R94 验证补充：按条码制式细化原生 HRI 能力

本轮将原生一维条码 HRI 从机型级布尔开关细化为可选 `BarcodeType` allowlist，并贯通 ESC/POS、TSPL、ZPL Profile、能力快照、独立命令、统一模板、Designer Host 和前端项目模型。`null` 表示不收窄，空列表表示全部原生 HRI 禁用；ESC/POS Raster 的 HRI 由主机绘制，继续独立于 Native allowlist；ZPL GS1 DataBar 没有原生 HRI 参数。

| 门禁 | 结果 |
| --- | --- |
| HRI Profile 精准回归 | `LabelBarcodeHumanReadableProfileTests`：`net6.0` `7/7`，`netcoreapp3.1` `7/7`；覆盖 TSPL/ZPL/ESC/POS Native allowlist、空列表、快照克隆和 Raster 例外 |
| Label Profile 完整回归 | `LabelPrinterProfileTests`：`net6.0` `37/37` |
| ESC/POS/能力/模板相关精准回归 | `EscPosPrinterProfileTests` `14/14`、`TemplateProtocolCapabilitiesTests` `16/16`、`BarcodeHumanReadableTests` `19/19`、`EscPosBarcodeValidationTests` `30/30`、`ZplDataBarProviderTests` `29/29`（`net6.0`） |
| Core 完整回归 | `net6.0` `2721/2721`；`netcoreapp3.1` `2721/2721` |
| Designer Host 非破坏性回归 | 现有 `net8.0` 测试二进制 `112/112`；源码 `CoreCompile` 通过。正常增量重建被运行中 Host 锁和旧静态资源清单阻塞，未将旧二进制结果作为新隔离重建证据 |
| Designer 前端 | Vitest `19` 个文件、`247/247`；Vite 生产构建通过 |
| 代码质量 | 受影响路径 `git diff --check` 通过 |
| 真实设备 | 未执行；HRI 字形、DPI、字高、纸宽、切裁、进纸和扫描质量仍需热敏/针式设备验收 |

本轮关闭按条码制式的原生 HRI 能力声明、严格预检和 Designer/Host 契约子范围；不宣称所有 HRI 位置/字体样式、二维码方向、固件差异或数值参数矩阵已完成。未映射协议变体、主机字体校准、性能容量、发送重试和真实硬件继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R93 验证补充：ESC/POS Native 全条码制式状态组合矩阵

本轮对 17 个 ESC/POS Native 一维条码制式执行 4 个状态组合：零静区/无旋转的有效动态尺寸路径，非零静区、旋转、非零静区+旋转的严格失败路径，共 68 个条码场景。成功输出检查 `GS H`、`GS h`、`GS w`、`GS k` 和动态高度；失败输出保留模板元素路径并说明静区或旋转原因。

| 门禁 | 结果 |
| --- | --- |
| Native 状态组合矩阵 | `TemplateEscPosNativeFullMatrixTests.Sequential_native_full_matrix_accepts_zero_quiet_zone_with_dynamic_dimensions` 与 `Sequential_native_full_matrix_rejects_all_unsupported_parameter_combinations`：`net6.0` 通过，17 个成功 + 51 个失败场景 |
| `netcoreapp3.1` 兼容性 | 同一测试类 `9/9` 通过 |
| 代码质量 | `git diff --check` 通过；新增文件尾随空白扫描通过 |
| 真实设备 | 未执行；协议固件对静区、旋转、HRI、DPI、纸宽和扫描行为仍需硬件验收 |

本轮关闭 17 个 Native 条码的静区/旋转状态级组合矩阵；不把当前代表性数值（Rotate90、静区 1 点、动态条高样本）扩展为所有数值范围已验证。按条码制式的 Profile 细节、未映射协议变体、性能/容量、发送重试和真实热敏/针式设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R92 验证补充：ESC/POS Native 全条码制式参数边界矩阵

本轮对 ESC/POS Native 当前声明的 17 个一维条码制式逐项验证参数边界：非零静区和条码旋转在模板元素路径处 Fail Fast；Sequential 动态布局使用自动宽度并改变条高与 HRI 预留高度，逐项检查 `GS h` 和布局高度。Rows 多行动态 Y 的严格拒绝也被明确记录为兼容模式边界。

| 门禁 | 结果 |
| --- | --- |
| Native 非零静区边界 | `TemplateEscPosNativeFullMatrixTests.Rows_native_full_matrix_rejects_nonzero_quiet_zone_with_element_paths`：`net6.0` 通过，覆盖 17 项 |
| Native 条码旋转边界 | `TemplateEscPosNativeFullMatrixTests.Rows_native_full_matrix_rejects_rotated_barcode_with_element_paths`：`net6.0` 通过，覆盖 17 项 |
| Sequential 动态尺寸 | `TemplateEscPosNativeFullMatrixTests.Sequential_native_full_matrix_preserves_automatic_width_and_dynamic_height`：`net6.0` 通过，覆盖 17 项 |
| Rows 非原点行边界 | `TemplateEscPosNativeFullMatrixTests.Rows_native_full_matrix_rejects_non_origin_rows_with_element_paths`：`net6.0` 通过，覆盖 17 项 |
| `netcoreapp3.1` 兼容性 | 同一测试类 `7/7` 通过 |
| 代码质量 | `git diff --check` 通过；新增文件尾随空白扫描通过 |
| 真实设备 | 未执行；静区、旋转、HRI 字形、DPI、纸宽和打印/扫描仍需硬件验收 |

本轮关闭 17 项条码的独立静区、旋转和动态尺寸边界验证；未宣称同时组合的笛卡尔矩阵已完成。Rows 的多行动态 Y 保持严格拒绝，复杂动态布局应显式选择 Sequential 或 Raster。Profile 细节、未映射协议变体、性能/容量、发送重试和真实热敏/针式设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R91 验证补充：ESC/POS Native 全条码制式跨页矩阵

本轮使用三页逻辑页面强制 ESC/POS Native Sequential 模板分页，将当前能力声明的 17 个一维条码制式全部送入分页路径。每项均验证元素来源路径、所属页、页内 Y 坐标、页边界、HRI、条高、模块宽度和 `GS k` 制式字节，并确认所有逻辑页都有输出。

| 门禁 | 结果 |
| --- | --- |
| ESC/POS Native 全矩阵跨页 | `TemplateEscPosNativeFullMatrixTests.Sequential_template_covers_the_full_native_matrix_across_pages`：`net6.0` 通过，覆盖 17 项、多个逻辑页 |
| `netcoreapp3.1` 兼容性 | 同一测试类 `3/3` 通过，包含 Rows、Sequential 连续行和跨页矩阵 |
| 代码质量 | `git diff --check` 通过；新增文件尾随空白扫描通过 |
| 真实设备 | 未执行；纸长、固件分页、HRI 字形、DPI、打印/扫描和切裁仍需硬件验收 |

本轮关闭 ESC/POS Native 当前已声明 17 个一维条码制式的基础跨页顺序与布局轨迹验证。静区、旋转、动态尺寸的全组合，按条码制式的 Profile 细节、未映射协议变体、性能/容量、发送重试和真实热敏/针式设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R90 验证补充：ESC/POS Native 全条码制式矩阵

本轮新增数据驱动模板回归，将 ESC/POS Native 能力快照声明的 17 个一维条码制式分别送入 Rows 和 Sequential 渲染路径。每项均验证 HRI 位置、条高、模块宽度、`GS k` 制式编号、元素来源路径和高度轨迹；Sequential 另外确认连续行输出保留完整条码顺序。

| 门禁 | 结果 |
| --- | --- |
| ESC/POS Native Rows 全矩阵 | `TemplateEscPosNativeFullMatrixTests.Rows_template_covers_every_declared_native_barcode_with_hri_parameters`：`net6.0` 通过，覆盖 17 项 |
| ESC/POS Native Sequential 全矩阵 | `TemplateEscPosNativeFullMatrixTests.Sequential_template_covers_the_full_native_matrix_with_continuous_rows`：`net6.0` 通过，覆盖 17 项 |
| `netcoreapp3.1` 兼容性 | 同一测试类 `2/2` 通过，覆盖 Rows 和 Sequential |
| 代码质量 | `git diff --check` 通过；新增文件尾随空白扫描通过 |
| 真实设备 | 未执行；HRI 字形、位置、字高、DPI、打印/扫描和切裁仍需硬件验收 |

本轮关闭 ESC/POS Native 当前已声明 17 个一维条码制式的 Rows/Sequential 基础参数矩阵。跨页、静区、旋转、动态尺寸的全组合，以及按条码制式的 Profile 细节、未映射协议变体、性能/容量、发送重试和真实热敏/针式设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。测试使用足够大的逻辑页承载 DataBar 协议最小高度，不将该夹具尺寸解释为设备纸宽支持结论。

## R89 验证补充：Designer HRI Profile 独立浏览器回归

本轮使用当前源码隔离构建 `tools/DesignerHost` 到 `tools/DesignerHost/artifacts/r89-host-build/net8.0`，在独立 API 端口 `5198` 和 Vite 端口 `5175` 运行 TSPL/ZPL `e2e/labelProfiles.spec.ts`。回归覆盖机型能力响应、HRI Profile 字段、越界/字库/图片预算拒绝、默认输出字节兼容、编辑器导入、能力过滤、项目保存重载、撤销重做和非法草稿阻断；最终 `8/8` 通过。

| 门禁 | 结果 |
| --- | --- |
| Designer HRI Profile 浏览器回归 | `labelProfiles.spec.ts`：`8/8`，零失败、零跳过 |
| 前端单元 | Vitest `19` 个文件、`247/247` |
| 前端生产构建 | `vue-tsc -b` 和 Vite build 通过；保留既有单 bundle 大于 500 kB 提示 |
| 隔离 Host 构建 | `tools/DesignerHost/artifacts/r89-host-build/net8.0`，0 warning、0 error |
| 真实设备 | 未执行；HRI 字形、位置、字高、DPI、打印/扫描仍需硬件验收 |

本轮关闭 R88 遗留的 Designer `labelProfiles.spec.ts` 浏览器端到端子范围。Native 全参数矩阵、按条码制式的 Profile 细节、未映射条码/厂商扩展、性能/容量、发送重试和真实热敏/针式设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。`NU1900` 漏洞源不可达及旧 TFM 警告不改变上述通过结果。

## R88 验证补充：TSPL/ZPL 机型 HRI 能力契约

本轮为 TSPL/ZPL `PrinterProfile` 增加 `supportsNativeBarcodeHumanReadable` 机型声明，能力快照、直接命令、Provider 预检和模板预检共用同一严格契约。默认值为 `true`，因此未配置该字段的旧 Profile 保持原有输出；显式关闭后，开启或协议默认的条码可读文字会在生成字节前失败，并保留模板元素路径。显式关闭 HRI 仍可输出。

| 门禁 | 结果 |
| --- | --- |
| Profile HRI 精准回归 | `LabelBarcodeHumanReadableProfileTests`：`net6.0` `3/3`，`netcoreapp3.1` `3/3` |
| 源码完整回归 | 两个受影响目标均 `2708/2708`，零失败、零跳过；包含新增 Profile 能力测试 |
| 当前包消费者 | `artifacts/r88-hri-pack`：`net7.0`、`net6.0`、`net5.0`、`netcoreapp3.1`、`netcoreapp2.2` 全部 PASS；另有 `net5.0 + netstandard2.1` 显式资产 PASS，候选/恢复包哈希一致 |
| Designer/Host 契约 | 前端 Vitest `19` 个文件、`247/247`；生产构建 PASS；隔离输出的 `DesignerHost.Tests` `114/114` |
| 严格边界 | TSPL/ZPL `HumanReadable=null` 按协议默认视为请求 HRI；Profile 关闭时 Provider/模板在输出前拒绝；`false` 保持可用 |
| 浏览器/外部设备 | 本轮未在独立 Host 上执行 Playwright `labelProfiles.spec.ts`；HRI 字形、位置、字高、DPI、打印和扫描仍需真实热敏/针式设备验收 |

本轮关闭 TSPL/ZPL 机型级 HRI 能力声明与 Fail Fast 校验；按条码制式分别声明 HRI、HRI 字体/位置校准、Native 全参数矩阵、性能证据、发送重试和真实硬件验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。`NU1900` 仍是漏洞源不可访问警告，不影响上述测试结果。

## R87 验证补充：当前运行时矩阵与源码包消费者

本轮使用现有 `Verify-ProtocolRuntimes.ps1` 重新验证当前工作区的五个目标运行时；五个目标均实际启动对应 testhost，各有 `2705/2705` 通过、零失败、零跳过。报告位于 `artifacts/r87-runtime-verification/20261006-193306-c3105860/runtime-verification.json`，其中 `net7.0`、`net5.0`、`netcoreapp2.2` 使用隔离便携运行时，`net6.0`、`netcoreapp3.1` 使用当前系统运行时。

随后从当前源码生成 `artifacts/r87-current-pack` 的 `Bing.EasyPrint.1.0.0.nupkg` 与 `Bing.EasyPrint.Templates.1.0.0.nupkg`，通过 `Verify-PackageConsumer.ps1` 在五个默认消费者目标执行 API、模板、四协议 Provider、HRI、填充矩形、ZPL 延迟输出和图片旋转回归。五份报告均为 `Status=PASS`、`Runtime.Status=PASS`、退出码 `0`，并确认候选包和恢复包 SHA-256 一致：

| 消费目标 | 实际运行时 | 结果证据 |
| --- | --- | --- |
| `net7.0` | 7.0.20 便携运行时 | `artifacts/r87-current-package-consumer-final-net7/consumer-net7.0/consumer-verification.json` |
| `net6.0` | 6.0.36 | `artifacts/r87-current-package-consumer-final-net6/consumer-net6.0/consumer-verification.json` |
| `net5.0` | 5.0.17 便携运行时 | `artifacts/r87-current-package-consumer-final-net5/consumer-net5.0/consumer-verification.json` |
| `netcoreapp3.1` | 3.1.32 | `artifacts/r87-current-package-consumer-final-core31/consumer-netcoreapp3.1/consumer-verification.json` |
| `netcoreapp2.2` | 2.2.8 便携运行时 | `artifacts/r87-current-package-consumer-final-core22/consumer-netcoreapp2.2/consumer-verification.json` |
| `net5.0` + `-PackageAssetFramework netstandard2.1` | 5.0.17 便携运行时 | `artifacts/r87-current-package-consumer-net5-netstandard21-fixed7/consumer-net5.0-netstandard2.1/consumer-verification.json`；两项 `OutputMatched=true` |

历史 `artifacts/t8-fixed-font-pack` 的三次 R87 消费失败属于候选包落后于当前公开 API 的失配，缺少 `SupportsImageRotation`、`RasterBarcodeHumanReadableFontSize`、`FillRectangle`、`CreateFilled` 等成员；重新生成当前源码包后五目标均通过。显式 `-PackageAssetFramework netstandard2.1` 案例最初触发 `MSB3030`，现已在消费者工程过滤重复 CopyLocal 项并由验证脚本补齐传递运行时 DLL 后通过；这不是协议 Provider 的兼容性失败。

| 门禁 | 结果 |
| --- | --- |
| 完整目标运行时 | 5/5 目标通过，每目标 2705/2705；`NU1900` 仅为漏洞源不可达警告 |
| 当前源码打包 | Core/Templates 两包生成成功，包含五个生产 TFM；保留 `NU5104` 预发布依赖提示 |
| 默认包消费者 | 5/5 目标构建、运行和公开 API/模板回归通过；旧 TFM 另有 `NETSDK1138` 警告 |
| 显式 `netstandard2.1` 资产 | PASS；Core/Templates 两个输出 DLL 均来自候选包 `lib/netstandard2.1`，传递运行时 DLL 已补齐 |
| 真实设备 | 未执行；DPI、字高、纸宽、切裁、打印/扫描仍是外部门禁 |

本轮关闭当前源码的五目标运行时矩阵、默认包消费者和一个显式 `netstandard2.1` 资产选择子范围；性能容量、复杂文字、发送重试及真实硬件验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R86 验证补充：ESC/POS Native Sequential 旋转边界

新增 `TemplateEscPosNativeSequentialRotationBoundaryTests`：Native Sequential Version 2 模板分别使用 Rotate90 条码和 Rotate90 文字，均按 `Rows[0].Cells[0].Elements[0]` 严格拒绝；非法元素后追加图片资源，测试确认两种失败的 `OpenRead` 次数均为 0。

| 门禁 | 结果 |
| --- | --- |
| Native Sequential 旋转精准回归 | `TemplateEscPosNativeSequentialRotationBoundaryTests`：`net6.0` `2/2`；`netcoreapp3.1` `2/2` |
| 覆盖范围 | Rotate90 条码、Rotate90 文字、元素路径和资源前置拒绝 |
| 外部限制 | 未覆盖 Raster/标签协议旋转视觉结果、Native 全参数组合、设备固件和真实打印；保留 `NU1900` 漏洞源不可访问警告 |

本轮关闭 ESC/POS Native Sequential 旋转严格边界的本地验证子范围；完整 Native 参数矩阵、性能证据、运行时矩阵和真实设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R85 验证补充：ESC/POS Native Sequential 参数组合

新增 `TemplateEscPosNativeSequentialParameterTests`：Native Sequential Version 2 模板包含固定页眉、表头、重复 Body 和页脚，两条明细分别使用带 HRI 的 Code39、I2OF5、UCC/EAN-128。测试按页和实例检查 `SourcePath`、`InstanceIndices`、尺寸、`GS H=2`、`GS h=40`、`GS w=3`、`GS k=69/70/74` 和 Payload；非零静区在后续图片资源打开前按元素路径严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| Native Sequential 参数精准回归 | `TemplateEscPosNativeSequentialParameterTests`：`net6.0` `2/2`；`netcoreapp3.1` `2/2` |
| 覆盖范围 | Sequential 跨页重复、三种 HRI 条码、实例轨迹、GS H/GS h/GS w/GS k 和静区严格拒绝 |
| 外部限制 | 未覆盖 Native 全条码/HRI/静区/旋转/动态尺寸组合、设备 HRI 字形、容量曲线和真实扫描；保留 `NU1900` 漏洞源不可访问警告 |

本轮关闭 ESC/POS Native Sequential 代表性 HRI 参数分页链路的本地验证子范围；完整 Native 参数矩阵、性能证据、运行时矩阵和真实设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R84 验证补充：ESC/POS Native Rows 参数组合

新增 `TemplateEscPosNativeParameterCombinationTests`：Native Rows 模板对 Code39、Code93、Codabar、I2OF5 和 UCC/EAN-128 开启 HRI，验证 `GS H=2`、HRI 预留高度、`GS w`、`GS k` 与 payload。模块宽度从 2 调整到 3 时，布局宽度和原生参数同步变化；`NarrowModuleWidth=1` 按模板元素路径严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| Native 参数组合精准回归 | `TemplateEscPosNativeParameterCombinationTests`：`net6.0` `7/7`；`netcoreapp3.1` `7/7` |
| 覆盖范围 | Native Rows、五种 HRI 条码、预留高度、模块宽度 2/3、GS H/GS w/GS k 和严格下限 |
| 外部限制 | 未覆盖 Native 全条码/HRI/静区/旋转/动态尺寸组合、设备 HRI 字形、容量曲线和真实扫描；保留 `NU1900` 漏洞源不可访问警告 |

本轮关闭 ESC/POS Native Rows 代表性 HRI 与模块宽度组合的本地验证子范围；完整 Native 参数矩阵、性能证据、运行时矩阵和真实设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R83 验证补充：ESC/POS Native Rows 条码矩阵

新增 `TemplateEscPosNativeRowsBarcodeMatrixTests`：Native Rows 模板覆盖 Code39、Code93、Codabar、I2OF5、I2OF5C 和 UCC/EAN-128。测试按能力快照、`SourcePath`、尺寸、页面边界和原生 `GS k` 69、70、71、72、74 验证输出；I2OF5C 检查 `1234` 到 `012348` 的校验位归一化，非法 Codabar 按元素路径严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| Native Rows 条码精准回归 | `TemplateEscPosNativeRowsBarcodeMatrixTests`：`net6.0` `3/3`；`netcoreapp3.1` `3/3` |
| 覆盖范围 | Native Rows、六种一维条码、SourcePath、布局尺寸、GS k 制式、I2OF5C 校验位和严格拒绝 |
| 外部限制 | 未覆盖 Native 全条码/HRI/静区/旋转/动态尺寸组合、容量曲线、其他运行时、设备固件和真实扫描；保留 `NU1900` 漏洞源不可访问警告 |

本轮关闭 ESC/POS Native Rows 代表性一维条码制式的本地验证子范围；完整 Native 参数矩阵、性能证据、运行时矩阵和真实设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R82 验证补充：ESC/POS Native 扩展条码模板

新增 `TemplateEscPosNativeExtendedBarcodeTests`：ESC/POS Native Sequential 的 Version 2 模板使用固定页眉、表头、重复明细和页脚，覆盖 GS1 DataBar Omnidirectional、Truncated、Limited、Expanded 与 Code128 auto。测试按 `SourcePath`、实例索引和 `BarcodeMetrics` 宽度验证两条明细跨页复制，并检查 Function B `GS k` 75–79 的制式、长度、payload 及 Expanded 括号/FNC1 转义；MSI 不支持时按元素路径严格拒绝。

| 门禁 | 结果 |
| --- | --- |
| Native 扩展条码精准回归 | `TemplateEscPosNativeExtendedBarcodeTests`：`net6.0` `3/3`；`netcoreapp3.1` `3/3` |
| 覆盖范围 | Native Sequential、五种 Function B 条码、跨页重复、来源轨迹、保守宽度、GS k 75–79 和严格拒绝 |
| 外部限制 | 未覆盖 Native 全条码/HRI/静区/旋转/动态尺寸组合、容量曲线、其他运行时、设备固件和真实扫描；保留 `NU1900` 漏洞源不可访问警告 |

本轮关闭 ESC/POS Native 扩展条码模板数据链路的本地验证子范围；完整 Native 参数矩阵、性能证据、运行时矩阵和真实设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R81 验证补充：ESC/POS Native 参数矩阵

新增 `TemplateEscPosNativeParameterMatrixTests`：Native Rows 模板分别验证 Code128、EAN13 的 HRI 预留高度、`GS H` 和 `GS k` 字节，以及显式零静区输出。非零静区、旋转条码和旋转文字在资源读取前按模板元素路径拒绝，后续图片资源打开次数保持为零。

| 门禁 | 结果 |
| --- | --- |
| Native 参数精准回归 | `TemplateEscPosNativeParameterMatrixTests`：`net6.0` `6/6`；`netcoreapp3.1` `6/6` |
| 覆盖范围 | Native Rows、HRI 预留、GS H/GS k、零静区兼容、静区/旋转严格拒绝和资源前置校验 |
| 外部限制 | 未覆盖所有 Native 条码、动态尺寸全组合、设备 HRI 字形、容量曲线和真实扫描；保留 `NU1900` 漏洞源不可访问警告 |

本轮关闭 ESC/POS Native Rows 代表性条码参数边界的本地验证子范围；完整 Native 矩阵、性能证据、运行时矩阵和真实设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R80 验证补充：ESC/POS Native 跨页条码模板

新增 `TemplateEscPosNativeCrossPageBarcodeTests`：ESC/POS Native Sequential 使用 Version 2 模板的固定页眉、表头、重复明细和页脚生成多页。每条明细包含 Code128、Code39、EAN13，测试按 `SourcePath` 与 `InstanceIndices` 验证跨页复制，并检查 `GS k` 的 73、69、67 制式标记；另有 MSI 不支持的严格元素路径拒绝用例。

| 门禁 | 结果 |
| --- | --- |
| Native 跨页精准回归 | `TemplateEscPosNativeCrossPageBarcodeTests`：`net6.0` `2/2`；`netcoreapp3.1` `2/2` |
| 覆盖范围 | Native Sequential、固定分区、重复明细、三种原生条码、布局轨迹和严格拒绝 |
| 断言方式 | 按来源路径和实例索引验证跨页复制，不假定每页固定条码数量 |
| 外部限制 | Native Rows、全条码/HRI/静区/旋转矩阵、容量曲线、设备固件和真实扫描仍未验证；保留 `NU1900` 漏洞源不可访问警告 |

本轮关闭 ESC/POS Native Sequential 代表性条码模板跨页本地验证子范围；完整 Native 矩阵、性能证据、运行时矩阵和真实设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R79 验证补充：跨页条码复杂布局

新增 `TemplateCrossPageBarcodeTests`：Version 2 模板同时包含 `PageHeader`、`TableHeader`、三条 `Body RepeatSource` 明细和 `PageFooter`，每条明细包含 Code39、Code93、Codabar、I2OF5C。四种协议均检查三页输出、每个 `SourcePath` 的 `InstanceIndices` `0/1/2`、正尺寸和非空页面数据；CPCL/TSPL/ZPL 检查原生条码命令，ESC/POS Raster 检查 `GS v 0` 光栅头和完整数据长度。

| 门禁 | 结果 |
| --- | --- |
| 跨页条码精准回归 | `TemplateCrossPageBarcodeTests`：`net6.0` `4/4`；`netcoreapp3.1` `4/4` |
| 覆盖范围 | Version 2 固定分区、重复明细、四种代表性非 Code128 条码、四协议布局轨迹与协议字节 |
| 断言方式 | 按来源路径和实例索引验证跨页复制；不假定每页固定条码数量 |
| 外部限制 | 未覆盖所有条码类型、完整 HRI/静区/旋转组合、Native 全矩阵、容量曲线或真实设备扫描；保留 `NU1900` 漏洞源不可访问警告 |

本轮关闭代表性非 Code128 条码跨页复杂布局的本地验证子范围；全类型组合、正式性能证据、完整运行时矩阵和真实设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R78 验证补充：图片长页裁切与复杂分区压力

新增 Version 2 长页夹具同时包含 `PageHeader`、`TableHeader`、重复明细和 `PageFooter` 图片；重复明细使用同一源图的两个不同裁切区域，7 条明细触发 4 页输出。有限 `MaxWorkingBytes` 下，静态分区图片各只打开一次，正文裁切图片按明细重新打开并在块完成后释放；同一渲染失败后使用新会话和更高预算可恢复。

| 门禁 | 结果 |
| --- | --- |
| 模板长页压力回归 | `TemplateImageLongPagePressureTests`：`net6.0` `3/3`；`netcoreapp3.1` `3/3` |
| 模板完整回归 | `Bing.EasyPrint.Tests`：`net6.0` `2676/2676`；`netcoreapp3.1` `2676/2676` |
| 覆盖范围 | Version 2 页眉/表头/重复明细/页脚、双裁切、跨页资源复用、流释放、缓存隔离、预算拒绝与恢复 |
| 预期结果 | `EXPECTED_SUCCESS`（256 KiB 工作集）与 `EXPECTED_RESOURCE_REJECTION`（1 KiB 工作集）均按契约通过 |
| 外部限制 | 固定 7 条明细和 128×64 源图只验证资源生命周期与隔离，不代表任意规模容量、进程峰值内存或真实设备吞吐 |

本轮关闭图片裁切与复杂分区组合的长页本地回归子范围；其他条码复杂组合、正式容量曲线、完整运行时矩阵和真实设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R77 验证补充：图片结构化像素诊断

模板图片错误现在携带可空的 `sourcePixelWidth`/`sourcePixelHeight`、`renderedPixelWidth`/`renderedPixelHeight` 和 `cropX`/`cropY`/`cropWidth`/`cropHeight`。源图已成功识别但裁切越界时返回源图尺寸与请求裁切区域；缺失、损坏或尚未完成识别时保留 `null`，不把未知值转为零。Designer Host 在预览和渲染错误 JSON 中透传这些字段，Designer 只在字段完整且为非负整数时显示源图、裁切和输出尺寸。

| 门禁 | 结果 |
| --- | --- |
| 模板图片诊断回归 | `TemplateImageDiagnosticTests`：`net6.0` `10/10`；`netcoreapp3.1` `10/10` |
| Host 契约回归 | 图片缺失/损坏、裁切越界、预览/渲染恢复精准用例 `7/7` |
| Designer 诊断单测 | `diagnostics.test.ts` `14/14` |
| 影响范围 | 模板异常公共只读属性、Host 错误 JSON、Designer 诊断规范化/格式化；旧字段和旧 CPCL 输出保持兼容 |
| 外部限制 | 像素元数据不替代 DPI、插值、纸宽、真实打印/扫描；性能容量曲线、完整运行时矩阵和硬件验收仍需后续或外部验证 |

本轮关闭损坏/未加载/裁切错误的结构化像素诊断本地子范围；长页资源压力、其他条码复杂组合、正式性能证据和真实设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R76 验证补充：多页图片资源生命周期与取消恢复

`TemplateImageWorkingBudgetTests` 新增四类生命周期断言：页眉图片跨页只打开一次且最终释放；正文 128×128 大图在约单图工作集预算下逐页回收并完成四页输出；资源打开阶段触发取消时流被释放；使用新的资源提供器和取消令牌后渲染可以恢复。生产路径没有为本轮测试放宽预算或改变公共 API。

| 门禁 | 结果 |
| --- | --- |
| `net6.0` 精准回归 | `TemplateImageWorkingBudgetTests` `8/8` |
| `netcoreapp3.1` 精准回归 | `TemplateImageWorkingBudgetTests` `8/8` |
| 覆盖范围 | 页眉跨页缓存、正文图片工作集回收、资源流释放、取消失败恢复、单图预算 |
| 生产变更 | 无；复用现有页眉缓存、正文块后清理、图片租约和会话释放路径 |
| 外部限制 | NU1900 漏洞源不可访问警告；图片解码内部中断、性能曲线、真实设备和发送重试仍需后续/外部验收 |

本轮关闭图片资源多页工作集和取消释放的本地验证子范围；结构化像素诊断已在 R77 补充关闭，不同裁切键并存的容量曲线、其他条码复杂组合、运行时矩阵和真实设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R75 验证补充：模板图片源区域裁切

模板图片新增 `Crop = { X, Y, Width, Height }`，坐标和尺寸使用源图像素。渲染流程在按 `Width`/`Height` 缩放前裁切，缓存键包含裁切区域；四协议统一验证裁切后的尺寸、按裁切比例自动补高、0/90/270 度旋转以及同资源不同裁切的像素差异。Designer 属性面板、模板校验、模板包往返和画布裁切显示同步覆盖。

| 门禁 | 结果 |
| --- | --- |
| 模板图片回归 | `TemplateImageRotationTests`：`net6.0` `38/38`；`netcoreapp3.1` `38/38` |
| Templates 编译 | `netstandard2.0`、`net7.0` 通过 |
| Designer 单测/类型检查 | `245/245`；`vue-tsc -b` 通过 |
| Designer 生产构建 | `pnpm build` 通过 |
| 覆盖范围 | JSON 往返、源区域边界、裁切后比例缩放、旋转、四协议图片命令、缓存隔离、画布裁切显示 |
| 外部限制 | 损坏资源诊断、插值质量、DPI、纸宽、真实打印和扫描仍需后续或硬件验收 |

本轮关闭模板图片源区域裁切的本地实现和主要验证子范围；多页资源生命周期已在 R76、结构化错误诊断已在 R77 补充验证，裁切与复杂分区组合、其他条码复杂组合、正式性能证据和真实设备验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R74 验证补充：模板图片显式缩放边界

`TemplateImageRotationTests` 使用非对称 4×3 PNG，覆盖显式目标尺寸 2×2、4×3、6×5 以及 0/90/270 度旋转。CPCL、TSPL、ZPL 和 ESC/POS Raster 均检查布局轨迹中的缩放后尺寸、旋转宽高交换和自动页面高度，并分别验证 `EG`、`BITMAP`、`^GFA`、`GS v 0` 输出标记。

| 门禁 | 结果 |
| --- | --- |
| `net6.0` 目标用例 | `25/25` |
| `netcoreapp3.1` 目标用例 | `25/25` |
| 覆盖范围 | 小于、等于、大于源图的显式尺寸；0/90/270 度旋转；四协议图片命令 |
| 外部限制 | 任意裁切、插值质量、资源压力、DPI、纸宽、真实打印和扫描仍需后续或硬件验收 |

本轮关闭模板图片显式目标尺寸的本地回归子范围；源区域裁切、多页资源压力、其他条码复杂组合和正式性能证据继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。ESC/POS Raster 的 `GS v 0` 是整页光栅，单图尺寸结论来自布局轨迹而非整页头部。

## R73 验证补充：Designer 四协议跨页点阵码

`designer.spec.ts` 新增 Version 2 跨页点阵码流程：分别使用 DataMatrix 和 PDF417，模板包含固定页眉、表头、3 条重复明细和固定页脚。CPCL、TSPL、ZPL 和 ESC/POS Raster 均检查每页固定区、Type 10 点阵码的 `sourcePath`/类型/实例顺序，并确认 ZIP 页面数量与协议预览页数一致。

| 门禁 | 结果 |
| --- | --- |
| 本轮 Designer E2E | 目标用例 `1/1` |
| 构建 | Designer `npm run build` 通过 |
| 覆盖范围 | DataMatrix、PDF417、跨页页眉/表头/页脚、四协议预览和 ZIP 页面映射 |
| 协议标记 | CPCL `EG`；TSPL `BITMAP`；ZPL `^GFA`；Raster `GS v 0` |
| 外部限制 | 任意裁切/缩放质量、DPI、纸宽、真实打印和扫描解码仍需后续或硬件验收 |

本轮关闭 DataMatrix/PDF417 跨页复杂分区的本地 Designer 验收子范围；原生 Native 点阵码、资源压力和真实设备验证继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R72 验证补充：Designer 四协议跨页条码混排

`designer.spec.ts` 新增 Version 2 跨页条码混排流程：模板包含固定页眉、表头、9 条重复明细和固定页脚，每条明细同时包含 Code 128 与二维码。CPCL、TSPL、ZPL 和 ESC/POS Raster 均检查每页固定区、两类条码的 `sourcePath`/类型/实例顺序，并确认 ZIP 页面数量与协议预览页数一致。

| 门禁 | 结果 |
| --- | --- |
| 本轮 Designer E2E | 目标用例 `1/1` |
| 覆盖范围 | 跨页页眉、表头、条码明细、页脚、四协议预览和 ZIP 页面映射 |
| 协议标记 | CPCL `B 128`/`B QR`；TSPL `BARCODE`/`QRCODE`；ZPL `^BC`/`^BQ`；Raster `GS v 0` |
| 外部限制 | 其他条码组合、动态尺寸、DPI、纸宽、走纸、真实打印和扫描结果仍需后续或硬件验收 |

本轮关闭跨页条码混排的本地 Designer 验收子范围；图片/点阵码复杂资源混排、性能证据和真实设备验证继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R71 验证补充：Designer 四协议跨页图片资源与旋转占位

`designer.spec.ts` 新增模板包跨页图片浏览器流程：模板包只包含一个非对称 PNG，Version 2 重复明细在 90 度旋转图片的同时触发多页。CPCL、TSPL、ZPL 和 ESC/POS Raster 均检查每页固定区、图片布局轨迹和 11 条明细顺序，并确认 ZIP 页面数量与协议自身预览页数一致。压缩 CPCL 页面校验 `CG`，TSPL/ZPL/Raster 分别校验 `BITMAP`、`^GFA`、`GS v 0`。

| 门禁 | 结果 |
| --- | --- |
| 本轮 Designer E2E | 目标用例 `1/1`；图片旋转回归 `5/5` |
| 构建 | Designer `pnpm build`（`vue-tsc -b` + Vite）通过 |
| 覆盖范围 | 模板包单资源、多页重复明细、90 度旋转占位、四协议预览、协议字节和 ZIP 页面映射 |
| 外部限制 | 任意裁切/缩放质量、资源压力边界、DPI、纸宽、走纸、真实打印和扫描结果仍需目标设备验收 |

本轮关闭跨页重复图片资源和旋转占位的本地验收子范围；图片裁切/缩放压力、其他条码复杂组合、性能证据和硬件验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。TSPL/ZPL 样式保持显式主机位图降级并列为 `ACCEPTED_LIMITATION`。

## R70 验证补充：Designer 四协议跨页页眉、表头与页脚

`designer.spec.ts` 新增 Version 2 跨页分区浏览器流程：模板包含固定页眉文字与水平线、可重复表头、11 条重复明细以及固定页脚文字与矩形。依次选择 CPCL、TSPL、ZPL 和 ESC/POS Raster，等待 Host 预览并检查每一页的页眉、表头、页脚、线框元素和明细顺序；随后下载 ZIP，确认各协议自己的预览页数与连续 `page-001.*` 页面完全对应。

| 门禁 | 结果 |
| --- | --- |
| 本轮 Designer E2E | 目标用例 `1/1`；与“同一”跨协议回归 `8/8`；动态表格分页回归 `1/1` |
| 构建 | Designer `pnpm build`（`vue-tsc -b` + Vite）通过 |
| 覆盖范围 | V2 `PageHeader`、`TableHeader`、重复明细、`PageFooter`、多页布局轨迹、协议切换和 ZIP 页面映射 |
| 外部限制 | 动态行高、跨页图片/点阵码旋转裁切、走纸、切裁、DPI、纸宽、真实打印和扫描结果仍需目标设备验收 |

本轮关闭跨页页眉/表头/页脚与重复明细同时输出的本地验收子范围；复杂资源混排、原生字体样式、性能证据和硬件验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R69 验证补充：Designer 四协议混合布局输出 E2E

`designer.spec.ts` 新增 Version 2 混合布局浏览器流程：同一页包含文字、水平线、空心矩形、Code 128 条码和二维码，依次选择 CPCL、TSPL、ZPL、ESC/POS Raster，等待 Host 预览并下载 ZIP。每种协议的布局响应均保留 Type 1/3/4/5/7 元素；CPCL/TSPL 的空心矩形通过线命令展开，ZPL 保留 `^GB`，Raster 页面保留 `GS v 0`。

| 门禁 | 结果 |
| --- | --- |
| 本轮 Designer E2E | 目标用例 `1/1`；与二维码、图片和点阵码跨协议用例联合 `4/4` |
| 构建 | Designer `pnpm build`（`vue-tsc -b` + Vite）通过 |
| 覆盖范围 | 同页文字、线框、矩形、条码、二维码、协议切换、布局轨迹和四种 ZIP 页面 |
| 外部限制 | 跨页复杂布局、走纸、切裁、DPI、纸宽、真实打印和扫描结果仍需目标设备验收 |

本轮关闭单页混合布局的四协议 Designer 输出子范围；跨页混合布局、图形旋转/裁切/多页资源、原生字体样式和硬件验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R68 验证补充：Designer 四协议长文本换行输出 E2E

`designer.spec.ts` 新增 Version 2 长文本浏览器流程：使用 240 个 ASCII 字符和 `NeedFeedLine`，依次选择 CPCL、TSPL、ZPL、ESC/POS Raster，等待 Host 预览并收集每页文本片段。四种协议的片段均能拼回完整原文，导出的 ZIP 页面编号连续，并分别与各自预览页数一致。默认夹具观察到 CPCL 6 页、TSPL 8 页、ZPL 8 页、ESC/POS Raster 7 页，差异来自协议字体度量，不作为内容丢失处理。

| 门禁 | 结果 |
| --- | --- |
| 本轮 Designer E2E | 目标用例 `1/1`；与旧 CPCL 长文本、四协议分页和二维码用例联合 `4/4` |
| 构建 | Designer `pnpm build`（`vue-tsc -b` + Vite）通过 |
| 覆盖范围 | Version 2 长文本片段、协议自身换行分页、四种 ZIP 页面序和 Raster 图片命令 |
| 外部限制 | 中文/复杂文字塑形、设备字体校准、走纸、DPI、纸宽、真实打印和扫描结果仍需目标设备验收 |

本轮关闭长文本内容完整性和多协议分页归档映射子范围；跨页表头/页脚、复杂布局、图形旋转/裁切/多页资源及硬件验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R67 验证补充：Designer 四协议分页输出 E2E

`designer.spec.ts` 新增一个只含跨协议文字重复行的 Version 3 模板，固定 13 条数据触发多页，依次选择 CPCL、TSPL、ZPL 和 ESC/POS Raster，等待 Host 预览并从 Designer 下载 ZIP。每种协议都校验预览页数大于一、ZIP 中连续的 `page-001.*` 页面数量与预览一致，并分别检查 CPCL `! 0`、TSPL `SIZE`、ZPL `^XA` 和 ESC/POS Raster `GS v 0` 标记。

| 门禁 | 结果 |
| --- | --- |
| 本轮 Designer E2E | 目标用例 `1/1`；与既有收据分页、二维码、图片和点阵码跨协议用例联合 `5/5` |
| 构建 | Designer `pnpm build`（`vue-tsc -b` + Vite）通过 |
| 覆盖范围 | Version 3 重复行、多页 Host 预览、协议切换、四种 ZIP 页面编号与协议字节标记 |
| 外部限制 | 走纸、切裁、DPI、纸宽、真实打印和扫描结果仍需目标设备验收 |

本轮关闭同一重复明细模板的四协议分页输出子范围；长文本换行边界、复杂布局组合、图形旋转/裁切/多页资源以及硬件验收继续按 TODO 或 `BLOCKED_EXTERNAL` 处理。

## R66 验证补充：Designer 四协议 DataMatrix/PDF417 输出 E2E

`designer.spec.ts` 新增 Type 10 点阵码浏览器流程：使用 Version 2 模板分别导入 DataMatrix 和 PDF417，依次选择 CPCL、TSPL、ZPL、ESC/POS Raster，等待引擎预览并分别下载 ZIP。`page-001.cpcl`、`page-001.tspl`、`page-001.zpl` 和 `page-001.escpos` 分别校验 `EG`、`BITMAP`、`^GFA` 和 `GS v 0` 图片命令。

| 门禁 | 结果 |
| --- | --- |
| 本轮 Designer E2E | 目标用例 `1/1`；与二维码、图片跨协议用例联合 `3/3` |
| 覆盖范围 | Version 2 Type 10 导入、DataMatrix/PDF417 编码、Host 预览恢复、协议切换、四种 ZIP 页面和图片命令标记 |
| 外部限制 | 点阵码的实际 DPI、纸宽、缩放、打印和扫描解码仍需目标设备验收 |

本轮关闭 DataMatrix/PDF417 的 Designer 跨协议输出子范围；原生 Native 点阵码、复杂布局和真实设备验收继续按 TODO/`BLOCKED_EXTERNAL` 处理。

## R65 验证补充：Designer 四协议图片输出 E2E

`designer.spec.ts` 新增同一图片模板的跨协议浏览器流程：测试在浏览器内生成 2×1 PNG，写入标准 `TemplateBundle` 的 `resources/001-logo.png` 和资源清单，通过模板导入控件恢复图片，再依次选择 CPCL、TSPL、ZPL、ESC/POS Raster，等待引擎预览并分别下载 ZIP。`page-001.cpcl`、`page-001.tspl`、`page-001.zpl` 和 `page-001.escpos` 分别校验 `EG`、`BITMAP`、`^GFA` 和 `GS v 0` 图片命令。

| 门禁 | 结果 |
| --- | --- |
| 本轮 Designer E2E | 目标用例 `1/1`；与二维码跨协议、ESC/POS Raster 阈值请求和图片快捷导入/模板包往返回归联合 `5/5` |
| 覆盖范围 | 真实模板包资源、PNG 解码、资源状态、Host 预览恢复、协议切换、四种 ZIP 页面和图片命令标记 |
| 外部限制 | 图片阈值、DPI、纸宽、旋转/缩放、打印和扫描结果仍需目标设备验收 |

本轮关闭同一图片资源的四协议 Designer 输出子范围；其他条码、复杂布局、像素质量和真实设备验收继续按 TODO/`BLOCKED_EXTERNAL` 处理。

## R64 验证补充：Designer 四协议二维码输出 E2E

`designer.spec.ts` 新增同一二维码模板的跨协议浏览器流程：导入模板后依次选择 CPCL、TSPL、ZPL、ESC/POS Raster，等待引擎预览，再分别下载 ZIP，校验 `page-001.cpcl` 的 `B QR`、`page-001.tspl` 的 `QRCODE`、`page-001.zpl` 的 `^BQ` 和 `page-001.escpos` 的 `GS v 0` 光栅命令，同时确认二维码数据存在于前三种文本协议输出。

| 门禁 | 结果 |
| --- | --- |
| 本轮 Designer E2E | 目标用例 `1/1`；与既有协议导出、Native 二维码和二维码旋转用例联合 `5/5` |
| 覆盖范围 | 同一模板协议切换、Host 预览恢复、四种导出文件、CPCL/TSPL/ZPL 原生二维码和 ESC/POS Raster 字节标记 |
| 外部限制 | 目标打印机固件、DPI、纸宽、纠错级别、打印和扫描结果仍需硬件验收 |

本轮关闭同一二维码模板的四协议 Designer 输出子范围；图片、其他条码、复杂布局和真实设备验收继续按 TODO/`BLOCKED_EXTERNAL` 处理。

## R63 验证补充：TSPL TELEPEN/TELEPENN/CODE49 Designer E2E

`designer.spec.ts` 新增 TSPL 变量比例条码浏览器流程：页面加载 TSPL 模板后，能力面板显示类型 22–24，显式窄条/宽条模块保持 `2/4`，逐项切换 TELEPEN、TELEPENN、CODE49 并编辑数据，再通过 `生成 TSPL` 下载 ZIP，校验 `page-001.tspl` 中的原生 `BARCODE` 类型、参数和完整数据。默认静区使编码区输出原点为 `20,0`，测试按模板布局契约断言。

| 门禁 | 结果 |
| --- | --- |
| 本轮 Designer E2E | 目标用例 `1/1`；与既有 TSPL PLESSEY/ITF-14/EAN-14、ZPL DataBar 用例联合 `3/3` |
| 覆盖范围 | Host 能力快照、22–24 制式选择、TSPL 比例字段、预览恢复、ZIP 导出和三种 `BARCODE` golden 片段 |
| 外部限制 | TSC 固件版本、堆叠行数、DPI、纸宽、HRI、打印和扫描仍需硬件验收 |

本轮关闭 TSPL TELEPEN/TELEPENN/CODE49 的 Designer 浏览器子范围；其他协议浏览器流程和真实设备验收继续按 TODO/`BLOCKED_EXTERNAL` 处理。

## R62 验证补充：ESC/POS Native GS1 DataBar Designer E2E

`designer.spec.ts` 新增 ESC/POS Native GS1 DataBar 浏览器流程：页面加载 Native 模板后，能力面板显示类型 25–28，逐项切换 Omnidirectional、Truncated、Limited、Expanded，编辑固定文字并等待服务端预览，再通过 `生成 ESC/POS` 下载 ZIP，校验 `page-001.escpos` 中的 Function B 75、76、77、78 命令头和数据字节。使用显式 `NarrowModuleWidth=2` 与足够的条码预留宽度，避免混入模板占用尺寸误差。

| 门禁 | 结果 |
| --- | --- |
| 本轮 Designer E2E | 目标用例 `1/1`；与既有 ESC/POS Native 文字契约、TSPL 特殊条码和 ZPL DataBar 用例联合 `4/4` |
| Host 构建 | `dotnet restore ... --ignore-failed-sources -p:NuGetAudit=false` 与 `dotnet build ... --no-restore` 成功，0 警告、0 错误 |
| 覆盖范围 | Host 能力快照、Designer 制式选择、预览恢复、ZIP 导出和四种 Function B golden 片段 |
| 外部限制 | Epson/兼容固件、GS1 AI/FNC1、DPI、纸宽、HRI、打印和扫描仍需硬件验收 |

本轮关闭 ESC/POS Native GS1 DataBar 的 Designer 浏览器子范围；其他协议浏览器流程和真实设备验收继续按 TODO/`BLOCKED_EXTERNAL` 处理。

## R61 验证补充：ZPL GS1 DataBar Designer E2E

`designer.spec.ts` 新增 ZPL GS1 DataBar 浏览器流程：页面加载 ZPL 模板后，能力面板显示类型 25–28，逐项切换 Omnidirectional、Truncated、Limited、Expanded，修改固定文字并等待服务端预览，再通过 `生成 ZPL` 下载 ZIP，校验 `page-001.zpl` 中的 `^BR` 类型、模块宽度、条高和数据字段。使用显式 `NarrowModuleWidth=2` 与足够的条码预留宽度，避免混入模板占用尺寸误差。

| 门禁 | 结果 |
| --- | --- |
| 本轮 Designer E2E | 目标用例 `1/1`；与既有 ZPL/TSPL 条码、HRI、协议导出、介质设置和文字样式用例联合 `6/6` |
| Host 构建 | `dotnet build tools/DesignerHost/DesignerHost.csproj --no-restore` 成功；使用最新二进制运行，离线还原阶段曾触发的 `NU1900` 通过 `--ignore-failed-sources -p:NuGetAudit=false` 规避 |
| 覆盖范围 | Host 能力快照、Designer 制式选择、预览恢复、ZIP 导出和四种 `^BR` golden 片段 |
| 外部限制 | Zebra 固件、GS1 AI/FNC1、DPI、纸宽、HRI、打印和扫描仍需硬件验收 |

本轮关闭 ZPL GS1 DataBar 的 Designer 浏览器子范围；其他协议浏览器流程和真实设备验收继续按 TODO/`BLOCKED_EXTERNAL` 处理。

## R60 验证补充：ZPL GS1 DataBar 原生 Provider

依据 Zebra [`^BR`](https://docs.zebra.com/us/en/printers/software/zpl-pg/zpl-commands/%5Ebr.html) 参数语法，本轮将 GS1 DataBar Omnidirectional、Truncated、Limited 和 Expanded 接入 ZPL。独立命令输出 `^BR` 的方向、类型、放大倍数、分隔高度、条高和 Expanded 分段宽度；统一 Provider 使用同一输入校验、页面宽度和最小高度预检。`^BR` 没有普通一维条码的 HRI 参数，模板显式请求 `HumanReadable=true` 时返回带元素路径的能力错误，未指定时保留设备默认行为。

| 门禁 | 结果 |
| --- | --- |
| ZPL DataBar 专项 | `ZplDataBarProviderTests`：net6.0 `29/29`；netcoreapp3.1 `29/29` |
| Core 全量回归 | net6.0 `2640/2640`；netcoreapp3.1 `2640/2640` |
| 覆盖范围 | `^BR` golden bytes、四角方向、四种类型、Expanded 2–22 偶数分段宽度、非法输入/尺寸失败恢复、显式 HRI 拒绝、Provider 页面包络、模板能力和 CPCL/TSPL 跨协议拒绝 |
| 外部限制 | Zebra 固件对 `^BR` 的版本支持、GS1 AI/FNC1 业务语义、堆叠布局、HRI、DPI、纸宽、打印和扫描仍需硬件验收；本地 NuGet 审计源保留 `NU1900` 外部警告 |

本轮关闭 ZPL GS1 DataBar 的本地 Provider 子范围；其他协议扩展、Designer 浏览器流程和真实设备验收仍按 TODO/`BLOCKED_EXTERNAL` 记录。

## R59 验证补充：TSPL/ZPL 文字样式 Designer E2E

`designer.spec.ts` 的文字样式用例在预构建 Host 与 Vite 服务下 `1/1` 通过；覆盖 TSPL/ZPL 默认严格拒绝、介质面板开关、预览请求携带协议选项、预览恢复，以及导出包中的 `BITMAP`/`^GFA`。既有 TSPL/ZPL 介质设置 E2E 同样 `1/1`。普通 Playwright 自动启动 Host 需要在线 NuGet 还原，当前环境会触发 `NU1900`，因此使用已通过 `--no-restore` 构建的 Host 进行该浏览器验证。

本轮关闭 R58 的 Designer E2E 子范围；其他协议能力的浏览器覆盖、真实设备字高/固件/扫描仍列为 TODO 或 `BLOCKED_EXTERNAL`。

## R58 验证补充：TSPL/ZPL 文字样式显式位图降级

验证结果：`TsplZplRasterizedTextStyleTests` 在 net6.0 与 netcoreapp3.1 各 `12/12`；`TemplateFontStylePreflightTests` 各 `7/7`；Core 完整测试各 `2611/2611`；DesignerHost `109/109`；前端 Vitest `243/243`，`vue-tsc`/Vite 构建通过；Core/Templates 五目标框架构建均为 0 警告、0 错误。覆盖默认严格拒绝、显式开关能力、TSPL `BITMAP`、ZPL `^GFA`、粗体/下划线、四角旋转、Provider、模板、Host 和项目保存路径。

本轮关闭 TSPL/ZPL 文字样式的本地主机位图降级子范围；原生内置字体样式仍不宣称支持，位图路径要求图片能力，主机字体与真实设备 DPI/字高/扫描结果仍需外部验收。Designer API、Host DTO、项目保存和介质面板已接入开关，但完整浏览器 E2E 与真实设备验证继续列为 TODO。

## R56 验证补充：ESC/POS Native Function B 75–79

R57 补充验证：GS1 DataBar Expanded 官方允许的裸 `{` 原样保留，逻辑括号和 ASCII 29 FNC1 仍转义为 `{(`、`{)`、`{1`；命令输出和模板度量均有回归。

验证结果：`EscPosNativeDataBarTests` 在 net6.0 与 netcoreapp3.1 各 `15/15`；`TemplateProtocolCapabilitiesTests` 各 `16/16`；Core 完整测试各 `2597/2597`；Core 与 Templates 五目标框架构建均为 0 警告、0 错误，`git diff --check` 无错误。

R56 将 ESC/POS Native 的 GS1 DataBar Omnidirectional/Truncated/Limited/Expanded 和 Code128 auto 接入 Epson GS k Function B 75–79。统一能力矩阵、Native Provider、页面宽高预检、Designer 条码选项和失败恢复已接入；Raster/CPCL/TSPL/ZPL 保持严格拒绝。DataBar 75–77 的 13 位数字约束、Limited 首位约束、Expanded 括号/FNC1 转义、Code128 auto 原始字节入口均有 golden-byte 与失败不污染测试。规范依据 [Epson GS k 参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_lk.html)；真实 Epson/兼容机固件能力、HRI、DPI、纸宽、打印和扫描仍是外部验收项。

## R55 验证补充：ZPL Code 49 原生条码

R55 将统一 `BarcodeType.Code49` 接入 ZPL 原生 `^B4`。独立命令、能力矩阵、Provider、模板能力快照和页面宽度预检使用同一契约：1–81 个安全嵌入字段的 ASCII 字符、1–32000 点单行高度、1–10 点模块宽度，方向为 N/R/I/B，可读文字映射到 `f=B`，起始模式默认 `A`。参数依据 [Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)。

| 门禁 | 结果 |
| --- | --- |
| ZPL Code 49 专项 | `ZplCode49ProviderTests`：net6.0 `37/37`；netcoreapp3.1 `37/37` |
| TSPL Code 49 回归 | `TsplCode49ProviderTests`：net6.0 `26/26`；netcoreapp3.1 `26/26` |
| Core 编译 | `Bing.EasyPrint` net6.0：0 错误；XML 文档警告已修正 |
| 覆盖范围 | `^B4` golden bytes、HRI、显式起始模式、四角方向、非法输入/尺寸/模式失败恢复、Profile/旋转禁用、页面边界、模板能力快照和 TSPL/CPCL/ESC/POS 跨协议矩阵 |
| 外部限制 | Code 49 堆叠行数、固件支持、HRI 显示、字高、DPI、纸宽、打印和扫描仍需目标设备验收；本地 NuGet 审计源可能保留 `NU1900` 外部警告 |

本轮关闭 ZPL Code 49 子范围；ZPL 其他未映射条码、ESC/POS 其他原生条码、TSPL DPI/DPL 完整产品语义及旋转/分页组合仍列为 TODO。

## R54 验证补充：ZPL Plessey 原生条码

R54 将统一 `BarcodeType.Plessey` 接入 ZPL 原生 `^BP`。独立命令、能力矩阵、Provider、模板能力快照和页面宽度预检使用同一契约：非空 ASCII 数字、1–32000 点条高、1–10 点窄条宽度，方向为 N/R/I/B，`e=N` 不重复请求设备打印校验位，`^BY` 默认 3:1。参数依据 [Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)。

| 门禁 | 结果 |
| --- | --- |
| ZPL Plessey 专项 | `ZplPlesseyProviderTests`：net6.0/netcoreapp3.1 各 `26/26` |
| TSPL Plessey 回归 | `TsplPlesseyProviderTests`：net6.0/netcoreapp3.1 各 `14/14` |
| Core 编译 | `Bing.EasyPrint` net6.0：0 警告、0 错误 |
| 覆盖范围 | `^BP` golden bytes、HRI、四角方向、非法输入/尺寸失败恢复、Profile/旋转禁用、页面边界、模板能力快照和 CPCL/TSPL/ESC/POS 跨协议矩阵 |
| 外部限制 | Zebra 固件支持、校验位/HRI 显示、字高、DPI、纸宽、打印和扫描仍需目标设备验收；本地 NuGet 审计源保留 `NU1900` 外部警告 |

本轮关闭 ZPL Plessey 子范围；ZPL Code 49、ESC/POS 其他原生条码、TSPL DPI/DPL 完整产品语义及旋转/分页组合仍列为 TODO。

## R53 验证补充：ZPL Code 11 原生条码

R53 将统一 `BarcodeType.Code11` 接入 ZPL 原生 `^B1`。独立命令、能力矩阵、Provider、模板能力快照和页面宽度预检使用同一契约：非空 ASCII 数字/连字符、1–32000 点条高、1–10 点窄条宽度，方向为 N/R/I/B，`e=N` 由设备生成两个 Modulo-11 校验位。参数依据 [Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)。

| 门禁 | 结果 |
| --- | --- |
| ZPL Code 11 专项 | `ZplCode11ProviderTests`：net6.0 `26/26`；netcoreapp3.1 `26/26` |
| Core 全量回归 | net6.0 `2519/2519` |
| Core/Templates 多目标框架构建 | `netcoreapp3.1`、`net6.0`、`net7.0`、`netstandard2.0`、`netstandard2.1`：0 警告、0 错误 |
| 覆盖范围 | `^B1` golden bytes、HRI、四角方向、非法输入/尺寸失败恢复、Profile/旋转禁用、页面边界、模板能力快照和 TSPL/CPCL/ESC/POS 跨协议矩阵 |
| 外部限制 | Zebra 固件支持、校验位/HRI 显示、字高、DPI、纸宽、打印和扫描仍需目标设备验收；本地 NuGet 审计源保留 `NU1900` 外部警告 |

本轮关闭 ZPL Code 11 子范围；ZPL Code 49、ESC/POS 其他原生条码、TSPL DPI/DPL 完整产品语义及旋转/分页组合仍列为 TODO。

## R52 验证补充：TSPL 条码类型专用比例编辑器

R52 将 Designer 的 Code 49、TELEPEN、TELEPENN 条码属性面板切换为 TSPL 专用比例编辑器。编辑器提供 `1:1`、`1:2`、`1:3`、`2:5`、`3:7` 五档窄条:宽条比例，按能力范围计算整数 `NarrowModuleWidth`/`WideModuleWidth`，超出范围的比例禁用，旧 `Ratio` 继续作为兼容回退。

| 门禁 | 结果 |
| --- | --- |
| 比例逻辑专项 | `barcodeOptions`：`5/5` |
| Designer 全量验证 | Vitest `19` 个文件 `243/243`；`vue-tsc -b` 与 Vite 生产构建通过 |
| 覆盖范围 | 五档比例、变量条码类型识别、2:5/3:7 最小整数对缩放、能力上限拒绝、模块对反向识别和旧 Ratio 导入映射 |
| 行为边界 | 只改变 Designer 的参数表达；Provider 仍负责最终 TSPL 输入、比例、页面边界和设备能力严格校验 |

本轮关闭 Designer 比例编辑器子范围；设备字高校准、Code 49 堆叠结构和真实扫描仍需外部验收。

## R51 验证补充：TSPL Code 49 原生条码

R51 将统一 `BarcodeType.Code49` 接入 TSPL 原生 `BARCODE` 类型 `CODE49`。独立及通用入口、Provider、模板能力、宽度度量、四角度坐标、页面边界、Profile 禁用和 Designer 选择器使用同一契约；CODE49 接受 1–81 个安全嵌入字段的 ASCII 字符，宽窄比限制为 1:1、1:2、1:3、2:5、3:7。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf)。

| 门禁 | 结果 |
| --- | --- |
| TSPL Code 49 专项 | `TsplCode49ProviderTests`：net6.0 `26/26`；netcoreapp3.1 `26/26` |
| Core 全量回归 | net6.0 `2493/2493`；netcoreapp3.1 `2493/2493` |
| Core/Templates 多目标框架构建 | `netcoreapp3.1`、`net6.0`、`net7.0`、`netstandard2.0`、`netstandard2.1`：0 警告、0 错误 |
| Designer 全量验证 | Vitest `19` 个文件 `243/243`；`vue-tsc -b` 与 Vite 生产构建通过 |
| 覆盖范围 | 原生及通用入口、DEL/非法输入/长度/比例失败恢复、Profile 禁用、五种合法比例、四角度原点、页边界、跨协议能力和模板输出 |
| 宽度契约 | Provider 与模板按数据长度、起止槽位和 49 单元/内容符号保守上界，避免页面预检低估 |
| 外部限制 | TSC 手册标注 CODE49 自 V6.89EZ 起可用；堆叠行数、控制字符转义、HRI 对齐、固件、条高、DPI、纸宽、打印和扫描仍需目标设备验收 |

本轮关闭 R5 的 TSPL Code 49 子范围；Designer 类型专用比例编辑器、DPI/DPL 以及其他协议剩余条码仍列为 TODO。

## R50 验证补充：TSPL Telepen 原生条码

R50 将统一 `BarcodeType.Telepen` 与 `BarcodeType.Telepenn` 接入 TSPL 原生 `BARCODE` 类型 `TELEPEN`/`TELEPENN`。独立及通用入口、Provider、模板能力、宽度度量、四角度坐标、页面边界、Profile 禁用和 Designer 选择器使用同一契约；TELEPEN 接受 1–30 个安全嵌入字段的 ASCII 字符，TELEPENN 接受 1–60 位 ASCII 数字，宽窄比限制为 1:1、1:2、1:3、2:5、3:7。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf)。

| 门禁 | 结果 |
| --- | --- |
| TSPL Telepen 专项 | `TsplTelepenProviderTests`：net6.0 `37/37`；netcoreapp3.1 `37/37` |
| Core 全量回归 | net6.0 `2467/2467`；netcoreapp3.1 `2467/2467` |
| Core/Templates 多目标框架构建 | `netcoreapp3.1`、`net6.0`、`net7.0`、`netstandard2.0`、`netstandard2.1`：0 警告、0 错误 |
| Designer 全量验证 | Vitest `19` 个文件 `240/240`；`vue-tsc -b` 与 Vite 生产构建通过 |
| 覆盖范围 | 两个原生类型 golden bytes、通用入口、DEL/非法输入/长度/比例失败恢复、Profile 禁用、五种合法比例、四角度原点、页边界、跨协议能力和模板输出 |
| 宽度契约 | Provider 与模板均按数据长度加起止符的 16 单元保守上界，避免页面预检低估 |
| 外部限制 | TSC 手册标注两类条码自 V6.89EZ 起可用；控制字符转义、HRI 对齐、固件、条高、DPI、纸宽、打印和扫描仍需目标设备验收 |

本轮关闭 R5 的 TSPL Telepen 子范围；Code 49、DPI/DPL 及其他协议剩余条码仍列为 TODO。

## R49 验证补充：TSPL China Post 原生条码

R49 将统一 `BarcodeType.ChinaPost` 接入 TSPL 原生 `BARCODE` 类型 `CPOST`。独立命令和通用入口只接受非空 ASCII 数字，固定 1:1 窄宽比且不由主机追加校验位；Provider、模板能力、宽度度量、四角度坐标、页面边界、失败恢复和 Designer 选择器保持一致。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf)。

| 门禁 | 结果 |
| --- | --- |
| TSPL China Post 专项 | `TsplChinaPostProviderTests`：net6.0 `16/16`；netcoreapp3.1 `16/16` |
| 完整 Core/Templates 回归 | net6.0 `2430/2430`；netcoreapp3.1 `2430/2430` |
| Core/Templates 多目标框架构建 | `netcoreapp3.1`、`net6.0`、`net7.0`、`netstandard2.0`、`netstandard2.1`：0 警告、0 错误 |
| Designer 条码选项 | `barcodeOptions`/能力选择回归：Vitest `19` 个文件 `240/240`；`vue-tsc -b` 和 Vite 构建通过 |
| 覆盖范围 | 原生及通用入口、数字输入、非法数据/比例失败恢复、Profile 禁用、四角度原点、页边界、跨协议能力、模板默认 1:1 和 golden bytes |
| 外部限制 | 真实 TSC 机型的 CPOST 固件可用性、条高、模块宽、DPI、纸宽、打印和扫描验收仍未执行 |

本轮关闭 R5 的 TSPL China Post 子范围；Telepen、Code 49、DPI/DPL 等 TSPL 变体及其他协议剩余类型仍列为 TODO。

## R48 验证补充：TSPL Code 11 原生条码

R48 将统一 `BarcodeType.Code11` 接入 TSPL 原生 `BARCODE` 类型 `11`。独立命令和通用入口只接受非空 ASCII 数字/连字符，Code 11 的一个或两个 Modulo-11 校验位由设备生成；Provider、模板能力、宽度度量、四角度坐标、页面边界和失败恢复保持一致。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。

| 门禁 | 结果 |
| --- | --- |
| TSPL Code 11 专项 | `TsplCode11ProviderTests`：net6.0 `14/14`；netcoreapp3.1 `14/14` |
| 完整 Core/Templates 回归 | net6.0 `2414/2414`；netcoreapp3.1 `2414/2414` |
| Designer Code 11 选项 | `barcodeOptions`/能力选择回归：Vitest `19` 个文件 `240/240`；`vue-tsc -b` 和 Vite 构建通过 |
| 覆盖范围 | 原生及通用入口、数字/连字符输入、非法数据失败恢复、Profile 禁用、四角度原点、页边界、跨协议能力和模板字节 |
| 外部限制 | 真实 TSC 机型的 Code 11 校验位显示、条高、模块间距、DPI、纸宽、打印和扫描验收仍未执行 |

本轮关闭 R5 的 TSPL Code 11 子范围；China Post、Telepen、Code 49、DPI/DPL 等变体仍列为 TODO。完整回归过程中同步修正了 ZPL PLANET 两项仍按旧 TSPL 能力断言的测试基线，ZPL PLANET 专项两个运行时各 `39/39`。

## R47 验证补充：Designer HRI 四协议端到端矩阵

R47 使用同一条码模板验证 Designer Host 对 CPCL、TSPL、ZPL 和 ESC/POS Raster 的 HRI 输出。模板保留 12 点可读文字预留和四边显式静区，并检查各协议实际生成的页面文件及特征字节。

| 门禁 | 结果 |
| --- | --- |
| Designer Host HRI 四协议 E2E | `HRI 通过 Designer Host 输出 CPCL、TSPL、ZPL 与 ESC/POS Raster`：`1/1` |
| 输出断言 | CPCL `BT`、TSPL `BARCODE`、ZPL `^BC`、ESC/POS Raster `GS v 0` 均通过 |
| Host 启动 | 预编译 Host 使用 `--ignore-failed-sources -p:NuGetAudit=false` 启动；默认源码启动仍受 `NU1900` 审计源不可访问影响 |
| 外部限制 | 真实设备的 HRI 字高、DPI、纸宽、打印和扫描验收仍未执行 |

本轮关闭 R3 的 Designer HRI 四协议端到端子范围；完整运行时矩阵、更多静区/旋转组合和实机字高校准仍保持 TODO。

## R46 验证补充：TSPL PLANET 原生条码

R46 将统一 `BarcodeType.Planet` 接入 TSPL 原生 `BARCODE` 类型 `PLANET`，并使用同一套 12/14 位完整校验位和连字符归一化语义。Provider、能力快照、页面宽度度量、四角度坐标和模板输出均已接入。协议类型和参数依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)，设备固件差异仍需实机确认。

| 门禁 | 结果 |
| --- | --- |
| TSPL PLANET 专项 | `TsplPlanetProviderTests`：net6.0 `16/16`；netcoreapp3.1 `16/16` |
| 覆盖范围 | 原生及通用入口、连字符/校验和输入、失败恢复、Profile 禁用、四角度、页边界、能力快照和模板字节 |
| 外部限制 | 真实 TSC 机型的 PLANET 条高、模块间距、最大长度、DPI、纸宽、打印和扫描验收仍未执行 |

本轮关闭 R5 的 TSPL PLANET 子范围；China Post、Telepen、Code 49、DPI/DPL 等 TSPL 变体以及完整 Profile 组合仍列为 TODO。

## R45 验证补充：ESC/POS Raster 旋转下划线回归闭环

R45 对照 `EscPosRasterCanvas` 旋转文字路径和统一模板输出，确认下划线随 0/90/180/270 度文字掩码旋转，并由模板使用实际墨迹边界进行布局和光栅输出。专项测试在两个目标运行时均通过。

| 门禁 | 结果 |
| --- | --- |
| ESC/POS Raster 旋转下划线专项 | `EscPosRasterRotatedUnderlineTemplateTests`：net6.0 `12/12`；netcoreapp3.1 `12/12` |
| 验证范围 | 四角度下划线像素存在性、`GS v 0` 输出、旋转后布局边界和模板失败/重试契约 |
| 外部限制 | 真实 ESC/POS 热敏或针式机的 DPI、字高、纸宽、打印和扫描验收仍未执行 |

本轮关闭 R6 的 ESC/POS Raster 旋转下划线子范围；TSPL/ZPL 内置字体粗体/下划线仍因原生命令表达能力不足而保持显式 Raster 或严格拒绝。

## R44 验证补充：Designer 文字旋转占用端点同步

R44 修正 Type1 文字在已有 `EndCoordinate` 下切换旋转角度的属性编辑行为：0/180 与 90/270 之间交换旋转后占用框的宽高，保留旋转前 `Width` 行宽，并将结果写回 JSON。新增用例覆盖四角度、属性面板尺寸和导出端点。

| 门禁 | 结果 |
| --- | --- |
| Designer 文字旋转 E2E | `文字属性旋转同步已有占用端点并保留旋转前行宽`：`1/1` |
| Designer 文件级 E2E | `93/94`；唯一失败为既有 `ESC/POS Native 能力过滤工具并保留不支持值直到用户修正`，预编译 Host 的条码能力快照与断言不一致，与本轮文字旋转无关 |
| Designer 全量单测 | Vitest：`19` 文件、`240/240` |
| 前端类型检查 | `vue-tsc -b`：通过 |
| 前端生产构建 | Vite：通过；保留既有单包超过 500 kB 的提示 |
| Host 启动限制 | 本轮复用预编译 Host 完成 E2E；源码启动仍受 NuGet `NU1900` 审计源不可访问影响 |
| 外部限制 | 真实 TSPL/ZPL/ESC/POS/热敏或针式打印、扫描、字高校准和固件验收仍未执行 |

## R43 验证补充：Designer 自动尺寸图片旋转占用同步

R43 修正自动尺寸图片旋转时的属性面板与模板端点契约：从已有 `EndCoordinate` 按旧角度还原源宽高，再按新角度生成占用框。图片旋转专项覆盖服务预览、ESC/POS Native/Raster 能力边界、显式图片 JSON 往返、自动尺寸导入导出和画布局部组旋转。

| 门禁 | 结果 |
| --- | --- |
| Designer 图片旋转专项 | `imageRotation.spec.ts`：`5/5` |
| Designer 自动图片旋转 E2E | `自动图片旋转按已有端点还原原始尺寸并同步占用框`：`1/1` |
| Designer 全量单测 | Vitest：`19` 文件、`240/240` |
| 前端类型检查 | `vue-tsc -b`：通过 |
| 前端生产构建 | Vite：通过；保留既有单包超过 500 kB 的提示 |
| Host 启动限制 | 本轮复用预编译 Host 程序完成 E2E；源码启动仍受 NuGet `NU1900` 审计源不可访问影响 |
| 外部限制 | 真实 TSPL/ZPL/ESC/POS/热敏或针式打印、扫描、字高校准和固件验收仍未执行 |

## R42 验证补充：Designer 自动尺寸条码旋转占用同步

R42 修正自动尺寸条码旋转时的属性面板与模板端点契约：从已有 `EndCoordinate` 还原原始条码尺寸，再按新角度重算占用框，保留自动 `Width` 和声明的 `Height`。TSPL 页面回归使用 Host 真实预览宽度，覆盖 90 度旋转与 JSON 导出。

| 门禁 | 结果 |
| --- | --- |
| Designer 自动条码旋转 E2E | `旋转自动尺寸条码同步服务端占用端点`：`1/1` |
| Designer 二维码旋转 E2E | `旋转二维码同步占用端点并按旋转轴写回原始尺寸`：`1/1` |
| Designer 显式条码旋转/HRI 回归 | `旋转条码尺寸面板按旋转后占位编辑并写回原始宽高`、`条码 HRI 与显式静区可往返，并在关闭 HRI 时清理预留高度`：各 `1/1` |
| Designer 全量单测 | Vitest：`19` 文件、`240/240` |
| 前端类型检查 | `vue-tsc -b`：通过 |
| 前端生产构建 | Vite：通过；保留既有单包超过 500 kB 的提示 |
| Host 启动限制 | 本轮复用预编译 Host 程序完成 E2E；源码启动仍受 NuGet `NU1900` 审计源不可访问影响 |
| 外部限制 | 真实 TSPL/ZPL/ESC/POS/热敏或针式打印、扫描、字高校准和固件验收仍未执行 |

## R41 验证补充：Designer 二维码旋转占用同步

R41 修正 Designer 二维码在旋转与尺寸编辑时的统一坐标契约：0/90/180/270 度会同步 `EndCoordinate`，90/270 度下的占用尺寸编辑会反向写回原始 `Width`/`Height`。TSPL 页面回归覆盖非正方形二维码、四角度、旋转后尺寸编辑和 JSON 导出端点。

| 门禁 | 结果 |
| --- | --- |
| Designer 二维码旋转 E2E | `旋转二维码同步占用端点并按旋转轴写回原始尺寸`：`1/1` |
| Designer 条码旋转回归 | `旋转条码尺寸面板按旋转后占位编辑并写回原始宽高`：`1/1` |
| Designer 二维码/条码尺寸回归 | `二进制元素尺寸写回打印字段且条码宽窄比只开放有效值`：`1/1` |
| Designer 全量单测 | Vitest：`19` 文件、`240/240` |
| 前端类型检查 | `vue-tsc -b`：通过 |
| 前端生产构建 | Vite：通过；保留既有单包超过 500 kB 的提示 |
| Host 启动限制 | 本轮复用预编译 Host 程序完成 E2E；源码启动仍受 NuGet `NU1900` 审计源不可访问影响 |
| 外部限制 | 真实 TSPL/ZPL/ESC/POS/热敏或针式打印、扫描、字高校准和固件验收仍未执行 |

## R40 验证补充：非法 HRI 组合与 TSPL/ZPL 矩阵

R40 为 Designer 导入的 HRI 关闭但残留预留高度增加明确提示和清理入口；清理后条码坐标重新计算，服务端严格预览恢复。Core 同时覆盖 TSPL 与 ZPL 的 HRI、四边显式静区、模板布局和输出字节。

| 门禁 | 结果 |
| --- | --- |
| Designer HRI 页面 E2E | HRI/静区往返 + 非法导入修复：`2/2` |
| Core HRI 精准测试 | net6.0 `19/19`；netcoreapp3.1 `19/19` |
| Designer 全量单测 | Vitest：`19` 文件、`240/240` |
| 前端类型检查 | `vue-tsc -b`：通过 |
| 前端生产构建 | Vite：通过；保留既有单包超过 500 kB 的提示 |
| Host 启动限制 | NuGet 审计源不可访问时，使用 `--ignore-failed-sources -p:NuGetAudit=false` 恢复后以 `--no-restore` 启动 |
| 外部限制 | 真实 TSPL/ZPL/热敏或针式打印、扫描、字高校准和固件验收仍未执行 |

## R39 验证补充：Designer HRI 与显式静区

R39 修正 Designer 显式关闭条码 HRI 时清理 `HumanReadableReserveHeight` 的行为，并新增 TSPL HRI、四边静区、固定占用尺寸、JSON 导出和引擎预览往返回归。用例使用 `12345` Code128 数据并将声明宽度扩大到足以容纳编码条宽、左右静区和设备可读文字预留，关闭 HRI 后再次预览确认严格组合恢复有效。

| 门禁 | 结果 |
| --- | --- |
| Designer HRI/静区页面 E2E | `条码 HRI 与显式静区可往返，并在关闭 HRI 时清理预留高度`：`1/1` |
| Designer 全量单测 | Vitest：`19` 文件、`240/240` |
| 前端类型检查 | `vue-tsc -b`：通过 |
| 前端生产构建 | Vite：通过；保留既有单包超过 500 kB 的提示 |
| Host 启动限制 | NuGet 审计源不可访问时，需使用 `dotnet restore --ignore-failed-sources -p:NuGetAudit=false` 后以 `--no-restore` 启动；不改变项目默认配置 |
| 外部限制 | 真实 TSPL/热敏或针式打印、扫描、字高校准和固件验收仍未执行 |

## R38 验证补充：Designer TSPL 条码制式

R38 将 Designer 条码制式下拉接入后端已经支持的 TSPL `PLESSEY`、`ITF14`、`EAN14`。切换 PLESSEY/EAN14 时，属性面板按当前窄条点宽自动保持 1:2/1:1 宽条，导出模板可以直接通过后端比例预检；Host 能力快照继续控制选项禁用。

| 门禁 | 结果 |
| --- | --- |
| Designer 纯函数单测 | `barcodeOptions.test.ts`：`2/2` |
| Designer 页面交互 E2E | TSPL 三个新选项与 PLESSEY/EAN14 固定宽条导出：`1/1` |
| 前端类型检查 | `vue-tsc -b`：通过 |
| 前端生产构建 | Vite：通过；保留既有 chunk 大小提示 |
| Host 启动限制 | NuGet 审计源不可访问时，需使用 `RestoreIgnoreFailedSources=true`、`NuGetAudit=false`；不改变项目默认配置 |
| 外部限制 | 真实 TSPL/热敏或针式打印、扫描和固件验收仍未执行 |

## R37 验证补充：TSPL EAN14

R37 将统一 `BarcodeType.Ean14` 接入 TSPL 原生 `BARCODE "EAN14"`。独立命令和统一 Provider 接受 13 位 ASCII 数字 payload，严格校验固定 1:1 窄宽比并按完整 GS1-128 符号预留宽度；设备完成最终校验位。覆盖 golden bytes、通用入口、非法输入、比例边界、四方向原点、占用宽度、页面边界、模板能力/输出、Profile 禁用和跨协议能力边界。

| 门禁 | 结果 |
| --- | --- |
| TSPL EAN14 定向测试 | net6.0 `16/16`；netcoreapp3.1 `16/16` |
| Core 完整测试 | net6.0、netcoreapp3.1、netcoreapp2.2 各 `2381/2381` |
| 生产项目多目标构建 | Core/Templates 的 net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1；0 warnings、0 errors |
| 外部限制 | NuGet `NU1900` 源不可访问；真实 TSPL/热敏或针式打印、扫描和固件验收仍未执行 |

## R36 验证补充：TSPL ITF14

R36 将统一 `BarcodeType.Itf14` 接入 TSPL 原生 `BARCODE "ITF14"`。独立命令和统一 Provider 接受 13 位 ASCII 数字 payload，按完整 14 位符号预留宽度，设备完成最终校验位；覆盖 golden bytes、通用入口、非法输入失败恢复、四方向原点、占用宽度、页面边界、模板能力/输出和跨协议能力边界。

| 门禁 | 结果 |
| --- | --- |
| TSPL ITF14 定向测试 | net6.0 `15/15`；netcoreapp3.1 `15/15` |
| Core 完整测试 | net6.0、netcoreapp3.1、netcoreapp2.2 各 `2365/2365` |
| 生产项目多目标构建 | Core/Templates 的 net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1；0 warnings、0 errors |
| 外部限制 | NuGet `NU1900` 源不可访问；真实 TSPL/热敏或针式打印、扫描和固件验收仍未执行 |


## R35 验证补充：TSPL PLESSEY

R35 将统一 `BarcodeType.Plessey` 接入 TSPL 原生 `BARCODE "PLESSEY"`。独立命令和统一 Provider 接受非空 ASCII 数字，严格校验 1:2 窄宽比；主机只负责输入、布局和能力校验，最终条形编码及校验由设备完成。覆盖独立命令 golden bytes、通用入口、非法输入失败恢复、四方向原点、占用宽度、页面边界、模板能力/输出和跨协议能力边界。

| 门禁 | 结果 |
| --- | --- |
| TSPL PLESSEY 定向测试 | net6.0 `14/14`；netcoreapp3.1 `14/14` |
| Core 完整测试 | net6.0、netcoreapp3.1、netcoreapp2.2 各 `2350/2350` |
| 生产项目多目标构建 | Core/Templates 的 net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1；0 warnings、0 errors |
| 外部限制 | NuGet `NU1900` 源不可访问；真实 TSPL/热敏或针式打印、扫描和固件验收仍未执行 |

## R34 验证补充：TSPL POSTNET（POST）

R34 将统一 `BarcodeType.Postnet` 接入 TSPL 原生 `BARCODE "POST"`。TSC 手册明确 POSTNET 输入为 5、9 或 11 位数据；主机移除连字符并校验数字与长度，设备负责最终校验位。独立命令、Provider、模板、四方向、占用宽度、页边界、失败恢复、元素路径和 Profile 能力边界均覆盖。

| 门禁 | 结果 |
| --- | --- |
| TSPL/ZPL POSTNET 定向及联合回归 | net6.0、netcoreapp3.1 各 `61/61` |
| Core 完整测试 | net6.0、netcoreapp3.1、netcoreapp2.2 各 `2336/2336` |
| 生产项目多目标构建 | Core/Templates 的 net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1；0 warnings、0 errors |
| 静态差异检查 | `git diff --check` 无新增错误；仅保留既有 CRLF 转换提示 |
| 外部限制 | NuGet `NU1900` 源不可访问；真实 TSPL/热敏或针式打印、扫描和固件验收仍未执行 |

## R33 验证补充：TSPL I2OF5G（DPI/DPL）

R33 将统一 `BarcodeType.I2OF5G` 接入 TSPL。TSC 官方手册把 `DPI` 定义为 11 位 Deutsche Post Identcode、`DPL` 定义为 13 位 Deutsche Post Leitcode；主机按统一 German Post 4/9 规则生成或验证 12/14 位完整值，再发送对应 11/13 位 payload。独立命令、Provider、模板、四方向、严格输入、页边界、失败恢复、元素路径和 Profile 均覆盖。

| 门禁 | 结果 |
| --- | --- |
| TSPL I2OF5G 精准及联合回归 | net6.0、netcoreapp3.1 各 `132/132` |
| Core 完整测试 | net6.0、netcoreapp3.1、netcoreapp2.2 各 `2310/2310` |
| 生产项目多目标构建 | Core/Templates 五个目标框架；0 warnings、0 errors |
| 静态差异检查 | `git diff --check` 无新增错误；仅保留既有 CRLF 转换提示 |
| 外部限制 | NuGet `NU1900` 源不可访问；真实 TSPL/热敏或针式打印、扫描和固件验收仍未执行 |

## R32 验证补充：ZPL I2OF5G（German Post 4/9）

R32 将统一 `BarcodeType.I2OF5G` 接入 ZPL。由于 ZPL `^B2` 只提供基础 Interleaved 2 of 5，主机按 CPCL German Post 4/9 规则生成或校验校验位，再输出完整数据并关闭 ZPL Mod10；覆盖独立命令、Provider、模板、四方向、严格输入、页边界、失败恢复、元素路径和 Profile。Zebra 官方资料将 `^B2` 列为 ZPL II 的 Interleaved 2 of 5，见 [ZPL 条码说明](https://docs.zebra.com/us/en/printers/software/zpl-pg/fonts-and-barcodes/barcodes.html)。

| 门禁 | 结果 |
| --- | --- |
| I2OF5 相关精准测试 | net6.0、netcoreapp3.1 各 `146/146` |
| ZPL 受影响测试 | net6.0、netcoreapp3.1 各 `490/490` |
| Core 完整测试 | net6.0、netcoreapp3.1、netcoreapp2.2 各 `2285/2285` |
| 生产项目多目标构建 | net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1；0 warnings、0 errors |
| 静态差异检查 | `git diff --check` 无新增错误；仅保留既有 CRLF 转换提示 |
| 外部限制 | NuGet `NU1900` 源不可访问；真实 ZPL 打印、扫描和固件验收仍未执行 |

## R31 验证补充：TSPL GS1-128（UCC/EAN-128）

R31 将统一 `BarcodeType.UccEan128` 接入 TSPL `BARCODE "EAN128"`。独立命令、Provider 和模板将 ASCII 29 转换为 `!102` FNC1 控制码，并覆盖四方向、能力快照、页边界、失败恢复和元素路径诊断。TSC 官方语义见 [TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。

| 门禁 | 结果 |
| --- | --- |
| TSPL GS1-128 定向测试 | net6.0 `15/15`；netcoreapp3.1 `15/15` |
| UCC/EAN-128 相关回归 | net6.0、netcoreapp3.1 各 `40/40` |
| Core 完整测试 | net6.0、netcoreapp3.1、netcoreapp2.2 各 `2274/2274` |
| 生产项目多目标构建 | net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1；0 warnings、0 errors |
| 静态差异检查 | `git diff --check` 无新增错误；仅保留既有 CRLF 转换提示 |
| 外部限制 | NuGet `NU1900` 源不可访问；真实 TSPL/热敏或针式打印、扫描和固件验收仍未执行 |

## R30 验证补充：ZPL GS1-128（UCC/EAN-128）

R30 将统一 `BarcodeType.UccEan128` 接入 ZPL `^BC`。ZPL 独立命令和统一 Provider 输出 `>;>8` 的子集 C/FNC1 前缀，并将输入 ASCII 29 分隔符转换为 `>8`；四方向、HRI、模板能力、页边界和非法输入路径均有回归。Zebra 官方语义见 [`^BC`](https://docs.zebra.com/us/en/printers/software/zpl-pg/c-zpl-zpl-commands/r-zpl-bc.html)。

| 门禁 | 结果 |
| --- | --- |
| ZPL GS1-128 精准测试 | net6.0 `15/15`；netcoreapp3.1 `15/15` |
| Core 完整测试 | net6.0、netcoreapp3.1、netcoreapp2.2 各 `2260/2260` |
| 生产项目多目标构建 | net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1；0 warnings、0 errors |
| 静态差异检查 | `git diff --check` 无新增错误；仅保留既有 CRLF 转换提示 |
| 外部限制 | 测试恢复阶段仍有 NuGet `NU1900` 源不可访问警告；真实 Zebra 打印/扫描仍未执行 |

## R28 验证补充：ESC/POS Raster Intelligent Mail/IMb

ESC/POS Raster Intelligent Mail/IMb 已接入 `EscPosRasterCanvas`、统一能力快照、Provider 和模板渲染。主机按 USPS-B-3200 生成 65 根四状态条，覆盖 20 位跟踪码及 0/5/9/11 位路由码组合、FCS、条形映射、连字符归一化、宽度、HRI、四角旋转、模板静区和预算恢复；四组官方样例全部匹配。IMb/能力矩阵精准测试 net6.0 95/95；Core 完整测试 net6.0、netcoreapp3.1、netcoreapp2.2 各 2238/2238。真实条高、DPI、纸宽、打印/扫描和固件兼容仍属外部验收。

## R29 验证补充：FIM D/E

统一 FIM 输入已扩展到 A–E；ESC/POS Raster 新增 D=`111010111`、E=`101000101` 的 USPS 九槽位主机图形，CPCL 模板校验同步放行 D/E。FIM 精准回归 net6.0/netcoreapp3.1 各 108/108，Core 完整测试三种可用运行时各 2246/2246，多目标生产构建 0 警告、0 错误。USPS 规定的条高、条宽、位置和邮件业务适用性仍需真实设备验收。

更新时间：2026-10-06。候选为当前未提交工作区，基线 HEAD 为 `ebba49c44884b820885dc9354a5b0a24cc50b4e2`。项目版本、协议范围和依赖沿用当前候选；本报告不表示已经发布或完成设备验收。T8 门禁证据仍作为固定历史基线保存，R1 图片旋转使用独立候选证据，不覆盖 T8 旧报告或失败诊断。

## 需求与实现对应

| 原计划要求 | 当前实现及证明 |
| --- | --- |
| 保留 CPCL API、默认模板和输出行为 | 旧 `IEasyPrint.CreateCommand<T>()` 保留；CPCL 适配器沿用旧命令。`CPCL` 测试、模板默认输出和新旧语义字体字节回归包含在五运行时完整测试中。 |
| ESC/POS、TSPL、ZPL 独立命令 | `Protocols` 下三个命令类、协议工厂和扩展入口；`ProtocolGoldenBytesTests`、原始注入、编码、失败恢复及取消契约回归。 |
| 协议无关页面和元素模型 | `PrintDocument`、各元素、`IPrintProtocolProvider` 和协议 Provider；静区、占位、页边界、资源副本与归属回归。 |
| 模板按目标协议输出 | `TemplateRenderer` 收集统一文档，协议度量与 Provider 分离；分页、换行、图片、布局轨迹和路径诊断由模板与跨协议集成测试覆盖。 |
| 图片旋转 | R1 已贯通统一图片元素、四个 Provider、模板/设计器预览与导出；支持 0/90/180/270 度，旋转后的占用尺寸参与边界、静区和分页计算；Native 图片继续严格拒绝。独立候选门禁见“R1 图片旋转增量验证”，最终桌面/移动视觉 QA 已确认。 |
| 二维码旋转 | TSPL/ZPL 命令、统一模板 QR 元素、模板预检/渲染、Host `qrCodeRotations` 和 Designer 属性面板已支持 0/90/180/270 度并有 golden/JSON 往返覆盖；ESC/POS Raster 现在通过临时位图支持四角度并保留静区占位，ESC/POS Native 继续严格拒绝；CPCL 保留既有 QR 角度能力但显式静区组合仍严格校验。 |
| ESC/POS Raster 文字旋转 | Raster 画布保留旧无旋转重载，并新增顺时针 0/90/180/270 度文字掩码旋转；下划线随文字旋转，模板按实际墨迹高度预留，静区按旋转后紧致墨迹框校验，独立命令提供字号/样式/角度重载；固定字体像素映射、Provider 页边界、模板能力快照和 Host/Designer 旋转能力通过；Native 继续严格拒绝。 |
| ESC/POS Raster 条码旋转 | Code 128、Code39、Code93、EAN/UPC、Codabar、基础 I2OF5 与二维码在临时位图中完成完整渲染后支持 0/90/180/270 度；条码 HRI 预留区随整体旋转，模板/Provider 预检按旋转后占用尺寸和静区校验；独立 `RasterBarcode`/`RasterQrCode` 入口与 GS v 0 输出通过逐像素和字节回归；Native 继续严格拒绝。 |
| 统一 Code39 / Code93 / EAN / UPC / Codabar / I2OF5 / UCC/EAN-128 / MSI / POSTNET / PLANET / Intelligent Mail 条码子范围 | ESC/POS Native 的能力集合、模板预检和 Provider 分别映射 Code39/Code93/EAN8/EAN13/UPC-A/UPC-E/Codabar/I2OF5/I2OF5C/I2OF5G/GS1-128 到 GS `k` 制式 69/72/68/67/65/66/71/70/74；TSPL 统一模型继续输出既有 `BARCODE` 制式，ZPL 新增 Code39/Code93/EAN/UPC/Codabar/I2OF5/MSI/POSTNET/PLANET/Intelligent Mail 的 `^B3`/`^BA`/`^B8`/`^BE`/`^BU`/`^B9`/`^BK`/`^B2`/`^BM`/`^BZ`/`^B5`/`^BZ` 类型 3 制式。独立命令复用各制式校验，模板层补齐 EAN/UPC 缺少的校验位，并对奇数 I2OF5 输入前缀补 `0`；I2OF5C 按 3/1 规则补齐校验位，I2OF5G 按 4/9 规则补齐或验证校验位；GS1-128 独立入口接受 2–255 字节 7-bit ASCII，统一模板保留数字应用标识开头语义，AI、FNC1 和业务校验由调用方提供；统一 MSI 使用 `^BMA` 保留输入数字且不追加校验位，POSTNET 接受 5/9/11 位数字并移除连字符，PLANET 接受 12/14 位数字并检查总和校验位，IMb 接受 20/25/29/31 位数字且不追加校验位，直接命令可选择 A-D 或 N/R/I/B。ESC/POS Raster 通过 ZXing 主机编码接入 Code39/Code93/EAN/UPC/Codabar/基础 I2OF5；GS1-128、I2OF5C/I2OF5G、MSI、POSTNET、PLANET、IMb 以及其他没有可靠主机编码器的类型仍由能力集合和模板预检严格拒绝。Codabar 的 A–D 守卫字符、I2OF5 基础 ITF 长度、ZPL Codabar 起止字符拆分、ZPL I2OF5 偶数数字校验、GS1-128/MSI/POSTNET/PLANET/IMb 长度、各条码字符集、golden、非法字符、规范化和能力矩阵已覆盖。其他 ESC/POS 条码及剩余 ZPL/TSPL 邮政变体仍保留为独立 TODO。 |
| TSPL Native MSI | `BarcodeType.Msi` 已接入 TSPL `BARCODE "MSI"`，统一 Provider、能力快照、模板四角度、元素路径预检、1–13 位数字校验、失败恢复和跨运行时 golden 字节均已覆盖；统一语义不追加校验位，`MSIC` 等扩展仍须显式原始指令。 |
| TSPL Native I2OF5/I2OF5C/I2OF5G | `BarcodeType.I2OF5`/`I2OF5C`/`I2OF5G` 已接入 TSPL `BARCODE "25"`/`"25C"`/`"DPI"` 或 `"DPL"`，数字输入按 TSC 要求归一化，I2OF5G 按 11/13 位 payload 选择 German Post 类型并由主机校验 4/9 语义；模板支持四角度、Profile 禁用、失败恢复和能力边界。 |
| ZPL Native I2OF5C | `BarcodeType.I2OF5C` 已接入 ZPL `^B2` 的 `e=Y` 校验位路径，新增 `BarcodeI2OF5C` 独立入口；偶数输入补前导 `0`、奇数输入保持，覆盖 N/R/I/B 四方向、Profile 禁用、失败恢复、模板预检和跨协议边界。 |
| ZPL Native POSTNET | `BarcodeType.Postnet` 已接入 ZPL `^BZ` Postnet 形式，支持 N/R/I/B 四方向、5/9/11 位数字、连字符归一化、严格长度/字符/尺寸校验、Provider/模板能力预检、Profile 禁用和跨协议拒绝。 |
| ZPL Native PLANET | `BarcodeType.Planet` 已接入 ZPL `^B5`，支持 N/R/I/B 四方向、12/14 位数字、数字和校验位、连字符归一化、严格长度/字符/尺寸校验、Provider/模板能力预检、Profile 禁用和跨协议拒绝；专项双运行时各 41/41。依据 [Zebra Programming Guide](https://cpws.zebra.com/cpws/docs/zpl/zpl_manual.pdf) 与 [USPS PLANET 说明](https://about.usps.com/postal-bulletin/2007/html/pb22214/cc.htm)，实际扫描结果仍需目标设备验收。 |
| ZPL Native Intelligent Mail | `BarcodeType.IntelligentMail` 已接入 ZPL `^BZ` 类型 3，支持 N/R/I/B 四方向、20/25/29/31 位数字、连字符归一化、严格长度/字符/尺寸校验、Provider/模板能力预检、Profile 禁用和跨协议拒绝；专项 net6.0/netcoreapp3.1 各 52/52。依据 [Zebra Programming Guide](https://cpws.zebra.com/cpws/docs/zpl/zpl-zbi2-pm-en.pdf) 与 [USPS Barcode Standards](https://pe.usps.com/text/dmm300/204.htm)，实际扫描结果仍需目标设备验收。 |
| ESC/POS Native UCC/EAN-128 | `BarcodeType.UccEan128` 已接入 ESC/POS Native `GS k 74`，新增 `EscPosPrintCommand.BarcodeUccEan128`，覆盖 2–255 字节 7-bit ASCII、Epson 特殊数据/FNC1 保留、严格长度、失败恢复、统一模板 Provider、能力快照、Raster/TSPL/ZPL 边界和模板预检；专项 net6.0/netcoreapp3.1 各 13/13，相关既有基线各 109/109。AI、FNC1、校验与固件扫描语义仍由调用方和目标 Epson 机型负责，协议依据为 [Epson GS k 参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_lk.html)。 |
| ESC/POS Raster 多条码主机光栅化 | Raster Provider 与统一模板已接入 Code39、Code93、EAN8、EAN13、UPC-A、UPC-E、Codabar、基础 I2OF5 的 ZXing 主机编码、类型化宽度测量、四角度旋转/HRI 和严格能力快照；GS1-128、I2OF5C/I2OF5G、MSI、POSTNET、PLANET、Intelligent Mail 仍严格拒绝；专项 net6.0/netcoreapp3.1 各 15/15，相关回归各 176/176。 |
| ESC/POS Raster / Native | 主机光栅布局及顺序 Native 输出；显式 Native 混合收据、文字契约与制表校准，不能表达的坐标和样式严格拒绝。Native 支持 24 点顺序文字的粗体、单线下划线和反白；Raster 反白仍严格拒绝。 |
| 公共能力和严格预检 | `PrintCapabilities`、`TemplateProtocolCapabilities`、语义字体及选项快照；静态预检在读取资源前拒绝，动态内容继续在编码前校验。 |
| 份数、进纸、裁切和独立文件 | `PrintJobRenderer`、页和 Job 收尾策略、设备 Profile、TSPL/ZPL 受控裁切；ZPL Delayed 输出独立控制文件和有序 ZIP 清单。 |
| Host / 设计器 | 协议和能力选择、配置保存、固定字体上传、稳定错误诊断、版本/特性协商；Host、HTTP、前端单元、完整 E2E 和视觉 QA。 |
| 累计预算、取消与失败恢复 | 引擎自有缓冲、模型/记录/列表/索引、模板绑定与资源存储，以及 Host 请求预算和准入。第三方内部对象及同步调用边界见 [protocols.md](protocols.md#累计资源预算与取消)。 |

四协议均已接入。支持范围由细粒度能力表达，不宣称四协议的所有元素、样式或原生命令集合完全相同。

## T8 固定基线验证

`artifacts/t8-runtime-tests/20261005-111638-f523cd4f/runtime-verification.json` 保存实际 testhost 运行时、配置、诊断与 TRX 路径。下面五次均零失败、零跳过；旧运行时通过工作区便携 host 执行，没有修改系统安装或测试依赖。

| 测试 TFM | 实际运行时 | 完整通过 |
| --- | --- | --- |
| net7.0 | 7.0.20 | 1469 / 1469 |
| net6.0 | 6.0.36 | 1469 / 1469 |
| net5.0 | 5.0.17 | 1469 / 1469 |
| netcoreapp3.1 | 3.1.32 | 1469 / 1469 |
| netcoreapp2.2 | 2.2.8 | 1469 / 1469 |

测试 TFM 与生产包资产 TFM 不相同：生产包为 net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1；net5.0/Core 2.2 测试是兼容消费者。netstandard 本身不能作为独立运行时执行。

| 门禁 | 结果与证据 |
| --- | --- |
| Solution 全目标串行构建 | `dotnet build Bing.EasyPrint.sln --no-restore -m:1 /p:BuildInParallel=false` 通过；包含新诊断断言修复。 |
| Host 独立完整测试 | 98 / 98，`artifacts/t8-fixed-font-tests/designer-host-net8.trx`；生产 Host 范围随后未变。 |
| 当前 Host 实际 HTTP | 33 项通过，`artifacts/t8-final-smoke.json`。五种输出配置、版本/特性拒绝、未知字段、绑定诊断、charset、413/503 和断开后的恢复；确认独立 Host 的已加载模块并清理本次实例。 |
| 前端单元与生产构建 | 225 / 225，vue-tsc/Vite 通过；本轮只改后端测试断言、验证工具与文档，前端未变范围复用 T8 固定字体阶段证据。 |
| 完整 Playwright E2E | 121 / 121，零跳过/失败/不稳定；外部证据目录的 `t8-fixed-font-full.json`。该次使用 S5 storage Host 输出目录，与 T8 Host 的相关程序集相同，按未变生产范围复用。 |
| 固定字体 UI 与视觉 QA | 桌面 1440×900、移动 390×844；实际上传 Regular/Bold、能力、预览/导出、字体字节保存和重载输出一致性通过。`t8-fixed-font-qa.json`，无 page/console 错误；最终截图确认当前尺寸和移动端无横向溢出。 |
| 本地打包 | `artifacts/t8-fixed-font-pack` 的 Core/Templates 两包；两包均包含五个生产 TFM，不含测试字体。 |
| 独立包消费者 | PASS：五个默认目标框架和一个显式 `netstandard2.1` 资产案例均使用当前候选包隔离消费；六份报告均确认运行时成功和候选包哈希一致。 |

UI 证据目录为 `C:/Users/jianx/.codex/visualizations/2026/10/03/01a10405-cc4c-7f91-97a5-bc7cdf4c823c`。最终截图为 `t8-fixed-font-desktop.png`、`t8-fixed-font-mobile.png`；旧尺寸/侧栏过渡状态截图保留为 `*-diagnostic.png`，不作为最终视觉通过证据。Browser plugin 不可用，该阶段使用仓库 Playwright。

## R1 图片旋转增量验证

R1 的实现、后端门禁、浏览器自动化、视觉 QA 和包消费者已完成独立验证；这些证据不能回写或替代上面的 T8 固定基线。初始 `r1-image-mobile.png` 仅是动画过渡诊断截图，最终视觉门禁使用确认后的截图和独立 QA 进程结果。

| 门禁 | 结果与证据 |
| --- | --- |
| 五个实际运行时完整测试 | 五个 TFM 各 1509 / 1509，零失败/跳过；`artifacts/r1-image-runtime-tests/20261005-121259-edb9a152/runtime-verification.json`。 |
| DesignerHost 完整测试 | 103 / 103；`artifacts/r1-image-host-confirmed-tests/designer-host-net8.trx`。 |
| 当前 Host 实际 HTTP | 82 项 PASS；`artifacts/r1-image-smoke.json`。包含四角度输出、能力数组、预览尺寸、原生图片拒绝和预算/取消路径。 |
| 前端单元与生产构建 | 233 / 233；`npm run test -- --reporter=dot`、`vue-tsc` 和 Vite 构建均通过。该结果来自本轮真实命令输出。 |
| 完整浏览器验证 | 126 / 126（125 个设计器 E2E + 1 个外部 QA）；`C:/Users/jianx/.codex/visualizations/2026/10/03/01a10405-cc4c-7f91-97a5-bc7cdf4c823c/r1-image-full.json`，进程退出码 0。 |
| 最终视觉 QA | `r1-image-qa-confirmed-run.json` 1 / 1，`r1-image-qa-confirmed.json` 为 PASS；实际 URL 为 `http://127.0.0.1:61081`，1440×900 与 390×844 均无错误、无横向溢出，模板包重载后输出字节一致。正式截图为 `r1-image-desktop-confirmed.png`、`r1-image-mobile-confirmed.png`。 |
| R1 候选包与消费者 | Core/Templates 两包位于 `artifacts/r1-image-pack`；六个隔离消费者的 `consumer-verification.json` 位于 `artifacts/r1-image-consumers-confirmed/`，均 PASS 且确认候选包归档一致。该包证据属于 R1 候选，不代表正在推进的 R2 最新候选。 |

Browser plugin 不可用，本轮使用仓库 Playwright。初始移动截图保留为诊断证据，最终视觉判断以确认后的截图和 QA 报告为准。

## R2 填充矩形增量验证

R2 已完成 TSPL/ZPL 原生填充命令、统一模型、模板、Host 能力、设计器预览及保存导出。公共 `PrintRectangleElement.CreateFilled` 与模板 `Filled=true` 使用精确实心区域；旧 CPCL 构造器的 IL 反色字节保留。ZPL 32000 点上限在绑定和打开资源前静态拒绝，填充以整块参与分页和静区检查。设计器填充不叠加描边，并使用组件定义路径解析属性。

| 门禁 | 当前 R2 证据 |
| --- | --- |
| L0 / 全目标构建 | Solution 串行 Debug build、`git diff --check` PASS；五个生产 TFM，无新增编译警告 |
| L1 精准回归 | 113/113，`artifacts/r2-filled-contract-confirmed-tests/r2-filled-contract-net6.trx`；字节、公开工厂、旧 CPCL、静态资源预检、分页、静区、取消及预算恢复 |
| L2/L4 五运行时完整 Core/Templates | `artifacts/r2-filled-runtime-tests/20261005-132414-8efb89e6/runtime-verification.json`，7.0.20、6.0.36、5.0.17、3.1.32、2.2.8 均 1555/1555，零失败/跳过 |
| L3 Host | `artifacts/r2-filled-host-tests/designer-host-net8.trx`，108/108，使用当前 `artifacts/r2-filled-host-build` |
| 前端 | 235/235，vue-tsc/Vite PASS；本轮真实命令输出，包含去掉填充描边的最新修改 |
| 浏览器 / 实际 HTTP | 外部目录 `r2-filled-full-confirmed.json`，130/130（129 仓库 E2E + 1 QA），零跳过/失败/不稳定；标签机型禁用图片仍可填充、Native 拒绝、保存重载与导出 |
| 桌面 / 移动视觉 QA | `r2-filled-qa.json` PASS、`r2-filled-desktop.png` / `r2-filled-mobile.png`；URL `http://127.0.0.1:61081`，1440×900 / 390×844，无错误/页面横向溢出；黑色区域比例正确、描边为零，重载后 70 字节 TSPL 输出一致 |
| 当前包资产 | `artifacts/r2-filled-pack-confirmed`，显式 Debug 打包；`package-build-verification.json` 确认两包的全部十个 DLL 与本轮 Debug 构建逐一一致 |
| 隔离包消费者 | 六份 `artifacts/r2-filled-consumers-confirmed/**/consumer-verification.json` 全部 PASS；五默认 TFM 和显式 netstandard2.1，覆盖新命令/公开工厂、四 Provider、V2 模板往返与两页标签 |

UI 证据仍在前述外部目录。Browser plugin 不可用，使用仓库 Playwright；已检查两张最终截图。首轮 128 PASS/2 FAIL 是测试误断言 `labelProfile` 内不存在的开关，已改为实际 `elementTypes` 能力断言，正式确认见独立目录。首轮包未显式指定配置而选入旧 Release DLL，`artifacts/r2-filled-pack` 和 `artifacts/r2-filled-consumers` 仅作失败诊断；正式包为 `*-confirmed`。首轮 ZPL 高度测试被默认 MaxPageHeight 提前拒绝的诊断保留在 `artifacts/r2-filled-contract-tests`，测试显式设置自身页面上限后确认静态协议拒绝。

Changed Scope：Core 填充命令/模型/能力/Provider/静区、Templates 元素/加载/布局/预检、Host 契约测试、设计器模型/能力/属性/画布及 E2E、PackageConsumer 和说明。RiskLevel：MEDIUM。目标框架、依赖、版本、包图和 Benchmark Harness 未变；L5 不重跑无关重型资源实验。R1/T8 仍独立保存。

复现 R2 使用下方通用入口，但将运行时/Host 证据目录改为 R2 路径，包命令必须显式添加 `-c Debug`。消费者需传入 `-CandidatePackagesDirectory artifacts/r2-filled-pack-confirmed -ResultsDirectory artifacts/r2-filled-consumers-confirmed`，五个默认 TFM 及显式 netstandard2.1 均执行；初次恢复不用 `-NoRestore`。浏览器确认入口为外部目录的 `r2-filled-playwright-confirmed.config.cjs`，指向同一 R2 Host DLL 和本地 Vite；临时服务已按模块/启动时间确认归属后停止。

## 通用验证入口

准备官方 Windows x64 便携 Runtime ZIP：7.0.20、5.0.17、2.2.8，分别解压到 `artifacts/test-runtimes/<version>`。来源为 Microsoft 的 `https://dotnetcli.blob.core.windows.net/dotnet/Runtime/<version>/dotnet-runtime-<version>-win-x64.zip`。验证脚本不下载或安装运行时；缺 host 明确失败。6.0.36、3.1.32 当前使用系统安装。需要相同 Windows 字库环境完成默认系统字体兼容场景，固定字库准备见 [测试字体说明](../tests/Fixtures/Fonts/README.md)。

```powershell
dotnet build Bing.EasyPrint.sln --no-restore -m:1 /p:BuildInParallel=false
./tools/Verify-ProtocolRuntimes.ps1 -NoBuild -NoRestore -ResultsDirectory artifacts/t8-runtime-tests
./tools/Verify-DesignerHost.ps1 -NoRestore -OutputDirectory artifacts/t8-host-verification-bin -ResultsDirectory artifacts/t8-host-verification-tests
$transitive = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
$runtimes = @(
    @{ TargetFramework = 'net7.0'; PortableDotNet = 'artifacts/test-runtimes/7.0.20/dotnet.exe' }
    @{ TargetFramework = 'net6.0'; PortableDotNet = $null }
    @{ TargetFramework = 'net5.0'; PortableDotNet = 'artifacts/test-runtimes/5.0.17/dotnet.exe' }
    @{ TargetFramework = 'netcoreapp3.1'; PortableDotNet = $null }
    @{ TargetFramework = 'netcoreapp2.2'; PortableDotNet = 'artifacts/test-runtimes/2.2.8/dotnet.exe' }
)
foreach ($entry in $runtimes) {
    $arguments = @(
        '-TargetFramework', $entry.TargetFramework
        '-TransitivePackagesDirectory', $transitive
        '-ResultsDirectory', 'artifacts/t8-package-consumer-public-api'
    )
    if ($entry.PortableDotNet) {
        $arguments += @('-PortableDotNet', $entry.PortableDotNet)
    }
    & ./tools/Verify-PackageConsumer.ps1 @arguments
}
./tools/Verify-PackageConsumer.ps1 -TargetFramework net5.0 -PackageAssetFramework netstandard2.1 `
    -PortableDotNet artifacts/test-runtimes/5.0.17/dotnet.exe -TransitivePackagesDirectory $transitive `
    -ResultsDirectory artifacts/t8-package-consumer-asset-confirmed
```

运行时验证每次创建独立证据目录，串行测试，预先声明 180 秒会话窗口。必须同时满足进程成功、全部测试通过、零跳过和实际 testhost 运行时匹配，才记录 PASS。
包消费者也按目标框架串行执行；为新的候选包使用新的 `-ResultsDirectory`，避免把旧输出当作当前资产证据。`-NoRestore` 只适用于同一候选、同一恢复目录已经准备好的重复执行。

### 当前包消费者证据

消费者只引用候选目录中的 `Bing.EasyPrint` 与 `Bing.EasyPrint.Templates` 包，依赖包从独立的 transitive cache 恢复；脚本会比较候选归档和实际恢复归档的 SHA-256，并记录 `project.assets.json` 的实际资产。六个案例如下，表中的资产顺序均为 Core / Templates：

| 消费目标 | 实际运行时 | 资产选择 | 结果证据 |
| --- | --- | --- | --- |
| `net7.0` | 7.0.20 | 默认 `net7.0` / `net7.0` | `artifacts/t8-package-consumer-public-api/consumer-net7.0/consumer-verification.json` |
| `net6.0` | 6.0.36 | 默认 `net6.0` / `net6.0` | `artifacts/t8-package-consumer-public-api/consumer-net6.0/consumer-verification.json` |
| `net5.0` | 5.0.17 | 默认 `netcoreapp3.1` / `netcoreapp3.1` | `artifacts/t8-package-consumer-public-api/consumer-net5.0/consumer-verification.json` |
| `netcoreapp3.1` | 3.1.32 | 默认 `netcoreapp3.1` / `netcoreapp3.1` | `artifacts/t8-package-consumer-public-api/consumer-netcoreapp3.1/consumer-verification.json` |
| `netcoreapp2.2` | 2.2.8 | 默认 `netstandard2.0` / `netstandard2.0` | `artifacts/t8-package-consumer-public-api/consumer-netcoreapp2.2/consumer-verification.json` |
| `net5.0` + `-PackageAssetFramework netstandard2.1` | 5.0.17 | 显式候选资产 `netstandard2.1` / `netstandard2.1`，两个输出 DLL 均 `OutputMatched=true` | `artifacts/t8-package-consumer-asset-confirmed/consumer-net5.0-netstandard2.1/consumer-verification.json` |

六份报告的 `Status`、`Runtime.Status` 和退出码均为 PASS/0，`CandidatePackagesMatched=true`。两份候选包在六次恢复中均哈希一致：Core `778F6351BB2DB5ACC64DC8C96F61ED9672A86DE95AB1A816A4AA49C5FB7719A2`，Templates `DC6E33744AB17374E8BD72DDC20BC56C49C2D659560DE984DE4D2D32ABD2B49B`。`net5.0` 的默认 NuGet 选择是 `netcoreapp3.1`；最后一行是显式从同一已校验候选包引用 `netstandard2.1`，不把它描述成 NuGet 默认选择。

早期消费者失败诊断输出仍保留在各自的 `artifacts/t8-package-consumer-*-diagnostic.log`、`*-confirmed.log`、`*-final.log` 和 `*-netstandard21.log` 路径；它们没有被覆盖，也不计入上述六个正式 PASS 案例。

## 诊断、证据复用和影响范围

首次 net7.0 完整测试为 1464 通过 / 5 失败，保存在 `artifacts/t8-fixed-font-tests/fonts-core-final-net7.trx`。该文件虽含 `final` 名称，仍是诊断失败，不能计入通过矩阵。根因为五个测试硬编码同值枚举别名；不同运行时的 `Enum.ToString()` 选择不同名称。只将断言改为对应协议值的当前表示，保留协议存在、路径和原因检查，未修改生产协议名称、字节或错误类型。修复后五运行时完整矩阵才作为当前正式证据。

ChangedFiles：T8 基线中的五处断言所在四个测试文件、验证工具、独立消费者及文档。ChangedProjects：测试/验证消费者。ChangedPublicContracts、ChangedRuntimePaths（生产）、ChangedProviders、ChangedTFMs（生产）、ChangedDependencies：无。ChangedBuildPackaging：新增包验证入口，不改变包内容或版本。RiskLevel：LOW，跨运行时和包消费门禁为 T8 检查点。S5/S8/字体生产实现和前端随后未变，复用其相关阶段证据；不重跑无关重型资源矩阵。

R1 影响范围：图片元素模型与四个 Provider 的栅格化、模板布局/分页、Host 能力序列化、设计器属性与预览，以及对应测试、包和消费者验证；公共图片构造器保留旧六参数调用，零度输出保持兼容。R1 使用独立候选与视觉 QA，R2 当前包的证明来自其专用目录，二者不能混用。RiskLevel：MEDIUM。

R3 影响范围：统一 HRI 能力描述、CPCL `BT` 输出、ESC/POS Native/Raster HRI、Raster 主机字体墨迹、全局字号配置和预留高度预检、模板能力路径、Host DTO/能力响应、Designer 设置与项目往返。当前 net6 与 netcoreapp3.1 完整测试各 1561/1561，DesignerHost net8 为 109/109，前端 Vitest 为 237/237 且 vue-tsc/Vite 构建通过；候选包 `artifacts/r3-hri-size-pack` 两包打包成功，`artifacts/r3-hri-size-consumers` 的 net6/netcoreapp3.1 消费者均 PASS。net7、net5、netcoreapp2.2 本机缺少对应运行时，本轮不把它们列为当前运行时通过。Raster HRI 默认 12 点，也可通过 `EscPosDeviceOptions.RasterBarcodeHumanReadableFontSize` 配置 1–255 点；实机字高校准和静区组合覆盖仍是后续 TODO。RiskLevel：MEDIUM。

R4/R5/R6/R7/R8/R9/R10/R11 影响范围：TSPL/ZPL 二维码命令增加显式四角度输出，ESC/POS Raster 文字、Code 128 条码和二维码新增临时位图四角度旋转；此前已接入 ESC/POS Native/TSPL Code39 和 Native Code93 统一映射，上一轮新增 ESC/POS Native EAN8/EAN13/UPC-A/UPC-E 的能力集合、GS `k` 65–68 映射、模板校验位规范化、模板预检和输入校验；再上一轮新增 Native Codabar/I2OF5 的能力集合、GS `k` 71/70 映射、Codabar 守卫字符校验、I2OF5 奇数模板补零和输入校验；R11 新增 Native I2OF5C/I2OF5G 的 3/1、4/9 校验位规范化、`GS k 70` 输出、模板路径预检和失败恢复；前几轮新增 ZPL Code39/Code93 的 `^B3`/`^BA` 独立命令、统一 Provider、能力快照、四角度、可选校验位显示、模板预检和非法输入校验；上一轮新增 ZPL EAN8/EAN13/UPC-A/UPC-E 的 `^B8`/`^BE`/`^BU`/`^B9` 命令、统一 Provider、能力集合、零售校验位处理和 UPC-E 展开；R8 新增 ZPL Codabar/I2OF5 的 `^BK`/`^B2` 命令、统一 Provider、能力集合、四角度、Codabar 起止字符拆分、I2OF5 偶数数字校验、尺寸度量、模板预检和 golden-byte 回归；R9 新增 ESC/POS Native `Underline` 命令、`ESC - n` 启用/关闭字节、Native 能力快照、Provider 文字状态切换、模板下划线预检/渲染和跨运行时回归；R10 新增 ESC/POS Native `Reverse` 命令、`GS B n` 启用/关闭字节、Native 能力快照、Provider 文字状态切换、模板反白预检/渲染、Designer 开关和跨运行时回归；下划线与反白及 HRI 预留区随动，模板按实际墨迹/码图占用排版，静区按旋转后占位校验，独立 Raster 文字/条码/二维码命令增加公开重载，CPCL 统一模型直连条码收紧 90/180 度并保留模板 270 度适配，统一能力、模板预检/渲染、Host/Designer 旋转选择及 Provider 页边界校验贯通。本轮 Core/Templates 完整测试 net6 与 netcoreapp3.1 各 1833/1833，I2OF5C/I2OF5G 专项各 16/16、相关联合回归各 216/216，ESC/POS Native 反白精准 74/74、下划线精准 74/74，ZPL Codabar/I2OF5 专项 43/43，ZPL 零售专项 47/47，ZPL Code93 精准 32/32、Code39 精准 24/24；Codabar/I2OF5 精准 15/15，联合协议能力/模板能力回归 95/95；EAN/UPC 精准 21/21、ESC/POS Code93 精准 6/6、Code39 精准 8/8、ProtocolRotation 69/69、码图旋转双运行时 18/18、模板 Raster 码图双运行时 3/3、DesignerHost 109/109；包消费者与前端范围无生产变更，复用既有证据。剩余其他 ESC/POS 条码、旋转下划线组合、组合 E2E、运行时矩阵和实机方向验证仍是后续 TODO。RiskLevel：MEDIUM。

原有 NU1900（漏洞源不可访问）、NU5104（Drawing 预发布依赖）、包 README 与前端大 chunk 提示保留，不把它们包装为漏洞检查或正式性能门禁通过。没有性能改善、进程堆上限或批准容量阈值的声明。

R12 影响范围：ZPL `^BM` MSI 原生命令、A-D 校验方案、N/R/I/B 四方向、统一 Provider/能力矩阵、模板预检、失败恢复和公开 API 文档；统一 `BarcodeType.Msi` 使用 A 方案无校验位以保持现有模型语义，直接命令可显式选择 A-D。ZPL MSI 专项在 net6.0 与 netcoreapp3.1 各 35/35，Core/Templates 完整各 1868/1868，DesignerHost 109/109；Core/Templates 五 TFM L0 构建无错误。RiskLevel：MEDIUM。

R13 影响范围：TSPL `BARCODE "MSI"` 类型映射、统一 `BarcodeType.Msi` 能力矩阵、1–13 位数字校验、Provider/模板四角度锚点、失败恢复、TSPL MSI 专项测试以及协议/模板文档。TSPL MSI 专项在 net6.0 与 netcoreapp3.1 各 21/21，Core/Templates 完整各 1888/1888；Core/Templates 五 TFM L0 构建 0 错误。旧 ZPL MSI 边界测试已同步为 TSPL 支持、ESC/POS 拒绝。RiskLevel：MEDIUM。

R14 影响范围：ZPL `^BZ` POSTNET 独立命令、`BarcodeType.Postnet` 统一能力矩阵、5/9/11 位输入及连字符归一化、四方向输出、Provider/模板预检、尺寸度量、Profile 禁用、失败恢复和协议/模板/设计器文档。ZPL POSTNET 专项在 net6.0 与 netcoreapp3.1 各 35/35，Core/Templates 完整各 1923/1923，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 错误；R14 候选包在 `artifacts/r14-postnet-pack` 重新打包，`net6.0` 与 `netcoreapp3.1` 隔离消费者均 PASS，其他消费者 TFM 未在本轮重跑。PLANET 已在 R15 另行接入，Intelligent Mail/IMB 等 `^BZ` 变体仍未接入统一 Provider。RiskLevel：MEDIUM。官方命令参数依据 [Zebra Programming Guide](https://cpws.zebra.com/cpws/docs/zpl/zpl_manual.pdf)，实际扫描结果仍需目标设备验收。

## 剩余项与状态

### Completed

- [x] T1–T8、S1–S8 的旧本地门禁保持 CLOSED，旧 16/16 计数作为固定基线保留。
- [x] R1 图片旋转的模型、Provider、模板、Host、设计器、四角度字节输出、预算/取消、126 项浏览器自动化、最终视觉 QA 和六个包消费者案例均已完成；初始过渡截图保留为诊断证据。
- [x] R2 填充矩形：原生命令、公开实心工厂、兼容 CPCL、模板/设计器、静态尺寸预检、分页/静区/预算恢复与本轮完整门禁均已闭环，见 R2 独立证据。
- [x] R4 二维码旋转增量：TSPL/ZPL 原生命令支持 0/90/180/270 度，统一模板 QR 元素、能力快照、模板预检/渲染、Host 能力响应、Designer 属性面板和 JSON 往返已接入；golden、模板集成、Host 109/109、前端 238/238、双运行时 1575/1575 与包消费者通过。该项只关闭 R4 的二维码子范围，不代表 R4 整体闭环。
- [x] R4 ESC/POS Raster 文字旋转增量（R4 候选证据）：Raster 画布以固定字体临时掩码支持 0/90/180/270 度和随动下划线，模板按实际墨迹高度排版，静区校验覆盖旋转后的紧致墨迹框，独立命令保留旧调用并增加公开字号/样式/角度重载；240/240 精准、双运行时 1587/1587、Host 109/109、前端 238/238、构建和两目标包消费者通过。该项只关闭 R4 的文字子范围，不代表 R4 整体闭环。
- [x] R5 ESC/POS Raster 码图旋转增量：Code 128 与二维码按完整临时位图支持 0/90/180/270 度，HRI 预留区随条码整体旋转；新增 `RasterBarcode`/`RasterQrCode` 独立坐标入口，统一能力、Provider 页边界、模板预检/渲染、静区占位和 GS v 0 输出均已覆盖。精准测试 258/258，码图旋转双运行时 18/18，模板 Raster 码图双运行时 3/3。该项只关闭 R4/R5 的 Raster 码图子范围，不代表更多条码类型或整体闭环。
- [x] R4 CPCL 统一模型直连角度边界：`PrintDocument` 直连 CPCL 条码拒绝 90/180 度并返回元素路径，0/270 保持输出；模板适配层继续使用内部 VB 映射，旧 `CPCLPrintCommand` 原始 API 不变。ProtocolRotation 69/69；当前 Core/Templates 双运行时完整各 1619/1619（该轮记录时为 1611/1611）。该项只关闭 CPCL 直连角度子范围，不代表剩余组合或实机方向验收。
- [x] R5 Code39 统一条码子范围：ESC/POS Native 与 TSPL 的统一 Provider 输出、能力快照和模板路径检查已闭环；Native 输出 GS `k` 69，TSPL 输出 `BARCODE "39"`，Raster 通过 ZXing 主机光栅化，ZPL 保持原生输出；小写/非法字符在编码前拒绝。Code39 8/8、R18 Raster 专项覆盖、协议能力与模板能力均已回归。
- [x] R5 Code93 统一条码子范围：ESC/POS Native 的能力快照、模板预检和 Provider 输出 Code93 的 GS `k` 72；Raster 通过 ZXing 主机光栅化，ZPL 保持原生输出；TSPL 通过 `BARCODE ...,"93",...` 输出；独立命令 7-bit ASCII 校验、非 ASCII 错误路径和 Native/Raster/TSPL 模板成功均已覆盖。Code93 精准与联合协议能力/模板能力回归通过。
- [x] R5 EAN/UPC 统一零售条码子范围：ESC/POS Native 的能力快照、模板预检和 Provider 分别输出 EAN8/EAN13/UPC-A/UPC-E 的 GS `k` 68/67/65/66；Raster 通过 ZXing 主机光栅化；独立命令校验与模板层缺位补齐、错误校验位拒绝和 Native/Raster 模板成功均已覆盖。零售码精准与联合协议能力/模板能力回归通过；TSPL 保留既有 EAN/UPC 集合并新增 Code93。
- [x] R5 Codabar/I2OF5 统一条码子范围：ESC/POS Native 的能力快照、模板预检和 Provider 分别输出 Codabar/I2OF5 的 GS `k` 71/70；Raster 通过 ZXing 主机光栅化；独立命令校验、Codabar A–D 守卫字符、I2OF5 基础 ITF 偶数长度和模板奇数补零均已覆盖。精准 15/15，R18 Raster 专项与完整双运行时回归通过。
- [x] R5 ZPL Code39 统一条码子范围：`ZplPrintCommand.Barcode39` 输出 `^B3` 的 N/R/I/B 四方向，统一 Provider 与能力快照支持 Code39；基础字符集、非法输入、模块/高度边界、失败恢复、设备 Profile 禁用和模板元素路径均已覆盖。精准 24/24，Core/Templates 双运行时 1685/1685，Host 109/109；ZPL EAN/UPC/Codabar/I2OF5 等类型保留为后续 TODO。
- [x] R6 ZPL Code93 统一条码子范围：`ZplPrintCommand.Barcode93` 输出 `^BAo,h,f,g,e` 的 N/R/I/B 四方向，统一 Provider 与能力快照支持 Code93；可读文字和校验位显示、可打印 ASCII、模块/高度边界、失败恢复、设备 Profile 禁用、模板四角度和跨协议边界均已覆盖。精准 32/32，Core/Templates 双运行时 1717/1717，Host 109/109；ZPL EAN/UPC/Codabar/I2OF5 等类型保留为后续 TODO。
- [x] R7 ZPL EAN/UPC 统一零售条码子范围：`ZplPrintCommand.BarcodeEan8`/`BarcodeEan13`/`BarcodeUpcA`/`BarcodeUpcE` 输出 `^B8`/`^BE`/`^BU`/`^B9` 的 N/R/I/B 四方向；统一 Provider 与能力快照、模板预检、零售数据/校验位校验、UPC-E 压缩数据展开、失败恢复、设备 Profile 禁用和 Raster/TSPL 边界均已覆盖。专项 47/47，Core/Templates 双运行时 1764/1764，DesignerHost 109/109，解决方案五 TFM 串行构建 0 错误；ZPL Codabar/I2OF5 及 I2OF5C/I2OF5G 仍保留为后续 TODO。
- [x] R8 ZPL Codabar/I2OF5 统一条码子范围：`ZplPrintCommand.BarcodeCodabar`/`BarcodeI2OF5`（含 `BarcodeI2of5` 别名和旋转重载）输出 `^BK`/`^B2` 的 N/R/I/B 四方向；统一 Provider 与能力快照、模板预检、Codabar A–D 起止字符拆分、I2OF5 偶数数字和可选 Mod10 标志、尺寸度量、失败恢复、设备 Profile 禁用和 Raster/TSPL 边界均已覆盖。专项 43/43，Core/Templates 双运行时 1807/1807，DesignerHost 109/109，解决方案五 TFM 串行构建 0 错误；I2OF5C/I2OF5G 与其他 ESC/POS 条码仍保留为后续 TODO。
- [x] R9 ESC/POS Native 文字下划线子范围：`EscPosPrintCommand.Underline` 输出 `ESC - n` 的启用/关闭字节；Native `PrintCapabilities` 和模板能力快照暴露下划线，Provider 在每段 24 点顺序文字前后显式切换状态，模板 `FontStyle=4` 在预检阶段通过并保持 Raster、TSPL、ZPL 的既有边界。精准回归 net6.0 与 netcoreapp3.1 各 73/73，Core/Templates 双运行时 1811/1811，DesignerHost 109/109，Core/Templates 五 TFM L0 构建 0 错误；旋转下划线和 TSPL/ZPL 内置字体样式仍保留为后续 TODO。
- [x] R10 ESC/POS Native 文字反白子范围：`EscPosPrintCommand.Reverse` 输出 `GS B n` 的启用/关闭字节；Native `PrintCapabilities` 和模板能力快照暴露反白，Provider 在每段文字前后显式切换状态，模板 `BpFontElement.Reverse=true` 通过预检并写入统一模型，Designer 增加能力受控开关；Raster、TSPL、ZPL 继续在预检阶段严格拒绝。精准回归 net6.0 与 netcoreapp3.1 各 74/74，Core/Templates 双运行时 1817/1817，DesignerHost 109/109，前端精准 46/46、vue-tsc/Vite 构建通过，Core/Templates 五 TFM L0 构建 0 错误；旋转下划线组合和 TSPL/ZPL 内置字体样式仍保留为后续 TODO。

- [x] R11 ESC/POS Native I2OF5C/I2OF5G 子范围：统一能力集合接入两种 CPCL 条码语义；I2OF5C 按 3/1 Mod10 规则补前导零和校验位，I2OF5G 按 4/9 Mod10 规则处理 11/13 位 payload 或验证 12/14 位完整输入，最终复用 ESC/POS ITF `GS k 70`；模板 Native 输出、Raster/TSPL/ZPL 严格拒绝、错误校验位、长度、能力路径和精确字节均已覆盖。专项回归 net6.0 与 netcoreapp3.1 各 16/16，相关协议/模板回归各 216/216，Core/Templates 完整各 1833/1833，DesignerHost 109/109；旋转下划线组合、其他 ESC/POS 条码和完整设备矩阵仍保留为后续 TODO。

- [x] R12 ZPL Native MSI 子范围：`ZplPrintCommand.BarcodeMsi` 输出 `^BM`，覆盖四方向、A-D 校验方案、可读文字标志、严格输入校验、失败恢复、统一 Provider、能力矩阵、模板路径预检及跨协议拒绝；专项 net6.0/netcoreapp3.1 各 35/35，完整 Core/Templates 各 1868/1868，DesignerHost 109/109。直接命令的校验方案和最终扫描结果仍需按 Zebra 固件手册与实机确认。
- [x] R13 TSPL Native MSI 子范围：TSPL `BARCODE` 类型映射为 `MSI`，统一能力矩阵、1–13 位数字校验、Provider 页面封装、四角度锚点、模板布局轨迹、失败恢复和 ESC/POS/ZPL 边界均已覆盖；专项 net6.0/netcoreapp3.1 各 21/21，Core/Templates 完整各 1888/1888，Core/Templates 五 TFM L0 构建 0 错误。`MSIC` 校验变体仍保留为显式原始指令扩展，最终扫描结果需按 TSC 固件手册与实机确认。
- [x] R14 ZPL Native POSTNET 子范围：`ZplPrintCommand.BarcodePostnet` 输出 `^BZ` Postnet 形式，覆盖 N/R/I/B 四方向、5/9/11 位数字、连字符归一化、输入/尺寸边界、失败恢复、统一 Provider、能力矩阵、模板路径预检、Profile 禁用及 TSPL/ESC/POS/CPCL 边界。专项 net6.0/netcoreapp3.1 各 35/35，Core/Templates 完整各 1923/1923，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 错误。
- [x] R16 ZPL Native Intelligent Mail 子范围：`ZplPrintCommand.BarcodeIntelligentMail` 输出 `^BZ` 类型 3，覆盖 N/R/I/B 四方向、20/25/29/31 位数字、连字符归一化、输入/尺寸边界、失败恢复、统一 Provider、能力矩阵、模板路径预检、Profile 禁用及 TSPL/ESC/POS/CPCL 边界。专项 net6.0/netcoreapp3.1 各 52/52；官方语义依据 Zebra `^BZ` 类型 3 和 USPS 65 根四状态条规范，实际扫描结果仍需目标设备验收。
- [x] R18 ESC/POS Raster 多条码子范围：主机 Code39/Code93/EAN8/EAN13/UPC-A/UPC-E/Codabar/基础 I2OF5 编码、类型化宽度测量、Provider/模板能力集合、旋转/HRI 路径及严格拒绝边界已接入；专项 net6.0/netcoreapp3.1 各 15/15，相关回归各 176/176，Core 完整各 2043/2043，DesignerHost 109/109，Core/Templates 五 TFM 构建通过，候选包与双运行时消费者 PASS。
- [x] R19 TSPL Code93 子范围：`TsplPrintCommand` 输出 `BARCODE ...,"93",...`，Provider/模板能力集合支持 Code93 四角度与 HRI，覆盖可打印 ASCII 校验、失败恢复、Profile 禁用和 ESC/POS/ZPL 边界；专项 net6.0/netcoreapp3.1 各 22/22，Core 完整各 2069/2069，DesignerHost 109/109，Core/Templates 五 TFM 构建 0 警告/错误，候选包与双运行时消费者 PASS。协议语义依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)，实际打印/扫描仍需目标设备验收。
- [x] R20 TSPL I2OF5/I2OF5C 子范围：`TsplPrintCommand` 输出 `BARCODE "25"`/`"25C"`，Provider/模板能力集合支持两类数字条码、奇偶长度前导零归一化、四角度、Profile 禁用、失败恢复和跨协议边界；`I2OF5G` 继续严格拒绝。专项 net6.0/netcoreapp3.1 各 34/34，联合回归各 126/126；协议规则依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)，实际打印/扫描仍需目标设备验收。
- [x] R21 ZPL I2OF5C 子范围：`ZplPrintCommand.BarcodeI2OF5C` 输出 `^B2` 的 `e=Y`，Provider/模板能力集合支持偶数输入补前导 `0`、奇数输入保持、N/R/I/B 四方向、Profile 禁用、失败恢复、元素路径预检和 TSPL/ESC/POS Raster 边界；Zebra 官方 `^B2` 语义依据 [ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)，实际打印/扫描仍需目标 Zebra 设备验收。
- [x] R87 当前源码运行时/默认包消费者：五个目标运行时各 `2705/2705`，当前源码生成的 Core/Templates 包在 `net7.0`、`net6.0`、`net5.0`、`netcoreapp3.1`、`netcoreapp2.2` 默认资产消费均 PASS；历史候选包失配已与当前实现区分。
- [x] R87 显式包资产：`net5.0 + netstandard2.1` 的 Core/Templates 输出 DLL 均 `OutputMatched=true`，验证工程的重复 CopyLocal 处理和传递 DLL 补齐已关闭该子范围。

### Open Actionable

- [ ] R3 条码可读文字（HRI）：CPCL 显式 BT、ESC/POS Native/Raster 输出、Raster 全局字号（1–255，默认 12）、Host/Designer 契约与四协议端到端矩阵已接入；完整目标运行时矩阵已在 R87 关闭，仍待更多 HRI 静区/旋转组合覆盖和实机字高校准。
- [ ] R4/R5 收口：TSPL/ZPL 二维码、ESC/POS Raster 文字、Code 128 条码和二维码四角度，以及 CPCL 统一模型直连条码角度边界已完成；仍需剩余锚点/占用尺寸/换行分页组合、完整设计器端到端覆盖和外部设备方向验证。Native 不可表达时必须显式转 Raster 或严格报错。
- [ ] R5 更多条码类型：ESC/POS Code39、Code93、EAN/UPC、Codabar/I2OF5/I2OF5C/I2OF5G/UCC-EAN-128、TSPL Code93/I2OF5/I2OF5C/I2OF5G/MSI/PLANET 及 ZPL Code39/Code93/EAN/UPC/Codabar/I2OF5/MSI/POSTNET/PLANET/Intelligent Mail 子范围已完成；仍需评估其他 ESC/POS 类型及剩余 ZPL/TSPL 类型，并补齐统一语义、各协议映射、编码校验、尺寸度量、扫码静区和 golden-byte 覆盖，不能只扩展枚举而缺少 Provider 实现。
- [ ] R6 文字样式：ESC/POS Native 粗体、单线下划线和反白，以及 ESC/POS Raster 旋转下划线组合已完成；仍需 TSPL/ZPL 内置字体样式及相应模板/设计器能力，无法原生表达时保持显式 Raster 配置，禁止静默降级。

### Blocked Approval

- [ ] 正式性能、容量和超时阈值：需要项目负责人批准工作负载、预算、超时和发布回归标准；当前测试只证明已声明边界，不替代阈值审批。

### Blocked External

- [ ] 真实热敏/针式机型与固件验收：按 [设备验收清单](protocols.md#实机验收) 记录型号、代码页、DPI、纸宽、打印/扫描、进纸和裁切结果。
- [ ] 条件项：针式设备若实际使用 ESC/P、ESC/P2 或厂商私有协议，须取得手册/抓包样本后另建 Provider；不能从 ESC/POS Profile 推断兼容。

### Not Applicable

- NOT_APPLICABLE：连接管理、端口、驱动安装和设备发现不属于本组件的字节生成范围；由发送方或设备集成层负责。

### Accepted Limitations

R20 TSPL I2OF5/I2OF5C 使用原生 `BARCODE "25"`/`"25C"`；统一输入只接受数字，25 类型归一化为偶数位，25C 类型归一化为奇数位并由设备生成校验位。`I2OF5G` 及其他未映射 TSPL 类型不纳入统一能力矩阵，必须通过原始指令显式使用并按目标 TSC 手册确认。

R21 ZPL I2OF5C 使用原生 `^B2` 的 `e=Y`；Provider 在设备计算 Mod10 前将偶数输入补前导 `0`，奇数输入保持，旧 `BarcodeI2OF5` 的偶数输入和字节行为不变。设备对校验位配对和最终可读文字的处理仍需按目标 Zebra 固件手册与实机扫描确认。

R15 ZPL PLANET 已使用 `^B5` 接入统一能力矩阵；R14 ZPL POSTNET 使用 `^BZ`，R16 Intelligent Mail/IMb 使用 `^BZ` 类型 3，三者均支持四方向并在校验失败时严格拒绝。IMb 接受 20/25/29/31 位数字，不追加校验位，20 位跟踪码和最多 11 位路由码由调用方按 USPS 业务规则提供。R17 ESC/POS Native UCC/EAN-128 使用 `GS k 74`，独立入口接受 2–255 字节 7-bit ASCII，统一模板保留数字应用标识开头语义；AI、FNC1、校验和固件数据结构由调用方按 Epson 手册负责。R13 TSPL MSI 统一语义为 `BARCODE "MSI"` 的 1–13 位数字且不追加校验位；`MSIC` 等固件扩展不纳入统一能力矩阵，调用方必须显式使用原始 TSPL 指令并按目标 TSC 手册确认。TSPL 其他未映射制式、ESC/POS Raster 的 Native-only 条码和不支持的样式仍严格拒绝。

当前统一 ESC/POS、TSPL 与 ZPL 的模板条码集合仍有限；ESC/POS Native 已增加 Code39、Code93、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、I2OF5C、I2OF5G，TSPL 已支持 Code128、Code39、Code93、EAN8、EAN13、UPC-A、UPC-E、Codabar、MSI，ZPL 已增加 Code39、Code93、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、MSI、POSTNET、PLANET、Intelligent Mail；Raster 已支持主机 Code39/Code93/EAN/UPC/Codabar/I2OF5，仍严格拒绝 GS1-128、I2OF5C/I2OF5G 和邮政码等缺少可靠主机编码器的类型。I2OF5C/I2OF5G 在 Provider 内分别按 3/1、4/9 规则规范化后复用 `GS k 70`，不代表所有固件对 I2OF5G 的处理一致。ZPL MSI 统一使用 `^BMA` 无校验位，直接 `BarcodeMsi` 可选择 A-D，方案和扫描结果仍需按固件手册确认。ZPL UPC-E 仅接受号码系统 0 的统一 6/7 位压缩数据，并在 Provider 内展开为 `^B9` 所需的 10 位字段；ZPL Codabar 要求 A–D 起止字符并拆分为 `^BK` 参数，ZPL I2OF5 要求偶数位数字并通过 `^B2` 的显式参数控制 Mod10。ZPL POSTNET、PLANET 和 Intelligent Mail 分别使用 `^BZ`、`^B5` 和 `^BZ` 类型 3；IMb 严格接受 20/25/29/31 位数字，不追加校验位。ESC/POS Native 现在支持 24 点顺序文字的粗体、单线下划线和反白，坐标旋转和非 24 点字号仍严格拒绝；ESC/POS Raster、TSPL/ZPL 内置字体的反白及 TSPL/ZPL 粗体/下划线也保持严格拒绝。R3–R19 已关闭的子范围保持稳定；剩余未支持的条码、旋转和样式按协议能力严格拒绝。TSPL/ZPL QR 旋转、TSPL Code93、ESC/POS Raster 文字、主机 Code39/Code93/EAN/UPC/Codabar/I2OF5 条码、Code 128 和二维码旋转已支持四角度，Raster 文字/码图静区按旋转后占位计算；ESC/POS Native 条码和二维码旋转仍严格拒绝，CPCL 显式静区与旋转组合按能力校验。Native 只接受能表达的顺序文字/码图和校准制表布局，继续拒绝图形和填充。
R20 更新：TSPL 统一模板已增加 I2OF5/I2OF5C，分别输出 `BARCODE "25"`/`"25C"`；输入只接受数字，25/25C 分别归一化为偶数/奇数位，25C 校验位由设备生成，`I2OF5G` 仍按能力矩阵严格拒绝。

第三方同步操作只能在调用边界检查取消，内部对象与缓冲未完全计入预算；当前计数不等于实际进程堆上限。固定字体不混合逐字替代、不合成粗体，不证明复杂塑形/双向布局；静区和声明占位不证明固件实际尺寸或可扫描性。发送中断和 raw 绕过后的设备状态由发送方管理。

### Verified Boundaries

- [x] R1 旋转图片的临时缓冲预算、取消恢复、源 padding 清除、Native 图片拒绝及静态资源预检已由独立测试覆盖；这些证据不宣称完整进程堆上限或实机能力。
- [x] R2 填充的正尺寸/溢出/页边界、ZPL 原生上限、图形能力与图片能力独立、静区占位、取消/预算恢复和 Native 拒绝已覆盖。
- [x] T8 五运行时、Host、HTTP、包消费者和固定字体 UI 门禁仍作为历史固定基线保存，未被文档更新覆盖。

### Deferred

- [ ] Profile 细化：按机型补充可用条码、HRI、文字样式、二维码方向等组合能力；依赖 R2–R10 的语义矩阵。
- [ ] GS1/FNC1 业务语义：确定输入表达、校验、编码和扫描器返回值，不能只验证打印字节。
- [ ] 复杂文字与字体范围：双向文字、塑形、组合字符、TTC、缺字替代和真实粗体策略。
- [ ] 日文及厂商代码页：补充设备字符集、代码页、设备字体和固件差异样本。
- [ ] 跨平台 CI 与默认系统字库：补充 Linux/macOS 运行时、字体和输出差异验证。
- [ ] 发送方恢复规范：明确分段顺序、中断、重试重复打印和设备状态恢复；不扩大本组件的连接管理范围。

R12 记录修正：ZPL Native 已接入 MSI `^BM`；统一模型使用 A 方案无校验位，直接命令可选择 A-D，四方向、严格输入校验、Provider/模板预检和能力矩阵均已覆盖。Raster、TSPL、ESC/POS Native/Raster 继续严格拒绝 MSI；R12 专项双运行时各 35/35，完整测试各 1868/1868，DesignerHost 109/109。后续继续处理其他 ESC/POS 条码、旋转下划线组合、Designer E2E、完整运行时矩阵和实机验收。

R11 记录修正：ESC/POS Native 已接入 I2OF5C/I2OF5G；I2OF5C 使用 3/1 Mod10，I2OF5G 使用 4/9 Mod10，最终输出基础 ITF `GS k 70`。Raster 和 TSPL 继续严格拒绝这两种统一条码类型；ZPL 在 R21 已接入 I2OF5C，I2OF5G 仍严格拒绝；R11 专项双运行时各 16/16，完整测试各 1833/1833。后续继续处理其他 ESC/POS 条码、旋转下划线组合、Designer E2E、完整运行时矩阵和实机验收。

R10 记录修正：ESC/POS Native 反白已完成，使用 `GS B n` 进行每段文字前后的状态切换；Raster、TSPL、ZPL 反白仍按能力预检严格拒绝。R10 精准回归为 74/74，Core/Templates 双运行时完整测试为 1817/1817，后续继续处理旋转下划线组合、Designer E2E、I2OF5C/I2OF5G、其他 ESC/POS 条码、完整运行时矩阵和实机验收。

历史综合状态段保留各阶段当时的计数；R14 当前状态见下段，避免用旧轮次的总数覆盖最新证据。

R14 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 ZPL POSTNET 子范围，不代表整体闭环，不将状态换算为功能百分比。Test Status：Core/Templates net6 与 netcoreapp3.1 完整各 1923/1923，ZPL POSTNET 专项各 35/35，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 错误，保留两个运行时测试的 NU1900 漏洞源不可访问警告。External Gate Status：BLOCKED_EXTERNAL。Next Action：继续剩余旋转锚点/分页组合和 Designer E2E，随后评估其他 ESC/POS 原生条码、剩余 ZPL/TSPL 类型与旋转下划线组合；Native 不可表达时保持显式 Raster 或严格报错。整个目标保持 active，不将本地通过等同实机验收或发布。

R15 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 ZPL PLANET 子范围，不代表整体闭环，不将状态换算为功能百分比。Test Status：PLANET 专项 net6.0/netcoreapp3.1 各 41/41，Core 完整各 1964/1964，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 错误。候选包 `artifacts/r15-planet-pack` 打包通过，net6.0/netcoreapp3.1 消费者在 `artifacts/r15-planet-consumers` PASS；保留 NU1900、NU5104、缺少 README 和 EOL TFM 警告。External Gate Status：BLOCKED_EXTERNAL。Next Action：继续剩余旋转锚点/分页组合、Designer E2E、其他 ESC/POS 条码和 Intelligent Mail/IMB 等剩余 ZPL/TSPL 类型；Native 不可表达时保持显式 Raster 或严格报错。整个目标保持 active，不将本地通过等同实机验收或发布。

R16 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 ZPL Intelligent Mail（IMb）子范围，不代表整体闭环，不将状态换算为功能百分比。Test Status：IMb 专项 net6.0/netcoreapp3.1 各 52/52，Core 完整各 2016/2016，DesignerHost 109/109，前端 238/238 与 vue-tsc/Vite 构建通过；Core/Templates L0 生产构建五 TFM 0 错误；候选包 `artifacts/r16-imb-pack` 打包通过，net6.0/netcoreapp3.1 消费者 `artifacts/r16-imb-consumers` 均 PASS，保留 NU1900、NU5104、缺少 README、EOL TFM 和前端 chunk 警告。External Gate Status：BLOCKED_EXTERNAL。Next Action：实现 ESC/POS Native UCC/EAN-128（GS `k` 74）已完成，继续剩余旋转锚点/分页组合与 Designer E2E；IMb 实际扫描结果仍需目标 Zebra 设备验收。整个目标保持 active，不将本地通过等同实机验收或发布。

R17 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 ESC/POS Native UCC/EAN-128 子范围，不代表整体闭环，不将状态换算为功能百分比。Test Status：UCC/EAN-128 专项 net6.0/netcoreapp3.1 各 13/13，相关 ESC/POS/模板基线双运行时各 109/109，Core 完整各 2028/2028，DesignerHost 109/109；Core/Templates 五 TFM L0 生产构建 0 错误；候选包 `artifacts/r17-ucc-pack` 打包通过，net6.0/netcoreapp3.1 消费者 `artifacts/r17-ucc-consumers` 均 PASS，保留 NU1900、NU5104、缺少 README 和 EOL TFM 警告。External Gate Status：BLOCKED_EXTERNAL。Next Action：继续剩余旋转锚点/分页组合、Designer E2E 和其他 ESC/POS/ZPL/TSPL 条码；GS1-128 实际扫描与固件差异仍需目标 Epson 设备验收。整个目标保持 active，不将本地通过等同实机验收或发布。
R18 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 ESC/POS Raster 主机 Code39/Code93/EAN/UPC/Codabar/I2OF5 子范围，不代表整体闭环，不将状态换算为功能百分比。Test Status：Raster 条码专项 net6.0/netcoreapp3.1 各 15/15，相关 ESC/POS/模板回归双运行时各 176/176，Core 完整各 2043/2043，DesignerHost 109/109；Core/Templates 五 TFM L0 生产构建 0 错误；候选包 `artifacts/r18-raster-barcode-pack` 打包通过，net6.0/netcoreapp3.1 消费者 `artifacts/r18-raster-barcode-consumers` 均 PASS。保留 NU1900、NU5104、缺少 README 和 EOL TFM 警告。External Gate Status：BLOCKED_EXTERNAL。Next Action：继续剩余旋转锚点/分页组合、Designer E2E、旋转下划线组合和其他未映射 ESC/POS/ZPL/TSPL 条码；Raster 其余没有可靠主机编码器的类型继续严格拒绝，实际扫描仍需目标设备验收。整个目标保持 active，不将本地通过等同实机验收或发布。
R20 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 TSPL I2OF5/I2OF5C 子范围，不代表整体闭环，不将状态换算为功能百分比。Test Status：TSPL I2OF5/I2OF5C 专项 net6.0/netcoreapp3.1 各 34/34，联合回归各 126/126，Core 完整各 2108/2108，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 警告/错误；候选包 `artifacts/r20-tspl-i2of5-pack` 打包通过，net6.0/netcoreapp3.1 消费者 `artifacts/r20-tspl-i2of5-consumers` 均 PASS。保留 NU1900、NU5104、缺少 README 和消费者 EOL 框架警告。External Gate Status：BLOCKED_EXTERNAL。Next Action：继续剩余旋转锚点/分页组合、Designer E2E、旋转下划线组合和其他未映射 ESC/POS/ZPL/TSPL 条码；保持未表达能力严格报错。整个目标保持 active，不将本地通过等同实机验收。
R21 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 ZPL I2OF5C 子范围，不代表整体闭环，不将状态换算为功能百分比。Test Status：ZPL I2OF5C 专项、受影响 Core/Templates 双运行时、L0 构建、DesignerHost 和候选包消费者验证将在本轮完成后记录；保留 NU1900、NU5104、缺少 README 和消费者 EOL 框架警告。External Gate Status：BLOCKED_EXTERNAL。Next Action：继续剩余旋转锚点/分页组合、Designer E2E、旋转下划线组合和其他未映射 ESC/POS/ZPL/TSPL 条码；保持未表达能力严格报错。整个目标保持 active，不将本地通过等同实机验收。
R21 验证补充：ZPL I2OF5C 专项及受影响 Core/Templates 双运行时各 208/208，Core 完整双运行时各 2133/2133，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 警告/错误；候选包 `artifacts/r21-zpl-i2of5c-pack` 打包通过，隔离消费者 `artifacts/r21-zpl-i2of5c-consumers-net6fresh`（net6.0）与 `artifacts/r21-zpl-i2of5c-consumers-netcorefresh`（netcoreapp3.1）均 PASS。
R22 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 ESC/POS Raster `I2OF5C`/`I2OF5G` 子范围，不代表整体闭环。Test Status：Raster I2OF5C/I2OF5G 专项及受影响 Core/Templates 双运行时各 71/71，Core 完整双运行时各 2140/2140，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 警告/错误；候选包 `artifacts/r22-escpos-raster-i2of5-pack` 打包通过，隔离消费者 `artifacts/r22-escpos-raster-i2of5-consumers-net6`（net6.0）与 `artifacts/r22-escpos-raster-i2of5-consumers-netcore`（netcoreapp3.1）均 PASS。保留 NU1900、NU5104、缺少 README 和消费者 EOL 框架警告。External Gate Status：BLOCKED_EXTERNAL。Next Action：继续 R3/R4/R5/R6 剩余旋转、HRI、Designer E2E、其他条码和样式；保持未表达能力严格报错。Goal active。

R22 能力修正：ESC/POS Raster 现支持 `I2OF5C`/`I2OF5G`。主机按 3/1、4/9 Mod10 规则补齐或验证校验位，HRI、宽度预检和旋转占位均使用归一化 ITF 数据；GS1-128、MSI、邮政码等没有可靠主机编码器的类型仍严格拒绝。前文 R18 的旧拒绝描述由本修正覆盖。

R23 当前轮次：完成 ESC/POS Raster MSI 子范围。`EscPosRasterCanvas` 使用 ZXing `BarcodeFormat.MSI`/`MSIWriter` 生成光栅数据，统一能力矩阵和模板 Provider 已开放 MSI；输入严格限制为 1-13 位数字且不追加校验位，宽度、HRI、旋转、页边界和失败恢复沿既有 Raster 路径执行。ESC/POS Native MSI 与没有可靠主机编码器的 GS1/邮政制式继续严格拒绝。新增 Raster MSI 输入/预算恢复、TSPL/ZPL/ESC/POS 能力矩阵与模板路径覆盖；新增旋转下划线模板测试覆盖 Raster 四角度墨迹/占位以及 Native、CPCL、TSPL、ZPL 严格拒绝。

R23 验证：MSI/相关能力精确测试 `net6.0` 与 `netcoreapp3.1` 各 81/81；旋转下划线精确测试各 12/12；Core/Templates 全量测试各 2155/2155；DesignerHost 109/109；Core 与 Templates 五目标框架 L0 构建均 0 警告、0 错误；候选包位于 `artifacts/r23-escpos-raster-msi-pack`，隔离 `net6.0` 与 `netcoreapp3.1` 包消费者 `artifacts/r23-escpos-raster-msi-consumers-net6fresh`、`artifacts/r23-escpos-raster-msi-consumers-netcorefresh` 均 PASS。保留 NU1900、NU5104、缺少 README 与消费者 EOL 框架提示。未修改版本号、提交 Git 或发布 NuGet；真实热敏/针式打印机打印与扫描仍是外部验收项。

R24 当前轮次：完成 ESC/POS Raster POSTNET 主机编码。`EscPosRasterCanvas` 支持 5/9/11 位数字和连字符归一化，主机追加校验位并生成全高/半高条码；统一 Provider、模板能力、宽度测量、四角旋转、HRI、页边界和失败恢复保持一致，Native ESC/POS、TSPL、ZPL 继续保留各自能力边界。

R24 验证：POSTNET 及相关能力专项 `net6.0` 与 `netcoreapp3.1` 各 76/76；Core/Templates 全量各 2170/2170；DesignerHost 109/109；Core 与 Templates 五目标框架 L0 构建均 0 警告、0 错误。候选包 `artifacts/r24-escpos-raster-postnet-pack` 打包通过，保留 `NU5104`、缺少 README 和消费者 EOL 框架警告；隔离 `net6.0` 消费者 `artifacts/r24-escpos-raster-postnet-consumers-net6fresh` PASS，`netcoreapp3.1` 消费者在当前 SDK 旧 TFM CopyLocal 阶段因 `MSB3030` 报传递 DLL 缺失而未通过，文件在隔离缓存中实际存在，标记为 BLOCKED_EXTERNAL。另保留 `NU1900` 漏洞源不可访问警告；未修改版本号、提交 Git 或发布 NuGet。真实热敏/针式打印机的条高、DPI、纸宽、打印与扫描仍是外部验收项。

R25 验证补充：ESC/POS Raster PLANET 已接入 `EscPosRasterCanvas`、统一能力快照、Provider 和模板渲染。PLANET 接受 12/14 位数字，去除分组连字符后验证数字和为 10 的倍数，使用 POSTNET 反相模式绘制 3 个全高与 2 个半高数据条，并保留全高起止框条。精准 PLANET/POSTNET/ZPL 回归在 net6.0 与 netcoreapp3.1 各 75/75；Core/Templates 完整测试 net6.0、netcoreapp3.1、netcoreapp2.2 各 2188/2188；DesignerHost 109/109；Core 与 Templates 五目标框架构建 0 警告、0 错误，`git diff --check` 通过。未改变公共方法签名、TFM、版本或包图，因此未重复执行 R24 已有包消费者；net7.0/net5.0 测试因本机缺少对应运行时无法启动，归类 `BLOCKED_EXTERNAL`。实际热敏/针式设备的条高、DPI、纸宽、打印与扫描仍需外部验收。

R26 验证补充：ESC/POS Raster FIM A/B/C 已接入 `EscPosRasterCanvas`、统一能力快照、Provider 和模板渲染。三种模式分别使用 USPS 九位二进制模式，逐像素验证条/空槽位，宽度、四角旋转、HRI、模板静区和预算恢复均覆盖。FIM 及相关 Raster 条码精准回归 net6.0 与 netcoreapp3.1 各 135/135；Core/Templates 完整测试 net6.0、netcoreapp3.1、netcoreapp2.2 各 2206/2206；DesignerHost 109/109；Core 与 Templates 五目标框架构建 0 警告、0 错误，`git diff --check` 通过。未改变公共方法签名、TFM、版本或包图，未重复执行包消费者；真实热敏/针式设备的 FIM 条高、DPI、纸宽、打印与扫描仍需外部验收。

R27 验证补充：ESC/POS Raster GS1-128/UCC-EAN-128 已接入 `EscPosRasterCanvas`、统一能力快照、Provider 和模板渲染。ZXing `GS1_FORMAT` 负责起始 FNC1，ASCII 29 分隔符负责后续 FNC1；精准测试 net6.0 与 netcoreapp3.1 各 80/80，netcoreapp2.2 专项 28/28；受影响 Core 完整测试三种可用运行时各 2221/2221；DesignerHost 109/109；Core/Templates 五目标框架构建 0 警告、0 错误，`git diff --check` 无错误。未改变公共方法签名、TFM、版本或包图，未重复执行包消费者；GS1 AI 业务校验、条高/DPI/纸宽、打印与扫描仍需调用方和目标热敏/针式设备外部验收。
