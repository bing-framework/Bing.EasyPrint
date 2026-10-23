# FNC1 修正、官方对照、真实码图预览与 PNG 导出验收

- 日期：2026-10-23
- 范围：A FNC1 分隔规则；B 官方 GS1 独立对照；C 真实码图预览；D PNG 导出
- 结论：本阶段验收通过；第一次集成复核发现的 R1/R2/R3 已修复并经主代理复核关闭，OPEN_ACTIONABLE=0。修复回归见第 2b 节，独立 HTTP 复核见第 2c 节。按用户要求不进行实机验收，改动未提交/未发布。
- 结果对象：`H:/Bing_Framework/Bing.EasyPrint`，起始 HEAD `89b117f31438507f64b395f8e31df3d01f766d31`；全部改动保持未提交

## 1. 能力结果

### A. FNC1
- `Gs1SyntaxAiDefinition.RequiresSeparator` 已改为 `!PredefinedLength`。
- `(01)12345678901231(7001)1234567890123(10)LOT` 输出 `011234567890123170011234567890123<GS>10LOT`。
- 固定但无 `*` 的 `7001`、`402`、`4307`、`4325`、`8111` 等后跟元素时插入 FNC1；最后元素不追加尾随 FNC1。
- 旧错误测试期望已修正；没有通过放宽生产规则保留旧断言。

### B. 官方对照
- 官方字典：`d1702ffb2fa5060f8460f7d581b01e377deaa1d6`。
- 官方 Engine：`9ae7fae0d43a731b6e22005d482f81482e092499`，`aitable.inc` 生成 536 项 FNC1 夹具。
- `Gs1SyntaxDictionaryTests` 共 **625/625** 通过，包含官方 FNC1 全表、linter/engine 向量、合法/非法字段、最后元素、多元素、未知 AI、跨码制可达输出及单元素兼容路径。
- 新增/更新测试：`Gs1SyntaxDictionaryTests`、`Fixtures/Gs1OfficialFnc1Flags.cs`、`Gs1Scan4TransportTests`、`EscPosNativeDataBarTests`、`EscPosRasterGs1DataBarExpandedTests`、`TsplUccEan128ProviderTests`、`ZplUccEan128ProviderTests`。

### C. 真实预览
- 新增 `src/Bing.EasyPrint/Printing/PrintRasterPreview.cs`，复用真实编码器、Composite 光栅、图片旋转和 ESC/POS Raster 画布。
- Host 新增 `/api/designer/element-png`、`/api/designer/page-png`、`raster-element-png`、`raster-page-png` 和 `rasterPreview` 能力。
- 前端按能力加载真实码图；旧 Host 保持占位并标注“示意”，禁用下载，不发起图片请求。
- 取消请求会中止旧图片请求并清理对象 URL；旧响应不能覆盖新结果。
- Host 测试用 ZXing 独立解码 Code128、EAN13、QR、DataMatrix、PDF417；Composite 使用外部向量核对自定义行数下的编码尺寸；普通 ExpandedStacked 自定义列数也验证 PNG 底部真实墨迹，防止漏传列/行参数。
- 元素 PNG 通过布局元素与 primitive 的 ownership 映射取齐全部绘制指令：矩形四边、椭圆分段、边框、空文本/空组和重复实例/分页不会按裸索引串位；`sourcePath` 不匹配仍拒绝。

### D. PNG
- 元素 PNG：`image/png`，包含静区、旋转、HRI 和图片内容。
- ESC/POS Raster：单页 PNG；多页 PNG ZIP（`page-001.png` 等）。
- PNG 写入实际 DPI 的 `pHYs`（203 DPI 读回 7992 pixels/metre）。
- 非 ESC/POS Raster 完整页 PNG 明确返回不支持；设备字体/固件专属完整页不生成假图。
- PNG 编码前预留 RGBA 像素预算；raster 快照、源图数据、RGBA、PNG 和 ZIP 在同时存活期间计入同一资源作用域，超限无部分输出；普通 `Render` 不再保留 Host 专用 primitive 快照。
- 非 ESC/POS Raster 的 1D 元素 PNG 不再宣称准确：CPCL 旋转/宽条、TSPL 显式宽条、Native HRI 返回 `UnsupportedCapability`，前端保持布局示意并给出原因；Composite/图片/矩阵/QR/形状/边框能力保留。

## 2. 首轮门禁（历史，已被 2b 集成修复结果替代）

| 门禁 | 结果 | 证据 |
| --- | --- | --- |
| Core 全量 `net6.0` | 4582/4582 通过 | `artifacts/protocol-runtime-verification/20261023-092305-6ca31410/net6.0.trx` |
| Core 全量 `netcoreapp3.1` | 4582/4582 通过；实际运行时 3.1.32 | 同目录 `netcoreapp3.1.trx`、`runtime-verification.json`（status PASS，.NET 6.0.36 / 3.1.32） |
| 官方字典定向 | 625/625 通过 | `artifacts/gs1-dictionary-fnc1-final.log` |
| Host 默认脚本 | 236/236 通过 | `artifacts/host-script-fnc1-preview-final2.log`、`artifacts/designer-host-verification-tests/designer-host-net8.trx` |
| Host PNG/ZXing/ExpandedStacked 定向 | 16/16 通过 | `artifacts/host-raster-preview-final2.log` |
| 前端生产构建 | 通过（`vue-tsc -b` + Vite） | `artifacts/web-build-fnc1-preview.log` |
| Vitest 全量 | 410/410 通过 | `artifacts/web-unit-fnc1-preview-final.log` |
| Chromium 全量 | 186/186 通过，0 失败，0 跳过，`workers=1`，`retries=0` | `artifacts/e2e-full-fnc1-preview-final2.log` |
| XML 五框架门禁 | 10/10 PASS | `artifacts/xml-documentation-verification/20261023-172241-455-209c6b7c/verification.json` |
| 示例 | 标准/小票 C#、JSON、往返一致 | `artifacts/fnc1-preview-png-20261023/samples`、`artifacts/samples-fnc1-preview.log` |
| 候选包 `net6.0` 消费 | PASS，实际 .NET 6.0.36 | `artifacts/fnc1-preview-png-20261023/package-consumer-20261023-170900` |
| Markdown 本地链接 | PASS | 新文档与更新文档的本地链接检查 |

补充说明：首次完整 Chromium 运行出现 2 项失败，原因是旧 E2E 断言仍查找 Konva `Text` 节点“GS1 Composite”，而新能力改为渲染真实 `Image` 节点。仅修正两处测试断言为检查 `Image` 节点后，定向 7/7 通过，最终完整 186/186 通过；生产代码未为此改变。

## 2b. 第一次集成 Review → Fix 结果

使用 Release Host 真实 HTTP 复核时发现 R1/R2/R3；复核原始证据在 `artifacts/png-integration-review-20261023/`。先加入失败回归，再做最小修复。首轮表保留为历史记录，不能替代本节。

| 项目 | 状态 | 修复与证据 |
| --- | --- | --- |
| R1 元素索引 | CLOSED | `RenderElements` 增加与布局元素平行的 ownership 快照，Host 按引用取齐同一布局元素的绘制指令；矩形、边框、空文本、重复实例及分页回归通过，条码 ZXing 解码 `12345`；`RasterPreviewHostTests` 24/24；Chromium 多元素回归通过 |
| R2 PNG 预算 | CLOSED | `Image<Rgba32>` 分配前预留 `width×height×4`；预览 raster 以租约存活到编码完成；普通 `Render` 不再捕获 primitive；1000×1000 + 1 MiB 返回 413 `ResourceLimitExceeded`；预算失败、预取消及租约释放回归通过；Core 预览租约测试 2/2 |
| R3 原生参数 | CLOSED | 非 ESC/POS Raster 的普通 1D 不再列入 `elementTypes` 或导出：CPCL 270/宽条、TSPL 显式宽条、Native HRI 返回 `UnsupportedCapability`，前端按钮禁用并显示原因；Composite/图片/矩阵/QR/形状/边框保留；UI 回归通过 |

集成修复后重验：

| 门禁 | 结果 | 证据 |
| --- | --- | --- |
| Core `net6.0` / `netcoreapp3.1` | 各 4584/4584；运行时 PASS（6.0.36 / 3.1.32） | `artifacts/protocol-runtime-verification/20261023-102407-b8b0ad14/runtime-verification.json` |
| Host 默认脚本 | 244/244 | `artifacts/host-script-integration-review.log`、`artifacts/designer-host-verification-tests/designer-host-net8.trx` |
| RasterPreviewHostTests | 24/24 | `artifacts/host-raster-preview-integration-review.log` |
| 前端生产构建 | PASS | `artifacts/web-build-integration-review-final2.log` |
| Vitest | 410/410 | `artifacts/web-unit-integration-review-final2.log` |
| Chromium 全量 | 188/188，0 失败、0 跳过；项目配置未设置重试 | `artifacts/e2e-full-integration-review-final.log` |
| XML 五框架 | 10/10 PASS | `artifacts/xml-documentation-verification/20261023-182337-654-f5ecc299/verification.json` |
| 示例 | PASS | `artifacts/integration-review-20261023/samples` |
| 候选包 `net6.0` 消费 | PASS，.NET 6.0.36 | `artifacts/integration-review-20261023/package-consumer-20261023-184203` |

说明：本轮首次完整 E2E 记录 1 项下载事件等待超时、187 项通过；测试增加协议切换后的真实 `/preview` 响应等待，最终运行 188/188。日志未单独证明超时根因，也未记录完整启动参数，不将配置默认值表述为日志实测参数。FNC1/字典证据未变化，继续沿用 625/625 结果。

## 2c. 主代理最终复核

OpenCode / deepseek-v4.1-flash 负责代码修复；主代理复核变更与真实 HTTP 行为，独立 luna_worker 只读核对第 2b 节的日志、JSON、TRX 和候选包消费产物。未重跑未受影响的全量门禁。

- 影响范围：本次仅编译当前 Host 及其依赖，重放 R1/R2/R3 的输入；不改源码、公共 API、框架、依赖或包版本。随后只更新计划及本报告。
- 构建：`dotnet build tools/DesignerHost/DesignerHost.csproj --no-restore --configuration Release --output artifacts/png-final-review-20261023/bin -m:1 /p:BuildInParallel=false /p:GenerateDocumentationFile=false`。0 错误，5 项既有依赖告警；关闭此独立构建的文档生成，避免覆盖跟踪 XML；严格 XML 结果复用第 2b 节。
- 实测：以独立输出的 Host 接收原始 HTTP 请求；结果写入 [http-results.json](../artifacts/png-final-review-20261023/http-results.json)，构建日志写入 [build.log](../artifacts/png-final-review-20261023/build.log)。

| 原复现输入 | 当前实测 | 结论 |
| --- | --- | --- |
| 矩形 40×30，后接条码，选择元素 0 | 200；136 个黑点，墨迹边界 `[0,0,39,29]`，中心为白色 | 四边完整，原来只有首边的错误消除 |
| 同模板选择元素 1 | 200；198×40，3680 个黑点；Host 回归另有 ZXing 解码断言 | 不再错取矩形绘制指令 |
| CPCL Code128 旋转 270 度 | 400 `UnsupportedCapability` | 明确表达能力边界，不再返回错误码图或画布越界错误 |
| 1000×1000 页面，工作预算 1 MiB | 413 `ResourceLimitExceeded`；资源 `Host.PngRgba`，申请 4,000,000 字节 | 在 RGBA 分配前拒绝，响应为结构化 JSON |

验收依据为各受影响范围的构建、测试与上述独立复核。历史门禁日志没有统一的源码快照标识，因此不将其描述为同一个冻结候选的不可变流水线记录；候选包消费 JSON 单独记录并核对包哈希。

当前分类：`CLOSED` 为 A/B/C/D 的本阶段实现及 R1/R2/R3；`OPEN_ACTIONABLE=0`；`BLOCKED_APPROVAL=0`；`BLOCKED_EXTERNAL` 的实机证据按用户要求不阻塞本阶段；`ACCEPTED_LIMITATION` 包括非 Raster 整页、普通原生 1D/文字/HRI 软件码图及 Composite 独立扫码验证；`VERIFIED_BOUNDARY` 为上述 1 MiB 图片预算拒绝；`DEFERRED` 为完整 GS1 req/ex 业务关系与图片依赖大版本迁移；`NOT_APPLICABLE` 为本阶段新 PNG 能力的历史等价性能对比。无进展检查为 CHANGED，下一动作 STOP，本阶段状态 COMPLETED。

## 3. 变更影响

- 不新增公共 API、公共签名、NuGet/模板包版本或持久化格式。
- 旧 `/preview` 几何 JSON、`/render` 协议字节和旧客户端保持兼容。
- A 是有意行为修正，会改变此前固定但非预定义 AI 漏 FNC1 的错误输出；不提供旧错误行为开关。
- 新特性缺失时设计器降级为几何占位；非 ESC/POS Raster 页面 PNG 明确不可用。
- `sourcePath` 仅匹配预览元素描述，不打开任意文件；新路由沿用 Host 准入、预算和取消语义。

## 4. 已知限制

- 软件位图不等于实机打印；未连接目标打印机，未做物理 DPI、纸宽、走纸、裁切、HRI 字形或扫码验收。
- 完整页面 PNG 仅 ESC/POS Raster；其他协议只提供声明范围内的元素 PNG，普通 1D、原生文字和 Native HRI 不提供，避免用 Raster 近似值冒充设备像素。
- 设备字体文本和固件专属页面不承诺像素一致；相关页面导出会明确失败。
- 资源预算覆盖 raster 快照和 RGBA；但该预算是逻辑工作内存估计，不是进程物理内存上限。
- Composite 没有 ZXing 独立解码（其 CC-A/B/C 不适合该解码器），使用官方外部向量核对编码矩阵与尺寸；这不等于实机扫码通过。
- GS1 字典的全部 `req`/`ex` 跨 AI 业务关系仍未实现；本阶段只覆盖语法、Linter 和既有明确业务规则。
- `npm run build` 仍有单个 chunk 超过 500 kB 的既有构建提示；未在本阶段做代码分割。
- 构建仍有 SixLabors.ImageSharp 2.1.11 的已知漏洞告警和 net6.0 EOL 告警；本阶段未升级依赖。

## 5. 相关文档

- [能力与接口说明](fnc1-preview-png.md)
- [AI 字典边界](expanded-stacked-rows-and-gs1-ai.md)
- [设计器 Host](designer-host.md)
- [设计器交互](designer.md)
- [开发与验证](development.md)
- 计划与影响分析：`ai_docs/plans/fnc1-preview-png-2026-10-23.md`
