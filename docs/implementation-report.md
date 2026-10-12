# 实施结果与验证证据

> 历史实现报告：本文描述早期阶段的实现状态。当前模块、协议和待确认事项分别以[架构与模块](architecture.md)、[核心打印与协议](core-printing.md)和[状态清单](status.md)为准。

2026-09-27 的基础模板与价签验收快照。后续设计器实现及修复见文末当前状态；下文的测试数量属于当时的候选版本，不能当作最新测试统计。改动基于 `4edfb2ed74d875f221cefaf8ec11f6a26c2c2fca`，结果保留在工作区，未提交、推送或发布；项目版本号保持不变。

## 已完成

| 问题 / 能力 | 结果 |
|---|---|
| ImageSharp 2.1.4 已知漏洞 | 升级至兼容现有框架的 2.1.11；Templates 及传递依赖的 NuGet 漏洞检查未发现已知漏洞 |
| 文本及条码指令注入 | 结构化入口预校验控制字符、参数标记与块结束符；保留可信 Raw/格式定义入口 |
| 图形数据逃逸声明长度 | EG/VEG 校验十六进制与尺寸；CG/VCG 校验编码字节数 |
| 重复构建及失败污染 | 每次构建独立结果，失败保留上次成功结果；重新设置页尺寸不清空已返回结果 |
| 自动页头不刷新 | 多位偏移量、份数更新生效，同时保留调用方显式原始页头 |
| 图片修改调用方对象 | 使用自有副本；流不关闭，可定位流恢复原位置；增加字节、像素与溢出检查 |
| 窄图编码越界、尾部黑色补齐 | 移除 BMP 中间编码，直接打包二值像素，未使用位保持白色 |
| 缓冲区和图片分配 | 字节缓冲按倍数扩展，直接编码到目标缓冲；避免 BMP 与十六进制中间副本 |
| 虚线范围错误 | 使用实际起点和长度，循环计数不因 int 溢出回绕 |
| 模板引擎 | C#/JSON、字段绑定、条件、重复明细、嵌套行列、四种元素、文字跨页、重复表头、动态页高与取消 |
| 默认测试打印和缺失资源 | 真实打印显式启用；修正缺图样例，并为既有命令样例增加断言 |

接口通过新增类型和具体类重载扩展，未向旧接口增加抽象成员。当前模板 JSON 支持 Version 1 和 Version 2；版本 2 提供组件、变量和页面分区。使用说明与明确支持边界见 [templates.md](templates.md)。

## 复核后补齐

上次复核指出的五项本地待办已补齐，替代此前仅凭主流程测试判定“全部完成”的结论：

| 待办 | 实现与验收 |
|---|---|
| 一维条码尺寸 | 覆盖现有 15 个制式；分别计算符号、起止/校验与静区；校验输入、补齐零售码校验位及交叉二五码前导零；修正 CPCL ratio 语义与 I2OF5G 指令拼写。57 项测试包含各制式宽度边界、无效数据、JSON 等价及整体跨页 |
| 二维码版本 | 完整 Model 2 1–40 版本，L/M/Q/H 容量；支持数字和字母数字压缩容量、普通文本编码字节、版本分界及容量超限 |
| 字体度量 | 24/55 点阵字库的基础字距与全部字号缩放一致；区分编码单/双字节、Unicode 半角/全角及组合附加符，未知字号/字体明确报错。二维码与字体合计 62 项回归 |
| 中途取消 | 在明细枚举、图片流读取期间取消；验证流释放、同一渲染器及模板取消后重试；另验证绑定失败后重试，3 项测试 |
| C# 小票示例 | 增加与 receipt.json 等价的 C# 模板；逐页比较页数、尺寸、字节及 JSON 往返，固定标签 1 页、小票 2 页 |

这些尺寸计算不代替设备字库与条码扫描验收。混合 Code128 按全 B 上界预留，混合 QR 按字节模式上界预留，固件优化时实际图形可以更小；不再使用跨制式通用乘数或将版本 11–39 一律当版本 40。GS1 应用标识的业务规则和设备所安装字形仍由调用方/设备负责，具体输入契约见使用文档。

## 验证

- .NET 6：当前价签候选完整测试 259/259 通过，价签精准测试 26/26 通过。
- .NET Core 3.1：当前价签候选完整测试 259/259 通过。
- 两个生产项目在 net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1 的 Release 构建通过，零编译警告与错误。
- 本地重新生成两个 NuGet 包，并通过独立项目、独立包缓存和源映射消费本地包；既有消费者验证 EAN13、版本 11 QR 与 JSON 往返，本轮价签消费者验证 90mm 价签、条码、促销划线、会员价和 JSON 往返。
- 示例生成固定标签 1 页和重复明细小票 2 页；PM0034 使用 ERP 的三全商品、条码、规格、产地和实际价格组合。已加载的静态 JSON 与 C# 模板对默认输入逐页字节一致，并分别验证常规、促销、会员、促销+会员分支。
- `git diff --check` 通过。

此前阶段证据位于 `tests/Bing.EasyPrint.Tests/TestResults/completion-net6.trx`、`completion-net31.trx`、`artifacts/packages-completion/`、`artifacts/package-consumer-completion/` 和 `artifacts/template-samples-completion/`。价签验收证据位于 `tests/Bing.EasyPrint.Tests/TestResults/erp-price-label-net6-targeted.trx`、`erp-price-label-net6-full.trx`、`erp-price-label-net31-full.trx`、`artifacts/erp-price-label-nuget-feed/`、`artifacts/erp-price-label-package-consumer/` 和 `artifacts/price-label-erp-validation/`。上轮 `final-*.trx` 保留，不覆盖历史证据。这些目录按项目既有规则忽略，不作为源代码提交。

## 性能对比

同一 Windows 10 x64 主机、.NET 6.0.36、Release 配置。固定输入，无随机种子；每组预热 3 次，5 个采样，每个采样执行 10 次，报告中位数。分配量为进程托管分配量，不代表峰值内存。

历史版本通过 `git archive` 导出后独立构建；前后使用同一探针代码和输入生成方式。新模板引擎不存在历史等价能力，仅记录当前表现。

| 场景 | 修改前耗时 | 修改后耗时 | 修改前分配 | 修改后分配 |
|---|---:|---:|---:|---:|
| 1000 条中英文文字 | 0.411 ms | 0.347 ms | 816,540 B | 640,108 B |
| 512×256 渐变图片 | 8.571 ms | 5.620 ms | 573,419 B | 425,429 B |

文字耗时下降约 15.6%、分配下降约 21.6%；图片耗时下降约 34.4%、分配下降约 25.8%。两组前后输出逐字节一致，分别为 57,933 字节和 32,826 字节。

补齐后的 100 条明细模板输出 3 页、4,701 字节：首次调用 64.239 ms，预热后中位数 0.145 ms、251,175 B 分配。首次调用包含首次类型初始化等成本，不能与预热结果作同比。

本轮原始探针输出保存在 `artifacts/performance/completion-run.log`，对应字节文件在 `artifacts/performance/completion/`。文字、图片、100 条明细三个固定输入的输出与上轮 `after/` 逐字节一致。上表历史比较复用未改变的文字/图片转换范围；本轮同一探针测得文字 0.335 ms、640,108 B，图片 5.528 ms、425,703 B，不重新建立历史基线。以上仍是固定输入测量，不是峰值内存或所有数据分布的性能保证。

运行入口：`tools/PerformanceProbe/PerformanceProbe.csproj`。`EasyPrintProject` 可指定历史快照的核心项目，`IncludeTemplates=true` 启用模板场景。这是本地相对性能证据，不设置新的发布阈值。

## 当前状态与保留限制

- **CLOSED / Completed**：以上本地修复、模板能力、测试、性能对比与包消费验证。
- **OPEN_ACTIONABLE**：无计划内待修复项。
- **BLOCKED_APPROVAL**：无。
- **BLOCKED_EXTERNAL**：真实打印机效果、外部 CI 和本机缺少的 .NET 5/7/2.2 运行时未执行；不影响已完成的本地编译和测试结论。
- **ACCEPTED_LIMITATION**：按用户要求保留旧框架支持，不代表这些运行时重新获得安全维护。测试项目原有 .NET Core 2.2 依赖仍有漏洞告警；稳定包仍保留原有 ImageSharp.Drawing beta 依赖，打包产生 NU5104 警告。未调整发布版本或发布策略。
- **NOT_APPLICABLE**：新模板引擎的历史性能对照；包发布验收（本次不发布）。
- **VERIFIED_BOUNDARY**：JSON、嵌套、行数、页数、输出、图片大小、条码尺寸、QR 容量、字号边界及调用前/运行中取消的回归测试通过。
- **DEFERRED**：新增协议、PDF、打印队列管理不在本轮计划内；独立 Vue 设计器已在后续阶段实现。

本地实施与验证完成。没有可执行问题时停止修复，不因外部硬件或旧运行时状态重复修改代码。

## 本轮执行收口

- Task / Round：模板能力补齐 / 1。
- Implementation：5/5 本地待办关闭；本轮本地验证 5/5（精准测试、受影响项目测试、五框架构建、包/样例消费、固定性能探针）。发布不在范围内。
- Changed Scope：Templates 尺寸计算及内部辅助类型、核心 Helper 的一个指令拼写、对应测试/样例/文档；公开成员签名、TFM、依赖、版本与打包配置无变化。RiskLevel：MEDIUM，涉及布局边界。
- Validation：L0 构建/差异检查通过；L1 新增 122 项通过；L2 两个已安装目标运行时分别 233 项通过；L3 包消费和样例通过；L4 阶段全目标构建通过；L5 固定探针已记录。无重复重建 baseline。
- Reused Evidence：未变更的核心文字/图片转换历史性能基线、原有安全修复证据。
- Completed：五项实现、集成与本地验证。
- Open Actionable / Blocked Approval：无。
- Blocked External：真实打印效果、外部 CI、缺失的旧运行时测试。
- Not Applicable：新模板引擎的历史等价性能基线、本轮发布门禁。
- Accepted Limitations：保留旧框架与既有打包告警；同步解码/getter 无法强制中断。明确的字体/条码协议支持范围见 templates.md。
- Verified Boundaries：上述回归覆盖的输入、资源和取消边界。
- Deferred：新增协议、编辑器、PDF、打印队列，仍不在任务范围。
- No-Progress Check：CHANGED；Next Action：STOP；Goal Status：STOPPED_BLOCKED（本地工作完成，外部设备验收独立保留）。

## ERP 价签验收补齐（2026-09-27）

- Implementation：本地 TODO 3/3 完成。PM0034 样例改用 ERP 实际商品名、条码、规格、产地与价格；标题和条码布局适配真实输入。四种价格分支在已加载的静态 JSON 上完成检查。
- Validation：L1 价签精准测试 26/26；L2 .NET 6 与 .NET Core 3.1 各 259/259；L4 两个生产项目在五个目标框架 Release 构建通过；L3 本地 NuGet 独立消费者通过。
- Changed Scope：价签 C# 样例、PM0034 JSON、回归测试及文档；类库公开 API、目标框架和依赖不变。RiskLevel：LOW。
- Evidence：见上文 `erp-price-label-*` TRX、NuGet 包、独立消费者及离线 CPCL 输出目录。
- Open Actionable：无。Blocked External：真实打印机版式/扫码验收及外部 CI；不以本地 CPCL 检查替代。Next Action：STOP；Goal Status：STOPPED_BLOCKED。

## 设计器 TODO 收口（2026-09-28）

- Implementation：修复自动流式元素首次拖动、属性面板编辑坐标时另一轴归零、自动文字及组合拆组后的坐标与间距、嵌套重复行字段候选，以及导出错误与预览错误的节点路径不一致。保留现有模板与服务接口。
- Validation：前端单测 72/72、浏览器端到端测试 33/33、前端和 DesignerHost 构建通过。浏览器覆盖五种业务价签样例、动态表格分页、变量绑定、CSV/TSV 表格、多元素组件保存复用、嵌套重复渲染、条件组合显隐、单段文字跨页、自动文字坐标编辑、组合拆组前后位置和 CPCL 一致性。离线 HTTP 请求也确认过大元素在预览与导出入口返回相同 `sourcePath`。
- Reused Evidence：本轮未修改生产类库，沿用上一轮 .NET 6 模板测试及目标框架构建结果；未重新建立无变化范围的性能基线。
- Open Actionable / Blocked Approval：无。Blocked External：真实打印机版式和扫码验收、外部 CI 与本机缺失运行时的执行证据。没有提交、推送或发布。
