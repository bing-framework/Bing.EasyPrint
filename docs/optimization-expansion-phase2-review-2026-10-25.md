# 第二阶段 O4 独立复核

> 本文件保留初版复核记录。当前状态见 [第 1 轮修正复核](optimization-expansion-phase2-review-r1-2026-10-25.md)：4 项已关闭，6 项继续修正，下一轮为 2/3。

日期：2026-10-25。当前结论：`NEEDS_FIX`。O4 有可用的文字样例和目录接入，但尚未满足已批准的完整新协议注册契约，暂不进入 F1/F2。以下是可执行缺口，不涉及实机或外部 CI。

## 证据

主代理阅读核心注册/收尾、模板会话与适配器、Host 自定义分发、项目格式及浏览器用例。另使用交付的 net10 Host DLL 在临时端口 5197 发起实际 HTTP 请求，完成后已停止该进程。请求与响应保存在：

`artifacts/optimization-expansion-20261025/phase2/main-review-r0/host-probes.json`

实际观察：

- 自定义能力返回 `protocol: Cpcl`，而目标是 `demo.protocol`。
- 同一个自动页高模板，4 条记录各 24 dot；配置最大页高 200 返回 1 页（高 96），配置 60 返回 400 “示例协议页面尺寸无效或超过配置上限”，未分页。
- `supportsFinishing=false` 搭配 `feedLines=3`、`cutAfterPrint=true` 返回 200；ZIP 内实际含 `FEED 3` 和 `CUT`。
- 显式 `protocolId: " "` 返回 200 并回退到内置能力，未拒绝无效选择。

## 修正清单

| ID | 状态 / 级别 | 证据与影响 | 修正及验收 |
| --- | --- | --- | --- |
| P2-R1 | OPEN_ACTIONABLE / P1 | `TemplateRenderer.cs:612-616` 将自定义结果构造成旧 `TemplatePage/TemplateRenderResult` 并携带 `options.Protocol` 默认 Cpcl；Host `CustomCapabilities` 同样写死 `protocol="Cpcl"`；App 的自定义 `printProtocol` 也伪装 Cpcl。违反独立标识/新结果类型约定，其他枚举分支仍可能影响自定义路径。 | 建立真正中立的新入口/结果/能力类型；旧 API 保留适配，不能拿任意内置枚举代表新协议。回归 C#、HTTP 和前端，自定义结果不携带虚假旧协议；新旧选择器互斥。 |
| P2-R2 | OPEN_ACTIONABLE / P1 | `IPrintCanvas.cs:148,188,218` 对所有 custom 的一维码、QR、Composite 无条件抛异常；`ITemplateProtocolAdapter` 仅提供文字测量和 Backend，缺少码制测量、转换等目标适配能力；Host 自定义条码、字体样式等能力写死为 false。第三方即使声明支持，也无法接入而不修改核心。 | 实现计划要求的目标测量/元素转换及协议中立能力契约，并贯穿模板、Host、设计器；不能以样例不支持为由全局禁止扩展。独立测试协议至少证明自定义文字及条码/二维度量改变真实布局、输出和协商；支持/不支持都按注册声明判定，不改内置 switch 或前端列表。 |
| P2-R3 | OPEN_ACTIONABLE / P1 | `TemplateRenderer.Session:376` 页高仍只读取模板 Height/旧 options.MaxPageHeight，未使用适配器的 MaxPageHeightDots。自定义能力只在 validator/backend 里拒绝。实际 96-dot 内容与 max60 请求失败，报告却宣称配置改变页数。 | 把目标分页策略/上限接入共享布局。自动页高、流式重复与固定高度语义明确；相同记录 max60 与 max200 在允许的自动分页模板下应产生不同正确页数，每页不越界，绑定/ownership 保持。 |
| P2-R4 | OPEN_ACTIONABLE / P1 | `RenderCustom` 调用 composer 时 finishing 固定 null；Host 不提供通用收尾请求且拒绝旧 job；未声明支持的样例仍直接执行 page.FeedLines/CutAfterPrint。`PrintProtocolOutputComposer` 先 Render 再 ValidateFinishing，不满足早拒绝；把控制文件作为新页面，破坏其“页面数与文档一致”语义。 | 将可选收尾贯穿核心/模板/Host/设计器，未声明时在任何编码前拒绝；旧快捷 feed/cut 不得绕过。预检长度、负预算、取消和返回页数要在正确阶段验证。控制文件保留独立 segments 与位置，不冒充新增打印页。测试调用顺序、unsupported 的 RenderCalls=0、JobEnd/PageEnd、份数、取消/Dispose、超限无部分输出与真实 HTTP 输出清单。 |
| P2-R5 | OPEN_ACTIONABLE / P1 | 报告明确内置仍原工厂，ProviderBackend 仅为旁路适配测试；模板自定义直接 `CustomProtocol.CreateBackend`，绕过冻结核心 registry 的 ID 校验。Core 注册描述只存任意 JSON 字符串，实际能力分散在样例常量、模板能力和 Host 硬编码中。 | 按已批准方案统一新旧执行逻辑和会话创建，保留旧入口外观/字节契约。注册目录、模板和后端共享一致的描述/配置及 ID 校验；配置/能力的有效性有明确服务端校验，避免不同 factory 分叉。测试错误会话 ID、坏配置、不可变描述与旧内置字节/segments。 |
| P2-R6 | OPEN_ACTIONABLE / P2 | Host `Capabilities` 遇 protocolId 直接走 CustomCapabilities，绕过 PrepareOptions 中的新旧互斥检查；空白 ID 静默回退已实证。TemplateRenderOptions 虽注释互斥，却不拒绝显式非默认旧 Protocol。 | 统一请求选择校验，按实际字段存在处理空串/空白/null/冲突并返回稳定诊断；能力、预览、导出、PNG 接口一致。合法旧省略默认仍保持；新增入口不再依赖旧枚举默认。 |
| P2-R7 | OPEN_ACTIONABLE / P1 | `customProtocol.spec.ts` 的成功路径将目录、能力、预览全部 mock，且只保存项目，没有实际 Host 导出/重新导入。不能证明“真实浏览器联动”；样例无正向软件像素路径，通用像素声明也未验证。 | 保留降级 mock，另加真实 Host 成功路径：发现独立样例、配置改变实际布局、预览、导出检查字节/segments、保存再导入并复现；补至少一个声明软件码图的独立目标验证同源布局与预算/取消。C#/JSON 可独立运行案例及包消费按总计划验收。 |
| P2-R8 | OPEN_ACTIONABLE / P2 | DesignerProject v2 仅把 version 改为 2 并增加 protocolId/config；顶层 known keys 仍仅四项，没有计划要求的 editor state 容器，导入 editorState 会报错。 | 增量建立 v2 编辑器元数据容器并往返保存，旧 v1/裸模板兼容，未知协议状态仍保留/禁用。这里只铺设 v2 状态契约，不提前实现 F6 锁定和排列。 |
| P2-R9 | OPEN_ACTIONABLE / P2 | 独立证据盘点：报告把较早 4640/4640 的 runtime-smoke.log 与最终 4641 TRX 并列；legacy 首次长路径失败没有原始日志；指南目录样例的名称和空 schema 与实际注册不一致；“net5/core2.2 五框架 XML”表述也不是实际生产构建矩阵。 | 区分最终与历史证据；缺失首次失败日志如实声明，不重造；指南给真实响应或明确示意。报告精确列出生产五框架及各测试运行时，保存后续首次失败/退出码。 |
| P2-R10 | OPEN_ACTIONABLE / P2 | 本阶段共享模板和公共 API 已改变，但报告未给本轮模板 C#/JSON/往返案例证据，候选旧消费仅 netcoreapp3.1，未满足阶段安排的 net6 消费；浏览器正向覆盖见 R7。 | 完成批准计划的案例与 net6 候选包消费，保留当前 netcoreapp3.1 的有效结果，不以它替代明确要求的 net6 路径；证据绑定修正后候选。 |

独立 luna_worker 已核对交付主要计数：核心三个运行时各 4641、Host 277、XML 10、Vitest 420、Chromium 200；现代/3.1 消费候选哈希匹配，实际运行时正确。这里确认已有测试结果属实，不将计数误作为上述缺口已覆盖的证明。

## 修复安排

第 1 轮集成修正（1/3）。上述架构项均在用户明确批准的 O4 范围，不是新增功能。先完成中立契约、共享执行与布局/收尾，再对接前端并补实际端到端证据；不要只扩大样例特判，也不要以已通过旧测试数量替代。

这轮改变公共增量契约与共享布局，须定向测试后重跑本阶段相关完整门禁：五框架 XML、核心真实 net10/net6/netcoreapp3.1、Host、前端构建/Vitest、完整 Chromium（workers=1/retries=0）、模板案例与本地候选包消费。保存首次失败与真实运行时，避免生成物覆盖跟踪 XML。外部 CI 保持 BLOCKED_EXTERNAL，不发布、不改版本、不覆盖旧 API 基线。
