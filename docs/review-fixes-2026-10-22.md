# 工程审查修复与验收（2026-10-22）

## 当前结论

本轮保留核心库与模板库的五个目标框架及 .NET 公共 API，修复审查 F1—F8，并在 Composite 浏览器验收中发现、修复 F9（画布过滤遗漏 Type 11）。未提交、推送或发布包，开始执行前的工作区文档修改保留。

本轮 10 项本地实施事项全部闭环，包含 F9 的完整 Chromium 回归为 154/154 通过，0 失败、0 跳过、0 flaky，关闭自动重试。图片依赖迁移与设备验收单独归类，不以本地测试通过代替这些事项。

计划：[修复清单](../ai_docs/plans/review-fixes-2026-10-22.md)。原审查依据：`artifacts/review-20261022/review-evidence.json`；新证据：`artifacts/review-fixes-20261022/`。

## 修改与验收对应

| 项目 | 实际修改 | 验收证据 | 状态 |
| --- | --- | --- | --- |
| F1 能力空值 | 保持 Host 响应不变；前端接受 null/缺省，非 Native 的真实数组（含空数组）仍拒绝 | 五模式 Host 响应测试、Vitest 空值/数组矩阵、真实浏览器模式切换 | CLOSED |
| F2 TIFF 异常目录 | 两个图片入口共享内部结构预检，检查大小端、目录范围、数量、循环及取消，恢复流位置 | 新增 37 项测试，四协议模板诊断、资源释放、合法 Classic/BigTIFF、PNG/JPEG/GIF、多帧首帧与隔离复现 | CLOSED |
| F3 ZPL 方向 | UI、类型、项目校验统一为默认/None/Down；非法旧值明确拒绝 | 项目往返单测、真实 Host PON/POI 字节及 Left/Right 拒绝 | CLOSED |
| F4 包消费 | 普通 ZPL 与延迟切纸案例均改为 320×40 dot，精确断言同步 | 当前源码候选包 net6.0 全部消费步骤通过，候选与消费包匹配 | CLOSED |
| F5 E2E 预期 | Code39 按实际能力可选，保留不支持字体/旋转检查；复选框改精确名称 | 原失败用例及完整 Web 回归 | CLOSED |
| F6 Host 默认构建 | 默认项排除项目内 artifacts/output，保留四个显式 Samples | 默认脚本、独立干净输出路径均 140 项通过，MSBuild Content 清单仅四个案例 | CLOSED |
| F7 公共 XML | 8 个核心源文件补齐 117 项缺失注释，新增严格五框架检查脚本 | Core/Templates 共 10 次 Release 重建通过，XML 解析通过，未改公共签名 | CLOSED |
| F8 文档 | 同步开发、核心、模板、设计器及状态说明，README 增加报告入口 | 严格文档门禁、示例、链接及差异检查 | CLOSED |
| Composite 案例 | 创建、属性编辑、Host 尺寸、CG 二进制、JSON/项目保存重载、旧 Host 降级 | `e2e/reviewFixes.spec.ts` 三项定向通过 | CLOSED |
| F9 画布遗漏 | Canvas 元素过滤上限包含 Type 11，加入真实画布 Text 节点断言 | 新断言先失败，修复后通过；最终完整 Chromium 154/154 | CLOSED |

TIFF 预检采用与现有解码器一致的递增目录及目录数量规则，使用常量额外空间；条目数量先按剩余字节校验再计算偏移，不由不可信 count 驱动逐条循环。新增检查抛出已有 `InvalidImageContentException`，模板保留资源键及路径，Host 继续映射到 `InvalidImageResource`。这不是完整 TIFF 解码器，也不保证能中断任意第三方同步解码调用。

Composite 画布显示用于选择、编辑和布局的占位，在线尺寸来自 Host 编码结果；最终输出包含实际复合码。画布占位不是可扫码的符号图片。

## 本轮验证

| 检查 | 结果 | 命令或证据 |
| --- | --- | --- |
| Core/Templates 五框架构建及 XML | 10/10 PASS | `./tools/Verify-XmlDocumentation.ps1`；`artifacts/xml-documentation-verification/20261022-111941-700-3eb238f1/verification.json` |
| 核心 net6.0 | 3397 通过，0 失败，0 跳过；实际 6.0.36 | `Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1`；`runtimes/20261022-032117-d9ecd8e3/runtime-verification.json` |
| 核心 netcoreapp3.1 | 3397 通过，0 失败，0 跳过；实际 3.1.32 | 同上，未使用运行时滚动替代 |
| Host 默认入口 | 140 通过，0 失败，0 跳过 | `./tools/Verify-DesignerHost.ps1`（仅指定证据目录）；`host/designer-host-net8.trx` |
| Host 新输出路径 | 140 通过，0 失败，0 跳过 | `host-clean/designer-host-net8.trx`；没有关闭默认 Content |
| Web 类型检查及生产构建 | PASS | `npm run build`；`web-build-canvas.log` |
| Vitest | 277 通过，0 失败，0 跳过 | `npm test`；`web-unit-canvas.json` |
| Chromium 最终回归 | 154 通过，0 失败，0 跳过，0 flaky | `npm run test:e2e -- --retries=0 --workers=1`；`web-e2e-canvas-final.json` |
| 模板案例 | PASS，24 个输出文件 | `dotnet run --project samples/Templates -- artifacts/review-fixes-20261022/samples tests/Fixtures/Fonts/DejaVuSans.ttf`；C#/JSON/往返一致 |
| README 示例 | PASS，14 字节 | `probe-results.json`，独立 net6.0 探针 |
| 本地候选包消费 | PASS | 两库 Release pack 后运行 `Verify-PackageConsumer.ps1 -TargetFramework net6.0`；`consumer-final/consumer-net6.0/consumer-verification.json` |
| 异常 BigTIFF 隔离确认 | CPCL 289 ms、模板 183 ms 内返回输入错误 | 24 字节、50 亿条目声明、8 MiB 预算；沿用 5000 ms 观察窗，含进程启动开销，非性能基准 |
| 桌面/移动端 QA | 无框架错误覆盖层、无控制台错误或警告，交互和画布节点通过 | `visual-qa.json`；1440×900 与 390×844 |
| 开发手册打包命令 | PASS，生成两份候选包、十份可解析 XML；按项目及框架隔离输出 | `development-pack-command.json`，直接执行手册代码块中的打包步骤 |
| 差异、编码与链接 | PASS，15 份变更 Markdown 无失效本地文件链接；UTF-8 和 diff 检查通过；8 个注释文件无非注释差异 | `static-checks.json`；未修改跟踪中的 XML 生成物 |

Web 验证使用 Chromium，地址为 `http://127.0.0.1:16173`，Host 为 `http://127.0.0.1:16187`。Browser 插件不可用，使用仓库已有 Playwright。实际流程为：空白模板→新增 Composite→修改模块宽度与次数据→Host 预览→检查画布节点→输出与保存重载；移动端检查属性面板和页面宽度。

首轮完整 E2E 曾报告 154/154，通过截图发现其未断言 Composite 的画布节点，故增加该断言并修复 F9 后重新做完整 Web 验收。中途还修正了新测试的 CG/EG 预期、图片测试的 ESC/POS Raster 设置，以及包消费后续延迟切纸案例的同类越界参数；未放宽生产边界、未增加超时、未跳过用例。

## 功能与配套覆盖矩阵

| 功能 | 实现与接口 | 单元/契约 | 案例与端到端 | 文档 |
| --- | --- | --- | --- | --- |
| CPCL | 原命令与统一模型；新增图片入口预检 | 核心测试、图片失败原子性/所有权 | 模板案例、README及消费程序 | 核心手册、模板图片限制 |
| ESC/POS Native/Raster | 分别保留顺序命令与光栅职责 | Native 能力、Raster 图片、前端空值矩阵 | 实际模式切换、Native/Raster 导出 | 模式边界及空值语义 |
| TSPL/ZPL | 设备契约保持；ZPL整页方向一致 | 四协议测试、方向拒绝 | 媒体配置、精确字节、延迟切纸消费 | 协议/设计器方向说明 |
| 模板/打印任务 | 共享模型、分页、预算、输出段 | 两运行时核心完整测试 | C#/JSON/往返、任务与切纸案例 | 模板使用及资源边界 |
| Host | HTTP 契约保持；默认生成目录排除 | 140 项 | 实际浏览器与包消费独立验证 | 默认验证入口、结构化错误 |
| 设计器/Composite | 能力准入、旧配置拒绝、Type11布局占位 | 277 项 Vitest | 编辑、预览、导入导出、特性降级 | 在线占位与真实输出区分 |
| 公共 API/抽象 | 无 .NET 签名变更；内部共享检查器 | 严格 XML、依赖构建 | 当前包消费通过 | 补齐缺失注释并说明扩展边界 |

项目依赖和功能归类继续维持 Core→Templates→Host 的消费方向及 Web HTTP 边界。自定义 Provider 可用于统一模型；模板静态工厂及部分内部收尾扩展仍是现有边界，本轮未改造成完整插件系统。

## 后续状态与执行规则

- **OPEN_ACTIONABLE**：无；本地实现与规定验证全部完成。
- **BLOCKED_EXTERNAL**：真实打印机验收。记录型号/固件、DPI/纸宽/耗材，验证字体和代码页、进纸与切纸位置、扫码回读、取消/传输中断/重试及设备状态恢复，并保存样张。
- **ACCEPTED_LIMITATION**：库生成协议字节；设备连接、驱动、发现、队列与状态查询由调用方承担。完整协议插件注入不在本轮范围。
- **DEFERRED**：ImageSharp 及关联包大版本迁移。为保留旧框架与公共图片类型，本轮采用已复现输入的前置防护，NuGet 原有 NU1902/NU1903 仍存在，不能据此宣称依赖全部安全。
- **NOT_APPLICABLE**：本轮没有大型性能矩阵或新增资源阈值；隔离复现只是缺陷验收。
- **BLOCKED_APPROVAL / VERIFIED_BOUNDARY**：无新增事项。

修复以 OPEN_ACTIONABLE 为驱动。F9 只影响 Web，复用相关源码未变的 Core、Host、案例、包消费证据；没有因更新文档重跑这些门禁。外部设备缺口不触发本地自动修复。

最终状态：`IMPLEMENTATION_STATUS=PASS`、`TEST_STATUS=PASS`、`EXTERNAL_GATE_STATUS=BLOCKED_EXTERNAL`；本地任务完成并停止。这里的 PASS 限于本计划的本地验收，不代表设备发布认证。
