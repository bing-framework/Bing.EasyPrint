# FNC1 修正、官方对照、真实码图预览与 PNG 导出

- 日期：2026-10-23
- 状态：首轮实现 + 第一次集成 Review→Fix 完成；R1/R2/R3 已修复并重验，实机验收未做
- 范围：A FNC1 分隔规则；B 官方 GS1 独立对照；C 真实码图预览；D PNG 导出
- 非目标：提交、推送、发布、改版本/API baseline、实机打印与扫描验收

## 工作清单

### A. 修正 FNC1 分隔规则
- [x] `Gs1SyntaxAiDefinition.RequiresSeparator` 改为 `!PredefinedLength`
- [x] 更新固定非 `*` AI、可变 AI、最后元素无分隔测试
- [x] 修正 ESC/POS Raster/Native、TSPL、ZPL、Composite 旧期望
- [x] 验证 `(01)...(7001)...(10)LOT` 插入 ASCII 29

### B. 官方 GS1 独立对照
- [x] 固定字典 `d1702ffb...` / Engine `9ae7fae0...`
- [x] 从 `aitable.inc` 生成 536 项 FNC1 夹具
- [x] 官方 linter 向量与 Engine JavaScript 规范化向量
- [x] 合法/非法字段、最后元素、多元素、未知 AI
- [x] GS1-128/Expanded/Composite 可达输出回归
- [x] 单元素括号兼容路径独立记录
- [x] ThirdPartyNotices 与来源/更新文档

### C. 真实码图预览
- [x] 复用真实编码/光栅路径的 `PrintRasterPreview`
- [x] Host `raster-element-png` / `raster-page-png` 特性
- [x] `rasterPreview` 能力对象与不支持原因
- [x] 元素 PNG 路由、索引/来源路径校验、错误语义
- [x] 前端能力协商、旧 Host 降级、取消与 URL 清理
- [x] Konva 真实 PNG 渲染、占位“示意”
- [x] Host ZXing 独立解码 Code128/EAN13/QR
- [x] Composite 外部向量尺寸/行数一致性

### D. PNG 导出
- [x] 元素 PNG `image/png`
- [x] ESC/POS Raster 单页 PNG、多页 ZIP
- [x] DPI `pHYs`
- [x] 非 ESC/POS 完整页明确拒绝
- [x] 资源预算、取消、失败清理
- [x] 浏览器下载与保存导入 E2E

### 文档与验收
- [x] 新能力指南与状态/README/设计器文档
- [x] 历史错误 FNC1 结论的替代说明
- [x] 新阶段验收报告
- [x] Core 全量 net6/netcoreapp3.1
- [x] Host 全量
- [x] Vitest 全量
- [x] Chromium 全量零重试
- [x] XML 五框架门禁
- [x] samples、候选包 net6 消费、静态/链接/跟踪 XML 检查

## Change Impact Analysis

### 公共 API 与序列化
- 不新增公共类型、公共成员或公共签名；`PrintRasterPreview`、`TemplatePage.RenderElements` 为 internal。
- Host JSON 仅新增可选请求字段 `pageIndex`、`elementIndex`、`sourcePath` 和响应能力字段 `rasterPreview`；旧请求/旧响应解析保持兼容。
- 模板 JSON、项目文件、模板包和 NuGet 包版本不变；不新增持久化状态。

### 默认输出与兼容性
- 普通打印、旧 `/preview` 几何 JSON、旧 `/render` 协议字节不变。
- A 是行为修正：没有 `*` 的固定长度 AI 后跟元素时新增 FNC1。该变化会改变此前错误的 GS1-128/DataBar/Composite 输出，属于有意修复，不提供旧错误行为开关。
- 旧 Host 缺少 `raster-element-png` / `raster-page-png` 或 `rasterPreview` 时，设计器保持几何占位、禁用下载且不发起图片请求；不静默降级为假码图。
- 非 ESC/POS Raster 的完整页面 PNG 明确返回不支持；元素 PNG 只承诺真实编码器可准确生成的类型。

### 安全与资源
- 新路由沿用现有 Host 准入、请求体上限、结构化错误和资源作用域；`sourcePath` 只用于匹配预览元素描述，不打开任意文件。
- PNG 编码复用现有图片资源解码/校验路径，不新增外部 URL 获取。
- 画布和派生 PNG 计入 Host 预算；取消请求会中止旧图片请求并清理对象 URL。
- 多页 PNG ZIP 可能产生较大内存/带宽峰值，受现有输出预算约束；超限返回结构化错误。

### 并发与一致性
- 不引入共享可变编码状态；同源编码器按请求创建。
- 前端以请求序号/AbortController 丢弃旧响应，旧能力或旧元素不会覆盖新结果。
- 页面 PNG 与 `/render` 使用同一 `TemplatePage.RenderElements` 快照，避免重新绑定数据造成不一致。

### 可观测性与回滚
- 沿用现有错误码、阶段和字段路径；新增不支持原因使用稳定字符串。
- 回滚可移除新特性声明和 UI 入口，旧预览/渲染继续工作；A 的 FNC1 修复需连同测试和文档整体回退，不能单独恢复旧规则。

## 第一次集成 Review → Fix（2026-10-23）

- 状态：R1/R2/R3 已修复并通过受影响门禁；OPEN_ACTIONABLE=0；实机验收仍未做
- 复核方式：使用刚生成的 Release Host 进行真实 HTTP 复现；不新增功能阶段
- 复核证据：`artifacts/png-integration-review-20261023/`

### R1 [P1] 元素布局索引与绘制指令索引不对应
- 状态：CLOSED（主代理独立复核通过，见验收报告第 2c 节）
- 触发：矩形实际生成多条 `PrintLineElement`，椭圆生成多段；Host 直接用 `RenderElements[elementIndex]`，条码索引会取到矩形第二条边。
- 根因：`RenderElements` 只是 primitive 平坦列表，没有把布局元素映射到其全部 primitive；不能按裸索引或仅 `SourcePath` 关联重复实例/组件。
- 验收：矩形四边、椭圆完整，后续条码可解码；组/边框、空文本、重复实例、分页不偏移；`sourcePath` 不匹配仍拒绝；增加真实 Host 多元素和浏览器回归。

### R2 [P1] PNG 内存预算与对象生命周期不完整
- 状态：CLOSED（主代理独立复核通过，见验收报告第 2c 节）
- 触发：1000×1000 page-png 在 1 MiB 请求预算下仍返回 200；RGBA 4,000,000 字节未预留。
- 根因：`new Image<Rgba32>` 未预留像素内存；`PrintRasterPreview` 的临时记账在返回后释放，但返回的 raster 仍在 PNG 编码期间存活；普通 `Render` 也无条件复制并保留 primitive/图片引用。
- 验收：分配前做溢出安全预算；源位图、快照、PNG/ZIP 同时存活期间正确计账；超限结构化失败且无部分输出；成功/失败/取消释放；加入 1 MiB/1000² 和取消清理回归。

### R3 [P1] 原生协议参数被直接当作 Raster 参数
- 状态：CLOSED（主代理独立复核通过，见验收报告第 2c 节）
- 触发：CPCL 270 的 `ElementPng` 返回 `InvalidOptions`；TSPL 显式宽条被忽略；Native HRI 语义未约束。
- 根因：`PrintRasterPreview` 直接使用 `NarrowWidth/Rotation`，忽略 `WideWidth/CpclRatioCode` 和 CPCL 模板旋转归一化；`ElementPngSupported` 与 `rasterPreview.elementTypes` 只按 Type 宣称支持。
- 验收：可准确转换的参数正确归一化；不能准确生成的组合明确 `UnsupportedCapability` 并让 UI 显示原因/布局示意；不新增编解码器或模拟固件；保留 Composite/图片/矩阵等已支持元素；补 CPCL 270/宽度、TSPL 显式宽条、Native HRI 场景。

### 本轮门禁规则
- 先增加能复现上述问题的定向回归，再实现最小修复。
- 只重验变化的共享代码/Host/前端/打包路径；复用未变化的 FNC1/字典证据，旧日志和旧结果保留，新结果单列。
- 修正报告中“资源预算已覆盖”和“元素均准确”的过度结论。


### 集成修复结果
- R1：`RenderElements` 与 `RenderElementOwnership` 平行捕获，Host 按布局元素引用取齐全部 primitive；矩形/边框/空文本/重复实例/分页及真实 Host 浏览器回归通过。
- R2：PNG RGBA 与 raster 快照租约进入现有预算；1 MiB/1000² 返回 413 且无部分输出；取消/成功/失败清理和 Core 租约测试通过。
- R3：非 ESC/POS Raster 的 1D 不列入能力且返回 `UnsupportedCapability`；CPCL 270、TSPL 显式宽条、Native HRI 回归通过，Composite/图片/矩阵/QR/形状/边框保留。
- 重验：Core 4584/4584（两个运行时）、Host 244/244、RasterPreviewHostTests 24/24、Vitest 410/410、Chromium 188/188、XML 10/10、samples/package consumer PASS。
- 证据：`artifacts/protocol-runtime-verification/20261023-102407-b8b0ad14/`、`artifacts/host-script-integration-review.log`、`artifacts/host-raster-preview-integration-review.log`、`artifacts/e2e-full-integration-review-final.log`、`artifacts/xml-documentation-verification/20261023-182337-654-f5ecc299/`、`artifacts/integration-review-20261023/`。
- 主代理独立复核：当前源码独立构建与四项 HTTP 检查通过，结果在 `artifacts/png-final-review-20261023/http-results.json`；R1/R2/R3 均 CLOSED，本阶段 OPEN_ACTIONABLE=0，COMPLETED，停止自动修复。
- 保留边界：非 ESC/POS Raster 的普通 1D/文字/HRI 和整页 PNG 不提供；完整 GS1 req/ex 与图片依赖大版本迁移 DEFERRED；实机验收按用户要求不执行。历史日志未记录统一源码快照，报告按验证范围列证据，不冒称统一冻结候选。
