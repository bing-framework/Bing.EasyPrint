# Vue 多协议模板设计器

本轮重新验证当前源码包在五个目标运行时的默认包消费者链路：Core/Templates 包、四协议模板输出、HRI、填充矩形、ZPL 延迟输出和图片旋转均通过；`net5.0` 显式 `netstandard2.1` 资产也确认两个输出 DLL 来自候选包。历史固定包与当前 Designer 依赖的公开 API 不同步，已单独标记为旧候选包失配。本轮不改变设计器界面，也不把字体、DPI、纸宽或真实打印扫描当作本地通过证据。

Host 依赖的模板回归现在覆盖 ESC/POS Native Sequential 的 Rotate90 条码和文字严格错误，并确认非法元素后的图片资源不会被打开。本轮只增强 Host 预检链路证据，不改变设计器界面；需要旋转输出时应选择 Raster 或标签协议。

Host 依赖的模板回归现在补充 ESC/POS Native Sequential 的跨页 HRI 参数：Code39、I2OF5、UCC/EAN-128 在固定页眉、表头、重复明细和页脚中按来源路径、实例索引、`GS H/GS h/GS w/GS k` 和预留高度检查；非零静区返回元素路径错误。本轮只增强 Host 模板链路证据，不改变设计器界面，也不代表设备 HRI 字形或真实扫描结果。

Host 依赖的模板回归现在补充 ESC/POS Native Rows 的 HRI 与模块宽度组合：Code39、Code93、Codabar、I2OF5 和 UCC/EAN-128 均检查 `GS H`、`GS w`、`GS k`、预留高度和布局宽度变化；模块宽度低于设备下限时返回元素路径错误。本轮只增强 Host 模板链路证据，不改变设计器界面，也不代表设备 HRI 字形或真实扫描结果。

Host 依赖的模板回归现在补充 ESC/POS Native Rows 的 Code39、Code93、Codabar、I2OF5、I2OF5C 和 UCC/EAN-128。测试按来源路径、布局尺寸、`GS k` 制式及 I2OF5C 校验位归一化检查输出，非法 Codabar 返回元素路径错误。本轮只增强 Host 模板链路证据，不改变设计器界面，也不代表完整 Native 参数矩阵或真实设备扫描结果。

Host 依赖的模板回归现在补充 ESC/POS Native Sequential 的 GS1 DataBar Omnidirectional、Truncated、Limited、Expanded 和 Code128 auto。跨页固定分区与重复明细按来源路径、实例索引、Function B `GS k` 75–79 和 Expanded FNC1 转义检查；MSI 不支持时返回元素路径错误。本轮只增强 Host 模板链路证据，不改变设计器界面，也不代表完整 Native 参数矩阵或真实设备扫描结果。

Host 依赖的模板回归现在补充 ESC/POS Native Rows 的 Code128/EAN13 HRI 和参数边界：显式零静区可输出，非零静区、旋转条码和旋转文字按模板路径严格报错。该回归只验证 Host 模板契约，不改变设计器界面，也不代表目标设备的 HRI 字形和扫描效果。

Host 依赖的模板回归现在包含 ESC/POS Native Sequential 的跨页条码流程：固定页眉、表头、重复明细和页脚中使用 Code128、Code39、EAN13，并按来源路径和实例索引检查多页输出；不支持的 MSI 会返回元素路径错误。该回归不改变设计器界面，也不代表 Native Rows、完整条码矩阵或真实设备固件。

后端模板回归现在补充了 Version 2 固定页眉、表头、重复明细和页脚中的 Code39、Code93、Codabar、I2OF5C 跨页组合，并按来源路径和实例索引验证 CPCL、TSPL、ZPL、ESC/POS Raster 的输出。该测试增强 Designer 依赖的 Host 模板链路证据，但不改变设计器界面，也不代表所有条码类型、Native 全矩阵或真实设备扫描结果。

长页图片模板现在有资源压力回归：Version 2 同时使用页眉、表头、重复明细和页脚图片，重复明细使用两个不同源裁切区域并生成多页。固定分区图片只加载一次，正文图片按明细释放，有限工作集超限会严格失败且下一次渲染可恢复；该证据不代表任意规模容量或真实设备吞吐。

图片错误诊断现在保留可空的源图像素尺寸、请求裁切区域和处理后输出尺寸。源图已识别但裁切越界时，错误面板会显示源图与裁切请求；图片缺失、损坏或尚未完成识别时保持未知，不把尺寸显示为零。该信息来自模板解码/布局阶段，不能替代目标打印机 DPI、插值质量和真实打印验收。

图片属性面板现在支持完整源图与显式源像素裁切区域切换，可编辑 `X/Y/Width/Height`，模板包会保留 `Crop` 字段；画布加载资源后按自然像素配置裁切预览，服务端仍负责最终边界校验和四协议输出。损坏资源、未加载资源及真实设备插值效果不由本地画布推断。

模板图片的显式缩放现在有四协议回归：固定 4×3 PNG 可按 2×2、4×3、6×5 目标尺寸输出，并覆盖不旋转、90 度和 270 度旋转。CPCL、TSPL、ZPL 与 ESC/POS Raster 的布局轨迹均验证目标尺寸和旋转后的宽高，页面字节分别检查 `EG`、`BITMAP`、`^GFA` 与 `GS v 0`；该测试不代表任意裁切、插值质量或真实设备物理打印效果。

跨页点阵码现在有四协议 Designer 验收：Version 2 模板分别使用 DataMatrix 和 PDF417，包含固定页眉、表头、3 条重复明细和固定页脚。CPCL、TSPL、ZPL 和 ESC/POS Raster 均保留 Type 10 点阵码实例顺序和每页固定区，导出的 ZIP 页面与协议预览页数对应；该流程不代表任意裁切、缩放质量、Native 点阵码或真实设备解码结果。

跨页条码混排现在有四协议 Designer 验收：Version 2 模板包含固定页眉、表头、9 条重复明细和固定页脚，每条明细同时包含 Code 128 与二维码。CPCL、TSPL、ZPL 和 ESC/POS Raster 均保留两类条码的实例顺序和每页固定区，导出的 ZIP 页面与协议预览页数对应；该流程不代表其他条码、动态尺寸或真实设备的 DPI、打印和扫描结果。

跨页图片资源现在有四协议 Designer 验收：模板包只包含一个非对称 PNG，Version 2 重复明细在 90 度旋转图片的同时生成多页。CPCL、TSPL、ZPL 和 ESC/POS Raster 均保留每页固定区、图片 `6×12` 旋转占位和明细顺序，导出的 ZIP 页面与各协议预览页数对应；CPCL 压缩图片使用 `CG`，TSPL/ZPL/Raster 分别使用 `BITMAP`、`^GFA` 和 `GS v 0`。该流程验证资源复用和协议归档，不代表任意裁切、缩放质量或真实打印效果。

跨页分区现在有四协议 Designer 验收：Version 2 模板包含固定页眉文字与水平线、可重复表头、重复明细以及固定页脚文字与矩形。CPCL、TSPL、ZPL 和 ESC/POS Raster 均能在多页预览中保留每页的页眉、表头、页脚、线框和明细顺序，导出的 ZIP 页面也与各协议自身预览页数对应。该流程验证模板分页和归档映射，不代表动态行高、复杂资源旋转裁切、走纸、切裁或真实打印质量。

混合布局现在有四协议 Designer 验收：Version 2 模板同页包含文字、水平线、空心矩形、Code 128 条码和二维码，CPCL、TSPL、ZPL 和 ESC/POS Raster 均能通过 Host 预览并导出。预览保留五种元素类型；CPCL/TSPL 将空心矩形展开为线命令，ZPL 使用 `^GB`，Raster 使用 `GS v 0`。该流程验证单页布局和协议字节链路，不代表跨页组合、走纸、切裁或真实打印质量。

长文本现在有四协议 Designer 换行验收：Version 2 模板中的 240 个 ASCII 字符会在 CPCL、TSPL、ZPL 和 ESC/POS Raster 中按各自字体度量拆分为多页，所有预览片段可拼回完整文本，导出的 ZIP 页面编号与对应协议的预览页数一致。默认夹具观察到四种协议的页数可以不同；需要统一物理页数时，应配置目标设备字体校准。该流程不代表中文/复杂文字塑形、设备字高校准或真实打印机的物理换行效果。

重复明细模板现在有四协议分页 Designer 验收：Version 3 模板固定 13 条文字明细后，CPCL、TSPL、ZPL 和 ESC/POS Raster 都能通过 Host 预览生成相同页数，导出的 ZIP 使用连续的 `page-001.*` 页面编号，并分别保留 `! 0`、`SIZE`、`^XA` 和 `GS v 0` 协议标记。该流程验证模板分页和页面归档映射，不代表真实设备的走纸、切裁、DPI、纸宽或打印质量。

DataMatrix 和 PDF417 现在有跨协议 Designer 验收：Version 2 模板中的 Type 10 点阵码可依次导出 CPCL、TSPL、ZPL 和 ESC/POS Raster，页面文件分别保留 `EG`、`BITMAP`、`^GFA` 和 `GS v 0` 图片命令。Version 1 按模板契约不允许 Type 10；当前统一模型使用受控黑白位图，不代表目标设备的原生 Native 点阵码、DPI、纸宽、打印或扫描解码结果。

图片模板现在有跨协议 Designer 验收：标准模板包可以携带真实 PNG 资源，导入后同一图片可依次导出 CPCL、TSPL、ZPL 和 ESC/POS Raster，页面文件分别保留 `EG`、`BITMAP`、`^GFA` 和 `GS v 0` 图片命令。该流程验证资源导入与协议字节链路，不代表目标设备对像素阈值、DPI、纸宽、旋转、缩放、打印或扫描结果的实际表现。

二维码模板现在有跨协议 Designer 验收：同一模板可依次导出 CPCL、TSPL、ZPL 和 ESC/POS Raster，页面文件分别保留 `B QR`、`QRCODE`、`^BQ` 和 `GS v 0` 光栅命令；固定数据会在预览恢复后写入导出结果。该流程验证协议字节链路，不代表目标设备的纠错、DPI、纸宽、打印或扫描结果。

TSPL 的 Designer 浏览器链路已覆盖 TELEPEN、TELEPENN 和 CODE49：能力快照开放类型 22–24，变量比例编辑器可保留显式窄条/宽条模块，切换制式和数据后可恢复服务端预览，生成 ZIP 中的 `page-001.tspl` 会保留对应原生 `BARCODE` 行。默认静区会使编码区从 `20,0` 开始；真实 TSC 固件、堆叠行数、DPI、纸宽、HRI、打印和扫描仍需设备验收。

ESC/POS Native 的 Designer 浏览器链路已覆盖 GS1 DataBar Omnidirectional、Truncated、Limited、Expanded 四种 Function B 制式：能力快照开放类型 25–28，切换制式和固定文字后可恢复服务端预览，生成 ZIP 中的 `page-001.escpos` 会保留 Function B 75–78 命令头和数据字节。测试使用显式条码宽度和窄条模块来验证协议链路；真实固件、GS1 业务语义、DPI、纸宽、HRI、打印和扫描仍需设备验收。

ZPL GS1 DataBar 的 Designer 浏览器链路已覆盖：能力快照开放 Omnidirectional、Truncated、Limited、Expanded 四种 `^BR` 类型，切换制式和固定文字后可恢复服务端预览，生成 ZIP 中的 `page-001.zpl` 会保留对应 `^BR` 字节。测试使用显式条码宽度和窄条模块来验证协议链路；真实固件、GS1 业务语义、DPI、纸宽、HRI、打印和扫描仍需设备验收。

ZPL 条码能力现包含 GS1 DataBar Omnidirectional、Truncated、Limited 和 Expanded，对应 `^BR` 类型 1、2、5、6。设计器通过 Host 能力快照显示这些选项；统一模板支持四角旋转、条高和 1–10 点模块放大倍数，Expanded 的分段宽度由后端固定为 22。`^BR` 没有 HRI 参数，显式开启可读文字时预览/导出会按元素路径报错，需要可读内容时请追加文字元素；真实 Zebra 固件、GS1 业务字段、DPI、纸宽、打印和扫描仍需设备验收。

TSPL/ZPL 介质设置提供“文字样式位图”开关，对应 `AllowRasterizedTextStyles`。默认关闭时，粗体、下划线和旋转下划线按协议能力严格报错；开启后使用主机字体生成 `BITMAP`/`^GFA`，需要目标 Profile 允许图片。该开关随设计器项目和模板包保存，浏览器 E2E 已覆盖默认严格错误、开关请求、预览恢复和协议字节导出；真实设备字高/字体效果仍需验收。

ESC/POS Native 条码选择器现包含 GS1 DataBar Omnidirectional、Truncated、Limited、Expanded 和 `CODE128 auto`，对应 Epson `GS k` Function B 75–79。75–77 要求 13 位数字，Limited 首位为 0/1；Expanded 支持模板中的括号与 ASCII 29 FNC1，并在输出时按设备语义转义，官方允许的裸 `{` 按字面保留。设计器只在 ESC/POS Native 能力快照中开放这些选项，Raster、CPCL、TSPL 和 ZPL 保持严格拒绝；真实 Epson/兼容机的固件、HRI、DPI、纸宽、打印和扫描仍需设备验收。参考：[Epson GS k](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_lk.html)。

ZPL 条码能力现包含 Code 11（`^B1`）、Plessey（`^BP`）和 Code 49（`^B4`）。设计器通过 Host 能力快照显示这些选项；Code 49 数据仅接受 1–81 个可安全嵌入 ZPL 字段的 ASCII 字符，四角旋转、条高、模块宽度和页面边界由 Provider 严格预检，可读文字使用 ZPL `f=B`，起始模式默认 `A`。Code 49 的模板 `Height` 表示期望的单行点高，Provider 会向上换算为 `^B4` 所需的模块宽度倍率；页面宽度按 70 模块的本地行宽估算，堆叠行数由设备固件决定。超过两行时 HRI 可能超出条码主体宽度，其横向占位尚未校准。`^`、`~`、引号和控制字符不会被静默转义，真实 Zebra 固件、HRI、DPI、纸宽、打印和扫描仍需设备验收。

ZPL 条码能力现包含 Code 11（`^B1`）和 Plessey（`^BP`）。设计器通过 Host 能力快照显示这两个选项；Code 11 数据仅接受非空数字和连字符，Plessey 数据仅接受非空数字，四角旋转、条高、模块宽度和页面边界由 Provider 严格预检。Plessey 使用 `^BY` 默认 3:1 比例，`e=N` 不重复请求设备打印校验位；真实 Zebra 固件、HRI、DPI、纸宽、打印和扫描仍需设备验收。ZPL 其他未映射类型不会在设计器中伪造为可用能力。

TSPL 条码选择器现包含 `CODE49`（24）。Code 49 使用 1–81 个可安全嵌入字段的 ASCII 字符；选择 Code 49、TELEPEN 或 TELEPENN 时，设计器切换为 TSPL 专用 `1:1`、`1:2`、`1:3`、`2:5`、`3:7` 窄宽比编辑器，并按当前能力范围自动写入合法整数 `NarrowModuleWidth`/`WideModuleWidth`。超出模块宽度范围的比例会禁用，手动改出不合法组合会保留原值并在预检前提示。旧 `Ratio` 字段仍保留为兼容回退。堆叠行数和最终模块布局由目标固件决定。TSC 手册标注 Code 49 自 V6.89EZ 起可用，实际固件、HRI、条高、DPI、纸宽和扫描结果仍需设备验收。

TSPL 条码选择器现包含 `TELEPEN`（22）和 `TELEPENN`（23）。两种制式分别校验 1–30 个安全嵌入字段的 ASCII 字符和 1–60 位 ASCII 数字；专用比例选择器会把比例转换为能力范围内的整数模块宽度。TSPL 手册标注两类条码自 V6.89EZ 起可用，实际固件、HRI、条高、DPI、纸宽和扫描结果仍需设备验收。

“任务级输出”面板可设置完整文档份数、Collated/Uncollated 顺序、进纸与切纸执行范围，以及明确的进纸单位和数量。启用时清理并隐藏旧页面收尾字段；设置随项目与模板包保存。请求通过可选 `job` 对象传入（`copies`、`copyOrder`、`feedScope`、`feedUnit`、`feedAmount`、`cutScope`），能力响应的 `jobFeedUnits` 描述当前可用单位。正数进纸配合 None 范围会在项目保存/导入及后端拒绝。

ESC/POS 显式裁切通过根 `cutOptions`（旧页面模式）或 `job.cutOptions`（任务模式）配置：`mode` 为 `Full`/`Partial`，`feedToCutPosition` 控制设备先送纸到切刀位置，`feedBeforeCutAmount` 为额外补偿量，`feedBeforeCutUnit` 默认为 `MotionUnits`，也可按显式机型换算 `Dots`。正补偿必须启用送纸到切刀位置；换算后的补偿范围是 0–255，零也能显式送纸到切刀位置。页选项不能与任务选项或关闭页面切纸组合，任务选项不能配 `cutScope: None`；未知裁切字段严格拒绝。未提供这些字段时保留旧输出。

ESC/POS Profile 的可选 `supportedCutModes` 声明已确认的切法（空数组禁切刀，省略沿用原入口）；`supportsFeedAndCut` 默认 false，确认设备支持 GS V Function B 后才可启用。能力响应的 `cutModes`、`cutFeedUnits` 和 `cutFeedMotionUnits` 用于限制显式裁切入口。TSPL/ZPL 可配置下文的切刀契约；没有 ZPL Cutter 声明时，其旧 `^CN` 字节不证明实际固件支持全切或半切。完整语义和官方参考见 [显式裁切契约](protocols.md#显式裁切形状与切刀位置补偿)。

“输出校准设置”中的页面收尾与任务面板分别提供“自定义裁切参数”：可选全切/半切、进纸到切刀位置、补偿量和单位。已有但不支持的配置保留显示，允许修正或清除；已启用但机型不支持的送纸选项仍可取消。非法数值保留为编辑值，保存和输出严格拒绝，避免界面显示新值却输出旧值。页面和任务设置随项目、模板包及撤销重做保存。

使用新增裁切参数或 Profile 裁切字段时，设计器及其预览/导出 API 会先按本节的 Host 契约协商，再检查对应的裁切能力字段；缺少契约头或能力字段时提示更新 Host。契约头负责版本与特性协商，裁切字段负责具体设备能力校验。旧 Profile 没有 `supportsFeedAndCut` 时保持字段省略，显式 false 则保留；旧调用可以省略请求协商头并继续调用同一 API，新版 UI 仍要求成功响应返回匹配的契约版本和特性。能力刷新期间会取消旧预览，取得当前能力后重新预览，旧响应不能覆盖新配置或输入错误。

任务模式预览按实际输出顺序显示逻辑页和份数归属；每页响应附带 `sourcePageIndex`、`copyIndex`、`feedAfterPrint`、`cutAfterPrint`。导出 ZIP 额外包含 `job.json`，其页面列表与有序 `page-001.*` 文件一一对应。未提供 job 的旧输出格式继续保持。多页多份 Uncollated 的 EachCopy 范围由后端在实际分页确定时拒绝。见 [协议任务策略](protocols.md#显式任务级份数与收尾)。

ESC/POS Native 可选择旧文字行或显式 Sequential 混合收据。请求字段为 `escPosDevice.nativeFlow`，取值 `Rows`/`Sequential`，缺省 Rows；能力响应的 `nativeFlow` 返回当前 Native 模式，其他布局为 null。混合模式用于文字、明细、原生码图及页尾的顺序校验；实际进纸由设备决定，不能把预览坐标视作精确物理位置。选择 Raster 时须清除 Sequential。模式随项目及模板包保存，裸模板 JSON 仍不包含输出设置。

仓库中的 Vue 设计器用于编辑 Bing.EasyPrint V1/V2/V3 模板、编辑业务样例数据、查看分页布局，并导出 CPCL、ESC/POS、TSPL 或 ZPL 页数据。它不连接打印机，也不保存到 ERP 数据库。

## Host 契约协商与错误响应

设计器 Host 的 HTTP 契约版本独立于 NuGet、模板 JSON 和模板包版本。请求方可以发送 `X-EasyPrint-Contract-Version: 1`，并以逗号分隔的 `X-EasyPrint-Required-Features` 声明必须具备的特性。当前特性为 `structured-errors`、`strict-options`、`binding-diagnostics`、`multi-protocol` 和 `ordered-output-segments`。省略请求协商头的旧调用仍可使用同一组接口；Host 会在响应中返回 `X-EasyPrint-Contract-Version: 1` 与 `X-EasyPrint-Features`。

新版设计器发送请求协商头，并只把成功响应中版本匹配且包含所需特性的 Host 视为兼容。旧 Host 缺少响应版本或特性头时，界面提示“请更新 Host”，并暂停依赖这些特性的新增入口。反向代理部署必须双向透传上述请求和响应头；跨域部署只有在现有网关需要时才配置 CORS，本契约不新增 CORS 实现，也不改变超时默认值。升级顺序为先升级并验证 Host，再升级设计器 UI。

错误响应保持 `errors` 数组结构，并使用稳定的 HTTP 状态、`code`、`phase` 和可选 `fieldPath`：

- `400` 表示请求、选项、模板、绑定或资源内容错误；未知根字段或固定嵌套 DTO 字段返回带 JSON 路径的请求错误，`data` 和 `resources` 内部的业务键保持自由。无效图片返回 `InvalidImageResource`、`Resource` 阶段，并在可知时保留对应资源的 `resourceName`（资源键）、`sourcePath`、`sourcePixelWidth`/`sourcePixelHeight`、`renderedPixelWidth`/`renderedPixelHeight` 和 `cropX`/`cropY`/`cropWidth`/`cropHeight`；未知尺寸字段为 `null`。
- `409` 表示契约版本或必需特性不满足，例如 `ContractVersionMismatch` 和 `ContractFeatureUnavailable`。
- `413` 表示请求或显式工作资源超过限制；请求体过大使用 `RequestTooLarge`、`Request` 阶段，资源错误使用 `ResourceLimitExceeded`、`Resource` 阶段，并在可知时附带 `resourceName`、`requestedBytes`、`currentBytes`、`maxBytes`。
- `415` 表示非 JSON 媒体类型或不支持的请求字符集，使用 `UnsupportedMediaType`、`Request` 阶段；`503` 表示准入队列或总资源额度拒绝，使用 `ResourceAdmissionRejected`、`Admission` 阶段，并以 `reason` 区分拒绝原因。
- `500` 只返回稳定的 `InternalError` 和通用处理失败信息，不暴露内部异常、对象或文件路径。

取消会保持取消语义，不伪装成 `400`。响应开始后如果写出或传输失败，Host 无法再把响应改写为新的 JSON 错误；调用方应按传输取消或失败处理。以上是当前 HTTP 契约，不代表所有浏览器、代理配置或真实打印机联调环境都已完成验证。

## 本地运行

需要 .NET 8 SDK、Node.js 和 npm。在两个终端分别运行：

```powershell
dotnet run --project tools/DesignerHost/DesignerHost.csproj
```

```powershell
cd web/Bing.EasyPrint.Designer
npm ci
npm run dev
```

默认设计器地址为 `http://127.0.0.1:5173`，API 为 `http://127.0.0.1:5187`，Vite 将 `/api` 请求代理到 API。端口被占用时可设置 `EASYPRINT_DESIGNER_API_URL`，并以另一个 `--port` 启动 Vite。服务不提供任意文件读取、URL 图片抓取、登录、持久化或打印队列。

前端单测运行 `npm test`；浏览器验收运行 `npm run test:e2e`，会启动或复用本地设计器服务和 Vite，不会提交打印任务。

可从模板列表选择“空白标签”，添加文字、图片、二维码、条码或线段后在画布上直接拖动。位移吸附到 4 点网格，靠近其他元素时也会显示边缘或中心对齐辅助线；写回元素在单元格内的打印点坐标。选中元素后可用方向键微调、在属性面板输入坐标，或拖动右下角控制点调整图片、码图、组合及形状尺寸。属性面板移动起点时会同步移动终点，保持组件尺寸。重复明细在预览中出现多个实例时，移动其中一个实例会调整同一模板定义。撤销、重做及 JSON 导出均保留位置修改。

自动流式文字首次拖动或在属性面板修改坐标时，设计器会按服务端预览的实际位置生成显式坐标并保留未修改的另一轴；居中或右对齐文字水平移动后会转为左对齐并固定在新位置。拆散组合会以当前样例数据的实际布局保留子元素位置和组合占用的高度；预览无法确定位置时会提示错误，不猜测坐标。

本地样例包含空白标签、商品标签、V1 小票及 V2 分页小票。V2 小票展示 `$var` 变量、动态明细、重复表头和页脚。15/70/90 mm 价签与 PM0025/PM0034 重制样例由业务服务加载，不列入通用组件库。

元件区提供单行/多行文字、矩形、椭圆、线段、一维条码、QR、PDF417/DataMatrix、图片、图标和表格。可点击添加，也可将元件拖到画布中指定单元格。选择模式拖动打印元素；拖拽画布模式平移视口。按住 Ctrl/Command 或 Shift 可在画布或结构树中多选同一单元格的元素，再将选中的元素编组；组合可拆散，引用的组合也可先独立化再编辑。自动流式文字编组前会刷新实际布局，跨页或不连续的自动流式选择会提示调整；拆组保留子元素的条件及相对位置，不能无损表示的组合外边距会明确报错。组合可命名保存到当前模板的组件定义，再插入多个实例；组件库支持单独导入、导出、重命名及删除未被引用的定义。动态表格由可编辑的表头行与重复明细行组成，可设置列标题、字段、宽度、对齐和重复表头；CSV/TSV 文字可解析成静态表格。V2 行属性可指定文档/每页顶部、正文、表格表头或底部，页面属性管理根数据变量；数据面板可编辑通用 JSON 样例，字段绑定可选择样例路径。

工具栏支持 `T` 文字、`I` 图片、`Q` 二维码、`B` 一维条码和 `L` 线段快捷键。字段候选按当前结构显示：普通行使用根数据字段，重复明细行使用当前项字段，并提供根数据引用与 `$var` 变量；也允许手动输入。选中单元格后可添加嵌套行。条码制式列表包含 TSPL 原生 `PLESSEY`、`ITF-14`、`EAN-14`、Code 11、China Post 和 `CODE49`；选择 PLESSEY、EAN-14 或 China Post 时，设计器按当前窄条点宽自动保持 1:2 或 1:1 宽条，TELEPEN、TELEPENN、CODE49 使用 TSPL 专用比例编辑器，其他显式值仍由 Host 能力快照和后端严格校验。ZPL Code 49 使用 `^B4`，最终堆叠行数由目标固件决定。Code 11 与 China Post 的最终校验/符号编码由目标 TSPL 固件完成。条码可读文字可设置为协议默认、开启或关闭；显式关闭会清除只在 HRI 开启时有效的预留高度。导入的 HRI 关闭但残留预留高度会显示原因并提供清理动作，不会被静默改写。启用 HRI 或显式四边静区时，条码 `Width`/`Height` 仍是用户确认的设备占位，预览会按协议严格检查是否容纳编码宽度、可读文字和静区。条码宽窄比按统一模板模型编辑，CPCL 保留原有 `Ratio` 语义，其他 Provider 会换算为对应的条宽；最终是否支持仍由所选协议校验。

文字、二维码和一维条码属性可选择 0/90/180/270 度顺时针旋转，文字可设置旋转前行宽控制换行。服务预览返回旋转后的占位及角度；画布选框直接使用该尺寸，仅对内部文字、二维码或条码示意内容旋转。文字已有 `EndCoordinate` 时，属性面板在 0/180 与 90/270 之间切换会同步交换旋转后占用端点，`Width` 仍表示旋转前行宽。二维码属性面板尺寸按旋转后占位显示，编辑 90/270 度占位时会按旋转轴写回模板原始宽高，并同步 `EndCoordinate`；自动尺寸条码和图片在已有服务端端点时也会按旋转前占用框还原原始宽高，再同步旋转后的端点。保存、导入及撤销重做保留旋转配置。目标协议不支持的角度在预览和导出时明确报错；ESC/POS Native 不支持文字旋转，ESC/POS Raster 支持文字四角度，条码/二维码仍按模式能力限制，CPCL 模板条码仍按其能力矩阵限制，TSPL/ZPL 二维码支持四角度，ZPL POSTNET、PLANET 和 Intelligent Mail 同样支持四方向。

## V3 组合编辑

结构树可展开组合内的 `Elements`、`Rows` 和共享组件定义；选中组件实例只编辑位置、数据上下文及参数，选中共享内容则定位到 `Components[...]` 定义，修改会影响所有实例。连续的同级行（包括嵌套行）可用“组合行”生成流式组合，表头与明细应一起选择；流式拖动排序不会拆散相邻的表头和明细。选中元素可用工具栏或 `Ctrl+D` 复制，嵌套内容和资源键一并复制。固定组合可拆组；带 `DataPath` 或参数绑定的组合会拒绝拆组，以免丢失绑定上下文。流式组件使用“独立编辑”解除引用后保留组合容器，以免改变流式与分页语义。

组合属性可切换流式/固定坐标、自动高度、整体同页/允许跨页，设置相对外层的 `DataPath` 和静态 JSON 或字段路径参数。参数在实例外层求值；内部字段使用 `$param.名称`。流式组合的画布拖动通过边距调整偏移或调整行次序，不会隐式切换为绝对坐标；需要固定位置时显式选择“固定坐标”。这些修改连同 V3 版本升级均进入撤销重做历史。

组件库导出时，顶部操作导出当前组件库；每个组件也可单独导出其传递依赖。导出的定义和图片资源会按组件引用闭包裁剪，删除元素后不再携带孤立图片。含图片资源生成 ZIP 模板包，不含图片生成 JSON；导入会检查组件标识及资源键冲突。模板仍遵守原有 JSON、ZIP 和图片大小限制。15/70/90mm 价签与 PM0025/PM0034 继续由业务系统提供，不进入通用内置组件库。

## 预览和导出

`GET /api/designer/samples` 返回固定标签、分页小票、15/70/90mm 价签及 PM0025/PM0034 重制模板。顶部“输出协议”可选择 CPCL、ESC Raster、ESC Native、TSPL 或 ZPL；ESC Raster/ESC Native 分别对应 ESC/POS 的 Raster/Native 布局。预览和指令导出接收相同 JSON 请求，未提供 `protocol` 时默认 CPCL：

```json
{
  "template": { "Version": 1, "Width": 576, "Height": 320, "Rows": [] },
  "data": {},
  "resources": { "images/logo.jpg": "<base64 图片数据>" },
  "protocol": "Tspl",
  "encoding": "Utf8",
  "imageThreshold": 128,
  "deviceHalfCellWidthAt24": 18,
  "deviceFullCellWidthAt24": 36,
  "tsplMedia": {
    "dpi": 300,
    "gapDistance": 3,
    "gapOffset": 1,
    "direction": 1,
    "referenceX": 0,
    "referenceY": 0,
    "speed": 5,
    "density": 8
  }
}
```

`protocol` 可取 `Cpcl`、`EscPos`、`Tspl` 或 `Zpl`；`escPosLayoutMode` 可取 `Raster` 或 `Native`，省略时默认 Native，只在 `protocol` 为 `EscPos` 时生效。选择 Native 时只能导出顺序文字、原生 Code 128/GS1-128/Code 39/Code 93/EAN/UPC/Codabar/I2OF5/二维码及进纸等设备命令；坐标线框、图片、图标和点阵码应选择 Raster。TSPL/ZPL 的斜线位图默认关闭，可通过 `allowRasterizedLines: true` 或界面的“位图斜线”复选框显式启用。TSPL、ZPL 和 ESC/POS Raster 的不支持元素或样式会在预览/导出前报错，并返回模板节点路径。

可选 `encoding` 为 `Gbk`、`Utf8`、`Ascii` 或 `Device`（仅声明了文字契约的 ESC/POS Native），省略时沿用协议默认：CPCL、ESC/POS、TSPL 为 GBK，ZPL 为 UTF-8。TSPL 会输出对应的 `CODEPAGE`；ZPL 仅接受 UTF-8 或 ASCII。ESC/POS Native 必须根据目标机手册，将 `.NET` 编码与 `escPosDevice.codePageIndex`（设备 `ESC t` 编号）配套设置；`escPosDevice.printAreaWidthUnits` 使用设备水平运动单位，`nativeTabWidthDots` 只用于 Native 的主机布局，且须为至少 8 点的 8 的倍数。`escPosDevice.dpi` 仅是设备元数据。设备选项只可用于 ESC/POS，不能从这些主机设置推断打印机已具备相应代码页或字形。

ESC/POS 机型配置放在 `escPosDevice.profile` 中，预览、指令导出和保存项目使用同一配置。界面可新建配置、应用通用八点针式起始参数或清除配置；可编辑图片指令、8/24 点阵、横纵 DPI、每英寸进纸单位、可打印宽度、图片分块预算及设备能力。Raster 还可设置 `escPosDevice.rasterBarcodeHumanReadableFontSize`（1–255 点，默认 12）作为条码可读文字的主机字号。参数必须根据目标机手册填写，针式预设不代表某个品牌已经兼容。没有 Profile 时保留既有 GS v 0 输出；选择 ESC * 后按设备纵向 DPI 编码图片。关闭切刀能力的配置必须关闭打印后切纸，Native 请求的原生码图也须为设备支持的能力；显式选择 Raster 可以由主机生成码图。机型不支持的命令和超出纸宽的页面在输出前报错。

非 CPCL 输出可通过 `imageThreshold` 设置透明图片合成后的黑白阈值，范围为 0–255，默认 128。TSPL/ZPL 可通过 `deviceHalfCellWidthAt24` 和 `deviceFullCellWidthAt24` 填写目标机内置字体在 24 点字号下的半角、全角字宽；省略时由引擎估算，全角省略时使用半角的两倍。需要区分设备字体时，可传 `deviceFontCalibrations`，其请求值按当前协议使用字体名索引，例如 `{ "1": { "halfCellWidthAt24": 16, "fullCellWidthAt24": 32, "lineHeightAt24": 48 } }`；TSPL 字体名为 `0`–`8`，ZPL 字体名为单个数字或大写字母。逐字体校准优先于全局校准，未配置的字体继续使用全局值或协议估算。校准项可包含 `textElements`，以精确 Unicode 文本单元为键，设置 `advanceAt24` 和可选的 `inkLeftAt24`、`inkTopAt24`、`inkRightAt24`、`inkBottomAt24`；该值同时用于预览换行、旋转占位和页面边界预检。设计器项目会分别保存 `outputSettings.deviceFontCalibrations.Tspl` 和 `.Zpl`，切换协议时不会串用字体编号；旧项目缺少该字段时按空表处理。设计器的“输出校准设置”面板会将这些值同时用于预览和指令导出。

TSPL 可在 `tsplMedia` 中设置标签间隙距离与偏移、方向（0/1）、参考点、速度、浓度（0–15）和 DPI。ZPL 可在 `zplMedia` 中设置 `printSpeed`、`orientation`（`None`、`Down`、`Left`、`Right`）、`mediaTracking`（`N`、`Y`、`W`、`M`、`A`、`V`）、`darkness`（-30–30）和 DPI。两个对象只能用于各自协议；未填写的字段保持协议默认。DPI 为设备元数据，不会发送改分辨率命令，也不会缩放模板坐标；应按实际设备点密度准备模板尺寸。

TSPL/ZPL 的机型声明放在对应介质对象的 `profile`：`tsplMedia.profile` 使用 `TsplPrinterProfile`，`zplMedia.profile` 使用 `ZplPrinterProfile`。两者共同支持 `name`、`firmwareVersion`、`printableWidthDots`、`maxLabelHeightDots`、`maxImageDataBytes`、`supportsImages`、`supportsGraphics`、`supportsNativeBarcode`、`supportsNativeBarcodeHumanReadable`、`supportsNativeQrCode`、`supportsRotation`、`deviceFontNames`、`encodingCodePages` 和 `characterRanges`；ZPL 另支持现有切纸能力字段 `supportsCut`。媒体对象及 Profile 的未知字段会严格拒绝；未提供 Profile 时旧的通用能力、默认编码和输出行为不变。Profile 是依据设备手册填写的声明，固件版本只作来源记录，不会自动探测或推断兼容性。

能力查询响应对 TSPL/ZPL 可返回可选 `labelProfile`：`{ name, firmwareVersion, printableWidthDots, maxLabelHeightDots, maxImageDataBytes, supportsNativeBarcodeHumanReadable, encodingCodePages, characterRanges }`。宽度、高度和单色图片字节预算为正 Int32 或 `null`，编码列表是当前协议映射的收窄结果，字符范围是共同覆盖的 Unicode 标量区间；未声明范围时为 `null`。旧 Host 缺少该字段时，设计器在已提交标签 Profile 的情况下明确提示“请更新 Host”；没有 Profile 时不触发该兼容性提示。

输出设置面板可新建、清除并编辑 TSPL/ZPL 的全部 Profile 字段。固件版本仅记录来源；三个数字限额必须是正 Int32；设备字体提供“协议默认范围”“指定已确认字体”“禁用文字”三种模式，TSPL 字体为 `0`–`8`，ZPL 字体为 `0`–`9`/`A`–`Z`；编码列表留空时沿用协议映射；字库范围支持 `first-last` 十进制列表或 JSON 数组。保存、重载、撤销和重做均使用严格深副本，避免 Profile 数组或嵌套字段共享引用；非法 Profile 草稿会阻止预览、协议/模板包导出和项目保存，修正后才可继续。输出设置面板本身使用纵向滚动，长表单不会被截断。

能力快照会过滤字体来源与设备字体、文字/二维码/条码旋转、二维码、条码、图片和椭圆入口；Host 返回的 `qrCodeRotations` 用于二维码角度选择。已有不支持值保留并在预览/导出时由服务端严格拒绝。Profile 只声明字体 `"1"` 而没有 `"0"` 时，协议默认字体会标记为不支持；新增文字会自动选择能力响应中的首个设备字体。图片能力关闭时，图片入口和 `check`/`cross` 图标不可用，但 `info` 图标仍保留为原生图标入口。

TSPL 的可选 `tsplMedia.profile.cutter` 声明受控切刀：`strategy` 为 `Batch` 或 `Immediate`，`supportedCutModes` 为已确认的非空切法列表，`supportsEndOfJob` 必须显式为 true。Batch 在 PRINT 批次末尾自动裁切并关闭自动设置；Immediate 只接受一种实际固定切法，并可声明实测 `cutPositionFeedDots`（0–9999 点）。声明位置后可在页面/任务裁切选项中启用送纸到切刀位置、选择 Dots 并填写额外补偿；实际位置加补偿不得超过 9999。Batch 拒绝补偿及具有正数额外进纸的裁切任务；不同收尾范围也会在任务末尾相遇。

设计器的 TSPL 机型面板可创建、编辑和清除切刀契约；页面与任务沿用自定义裁切面板。新增契约随项目、模板包及撤销重做保存，导入的旧 Profile 不自动增加 cutter。预览/导出 API 检查能力响应是否包含匹配的 `tsplCutter`，旧 Host 缺失时明确拒绝输出；能力返回 `cutFeedDots` 的额外补偿范围。机型和固件能力应依据手册确认，切刀位置应采用实测值。详细说明见 [TSPL 受控裁切](protocols.md#tspl-受控裁切)。

ZPL 的可选 `zplMedia.profile.cutter` 包含 `strategy`（Cutter/Delayed/Kiosk）、`supportedCutModes`、必须明确确认的 `supportsTearOff`，以及 Kiosk 的 `kioskCutMarginMillimeters`（2–9）和可选 `kioskPartialUncutMillimeters`（10–60）。普通 Cutter/Delayed 只映射 Full，Kiosk 声明 Partial 时必须设置未切长度；Kiosk 参数不接受其他策略。`supportsCut: false` 不能配 Cutter。界面提供创建/清除、策略、非裁切模式确认、切法及尺寸控件，按手册确认后保存为机型契约。

能力响应新增 `zplCutter` 和 `supportsIndependentOutputSegments`；直接预览/导出 API 也核对 Host 返回的策略、切法和尺寸，缺少字段或不匹配时提示更新 Host。Delayed ZIP 中页面文件与 `~JK` 控制文件分开，`job.json` 使用 `outputVersion: 2`、`preserveFileBoundaries: true` 及有序 `files` 列表，发送方必须按清单保留独立文件边界。没有多文件的旧页面/Job ZIP 保持原结构。详情见 [ZPL 受控裁切与独立文件](protocols.md#zpl-受控裁切与独立文件)。

文字元素的 `FontFamily` 可在属性面板中指定 ESC/POS Raster 使用的主机字体；未配置固定字体时是系统字体族，留空自动选择。配置 `escPosDevice.rasterFonts` 后使用已上传资源的别名，留空使用默认资源，缺字或缺少所需样式只按显式替代列表处理。`DeviceFontName` 可选择 TSPL/ZPL 内置字体。条码元素的 `HumanReadable` 支持“协议默认”、开启和关闭三种状态，`HumanReadableReserveHeight` 可填写设备实测的可读文字预留高度并参与分页布局。非 CPCL 条码可显式设置 `NarrowModuleWidth`，TSPL 还可设置 `WideModuleWidth`；不支持的协议在预览或导出时报告元素路径。模板 JSON 导出会保留这些属性。

ESC/POS Native 的可选 `escPosDevice.profile.text` 声明编码代码页、设备索引、字符模式、Unicode 字库范围和实测半角/全角宽度及逻辑行高。选择 `encoding: "Device"` 时按声明的代码页编码；其他显式编码须匹配。Raster 的固定字体配置独立放在 `escPosDevice.rasterFonts`，上传有使用许可的单个常规 TTF/OTF 文件及可选的同族粗体，填写别名和版本并选择默认/有序替代资源。资源内容随项目和模板包保存，裸模板 JSON 不携带输出配置或字体字节；切换到 Native 清理固定主机字体配置，可通过撤销恢复。详细契约见 [打印指令集](protocols.md)。

“字体来源”提供旧版字段、协议默认、设备字体、主机字体四种入口。选择新的来源会清除旧字体字段，并保存为 `Font: { Kind, Name? }`；选择协议默认不保存名称，设备/主机字体需要填写名称。CPCL 的设备标识为 `24/55`，TSPL 为 `0`–`8`，ZPL 为单个数字或大写字母；主机字体只用于 ESC/POS Raster。切回旧版删除 `Font`，不修改字号和样式。逐字体校准面板按语义设备名称选择校准键。已有非 CPCL 模板若仍指定 CPCL `FontName=24/55`，预览会明确拒绝，需选择目标协议字体；不会静默忽略该编号。

`POST /api/designer/capabilities` 仅接收输出配置，无需 `template`、`data` 或图片资源。例如 `{ "protocol": "EscPos", "escPosLayoutMode": "Native", "escPosDevice": { "profile": { "supportsNativeBarcode": false, "supportsNativeQrCode": false } } }` 会返回仅支持文字和组合的元素类型。响应包含设备字体与字号范围、文字/条码角度、码图模块范围以及样式、进纸和切刀能力；配置解析与预览/导出共用同一入口。

设计器在输出协议、布局或机型配置改变后重新查询能力，工具栏、快捷键和拖入入口据此控制新增元素，属性面板禁用不支持的选项并说明原因。已有不支持值仍保留并显示，允许用户清除或改选；能力查询和过滤不会为消除不支持值而自动改写模板或输出设置。非 CPCL 的可缩放字号使用整数输入，固定字库和 CPCL 字体按各自范围或离散字号约束。查询尚未完成或失败时，新增入口暂停使用；旧查询响应不会覆盖新配置的能力。能力查询不能替代后端的严格内容与布局校验。

`POST /api/designer/preview` 返回实际分页结果和布局节点。`sourcePath` 使用 `Rows[0].Cells[0].Elements[0]` 格式，可直接映射到模板结构树；重复模板行由 `instanceIndices` 标识当前数据项。V3 组合片段还返回 `definitionPath`、`componentInstancePath` 和 `fragmentIndex`，分别定位共享定义、当前实例和所在页。错误响应的 `errors` 尽可能包含可定位的 `sourcePath`；共享组件错误还会返回 `definitionPath`、`componentInstancePath` 和 `instanceIndices`，其中嵌套组件保留最内层归属。Provider 阶段还返回实际输出页面及页内打印元素的 `pageIndex`、`elementIndex`，均从零开始；静态预检尚未展开实例时这些字段为空。页面整体尺寸、JSON 语法等没有具体节点的错误仍显示为全局错误。

设计器的打印诊断面板保留预览与导出的同一错误结构，显示组件定义、触发实例、各层重复项及已知页号。可分别定位节点、共享定义或组件实例；字段错误定位到最近的可编辑祖先。重复项及页号在界面上从 1 开始显示。静态定义错误不会猜测第一个引用实例，无法确认对应页面时不会跳到某个近似页面，渲染失败也不保留部分打印页。

`POST /api/designer/render` 返回逐页文件的 ZIP：CPCL 使用 `.cpcl`，ESC/POS 使用 `.escpos`，TSPL 使用 `.tspl`，ZPL 使用 `.zpl`。导出和预览使用同一布局轨迹，布局错误会返回一致的模板节点路径，不产生部分打印页。尺寸以逻辑打印点表示；设计器未指定编码时 CPCL、ESC/POS 和 TSPL 使用 GBK，ZPL 使用 UTF-8。屏幕上的字形、二维码和条码是布局预览；打印机实际表现仍取决于设备字库、代码页和固件。

## API 预算、输入与验证

`POST /api/designer/capabilities`、`POST /api/designer/preview` 和 `POST /api/designer/render` 使用同一请求资源作用域。服务器可通过环境变量 `EASYPRINT_DESIGNER_MAX_WORKING_BYTES` 设置非负整数字节上限；未设置时不增加新的累计工作资源阈值。请求根对象的 `maxWorkingBytes` 与服务器上限取较小值，不能放宽服务器限制；服务器环境上限在读取 JSON 前生效，请求字段在 JSON 解析完成后生效。负数配置或请求值明确拒绝。

三个接口的读取、模型保留、Base64 字体与图片解码、模板准备与渲染、JSON 或 ZIP 生成，以及响应发送都在同一作用域内计账。超出预算返回 HTTP 413；错误项的 `code` 为 `ResourceLimitExceeded`，并尽可能包含 `resourceName`、`requestedBytes`、`currentBytes` 和 `maxBytes`，同时保留模板诊断中的 `sourcePath`、页号或组件归属。小型错误响应保留这个诊断出口，不把预算错误静默转换成成功输出。该预算是显式资源估计，不等于完整进程堆、GC 保留量或第三方库内部全部分配；同步 JSON/图片解析和第三方调用只能在进入、返回或内部分块边界观察取消。

可选的全局准入由 `EASYPRINT_DESIGNER_MAX_TOTAL_WORKING_BYTES`（非负 `long`，所有请求的显式工作预算总额）和 `EASYPRINT_DESIGNER_MAX_CONCURRENT_REQUESTS`（正 `int`，同时执行请求数）分别控制；任一前述主开关开启时才启用准入门。总额准入必须同时显式配置既有的 `EASYPRINT_DESIGNER_MAX_WORKING_BYTES` 非负每请求服务器上限。启用总额时，进入请求按该服务器上限整额预留总额；启用并发时预留并发名额，均直到响应发送完成才归还。请求 JSON 中更小的 `maxWorkingBytes` 只能收紧单请求预算，不会提前释放已预留名额。并发上限与总额上限独立生效，未配置准入环境变量时保持原行为。

可选 `EASYPRINT_DESIGNER_MAX_QUEUED_REQUESTS` 为非负 `int`，只在准入门启用时有效，默认 `0` 表示满额后立即拒绝。等待队列按 FIFO 排队；队列已满，或服务器每请求上限大于总额上限时，在读取 JSON 前立即返回 HTTP 503，`errors[0].code` 为 `ResourceAdmissionRejected`，`reason` 分别为 `QueueFull` 或 `RequestBudgetExceedsTotal`。排队期间不读取请求 JSON，也不创建应用工作缓冲；客户端断开或取消时退出队列。已经进入请求后的单请求超限仍按原契约返回 HTTP 413 `ResourceLimitExceeded`。总额只控制显式的每请求工作估计预留，不代表进程堆、框架传输缓冲或排队对象的全部内存。

例如 `EASYPRINT_DESIGNER_MAX_WORKING_BYTES=67108864`、`EASYPRINT_DESIGNER_MAX_TOTAL_WORKING_BYTES=268435456`、`EASYPRINT_DESIGNER_MAX_CONCURRENT_REQUESTS=4` 和 `EASYPRINT_DESIGNER_MAX_QUEUED_REQUESTS=8` 表示最多四个 64 MiB 工作额度，同时允许八个请求等待。实际额度应按工作负载设置。

请求 `Content-Type` 为 JSON 时，未声明 `charset` 按 UTF-8 处理；声明的字符集必须是 .NET 可识别的编码，服务会按声明解码并转换为 UTF-8 后再解析 JSON。未知字符集或非 JSON 媒体类型返回 HTTP 415；声明编码下的非法字节返回 HTTP 400。原始 JSON 输入和转换后的 UTF-8 表示都受 44 MiB 上限约束。HTTP JSON 的 `charset` 只负责请求输入，根对象的 `encoding`、ESC/POS 设备代码页和其他 Host 绑定只负责打印输出编码，二者不是同一个设置。

`DesignerHost.Tests` 是独立的 net8.0 测试项目，不会由现有解决方案隐式执行。需要验证 DesignerHost 时，在仓库根目录运行：

```powershell
.\tools\Verify-DesignerHost.ps1
.\tools\Verify-DesignerHost.ps1 -OutputDirectory artifacts\designer-host-verification-bin
.\tools\Verify-DesignerHost.ps1 -NoRestore -ResultsDirectory artifacts\designer-host-verification-tests
```

验证入口以串行方式执行该测试项目，将构建输出写入独立目录，并将失败退出码原样返回；`-OutputDirectory` 用于指定该目录，默认值为 `artifacts/designer-host-verification-bin`。

## 码图静区

二维码和条码属性面板支持“协议默认／显式静区”及左、上、右、下四边打印点设置。未配置时保留原模板留白；显式四边零值关闭默认留白。静区计入预览占位与尺寸回写，并随模板 JSON、项目、模板包及撤销重做保留。Native 或能力尚未就绪时不能新增显式静区；导入的不支持设置仍会显示，并提供清除入口。预览和指令导出共用后端的边界及相邻占位校验。

自动尺寸元素已有 `EndCoordinate` 时，静区修改或清除按旧值到新值的差量调整外框，二维码仅更新自动尺寸轴；条码按旋转后的数据轴和条高轴更新。默认二维码静区为每边四个模块，默认条码静区按制式及模块点宽计算，显式零值会扣除这部分默认留白。已有外框中的可读文字预留不会重复追加。普通预览不为缺少端点的模板自动创建 `EndCoordinate`；引擎返回的布局是实际占位依据。

## 模板包

“导出 JSON”保持可由模板引擎直接读取的裸模板格式，不包含设计器输出设置。“保存项目”会保存输出协议、编码、介质参数和设备校准值：无图片时导出带版本号的设计器项目 JSON，含图片时导出 ZIP，输出设置放在 `manifest.json` 中，`template.json` 仍是裸模板。既有“模板包”按钮也会保存输出设置。导入兼容旧的裸模板 JSON 和旧 ZIP；旧格式未保存输出设置，导入时恢复 CPCL 默认设置，避免沿用上一个项目的打印配置。编辑器的撤销、重做和未保存标记同时覆盖模板、资源与输出设置。

模板包包含 `template.json`、`manifest.json` 和 `resources/` 下的资源；资源在模板中以资源键引用。导入会检查版本、模板结构、JSON 字节数、ZIP 条目路径、文件数量和展开数据量。设计器不会把资源键解释为磁盘路径或网络地址。

## 当前边界

- 纸张尺寸保存在打印点，毫米按目标 DPI 换算。CPCL 保留 200 DPI；其他协议使用显式设备 DPI 或默认 203，ESC/POS 未指定 DPI 时优先使用 Profile 的横向 DPI。修改 DPI 不重写页面或元素点数。
- 支持通用单色元素、行/单元格边框与字段条件；不提供彩色背景或任意角度自由旋转，元素旋转仍受目标 Provider 能力约束。
- ESC/POS Raster 支持文字、线段、边框、填充矩形、主机 Code 128/Code 39/Code 93/EAN/UPC/Codabar/I2OF5、二维码和单色图片，但严格拒绝反白；ESC/POS Native 支持 24 点顺序文字、粗体、单线下划线、反白、Code 128、Code 128 auto、GS1-128、GS1 DataBar、Code 39、Code 93、EAN/UPC、Codabar、I2OF5、I2OF5C、I2OF5G、二维码和设备级进纸。GS1-128 使用 `GS k 74`，GS1 DataBar/Code 128 auto 使用 Function B 75–79；业务 AI、FNC1 和校验由调用方按目标 Epson 固件约定提供；I2OF5C/I2OF5G 的校验位由 Native Provider 在输出前处理。TSPL/ZPL 支持原生填充矩形，TSPL 还支持统一 MSI 条码（`BARCODE "MSI"`，无校验位语义），ZPL 同样支持 MSI、POSTNET、PLANET 和 Intelligent Mail 条码；当前不支持内置字体粗体、下划线或反白，不支持的能力会严格报错。
- 矩形属性中的“填充矩形”按服务能力启用；填充预览为精确宽高内的黑色区域，保存项目和模板后保留 `Filled`。当前模式或机型不支持已保存的填充设置时保留现值、显示原因，并允许取消填充。图片能力与图形能力独立，标签机型禁用图片不会禁用其原生填充。
- 小票分页由 `TemplateRenderer` 计算；浏览器预览不代替实机校准。
- 设计器只生成协议字节，不负责端口、驱动、打印机控制或真实打印；目标设备的代码页、字体字距、标签介质和裁切行为仍需实机校准。
- ERP 集成仍由 ERP 选择和保存模板，并提供实际商品数据及图片资源。
