# Bing.EasyPrint 优化与扩展最终本地验收

日期：2026-10-26。范围为 [十项实施计划](../ai_docs/plans/optimization-expansion-2026-10-25.md)。本报告同时记录第五阶段修正 2/3 的独立复核，不覆盖或重写此前失败记录。

后续更新：2026-10-27 已完成批量案例能力协商和协议文档勘误，见[配套完善报告](post-review-polish-2026-10-27.md)。本页保留原验收日期与结果。

## 结论

**本地实现 10/10 CLOSED，OPEN_ACTIONABLE=0，停止自动修复。** Windows/Linux GitHub Actions 尚无外部执行结果，保持 `BLOCKED_EXTERNAL`；依据 GOAL_RULES，整体状态为 `STOPPED_BLOCKED`，不是继续改代码的理由。实机验收按用户要求排除。

未提交、推送或发布；未改产品版本或自动覆盖 API 基线。Core/Templates 继续五框架，Host 为 net10.0。本结论只覆盖本计划和已验证场景，不宣称所有未来协议/设备能力均已实现。

## 功能与契约覆盖矩阵

| 项目 | 实现/边界 | 测试、案例和文档证据 | 状态 |
| --- | --- | --- | --- |
| O1 Host 升级 | Host/测试 net10，核心五 TFM 保留 | [第一阶段复核](optimization-expansion-phase1-review-2026-10-25.md)、当前默认 Host 368 项、开发/Host 手册 | CLOSED |
| O2 合并预览 | 同请求一次渲染，图片/布局/诊断 ZIP，旧 Host 回退、取消与预算 | 第一阶段反例/契约门禁；当前真实浏览器及 Host 回归 | CLOSED |
| O3 跨平台验证入口 | Windows/Linux 工作流和现代消费路径 | 第一阶段复核、三真实运行时/现代包消费；实际 Actions 运行另列外部门禁 | CLOSED |
| O4 协议注册 | 字符串标识、冻结注册表、会话所有权、独立布局/收尾、Host/设计器配置、项目 v2 | [第二阶段最终复核](optimization-expansion-phase2-review-r3-2026-10-25.md)、独立协议样例、[扩展指南](protocol-extension-guide.md) | CLOSED |
| F1 字段格式 | 显式文化、数值/日期/映射、空值及固定处理顺序，旧行为保持 | [第三阶段复核](optimization-expansion-phase3-review-r3-2026-10-26.md)、JSON/C# 往返与失败用例、模板手册 | CLOSED |
| F2 批量生成 | CSV/JSON/JSONL，25 条分块、1000 条/页及 32 MiB 限额，部分失败和独立 segment | [第三阶段续作复核](optimization-expansion-phase3-resume-review-r3-2026-10-26.md)、BatchConsumer、[批量指南](batch-generation.md) | CLOSED |
| F3 Digital Link | GTIN AI01 未压缩 URI、合法限定符和属性、转义/规范化、结构化绑定 | [第四阶段最终复核](optimization-expansion-phase4-review-r2-2026-10-26.md)、URI/重复 AI/重复实例反例、[Digital Link 文档](gs1-digital-link.md) | CLOSED |
| F4 AI 编辑 | 服务端固定字典目录、结构化诊断、动态数据 pending、旧 Host 降级 | 第四阶段复核、真实 Host/浏览器保存转换与校验、GS1 使用文档 | CLOSED |
| F5 PDF/SVG | ESC/POS Raster 位图 PDF；软件码图矢量 SVG，无设备 HRI；单条/批量同布局和 DPI | [第五阶段上一复核](optimization-expansion-phase5-review-r2-2026-10-26.md)、PDF 独立读取/渲染、SVG 栅格/解码、Phase5/批量样例、[导出指南](phase5-export-editor-guide.md) | CLOSED |
| F6 编辑操作 | 稳定节点身份、继承锁、锁定子树保护、有效布局排列、项目/数据历史及预算 | 第五阶段两轮修正，当前 230 项浏览器和独立反例重放；v1/v2、JSON/ZIP、撤销/重做案例 | CLOSED |

当前接口、抽象和功能归类沿用各阶段审定边界：新协议注册不借用内置枚举；模板适配承担目标度量；Host 承担 PDF 依赖与 HTTP 预算；编辑状态保留在项目元数据；软件编码共享核心能力。独立样例不声明真实设备兼容。

README、开发手册、核心/模板/设计器指南和新增专题文档已随各阶段更新。历史验收结果保留原日期，不将旧结果改写成新结果；根 README 原始快速示例本轮已直接验证。

## 最终本地验证清单

以下按八组本地门禁记录（8/8 通过），并显式说明复用范围。并非声称所有命令都在最后一次编辑后重新运行；最后两轮仅改变 Host/前端/案例时，未变化的库范围复用原证据。

| 门禁组 | 结果及实际环境 | 证据/命令入口 |
| --- | --- | --- |
| 五 TFM/XML | Core/Templates 各五框架，共 10/10，严格 XML 检查通过 | `phase5/final-candidate/xml-documentation/`；`tools/Verify-XmlDocumentation.ps1`；生产库未变复用 |
| Core 三运行时 | net6.0、netcoreapp3.1、net10.0 各 4705/4705，失败/跳过均 0；实际 .NET 6.0.36、3.1.32、10.0.11 | `phase5/final-candidate/core-runtime-r1/`；`Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1,net10.0`，未滚动替代 |
| Host | 默认无参数验证 368/368，定向导出 11/11，实际 .NET 10.0.11；失败/跳过 0 | `artifacts/designer-host-verification-tests/designer-host-net10.trx`；`phase5/integration-r1/green-r1-host-regressions-final/`；`tools/Verify-DesignerHost.ps1` |
| 前端类型/构建/单元 | `npm run build` 含 vue-tsc，1982 modules；Vitest 27 文件 476/476，失败/跳过 0 | `phase5/integration-r2/diagnose-build.log`、`vitest.log`；Node 22.22.0 |
| Chromium | 当前有效候选 230/230，定向 3/3；workers=1、retries=0，Chromium 153.0.8010.12、Playwright 1.63.0 | `phase5/integration-r2/chromium-full-corrected.log`、`green-targeted-r3.log`；主代理额外 UI 探针见下 |
| 案例/独立导出/README | 模板 21 项一致性检查/136 输出；批量 opt-in 2/2 记录、2 页、2 PDF+4 SVG；根 README 14 bytes | Phase5 执行报告明确路径；PDF 7 页检查/7 次解码；SVG 10 个样本中 7 个独立解码向量，Composite 仅模块等值，不冒充解码 |
| 候选包消费 | net6、net10 均通过；实际 .NET 6.0.36/10.0.11，分别选 net6/net7 资产，候选与还原包 hash 相符 | `phase5/final-candidate/package-net6-shortpath/`、`package-net10/`；Core/Templates 未变复用 |
| 差异/生成物 | `git diff --check` exit 0；已有 CRLF→LF 提示保留，不为格式提示批量重写文件；无提交/发布/版本变更 | 当前工作树；独立证据写 artifacts，复核/计划局部修改 |

上述 `phase5/` 均位于 `artifacts/optimization-expansion-20261025/`。精确参数、TRX、testhost 日志、输出位置见 [阶段执行报告](optimization-expansion-phase5-2026-10-26.md) 和 [修正 2 命令记录](../artifacts/optimization-expansion-20261025/phase5/integration-r2/commands.md)。

候选范围：Host DLL SHA-256 为 `53D4FF35046C1C180BAFA677D8CB4BF97796FDE8A31733E49A67E92C3A6BF10C`。Core 候选包 `D7BB11AA3321F824F20C1AE657FAC9585348B5D84F2C39DB62EA19B5B6D05683`；Templates `9B917FFDD2426640A1D584B19024D59C342ED6B81C348987D35A535112F6BBA7`。前端 r2 没有额外源码哈希记录；源码/测试时间戳早于有效构建/测试，主代理在当前工作树独立重放两项关键交互。该证据不能等同外部 CI 的可重复构建证明。

## 修正 2/3 独立复核结果

### R4-B：旧预览排列 — CLOSED

实现给模板/数据/配置变更递增 `previewRevision`，布局只有在成功且请求未失效时记录对应版本。去抖、请求在途、失败或本地近似预览期间排列禁用，因此不是仅增加测试等待。

主代理使用真实 Vite/Host、Chromium，重放“左对齐→预览未返回→右对齐”，延迟预览网络响应 1500ms 以稳定观测。在等待期 `staleDisabled=true`；返回成功布局后，两个宽 40/30 的矩形得到 X=10/20，符合实际占位框。页面非空、标题正确、无错误覆盖层、pageerror 为空。执行侧还覆盖上/下、幂等、失败预览及逐步撤销。

勘误：上一轮原文字夹具的 EndCoordinate 不能代表文字的实际渲染宽度。上一报告写该夹具预期 X=10/20 不准确；两段等宽单字在新鲜布局下应同为 X=10。旧实现实际 X=10/70 仍是错误，因此根因判断不变。本轮先观察到修正后的 X=10/10，然后改用明确宽 40/30 的矩形做正式确认，得到 X=10/20。保留原探针文件，不将夹具期望错误记成产品回归或第三轮修复。

### R3-B：排列锁定子树 — CLOSED

`firstLockedSubtreePath` 遍历实例与定义后代，结合继承锁；按钮状态和执行函数均检查。主代理真实浏览器重放锁定文字 A 的组合：排列被禁用、原因指向锁节点，组合前后均为 `(20,20)`，子节点锁保持 true。此前 `(20,20)→(100,20)` 的移动已不再发生。

### E1-README — CLOSED

独立 net6.0 工程的 Program.cs 以前述根 README 原片段开始，后接输出/运行时采集代码。验证记录 `readmeSnippetMatch=true`、`.NET 6.0.36`、exit 0，输出 `1B40B6A9B5A5CDEAB3C90A1B6402` 共 14 bytes，GBK 解码为“订单完成”，结尾为 Feed(2)。使用的核心候选包 hash 与上述候选一致。

证据目录：[主代理重放](../artifacts/optimization-expansion-20261025/phase5/main-review-r3/)、[根 README 验证](../artifacts/optimization-expansion-20261025/phase5/integration-r2/readme-root-example/)。主代理使用临时 `5193` Host、`5189` Vite，均已停止，没有结束其他已有服务。独立 luna_worker 只读核实了本轮所有计数和 README 记录。

## 首败与剩余边界

- 本轮首次完整 Chromium 为 225 通过/5 失败：Vite 代理指向旧 Host。记录与上下文保留；修正进程环境后验证代理 health/features，候选全量 230 通过。两次都 retries=0，不用最终通过改写首次失败。
- README 首次用户 NuGet 缓存构建失败，隔离还原后运行通过；日志保留。此前 Core 取消时序、旧 mock、长路径和导出样例首败保持各阶段原记录。
- Windows Actions、Linux Actions 为两项外部门禁，当前 0/2 有真实运行证据，均 `BLOCKED_EXTERNAL`；不能以本机 Windows 结果替代 Linux。
- `ACCEPTED_LIMITATION`：SVG 不含设备 HRI；PDF 仅明确准确光栅目标，首个为 ESC/POS Raster；Digital Link 第一版 GTIN/未压缩；批量容量按现有限额。
- `DEFERRED`：设备传输、复杂字体塑形、富文本、更多厂商协议及图片依赖大版本迁移。依赖安全告警仍可见；保留旧框架的兼容决策未变，不宣称消除全部依赖告警。

## 停止记录

- Implementation：10/10；八组本地门禁 8/8；外部平台门禁 0/2。
- Completed：O1—O4、F1—F6；第五阶段 R1—R4、E1 及 R3-B/R4-B/E1-README。
- Open Actionable：无。Blocked Approval：无。
- Blocked External：两平台 CI。Not Applicable：实机验收及本轮发布。
- Verified Boundaries：既有输入、输出、工作内存预算及取消拒绝路径按阶段证据保留，不提高阈值。
- No-Progress Check：CHANGED；两轮修正后已有独立反例闭环，未触及第三轮或无进展停止阈值。
- Next Action：STOP，不再派发自动修复或监听实现线程。
- IMPLEMENTATION_STATUS=PASS；TEST_STATUS=PASS（本地范围）；EXTERNAL_GATE_STATUS=BLOCKED_EXTERNAL；RELEASE_STATUS=NOT_REQUESTED；GOAL_STATUS=STOPPED_BLOCKED。
