# 多协议 TODO 当前执行进度

## R153 本轮：多协议反白文字与 CPCL Helper 边界

更新时间：2026-10-13。本轮按已确认的“多协议打印能力与旋转链路完善”计划推进现有能力；GS1 Composite 尚未开始编码，不能视为计划完成。

### 已完成

- [x] ESC/POS Raster 新增反白文字重载：以实际文字布局外框绘制黑底、白字；背景、粗体和下划线整体支持 0/90/180/270 度旋转；空字符串不绘制背景。
- [x] TSPL/ZPL 仅在 `AllowRasterizedTextStyles` 开启且图片能力可用时光栅化反白；默认仍使用设备内置字体。Provider、模板度量、静态预检、Host 能力响应及设计器选项同步该能力。
- [x] 更新旧 ESC/POS Raster 模板反白测试，覆盖真实位图输出；新增反白换行、旋转整块分页、标签协议 opt-in 及失败边界回归。
- [x] CPCL Helper 保持既有支持类型的命令标记；有效但未映射的枚举统一抛出 `NotSupportedException`，无效枚举抛出参数异常。

### 未完成 / OPEN_ACTIONABLE

- [ ] GS1 Composite 尚未实现：缺少 `Gs1CompositeMode`、独立公共元素、CC-A/CC-B 编码矩阵、链接型 DataBar/分隔图案、四协议图片输出、模板 Type=11、Host 能力协商和设计器编辑/往返。现有 ZXing 依赖不提供 MicroPDF417；已核查的纯托管候选库要么只支持 GS1-128 线性组件，要么仅面向 .NET 8，均不能满足 DataBar Omnidirectional/Limited × CC-A/CC-B 和本库五个声明 TFM 的契约。因此本轮没有暴露不完整的 Composite API 或能力标志。下一步仍需完成可在现有 TFM 上运行的托管编码器，并以固定 Zint 2.15.0 四组独立矩阵作逐模块验证。
- [ ] Composite 输入 AI/GTIN 校验、容量边界、旋转/静区/预算/取消、四 Provider 矩阵一致性、V2/V3 模板绑定与原子分页、Host 协商和设计器往返均未实现。

### 验证

- Core/Templates 完整测试：`net6.0` 3258/3258；`netcoreapp3.1` 3258/3258。
- Core 与 Templates 五目标框架构建：`net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，均 0 警告、0 错误。
- DesignerHost `net8.0`：122/122；前端 Vitest：250/250；前端类型检查与生产构建通过。
- 设计器反白 opt-in 定向 E2E：Playwright 正常退出，1/1 通过。
- NuGet 漏洞数据源不可访问产生 NU1900；未修改项目、依赖或 CI 配置。
- 真实设备校准/打印验收按要求暂缓。Host 进程隔离、监听策略和正式性能阈值不在本轮范围。

---

Task：多指令集打印剩余 TODO。Round：R152 GS1 AI 253/254/255 有界扩展。更新时间：2026-10-09。

## R152 本轮：GS1 AI 253/254/255 有界扩展

按 GS1 General Specifications 的字段定义，扩展共享括号 AI 解析器支持 GDTI（253）、GLN 扩展（254）和 GCN（255）。严格检查键的 GS1 校验位、可选组件长度/字符集、AI 254 与物理位置 GLN（414）的配对及变量字段 FNC1；单个新 AI 也会进入严格解析。Native ESC/POS、Raster DataBar Expanded、TSPL 和 ZPL 共用该规则，Raster Expanded 继续以原括号输入显示 HRI。本轮只完成这三个 AI，不代表完整 GS1 AI 字典、复合码或所有业务关系。

### 已完成 TODO

- [x] 将 GS1 AI 支持扩展范围限定到 253、254、255，保持未知 AI 严格拒绝。
- [x] 校验 253/255 的 13 位 GS1 标识校验位、可选序列组件上限及字符集；校验 254 长度和 AI 414 GLN 校验位/配对。
- [x] 验证变量字段的 FNC1 边界，以及单个新 AI 的严格验证路径。
- [x] 验证 ESC/POS Native、Raster DataBar Expanded、TSPL、ZPL 的共用编码和括号 HRI 保留行为。

### 本轮文件

- `src/Bing.EasyPrint/Protocols/Gs1ApplicationIdentifierParser.cs`
- `tests/Bing.EasyPrint.Tests/Gs1Scan4TransportTests.cs`
- `tests/Bing.EasyPrint.Tests/EscPosRasterGs1DataBarExpandedTests.cs`
- `docs/protocols.md`
- `ai_docs/plans/multi-protocol-todo-progress.md`

### 累计已完成 TODO

- [x] R1-R143：多协议 API、Provider、模板、条码、受限 Raw、布局/校准、资源限制及状态共享；逐轮实现与证据见历史记录。
- [x] R144：TSPL BITMAP 二进制线上载荷与受限 Raw 字符串契约；结构化 HEX 输入解码输出。
- [x] R145：ZPL 格式闭合和结构化续写生命周期。
- [x] R146：ESC/POS Raw 行游标几何及原子失败恢复。
- [x] R147：ZPL Raw 裁切模式、Kiosk 配对校验和输出分段状态。
- [x] R148：CPCL 完整 Raw 追加拒绝、即时失败及实例重置恢复。
- [x] R149：CPCL/TSPL 受限 Raw 行分隔及拒绝原子性。
- [x] R150：四协议受限 Raw tokenizer 临时工作集预算及失败原子性。
- [x] R151：修正 TSPL/ZPL QR allowlist 能力测试的 JSON 请求格式。
- [x] R152：有界支持 GS1 AI 253/254/255，并验证跨 Provider 编码、校验位、长度、配对和 HRI。

### 未完成 TODO（沿用原优先级）

1. [ ] GS1 DataBar Composite 与链接字段。状态：`BLOCKED_APPROVAL`；依赖：R152 已定义的 GS1 AI 字段解析；阻塞：尚未选定 Composite 子型、字段映射与验收向量。
2. [ ] 具体 TSPL/ZPL 机型 glyph advance、组合字形、字体选择和墨迹范围校准。状态：`BLOCKED_EXTERNAL`；依赖：R139 校准 API/布局；阻塞：缺少设备、手册样本或实测数据。
3. [ ] 设备容量约束。状态：`BLOCKED_EXTERNAL`；依赖：目标机型、DPI、固件与介质资料；阻塞：真实可打印边界和符号容量未知。
4. [ ] DesignerHost 进程隔离和监听边界。状态：`BLOCKED_APPROVAL`；依赖：部署信任边界、允许监听地址与隔离验收契约；阻塞：尚未批准。
5. [ ] 正式性能、吞吐、容量和超时门槛。状态：`BLOCKED_APPROVAL`；依赖：批准的负载模型与阈值；阻塞：无正式性能指标。
6. [ ] 跨平台运行时/字库验证。状态：`BLOCKED_EXTERNAL`；依赖：目标 OS、运行时和字体环境；阻塞：缺少相应环境执行证据。
7. [ ] 具体机型 HRI 四边、字形墨迹与旋转方向实测。状态：`BLOCKED_EXTERNAL`；依赖：R138 角度边界配置；阻塞：缺少目标设备与样本。

**R152 验证状态**

- L1：GS1 AI/Scan4Transport、Native DataBar、Raster DataBar Expanded 相关测试，`net6.0`，189/189 通过。
- Core 项目 `net7.0`、`net6.0`、`netstandard2.0`、`netstandard2.1`、`netcoreapp3.1` 五目标框架构建成功，0 错误。
- 本机未安装 .NET 7 运行时，故 `net7.0` 测试无法启动；改用已安装的 `net6.0` 完成测试。
- NU1900：NuGet 漏洞索引不可达；不影响本轮构建和测试。
- 本轮未修改 workflow、job、matrix、依赖或基线。

### R152 下一步

下一项按原优先级为 GS1 DataBar Composite 与链接字段；当前受阻于未确认的 Composite 子型、输入字段映射和验收向量。待该契约获批后再编码，不能据此把 R152 的三个 AI 扩展成完整 GS1 或 Composite 支持。

Goal Status 保持 active。

---

Task：多指令集打印剩余 TODO。Round：R151 DesignerHost 标签二维码旋转 allowlist 测试。更新时间：2026-10-09。

## R151 本轮：DesignerHost 标签二维码旋转 allowlist 测试

定位 TSPL/ZPL 两个预期返回 200、实际返回 400 的能力端点用例：动态拼接 profile 属性名时遗漏了 JSON key 的结束引号，请求未通过 JSON 解析，能力映射没有执行。补回引号后，两种协议均返回能力数组 `Rotate90`、`Rotate270`。

### 已完成 TODO

- [x] 修正 TSPL/ZPL 标签二维码旋转 allowlist 测试请求的 JSON 属性名拼接。
- [x] 两个协议用例均验证 Capabilities 返回 200，并保留请求指定的旋转白名单顺序和值。

### 本轮文件

- `tests/DesignerHost.Tests/HostContractTests.cs`
- `ai_docs/plans/multi-protocol-todo-progress.md`

### 累计已完成 TODO

- [x] R1-R143：多协议 API、Provider、模板、条码、受限 Raw、布局/校准、资源限制及状态共享；逐轮实现与证据见历史记录。
- [x] R144：TSPL BITMAP 二进制线上载荷与受限 Raw 字符串契约；结构化 HEX 输入解码输出。
- [x] R145：ZPL 格式闭合和结构化续写生命周期。
- [x] R146：ESC/POS Raw 行游标几何及原子失败恢复。
- [x] R147：ZPL Raw 裁切模式、Kiosk 配对校验和输出分段状态。
- [x] R148：CPCL 完整 Raw 追加拒绝、即时失败及实例重置恢复。
- [x] R149：CPCL/TSPL 受限 Raw 行分隔及拒绝原子性。
- [x] R150：四协议受限 Raw tokenizer 临时工作集预算及失败原子性。
- [x] R151：修正 TSPL/ZPL QR allowlist 能力测试的 JSON 请求格式。

### 未完成 TODO（沿用原优先级）

1. [ ] 扩展 GS1 AI 解析范围。状态：`OPEN_ACTIONABLE`；依赖：明确 AI 家族、长度/校验与展示输入约定；阻塞：不能从“GS1 AI”推定支持全部标准 AI。
2. [ ] GS1 DataBar Composite 与链接字段。状态：`BLOCKED_APPROVAL`；依赖：第 1 项 GS1 AI 输入契约；阻塞：未选定 Composite 子型、字段映射与验收向量。
3. [ ] 具体 TSPL/ZPL 机型 glyph advance、组合字形、字体选择和墨迹范围校准。状态：`BLOCKED_EXTERNAL`；依赖：R139 校准 API/布局；阻塞：缺少设备、手册样本或实测数据。
4. [ ] 设备容量约束。状态：`BLOCKED_EXTERNAL`；依赖：目标机型、DPI、固件与介质资料；阻塞：真实可打印边界和符号容量未知。
5. [ ] DesignerHost 进程隔离和监听边界。状态：`BLOCKED_APPROVAL`；依赖：部署信任边界、允许监听地址与隔离验收契约；阻塞：尚未批准。
6. [ ] 正式性能、吞吐、容量和超时门槛。状态：`BLOCKED_APPROVAL`；依赖：批准的负载模型与阈值；阻塞：无正式性能指标。
7. [ ] 跨平台运行时/字库验证。状态：`BLOCKED_EXTERNAL`；依赖：目标 OS、运行时和字体环境；阻塞：缺少相应环境执行证据。
8. [ ] 具体机型 HRI 四边、字形墨迹与旋转方向实测。状态：`BLOCKED_EXTERNAL`；依赖：R138 角度边界配置；阻塞：缺少目标设备与样本。

**R151 验证状态**

- L1：`Label_profile_qr_rotation_allowlist_is_forwarded_to_host_capabilities`，`net8.0`，2/2 通过。
- 测试命令额外设置 `EnableDefaultContentItems=false`，以避开工作区已有 `tools/DesignerHost/artifacts` 被 Web SDK 内容 glob 递归复制；未改项目或 CI 配置。
- 首次默认构建在测试启动前因上述递归 artifacts 路径报 MSB3030；使用命令行属性后，DesignerHost 与测试项目编译成功并执行了两项用例。
- NU1900：NuGet 漏洞索引不可达；不影响测试结果。
- 本轮未修改 workflow、job、matrix、依赖或基线。

### R151 下一步

按原优先级处理 GS1 AI 解析范围：先审查当前 GS1 AI 入口和已支持解析器，确定可由仓库现有语义及测试向量明确覆盖的 AI 子集，再补齐长度、校验与 HRI 展示测试；不扩大为未定义的全量 AI 支持。

Goal Status 保持 active。

---

Task：多指令集打印剩余 TODO。Round：R146 ESC/POS Raw 行游标几何。更新时间：2026-10-09。

## R146 本轮：ESC/POS Raw 行游标几何

修正 Native 行宽估算在对齐命令处被清零的问题，并在受限 Raw 的水平制表符推进后立即验证打印区域边界。解析仍在候选命令状态中进行，越界批次不会追加输出或污染后续状态。

### 已完成 TODO

- [x] `ESC a` 对齐设置保留当前行已累计宽度，不能通过切换对齐绕过宽度预检。
- [x] Raw 水平制表符推进后立即校验横向范围；越界不能被后续换行重置状态掩盖。
- [x] 新增对齐宽度绕过、Tab 越界、失败输出原子性及后续合法调用恢复测试。

### 本轮文件

- `src/Bing.EasyPrint/Protocols/EscPosPrintCommand.cs`
- `src/Bing.EasyPrint/Protocols/EscPosPrintCommand.RestrictedRaw.cs`
- `tests/Bing.EasyPrint.Tests/ProtocolRestrictedRawTests.cs`
- `docs/protocols.md`
- `ai_docs/plans/multi-protocol-todo-progress.md`

### 累计已完成 TODO

- [x] R1-R143：多协议 API、Provider、模板、条码、受限 Raw、布局/校准、资源限制及状态共享；逐轮实现与证据见本账本历史。
- [x] R144：TSPL BITMAP 二进制线上载荷与受限 Raw 字符串契约；结构化 HEX 输入解码输出。
- [x] R145：ZPL 格式闭合和结构化续写生命周期。
- [x] R146：ESC/POS Raw 行游标几何及原子失败恢复。

### 未完成 TODO（沿用原优先级）

1. [ ] ZPL Raw 裁切模式与输出分段状态。状态：`OPEN_ACTIONABLE`；依赖：Profile 对全切、半切、Kiosk、Delayed 合法组合的定义；阻塞：Kiosk 比较对象和 Delayed 状态更新仍有缺口。
2. [ ] CPCL 完整 Raw 程序后的追加与失败恢复。状态：`OPEN_ACTIONABLE`；依赖：确定完整程序重复写入的追加语义；阻塞：结构化追加部分延迟到 Build 才失败，重复完整程序可能覆盖已存文本。
3. [ ] CPCL/TSPL 连续受限 Raw 调用的命令分隔。状态：`OPEN_ACTIONABLE`；依赖：确定末行无换行的 Raw 是否有效；阻塞：解析接受无换行末行，后续追加可能与末行拼接。
4. [ ] 四协议 tokenizer 临时内存预算。状态：`OPEN_ACTIONABLE`；依赖：定义解析器集合/字符串副本的预算计量；阻塞：部分临时分配未预留资源预算。
5. [ ] DesignerHost TSPL/ZPL 二维码旋转 allowlist 测试失败。状态：`OPEN_ACTIONABLE`；依赖：复现当前结果；阻塞：已有记录为两项预期 200、实际 400，尚无后续关闭证据。
6. [ ] 扩展 GS1 AI 解析范围。状态：`OPEN_ACTIONABLE`；依赖：明确 AI 家族、长度/校验与展示输入约定；阻塞：不能从“GS1 AI”推定支持全部标准 AI。
7. [ ] GS1 DataBar Composite 与链接字段。状态：`BLOCKED_APPROVAL`；依赖：GS1 AI 输入契约；阻塞：未选定 Composite 子型、字段映射与验收向量。
8. [ ] 具体 TSPL/ZPL 机型 glyph advance、组合字形、字体选择和墨迹范围校准。状态：`BLOCKED_EXTERNAL`；依赖：R139 校准 API/布局；阻塞：缺少设备、手册样本或实测数据。
9. [ ] 设备容量约束。状态：`BLOCKED_EXTERNAL`；依赖：目标机型、DPI、固件与介质资料；阻塞：真实可打印边界和符号容量未知。
10. [ ] DesignerHost 进程隔离和监听边界。状态：`BLOCKED_APPROVAL`；依赖：部署信任边界、允许监听地址与隔离验收契约；阻塞：尚未批准。
11. [ ] 正式性能、吞吐、容量和超时门槛。状态：`BLOCKED_APPROVAL`；依赖：批准的负载模型与阈值；阻塞：无正式性能指标。
12. [ ] 跨平台运行时/字库验证。状态：`BLOCKED_EXTERNAL`；依赖：目标 OS、运行时和字体环境；阻塞：缺少相应环境执行证据。
13. [ ] 具体机型 HRI 四边、字形墨迹与旋转方向实测。状态：`BLOCKED_EXTERNAL`；依赖：R138 角度边界配置；阻塞：缺少目标设备与样本。

**R146 验证状态**

- L1：`ProtocolRestrictedRawTests` `net6.0` `26/26` 通过。
- L2：Core.Tests 完整 `net6.0` `3189/3189` 通过。
- L0：Core 与 Templates 五目标框架串行构建通过：`net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 错误。
- NU1900：无法访问 NuGet 漏洞索引；不影响编译和测试结果。
- 本轮未修改 workflow、job、matrix、依赖或基线。
- L5：未进行真实打印机验收。

### R146 下一步

按原优先级建议处理 ZPL Raw 裁切模式与输出分段状态。该项已有 Profile 和 `BuildOutput` 分段基础，可在本地补齐 Kiosk/Delayed 状态转换与 Raw/结构化互通测试。

Goal Status 保持 active。

---

Task：多指令集打印剩余 TODO。Round：R145 ZPL 格式生命周期收口。更新时间：2026-10-09。

## R145 本轮：ZPL 格式闭合与结构化续写

统一 ZPL 格式的打开、闭合和构建状态。受限 Raw 仍可开启格式后跨调用续写，允许结构化字段与结构化 `End()` 接续；但重复 `Start()`、未匹配的 `End()`、以及格式仍打开时的 `Build()`/`BuildOutput()` 均失败。失败不会追加字节或改变格式状态。

### 已完成 TODO

- [x] ZPL Raw 自开格式与结构化续写共享同一生命周期；完整 Raw 格式仍可直接构建，跨调用打开的格式必须闭合后才能输出。
- [x] `Start()`/`End()` 对受控与非受控格式执行一致的配对校验；构建未闭合格式失败，调用 `End()` 后可恢复构建。
- [x] 新增未闭合 Raw 构建恢复、重复开始、未匹配结束及重复构建回归测试；更新协议说明。

### 本轮文件

- `src/Bing.EasyPrint/Protocols/ZplPrintCommand.cs`
- `tests/Bing.EasyPrint.Tests/ProtocolRestrictedRawTests.cs`
- `tests/Bing.EasyPrint.Tests/ZplMediaOptionsTests.cs`
- `tests/Bing.EasyPrint.Tests/ZplUccEan128ProviderTests.cs`
- `tests/Bing.EasyPrint.Tests/LabelPrinterProfileTests.cs`
- `tests/Bing.EasyPrint.Tests/ProtocolStrictEncodingTests.cs`
- `tests/Bing.EasyPrint.Tests/ProtocolPrintCommandTests.cs`
- `docs/protocols.md`
- `ai_docs/plans/multi-protocol-todo-progress.md`

### 累计已完成 TODO

- [x] R1-R143：多协议 API、Provider、模板、条码、Raw 校验、布局/校准、资源限制和状态共享；分轮细节见本账本历史。
- [x] R144：TSPL BITMAP 二进制线上载荷与受限 Raw 字符串契约；结构化 HEX 输入解码输出。
- [x] R145：ZPL 格式闭合和结构化续写生命周期；详见本节。

### 未完成 TODO（沿用原优先级）

1. [ ] ESC/POS Raw 行游标几何。状态：`OPEN_ACTIONABLE`；依赖：无；阻塞：对齐命令可能清零已有行宽，制表位前进后未立即检查横向边界。
2. [ ] ZPL Raw 裁切模式与输出分段状态。状态：`OPEN_ACTIONABLE`；依赖：Profile 对全切、半切、Kiosk、Delayed 合法组合的定义；阻塞：Kiosk 比较对象和 Delayed 状态更新仍有缺口。
3. [ ] CPCL 完整 Raw 程序后的追加与失败恢复。状态：`OPEN_ACTIONABLE`；依赖：确定完整程序重复写入的追加语义；阻塞：结构化追加部分延迟到 Build 才失败，重复完整程序可能覆盖已存文本。
4. [ ] CPCL/TSPL 连续受限 Raw 调用的命令分隔。状态：`OPEN_ACTIONABLE`；依赖：确定末行无换行的 Raw 是否有效；阻塞：解析接受无换行末行，后续追加可能与末行拼接。
5. [ ] 四协议 tokenizer 临时内存预算。状态：`OPEN_ACTIONABLE`；依赖：定义解析器集合/字符串副本的预算计量；阻塞：部分临时分配未预留资源预算。
6. [ ] DesignerHost TSPL/ZPL 二维码旋转 allowlist 测试失败。状态：`OPEN_ACTIONABLE`；依赖：复现当前结果；阻塞：已有记录为两项预期 200、实际 400，尚无后续关闭证据。
7. [ ] 扩展 GS1 AI 解析范围。状态：`OPEN_ACTIONABLE`；依赖：明确 AI 家族、长度/校验与展示输入约定；阻塞：不能从“GS1 AI”推定支持全部标准 AI。
8. [ ] GS1 DataBar Composite 与链接字段。状态：`BLOCKED_APPROVAL`；依赖：GS1 AI 输入契约；阻塞：未选定 Composite 子型、字段映射与验收向量。
9. [ ] 具体 TSPL/ZPL 机型 glyph advance、组合字形、字体选择和墨迹范围校准。状态：`BLOCKED_EXTERNAL`；依赖：R139 校准 API/布局；阻塞：缺少设备、手册样本或实测数据。
10. [ ] 设备容量约束。状态：`BLOCKED_EXTERNAL`；依赖：目标机型、DPI、固件与介质资料；阻塞：真实可打印边界和符号容量未知。
11. [ ] DesignerHost 进程隔离和监听边界。状态：`BLOCKED_APPROVAL`；依赖：部署信任边界、允许监听地址与隔离验收契约；阻塞：尚未批准。
12. [ ] 正式性能、吞吐、容量和超时门槛。状态：`BLOCKED_APPROVAL`；依赖：批准的负载模型与阈值；阻塞：无正式性能指标。
13. [ ] 跨平台运行时/字库验证。状态：`BLOCKED_EXTERNAL`；依赖：目标 OS、运行时和字体环境；阻塞：缺少相应环境执行证据。
14. [ ] 具体机型 HRI 四边、字形墨迹与旋转方向实测。状态：`BLOCKED_EXTERNAL`；依赖：R138 角度边界配置；阻塞：缺少目标设备与样本。

**R145 验证状态**

- L1：ZPL RestrictedRaw、命令验证、Profile、编码、Provider 与命令测试 `net6.0` `196/196` 通过。
- L2：Core.Tests 完整 `net6.0` `3187/3187` 通过。
- L0：Core 与 Templates 五目标框架串行构建通过：`net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误。
- NU1900：测试无法访问 NuGet 漏洞索引；未影响编译和测试结果。
- 本轮未修改 workflow、job、matrix、依赖或基线。
- L5：未进行真实打印机验收。

### R145 下一步

按原优先级建议处理 ESC/POS Raw 行游标几何：它是下一项本地可执行 TODO，且不依赖 ZPL 生命周期或外部机型数据。

Goal Status 保持 active。

---

Task：多指令集打印剩余 TODO。Round：R144 TSPL BITMAP Raw 载荷契约。更新时间：2026-10-09。

## R144 本轮：TSPL BITMAP Raw 载荷契约

核对 TSC 官方 TSPL/TSPL2 编程手册后确认 `BITMAP` 的线上载荷是二进制；手册中的 HEX 列是字节的表示方式，并非可直接发送的 ASCII HEX。由于 `WriteRestrictedRaw(string)` 成功时保留原字节，接受 `FF` 会把两个 ASCII 字节发送到打印机而不是一个 `0xFF` 图像字节。本轮因此修正受限子集：Raw 字符串入口严格拒绝 BITMAP；结构化 `Bitmap(..., asciiHex)` 仍解码并输出原生二进制，可信 `WriteRawBytes` 仍可承载完整原始二进制命令。

依据：[TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf)，BITMAP 指令说明及第 86 页示例字节列。

### R144 已完成 TODO

- [x] 校正 TSPL BITMAP Raw 载荷契约：`WriteRestrictedRaw(string)` 对 BITMAP 明确报错，说明 ASCII HEX 与二进制线上载荷不等价，并指引到对应结构化/可信入口。
- [x] 保留 `Bitmap(..., asciiHex)` 的 ASCII HEX 输入便利性，确认其先解码再写入二进制载荷；新增测试断言线上出现 `0xFF`，而不是 ASCII 字符 `F`、`F`。
- [x] Raw BITMAP 拒绝保持原输出和状态不变；删除“受限 Raw 接受 ASCII HEX BITMAP”的旧测试预期并修正文档能力表。
- [x] 定向 `ProtocolRestrictedRawTests` 与 `ProtocolGoldenBytesTests` `net6.0` `35/35`；Core 全量测试 `net6.0` `3185/3185`；Core 五目标框架构建 0 警告、0 错误。

### R144 本轮文件

- `src/Bing.EasyPrint/Protocols/TsplPrintCommand.RestrictedRaw.cs`
- `tests/Bing.EasyPrint.Tests/ProtocolRestrictedRawTests.cs`
- `docs/protocols.md`
- `ai_docs/plans/multi-protocol-todo-progress.md`

### 累计已完成 TODO

- [x] R1-R143：多协议 API、Provider、模板、条码、Raw 校验、布局/校准、资源限制和状态共享；各轮具体条目与验证详见本账本历史记录。
- [x] R144：TSPL BITMAP 二进制线上载荷与受限 Raw 字符串契约；结构化 HEX 输入解码输出，详见本节。

### 未完成 TODO（沿用原优先级）

1. [ ] ZPL 自开格式的闭合与结构化续写规则。状态：`OPEN_ACTIONABLE`；依赖：受限 Raw 格式边界/结构化续写状态契约；阻塞：现有行为允许 Raw 打开格式，再由结构化命令和 End 完成，尚未统一完整批次与续写语义。
2. [ ] ESC/POS Raw 行游标几何。状态：`OPEN_ACTIONABLE`；依赖：无；阻塞：对齐命令可能清零已有行宽，制表位前进后未立即检查横向边界。
3. [ ] ZPL Raw 裁切模式与输出分段状态。状态：`OPEN_ACTIONABLE`；依赖：Profile 对全切、半切、Kiosk、Delayed 合法组合的定义；阻塞：Kiosk 比较对象和 Delayed 状态更新仍有缺口。
4. [ ] CPCL 完整 Raw 程序后的追加与失败恢复。状态：`OPEN_ACTIONABLE`；依赖：确定完整程序重复写入的追加语义；阻塞：结构化追加部分延迟到 Build 才失败，重复完整程序可能覆盖已存文本。
5. [ ] CPCL/TSPL 连续受限 Raw 调用的命令分隔。状态：`OPEN_ACTIONABLE`；依赖：确定末行无换行的 Raw 是否有效；阻塞：解析接受无换行末行，后续追加可能与末行拼接。
6. [ ] 四协议 tokenizer 临时内存预算。状态：`OPEN_ACTIONABLE`；依赖：定义解析器集合/字符串副本的预算计量；阻塞：部分临时分配未预留资源预算。
7. [ ] DesignerHost TSPL/ZPL 二维码旋转 allowlist 测试失败。状态：`OPEN_ACTIONABLE`；依赖：复现当前结果；阻塞：已有记录为两项预期 200、实际 400，尚无后续关闭证据。
8. [ ] 扩展 GS1 AI 解析范围。状态：`OPEN_ACTIONABLE`；依赖：明确 AI 家族、长度/校验与展示输入约定；阻塞：不能从“GS1 AI”推定支持全部标准 AI。
9. [ ] GS1 DataBar Composite 与链接字段。状态：`BLOCKED_APPROVAL`；依赖：GS1 AI 输入契约；阻塞：未选定 Composite 子型、字段映射与验收向量。
10. [ ] 具体 TSPL/ZPL 机型 glyph advance、组合字形、字体选择和墨迹范围校准。状态：`BLOCKED_EXTERNAL`；依赖：R139 校准 API/布局；阻塞：缺少设备、手册样本或实测数据。
11. [ ] 设备容量约束。状态：`BLOCKED_EXTERNAL`；依赖：目标机型、DPI、固件与介质资料；阻塞：真实可打印边界和符号容量未知。
12. [ ] DesignerHost 进程隔离和监听边界。状态：`BLOCKED_APPROVAL`；依赖：部署信任边界、允许监听地址与隔离验收契约；阻塞：尚未批准。
13. [ ] 正式性能、吞吐、容量和超时门槛。状态：`BLOCKED_APPROVAL`；依赖：批准的负载模型与阈值；阻塞：无正式性能指标。
14. [ ] 跨平台运行时/字库验证。状态：`BLOCKED_EXTERNAL`；依赖：目标 OS、运行时和字体环境；阻塞：缺少相应环境执行证据。
15. [ ] 具体机型 HRI 四边、字形墨迹与旋转方向实测。状态：`BLOCKED_EXTERNAL`；依赖：R138 角度边界配置；阻塞：缺少目标设备与样本。

**R144 验证状态**

- [x] L1：TSPL RestrictedRaw 与 GoldenBytes `net6.0` `35/35` 通过。
- [x] L2：Core.Tests 完整 `net6.0` `3185/3185` 通过。
- [x] L0：Core 五目标框架构建通过：`net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误。
- [x] `git diff --check` 通过；仅报告仓库已有 CRLF/LF 转换提示。
- NU1900：测试无法访问 NuGet 漏洞索引；未影响编译和测试结果。
- L5：未进行真实打印机验收。

### R144 下一步

建议按原优先级处理 ZPL 自开格式的闭合与结构化续写状态；它是当前剩余清单首项，且不依赖外部机型数据。

配置/CI 清理：无；本轮未修改 workflow、job、matrix、依赖或基线。

Goal Status 保持 active；仅 R144 TSPL Raw 子范围完成，未关闭整体多协议目标。

---

Task：多指令集打印剩余 TODO。Round：R143 CPCL 受限 Raw 状态共享。更新时间：2026-10-09。

## R143 本轮：CPCL 受限 Raw 与结构化状态共享

按 R142 原优先级完成 CPCL 状态共享：受限 Raw 状态跨批次持久化，结构化 SETMAG、BT、对齐、SETSP、旋转入口同步同一格式状态；可信 `WriteRaw` 使状态未知，后续受限绘制须显式重建状态。文本/条码几何采用当前倍率、间距、HRI 与对齐范围估算，完整 Raw 原文保持不变。

### R143 已完成 TODO

- [x] CPCL 受限 Raw 与结构化 API 跨调用共享 SETMAG、BT、CENTER/LEFT/RIGHT、SETSP 和旋转状态；解析失败不提交候选状态。
- [x] `WriteRaw` 不透明内容使几何状态失效；可通过结构化命令或受限 Raw 中的显式状态命令恢复，避免使用过期状态通过边界校验。
- [x] CPCL 文本、ML、CONCAT 和条码 HRI footprint 纳入横/纵向倍率、字符间距及对齐字段范围；旋转文本与非左对齐严格拒绝。
- [x] 结构化 `DrawText` 结束后同步其输出的默认 SETMAG 复位状态。
- [x] 定向 CPCL/RestrictedRaw `net6.0` 测试 `41/41`；Core 完整测试 `net6.0` `3183/3183`；Core 五目标框架构建 0 警告、0 错误。

### R143 本轮文件

- `src/Bing.EasyPrint/CPCL/CPCLPrintCommand.RestrictedRaw.cs`
- `src/Bing.EasyPrint/CPCL/CPCLPrintCommand.cs`
- `src/Bing.EasyPrint/CPCL/CPCLPrintCommand.Component.cs`
- `tests/Bing.EasyPrint.Tests/CPCL/CPCLRestrictedRawTests.cs`
- `ai_docs/plans/multi-protocol-todo-progress.md`

### 累计已完成 TODO

- [x] R1-R142：沿本账本各轮记录完成的多协议 API、Provider、模板、条码、Raw 校验、布局/校准、资源限制与测试能力；各轮实现说明和验证数据保留在各自轮次下。
- [x] R143：本轮 CPCL 受限 Raw/结构化状态共享与几何估算，详见本节。

### 未完成 TODO（沿用原优先级）

1. [ ] TSPL ASCII 十六进制 BITMAP 的受限 Raw 线载荷契约。状态：`OPEN_ACTIONABLE`；依赖：无；阻塞：确认 TSPL 语法中的 ASCII HEX 是文本还是解码后的点阵字节，当前结构化 API 会解码，而 Raw 成功后保留原 ASCII。
2. [ ] ZPL 自开格式的闭合与结构化续写规则。状态：`OPEN_ACTIONABLE`；依赖：受限 Raw 格式边界/结构化续写状态契约；阻塞：现有行为允许 Raw 打开格式，再由结构化命令与 End 完成，尚未统一完整批次与续写语义。
3. [ ] ESC/POS Raw 行游标几何。状态：`OPEN_ACTIONABLE`；依赖：无；阻塞：对齐命令可能清零已有行宽，制表位前进后未立即检查横向边界。
4. [ ] ZPL Raw 裁切模式与输出分段状态。状态：`OPEN_ACTIONABLE`；依赖：Profile 对全切、半切、Kiosk、Delayed 合法组合的定义；阻塞：Kiosk 比较对象和 Delayed 累积状态仍有实现缺口。
5. [ ] CPCL 完整 Raw 程序后的追加与失败恢复。状态：`OPEN_ACTIONABLE`；依赖：确定完整程序重复写入的追加语义；阻塞：结构化追加部分延迟到 Build 才失败，重复完整程序可能覆盖已存文本。
6. [ ] CPCL/TSPL 连续受限 Raw 调用的命令分隔。状态：`OPEN_ACTIONABLE`；依赖：确定末行无换行的 Raw 是否有效；阻塞：解析接受无换行末行，后续追加可能与末行拼接。
7. [ ] 四协议 tokenizer 临时内存预算。状态：`OPEN_ACTIONABLE`；依赖：定义解析器集合/字符串副本的预算计量；阻塞：部分临时分配未预留资源预算。
8. [ ] DesignerHost TSPL/ZPL 二维码旋转 allowlist 测试失败。状态：`OPEN_ACTIONABLE`；依赖：复现当前结果；阻塞：最近证据为两项预期 200、实际 400，尚无后续关闭记录。
9. [ ] 扩展 GS1 AI 解析范围。状态：`OPEN_ACTIONABLE`；依赖：明确 AI 家族、长度/校验与展示输入约定；阻塞：不能从“GS1 AI”推定支持全部标准 AI。
10. [ ] GS1 DataBar Composite 与链接字段。状态：`BLOCKED_APPROVAL`；依赖：GS1 AI 输入契约；阻塞：未选定 Composite 子型、字段映射与验收向量。
11. [ ] 具体 TSPL/ZPL 机型 glyph advance、组合字形、字体选择和墨迹范围校准。状态：`BLOCKED_EXTERNAL`；依赖：R139 校准 API/布局；阻塞：缺少设备、手册样本或实测数据。
12. [ ] 设备容量约束。状态：`BLOCKED_EXTERNAL`；依赖：目标机型、DPI、固件与介质资料；阻塞：真实可打印边界和符号容量未知。
13. [ ] DesignerHost 进程隔离和监听边界。状态：`BLOCKED_APPROVAL`；依赖：部署信任边界、允许监听地址与隔离验收契约；阻塞：尚未批准。
14. [ ] 正式性能、吞吐、容量和超时门槛。状态：`BLOCKED_APPROVAL`；依赖：批准的负载模型与阈值；阻塞：无正式性能指标。
15. [ ] 跨平台运行时/字库验证。状态：`BLOCKED_EXTERNAL`；依赖：目标 OS、运行时和字体环境；阻塞：缺少相应环境执行证据。
16. [ ] 具体机型 HRI 四边、字形墨迹与旋转方向实测。状态：`BLOCKED_EXTERNAL`；依赖：R138 角度边界配置；阻塞：缺少目标设备与样本。

**R143 验证状态**

- [x] L1：CPCLRestrictedRawTests 与 ProtocolRestrictedRawTests `net6.0` `41/41` 通过。
- [x] L2：Core.Tests 完整 `net6.0` `3184/3184` 通过。
- [x] L0：Core 五目标框架构建通过：`net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误。
- [x] `git diff --check` 通过；仅报告仓库既有 CRLF/LF 转换提示。
- NU1900：测试无法访问 NuGet 漏洞索引；未影响编译和测试结果。
- L5：未进行真实打印机验收。

### R143 下一步

建议按原优先级处理 TSPL ASCII 十六进制 BITMAP 的 Raw 载荷契约；先明确 ASCII 原文与点阵解码的对应关系，再补解析与字节保持测试，避免 Raw 与结构化 API 对同一输入含义不一致。

配置/CI 清理：无；本轮未修改 workflow、job、matrix、依赖或基线。

Goal Status 保持 active；仅 R143 CPCL 子范围完成，未关闭整体多协议目标。

---

Task：多指令集打印剩余 TODO。Round：R142 受限 Raw 几何校验修正。更新时间：2026-10-09。

## R142 本轮：受限 Raw 几何校验修正

基于 R141 完成审查，修复 CPCL 多行文字块的首行夹带与行高估算，以及 TSPL 受限 TEXT 中引号内空格在校验时被裁剪的问题。两处均新增原子失败/输出保持回归；其余审查发现保留在待办，不据 R141 既有通过记录推定完成。

### R142 已完成 TODO

- [x] CPCL ML 拒绝文本命令头内联数据，避免首行绕过文本宽度校验；高度按字号占位与行距共同计算，不再只用行距乘行数。
- [x] 受限 Raw 逗号字段解析保留引号包围字段中的前后空格；TSPL TEXT 边界验证与最终原文一致，越界失败不追加输出。
- [x] 新增 CPCL 多行几何及 TSPL 空白/越界原子性回归。

### R142 本轮文件

- `src/Bing.EasyPrint/CPCL/CPCLPrintCommand.RestrictedRaw.cs`
- `src/Bing.EasyPrint/Protocols/RestrictedRawSyntax.cs`
- `tests/Bing.EasyPrint.Tests/CPCL/CPCLRestrictedRawTests.cs`
- `tests/Bing.EasyPrint.Tests/ProtocolRestrictedRawTests.cs`
- `ai_docs/plans/multi-protocol-todo-progress.md`

### R142 未完成 TODO

- [ ] CPCL 受限 Raw/结构化 API 跨调用的字体、条码 HRI、对齐与间距状态共享。状态：`OPEN_ACTIONABLE`；上游依赖：明确可信 `WriteRaw`/`WriteRawLine` 令本地状态失效时的处理；当前阻塞：CPCL 结构化命令通过 Raw 行延迟写入，不能只把解析器临时状态保存后就声称双向共享。
- [ ] TSPL ASCII 十六进制 BITMAP 的受限 Raw 线载荷契约。状态：`OPEN_ACTIONABLE`；上游依赖：确认 ASCII HEX 在目标 TSPL 语法中代表文本还是解码后的点阵字节；当前阻塞：当前 Raw 校验复用会解码数据的结构化 API，成功后却保留 ASCII 原文。
- [ ] ZPL 自开格式的闭合与结构化续写规则。状态：`OPEN_ACTIONABLE`；上游依赖：统一“受限 Raw 批次完整格式”与 Raw 打开、结构化 API 续写并关闭的状态共享契约；当前阻塞：现有测试允许 Raw 只打开格式，之后由结构化命令和 End 完成。
- [ ] ESC/POS Raw 行游标几何。状态：`OPEN_ACTIONABLE`；上游依赖：无；当前阻塞：Raw 对齐命令会清零已有行宽，制表位前进后未立即检查横向边界。
- [ ] ZPL Raw 裁切模式与输出分段状态。状态：`OPEN_ACTIONABLE`；上游依赖：Profile 对全切/半切/Kiosk/Delayed 的合法组合；当前阻塞：Kiosk 校验把全切值与半切未切长度比较，Delayed 状态使用累积更新。
- [ ] CPCL 完整 Raw 程序后的追加与失败恢复。状态：`OPEN_ACTIONABLE`；上游依赖：确定完整程序重复写入的追加语义；当前阻塞：部分结构化追加延迟到 Build 才失败，重复完整程序会覆盖已存文本。
- [ ] CPCL/TSPL 连续受限 Raw 调用的命令分隔。状态：`OPEN_ACTIONABLE`；上游依赖：确定末行无换行的受限 Raw 是否有效；当前阻塞：解析接受末行但输出不补分隔符，后续追加可能与之连成一个命令。
- [ ] 四协议受限 Raw tokenizer 临时内存预算。状态：`OPEN_ACTIONABLE`；上游依赖：将解析器集合/字符串副本纳入资源预算的计量方式；当前阻塞：部分 token/list 分配发生在临时资源作用域内，但并未对这些副本进行预算预留。
- [ ] DesignerHost TSPL/ZPL 二维码旋转 allowlist 测试失败。状态：`OPEN_ACTIONABLE`；上游依赖：复现当前测试结果；当前阻塞：最近可查验证记录仍为两项预期 200、实际 400，未找到后续关闭证据。
- [ ] 扩展 GS1 AI 解析范围。状态：`OPEN_ACTIONABLE`；上游依赖：明确目标 AI 家族、长度/校验与展示输入约定；当前阻塞：不能从“扩展 GS1 AI”推定应支持所有标准 AI。
- [ ] GS1 DataBar Composite 与链接字段。状态：`BLOCKED_APPROVAL`；上游依赖：GS1 AI 输入约定；当前阻塞：未选定 Composite 子型、字段映射与输出验收向量。
- [ ] 具体 TSPL/ZPL 机型的 glyph advance、组合字形、字体选择和墨迹范围校准。状态：`BLOCKED_EXTERNAL`；上游依赖：R139 校准 API 与布局；当前阻塞：缺少设备、手册样本或实测数据。
- [ ] 设备容量约束。状态：`BLOCKED_EXTERNAL`；上游依赖：目标机型、固件、DPI 和介质资料；当前阻塞：设备的真实可打印边界和符号容量未知。
- [ ] DesignerHost 进程隔离和监听边界。状态：`BLOCKED_APPROVAL`；上游依赖：部署信任边界和允许监听地址；当前阻塞：没有获批的隔离验收契约。
- [ ] 正式性能、吞吐、容量和超时门槛。状态：`BLOCKED_APPROVAL`；上游依赖：获批的工作负载与阈值；当前阻塞：无正式性能指标。
- [ ] 跨平台运行时/字库验证。状态：`BLOCKED_EXTERNAL`；上游依赖：目标 OS、运行时及字体环境；当前阻塞：缺少这些环境的执行证据。
- [ ] 具体机型 HRI 四边、字体墨迹及旋转方向实测。状态：`BLOCKED_EXTERNAL`；上游依赖：R138 角度边界配置；当前阻塞：缺少目标设备与样本。

**R142 验证状态**

- [x] L1：Core.Tests `net6.0`，CPCLRestrictedRawTests 与 ProtocolRestrictedRawTests `35/35` 通过。
- [x] L0：Core 五目标框架构建通过：`net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误。
- NU1900：定向测试无法访问 NuGet 漏洞索引；未影响测试结果。
- L5：未进行真实打印机验收。

### R142 下一步

建议先处理 CPCL Raw/结构化状态共享，明确可信 Raw 造成状态未知后的严格校验边界；它会影响受限几何判断的正确性。GS1 AI 仍按原计划待明确新增 AI 范围后实现。

---

Task：多指令集打印剩余 TODO。Round：R141 四协议受限 Raw 校验。更新时间：2026-10-09。

## R141 本轮：四协议受限 Raw 校验

新增独立的 `IRestrictedRawPrintCommand<TCommand>.WriteRestrictedRaw(string)`，不改动旧 `IPrintCommand<T>` 和可信 `WriteRaw`、`WriteRawLine`、`WriteRawBytes` 入口。CPCL、ESC/POS Native、TSPL、ZPL 分别扫描并校验各自的完整受限子集；成功后按命令原编码写入原文本/字节，不重排格式。解析在候选状态和临时资源预算中完成，语法、能力、编码或几何校验失败时不写入 Raw，也不提交协议状态。

### R141 已完成 TODO

- [x] 公共受限 Raw 接口独立于旧命令接口，四协议命令均实现该契约。
- [x] CPCL 校验任务边界、页面方向/尺寸、文字/文字块、线框、条码、二维码/二维条码及长度可核验图形；完整任务必须包含 `!`、`PAGE-WIDTH` 和 `PRINT`，绘制 footprint 越界或无法估算的格式拒绝。
- [x] ESC/POS 仅接受 Native 可解析文本、状态、进纸、切纸、原生条码和二维码序列；Raster/状态查询/不完整载荷拒绝，按 `GS W`、Profile 和结构化估算器验证打印宽度。
- [x] TSPL 校验 SIZE、介质/方向/参考点、文字、线框/填充、条码、二维码、ASCII 十六进制 BITMAP、进纸/打印/切纸与自检，并以页面状态检查绘制范围。
- [x] ZPL 校验完整格式、`^PW`/`^LL`、字段原点/文字/线框/受支持条码、ASCII 十六进制 `^GFA`、打印数量和切纸；拒绝格式外绘制、未知命令、下载及字段夹带。
- [x] 修复 ZPL Raw 多字段迭代边界，连续 `^FO` 字段不再被跳过；`^GFA` 正确消费图形载荷及其 `^FS`。
- [x] 四协议解析都先验证候选状态与几何再提交，拒绝未知/私有语法、二进制下载/载荷、未闭合块、长度不符和命令夹带；诊断含协议、命令及行号/字节偏移。
- [x] 加入允许语法、原始字节保持、结构化/Raw 双向状态、宽度与几何失败、能力拒绝及原子失败回归；覆盖四协议的介质、绘制/条码/二维码、图形及结束/作业命令，CPCL 补充 MaxiCode POST 必填和 Aztec 两种结束符。
- [x] `WriteRaw` 受限指令校验 TODO 关闭；现有可信 Raw 入口的兼容字节行为未改变。

### R141 本轮文件

- `docs/protocols.md`
- `src/Bing.EasyPrint/IRestrictedRawPrintCommand.cs`
- `src/Bing.EasyPrint/Protocols/RestrictedRawSyntax.cs`
- `src/Bing.EasyPrint/CPCL/CPCLPrintCommand.RestrictedRaw.cs`
- `src/Bing.EasyPrint/CPCL/CPCLPrintCommand.cs`
- `src/Bing.EasyPrint/CPCL/CPCLPrintCommand.Component.cs`
- `src/Bing.EasyPrint/Protocols/EscPosPrintCommand.RestrictedRaw.cs`
- `src/Bing.EasyPrint/Protocols/TsplPrintCommand.RestrictedRaw.cs`
- `src/Bing.EasyPrint/Protocols/ZplPrintCommand.RestrictedRaw.cs`
- `tests/Bing.EasyPrint.Tests/CPCL/CPCLRestrictedRawTests.cs`
- `tests/Bing.EasyPrint.Tests/ProtocolRestrictedRawTests.cs`

### R141 未完成 TODO

依 R140 原优先级保留：

- [ ] 为具体 TSPL/ZPL 机型采集 glyph advance、组合字形、字体选择和墨迹范围数值。状态：`BLOCKED_EXTERNAL`；上游依赖：R139 本地校准 API/布局完成；当前阻塞：缺少目标设备、手册样本或实测数据。
- [ ] 扩展 GS1 AI 解析范围。状态：`OPEN_ACTIONABLE`；上游依赖：无；当前阻塞：需明确目标 AI 家族、字段长度/校验及展示输入约定，不能从“GS1 AI”推断为支持全部 AI。
- [ ] GS1 DataBar Composite 与链接字段。状态：`BLOCKED_APPROVAL`；上游依赖：GS1 AI 输入约定；当前阻塞：尚无选定 Composite 子型、字段映射和输出验收向量。
- [ ] 设备容量约束。状态：`BLOCKED_EXTERNAL`；上游依赖：明确目标打印机型号、DPI、可打印宽度/高度和固件资料；当前阻塞：本地不能推断设备载体极限。正式性能、容量和超时门槛还需项目负责人批准。
- [ ] DesignerHost 进程隔离和监听边界。状态：`BLOCKED_APPROVAL`；上游依赖：明确进程/租户信任边界、允许监听地址和隔离强度；当前阻塞：缺少获批的隔离验收契约。
- [ ] 正式性能、吞吐、容量和超时门槛。状态：`BLOCKED_APPROVAL`；上游依赖：项目负责人批准可测阈值与负载模型；当前阻塞：没有正式指标，不能臆造预算或性能基线。
- [ ] 跨平台运行时/字库验证。状态：`BLOCKED_EXTERNAL`；上游依赖：确定目标操作系统、运行时和字体环境；当前阻塞：本轮只有当前平台的验证证据。
- [ ] 完成具体机型 HRI 四边、字体墨迹及旋转方向实测。状态：`BLOCKED_EXTERNAL`；上游依赖：R138 角度边界配置已完成；当前阻塞：缺少目标机型、样本或设备。

**Accepted Limitation**

- [ ] `TextElements` 使用运行时 `StringInfo` 文本单元精确匹配，不做 Unicode 规范化；校准值不代表已验证任一机型真实字形或墨迹。
- [ ] CPCL MaxiCode 与设备字体/条码边界采用计划要求的保守 footprint；固件差异可能造成拒绝或设备端表现差异，仍需具体机型验收。
- [ ] CPCL `B RSS` 复合命令明确不属于 R141 受限 Raw 子集；其分段/载荷边界和 footprint 需要更精确的固件语法与 GS1 Composite 输入契约。

**R141 验证状态**

- [x] L1：四协议 RestrictedRaw 定向测试 `net6.0` `33/33` 通过。
- [x] L2：Core.Tests 完整 `net6.0` `3176/3176` 通过。
- [x] L0：Core 五目标框架构建通过：`net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，0 警告、0 错误。
- [x] L0：`git diff --check` 通过；新增受限 Raw 文件无行尾空白。
- NU1900：测试无法访问 NuGet 漏洞索引；不影响测试结果。
- L5：真实热敏/针式打印机行为未在本地验证，仍需目标设备验收。

### R141 下一步

建议下一轮明确 GS1 AI 目标家族、字段长度/校验与显示输入约定后扩展解析；理由是 R141 已关闭原优先级在前的 Raw 校验项，而具体 TSPL/ZPL 字形采集目前受外部设备资料阻塞。

---

## R140 历史轮次：ESC/POS Raster GS1 DataBar Stacked

## R140 本轮：ESC/POS Raster GS1 DataBar Stacked

新增 Raster 主机编码和模板能力：沿用 DataBar Omnidirectional 的 13 位 payload 编码，按 50X 宽度重排成 5X 顶行、1X 分隔行和 7X 底行。总高度小于 13X 时在绘制前拒绝；HRI 使用隐含 AI `(01)` 与 GTIN 校验位。ESC/POS Native、TSPL、ZPL、CPCL 保持不支持。

### R140 已完成 TODO

- [x] `BarcodeType.Gs1DataBarStacked` 追加为值 30，不改变既有枚举数值。
- [x] ESC/POS Raster 生成 50×13 Stacked 模块矩阵，含两侧结构守卫和 GS1 分隔行；矩阵预算按固定尺寸计入。
- [x] Raster 绘制和页面边界使用总条高；最低高度为 `13 * NarrowWidth`，行高比例按 5:1:7 映射。
- [x] Raster HRI、能力快照、模板宽度和模板输出接入；Native、TSPL、ZPL、CPCL 不声明该能力。
- [x] 新增 golden 模块行、尺寸下限、无效 payload 预算恢复、Raster 命令输出及模板输出回归。
- [x] 更新协议与模板使用说明，注明能力边界和标准参考。
- [x] DesignerHost 请求/输出资源预算、并发准入和取消路径已有实现；本轮定向回归 `33/33` 通过。

### R140 未完成 TODO

按 R139 原优先级保留，组内仍按原顺序：

- [ ] 为具体 TSPL/ZPL 机型采集 glyph advance、组合字形、字体选择和墨迹范围数值。状态：`BLOCKED_EXTERNAL`；上游依赖：R139 本地校准 API/布局完成；当前阻塞：缺少目标设备、手册样本或实测数据。
- [ ] 完善 `WriteRaw` 受限指令校验。状态：`BLOCKED_APPROVAL`；上游依赖：需先定义各协议允许的原始指令语法和几何校验范围；当前阻塞：现有 Raw 是兼容的可信调用方逃生口，未经批准的白名单会改变兼容契约。
- [ ] 扩展 GS1 AI 解析范围。状态：`BLOCKED_APPROVAL`；上游依赖：明确目标 AI 家族、字段长度/校验和展示输入约定；当前阻塞：现有实现只覆盖声明的子集，不能从“GS1 AI”推断为支持全部 AI。
- [x] ESC/POS Raster GS1 DataBar Stacked 编码、模板能力与 5:1:7 两行几何；本轮完成，详见上述 R140 已完成列表。
- [ ] GS1 DataBar Composite 与链接字段。状态：`BLOCKED_APPROVAL`；上游依赖：GS1 AI 输入约定及 Composite 组件/链接指示的编码契约；当前阻塞：尚无选定的 Composite 子型、字段映射和输出验收向量。
- [ ] 设备容量约束。状态：`BLOCKED_EXTERNAL`；上游依赖：明确目标打印机型号、DPI、可打印宽度/高度和固件资料；当前阻塞：本地不能推断设备载体极限。正式性能、容量和超时门槛还需项目负责人批准。
- [x] DesignerHost 请求大小/工作字节预算、并发准入、取消及输出流预算；状态：`COMPLETED`；本轮定向验证 `HostRequestBudgetTests`、`HostAdmissionTests`、`HostPrintPipelineBudgetTests` 共 `33/33`。
- [ ] DesignerHost 进程隔离和监听边界。状态：`BLOCKED_APPROVAL`；上游依赖：明确进程/租户信任边界、允许监听地址和所需隔离强度；当前阻塞：现有默认仅监听 loopback，但没有获批的隔离验收契约。
- [ ] 正式性能、吞吐、容量和超时门槛。状态：`BLOCKED_APPROVAL`；上游依赖：项目负责人批准可测阈值与负载模型；当前阻塞：无正式指标，不能臆造预算或性能基线。
- [ ] 跨平台运行时/字库验证。状态：`BLOCKED_EXTERNAL`；上游依赖：确定目标操作系统、运行时和字体环境；当前阻塞：当前工作环境仅能提供本机平台证据，不能替代目标平台验证。
- [ ] 完成具体机型 HRI 四边、字体墨迹及旋转方向实测。状态：`BLOCKED_EXTERNAL`；上游依赖：R138 角度边界配置已完成；当前阻塞：缺少目标机型、样本或设备。

**Accepted Limitation**

- [ ] `TextElements` 使用运行时 `StringInfo` 文本单元精确匹配，不做 Unicode 规范化；校准值不代表库已验证任一机型的真实字形或墨迹。

### R140 验证状态

- [x] CHANGE IMPACT：枚举、ESC/POS Raster 编码/绘制/预算、能力快照、模板宽度和相关测试；未改协议原始指令、Native、TSPL、ZPL、CPCL 输出。
- [x] L1：`EscPosRasterGs1DataBarTests` 在 `net6.0`、`netcoreapp3.1` 各 `20/20` 通过。
- [x] L2：Core.Tests 完整 `net6.0` `3143/3143` 通过。
- [x] Host 资源/准入/取消定向测试 `net8.0` `33/33` 通过；默认命令受已有 artifacts 静态内容 glob 影响，使用 `EnableDefaultContentItems=false` 后通过，未改项目或 CI 配置。
- [x] L0：Core 与 Templates 五个目标框架构建通过，0 警告、0 错误。
- [x] L0：`git diff --check` 无空白错误。
- NU1900：NuGet 漏洞索引不可访问；不影响定向测试结果。
- L5：真实打印和扫描质量仍需设备验收。
- [x] No-Progress Check：CHANGED；ESC/POS Raster 新增可用的 GS1 DataBar Stacked 主机编码和模板输出。

### R140 下一步

建议下一轮先明确 `WriteRaw` 可接受的指令语法与兼容策略：它位于原优先级首位且目前没有安全白名单契约；随后才能继续 GS1 AI/Composite 编码范围。DesignerHost 的请求资源预算已验证，进程隔离和正式性能指标需先有验收契约。

---

## R139 历史轮次：TSPL/ZPL 逐文本单元字体度量

在既有逐设备字体半角/全角宽度及行高校准之上，增加精确 Unicode 文本单元的前进宽度和可选墨迹矩形。模板换行、分页、Fit、四种方向的旋转占位及页面预检使用校准值；DesignerHost 的严格 JSON 请求也能将该数据送入同一渲染路径。校准数值仍由调用方按目标机型测量后提供，本轮未伪造设备数据。

### R139 已完成 TODO

- [x] `DeviceFontCalibration.TextElements` 可按 `StringInfo` 文本单元配置 24 点下的 `AdvanceAt24`；组合附加符可随所属文本单元校准，零前进宽度有效。
- [x] 可选墨迹四边以命令原点为基准，允许负向 bearing；部分边界、非有限值、倒置范围和无效文本单元会在布局前拒绝。
- [x] 校准按 TSPL/ZPL 设备字体名隔离，按字号缩放；TSPL 固定字体使用其整数放大倍率。
- [x] 文本度量接入换行、Fit、分页、四种旋转边界和 TSPL 原生锚点补偿；ZPL 保持其旋转占位左上角语义。
- [x] DesignerHost 接收并深拷贝 `textElements` 字段，继续拒绝未知嵌套字段。
- [x] 新增 TSPL/ZPL 字体隔离、组合文本、缩放、负向墨迹、四方向边界、ZPL 原始坐标与 Host JSON 预览回归覆盖。
- [x] 更新模板、协议、DesignerHost 使用说明及生成的 API XML 文档。

### R139 未完成 TODO

按原优先级：

- [ ] 为具体 TSPL/ZPL 机型采集 glyph advance、组合字形、字体选择和墨迹范围数值。状态：`BLOCKED_EXTERNAL`；上游依赖：R139 本地校准 API 与布局路径已完成；当前阻塞：没有目标设备、手册样本或实测记录，不能生成机型校准数据。
- [ ] 完善 `WriteRaw` 受限指令校验、GS1 AI/Stacked/Composite/链接字段、设备容量约束，以及 DesignerHost 隔离和资源/性能/跨平台验证。状态：`OPEN_ACTIONABLE`；上游依赖：无；当前阻塞：尚未进入本轮，按原优先级留待下一轮。
- [ ] 完成具体机型 HRI 四边、字体墨迹及旋转方向实测。状态：`BLOCKED_EXTERNAL`；上游依赖：R138 角度边界配置已完成；当前阻塞：缺少目标机型手册、打印样本或设备。

**Accepted Limitation**

- [ ] `TextElements` 使用运行时 `StringInfo` 文本单元精确匹配，不做 Unicode 规范化；数据表达的是主机布局校准，不代表库已验证任一机型的实际字形或墨迹。

### R139 验证状态

- [x] CHANGE IMPACT：模板公开校准 DTO、文本度量、共享布局/Provider 输入、DesignerHost 严格请求 DTO 与 JSON 映射、Core.Tests 和 Host.Tests；风险中等。
- [x] L0：`git diff --check` 通过；仅有既有 XML/CPCL 文件的 CRLF 转换提示，无空白错误。
- [x] L1：模板字体校准定向测试 `net6.0` `31/31`，零失败、零跳过。
- [x] L2：Core.Tests `net6.0` `3138/3138`，零失败、零跳过。
- [x] Host R139 JSON 预览定向冒烟 `net8.0` `1/1` 通过；Host.Tests 全量 `120/122`，两项 TSPL/ZPL 二维码旋转 allowlist 测试仍返回 400，未由本轮字体校准改动触及。
- [x] Core 与 Templates 的 `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部构建通过，零警告、零错误。
- [x] Host/Host.Tests 项目编译通过。工作区已有 `tools/DesignerHost/artifacts` 被 Web SDK 默认静态内容 glob 纳入，标准测试命令尝试复制其中不存在的嵌套输出；测试命令通过 `EnableDefaultContentItems=false` 排除该生成目录，没有修改项目或 CI 配置。
- NU1900：Core.Tests 无法加载 NuGet 漏洞索引；不影响测试结果。
- L3/L4：未改依赖、TFM、打包或 CI；Host 契约已用 JSON 预览验证。
- L5：`BLOCKED_EXTERNAL`；真实字体、墨迹和 HRI 数值需要目标设备验收。
- [x] No-Progress Check：CHANGED；模板可逐文本单元表达设备字体宽度和墨迹范围，并将其传至 Host 请求。

### R139 下一步

本轮本地字体校准能力和 Host 请求通路完成。下一轮建议处理剩余 grouped TODO：`WriteRaw` 受限指令校验、GS1 变体、设备容量以及 DesignerHost 隔离和资源/性能/跨平台验证；理由是具体字体/HRI 数值当前受外部设备资料阻塞。

---

## R138 本轮：TSPL/ZPL 原生 HRI 逐角度边界

为条码自身的 90/180/270 度旋转增加独立 HRI 页面四边覆盖值。各边使用页面坐标点数；角度专用边为 null 时逐边继承原 Profile 通用值，显式 0 表示覆盖为零。该设置只影响 direct 与模板边界预检，机型未配置角度覆盖时保持既有行为。

### R138 已完成 TODO

- [x] 增加不可变 `NativeBarcodeHumanReadableOverhang` 边距值类型，拒绝负值并允许显式零。
- [x] TSPL/ZPL Profile 与能力快照分别描述 90/180/270 度四边覆盖；构造命令后替换 Profile 配置不会改变快照。
- [x] Direct TSPL/ZPL 按实际条码角度选择外伸，未覆盖的单边继承通用值。
- [x] 模板 Provider 使用相同角度选择和逐边继承规则，覆盖旋转 exact-edge 与 overflow。
- [x] 增加三种非零角度、负值、零值、Profile 快照、通用回退、direct/template 边界及命令字节不变回归用例。
- [x] 更新协议、模板和验证文档。

### R138 未完成 TODO

**Open Actionable**

- [ ] 校准具体 TSPL/ZPL 字体的 glyph advance、组合字形、字体选择及真实墨迹范围。状态：`OPEN_ACTIONABLE`；上游依赖：无，R138 角度边界已完成；当前阻塞：本地度量能力仍待实现，真实墨迹校准另依赖目标机型资料。
- [ ] 完善 `WriteRaw` 受限指令校验、GS1 AI/Stacked/Composite/链接字段、设备容量约束，以及 DesignerHost 隔离和资源/性能/跨平台验证。状态：`OPEN_ACTIONABLE`；上游依赖：无已识别的 R138 阻塞；当前阻塞：本轮按原优先级先完成字体度量，后续逐项推进本条范围。

**Blocked Approval**

- None。本轮只增加可选的边界预检配置，不设定机型数值、容量阈值或支持承诺。

**Blocked External**

- [ ] 具体机型的四边 HRI 外伸、字体墨迹及旋转方向数据仍需设备手册或打印样本。依赖上游：R138 已完成按角度承载校准值的 API；当前阻塞：缺少目标设备资料/实测条件，测量值未伪造。

**Not Applicable**

- CPCL/ESC/POS 不使用 TSPL/ZPL 原生 HRI 边界 Profile；协议字节、包图与 CI 配置不变。

**Accepted Limitation**

- [ ] 角度覆盖仍是调用方声明的主机预检边距；它不改变设备字段原点、字体或 HRI 对齐，也不证明真实墨迹尺寸。

**Verified Boundary**

- Awaiting L1/L2 execution.

**Deferred**

- 逐机型 HRI 实物轮廓校准等待目标设备数据；下一项本地实现按待办顺序处理字体度量。

### R138 验证状态

- [x] CHANGE IMPACT：Core 新增只读值类型和 Profile/Capabilities 属性，影响 LabelDeviceContract、TSPL/ZPL direct 预检、共享模板 Provider、Core.Tests 与协议/模板文档；风险中等。
- [x] L0：`git diff --check` 通过；本轮触及源码和文档无行尾空白，Profile/Contract/Capabilities/direct/Provider 角度属性调用点对齐。
- [x] L1：HRI Profile 定向测试 `net6.0` `42/42`，零失败、零跳过。
- [x] L2：Core.Tests `net6.0` `3120/3120`，零失败、零跳过。
- [x] 受影响项目构建：Core 与 Templates 的 `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部通过，零警告、零错误。
- NU1900：测试无法加载 NuGet 漏洞索引；不影响测试结果。
- L3/L4：未改模板协议选择、DesignerHost 契约、包图或项目依赖；按影响范围不运行全解决方案门禁。
- L5：`BLOCKED_EXTERNAL`；需要目标机型 HRI 数据。
- [x] No-Progress Check：CHANGED；此前共用四边值导致的旋转方向误拒现在可按 90/180/270 度单独声明。

### R138 下一步

R138 的定向/完整测试与双项目五目标框架构建均通过。下一轮按待办顺序处理 TSPL/ZPL 字体度量；真实机型墨迹边界继续等待设备资料。

## R137 本轮：TSPL/ZPL 原生 HRI 四边页面包围盒

在 R136 左侧外伸基础上，为 TSPL/ZPL Profile 增加上、右、下三边的页面坐标外伸，形成当前估算条码矩形之外的四边附加边距。HRI 启用时 direct 和模板 Provider 都使用该边界；右边距叠加在 R135 最小总宽之外，旧属性语义保持不变。对 90/270 度条码先换轴条码主体尺寸，再应用页面方向四边距；同时修正模板 Provider 原先没有把 HRI 预留高度转换到旋转后横向范围的问题。

### R137 已完成 TODO

- [x] TSPL/ZPL Profile 增加非负 `Top/Right/BottomOverhangDots`；四向属性均进入不可变能力快照。
- [x] Direct 命令与模板 Provider 对 HRI 启用状态统一执行四边页面边界检查。
- [x] 保留 `NativeBarcodeHumanReadableReserveWidthDots` 的最小总宽含义，新增右侧外伸作为额外边距。
- [x] 模板 Provider 旋转条码使用含 HRI 预留高度的纵向 footprint 换算横向尺寸，与 direct 估算一致。
- [x] 增加四边非法值、快照、各边贴边/越界、HRI 关闭和 90 度 direct/模板回归用例。
- [x] 更新协议、模板文档与验证记录。

### R137 未完成 TODO

**Open Actionable**

- [ ] 若需避免保守外伸造成过度拒绝，为不同条码旋转角度声明独立 HRI 边界，而非共用页面坐标上界。
- [ ] 校准具体 TSPL/ZPL 字体的 glyph advance、组合字形、字体选择及真实墨迹范围。
- [ ] 完善 `WriteRaw` 受限指令校验、GS1 AI/Stacked/Composite/链接字段、设备容量约束，以及 DesignerHost 隔离和资源/性能/跨平台验证。

**Blocked Approval**

- None。本轮添加可选边界预检值，不新增容量阈值或机型支持承诺。

**Blocked External**

- [ ] 真实字体、HRI 对齐及各旋转角度包围盒仍需目标机型手册或打印样本。

**Not Applicable**

- CPCL/ESC/POS 不使用本轮 TSPL/ZPL Profile 四边校准；命令字节和包图不变。

**Accepted Limitation**

- [ ] 四边值是当前旋转后矩形之外、按页面坐标应用的附加边距；单一 Profile 不存储逐角度实测值。没有校准资料时留空，维持既有估算。

**Verified Boundary**

- Awaiting L1 execution.

**Deferred**

- 逐角度、逐机型精细 HRI 轮廓和实际墨迹验证推迟至目标设备资料可用后。

### R137 验证状态

- [x] CHANGE IMPACT：Core Profile/能力 API、不可变契约、TSPL/ZPL direct 与共享模板 Provider 边界、Core.Tests、协议/模板文档；风险中等。
- [x] L0：`git diff --check` 通过；Profile 创建点和能力快照参数对齐，R137 触及文件无行尾空白。
- [x] L1：定向测试 `net6.0` `138/138`，零失败、零跳过。
- [x] L2：Core.Tests `net6.0` `3111/3111`，零失败、零跳过。
- [x] 受影响项目构建：Core 与 Templates 的 `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部通过，零警告、零错误。
- NU1900：测试无法加载 NuGet 漏洞索引；不影响测试结果。
- L3/L4：`NOT_APPLICABLE`；未修改包图、TFM 或 DesignerHost 契约。
- L5：`BLOCKED_EXTERNAL`；真实机型 HRI 轮廓与打印效果需要设备。
- [x] No-Progress Check：CHANGED；模板 Provider 现在完整覆盖 HRI 四边外伸并转置旋转预留高度。

### R137 下一步

本轮四边边界和旋转模板 footprint 已通过 L1/L2 及双项目五目标框架构建。下一轮可从 GS1/原始指令、设备容量或 DesignerHost 资源隔离等剩余本地能力继续；逐角度设备校准继续等待机型资料。

---

## R136 本轮：TSPL/ZPL 原生 HRI 左侧边界校准

在 R135 向右最小占位配置上，增加 `NativeBarcodeHumanReadableLeftOverhangDots`。配置按页面 X 轴、从字段坐标向左计量，仅扩充 HRI 开启时 direct 命令和模板 Provider 的主机边界；不移动字段、不改变协议输出，未设置或 HRI 关闭时保持原行为。旋转方向仍须按目标设备单独校准。

### R136 已完成 TODO

- [x] TSPL/ZPL Profile 增加非负左侧外伸值，并纳入不可变 `LabelDeviceContract` 与公开能力快照。
- [x] direct TSPL/ZPL 条码页边界预检计入左边界，拒绝越过页面左沿的 HRI footprint。
- [x] 模板 Provider 对应计入左边界，并保留 `PrintElementValidationException` 元素路径。
- [x] 新增 Profile 非负验证、快照、贴边/越界原子性和 HRI 关闭兼容用例。
- [x] 更新协议和模板文档，说明页面 X 轴语义、输出不变及旋转校准限制。

### R136 未完成 TODO

**Open Actionable**

- [ ] 将 HRI 校准扩展为可表达四向/旋转后的完整边界框，或按角度声明 profile，使旋转条码不复用页面 X 轴左侧值。
- [ ] 校准具体 TSPL/ZPL 字体的 glyph advance、组合字形、字体选择及真实墨迹范围。
- [ ] 完善 `WriteRaw` 受限指令校验、GS1 AI/Stacked/Composite/链接字段、设备容量约束，以及 DesignerHost 隔离和资源/性能/跨平台验证。

**Blocked Approval**

- None。本轮增加可选估算值，默认行为不变，不新增容量阈值或支持承诺。

**Blocked External**

- [ ] 真实 HRI 对齐、左右外伸、字宽及旋转后边界仍需目标机型手册或设备样本校准。

**Not Applicable**

- CPCL/ESC/POS HRI、协议输出字节、包图和版本不受本轮 Profile 预检属性影响。

**Accepted Limitation**

- [ ] 左侧外伸只表示页面 X 轴的保守边界，不改变实际设备字段原点、字体或对齐；旋转条码仍需按设备方向校准。

**Verified Boundary**

- direct 与模板 Provider 的页面 X 轴左侧边界均由 R136 值扩充；L1 `52/52` 与 L2 Core `net6.0` `3103/3103` 通过，零失败。首轮仅出现既有错误文案断言失配，修正断言后精准集通过。

**Deferred**

- 逐机型 HRI 轮廓、字体与旋转校准推迟至提供设备资料后。

### R136 验证状态

- [x] CHANGE IMPACT：TSPL/ZPL Profile、不可变设备契约、公开能力快照、direct 与模板边界预检、Core.Tests、协议/模板文档；风险中等。
- [x] L0：`git diff --check` 通过，Profile/Provider 调用点对齐，本轮触及文件行尾空白扫描无命中。
- [x] L1：`net6.0` `52/52`，零失败、零跳过。
- [x] L2：Core.Tests `net6.0` `3103/3103`，零失败、零跳过。
- [x] 受影响项目构建：Core 与 Templates 的 `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 全部通过，零警告、零错误；Core.Tests `net6.0` 构建通过。
- NU1900：当前环境无法访问 NuGet 漏洞索引；不影响上述构建和测试。
- L3/L4：`NOT_APPLICABLE`；未更改 DesignerHost 契约、包图或项目依赖。
- L5：`BLOCKED_EXTERNAL`；真实 HRI 字体、外伸与旋转后的实物边界需要目标设备。
- [x] No-Progress Check：CHANGED；Host 预检现可表达字段原点左侧 HRI 外伸。

### R136 下一步

后续为旋转方向补充明确的 HRI 边界模型，再从剩余 GS1/原始指令与跨平台 TODO 中选择一项继续。真实 HRI 边界仍需目标设备资料。

---

## R135 本轮：TSPL/ZPL 原生 HRI 横向占位校准

TSPL/ZPL Profile 可声明 `NativeBarcodeHumanReadableReserveWidthDots`，作为 HRI 启用时条码横向边界的最小占位；值会进入不可变设备契约及能力快照，并用于 direct 命令和模板 Provider 预检。空值保持现有条码主体估算，HRI 关闭时不应用宽度；不改变打印字节。该点数从字段原点向右计算，不推断设备字体、对齐或左侧外伸。

### R135 已完成 TODO

- [x] TSPL/ZPL Profile 增加可选 HRI 横向预留宽度，拒绝零和负值并纳入不可变快照与能力描述。
- [x] direct TSPL/ZPL 命令只在 HRI 启用时用校准宽度扩充页面 footprint；HRI 关闭和默认配置保持旧预检行为。
- [x] 模板 Provider 对相同 Profile 校准执行页面宽度检查，保留元素路径错误。
- [x] 增加 Profile 快照、精确贴边、少 1 点越界、HRI 关闭忽略和非法宽度回归用例。
- [x] 更新协议和模板文档，明确字段原点语义与居中 HRI 左侧外伸限制。

### R135 未完成 TODO

**Open Actionable**

- [ ] 补充可配置/可校准的原生 HRI 水平偏移或边界框，覆盖居中文字向字段原点左侧外伸及不同对齐方式。
- [ ] 校准具体 TSPL/ZPL HRI 字体的 glyph advance、组合字形、字体选择及真实墨迹范围。
- [ ] 继续完善 `WriteRaw` 受限指令校验、GS1 AI/Stacked/Composite/链接字段、设备容量约束，以及 DesignerHost 隔离和资源/性能/跨平台验证。

**Blocked Approval**

- None。本轮增加可选 Profile 校准值，默认行为不变，无新增容量阈值或产品支持承诺。

**Blocked External**

- [ ] 真实 HRI 对齐、左侧外伸、字宽和打印边界仍需目标机型手册或设备样本校准。
- [ ] 本轮 L1/L2 构建及测试验证结果见下方验证状态；编译输出若再次被访问拒绝，将保留为环境阻断。

**Not Applicable**

- CPCL/ESC/POS HRI 路径、协议输出字节、包图与版本不受本轮 Profile 估算属性影响。

**Accepted Limitation**

- [ ] 横向占位按字段原点向右计量；居中 HRI 的文字若向左超出原点，调用方需自行留出左边距。
- [ ] Profile 宽度只控制主机边界估算，不设置设备字体、字号或 HRI 对齐，不保证真实墨迹边界。

**Verified Boundary**

- L0 静态检查通过；L1 在编译器写入 `Bing.EasyPrint.xml` 时返回 `CS0016 Access denied`，测试未启动；L2 因 L1 构建未完成而未运行。新回归用例已编写但未执行。

**Deferred**

- 逐机型字体、对齐与打印/扫码校准推迟至提供目标设备资料后。

### R135 验证状态

- [x] CHANGE IMPACT：TSPL/ZPL Profile、不可变 `LabelDeviceContract`、`PrintCapabilities`、direct 与模板页面边界、Core.Tests、协议和模板文档；新增可选公开 Profile/能力属性，默认输出与边界不变；Core 五 TFM；风险中等。
- [x] L0：`git diff --check` 通过；本轮触及文件行尾空白扫描无命中。编译器仍未验证。
- [ ] L1：定向测试未启动；编译器 `CS0016` 无法写入 `src/Bing.EasyPrint/Bing.EasyPrint.xml`，返回 `Access denied`。本轮出现 `NU1900`，当前环境无法访问 NuGet 漏洞索引。
- L2：`BLOCKED_EXTERNAL`；L1 在编译阶段被阻断，未运行 Core 全量回归。
- 多目标框架构建：未运行；L1 已暴露同一输出写入拒绝，不再尝试替换输出目录。
- L3/L4：`NOT_APPLICABLE`；未改 DesignerHost 契约、PackageConsumer 或包图。
- L5：`BLOCKED_EXTERNAL`；真实 HRI 字体与边界需要目标打印机。
- [x] 未终止或干预其他 dotnet 进程，也未重复尝试写入其他输出目录。
- [x] No-Progress Check：CHANGED；Profile 可用目标机型数据明确扩展 TSPL/ZPL 原生 HRI 页面宽度预检。

### R135 下一步

恢复编译输出文件的写入访问后，先运行 R135 L1，再运行 Core `net6.0` L2；随后为居中 HRI 加入可表达左右偏移的校准边界，避免把“总宽”误当成右侧宽度。目标设备实测仍是外部门禁。

---

## R134 本轮：ZPL Code 49 行高与 footprint 校准

ZPL `^B4` 的 `h` 参数是相对 `^BY` 模块宽度的行高倍率，而公开 API 的 `height` 定义为目标点数。本轮保留 API 签名和点数语义，将点数向上换算为整数倍率，并以实际输出行高检查页面边界。Code 49 行宽按本地编码器的 70 模块行宽估算；纵向 footprint 使用本地编码器确定的堆叠行数，并保守计入分隔模块和 HRI 预留；ZPL Code 49 的 90/270 度页面边界使用完整估算高度。

### R134 已完成 TODO

- [x] 修正 ZPL `BarcodeCode49` 的 `height` 点数到 `^B4` 行高倍率换算，向上取整且不改公开签名。
- [x] Code 49 宽度预检从 49 模块改为本地标准行宽 70 模块；高度预检使用本地编码行数、实际行高、分隔条和 HRI 预留。
- [x] direct 命令与模板 Provider 增加倍率换算、贴边/越界、完整宽高、旋转边界和失败原子性回归用例。
- [x] 协议、模板与设计器说明补充 ZPL `^B4` 高度单位、70 模块行宽及固件估算限制；更新模板 golden 字节断言。
- [x] `git diff --check` 通过，新增/未跟踪文件的行尾空白扫描无命中；编译和定向测试因编译器无法写入目标 DLL 被环境阻断，未将未运行测试记为通过。

### R134 未完成 TODO

**Open Actionable**

- [ ] 校准原生 HRI 横向占位、对齐和具体字体字宽；当前 Profile 只校准纵向预留高度。
- [ ] 完善 TSPL font `0`、固定字体和 ZPL 字体的实际 glyph advance/组合字形边界；scalar 固定格数仍只是估值。
- [ ] 评估直接 API 能否为 `WriteRaw` 提供可选的受限指令/边界检查；任意原始指令仍无法结构化预检。
- [ ] 扩展 GS1 AI 元数据、压缩 AI 族、Raster Stacked/Composite 和链接字段；补充具体 Profile 的模块点宽、行数及载体容量约束。
- [ ] 完成 DesignerHost 隔离重建，以及打印资源预算、取消/重试、性能容量和跨平台字库验证。

**Blocked Approval**

- None。本轮只修正既有 ZPL Code 49 参数单位实现与几何预检，没有新增 API 或阈值决策。

**Blocked External**

- [ ] 本轮 Core 构建及定向测试被编译器写入 DLL 时的 `Access denied` 阻断；需要构建输出访问恢复后运行 L0/L1。
- [ ] 目标热敏/针式打印机的 Code 49 行数、实际 HRI footprint、DPI、纸宽、打印和扫码验收仍需具体机型和外部设备。

**Not Applicable**

- CPCL、ESC/POS、TSPL 输出不受本轮 ZPL `^B4` 参数修正影响；包版本、连接管理和设备发布不由本轮触发。

**Accepted Limitation**

- [ ] `height` 不能被 `moduleWidth` 整除时，输出行高向上取整至模块宽度的整数倍，实际单行高度最多比请求值多 `moduleWidth - 1` 点。
- [ ] 主机 Code 49 高度使用本地编码器推导行数与保守分隔预留；打印机固件可能有不同布局，不能替代设备测量。
- [ ] 超过两行时，Zebra Code 49 interpretation line 可能超出条码主体宽度；当前页宽模型仍按主体宽度估算，HRI 水平 footprint 待后续校准。

**Verified Boundary**

- Awaiting L1 execution; new boundary tests are present but not yet verified because the compiler could not write its output assembly.

**Deferred**

- 真实固件行数、HRI 字形与可扫码边界推迟至提供目标机型手册或设备样本后。

### R134 验证状态

- [x] CHANGE IMPACT：ZPL Code 49 命令、共享条码估算、Raster 内部 Code 49 几何复用、Core.Tests 与协议文档；公开签名不变但修正字节参数语义；Core 五目标框架和其测试项目受影响；风险中等。
- [x] L0：`git diff --check` 通过，新增/未跟踪文件行尾空白扫描无命中；Core 编译因目标 DLL 写入 `Access denied` 未完成。
- [ ] L1：`ZplCode49ProviderTests` 与 `ProtocolCommandPageBoundsTests` 执行被 `CS2012` 拒绝写入 `Bing.EasyPrint.dll` 阻断；未进入测试运行。
- L2：`BLOCKED_EXTERNAL`；L1 未能启动，且该轮共用几何行为未获运行时回归证据。
- L3/L4：`NOT_APPLICABLE`；本轮未改模板协议选择、DesignerHost 契约或包图。
- L5：`BLOCKED_EXTERNAL`；真实设备验收需要目标机型。
- [x] 环境诊断：项目输出目录和本轮专用用户输出目录均出现编译器 `Access denied`；未终止或干预其它 dotnet 进程，也未继续重复相同输出写入实验。
- [x] No-Progress Check：CHANGED；源码和回归用例已新增，根因范围及实现语义得到协议手册与本地编码器核实。

### R134 下一步

恢复构建输出可写后先运行本轮 L1，再运行 Core `net6.0` L2；随后优先处理原生 HRI 横向 footprint 与字体度量。不得把固件外部验收缺失转成无证据的本地代码修复。

---

## R133 本轮：TSPL/ZPL direct HRI 预留高度校准

TSPL/ZPL Profile 新增 `NativeBarcodeHumanReadableReserveHeightDots`，用于 direct 结构化条码命令的页面边界估算。调用方可按设备手册或实测填写正数点值；空值沿用历史 24 点主机估值，保持原有边界行为。该配置不改变条码命令字节，也不会设置打印机实际字体。TSPL 手册未承诺固定 HRI 行高，ZPL HRI 字体受当前字体命令影响，因此通用值只表示主机估算，不代表实际墨迹保证。

### R133 已完成 TODO

- [x] TSPL/ZPL Profile 增加可选 HRI 预留高度校准值，并纳入不可变命令快照。
- [x] 拒绝零或负数预留高度；正数只参与启用 HRI 时的 direct 条码页面 footprint 估算。
- [x] 回归验证两协议的配置高度贴边通过、少 1 点越界、失败不追加输出和 Profile 快照隔离。
- [x] 定向测试 `net6.0` `37/37`、Core 全量测试 `net6.0` `3083/3083`；Core 五目标框架构建全部通过，零警告、零错误。
- [x] 更新协议说明、验证记录和当前 TODO。

### R133 未完成 TODO

**Open Actionable**

- [ ] 继续完善 direct TSPL font `0`、固定字体和 ZPL 字体的实际 glyph advance/组合字形边界；scalar 固定格数仍只是估值。
- [ ] 校准原生 HRI 的横向占位、对齐和具体字体字宽；当前配置只校准纵向预留高度。
- [ ] 审核 ZPL Code 49 的 `^B4` 行高参数单位，并令输出参数语义与页面 footprint 估值一致。
- [ ] 评估直接 API 能否为 `WriteRaw` 提供可选的受限指令/边界检查；任意原始指令仍无法进行结构化几何预检。
- [ ] 扩展 GS1 AI 元数据、压缩 AI 族、Raster Stacked/Composite 和链接字段；补充具体 Profile 的模块点宽、行数及载体容量约束。
- [ ] 完成 DesignerHost 隔离重建，以及打印资源预算、取消/重试、性能容量和跨平台字库验证。

**Blocked Approval**

- None。本轮只增加可选 Profile 属性，没有新增需人工批准的 API 或容量阈值。

**Blocked External**

- [ ] 目标热敏/针式打印机的方向/参考点、字体墨迹、HRI 实际占位、条码 footprint、纸宽/DPI 校准、打印和扫码验收仍需具体机型与外部设备。

**Not Applicable**

- 模板 Provider 布局、ESC/POS/CPCL 输出、协议命令字节、包版本和真实设备联调不由本轮直接 HRI 估值校准触发。

**Accepted Limitation**

- [ ] 未填写 Profile 时 direct TSPL/ZPL HRI 仍沿用兼容性 24 点估值；它不代表设备字体的标准高度。
- [ ] Profile 高度仅校准 direct 页边界纵向 footprint；原生 HRI 横向字宽和位置仍未建模。
- [ ] TSPL/ZPL Native 文字、条码和二维码边界仍依赖主机估值；字体替换、组合字形、模块比例和固件可能改变实际墨迹。
- [ ] `WriteRaw`/任意协议注入不参与结构化页面边界预检；模板 Provider 继续由模板层的布局、Profile 和声明占位检查负责。
- [ ] 当前环境测试构建可能报告 `NU1900`，原因是无法访问 NuGet 漏洞索引；本轮全量测试通过，Core 构建无警告。

**Verified Boundary**

- [x] 两协议配置 8 点 HRI 保留高度后，40 点条高在 48 点页面贴边通过，在 47 点页面拒绝；拒绝不追加协议输出。
- [x] 未配置 Profile 时保留 24 点历史估值；命令构造后修改公开 Profile 不会改变命令内部高度快照；HRI 关闭时不使用 HRI 预留高度。

**Deferred**

- [ ] 具体字体、HRI 字宽和方向/参考点实测校准推迟至提供目标型号手册或设备样本后。

### R133 验证状态

- [x] CHANGE IMPACT：TSPL/ZPL Profile、共享 `LabelDeviceContract`、direct 条码页边界估算与 Core.Tests；新增可选公开属性，不改变输出字节、模板 Provider 或包图；中风险。
- [x] L0：Core `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 构建全部通过，零警告、零错误。
- [x] L1：页面边界和 HRI Profile 定向测试 `net6.0` `37/37`，零失败、零跳过。
- [x] L2：Core 全量测试 `net6.0` `3083/3083`，零失败、零跳过。
- L3/L4：`NOT_APPLICABLE`；本轮仅增加 Core direct-command 估值属性，未改变模板、DesignerHost、PackageConsumer 或 NuGet 包。
- L5：`BLOCKED_EXTERNAL`；真实 HRI 高度、字形、打印质量及扫码验收需要目标设备。
- [x] NuGet 漏洞索引不可达导致测试显示既有 `NU1900`；测试结果通过。
- [x] No-Progress Check：CHANGED；机型资料现在可校准 direct HRI 纵向 footprint，且保留无配置时旧边界行为。

### R133 下一步

优先核验 ZPL Code 49 `^B4` 的行高参数与公共 `barHeight` 单位映射；之后处理 direct 原生 HRI 横向字体占位。实际设备字高和字宽仍需要机型手册或实测数据。

## R132 本轮：TSPL/ZPL 补充 Unicode 文本边界估算

TSPL 和 ZPL direct 原生文字页边界预检现按 Unicode scalar 计数，不再把一个 supplementary-plane 字符的 UTF-16 代理对当成两个字符。TSPL 继续按当前主机估算规则将 ASCII 计一格、非 ASCII 标量计两格；ZPL 按每个标量乘请求的字段宽度。只改变预检范围，不改变有效命令的协议字节；字体墨迹和设备字库仍须用 Profile/实机资料确认。

### R132 已完成 TODO

- [x] TSPL 固定字体估算改用项目共享的 Unicode scalar 枚举，避免补充平面字符双计。
- [x] 同步修正 ZPL `Text` 的 UTF-16 长度估算，使字段宽度按 Unicode scalar 计算。
- [x] 增加 TSPL/ZPL 回归：补充平面字符在右边界恰好贴边时通过，超出一格时明确报“估算”且失败不追加输出。
- [x] `ProtocolCommandPageBoundsTests` 定向测试 `net6.0` `20/20`；Core 五目标框架构建通过，零警告、零错误。
- [x] 更新协议说明、验证记录和当前 TODO。

### R132 未完成 TODO

**Open Actionable**

- [ ] 继续完善 direct TSPL font `0`、固定字体和 ZPL 字体的主机 footprint；当前宽度仍是每 scalar 固定一格/两格，不能代表真实字体 advance 或组合字形墨迹。
- [ ] 继续细化 direct 原生 HRI 的水平占位、协议默认字体与不同条码的实际可读文字布局；目前高度使用通用估算，仍可能与固件不同。
- [ ] 评估直接 API 能否为 `WriteRaw` 提供可选的受限指令/边界检查；目前任意原始指令无法被结构化几何预检解析。
- [ ] 继续扩展 GS1 AI 元数据、压缩 AI 族、Raster Stacked/Composite 和链接字段；补充具体 Profile 的符号模块数/点宽/行数容量约束。
- [ ] 完成 DesignerHost 隔离重建，以及打印资源预算、取消/重试、性能容量和跨平台字库验证。

**Blocked Approval**

- None。本轮未新增需人工批准的 API 或容量阈值决策。

**Blocked External**

- [ ] 目标热敏/针式打印机的方向/参考点、字体墨迹、条码/HRI footprint、纸宽/DPI 校准、真实打印和扫码验收需要具体机型与外部设备。

**Not Applicable**

- 模板 Provider 输出、ESC/POS、CPCL、协议字节格式和包版本不在本轮改动范围。

**Accepted Limitation**

- TSPL/ZPL Native 文字、条码和二维码边界仍是保守主机估算；字体替换、组合字形、HRI 和固件编码模式可能改变实际墨迹。
- `WriteRaw`/任意协议注入不参与结构化页面边界预检。
- 模板 Provider 继续由模板层的布局、Profile 和声明占位检查负责；direct API 预检不会替代模板诊断。
- 当前环境测试构建可能报告 `NU1900`，原因是无法访问 NuGet 漏洞索引；本轮 Core 构建没有警告。

**Verified Boundary**

- [x] 对一个有效 supplementary-plane scalar，TSPL 按一个非 ASCII 标量计两格，ZPL 按一个 scalar 乘请求字段宽度；代理对不再重复计数。

**Deferred**

- [ ] 字体/HRI 的物理边界校准推迟到获得具体打印机型号、手册或实测样本后进行；不影响本地主机估算回归。

### R132 验证状态

- [x] CHANGE IMPACT：Core TSPL/ZPL direct 原生文字页边界估算与 Core.Tests；无公开签名、命令字节、模板 Provider 或包图变化；中风险。
- [x] L0：Core 的 `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 构建全部通过，零警告、零错误。
- [x] L1：`ProtocolCommandPageBoundsTests` `net6.0` `20/20`，零失败、零跳过。
- L2：`NOT_RUN`；本轮未重跑 Core 全量，R130 全量结果仅作历史记录，不作为 R132 候选的全量通过证据。
- L3/L4：`NOT_APPLICABLE`；模板/consumer 与包验证未由本轮范围触发。
- L5：`BLOCKED_EXTERNAL`；真实设备验收需要目标型号和外部打印机。
- [x] NuGet 审计索引不可达导致定向测试显示既有 `NU1900`；不影响测试结果。
- [x] No-Progress Check：CHANGED；TSPL/ZPL 文本估算现在与项目的 Unicode scalar Profile 校验语义一致。

### R132 下一步

下一轮聚焦 direct 原生 HRI 的估值边界，先核实 TSPL/ZPL 手册中的默认可读字体、定位与输出尺寸规则，再决定能否提供 Profile 校准入口；未知机型仍保持保守估算并显式说明。

## R131 本轮：ZPL 整页方向标准校验

ZPL `^PO` 只表示整页正常或 180 度反向；`ZplMediaOptions.Orientation` 现仅接受 `None`/`Down`，对 `Left`/`Right` 和未定义枚举值在创建命令时严格抛错，不再生成无效 `^POB`/`^POR`。ZPL 元素自己的字段旋转仍由对应 `^A`、条码或二维码命令表达。本轮未改变公开签名或默认输出。

### R131 已完成 TODO

- [x] 将 ZPL 整页方向限制为 `^PO` 标准支持的 `N`/`I`，并提供带参数名及原因的校验异常。
- [x] 用 `Down -> ^POI` 覆盖有效反向输出；覆盖 `Left`、`Right` 和非法枚举在输出前被拒绝。
- [x] 保持省略选项时的既有默认字节，并回归 ZPL 页面边界方向逻辑。
- [x] 定向测试 `net6.0` `27/27`；Core 五目标框架构建通过，零警告、零错误。
- [x] 更新协议说明、验证记录和本进度 TODO。

### R131 未完成 TODO

**Open Actionable**

- [x] TSPL/ZPL 对补充平面 Unicode 字符使用 scalar 而非 UTF-16 单元计数见 R132；固定字体/字体 `0` 实际 glyph advance 与 HRI footprint 仍待后续。
- [ ] 评估直接 API 能否为 `WriteRaw` 提供可选的受限指令/边界检查；目前任意原始指令无法被结构化几何预检解析。
- [ ] 继续扩展 GS1 AI 元数据、压缩 AI 族、Raster Stacked/Composite 和链接字段；补充具体 Profile 的符号模块数/点宽/行数容量约束。
- [ ] 完成 DesignerHost 隔离重建，以及打印资源预算、取消/重试、性能容量和跨平台字库验证。

**Blocked External**

- [ ] 目标热敏/针式打印机的方向/参考点、字体墨迹、条码 footprint、纸宽/DPI 校准、实际打印和扫码验收需要具体机型与外部设备。

**Accepted Limitation**

- [ ] Native 文字/条码/二维码边界基于保守主机估算；不同字体、固件、模块比例和 HRI 实现可能产生不同的实际墨迹范围。
- [ ] `WriteRaw`/任意协议注入不参与结构化页面边界预检。
- [ ] 模板 Provider 继续由模板层的布局、Profile 和声明占位检查负责；direct API 预检不会替代模板诊断。
- [ ] 当前环境的测试构建可能报告 `NU1900`，原因是无法访问 NuGet 漏洞索引；本轮目标框架构建未产生警告。

### R131 验证状态

- [x] CHANGE IMPACT：Core ZPL 选项校验与整页方向输出、ZPL 媒体选项测试、协议/进度文档；无公开签名变化；中风险。
- [x] L1：`ZplMediaOptionsTests` 与 `ProtocolCommandPageBoundsTests` 定向测试 `27/27`；L0：Core 的 `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 均构建通过。
- [x] 本轮复用 R130 的全量测试证据；无跨协议实现变化，因此未重跑完整 Core 套件。NuGet 审计索引不可达警告不影响测试通过。
- [x] Verified Boundaries：默认不输出 `^PO`；显式 `None`/`Down` 仅输出 `^PON`/`^POI`；`Left`/`Right` 与未知值构造期失败；条目级旋转仍走独立 ZPL 字段方向。
- [x] No-Progress Check：CHANGED；消除了 ZPL 整页方向的无效指令映射与静默错印风险。
- [ ] Deferred：设备级整页方向、字体墨迹、纸张校准和真实打印/扫码验收。

### R131 下一步

优先继续细化 direct TSPL/ZPL 边界估算的固定字体、非 ASCII 字符和 HRI footprint，并将无法实测的限制保留为估算；有具体设备手册后再增加 Profile 物理容量约束。

## R130 本轮：TSPL/ZPL 直接命令页面边界预检

TSPL `Size` 与 ZPL `Label` 成功后会保存标签尺寸。直接结构化命令在写入前检查文字、条码、二维码、线、矩形和光栅元素的页面 footprint；越界拒绝不会追加命令。TSPL 毫米标签尺寸按当前 DPI 换算成 dots，`REFERENCE`/`DIRECTION` 和 ZPL 页面方向参与坐标框变换。Native 字体、二维码和条码 footprint 属于主机估算，错误信息会标明“估算”；Bitmap/GraphicField、线和矩形使用实际输出尺寸。未设置标签尺寸的直接命令继续保持原行为。模板 Provider 使用模板层已有布局与边界检查，不复用 direct 命令的字体估算，以保持模板输出兼容。

### R130 已完成 TODO

- [x] 增加 TSPL/ZPL 共享页面矩形预检，并记录 `SIZE`/`^PW`、`^LL` 页尺寸。
- [x] 接入直接文字、原生条码、二维码、线/线框、填充矩形、TSPL Bitmap 与 ZPL GraphicField；覆盖旋转、TSPL DPI/毫米尺寸、参考点/方向和页面方向。
- [x] Native footprint 估算在异常中明确标注；Code39/Code93 等缺少协议专用宽度公式的类型仅在 direct 预检中回退到主机编码宽度，不改变模板宽度度量。
- [x] 覆盖精确贴边、越界失败不追加输出、TSPL font `0` point 到 dots 转换、原生估算、光栅尺寸及无尺寸兼容路径。
- [x] Core 全量测试 `net6.0`、`netcoreapp3.1` 各 `3068/3068`；Core 五目标框架构建零警告、零错误。

### R130 未完成 TODO

**Open Actionable**

- [x] ZPL `^PO` 与整页方向枚举不匹配的问题已在 R131 解决：`Left`/`Right` 改为严格拒绝，`None`/`Down` 映射到 `N`/`I`。
- [ ] 继续完善 TSPL font `0`、固定字体、非 ASCII 字形、原生 HRI 和各条码制式的边界估算；当前结果是主机估算，不是具体固件的实际墨迹边界。
- [ ] 评估直接 API 能否为 `WriteRaw` 提供可选的受限指令/边界检查；目前任意原始指令无法被结构化几何预检解析。
- [ ] 继续扩展 GS1 AI 元数据、压缩 AI 族、Raster Stacked/Composite 和链接字段；补充具体 Profile 的符号模块数/点宽/行数容量约束。
- [ ] 完成 DesignerHost 隔离重建，以及打印资源预算、取消/重试、性能容量和跨平台字库验证。

**Blocked External**

- [ ] 目标热敏/针式打印机的方向/参考点、字体墨迹、条码 footprint、纸宽/DPI 校准、实际打印和扫码验收需要具体机型与外部设备。

**Accepted Limitation**

- [ ] Native 文字/条码/二维码边界基于保守主机估算；不同字体、固件、模块比例和 HRI 实现可能产生不同的实际墨迹范围。
- [ ] `WriteRaw`/任意协议注入不参与结构化页面边界预检。
- [ ] 模板 Provider 继续由模板层的布局、Profile 和声明占位检查负责；direct API 预检不会替代模板诊断。

### R130 验证状态

- [x] CHANGE IMPACT：Core TSPL/ZPL direct 命令与共享原生条码估算；模板 Provider 显式保留模板层检查；无公开签名变化；中风险。
- [x] L1：边界/字体/Provider 专项 `175/175`；L2：Core 全量 `net6.0`、`netcoreapp3.1` 各 `3068/3068`；L0：五目标框架通过。
- [x] `NU1900` 仅表示当前环境无法访问 NuGet 漏洞索引；实机打印、固件方向和扫描率未验证。
- [x] No-Progress Check：CHANGED；新增 TSPL/ZPL 直接结构化绘制边界保护和失败原子性。

## R129 本轮：GS1 Scan4Transport AI 子集

共享 AI 元数据解析器增加 AI `4300`–`4326` 的字段长度、数字/CSET 82 字符规则及 GS1 变量字段 FNC1 规范化。Scan4Transport 字段必须与 SSCC（AI `00`）一起使用；收货地址/回运地址第二行 AI `4303`/`4313` 分别要求对应第一行 `4302`/`4312`。这让 ESC/POS Native、ESC/POS Raster GS1-128、TSPL EAN128 和 ZPL GS1-128 复用同一份输入规范化结果。规范依据：[GS1 General Specifications 26.0](https://ref.gs1.org/standards/genspecs/)、[GS1 Application Identifiers](https://ref.gs1.org/ai/)及 Scan4Transport 关联规则变更 [GSCN 25-306](https://ref.gs1.org/standards/genspecs/gscn/2025/GSCN-25-306-MandAssoc03.pdf)。

### R129 已完成 TODO

- [x] 补齐 `4300`–`4326` 已分配 Scan4Transport AI 的固定/可变长度、数字/CSET 82 校验和 FNC1 边界。
- [x] 严格验证 Scan4Transport 与 SSCC 关联，以及收货/回运地址第二行对第一行的依赖；失败在输出前抛错。
- [x] 增加字段格式矩阵、非法值、长度边界、AI 关联和 Native/Raster/TSPL/ZPL GS1-128 共用输出测试。
- [x] 定向回归 `net6.0` `146/146`；Core 全量测试 `net6.0` 与 `netcoreapp3.1` 各 `3050/3050`，零失败、零跳过。
- [x] Core 与 Templates 五目标框架构建通过，零警告、零错误；测试构建仍报告当前环境既有 NuGet 漏洞数据源不可达 `NU1900`。
- [x] 更新协议说明、验证记录和本进度 TODO。

### R129 未完成 TODO

**Open Actionable**

- [x] TSPL/ZPL 直接命令设置页面尺寸后的结构化绘制边界预检见 R130；Native 估算与硬件边界校准仍待后续。
- [ ] 继续按独立 GS1 AI 家族扩展共享元数据及其标准长度、字符集、关联规则和 FNC1 回归；当前仍不是完整 GS1 AI 字典。
- [ ] 增加其它压缩 AI 家族以及 Raster Stacked/Composite、链接字段的编码支持和能力预检。
- [ ] 有厂商资料时为具体协议/固件补充符号模块数、点宽、行数或编码模式容量约束；字符数 Profile 不能代替这些物理约束。
- [ ] 完成 DesignerHost 隔离重建，以及打印资源预算、取消/重试、性能容量和跨平台字库的验证。

**Blocked External**

- [ ] 目标热敏/针式打印机的 Scan4Transport 固件输出、实际纸宽/DPI 校准、标签可读性与扫描验收需要机型资料和外部设备。

**Accepted Limitation**

- [ ] AI `4307`/`4317` 只检查两个大写 ASCII 字母的形状，不查 ISO 国家/地区代码注册表；AI `4324`/`4325`/`4326` 只检查规定数字长度，不做日期、时区或日历语义校验。
- [ ] 文本按 GS1 字符集验证；调用方负责依 GS1 规则预编码空格/扩展文本，解析器不解码传输表示。
- [ ] AI `4300`–`4326` 的关联检查仅覆盖本轮明确列出的 SSCC 与地址行规则，不声称验证全部业务关系。
- [ ] `BpTemplate` 条码字段仍采用原始 AI/FNC1 数据；括号 AI 展示语法仅在直接协议命令路径解析。

### R129 验证状态

- [x] CHANGE IMPACT：共享 GS1 解析器；ESC/POS Native/Raster、TSPL、ZPL 直接条码命令路径；新增测试文件；没有公开 API 签名变化；中风险。
- [x] L1：GS1/Native/TSPL/ZPL 定向回归 `146/146`；L2：Core `net6.0`、`netcoreapp3.1` 各 `3050/3050`；L0：Core 与 Templates 五目标框架构建全部通过。
- [x] 当前测试未验证机型固件、实物打印、纸张校准或扫描率；这些作为 `BLOCKED_EXTERNAL` 保留。
- [x] No-Progress Check：CHANGED；新增一组有标准依据的运输字段、关联验证和跨协议输出覆盖。

## R128 本轮：GS1 设备 payload 容量预检

ESC/POS、TSPL、ZPL Profile 现在可声明手册确认的 GS1 线性条码 payload 字符上限；ESC/POS 与 ZPL 还可为 DataBar Expanded 单独声明上限。配置会进入不可变命令/能力快照。直接命令在写入前校验；Raster Expanded 在绘制前校验；共享模板 Provider 在输出前校验并保留元素路径。括号 AI 先规范化，字符数包含 ASCII 29/FNC1 分隔符，不含协议 framing。Profile 不配置上限时不推断未知固件容量；Raster Expanded 原有 77 字符输入边界与各协议既有 255 字符结构化输入上限仍有效。

### R128 已完成 TODO

- [x] 为 ESC/POS、TSPL、ZPL 增加可选 GS1-128 payload 上限；为具备 Expanded Provider 的 ESC/POS、ZPL 增加独立 DataBar Expanded 上限，并拒绝零/负值。
- [x] 在命令 Profile 快照、`PrintCapabilities` 和模板能力中传播配置；Profile 修改不影响已创建命令。
- [x] Native 直接命令、Raster 条码绘制和模板 Provider 在输出前严格拒绝超限输入；模板异常保留条码元素路径，失败不污染指令或画布。
- [x] 新增规范化长度、FNC1/ASCII 29 计数、Profile 快照、模板预检、Raster 失败恢复及正数约束测试。
- [x] 定向协议回归 `net6.0` `229/229`；Core 全量测试 `net6.0` 与 `netcoreapp3.1` 各 `3014/3014`，均零失败、零跳过。
- [x] Core 与 Templates 五目标框架构建成功，零警告、零错误；测试构建出现既有 NuGet 漏洞数据源不可达 `NU1900` 警告。

### R128 未完成 TODO

**Open Actionable**

- [ ] 容量配置当前按规范化 payload 字符数表达；若厂商资料给出符号模块数、实际点宽、行数或编码模式上限，需为对应制式补充可计算的设备约束，并以具体协议/固件样本验证。
- [ ] 扩展共享 AI 元数据子集并逐项添加标准长度、字符集和 FNC1 边界回归；目前仍未覆盖完整 GS1 AI 字典及所有行业字段。
- [ ] 增加其它压缩 AI 家族以及 Raster Stacked/Composite、链接字段的编码支持和能力预检。
- [ ] 完成 DesignerHost 隔离重建，以及打印资源预算、取消/重试、性能容量和跨平台字库的验证。

**Blocked External**

- [ ] 目标热敏/针式打印机的 GS1 字段容量、固件输出、HRI、DPI/纸宽校准和实际扫码验收需要外部设备与机型资料。

**Accepted Limitation**

- [ ] 未配置手册确认的 Profile 上限时，不推断未知固件的 GS1 容量；Profile 的字符上限不等于扫码成功或物理符号可打印保证。
- [ ] `BpTemplate` 条码字段继续使用原始 AI/FNC1 数据；括号展示语法仅由直接协议命令路径解析。
- [ ] 金额/折扣 AI 的货币清单、小数/业务值和字段组合语义不由共享语法解析器验证。

### R128 验证状态

- [x] CHANGE IMPACT：GS1 容量 helper、ESC/POS/TSPL/ZPL Profile 与命令、公共能力、模板 Provider；公开配置/API 扩展；中风险。
- [x] L1：GS1/Native/TSPL/ZPL 定向 `229/229`；L2：Core `net6.0`、`netcoreapp3.1` 各 `3014/3014`；L0：Core/Templates 五目标框架构建通过。
- [x] 本地测试不验证物理打印、符号读取率或设备固件对 profile 值的真实行为。
- [x] No-Progress Check：CHANGED；新增基于设备显式声明值的跨协议 payload 预检，不以通用字符上限替代未知固件事实。

## R127 本轮：括号式 GS1 字段中的字面左括号

直接命令的括号 AI 值可用 `\(` 表示字面左括号；解析时先解码，再验证 CSET82 和字段长度。多元素变量字段仍在后续 AI 前写入 FNC1；无反斜杠的历史单元素展示输入继续保持兼容路径。Raster Expanded 的原始通用字段按 CSET82 接受圆括号，HRI 去掉展示转义符。

### R127 已完成 TODO

- [x] 解析圆括号与方括号 AI 输入中的 `\(`；单字段显式转义会触发严格解析，未识别的反斜杠序列拒绝，字段长度按解码后的实际数据计算。
- [x] ESC/POS Native GS1-128 与 Raster GS1-128、TSPL EAN128、ZPL GS1-128 输出解码后的字面圆括号；变量字段后的 FNC1 边界保持正确。
- [x] Raster Expanded Method 2/GTIN 通用字段接受规范的 CSET82 圆括号，仍拒绝不在 CSET82 的方括号；HRI 显示解码文本而不显示反斜杠。
- [x] 定向测试 `net6.0` `190/190`；Core 完整测试 `net6.0` 与 `netcoreapp3.1` 各 `3007/3007`；Core/Templates 五目标框架构建成功，零警告、零错误。
- [x] 更新协议、验证及 TODO 文档。

### R127 未完成 TODO

**Open Actionable**

- [ ] 按协议和设备配置建立 GS1 符号容量预检；AI 字段元数据上限不等于每个 GS1-128/DataBar 固件都能编码该字段，Raster Expanded 还受独立 77 字符总输入限制。
- [ ] 扩展共享 AI 元数据子集并逐项添加标准长度、字符集和 FNC1 边界回归；目前仍未覆盖完整 GS1 AI 字典及所有行业字段。
- [ ] 增加其它压缩 AI 家族以及 Raster Stacked/Composite、链接字段的编码支持和能力预检。
- [ ] 完成 DesignerHost 隔离重建，以及打印资源预算、取消/重试、性能容量和跨平台字库的验证。

**Blocked External**

- [ ] 目标热敏/针式打印机的 GS1 字段容量、固件输出、HRI、DPI/纸宽校准和实际扫码验收需要外部设备。

**Accepted Limitation**

- [ ] `BpTemplate` 条码字段保持原始 AI/FNC1 输入；括号展示语法及 `\(` 转义只由直接协议命令路径解析。
- [ ] 金额/折扣 AI 的货币清单、小数/业务值和字段组合语义不由共享语法解析器验证。

**Deferred**

- [ ] 扩展为完整 GS1 语义词典前，继续按常用 AI 子集严格支持；后续工作应先拆分 AI 家族和各载体编码范围，再分别验收。

### R127 验证状态

- [x] CHANGE IMPACT：共享 GS1 解析器，ESC/POS Native/Raster、TSPL、ZPL 输出前路径；无公开签名变化；中风险。
- [x] L1：定向 `190/190`；L2：Core `net6.0`、`netcoreapp3.1` 各 `3007/3007`；L0：Core/Templates 五目标构建全部成功。
- [x] 测试的 NuGet 漏洞索引出现 `NU1900` 网络警告；没有测试失败。真实设备属于 `BLOCKED_EXTERNAL`，未以本地测试替代。
- [x] No-Progress Check：CHANGED；本轮关闭新的括号转义根因，不重复 R126 的已关闭长度/字符集修正。

## R126 本轮：GS1 公司内部 AI 长度与字符集边界

本轮按 GS1 字段定义修正共享 AI 元数据：AI `90` 接受 1–30 个 CSET82 字符；AI `91`–`99` 接受 1–90 个 CSET82 字符。变量字段后还有 AI 时仍插入 FNC1。CSET82 校验不再错误地接受空格；字段元数据上限不等价于 GS1-128/DataBar 符号容量，Raster Expanded 仍受现有 77 字符输入上限约束。

### R126 已完成 TODO

- [x] Native GS1-128 覆盖 AI `90`、`91`、`99` 最大字段长度、超长拒绝、后继定长 AI 的 FNC1 和失败不污染输出；TSPL、ZPL 覆盖 AI `91` 的 90 字符字段。
- [x] 修正 CSET82 常量并验证 82 字符集合长度、现有括号格式可表达字符全部通过，以及空格和 `#` 拒绝。
- [x] Raster Expanded 验证共享解析器仍接受协议字段元数据上限，并保留 Raster 自己的 77 字符总输入边界。
- [x] 协议定向测试 `net6.0` `175/175`；Core 全量 `net6.0` 与 `netcoreapp3.1` 各 `2992/2992`；Core/Templates 五目标框架构建成功，零警告、零错误。
- [x] 更新协议说明、验证记录和当前 TODO。

### R126 未完成 TODO

- [ ] 括号 AI 展示语法把值中的 `(` 当成下一字段起点，因此虽然该字符属于 CSET82，当前结构化括号输入不能表达它。需定义无歧义的转义或结构化字段 API，并覆盖与 `(AI)` 形态冲突的字面文本。
- [ ] AI 单字段元数据长度与各条码载体/固件可编码容量尚未形成统一预检契约；例如 Native 接受字段结构上限不代表所有设备都能编码或打印该符号，Raster Expanded 还受 77 字符总输入上限约束。
- [ ] GS1 完整 AI 字典、其它重量/价格/物流/医疗/资产及行业 AI、业务值与字段组合语义仍未实现；`391n` 货币代码/小数语义、`394n` 折扣范围和 `395n` 单位价格规则仍不校验。
- [ ] 其它压缩 AI、GS1 DataBar Stacked/Composite、链接字段，以及不同固件的原生压缩行为仍未实现或验证。
- [ ] ESC/POS Native 固件 payload/HRI 矩阵、字体/DPI/纸宽校准和真实热敏/针式设备打印与扫码验收仍需外部证据。
- [ ] DesignerHost 隔离重建、性能容量、取消/重试及跨平台字库历史验证仍未完成。

### R126 本轮限制

- [x] 按 GS1 General Specifications 26.0 核对 AI `90`/`91`–`99` 字段长度；CSET82 的空格排除规则由 GS1 21.0.1 明确说明，字符集表参考 GS1 24.0。
- [x] 本轮修正的是语法、字符集和协议数据生成边界，不把字段通过校验描述成符号容量、固件支持、打印质量或扫码验收通过。
- [x] 测试出现既有 `NU1900`，原因是环境无法访问 NuGet 漏洞索引；测试与构建本身通过。

## R125 本轮：GS1 金额/折扣 AI 子集

本轮扩大共享括号 AI 元数据解析器，支持 `390n`/`391n` 金额以及 `394n`/`395n` 折扣/单位价格字段，`n=0..9`。变长字段在后续 AI 前插入 FNC1；字段长度、ASCII 数字、三位货币码字段结构和未知 AI 均严格校验。ESC/POS Native、TSPL、ZPL 直接命令和模板 Provider 共用支持结果；Raster DataBar Expanded 对 GTIN 后的新 AI 按通用 Expanded 字段编码处理，旧 `3920`–`3923`/`3930`–`3933` 压缩映射范围保持不变。

### R125 已完成 TODO

- [x] 增加 `390n` 变长 1–15 位数字、`391n` 变长 3 位货币代码加 1–15 位金额、`394n` 固定 4 位数字、`395n` 固定 6 位数字定义，`n` 均覆盖 0–9。
- [x] 对 `391n` 强制货币码后至少有一位金额；不放宽现有 `392n`/`393n` 的 Raster 压缩后缀子集。`390n`/`391n` 按变量字段分隔，`394n`/`395n` 按定长字段直拼。
- [x] ESC/POS Native、TSPL、ZPL 命令与模板验证规范化/FNC1 输出；Raster Expanded 验证 GTIN 加四类 AI 与等价原始 FNC1 字段生成同一位流。
- [x] 验证最大长度、固定字段错误长度、非数字、未知 AI 和失败不污染输出；定向回归 `net6.0` `175/175`。
- [x] Core 全量测试 `net6.0` 与 `netcoreapp3.1` 各 `2981/2981`；Core/Templates 五目标框架构建零警告、零错误。
- [x] 更新协议、模板、验证记录与本进度文档。

### R125 未完成 TODO

- [ ] GS1 完整 AI 字典及更多重量、价格、物流、医疗、资产和行业 AI 仍未纳入；当前不是完整通用 GS1 解析器。
- [ ] 不验证 `391n` 三位货币码是否为 ISO 4217，有效金额的小数位语义也不依据 AI 后缀 `n` 计算；不验证 `394n` 折扣范围、`395n` 单位价格语义和 AI 业务组合约束。
- [ ] `90`–`99` 公司内部 AI 的范围/接入策略、厂商扩展 AI 的注册边界和完整字符集规则仍需明确。
- [ ] 其它压缩 AI、GS1 DataBar Stacked/Composite、链接字段和不同固件的原生压缩解释仍未实现或验证。
- [ ] ESC/POS Native 固件 payload/HRI 组合矩阵、字体/DPI/纸宽校准与真实热敏/针式设备打印和扫码验收仍需独立证据；本轮没有设备条件。
- [ ] DesignerHost 隔离重建、性能容量、取消/重试和跨平台字库的历史验证 TODO 仍未完成。

### R125 本轮限制

- [x] 本地只实现字段语法，不解释 AI 业务值；打印机固件可能对压缩、金额呈现和 HRI 有不同支持。
- [x] `BpTemplate` 使用原始 AI/FNC1 条码字段；括号/方括号结构化输入由直接协议命令解析器处理，模板字段格式未扩展。
- [x] 设备打印和读取率没有在本地测试中模拟或宣称通过。

## R124 本轮：ESC/POS Raster Code 49 完整符号几何

本轮在 R123 的 Code 49 矩阵编码上补齐 USS-49 的 10X 左静区、70X 条空区、1X 右静区和顶/底边界分隔条。Raster `PrintBarcodeElement` 坐标指向完整符号外框左上角；行条从外框内缩 10X 和 1X，符号高度按每行条高及顶部、行间、底部 1X 分隔条测量。旋转、HRI 居中、静区占用与页边界预检使用相同完整尺寸。

### R124 已完成 TODO

- [x] 绘制横跨 81X footprint 的顶/底边界分隔条，保留 70X 行矩阵和 1X 行间分隔条；行区域中的固有左右静区保持无墨，边界条按标准延伸穿过静区。
- [x] Raster Code 49 宽度/高度测量、旋转画布、HRI 居中与旋转后的 HRI 预留页宽、显式静区占位和页面边界统一使用完整符号几何；内置左右静区也参与元素重叠预检，并随 90/180/270 度旋转换算。
- [x] 扩展 Code 49 golden 和页面边界回归，并更新协议、模板及进度文档。
- [x] Code49 专项回归 `net6.0` `15/15`；Core 全量 `net6.0` 与 `netcoreapp3.1` 各 `2972/2972`；Core 与 Templates 五目标框架构建成功、零警告零错误。测试阶段仅出现既有 NuGet 漏洞数据源不可达 `NU1900` 提示。

### R124 未完成 TODO

- [ ] 完整 GS1 AI 字典、其它压缩 AI 家族、小数/业务关联规则、厂商扩展 AI 和原生协议压缩位流仍未实现。
- [ ] ESC/POS Native 固件 payload/HRI 组合、DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试和跨平台字库仍需独立验证。
- [ ] 真实热敏/针式设备打印与扫码验收未执行；主机 golden 不代表目标设备支持、符号等级或读取率。

## R123 本轮：ESC/POS Raster Code 49 编码接入

本轮新增 Code 49 主机编码器，并接入 ESC/POS Raster 的绘制、能力快照、模板 Provider 和页面预检。编码覆盖 7-bit ASCII、数字压缩、2–8 行、70 模块宽度及行/符号校验；绘制按模块宽扩展内部行分隔，并要求每行条高至少为 8X。该轮完成编码和 Raster 子范围，不代表完整 USS-49 输出合规。

### R123 已完成 TODO

- [x] 新增 Code 49 主机编码器：完整 ASCII 映射、数字压缩、81 位数字容量、2–8 行、行校验与符号校验；码表来源和 BSD-3-Clause 声明记录在实现文件。
- [x] ESC/POS Raster 接入 Code 49：支持能力快照、Raster 绘制、按实际堆叠高度进行页面预检、旋转和 HRI 布局；行间 1X 分隔条使用条码模块宽度绘制。
- [x] 校验每行条高不得低于 8X；模板统一 Provider 可输出 Code 49 Raster 数据，模板预检仍使用现有保守条码宽度。
- [x] 新增参考矩阵、81 位数字容量、最小条高、Raster 字节 golden、模板输出和非法 ASCII 回归；Code 49/TSPL/ZPL 定向测试 `net6.0` `72/72`，Core 全量 `net6.0`、`netcoreapp3.1` 各 `2966/2966`；Core 与 Templates 五目标框架构建成功。构建/测试仅有 NuGet 漏洞数据源不可达的 `NU1900` 提示。

### R123 未完成 TODO

- [ ] USS-49 要求的顶/底边界分隔条以及通过左右静区延伸的输出尚未实现；Raster 的 70X 编码区与完整 81X 符号 footprint 尚未统一，需完成边界、静区、旋转和页边界语义后再称为完整 USS-49 输出。
- [ ] 完整 GS1 AI 字典、其它压缩 AI 家族、小数/业务关联规则、厂商扩展 AI 和原生协议压缩位流仍未实现。
- [ ] ESC/POS Native 固件 payload/HRI 组合、DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试、跨平台字库和真实热敏/针式设备扫描仍需独立验证。

### R123 本轮接受的限制与验证边界

- [x] Raster Code 49 当前明确覆盖编码矩阵、内部行分隔和最小条高；顶底边界条/左右静区未完成前不声明为完整 USS-49 符号或扫描验收通过。
- [x] 本地 golden、模板和双运行时回归只证明主机字节、预检与失败边界；真实打印质量及读取率仍需目标设备外部验收。

## R122 本轮：跨协议常用压缩 AI 规范化

本轮把 Raster 已有明确编码路径使用的 `310x`/`320x`、`3920`–`3923` 和 `3930`–`3933` 输入形态接入共享括号 AI 解析器。ESC/POS Native、TSPL EAN128、ZPL GS1-128/DataBar Expanded 的直接命令与模板宽度预检现在使用同一固定/变长字段契约，变长价格字段在后续 AI 前插入 ASCII 29/FNC1。

### R122 已完成 TODO

- [x] `310x`/`320x` 接入共享解析器：要求六位 ASCII 数字重量，覆盖 `x=0..9`。
- [x] `3920`–`3923` 与 `3930`–`3933` 接入共享解析器：分别要求 1–15 位数字价格，以及 3 位货币代码加 1–15 位数字价格。
- [x] ESC/POS Native、TSPL EAN128、ZPL GS1-128/DataBar Expanded 直接命令和统一模板宽度预检复用压缩 AI 解析结果；变量字段 FNC1 按协议转义。
- [x] 新增固定/变长字段、非法长度、模板预检、golden 和失败恢复回归；定向 `net6.0` 与 `netcoreapp3.1` 各 `97/97`，Core 全量各 `2957/2957`，Core/Templates 五目标框架构建 0 警告、0 错误。

### R122 未完成 TODO

- [ ] 其它压缩 AI 家族、完整小数/业务关联规则、厂商扩展 AI 和完整 GS1 语法字典仍未实现。
- [ ] Native/TSPL/ZPL 本轮只做原始 AI/FNC1 输入规范化，不模拟 Raster Method 3–6 的压缩位流；链接标志、Stacked/Composite 和 Code 49 Raster 仍未实现。
- [ ] Native 固件 payload/HRI 矩阵、DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试、跨平台字库和真实热敏/针式设备验收仍需独立证据。

### R122 本轮接受的限制与验证边界

- [x] `393x` 只接受三位数字货币代码加非空数字价格；本地解析器不验证 ISO 4217 货币业务语义。
- [x] 原生协议只共享输入语法和 FNC1 转义，不把固件对压缩 AI 的解释或扫描结果视为本地测试已证明。

## R121 本轮：Native/TSPL/ZPL 共享括号 AI 规范化

本轮把当前已声明的 GS1 AI 子集接入三个原生协议路径：ESC/POS Native 的 GS1-128/DataBar Expanded、TSPL EAN128、ZPL GS1-128/DataBar Expanded。至少两个括号 AI 元素会先由共享解析器校验并转换为原始 AI/FNC1，再分别输出 Epson `{1`、TSPL `!102` 或 ZPL `>8`。单个历史括号展示值保持旧行为，避免兼容性回归。

### R121 已完成 TODO

- [x] 增加协议长度可配置的共享 AI 规范化入口；原生路径允许最多 255 字节，Raster 77 字符上限保持不变。
- [x] ESC/POS Native GS1-128/DataBar Expanded、TSPL EAN128、ZPL GS1-128/DataBar Expanded 接入共享括号 AI/FNC1 转换；模板 TSPL/ZPL 宽度预检同步复用该规范化入口。
- [x] 多元素未知 AI、混用括号和字段长度错误严格失败；单元素旧括号值保留历史输出。
- [x] 新增 Epson/TSPL/ZPL golden、FNC1 转义、非法输入无污染和兼容回归；定向回归 `net6.0` `83/83` 通过，Core 全量 `net6.0` 与 `netcoreapp3.1` 各 `2943/2943` 通过，Core/Templates 五目标框架构建 0 警告、0 错误。

### R121 未完成 TODO

- [ ] 当前解析器仍是常用 AI 子集；完整 GS1 AI 字典、跨协议全部业务规则、关联/序列、多日期组合和厂商扩展 AI 仍未实现。
- [ ] 完整压缩 AI 组合、链接标志、GS1 DataBar Stacked/Composite 和 Code 49 的 Raster 主机编码仍未实现。
- [ ] Native 仍未穷举全部固件 payload/HRI 组合；DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试和真实热敏/针式扫描仍需独立证据或外部硬件验收。

### R121 本轮接受的限制与验证边界

- [x] 共享规范化只对至少两个括号 AI 元素生效；单括号历史展示值不强制重新解释，保持 CPCL/原生协议兼容。
- [x] 本地回归证明协议层 FNC1 转义、严格失败和失败恢复稳定；不把常用子集声明为完整 GS1 字典，也不把本地结果宣称为真实设备验收。

## R120 本轮：Expanded GTIN 加常用 AI 序列

本轮补齐 R118 解析器对 `(01)`/`[01]` GTIN 前缀组合的接入。括号 AI 序列先校验 GTIN-14 和当前常用 AI 子集，再将变长字段在后续元素前转换为 FNC1，定长字段直接拼接，最后复用 Method 1 通用字段编码。首个 `(310x)`、`(320x)`、`(392x)`、`(393x)` 仍保持原有压缩 Method 3–14 识别，避免改变历史 golden。

### R120 已完成 TODO

- [x] GTIN 加常用 AI 规范化：支持圆括号/方括号序列、定长/变长字段和 FNC1 插入，输出与等价原始 Method 1 通用字段一致。
- [x] GTIN 校验和严格边界：未知 AI、校验位错误、字段长度错误和括号样式混用在矩阵预算前失败。
- [x] HRI、宽度、直接 Raster 和模板接入；压缩重量/价格/日期 AI 继续由既有 Method 3–14 路径处理。
- [x] 新增等价位流、HRI、直接 Raster、模板和预算回滚回归；`EscPosRasterGs1DataBarExpandedTests` 在 `net6.0`、`netcoreapp3.1` 各 `91/91` 通过，Core 全量在两个目标框架各 `2934/2934` 通过；Core 与 Templates 五目标框架构建均为 0 警告、0 错误。

### R120 未完成 TODO

- [ ] 完整 GS1 AI 字典、跨协议统一解析、业务关联/序列规则、多日期组合和厂商扩展 AI 仍未实现。
- [ ] 完整压缩 AI 组合、链接标志、GS1 DataBar Stacked/Composite 和 Code 49 的 Raster 主机编码仍未实现。
- [ ] Native 全业务 payload、固件/HRI 矩阵、DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试和真实热敏/针式扫描仍需独立证据或外部硬件验收。

### R120 本轮接受的限制与验证边界

- [x] 只开放解析器列出的常用 AI，并保留压缩 AI 的既有专用路径；不把完整 GS1 业务规则伪装成通用文本解析。
- [x] 本地回归证明 GTIN AI 序列、FNC1、HRI、Raster 和模板路径稳定；真实固件与扫描质量属于 `BLOCKED_EXTERNAL`。

## R119 本轮：Expanded 日期后的括号 AI 尾部

本轮在 R117 原始日期尾部和 R118 常用 AI 解析器基础上，开放日期 Method 7–14 后的常用括号 AI 尾部。日期值之后的 `(10)AB(20)12` 或方括号形式先按 AI 定长/变长规则规范化，再进入既有通用字段状态机；变长字段在后续元素前插入 ASCII 29/FNC1，定长字段直接拼接。额外日期 AI、未知 AI、压缩重量/价格 AI 和混用括号严格失败，避免将未实现的业务语义当作普通文本编码。

### R119 已完成 TODO

- [x] 日期 Method 7–14 尾部解析：复用常用 GS1 AI 子集，支持圆括号/方括号，变量字段自动插入 FNC1，定长字段保持连续。
- [x] HRI、宽度、直接 Raster 和统一模板接入：结构化日期尾部与等价原始 FNC1 尾部生成相同 Expanded 位流，HRI 保留用户输入。
- [x] 严格失败边界：额外日期 AI、未知/压缩 AI、字段长度错误和混用括号在矩阵预算前拒绝。
- [x] 新增 Method 7–14 日期尾部 golden 等价、FNC1、HRI、宽度、直接 Raster、模板和预算回滚回归；`EscPosRasterGs1DataBarExpandedTests` 在 `net6.0`、`netcoreapp3.1` 各 `85/85` 通过，Core 全量在两个目标框架各 `2928/2928` 通过；Core 与 Templates 五目标框架构建均为 0 警告、0 错误。

### R119 未完成 TODO

- [ ] 完整 GS1 AI 字典、跨协议统一解析、业务关联/序列规则、多日期组合和厂商扩展 AI 仍未实现。
- [ ] 日期尾部目前只接受已声明的常用 AI；链接标志、完整压缩 AI 组合和 Composite/Stacked 语义仍未实现。
- [ ] Raster 仍未实现 GS1 DataBar Stacked/Composite 和 Code 49；需要分别完成多行矩阵、分隔符、链接字段以及 Code 49 行/校验编码。
- [ ] Native 尚未穷举所有业务 payload、厂商固件和真实 HRI 字体；DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试以及真实热敏/针式扫描仍需独立证据或外部硬件验收。

### R119 本轮接受的限制与验证边界

- [x] 日期后的括号 AI 只做已声明子集的语法到 FNC1 规范化，不宣称完整 GS1 业务语义或多日期组合兼容。
- [x] 本地 golden 和模板回归证明主机位流与资源预算路径稳定；真实固件和扫描质量仍属于 `BLOCKED_EXTERNAL`。

## R118 本轮：Expanded 常用括号 AI 到 FNC1 规范化

本轮在保留 R117 日期后通用字段、R116 原始 FNC1 Method 2 和既有压缩 Method 1/3–14 的前提下，增加常用 GS1 AI 子集的圆括号/方括号输入。解析器依据 GS1 字典的字段形态校验定长/变长长度、数字/CSET 82 字符集和括号匹配；变长字段在后续 AI 前插入 ASCII 29/FNC1，定长字段直接拼接。当前实现是 Raster Expanded 的严格子集，不把完整 GS1 字典或业务关联规则伪装成已完成能力。

### R118 已完成 TODO

- [x] 新增内部 GS1 AI 解析器：覆盖当前 Provider 声明的常用 AI 子集，支持圆括号和方括号，未知 AI、未闭合/混用括号、字段长度、数字和 CSET 82 错误均在矩阵预算前失败。
- [x] Method 2 规范化：后续变长字段自动插入 ASCII 29/FNC1，定长字段不插入；规范化结果与等价原始 FNC1 输入复用同一 Expanded 位流、HRI、宽度和 Raster 页面。
- [x] 模板接入：模板条码预检接受方括号并把括号/FNC1 计入转义长度上限；直接命令与统一模板使用同一输入校验和输出路径。
- [x] 新增括号/方括号 golden、固定/变长末字段、HRI、宽度、直接 Raster、模板和预算前非法输入回归；`EscPosRasterGs1DataBarExpandedTests` 在 `net6.0`、`netcoreapp3.1` 各 `77/77` 通过，Core 全量在两个目标框架各 `2920/2920` 通过；Core 与 Templates 五目标框架构建均为 0 警告、0 错误；同步补充 GS1 官方字典与 General Specifications 链接。

### R118 未完成 TODO

- [ ] 仍未覆盖完整 GS1 AI 字典、310x/320x 等压缩重量 AI 的通用括号组合、多日期组合、链接标志、业务关联规则、跨协议统一输入契约和厂商扩展 AI；当前仅是明确声明的常用子集。
- [ ] 日期后的括号 AI 语义仍未开放；R117 日期尾部继续只接受原始通用字段和 FNC1。
- [ ] Raster 仍未实现 GS1 DataBar Stacked/Composite 和 Code 49；需要分别完成多行矩阵/分隔符/链接字段以及 Code 49 行/校验编码。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试和真实热敏/针式打印扫描仍需独立证据或外部硬件验收。

### R118 本轮接受的限制与验证边界

- [x] 只有解析器列出的常用 AI 子集会自动规范化；未知 AI 和业务关联不会猜测或静默降级。
- [x] 本地 golden 证明括号/方括号与原始 FNC1 的 Raster 位流一致；不把本地结果宣称为完整 GS1 字典兼容或真实设备扫描验收。

## R117 本轮：Expanded 日期后的通用字段

本轮在保留 R116 Method 2 原始 FNC1 和 R115 Method 9–14 日期字段的前提下，开放 Method 7/8、9–14 日期压缩字段后的原始通用字段。归一化仍要求日期 AI 和六位日期使用结构化输入；日期后的尾部从编码索引 34 开始进入既有 Numeric、Alphanumeric、ISO/IEC 646 和 FNC1 状态机。规则与位流布局沿用 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 的 Expanded 实现；本轮不把尾部括号解释为新的 AI 元数据。

### R117 已完成 TODO

- [x] Method 7/8、9–14 日期字段后的原始通用字段：允许非空通用尾部，保留既有日期方法位、重量和日期编码；尾部继续执行字符集、连续 FNC1 和末尾 FNC1 校验。
- [x] HRI、宽度测量、直接 Raster 命令和统一模板接入：HRI 保留日期字段及尾部，页面边界和资源预算复用同一编码路径。
- [x] 新增 Method 7、Method 10 golden、括号/方括号等价输入、FNC1 尾部、非法结构化尾部、直接 Raster 和模板回归。
- [x] 本地验证：`EscPosRasterGs1DataBarExpandedTests` 在 `net6.0`、`netcoreapp3.1` 各 `68/68` 通过；Core 全量在两个目标框架各 `2911/2911` 通过；Core 与 Templates 五目标框架构建均为 0 警告、0 错误；测试阶段仅保留既有 `NU1900` 漏洞源不可达警告。

### R117 未完成 TODO

- [ ] 完整 GS1 AI 元数据解析、括号/方括号到 FNC1 的跨协议规范化、定长/变长字段业务校验、多日期组合、链接标志和统一输入契约仍未实现。
- [ ] 日期后的括号 AI 语义仍未实现；当前尾部只接受已经编码为原始通用字段的字符和 FNC1。
- [ ] Raster 仍未实现 GS1 DataBar Stacked/Composite 和 Code 49；需要分别完成多行矩阵/分隔符/链接字段以及 Code 49 行/校验编码。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试和真实热敏/针式打印扫描仍需独立证据或外部硬件验收。

### R117 本轮接受的限制与验证边界

- [x] 日期后尾部沿用原始通用字段契约，不静默把括号内容转换为 AI，也不改变 Method 2 的显式 FNC1 输入边界。
- [x] 本地 golden 证明日期压缩字段与尾部状态机、HRI、Raster 页面和模板路径稳定；不把本地结果宣称为完整 GS1 AI 解析或真实设备扫描验收。

## R116 本轮：Expanded Method 2 原始 FNC1 数据

本轮在保留 R115 Method 9–14 日期字段及既有 GTIN 压缩路径的前提下，增加 ESC/POS Raster GS1 DataBar Expanded Method 2 的原始 GS1 通用字段输入。输入必须以两位数字 AI 开头并包含 ASCII 29/FNC1 分隔符，主机写入 `0000` 方法字段，然后复用 Numeric、Alphanumeric、ISO/IEC 646 和 FNC1 状态机、动态 check/finder、宽度、HRI、Raster 命令和模板路径。规则依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 的 Method 2 分支和 [libzint general_field.c](https://raw.githubusercontent.com/zint/zint/master/backend/general_field.c) 的通用字段编码。

### R116 已完成 TODO

- [x] Method 2 识别和规范化：接受 3–77 字符、两位数字 AI 开头且含显式 FNC1 的原始数据；括号/方括号展示语法、首尾/连续 FNC1 和不支持字符在预算前严格拒绝。
- [x] Method 2 位流：写入 `0000` 方法字段，跳过 GTIN 压缩字段，直接编码完整通用字段并按规范回填变量符号长度位；既有 Method 1/3–14 位流保持不变。
- [x] HRI、宽度测量、GS1 DataBar Expanded Raster 直接命令和统一模板复用同一 Method 2 编码路径；新增 golden、非法输入、直接 Raster 和模板回归。
- [x] 本地验证：`EscPosRasterGs1DataBarExpandedTests` 在 `net6.0`、`netcoreapp3.1` 各 `64/64` 通过；Core 全量在两个目标框架各 `2907/2907` 通过；Core 与 Templates 五目标框架构建均为 0 警告、0 错误；测试阶段仅保留既有 `NU1900` 漏洞源不可达警告。

### R116 未完成 TODO

- [ ] 完整 GS1 AI 元数据解析、括号/方括号到 FNC1 的跨协议规范化、定长/变长字段业务校验、多日期组合、链接标志和统一输入契约仍未实现；当前 Method 2 只接受显式 FNC1 原始串。
- [ ] 日期字段后的通用字段及完整 GS1 日期业务语义仍未实现；Method 9–14 继续要求日期字段为结构化输入最后一段。
- [ ] Raster 仍未实现 GS1 DataBar Stacked/Composite 和 Code 49；需要分别完成多行矩阵/分隔符/链接字段以及 Code 49 行/校验编码。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试和真实热敏/针式打印扫描仍需独立证据或外部硬件验收。

### R116 本轮接受的限制与验证边界

- [x] Method 2 仅开放原始 FNC1 数据，不把括号语法、完整 AI 语义或历史非 GTIN 裸串静默转换为另一种输入契约。
- [x] 本地 golden 证明 Method 2 主机位流、HRI、Raster 页面和模板路径稳定；不把本地结果宣称为完整跨协议 GS1 解析或真实设备扫描验收。

## R115 本轮：Expanded Method 9–14 `(11)/(13)/(15)/(17)` 日期字段

本轮在保留 R114 Method 7/8、R113 Method 5/6、R112 Method 4、R111 Method 3、R110 Method 1 通用字段和既有 golden 输出的前提下，增加显式 `(310x)`/`[310x]`、`(320x)`/`[320x]` 后接日期 AI `(11)`、`(13)`、`(15)`、`(17)` 及六位日期的 Method 9–14 编码。日期 AI 分别映射到 Method 7/8、9/10、11/12、13/14；主机写入对应 7 位方法字段、20 位重量和 16 位 `yy*384+(mm-1)*32+dd` 日期值，月限制 `01-12`、日限制 `00-31`，HRI 保留原始日期 AI 和文本。规则依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 的 Method 7–14 分支。

### R115 已完成 TODO

- [x] Expanded 规范化支持四种日期 AI 的括号/方括号形式，并按 `(310x)`/`(320x)` 选择 Method 7/8、9/10、11/12、13/14；无日期输入继续使用虚拟日期 `38400`。
- [x] 实现 Method 9–14 的方法字段、GTIN/重量压缩、日期值编码和严格月日校验；日期后的额外字段和未实现两位结构化 AI Fail Fast。
- [x] HRI 恢复重量 AI、重量、日期 AI 和日期文本；宽度、矩阵预算、Raster 命令和统一模板复用同一编码路径。
- [x] 新增八个方法 golden、canonical 输入、日期 AI 映射、日期边界、直接 Raster 命令和模板回归；定向回归在 `net6.0`、`netcoreapp3.1` 各 `57/57` 通过。
- [x] Core 全量回归在 `net6.0`、`netcoreapp3.1` 各 `2900/2900` 通过；Core 与 Templates 五目标框架构建均为 0 警告、0 错误；测试阶段仅保留既有 `NU1900` 漏洞源不可达警告。

### R115 未完成 TODO

- [ ] 日期字段后的通用字段、FNC1、多日期组合和完整 GS1 日期业务语义仍未实现。
- [ ] Expanded 其它结构化 AI、链接标志、跨协议字符分段/HRI 和统一 GS1 输入契约仍未实现。
- [ ] Raster 仍未实现 GS1 DataBar Stacked/Composite 和 Code 49；需要分别完成堆叠行/分隔符/链接字段以及 Code 49 行/校验编码。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试和真实热敏/针式打印扫描仍需独立证据或外部硬件验收。

### R115 本轮接受的限制与验证边界

- [x] 仅开放显式 `(11)/(13)/(15)/(17)` 日期 AI 且日期必须是结构化输入最后一段；未知日期 AI、日期后的通用字段和多日期组合仍严格拒绝。
- [x] 本地 golden 证明主机位流、HRI、Raster 页面和模板路径稳定；不把本地编码结果宣称为目标固件、完整 GS1 日期业务兼容或热敏/针式扫描验收。

## R114 本轮：Expanded Method 7/8 `(310x)/(320x)` 重量与虚拟日期

本轮在保留 R113 Method 5/6、R112 Method 4、R111 Method 3、R110 Method 1 通用字段和既有 golden 输出的前提下，增加 ESC/POS Raster GS1 DataBar Expanded 的显式 Method 7/8 无日期子范围。对 `(310x)`/`[310x]` 和 `(320x)`/`[320x]` 后接 6 位 `000000-099999` 重量的输入，`(3103)` 在 `032768-099999` 或其它 `310x` 选择 Method 7，`(3202)/(3203)` 超出 Method 4 上限或其它 `320x` 选择 Method 8。主机写入 `0111000`/`0111001` 方法字段、20 位重量和固定虚拟日期 `38400`，HRI 保留原始 AI 与重量文本，不追加虚拟日期。规则依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 的 Method 7/8 分支。

### R114 已完成 TODO

- [x] Expanded 规范化支持显式 `(310x)`/`[310x]` 与 `(320x)`/`[320x]`；有效 Method 3/4 范围继续选择旧方法，其余 `000000-099999` 重量自动选择 Method 7/8。
- [x] 实现 Method 7/8 的 `0111000`/`0111001` 方法字段、GTIN 压缩字段、20 位重量字段和固定 `38400` 日期字段；无日期后缀严格限制为单一 6 位重量。
- [x] HRI 恢复 `(310x)`/`(320x)` 和重量文本；宽度、矩阵预算、Raster 命令和统一模板复用同一编码路径。
- [x] 新增 Method 7/8 golden、canonical 输入、方法选择边界、重量上限、直接 Raster 命令和模板回归；定向回归在 `net6.0`、`netcoreapp3.1` 各 `49/49` 通过。
- [x] Core 全量回归在 `net6.0`、`netcoreapp3.1` 各 `2892/2892` 通过；Core 与 Templates 五目标框架构建均为 0 警告、0 错误；测试阶段仅保留既有 `NU1900` 漏洞源不可达警告。

### R114 未完成 TODO

- [ ] Method 9–14 的显式生产/包装/最佳食用/有效期日期字段仍未实现；跨日期 AI 的业务校验、FNC1 和 HRI 语义仍待定义。
- [ ] Expanded 其它结构化 AI、链接标志、跨协议字符分段/HRI 和统一 GS1 输入契约仍未实现。
- [ ] 本轮 Method 7/8 只开放无日期后缀的单一重量字段；不接受虚拟日期作为用户输入，也未开放完整 `(310x)/(320x)` 后续通用字段。
- [ ] Raster 仍未实现 GS1 DataBar Stacked/Composite 和 Code 49；需要分别完成堆叠行/分隔符/链接字段以及 Code 49 行/校验编码。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试和真实热敏/针式打印扫描仍需独立证据或外部硬件验收。

### R114 本轮接受的限制与验证边界

- [x] 仅开放没有日期后缀的 `(310x)`/`[310x]` 和 `(320x)`/`[320x]` Method 7/8；显式日期后缀、Method 9–14、其它结构化 AI 或结构化后缀仍严格拒绝。
- [x] 本地 golden 证明主机位流、HRI、Raster 页面和模板路径稳定；不把本地编码结果宣称为目标固件、完整 GS1 日期业务兼容或热敏/针式扫描验收。

## R113 本轮：Expanded Method 5/6 `(392x)/(393x)` 本地价格与货币价格

本轮在保留 R112 Method 4、R111 Method 3、R110 Method 1 通用字段和既有 golden 输出的前提下，增加 ESC/POS Raster GS1 DataBar Expanded 的显式 Method 5/6 子范围。输入接受 `(01)`/`[01]` GTIN-14 后的 `(392x)`/`[392x]` 本地价格或 `(393x)`/`[393x]` 三位 ASCII ISO 4217 货币代码加价格，`x` 限制为 `0-3`；价格按严格输入契约要求为非空 ASCII 数字。主机写入 `01100xx`/`01101xx` 方法字段，Method 6 再写入 10 位货币字段，然后复用通用字段状态机、动态符号宽度、check/finder、HRI、Raster 位图和模板路径。规则依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 的 Method 5/6 分支。

### R113 已完成 TODO

- [x] Expanded 规范化支持显式 `(392x)`/`[392x]` 和 `(393x)`/`[393x]`；`x=0-3`，Method 6 严格验证三位 ASCII 数字货币代码和非空 ASCII 数字价格。
- [x] 实现 Method 5/6 的 `01100xx`/`01101xx` 方法字段、GTIN 压缩字段、Method 6 的 10 位货币字段和通用价格字段；保留 Method 1/3/4 的位流和动态符号宽度。
- [x] HRI 恢复 `(392x)`/`(393x)`、货币代码和价格文本；宽度、矩阵预算、Raster 命令和统一模板复用同一编码路径。
- [x] 新增 Method 5/6 golden、canonical 输入、`x=3` 边界、货币/价格非法输入、直接 Raster 命令和模板回归；定向回归在 `net6.0`、`netcoreapp3.1` 各 `49/49` 通过。
- [x] Core 全量回归在 `net6.0`、`netcoreapp3.1` 各 `2892/2892` 通过；测试阶段仅保留既有 `NU1900` 漏洞源不可达警告。

### R113 未完成 TODO

- [ ] Expanded Method 7–14、其它结构化 AI（日期、折扣、批次/序列号等）、链接标志、跨协议字符分段/HRI 和统一 GS1 输入契约仍未实现。
- [ ] 本轮 Method 5/6 采用非空 ASCII 数字价格和三位 ASCII 数字货币代码的严格契约；完整 GS1 价格小数语义、货币代码业务校验、FNC1/后续字段和厂商扩展尚未实现。
- [ ] Raster 仍未实现 GS1 DataBar Stacked/Composite 和 Code 49；需要分别完成堆叠行/分隔符/链接字段以及 Code 49 行/校验编码。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试和真实热敏/针式打印扫描仍需独立证据或外部硬件验收。

### R113 本轮接受的限制与验证边界

- [x] 仅开放显式 `(392x)`/`[392x]` 和 `(393x)`/`[393x]` 的 Method 5/6；其它结构化 `(dddd)`/`[dddd]` AI、日期/其它价格 AI、Method 7–14 或结构化后缀仍严格拒绝，普通非结构化通用字段继续按既有字符契约处理。
- [x] 本地 golden 证明主机位流、HRI、Raster 页面和模板路径稳定；不把本地编码结果宣称为目标固件、完整 GS1 价格/货币业务兼容或热敏/针式扫描验收。

## R112 本轮：Expanded Method 4 `(3202)/(3203)` 英制重量

本轮在保留 R111 Method 3、R110 Method 1 通用字段和既有 golden 输出的前提下，增加 ESC/POS Raster GS1 DataBar Expanded 的显式 Method 4 子范围。输入接受 `(01)`/`[01]` GTIN-14 后的 `(3202)`/`[3202]` 或 `(3203)`/`[3203]` 6 位英制重量；`3202` 限制为 `000000-009999`（百分之一磅），`3203` 限制为 `000000-022767`（千分之一磅）。主机写入固定 `0101` 方法字段，`3203` 重量按规范加 `10000` 后写入 15 位字段，再复用通用字段状态机、动态符号宽度、check/finder、HRI、Raster 位图和模板路径。规则依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 的 Method 4 分支。

### R112 已完成 TODO

- [x] Expanded 规范化支持显式 `(3202)`/`[3202]` 和 `(3203)`/`[3203]`；裸数字 `3202...`/`3203...` 继续按既有 Method 1 通用字段处理。
- [x] 实现 Method 4 的 `0101` 方法字段、GTIN 压缩字段、`3203` 的 `+10000` 重量编码和两种 AI 的严格上限校验。
- [x] HRI 恢复 `(3202)`/`(3203)` 和重量文本；宽度、矩阵预算、Raster 命令和统一模板复用同一编码路径。
- [x] 新增 Method 4 golden、canonical 输入、边界/非法重量、结构化 AI 拒绝、直接命令、通用字段和模板回归；定向回归在 `net6.0`、`netcoreapp3.1` 各 `41/41` 通过。
- [x] Core 全量回归在 `net6.0`、`netcoreapp3.1` 各 `2884/2884` 通过；Core 与 Templates 五目标框架构建均为 0 警告、0 错误；测试阶段仅保留既有 `NU1900` 漏洞源不可达警告。

### R112 未完成 TODO

- [ ] Expanded Method 5/6、Method 7–14、其它 AI、链接标志、跨协议字符分段/HRI 和统一 GS1 输入契约仍未实现。
- [ ] Raster 仍未实现 GS1 DataBar Stacked/Composite 和 Code 49；需要分别完成堆叠行/分隔符/链接字段以及 Code 49 行/校验编码。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试和真实热敏/针式打印扫描仍需独立证据或外部硬件验收。

### R112 本轮接受的限制与验证边界

- [x] 仅开放显式 `(3202)`/`[3202]` 和 `(3203)`/`[3203]` 的 Method 4；其它结构化 `(dddd)`/`[dddd]` AI、日期/价格 AI 或结构化后缀仍严格拒绝，普通非结构化通用字段继续按既有字符契约处理。
- [x] 本地 golden 证明主机位流、HRI、Raster 页面和模板路径稳定；不把本地编码结果宣称为目标固件、完整 GS1 业务字段兼容或热敏/针式扫描验收。

## R111 本轮：Expanded Method 3 `(3103)` 公斤重量

本轮在保留 R110 Method 1 通用字段和既有 golden 输出的前提下，增加 ESC/POS Raster GS1 DataBar Expanded 的显式 Method 3 子范围。输入接受 `(01)`/`[01]` GTIN-14 后的 `(3103)`/`[3103]` 6 位公斤重量，严格限制重量为 `000000-032767`；主机写入固定 `0100` 方法字段、15 位重量字段，再复用通用字段状态机、动态符号宽度、check/finder、HRI、Raster 位图和模板路径。规则依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 的 Method 3 分支。

### R111 已完成 TODO

- [x] Expanded 规范化支持显式 `(3103)`/`[3103]`；保留裸数字 `3103...` 的 Method 1 兼容行为，避免改变现有调用语义。
- [x] 实现 Method 3 的 `0100` 方法字段、GTIN 压缩字段和 15 位 `000000-032767` 公斤重量字段；固定方法位不再被 Method 1 的可变符号长度位覆盖。
- [x] HRI 恢复 `(01)`、`(3103)` 和重量文本；宽度、矩阵预算、Raster 命令和统一模板复用同一编码路径。
- [x] 新增 Method 3 golden、`[01]/[3103]` 等价输入、非法重量、直接 Raster 和模板回归；定向回归在 `net6.0`、`netcoreapp3.1` 各 `31/31` 通过。
- [x] Core 全量回归在 `net6.0`、`netcoreapp3.1` 各 `2874/2874` 通过；Core 与 Templates 五目标框架构建均为 0 警告、0 错误；测试阶段仅保留既有 `NU1900` 漏洞源不可达警告。

### R111 未完成 TODO

- [ ] Expanded Method 4、Method 5/6、Method 7–14、其它 AI、链接标志、跨协议字符分段/HRI 和统一 GS1 输入契约仍未实现。
- [ ] Raster 仍未实现 GS1 DataBar Stacked/Composite 和 Code 49；需要分别完成堆叠行/分隔符/链接字段以及 Code 49 行/校验编码。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试和真实热敏/针式打印扫描仍需独立证据或外部硬件验收。

### R111 本轮接受的限制与验证边界

- [x] 仅开放显式 `(3103)`/`[3103]` 的 Method 3 公斤重量；未将其它 `(31xx)`、英制重量、日期/价格 AI 或结构化后缀静默降级为 Method 3。
- [x] 本地 golden 证明主机位流、HRI、Raster 页面和模板路径稳定；不把本地编码结果宣称为目标固件、完整 GS1 业务字段兼容或热敏/针式扫描验收。

## R110 本轮：Expanded 通用字段字符集与 FNC1

本轮在保留 R109 数字字段和 R108 五符号 134 模块字节兼容的前提下，扩展 ESC/POS Raster `BarcodeType.Gs1DataBarExpanded` Method 1 的通用字段编码。主机移植 ISO/IEC 24724 的 Numeric、Alphanumeric、ISO/IEC 646 状态机，支持数字成对压缩、A-Z、a-z、标准字母数字/ISO 标点、空格和 ASCII 29 FNC1；字段末尾 FNC1、`#`、`$` 等不在当前编码表中的字符仍严格拒绝。其它 AI、Method 2–14、链接标志、Stacked/Composite 不在本轮开放。

### R110 已完成 TODO

- [x] Expanded `(01)` GTIN-14 后的通用字段不再局限纯数字；补齐 Numeric/Alphanumeric/ISO/IEC 646 latch、数字压缩、末位数字、FNC1 和 `00100` padding 状态转换。
- [x] 保留 R108/R109 数字字段 golden；新增字母数字及 ISO/FNC1 golden，HRI 显示继续移除控制分隔符，非法字符和末尾 FNC1 在矩阵预算前 Fail Fast。
- [x] Raster 独立命令、模板 Provider、宽度测量和动态矩阵预算复用同一编码路径；CPCL、TSPL、ZPL、ESC/POS Native 行为未改。
- [x] Expanded 定向回归在 `net6.0`、`netcoreapp3.1` 各 `26/26` 通过；完整 Core 回归各 `2869/2869`，Core 与 Templates 五目标框架构建 0 警告、0 错误。

### R110 未完成 TODO

- [ ] Expanded 仍只实现 Method 1 的 `(01)` 压缩字段；其它 GS1 AI/压缩方法、链接标志，以及跨协议的字符分段/HRI/输入分隔契约尚未统一。
- [ ] Raster 仍未实现 GS1 DataBar Stacked/Composite 和 Code 49；需要分别完成堆叠行/分隔符/链接字段以及 Code 49 行/校验编码。
- [ ] GS1 DataBar 跨协议的 13 位 payload、14 位含校验输入、AI `(01)`、通用字段与复合链接字段尚未形成统一公开输入契约；当前通用字段扩展只在 Raster 内部生效。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试和真实热敏/针式打印扫描仍需独立证据或外部硬件验收。

### R110 本轮接受的限制与验证边界

- [x] 当前 Raster 字符表严格跟随 `general_field.c` 的 Alphanumeric/ISO/IEC 646 编码表；不把 `#`、`$` 等未经编码表确认的字符静默映射，末尾 FNC1 直接拒绝。
- [x] 本地 golden 只证明主机位图字节和旧数字输出稳定；不把本地编码结果宣称为目标固件、完整 GS1 业务字段兼容或热敏/针式扫描验收。

## R109 本轮：Expanded Method 1 数字通用字段

本轮在保留 R108 GTIN-only 134 模块字节兼容的前提下，扩展 ESC/POS Raster `BarcodeType.Gs1DataBarExpanded`：输入可为 `(01)`、`[01]` 或裸 `01` 加 14 位合法 GTIN-14，再接纯 ASCII 数字通用字段。主机按 Method 1 的数字成对压缩、末位 4/7 位规则、Numeric latch/填充、动态 check/finder 表和变量符号宽度生成 `GS v 0` 位图；字母、ISO、其它 AI、FNC1、Stacked/Composite 仍严格拒绝。

### R109 已完成 TODO

- [x] Expanded 输入归一化允许合法 GTIN-14 后接纯数字通用字段；GTIN 校验位、字符集和最大 22 个符号字符在矩阵预算前验证。
- [x] 实现数字通用字段 7-bit 成对压缩、末位数字 4/7-bit 分支、Numeric terminator、`00100` padding 和 Method 1 可变长度位。
- [x] 接入完整 Expanded checksum weight、finder pattern/sequence 表，按数据字符数计算校验、线性模块宽度和 `PrintMatrixBudget`；R108 五字符 GTIN-only golden 保持不变。
- [x] HRI、宽度测量、直接 Raster 命令、预算回滚和模板 Raster 路径覆盖数字通用字段；新增 151 模块 golden。
- [x] `EscPosRasterGs1DataBarExpandedTests` 在 `net6.0`、`netcoreapp3.1` 各 `21/21` 通过；Core 全量回归各 `2864/2864`；Core 与 Templates 五目标框架构建 0 警告、0 错误；测试构建仅保留既有 `NU1900` 漏洞源不可达警告。

### R109 未完成 TODO

- [ ] Expanded 仍只支持 Method 1 的 `(01)` 加纯数字通用字段；完整 CSET 82/CSET 64/ISO 646 编码、其它压缩方法、FNC1/其它 AI、链接标志、Stacked/Composite 尚未实现。
- [ ] Raster 仍未实现 GS1 DataBar Stacked/Composite 和 Code 49；需要分别完成堆叠行/分隔符/链接字段以及 Code 49 行/校验编码。
- [ ] GS1 DataBar 跨协议的 13 位 payload、14 位含校验输入、AI `(01)`、数字通用字段与复合链接字段尚未形成统一公开输入契约；当前扩展只在 Raster 内部生效。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 隔离重建、字体/DPI/纸宽校准、性能容量、取消/重试和真实热敏/针式打印扫描仍需独立证据或外部硬件验收。

### R109 本轮接受的限制与验证边界

- [x] 仅开放 `(01)` GTIN-14 后的 ASCII 数字通用字段；字母、ISO 字符、ASCII 29/FNC1、其它 AI 和组合/堆叠形式在规范化阶段 Fail Fast。
- [x] 本地 golden 验证动态符号宽度和字节稳定性；不把本地编码结果宣称为目标固件、GS1 业务字段兼容或热敏/针式扫描验收。

## R108 本轮：ESC/POS Raster GS1 DataBar Expanded GTIN-only 主机编码

本轮将 `BarcodeType.Gs1DataBarExpanded` 接入 ESC/POS Raster 主机编码的严格 GTIN-only 子集。输入接受 `(01)`、`[01]` 或裸 `01` 加 14 位 GTIN-14，并在矩阵预算前验证 ASCII 数字和 Mod-10 校验位；主机按 GS1 DataBar Expanded Method 1 生成 48 位压缩字段、四个数据字符、check character、finder sequence 和线性 `GS v 0` 位图。五个符号字符展开为固定 134 模块宽度，复用现有模块缩放、HRI、旋转、模板布局和失败恢复。

### R108 已完成 TODO

- [x] `EscPosRasterCanvas` 增加 Expanded GTIN-14 `(01)` 输入归一化、校验位验证、Method 1 压缩字段、DataBar Expanded 字符宽度、check character、finder pattern/sequence 和 134 模块位图。
- [x] `PrintMatrixBudget` 增加 Expanded 主机矩阵预算；输入错误在资源预留前失败，预算超限、命令写入、模板预检、HRI 和四方向旋转保持失败恢复语义。
- [x] ESC/POS Raster 能力矩阵、独立命令、宽度测量和模板 Provider 接入 Expanded；ESC/POS Native Function B 78、TSPL、ZPL 和 CPCL 的既有语义保持不变。
- [x] 新增 `(01)`/`[01]`/裸 `01` 等价输入、golden pattern、GTIN 校验/非法 AI、预算回滚、直接命令、四方向 Provider、模板元素路径和能力传播回归。
- [x] Expanded 定向回归在 `net6.0`、`netcoreapp3.1` 各 `30/30` 通过；Core 完整回归各 `2858/2858`；Core 与 Templates 五目标框架构建 0 警告、0 错误；测试构建仅保留既有 `NU1900` 漏洞源不可达警告。
- [x] 编码流程和表格依据 [GS1 DataBar](https://www.gs1.org/standards/barcodes/databar)、[GS1 General Specifications](https://ref.gs1.org/standards/genspecs/11.0.0/)、[libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 和 [rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h)；项目保留 BSD-3-Clause 归因说明。

### R108 未完成 TODO

- [ ] Expanded 目前只支持 Method 1 的 GTIN-only `(01)` 子集；其它 AI/通用字段、链接标志、Stacked/Composite 和完整 4–21 字符可变宽度编码仍未实现，不能将当前能力宣传为全量 GS1 DataBar Expanded。
- [ ] Raster 仍未实现 GS1 DataBar Stacked/Composite 和 Code 49；需要分别完成堆叠行/分隔符/链接字段以及 Code 49 行/校验编码。
- [ ] GS1 DataBar 跨协议 13 位 payload、14 位含校验输入、AI `(01)` 和复合链接字段尚未形成统一公开输入契约；当前 Expanded 只在 Raster 内部严格接受规范化 GTIN-14。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 尚未完成基于当前源码的全新隔离完整重建和新测试二进制执行；仍受静态资源、嵌套中间目录和运行中 DLL 锁影响。
- [ ] HRI 字体/样式、TSPL/ZPL 内置字体样式、二维码固件方向和设备固件版本组合仍未建立。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R108 本轮接受的限制与验证边界

- [x] 仅把 ASCII `(01)`/`[01]`/`01` 加合法 GTIN-14 映射为 Raster Expanded；其它 AI、通用字段、Composite、Stacked 和完整可变长度方法在规范化阶段 Fail Fast。
- [x] 本地回归证明 Method 1 压缩、字符宽度、check/finder、134 模块展开、HRI、布局和失败恢复，不证明目标打印机固件、GS1 业务字段语义、DPI、纸宽、条高、HRI 字形或热敏/针式扫描质量。

## R107 本轮：ESC/POS Raster China Post（Datalogic 2 of 5）主机编码

本轮将 `BarcodeType.ChinaPost` 接入 ESC/POS Raster 主机编码。输入严格限制为非空 ASCII 数字；数字字符沿用 Matrix 2 of 5，使用 Datalogic/China Post 的两窄条起始符和宽条/窄条结束符，不追加可选校验位。由于 Raster 统一入口只有一个模块宽度，本轮固定主机宽窄比为 3:1，并保留字符间窄空白，再复用 `GS v 0`、模块缩放、旋转、HRI 和模板布局路径。TSPL 原生 CPOST 的既有 1:1 校验和设备编码语义保持不变。

### R107 已完成 TODO

- [x] `EscPosRasterCanvas` 增加 China Post 能力声明、数字输入校验、Matrix 2 of 5 数字表、Datalogic 起止符、3:1 宽窄比和确定性模块串编码。
- [x] `PrintMatrixBudget` 增加按 `10n+9` 模块计算的 China Post 矩阵预算；非法输入在预算变更前失败，编码、HRI、旋转和页面绘制复用既有失败恢复语义。
- [x] ESC/POS Raster 独立命令、`PrintCapabilities` 能力快照、宽度测量、四方向 Provider、模板元素路径和能力预检接入 China Post；Native、TSPL、ZPL 和 CPCL 行为保持原有边界。
- [x] 新增起止符/数字表 golden pattern、长度边界、非法输入/预算恢复、直接命令、四方向 Provider、模板输出和能力传播回归。
- [x] Raster/TSPL China Post 定向回归在 `net6.0`、`netcoreapp3.1` 各 `30/30` 通过；Core 完整回归各 `2843/2843`，Core 与 Templates 五目标框架构建 0 警告、0 错误；仅保留既有 `NU1900` 漏洞源不可达警告。
- [x] China Post 结构和输入语义参考 [TCPDF DatalogicTwoOfFive](https://tcpdf.org/docs/srcdoc/tc-lib-barcode/classes-Com-Tecnick-Barcode-Type-Linear-DatalogicTwoOfFive/)、[Seagull Barcode Guide](https://barcodeguide.seagullscientific.com/Content/Symbologies/Datalogic_2_of_5.htm)；宽窄比范围参考 [Seagull Datalogic 2 of 5 options](https://help.seagullscientific.com/12.0/en/Content/symb_Datalogic2of5.htm)。

### R107 未完成 TODO

- [ ] Raster 仍未实现 GS1 DataBar Expanded、Stacked/Composite 和 Code 49；需要分别完成 GS1 链接字段/堆叠布局与 Code 49 行/校验编码，不以保守宽度估算替代编码器。
- [ ] GS1 DataBar 目前仍只接受统一模型的 13 位 payload，尚未开放可选的 14 位含校验输入、AI `(01)` 前缀或复合链接字段；需要先定义跨协议输入契约和兼容测试。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、每种厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 尚未完成基于当前源码的全新隔离完整重建和新测试二进制执行；仍受静态资源、嵌套中间目录和运行中 DLL 锁影响。
- [ ] HRI 字体/样式、TSPL/ZPL 内置字体样式、二维码固件方向和设备固件版本组合仍未建立。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R107 本轮接受的限制与验证边界

- [x] Raster China Post 只声明固定 3:1 宽窄比、非空数字输入和 `10n+9` 编码区宽度；不把 TSPL CPOST 的设备端 1:1 参数、可选校验位或固件长度规则扩展到主机 Raster。
- [x] 本地回归证明数字表、起止符、字符间隔、固定宽度、能力传播、布局和失败恢复，不证明目标打印机固件、DPI、纸宽、条高、HRI 字形或热敏/针式扫描质量。

## R106 本轮：ESC/POS Raster GS1 DataBar Limited 主机编码

本轮将 `BarcodeType.Gs1DataBarLimited` 接入 ESC/POS Raster 主机编码。输入沿统一模型限制为 13 位 ASCII 数字且首位为 0 或 1；主机按 libzint 的 Limited 分组、7 元素组合宽度、Mod-89 校验和 Annex C finder 表生成固定 79 模块一维位图，再复用 `GS v 0`、模块缩放、旋转、HRI 和模板布局路径。Limited 使用 `(01)` 加 GTIN 校验位的 HRI 语义；14 位含校验输入、AI 前缀和复合链接字段仍留在后续契约 TODO。

### R106 已完成 TODO

- [x] `EscPosRasterCanvas` 增加 Limited 的 13 位/首位范围校验、两组数据 pair、7 元素宽度解码、Mod-89 校验、89 个 finder 模式和 79 模块位图。
- [x] `PrintMatrixBudget` 增加固定 79 模块矩阵预算；非法输入在预算变更前失败，编码、HRI 和页面绘制复用既有失败恢复语义。
- [x] Raster 独立命令、宽度测量、四方向 Provider、模板元素路径、能力快照和 HRI 接入 Limited；Native Function B 77、TSPL/ZPL 现有行为保持不变。
- [x] 新增 79 模块 golden pattern、边界 payload、首位/长度/字符非法输入、预算恢复、直接命令、四方向 Provider、模板输出和能力传播回归。
- [x] Limited 定向回归在 `net6.0`、`netcoreapp3.1` 各 `16/16` 通过；算法表格依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 与 [rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h)，保留 BSD-3-Clause 归因。
- [x] Core 完整回归在 `net6.0`、`netcoreapp3.1` 各 `2829/2829` 通过；Core 与 Templates 五目标框架构建 0 警告、0 错误；仅保留既有 `NU1900` 漏洞源不可达警告。

### R106 未完成 TODO

- [ ] Raster 仍未实现 GS1 DataBar Expanded、Stacked/Composite、China Post、Code 49 等变体；本轮只关闭 Limited 主机编码子范围。
- [ ] GS1 DataBar 目前仍只接受统一模型的 13 位 payload，尚未开放可选的 14 位含校验输入、AI `(01)` 前缀或复合链接字段；需要先定义跨协议输入契约和兼容测试。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、每种厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 尚未完成基于当前源码的全新隔离完整重建和新测试二进制执行；仍受静态资源、嵌套中间目录和运行中 DLL 锁影响。
- [ ] HRI 字体/样式、TSPL/ZPL 内置字体样式、二维码固件方向和设备固件版本组合仍未建立。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R106 本轮接受的限制与验证边界

- [x] Limited 只声明固定 79 模块主机编码和统一 13 位/首位 0 或 1 输入；不把 14 位、`(01)`、复合或设备原生 Function B 77 语义扩展到 Raster。
- [x] 本地回归证明分组、组合宽度、finder/checksum、固定宽度、布局和失败恢复，不证明目标打印机固件、DPI、纸宽、条高合规、HRI 字形或热敏/针式扫描质量。

## R105 本轮：ESC/POS Raster GS1 DataBar Omnidirectional/Truncated 主机编码

本轮为 ESC/POS Raster 增加 GS1 DataBar Omnidirectional 与 Truncated 的主机编码。输入沿统一模型保持 13 位 ASCII 数字 payload；主机按 GS1 DataBar/RSS-14 的四个数据字符、查找模式和 Mod-79 符号校验生成固定 96 模块的一维位图，再复用 `GS v 0`、模块缩放、旋转、HRI 和模板布局路径。Omnidirectional 与 Truncated 的横向结构相同，差异只在规范最小条高，因此两者共用编码串而由模板/Provider 的条高约束表达语义。

### R105 已完成 TODO

- [x] `EscPosRasterCanvas` 增加 13 位 payload 的 DataBar 数值分组、组合宽度解码、四数据字符、Mod-79 校验、finder pattern、96 模块位图和 Omnidirectional/Truncated 能力声明。
- [x] `PrintMatrixBudget` 增加固定 96 模块矩阵预算；非法输入在预算变更前失败，编码和页面绘制复用既有失败恢复语义。
- [x] Raster 独立命令、宽度测量、四方向 Provider、模板元素路径、能力快照和页面边界接入两个线性变体；HRI 统一显示隐含 `(01)`、13 位 payload 和 GTIN 校验位；Native DataBar 能力保持原有 Function B 语义。
- [x] 新增 96 模块 golden pattern、边界 payload、非法输入/预算恢复、直接命令、四方向 Provider、模板输出和能力传播回归。
- [x] `EscPosRasterGs1DataBarTests` 与既有 Native DataBar 回归在 `net6.0`、`netcoreapp3.1` 各 `30/30` 通过；Core 完整回归两套 TFM 各 `2813/2813` 通过。
- [x] Core 与 Templates 五目标框架构建 0 警告、0 错误；算法表格依据 [libzint rss.c](https://raw.githubusercontent.com/zint/zint/master/backend/rss.c) 与 [rss.h](https://raw.githubusercontent.com/zint/zint/master/backend/rss.h)，保留 BSD-3-Clause 归因。

### R105 未完成 TODO

- [x] Raster GS1 DataBar Limited 已在 R106 接入；Expanded、Stacked/Composite、China Post、Code 49 等变体仍在能力预检阶段严格拒绝。
- [ ] GS1 DataBar 目前只接受项目统一模型的 13 位 payload，尚未开放可选的 14 位含校验输入、AI `(01)` 前缀或复合链接字段；需要先定义跨协议输入契约和兼容测试。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、每种厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 尚未完成基于当前源码的全新隔离完整重建和新测试二进制执行；仍受静态资源、嵌套中间目录和运行中 DLL 锁影响。
- [ ] HRI 字体/样式、TSPL/ZPL 内置字体样式、二维码固件方向和设备固件版本组合仍未建立。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R105 本轮接受的限制与验证边界

- [x] Omnidirectional/Truncated 共用 96 模块线性编码串；Raster 只按请求条高绘制，不宣称自动替代规范最小条高校验或设备级全向扫描验收。
- [x] 本地回归证明数据字符、组合宽度、finder/checksum、固定宽度、布局和失败恢复，不证明目标打印机固件、DPI、纸宽、HRI 字形、条高合规或扫描质量。

## R104 本轮：ESC/POS Raster Telepenn 数值主机编码

本轮在 R103 的 Telepen 符号表基础上增加 `BarcodeType.Telepenn` 的 Raster 主机编码。输入沿现有统一模型保持 1–60 位 ASCII 数字；奇数长度前补零，每两位压缩为一个 Telepen Numeric 符号，按压缩后的 glyph 值求 Mod-127 校验位，再输出起始 `_`、payload、校验和结束 `z`。每个符号展开为固定 16 个模块并进入 `GS v 0` 路径。为保持与仓库已有 TSPL 契约一致，本轮不开放 Telepen 规范中的 `X` 扩展。

### R104 已完成 TODO

- [x] `EscPosRasterCanvas` 增加 Telepen Numeric 的数字压缩、奇数位补零、Mod-127 校验、起止符和主机位图编码。
- [x] `PrintMatrixBudget` 增加 1–60 位输入按打包符号数计算的保守矩阵预算；非法输入和预算失败恢复保持一致。
- [x] Raster 独立命令、宽度测量、四方向旋转、模板 Provider、能力快照和模板元素路径接入 `Telepenn`；ESC/POS Native 原生 Telepenn 仍严格拒绝。
- [x] 新增奇数位 golden pattern、00–99 全 glyph 范围、60 位上限、非法输入、失败预算恢复、直接命令、四方向 Provider 和模板输出回归。
- [x] `EscPosRasterTelepennTests` 在 `net6.0`、`netcoreapp3.1` 各 `14/14` 通过；TSPL Telepen 既有专项各 `37/37` 通过。
- [x] Core 完整回归在 `net6.0`、`netcoreapp3.1` 各 `2798/2798` 通过，0 失败、0 跳过；Core/Templates 五目标框架构建 0 警告、0 错误。

### R104 未完成 TODO

- [ ] Raster 仍未映射 GS1 DataBar Limited/Expanded、Stacked/Composite、China Post、Code 49 等变体；Omnidirectional/Truncated 已在 R105 接入主机编码。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、每种厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 尚未完成基于当前源码的全新隔离完整重建和新测试二进制执行；仍受静态资源、嵌套中间目录和运行中 DLL 锁影响。
- [ ] HRI 字体/样式、TSPL/ZPL 内置字体样式、二维码固件方向和设备固件版本组合仍未建立。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R104 本轮接受的限制与验证边界

- [x] Raster Telepenn 只声明纯数字统一语义，不把 Telepen Alpha、Telepen Numeric 或 TSPL 原生能力互相推断。
- [x] 本地回归证明压缩映射、奇数位补零、校验位、固定模块宽度、布局和失败恢复，不证明目标打印机固件、DPI、纸宽、条高、HRI 字形或扫描质量。

## R103 本轮：ESC/POS Raster Telepen Alpha 主机编码

本轮为 ESC/POS Raster 增加 `BarcodeType.Telepen` 的 Telepen Alpha 主机编码。编码器使用 Telepen 规范的 128 个 7-bit ASCII 符号宽度表，按原始 payload 求 Mod-127 校验位，输出起始 `_`、payload、校验符号和结束 `z`；每个符号展开为 16 个模块后进入统一 `GS v 0` 光栅路径。规范宽度表参考 [libzint telepen.c](https://github.com/zint/zint/blob/master/backend/telepen.c)，表格来源保留 BSD-3-Clause 归属说明。`Telepenn` 数值压缩模式仍只由 TSPL 原生 Provider 提供。

### R103 已完成 TODO

- [x] `EscPosRasterCanvas` 增加 Telepen Alpha 1–30 个 7-bit ASCII 字符的严格规范化、Mod-127 校验、起止符和主机位图编码。
- [x] `PrintMatrixBudget` 增加 Telepen 固定 16 模块/符号的保守矩阵预算；非法或超长输入在占用预算前失败。
- [x] Raster 独立命令、宽度测量、四方向旋转、模板 Provider、能力快照和模板元素路径复用统一主机编码路径；ESC/POS Native 原生 Telepen 仍严格拒绝。
- [x] 新增规范模块串 golden test、30 字符上限、非法 ASCII、失败预算恢复、直接命令、四方向 Provider 和模板输出回归。
- [x] `EscPosRasterTelepenTests` 在 `net6.0`、`netcoreapp3.1` 各 `13/13` 通过；TSPL Telepen 既有专项各 `37/37` 通过。
- [x] Core 完整回归在 `net6.0`、`netcoreapp3.1` 各 `2784/2784` 通过，0 失败、0 跳过；仅保留既有 `NU1900` 漏洞源不可达警告。

### R103 未完成 TODO

- [ ] Raster 仍未映射 GS1 DataBar、China Post、Code 49 等没有可靠仓库内主机编码器的变体；继续严格拒绝。
- [x] `BarcodeType.Telepenn` 的数值压缩算法已在 R104 接入 Raster；本轮历史记录保留 Alpha 与 Numeric 分步落地边界。
- [ ] Native 尚未穷举每个条码制式的所有业务 payload、每种厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] DesignerHost 尚未完成基于当前源码的全新隔离完整重建和新测试二进制执行；仍受静态资源、嵌套中间目录和运行中 DLL 锁影响。
- [ ] HRI 字体/样式、TSPL/ZPL 内置字体样式、二维码固件方向和设备固件版本组合仍未建立。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R103 本轮接受的限制与验证边界

- [x] Raster 只声明 Telepen Alpha，不把 Telepen Alpha 推断为 Telepenn 数值压缩，也不伪造 ESC/POS GS k 原生 Telepen 能力。
- [x] 本地回归证明规范表、校验位、固定模块宽度、布局和失败恢复，不证明目标打印机固件、DPI、纸宽、条高、HRI 字形或扫描质量。

## R102 本轮：ESC/POS Native profile 矩阵验证

本轮把 ESC/POS Native 的 17 个已声明一维条码制式与设备 profile 约束组合起来验证。完整 allowlist profile 下，Rows 模板的每个制式都能保留 HRI、GS k 制式和元素路径；关闭原生条码时在模板输出前严格失败；HRI 制式 allowlist 或位置 allowlist 收窄时也按元素路径失败。该轮不改变生产代码或公开 API，补齐的是能力声明、模板预检和 profile 组合的证据闭环。

### R102 已完成 TODO

- [x] 新增完整 Native profile allowlist 回归，覆盖 17 个 Function B 65–79 条码制式和统一模板路径。
- [x] 新增 `SupportsNativeBarcode=false` 的严格失败回归，确认不生成部分模板输出。
- [x] 新增 HRI 条码制式 allowlist 与 HRI 位置 allowlist 的严格失败回归，保留准确元素路径和中文原因。
- [x] 定向 `TemplateEscPosNativeFullMatrixTests` 在 `net6.0`、`netcoreapp3.1` 各 `12/12` 通过。
- [x] Core 完整回归在 `net6.0`、`netcoreapp3.1` 各 `2771/2771` 通过，0 失败、0 跳过；仅保留既有 `NU1900` 漏洞源不可达警告。

### R102 未完成 TODO

- [ ] Native 尚未穷举每个条码制式的所有业务 payload、每种厂商固件和真实 HRI 字形；当前证据是受控代表性矩阵，不替代设备验收。
- [ ] Raster 仍未映射 GS1 DataBar、China Post、Code 49 等没有可靠仓库内主机编码器的变体；Telepen Alpha 与 Telepenn Numeric 已分别在 R103/R104 接入。
- [ ] DesignerHost 尚未完成基于当前源码的全新隔离完整重建和新测试二进制执行；仍受静态资源、嵌套中间目录和运行中 DLL 锁影响。
- [ ] HRI 字体/样式、TSPL/ZPL 内置字体样式、二维码固件方向和设备固件版本组合仍未建立。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R102 本轮接受的限制与验证边界

- [x] profile allowlist 只证明 Provider 能按声明严格放行/拒绝，不证明目标设备真的实现每个 Function B 制式。
- [x] 跨 payload 组合、固件差异、DPI、纸宽、扫描和针式打印质量继续归为 `BLOCKED_EXTERNAL` 或后续 TODO。

## R101 本轮：ESC/POS Raster Code 11 主机编码

本轮为 ESC/POS Raster 增加 Code 11 主机编码。输入严格限制为 1–255 个 ASCII 数字或连字符；主机按 Code 11 规则追加 C 校验位，payload 超过 10 个字符时再追加 K 校验位，使用完整起止星号和窄空白模块串生成 `GS v 0` 位图。能力快照、独立 Raster 命令、模板预检、宽度测量、旋转、HRI、页边界、资源预算和失败恢复复用既有 Raster 路径；ESC/POS Native 原生命令仍保持不支持。

### R101 已完成 TODO

- [x] `EscPosRasterCanvas` 增加 Code 11 规范化、C/K 校验、符号模式、位图生成和 `SupportsBarcodeType` 能力传播。
- [x] `PrintMatrixBudget` 增加 Code 11 符号矩阵的保守内存上界；校验或编码失败不会留下工作预算。
- [x] 新增直接命令、宽度缩放、短/长 payload 校验、非法输入、超长输入、四向旋转、能力快照和模板输出回归。
- [x] 修正 TSPL/ZPL Code 11 能力矩阵测试：ESC/POS Raster 支持主机光栅，Native 仍不支持原生命令。
- [x] 定向回归在 `net6.0`、`netcoreapp3.1` 各 `13/13` 通过；Core/Templates 五目标框架构建 0 警告、0 错误。
- [x] Core 完整回归在 `net6.0`、`netcoreapp3.1` 各 `2768/2768` 通过，0 失败、0 跳过；仅保留既有 `NU1900` 漏洞源不可达警告。

### R101 未完成 TODO

- [ ] Raster 仍未映射 GS1 DataBar、China Post、Telepen、Code 49 等没有可靠仓库内主机编码器的变体；继续严格拒绝。
- [ ] Native 条码完整笛卡尔矩阵仍未关闭：静区、旋转、逐制式 payload、跨页布局和设备 profile 组合仍需分层验证。
- [ ] DesignerHost 尚未完成基于当前源码的全新隔离完整重建和新测试二进制执行；仍受静态资源、嵌套中间目录和运行中 DLL 锁影响。
- [ ] HRI 字体/样式、TSPL/ZPL 内置字体样式、二维码固件方向和设备固件版本组合仍未建立。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R101 本轮接受的限制与验证边界

- [x] Code 11 的 Raster 输入只接受 ASCII 数字和连字符，校验位由主机生成；该路径不宣称 ESC/POS GS k 原生 Code 11 能力。
- [x] 本地回归证明模式、校验位、布局宽度和失败恢复，不证明目标打印机固件、DPI、纸宽、条高、HRI 字形或扫描质量；Code 11 的标准字符模式与校验语义参考 [Code 11](https://en.wikipedia.org/wiki/Code_11)。
- [x] `NU1900` 漏洞源不可达、Host 隔离构建、真实设备、外部 CI、性能和发布验证继续保留为环境/外部门禁。

## R100 本轮：ESC/POS Native 数值边界矩阵

本轮补齐独立 ESC/POS Native Function B 原生条码的数值边界证据。测试覆盖模板能力矩阵中的 17 个一维条码类型（I2OF5、I2OF5C、I2OF5G 共享 Function B 70，但分别保留业务载荷），对模块宽度 2/6、条高 1/255、HRI 位置 0/1/2/3 的四组端点组合逐一检查 GS H、GS h、GS w、GS k 字节；同时对条高 0、模块宽度 1/7、HRI 位置 4 验证异常前缓冲区不污染，并锁定公开能力范围与命令校验一致。

### R100 已完成 TODO

- [x] 新增 `EscPosNativeNumericBoundaryMatrixTests`，覆盖 17 个 Native 条码类型的 68 个合法数值端点组合和 68 个非法边界恢复场景。
- [x] 每个合法组合验证 `GS H`、`GS h`、`GS w`、`GS k` 顺序、制式编号、长度字段和可重复构建结果；Function B 70 使用直接命令可接受的偶数位 ITF 载荷。
- [x] 能力快照断言 `CommandBarcodeModuleWidth=2..6`、`CommandBarcodeHeight=1..255`，并逐项确认 17 个模板条码类型仍被 Native 能力声明支持。
- [x] 定向回归在 `net6.0`、`netcoreapp3.1` 各 `3/3` 通过；本轮仅增加测试证据，未改变生产 API、协议字节或目标框架。
- [x] Core 完整回归在 `net6.0`、`netcoreapp3.1` 各 `2755/2755` 通过，0 失败、0 跳过；仅保留既有 `NU1900` 漏洞源不可达警告。

### R100 未完成 TODO

- [ ] Native 条码完整笛卡尔矩阵仍未关闭：本轮覆盖数值端点和 HRI 位置，但静区、旋转、逐制式 payload 变体、跨页布局及设备 profile 组合仍需分层验证。
- [ ] DesignerHost 尚未完成基于当前源码的全新隔离完整重建和新测试二进制执行；Host 仍受旧静态资源清单、嵌套中间目录和运行中的 `DesignerHost.dll` 锁影响。
- [ ] Raster 仍未映射 GS1 DataBar、Code 11、China Post、Telepen、Code 49 等需要独立编码规则或可靠主机编码器的变体；无协议手册/样本时继续严格拒绝。
- [ ] HRI 字体/样式、TSPL/ZPL 内置字体样式、二维码固件方向和设备固件版本组合仍未建立。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R100 本轮接受的限制与验证边界

- [x] Function B 70 的直接 ESC/POS 命令按 ITF 规范要求偶数位载荷；模板层 I2OF5G 的业务输入归一化不在本轮直接命令矩阵中重复推断。
- [x] 数值端点测试证明主机参数校验、字节字段和失败恢复，不证明所有打印机固件都实现 17 个制式、HRI 字形或相同的物理条高。
- [x] `NU1900` 漏洞源不可达、Host 隔离构建、真实设备、外部 CI、性能和发布验证继续保留为环境/外部门禁。

## R99 本轮：ESC/POS Raster Plessey 主机编码子范围

本轮关闭 ESC/POS Raster 条码能力中可由现有可靠编码器表达的 `BarcodeType.Plessey` 子范围。Raster 复用 ZXing.Net 0.16.11 的 `PlesseyWriter` 生成位序，移除编码器自带的空白边界后再按统一模块宽度绘制；统一能力快照、独立 `RasterBarcode`、模板 Provider、宽度度量、旋转/HRI 和资源回滚共享同一规范化路径。输入契约与 TSPL/ZPL 统一模型保持一致，只接受非空 ASCII 数字。

### R99 已完成 TODO

- [x] `EscPosRasterCanvas` 将 `Plessey` 纳入主机条码集合，使用 ZXing `PlesseyWriter` 生成并裁剪编码器空白边界，保留完整条码位序和 CRC。
- [x] Plessey 的宽度测量、模块缩放、旋转、HRI 预留、页面边界、预算和失败恢复复用现有 Raster 路径；不会把编码器默认静区重复叠加到统一布局。
- [x] 独立 `EscPosPrintCommand.RasterBarcode` 与统一模板 Provider 可输出 Plessey `GS v 0` 光栅数据；元素路径和能力预检保持一致。
- [x] 非空、纯 ASCII 数字校验在编码前执行；编码器异常不会留下部分画布或资源预算，失败后可继续使用同一资源作用域。
- [x] 新增 Raster 直接入口、模板入口、能力传播、模块宽度缩放和非法输入回归；Plessey/TSPL/ZPL 能力矩阵在 `net6.0`、`netcoreapp3.1` 精准集各 `83/83` 通过。
- [x] Core 完整回归在 `net6.0` 与 `netcoreapp3.1` 各 `2752/2752` 通过；Core/Templates 五目标框架构建 0 警告、0 错误。

### R99 未完成 TODO

- [ ] DesignerHost 尚未完成基于当前源码的全新隔离完整重建和新测试二进制执行；Host 仍受旧静态资源清单、嵌套中间目录和运行中的 `DesignerHost.dll` 锁影响。
- [ ] Native 条码数值笛卡尔矩阵（模块宽度、条高、静区、HRI 位置、旋转）仍未覆盖全部逐协议逐制式组合；本轮只增加 Raster Plessey 主机映射。
- [ ] Raster 仍未映射 GS1 DataBar、Code 11、China Post、Telepen、Code 49 等需要独立编码规则或可靠主机编码器的变体；无协议手册/样本时继续严格拒绝。
- [ ] HRI 字体/样式、TSPL/ZPL 内置字体样式、二维码固件方向和设备固件版本组合仍未建立。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R99 本轮接受的限制与验证边界

- [x] Raster Plessey 只接受非空 ASCII 数字；设备原生 Plessey 命令仍由 TSPL/ZPL Provider 表达，ESC/POS Raster 不宣称 GS k 原生 Plessey。
- [x] ZXing 编码器返回的默认空白边界在主机侧裁剪，统一模型的显式静区仍由 Raster 画布单独管理；真实条高、DPI、纸宽和扫描质量仍需实机验收。
- [x] `NU1900` 漏洞源不可达、Host 隔离构建、真实设备、外部 CI、性能和发布验证继续保留为环境/外部门禁。

## R98 本轮：ESC/POS Raster ITF-14 主机编码子范围

本轮关闭 ESC/POS Raster 条码能力中可由主机可靠表达的 `BarcodeType.Itf14` 子范围。Raster 接受 13 位 ITF-14 payload，在主机侧按既有 Mod10 规则生成第 14 位校验位，再复用 ZXing ITF 矩阵；统一能力快照、模板 Provider、独立 Raster 命令、宽度度量和失败恢复共享同一规范化路径。TSPL 的原生 `ITF14` 设备语义不变，其他协议仍按各自能力矩阵处理。

### R98 已完成 TODO

- [x] `EscPosRasterCanvas` 将 `Itf14` 纳入主机条码集合，13 位数字 payload 生成 Mod10 校验位并映射到 ITF 编码。
- [x] Raster 独立 `RasterBarcode`、`PrintCapabilities`、Provider 和统一模板能力快照自动暴露 `Itf14`；HRI、旋转、静区、页面边界和资源预算复用现有 Raster 路径。
- [x] 非空、恰好 13 位、纯数字输入校验在编码前执行；非法输入不会产生部分矩阵，失败后可继续使用同一资源作用域。
- [x] 新增 Raster 回归：能力、13/14 位等价宽度和位图、模板元素路径、非法输入边界；`EscPosRasterBarcodeTypeTests` 在 `net6.0`、`netcoreapp3.1` 各 `38/38` 通过。
- [x] 当前 FIM A–E 主机模式已由既有生产代码和测试覆盖；本轮同步把旧文档中的 FIM D/E 历史描述标为过时，不重复改动该实现。
- [x] Core 完整回归 `net6.0` 与 `netcoreapp3.1` 各 `2747/2747`；Core/Templates 五目标框架 L0 构建 0 警告、0 错误。

### R98 未完成 TODO

- [ ] DesignerHost 尚未完成基于当前源码的全新隔离完整重建和新测试二进制执行；Host 仍受旧静态资源清单、嵌套中间目录和运行中的 `DesignerHost.dll` 锁影响。
- [ ] Native 条码数值笛卡尔矩阵（模块宽度、条高、静区、HRI 位置、旋转）仍未覆盖全部逐协议逐制式组合；本轮只增加 Raster ITF-14 主机映射。
- [ ] Raster 仍未映射 GS1 DataBar、Plessey、Code 11、China Post、Telepen、Code 49 等需要独立编码规则或可靠主机编码器的变体；无协议手册/样本时继续严格拒绝。
- [ ] HRI 字体/样式、TSPL/ZPL 内置字体样式、二维码固件方向和设备固件版本组合仍未建立。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R98 本轮接受的限制与验证边界

- [x] Raster ITF-14 入口只接受 13 位 payload，由主机生成最后一位校验位；已包含 14 位完整值不会被静默接受或重复校验。
- [x] 输出复用 ITF 矩阵，未额外伪造 ITF-14 bearer bars；目标设备的 DPI、条高、静区和扫描质量仍需实机验收。
- [x] `NU1900` 漏洞源不可达、Host 隔离构建、真实设备、外部 CI、性能和发布验证继续保留为环境/外部门禁。

## R97 本轮：ESC/POS Raster Code128 auto 主机光栅子范围

本轮关闭 ESC/POS Raster 条码能力中可由主机可靠表达的 `BarcodeType.Code128Auto` 子范围。Raster 复用 ZXing Code 128 自动分组生成位图；统一能力快照、模板 Provider、独立 Raster 命令和输入校验保持同一语义。Native 的 Function B 79 原始字节入口继续保留，Raster 不承诺把任意 8-bit 原始 payload 转成可扫描的文字条码。

### R97 已完成 TODO

- [x] `EscPosRasterCanvas` 将 `Code128Auto` 纳入稳定主机编码集合，复用 Code 128 矩阵、宽度度量、旋转/HRI/页边界和资源预算路径。
- [x] Raster Code128 auto 严格接受 1–255 个 7-bit ASCII 字符；空值、超长和非 ASCII 数据在画布变更前拒绝，失败后仍可结束空白页并继续构建。
- [x] `PrintCapabilities`、模板 Provider 和独立 `EscPosPrintCommand.RasterBarcode` 自动暴露该能力；Native 原始 `BarcodeCode128Auto(byte[])` 语义保持不变。
- [x] 新增 Raster 类型回归：Code128 auto 与 Code128 的主机矩阵/宽度一致、模板输出包含 Raster 指令、非法输入不污染画布；`EscPosRasterBarcodeTypeTests` 在 `net6.0`、`netcoreapp3.1` 各 `31/31` 通过。
- [x] Core 完整回归 `net6.0` 与 `netcoreapp3.1` 各 `2740/2740`；Core/Templates 五目标框架 L0 构建 0 警告、0 错误；本轮未改公开枚举值、目标框架、版本或包配置。

### R97 未完成 TODO

- [ ] DesignerHost 尚未完成基于当前源码的全新隔离完整重建和新测试二进制执行；Host 仍受旧静态资源清单、嵌套中间目录和运行中的 `DesignerHost.dll` 锁影响。
- [ ] Native 条码数值笛卡尔矩阵（模块宽度、条高、静区、HRI 位置、旋转）仍未覆盖全部逐协议逐制式组合；本轮只增加 Raster Code128 auto 主机映射。
- [ ] HRI 字体/样式、TSPL/ZPL 内置字体样式、二维码固件方向和设备固件版本组合仍未建立；本轮不把 Raster 主机字体结果扩展为固件证明。
- [ ] 仍有未映射的 ESC/POS、TSPL、ZPL 条码变体和厂商扩展；没有协议手册或样本时继续严格拒绝，不推断字节语义。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R97 本轮接受的限制与验证边界

- [x] Raster `Code128Auto` 只接受字符串形式的 7-bit ASCII；需要任意 0–255 字节时仍须使用 Native `BarcodeCode128Auto(byte[])`，两者不混淆。
- [x] 主机 Code 128 自动分组与基础 Code 128 复用同一 ZXing 矩阵，输出等价仅表示本地位图编码语义，不代表所有 ESC/POS 固件的 Function B 79 行为一致。
- [x] `NU1900` 漏洞源不可达、Host 隔离构建、真实设备、外部 CI、性能和发布验证继续保留为环境/外部门禁，不伪造为本地 PASS。

## R96 本轮：TSPL/ZPL 原生二维码旋转白名单

本轮关闭“二维码方向”中可由协议和设备 profile 明确表达的子范围：TSPL/ZPL profile 新增可选原生二维码非零旋转角度列表，统一能力、直接命令、模板预检、Host JSON 和 Designer 项目模型使用同一份严格契约。`null` 保持旧行为并允许全部非零角度，空数组只允许不旋转；`None` 不作为白名单项。未配置 profile 的旧 TSPL/ZPL 输出不变。

### R96 已完成 TODO

- [x] `TsplPrinterProfile`、`ZplPrinterProfile` 和不可变 `LabelDeviceContract` 支持 `SupportedNativeQrCodeRotations`；快照克隆并拒绝 `None`、未定义值和重复角度。
- [x] `PrintCapabilities` 增加按角度查询和公开能力列表；`TemplateProtocolCapabilities`、Provider 预检按实际二维码角度过滤，保留 `None` 的兼容语义。
- [x] TSPL `QRCODE`、ZPL `^BQ` 独立命令在参数校验后严格执行 profile 白名单；失败不会追加部分输出，未配置 profile 的四角度字节输出保持兼容。
- [x] DesignerHost 请求 DTO、枚举解析和 labelProfile 能力 JSON 支持旋转列表；Designer API、项目导入/导出、能力解析和机型编辑器支持往返及非法/重复值校验。
- [x] 新增 C# 二维码 profile/模板回归 `9/9`（`net6.0`、`netcoreapp3.1` 各 `9/9`），覆盖快照、直接命令、模板路径、空数组和拒绝边界；Host 契约测试覆盖能力字段。
- [x] Core/Templates 完整回归 `net6.0` 与 `netcoreapp3.1` 各 `2735/2735`；Core 与模板五目标框架构建通过；Designer Vitest `250/250`、vue-tsc/Vite 构建和 `git diff --check` 通过。

### R96 未完成 TODO

- [ ] DesignerHost 尚未完成基于当前源码的全新隔离完整重建和新测试二进制执行；Host 应用 `CoreCompile` 通过，但完整输出仍受旧静态资源清单/嵌套中间目录和运行中的 `DesignerHost.dll` 锁影响，不能把旧二进制当作本轮新证据。
- [ ] HRI 字体/样式、二维码固件差异、设备固件版本，以及 HRI 位置与字体的组合能力仍未建立；二维码旋转白名单不等于实机固件兼容证明。
- [ ] Native 条码数值笛卡尔矩阵（模块宽度、条高、静区、位置、旋转）仍未覆盖全部逐协议逐制式组合；本轮只增加二维码方向 profile 子范围。
- [ ] 仍有未映射的 ESC/POS、TSPL、ZPL 条码变体和厂商扩展；没有协议手册或样本时继续严格拒绝，不推断字节语义。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R96 本轮接受的限制与验证边界

- [x] 白名单只描述 TSPL/ZPL 原生 `QRCODE`/`^BQ` 角度能力；ESC/POS Raster 的二维码旋转仍由主机光栅化，ESC/POS Native 不因该字段改变。
- [x] `null` 表示不收窄，空数组表示禁用非零旋转；不旋转始终单独处理，避免把 `None` 混入设备白名单。
- [x] Host 完整输出构建失败属于当前工作区构建状态，不代表核心协议实现失败；需在不复用旧中间目录且无运行进程锁的隔离环境中重新构建后再关闭门禁。
- [x] `NU1900` 漏洞源不可达警告和真实设备、外部 CI、发布验证仍不在本地证据范围内。

## R95 本轮：ESC/POS Native HRI 位置能力

本轮将 ESC/POS Native 条码 HRI 从单一“支持/不支持”进一步细化为 `None`、`Above`、`Below`、`Both` 四种协议语义。Profile、能力快照、独立命令、统一模板、Designer Host DTO 和浏览器项目模型保持同一严格契约；Raster 仍由主机绘制 HRI，因此不受 Native 位置列表限制。旧的 `byte textPosition` 入口保持不变，默认 `Below` 的模板条码会在能力不允许时于渲染前失败。

### R95 已完成 TODO

- [x] 新增 `EscPosBarcodeTextPosition` 语义枚举和 Profile 可选 allowlist；`null` 保持兼容并允许全部位置，空列表禁用全部原生 HRI，`None` 不允许作为 allowlist 项。
- [x] `PrintCapabilities`、`TemplateProtocolCapabilities`、ESC/POS Native 直写和模板预检按 HRI 位置执行严格能力检查；位置不支持时保留明确协议和参数原因。
- [x] Host JSON 能力响应、Profile 请求 DTO、Designer 项目导入/导出、能力解析和 Profile 编辑器支持 `Above`/`Below`/`Both` 往返与重复/非法值校验。
- [x] 刷新 Core 的 net7 依赖资产后，`DesignerHost` 当前源代码 `CoreCompile` 通过；TSPL/ESC/POS Profile 字段归属错误已修正。
- [x] 新增 Profile、直接命令和模板默认位置回归；Core 精准集 `5/5`（`net6.0`、`netcoreapp3.1`），受影响精准集 `68/68`（`net6.0`）。
- [x] Core 完整回归在 `net6.0` 与 `netcoreapp3.1` 均为 `2726/2726`；Core/模板项目编译、前端 Vitest `249/249`、Vite 构建和 `git diff --check` 通过。

### R95 未完成 TODO

- [ ] DesignerHost 尚未完成基于当前源代码的全新隔离完整重建和新测试二进制执行；当前源代码编译已通过，但现有 net8 输出目录只有旧产物，正常构建仍受工作区运行进程锁、旧静态资源清单和 TFM 依赖图影响，不能把旧二进制回归当作本轮新证据。
- [ ] HRI 字体/样式、二维码方向、设备固件版本和位置与字体的组合能力尚未建立；本轮只完成 ESC/POS Native HRI 位置枚举。
- [ ] Native 条码数值笛卡尔矩阵（模块宽度、条高、静区、位置、旋转）仍未覆盖所有逐协议逐制式组合；已完成的代表性边界不扩展为完整声明。
- [ ] 仍有未映射的 ESC/POS、TSPL、ZPL 条码变体和厂商扩展；没有协议手册或样本时继续严格拒绝，不推断字节语义。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R95 本轮接受的限制与验证边界

- [x] `None` 只表示不生成 HRI，不作为机型 allowlist 项；直接命令仍接受原有 `byte` 参数并在输出前映射到语义枚举。
- [x] ESC/POS Raster 的 HRI 由主机光栅化，位置 allowlist 只描述 Native 固件能力；Raster 能力快照不报告 Native 位置列表。
- [x] Host `CoreCompile` 已通过；完整输出构建失败属于当前工作区外部构建状态（旧 net8 程序集/静态资源清单未包含本轮结果，且默认 apphost 被运行进程锁定），不是已验证的生产代码失败；需在干净且不复用旧中间目录的隔离输出中重建后再关闭该门禁。
- [x] `NU1900` 漏洞源不可达警告和真实设备、外部 CI、发布验证仍不在本地证据范围内。

Task：多指令集打印剩余 TODO。Round：R94 按条码制式细化原生 HRI 能力。更新时间：2026-10-07。

原范围保持：CPCL API/输出兼容，ESC/POS、TSPL、ZPL 独立命令与统一模板输出，严格能力校验、设计器和验证；生成字节，不负责连接、驱动、发现或实际发送。

## R94 本轮：按条码制式细化原生 HRI 能力

本轮把 R93 保留的“按条码制式细化 HRI”从笼统机型开关落到 Profile、能力快照、独立命令、统一模板、Designer Host 和浏览器项目模型。`null` 列表保持不收窄，空列表明确禁用全部原生 HRI；全局关闭仍优先生效。ESC/POS Raster 的 HRI 由主机光栅化，不受 Native 列表限制；ZPL GS1 DataBar 没有原生 HRI 参数，按条码制式报告不支持。

### R94 已完成 TODO

- [x] ESC/POS、TSPL、ZPL Profile 新增可选原生 HRI 条码制式列表，并在快照时校验未知值和重复值；旧 `null`/全局布尔语义保持兼容。
- [x] `PrintCapabilities` 和 `TemplateProtocolCapabilities` 新增按 `BarcodeType` 查询能力；模板预检、统一 Provider 和独立命令均按具体制式严格拒绝不支持的 HRI。
- [x] ESC/POS Native、TSPL 和 ZPL 直写条码入口完成制式映射；Raster 路径继续使用主机 HRI，不受 Native allowlist 误伤；ZPL DataBar 无 HRI 能力保持明确。
- [x] Host JSON 能力响应、Profile 请求 DTO、Designer 项目导入/导出、能力解析和 Profile 编辑器均支持列表往返与非法值校验。
- [x] 新增 HRI Profile 精准回归：Core HRI Profile `7/7`（`net6.0`、`netcoreapp3.1`），Label Profile `37/37`，Host 既有 `net8.0` 测试二进制 `112/112`；前端 Vitest `247/247`、Vite 构建通过。
- [x] 受影响 Core 完整测试在 `net6.0` 与 `netcoreapp3.1` 各 `2721/2721` 通过；核心项目、模板项目编译和 `git diff --check` 通过。

### R94 未完成 TODO

- [ ] Native 条码数值范围仍未完成全部笛卡尔矩阵：模块宽度、条高、静区尺寸、HRI 位置枚举和所有旋转角度仍需逐协议逐制式验证。
- [ ] Profile 尚未细化 HRI 位置、HRI 字体/样式、二维码方向和设备固件版本；当前只完成一维条码制式级 HRI allowlist。
- [ ] DesignerHost 本轮未完成一次全新隔离重建和新测试二进制执行；正常增量构建受工作区现有运行进程锁及旧静态资源清单阻塞，源码 CoreCompile 已通过。
- [ ] 仍有未映射的 ESC/POS、TSPL、ZPL 条码变体和厂商扩展；没有协议手册或样本时继续严格拒绝，不推断字节语义。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实热敏/针式打印扫描、切裁和纸张进给仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局、发送中断和重试策略仍未形成证据。

### R94 本轮接受的限制与验证边界

- [x] `null` allowlist 表示“不收窄”，空列表表示“全部原生 HRI 禁用”；这两个状态与全局 `SupportsNativeBarcodeHumanReadable=false` 的优先级已由单测固定。
- [x] ESC/POS Raster 的 HRI 由主机绘制，故 `SupportsBarcodeHumanReadableType` 对 Raster 不受 Native allowlist 影响；这不是对目标固件原生 HRI 的承诺。
- [x] Host 旧二进制 `112/112` 仅作为非破坏性回归证据；源码 CoreCompile 和 DTO/测试源码检查通过，但不把它写成新 Host 隔离重建结果。
- [x] `NU1900` 漏洞源不可达警告保留为环境限制；真实设备、外部 CI 和发布仍不在本地证据范围内。

## R93 本轮：ESC/POS Native 全条码制式状态组合矩阵

本轮把 R92 的独立边界组合为状态级笛卡尔矩阵：17 个制式分别验证“零静区 + 无旋转 + 动态尺寸”的成功路径，以及“非零静区”“旋转”“非零静区 + 旋转”三种 Native 不可表达组合的严格失败路径，共 68 个条码场景。每个失败仍保留模板元素路径和静区/旋转原因，成功路径继续检查 `GS H`、`GS h`、`GS w`、`GS k` 和动态布局高度。

### R93 已完成 TODO

- [x] 17 个 Native 条码 × 4 个静区/旋转状态组合全部覆盖：17 个成功场景和 51 个严格失败场景。
- [x] 有效组合确认零静区、无旋转、自动宽度和动态 HRI 预留高度可以共同输出；无效组合不会静默降级到其他协议语义。
- [x] `TemplateEscPosNativeFullMatrixTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `9/9` 通过；本轮没有修改生产 Provider、公开 API 或打包配置。

### R93 未完成 TODO

- [ ] Native 条码参数的数值范围矩阵仍可继续扩展到全部非零旋转角度、多个静区尺寸、模块宽度和条高边界；当前已闭合状态级组合，不宣称所有数值笛卡尔积完成。
- [ ] Profile 仍未细化到按条码制式、HRI 位置、文字样式和二维码方向的组合能力。
- [ ] 其他未映射的 ESC/POS、TSPL、ZPL 条码变体和厂商扩展仍需明确协议语义与专用 Provider；当前继续按能力矩阵严格拒绝。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实打印扫描和切裁仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局和发送中断重试仍未形成证据。

### R93 本轮接受的限制

- [x] Native 的旋转和非零静区组合按设计 Fail Fast；需要几何旋转或显式静区时应选择 ESC/POS Raster、TSPL 或 ZPL，并由对应能力矩阵决定输出。
- [x] `NU1900` 漏洞源不可达警告保留为环境限制；本轮复用未受影响的前端、Host、包消费者和性能证据。

## R92 本轮：ESC/POS Native 全条码制式参数边界矩阵

本轮补齐 R91 遗留的三个 Native 参数边界子范围。对全部 17 个已声明一维条码制式分别验证：非零静区在模板路径处严格拒绝；条码旋转在模板路径处严格拒绝；Sequential 动态布局使用 `Width=0` 自动计算宽度，并逐项改变条高与可读文字预留高度。Rows 的兼容模式仍明确保持单行原点约束，后续动态 Y 不被静默转换。

### R92 已完成 TODO

- [x] 全部 17 项 Native 条码的非零静区严格拒绝，错误保留 `Rows[0].Cells[0].Elements[0]` 元素路径。
- [x] 全部 17 项 Native 条码的四方向之外旋转边界（代表性 `Rotate90`）严格拒绝，错误保留元素路径。
- [x] Sequential 全部 17 项覆盖自动宽度、动态条高和动态 HRI 预留高度，输出 `GS h` 与布局轨迹保持一致。
- [x] Rows 全部 17 项的非原点第二行严格拒绝，错误保留 `Rows[1].Cells[0].Elements[0]` 路径，避免兼容模式静默错位。
- [x] `TemplateEscPosNativeFullMatrixTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `7/7` 通过；确认 Rows 多行动态 Y 是既定严格边界，没有改动生产 Provider。

### R92 未完成 TODO

- [ ] 全部 17 项静区、旋转、动态尺寸的状态组合已在 R93 覆盖；全部数值范围笛卡尔矩阵仍未逐格执行。
- [ ] Profile 仍未细化到按条码制式、HRI 位置、文字样式和二维码方向的组合能力。
- [ ] 其他未映射的 ESC/POS、TSPL、ZPL 条码变体和厂商扩展仍需明确协议语义与专用 Provider；当前继续按能力矩阵严格拒绝。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实打印扫描和切裁仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局和发送中断重试仍未形成证据。

### R92 本轮接受的限制

- [x] ESC/POS Native Rows 是兼容的单行原点模式；多行动态 Y 由 Provider 严格拒绝，复杂动态布局应使用 Sequential 或 Raster。
- [x] `NU1900` 漏洞源不可达警告保留为环境限制；本轮未重复未受影响的前端、Host、包消费者和性能证据。

## R91 本轮：ESC/POS Native 全条码制式跨页矩阵

本轮在 R90 的 17 项基础矩阵之上，使用三页逻辑页面强制 Sequential 模板分页，验证全部条码制式的元素来源路径、页内坐标、页边界和输出顺序。每个条码仍检查 HRI、条高、模块宽度和 `GS k` 制式字节，确保分页不会丢失或重排 Native 输出。

### R91 已完成 TODO

- [x] ESC/POS Native Sequential 全部 17 个已声明一维条码制式均能跨多页输出，并保留每个 `Rows[index].Cells[0].Elements[0]` 来源路径。
- [x] 跨页结果逐页包含非空输出；每个元素的页内 Y 坐标和高度均在所属逻辑页边界内。
- [x] `TemplateEscPosNativeFullMatrixTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `3/3` 通过；没有修改生产 Provider 或公开契约。

### R91 未完成 TODO

- [ ] Native 全矩阵的独立边界和状态组合已在 R92/R93 覆盖；全部数值范围组合仍未逐格执行。
- [ ] Profile 仍未细化到按条码制式、HRI 位置、文字样式和二维码方向的组合能力。
- [ ] 其他未映射的 ESC/POS、TSPL、ZPL 条码变体和厂商扩展仍需明确协议语义与专用 Provider；当前继续按能力矩阵严格拒绝。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实打印扫描和切裁仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局和发送中断重试仍未形成证据。

### R91 本轮接受的限制

- [x] 三页是受控逻辑模板分页夹具，用来验证布局和字节顺序，不代表任一真实设备的纸长或分页固件行为。
- [x] `NU1900` 漏洞源不可达警告保留为环境限制；本轮复用未受影响的前端、Host、运行时矩阵和包消费者证据。

## R90 本轮：ESC/POS Native 全条码制式矩阵

本轮将 ESC/POS Native 能力快照中声明的 17 个一维条码制式放入同一组数据驱动模板回归，分别覆盖 Rows 和 Sequential 路径。测试同时检查 HRI 位置、条高、模块宽度、原生 `GS k` 制式映射、元素来源路径和布局轨迹；Sequential 使用连续行验证全矩阵不会在模板布局中丢失或改写条码顺序。测试容器对 DataBar 的协议最小高度和长页布局留出足够空间，未把容器尺寸误当成设备纸宽结论。

### R90 已完成 TODO

- [x] ESC/POS Native Rows 覆盖当前能力声明的全部 17 个一维条码制式：Code128、Code39、Code93、EAN-8、EAN-13、UPC-A、UPC-E、Codabar、I2OF5/I2OF5C/I2OF5G、UCC/EAN-128、四种 GS1 DataBar 和 Code128Auto。
- [x] ESC/POS Native Sequential 使用同一 17 项矩阵覆盖连续行输出，逐项校验元素路径、HRI、条高、模块宽度和 `GS k` 制式字节。
- [x] `TemplateEscPosNativeFullMatrixTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `2/2` 通过；本轮没有改动生产 Provider 或既有协议字节行为。

### R90 未完成 TODO

- [ ] Native 全矩阵的跨页基础组合、独立参数边界和状态组合已在 R91–R93 覆盖；全部数值范围组合仍未完成验证。
- [ ] Profile 仍未细化到按条码制式、HRI 位置、文字样式和二维码方向的组合能力。
- [ ] 其他未映射的 ESC/POS、TSPL、ZPL 条码变体和厂商扩展仍需明确协议语义与专用 Provider；当前继续按能力矩阵严格拒绝。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实打印扫描和切裁仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局和发送中断重试仍未形成证据。

### R90 本轮接受的限制

- [x] 本轮矩阵验证的是当前 Provider 的能力声明、模板预检和本地字节映射；不替代目标固件对各条码制式、HRI 字形或扫描质量的实机验收。
- [x] `NU1900` 漏洞源不可达警告保留为环境限制，不把它解释为依赖漏洞已清零；本轮没有重复未受影响的前端、Host 或完整解决方案回归。

## R89 本轮：Designer HRI Profile 浏览器端到端回归

本轮完成 R88 遗留的 Designer `labelProfiles.spec.ts` 独立 Host 回归。使用当前源码隔离构建 `DesignerHost`，在独立 API/Vite 端口运行 TSPL/ZPL 配置能力查询、边界预览、项目导入导出、HRI Profile 字段保存往返、能力过滤和非法草稿阻断；同时修正新增“支持原生条码可读文字”字段与旧标签前缀匹配造成的 Playwright 严格定位歧义。

### R89 已完成 TODO

- [x] TSPL/ZPL `labelProfiles.spec.ts` 在当前源码 Host 和独立 Vite 端口执行 `8/8`，覆盖 HRI Profile 能力字段与 Designer 保存/重载输出字节一致性。
- [x] 新增 HRI 字段后，Designer E2E 使用精确标签匹配，避免“支持原生条码”与“支持原生条码可读文字”的前缀歧义。
- [x] 前端 Vitest `19` 个文件、`247/247` 通过；`vue-tsc` 与 Vite 生产构建通过。
- [x] 隔离 Host 使用 `tools/DesignerHost/artifacts/r89-host-build/net8.0` 输出构建，0 warning、0 error；浏览器回归没有复用旧 Host 二进制。

### R89 未完成 TODO

- [ ] Native Rows/Sequential 的全条码制式基础参数、跨页基础组合、独立边界和状态组合已在 R90–R93 覆盖；数值范围组合仍未完成验证。
- [ ] Profile 仍未细化到按条码制式、HRI 位置、文字样式和二维码方向的组合能力。
- [ ] 其他未映射的 ESC/POS、TSPL、ZPL 条码变体和厂商扩展仍需明确协议语义与专用 Provider；当前继续按能力矩阵严格拒绝。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实打印扫描和切裁仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局和发送中断重试仍未形成证据。

### R89 本轮接受的限制

- [x] 浏览器回归证明 Host/Designer 的请求契约、保存往返和能力过滤；不证明目标打印机 HRI 字形、位置、字高或条码扫描结果。
- [x] 独立 Host 使用本地隔离输出和已恢复依赖；NuGet 漏洞源不可达的 `NU1900` 环境限制仍不作为硬件或性能通过证据。

## R88 本轮：TSPL/ZPL 机型 HRI 能力契约

本轮补齐标签机型 Profile 对原生一维条码可读文字（HRI）的声明能力。新增 `SupportsNativeBarcodeHumanReadable`，贯通 `LabelDeviceContract`、`PrintCapabilities`、TSPL/ZPL 独立命令、统一 Provider 和模板预检；对 TSPL/ZPL 的 `HumanReadable=null` 按协议默认开启处理，机型明确禁用时在生成字节前严格失败，显式 `false` 仍可输出无 HRI 条码。

### R88 已完成 TODO

- [x] TSPL/ZPL Profile 可独立声明原生 HRI 能力，默认值保持兼容为 `true`。
- [x] 独立 `TsplPrintCommand`/`ZplPrintCommand` 在 HRI 请求与机型禁用冲突时拒绝输出，关闭 HRI 的调用保持可用。
- [x] 统一 Provider 和模板 Graph 对协议默认 HRI、显式开启和显式关闭使用一致语义，并保留元素路径错误。
- [x] `LabelBarcodeHumanReadableProfileTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `3/3` 通过；受影响完整测试各 `2708/2708` 通过。
- [x] 从当前源码生成 `artifacts/r88-hri-pack` 的 Core/Templates 包；五个默认目标消费者和 `net5.0 + netstandard2.1` 显式资产均构建、运行通过，候选包与恢复包哈希一致。
- [x] Designer Profile 编辑器、项目规范化、Host 请求 DTO 和能力快照均传递 `supportsNativeBarcodeHumanReadable`；前端 `247/247`、生产构建和隔离输出的 `DesignerHost.Tests` `114/114` 通过。

### R88 未完成 TODO

- [ ] Native Rows/Sequential 其余条码制式、HRI、静区、旋转、动态尺寸及其组合仍未完成全矩阵验证。
- [ ] Profile 仍未细化到按条码制式、HRI 位置、文字样式和二维码方向的组合能力。
- [ ] Designer `labelProfiles.spec.ts` 浏览器端到端回归尚未在独立的本轮 Host 上执行；目前证据是规范化单测、生产构建和 Host 契约测试。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实打印扫描和切裁仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局和发送中断重试仍未形成证据。

### R88 本轮接受的限制

- [x] HRI Profile 是机型级开关，不推断单个条码制式或固件版本的细节；ZPL GS1 DataBar 无 HRI 参数的既有严格拒绝保持不变。
- [x] 本轮只证明本地能力契约和失败恢复，不证明目标设备的 HRI 字形、位置、字高或扫描结果。

## R87 本轮：运行时矩阵与当前源码包消费者

本轮先复用现有验证器，重新执行当前工作区的完整目标运行时矩阵，并从当前源码生成隔离的 Core/Templates `1.0.0` 包后验证外部包消费者。旧的 `artifacts/t8-fixed-font-pack` 仍是历史候选包：它与当前消费者 API 不同步，三次消费编译均因缺少 `SupportsImageRotation`、`RasterBarcodeHumanReadableFontSize`、`FillRectangle`、`CreateFilled` 等当前成员而失败；该失败不能当作当前源码能力失败。

### R87 已完成 TODO

- [x] 当前 Core/Templates 测试在 `net7.0`、`net6.0`、`net5.0`、`netcoreapp3.1`、`netcoreapp2.2` 五个目标运行时各 `2705/2705` 通过，实际运行时已在报告中核验。
- [x] 从当前源码生成 `artifacts/r87-current-pack` 的 Core/Templates 两个 `1.0.0` 包，包含现有五个生产 TFM；包消费者使用当前包而不是历史包。
- [x] 当前包消费者默认资产在上述五个目标框架均构建、运行和 API/模板/四协议模型回归通过；每个报告的候选包归档与恢复归档 SHA-256 一致。
- [x] `net5.0 + PackageAssetFramework=netstandard2.1` 显式资产消费者通过，Core/Templates 两个输出 DLL 均确认来自候选包的 `netstandard2.1` 资产；验证工程已隔离重复 CopyLocal 项并补齐传递运行时 DLL。
- [x] 运行时与包消费者结果保存在独立 R87 证据目录，未修改打印协议字节或设备边界契约。

### R87 未完成 TODO

- [ ] Native Rows/Sequential 其余条码制式、HRI、静区、旋转、动态尺寸及其组合仍未完成全矩阵验证。
- [ ] 主机字体墨迹、设备 DPI/字高/纸宽校准、真实打印扫描和切裁仍需外部硬件验收。
- [ ] 正式性能、容量、超时阈值、复杂文字塑形/双向布局和发送中断重试仍未形成证据。

### R87 本轮接受的限制

- [x] 五目标运行时与五目标默认 NuGet 消费证明的是当前源码和固定 DejaVuSans 测试字体的可执行契约，不等于所有设备固件或所有包资产选择均已验收。
- [x] `NU1900`（漏洞源不可达）和旧 TFM 的 `NETSDK1138`（已停止支持）作为环境警告保留，不把它们伪装成通过或漏洞清零。
- [x] 显式 `netstandard2.1` 资产需要验证工程的 CopyLocal 适配；该适配只影响测试消费者的资产选择，不改变生产包内容或协议 Provider。

## R86 本轮：ESC/POS Native Sequential 旋转边界

本轮补齐 Native Sequential 模板对旋转条码和旋转文字的严格能力边界。Version 2 模板在非法元素后放置图片资源，条码或文字使用 `Rotate90` 时按元素路径失败，并确认预检不会打开后续资源。

### R86 已完成 TODO

- [x] Native Sequential 旋转条码在模板输出前按元素路径严格拒绝。
- [x] Native Sequential 旋转文字在模板输出前按元素路径严格拒绝。
- [x] 两种失败均保持后续图片资源 `OpenRead` 次数为 0。
- [x] `TemplateEscPosNativeSequentialRotationBoundaryTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `2/2` 通过；保留既有 `NU1900` 漏洞源不可访问警告。

### R86 未完成 TODO

- [ ] Native Rows/Sequential 其余条码制式、HRI、静区、旋转、动态尺寸及其组合仍未完成全矩阵验证。
- [ ] 完整目标运行时矩阵、主机字体墨迹和真实设备字高校准仍待补齐。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍待后续或外部验收。

### R86 本轮接受的限制

- [x] 只验证 `Rotate90` 的 Native 严格拒绝和资源前置顺序，不推断 Raster 或标签协议的旋转输出。
- [x] Native 旋转继续保持 Fail Fast；需要旋转输出时应显式选择 Raster 或坐标型标签协议。

## R85 本轮：ESC/POS Native Sequential 参数组合

本轮将 HRI 和模块宽度参数从单页 Rows 扩展到 Native Sequential 跨页重复明细。固定页眉、表头、重复 Body 和页脚包含 Code39、I2OF5、UCC/EAN-128；两条明细在两页中保留来源路径、实例索引、HRI 预留高度、`GS H`、`GS w`、`GS k` 和 Payload。非零静区继续在资源读取前严格拒绝。

### R85 已完成 TODO

- [x] Sequential 跨页重复明细覆盖三种带 HRI 的原生条码。
- [x] 每页及每个重复实例保留 `SourcePath`、`InstanceIndices`、宽高和页内坐标。
- [x] 每页验证 `GS H=2`、`GS h=40`、`GS w=3`、`GS k=69/70/74` 和对应 Payload。
- [x] 非零静区在打开后续图片资源前按元素路径严格拒绝。
- [x] `TemplateEscPosNativeSequentialParameterTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `2/2` 通过；保留既有 `NU1900` 漏洞源不可访问警告。

### R85 未完成 TODO

- [ ] Native Rows/Sequential 其余条码制式、HRI、静区、旋转、动态尺寸及其组合仍未完成全矩阵验证。
- [ ] 完整目标运行时矩阵、主机字体墨迹和真实设备字高校准仍待补齐。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍待后续或外部验收。

### R85 本轮接受的限制

- [x] 夹具固定为两条明细、三种条码和固定页眉/表头/页脚，只验证 Sequential 参数与分页轨迹契约，不推断设备 HRI 字形或扫描结果。
- [x] Native 旋转继续严格拒绝；需要旋转输出时仍应选择 Raster 或标签协议。

## R84 本轮：ESC/POS Native Rows 参数组合

本轮补充 Native Rows 对 Code39、Code93、Codabar、I2OF5 和 UCC/EAN-128 的 HRI 组合验证。模板布局包含 HRI 预留高度，输出同时检查 `GS H`、`GS w`、`GS k` 和数据；显式模块宽度从 2 调到 3 时，布局宽度与原生字节同步变化，低于设备范围的模块宽度按元素路径严格拒绝。

### R84 已完成 TODO

- [x] 五种代表性 Native Rows 条码开启 HRI 时保留 `GS H=2` 和 HRI 预留高度。
- [x] Native 条码高度、模块宽度、`GS w`、对应 `GS k` 和 payload 在模板输出中保持一致。
- [x] 显式模块宽度从 2 到 3 时，布局宽度随条码尺寸变化并与输出参数一致。
- [x] `NarrowModuleWidth=1` 在输出前按 `Rows[0].Cells[0].Elements[0]` 严格拒绝。
- [x] `TemplateEscPosNativeParameterCombinationTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `7/7` 通过；保留既有 `NU1900` 漏洞源不可访问警告。

### R84 未完成 TODO

- [ ] Native Rows/Sequential 其余条码制式、HRI、静区、旋转、动态尺寸及其组合仍未完成全矩阵验证。
- [ ] 完整目标运行时矩阵、主机字体墨迹和真实设备字高校准仍待补齐。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍待后续或外部验收。

### R84 本轮接受的限制

- [x] 夹具固定为 Native Rows、ASCII 数据和五种条码，只验证布局与原生参数契约，不推断任意设备的 HRI 字形、条高或扫描效果。
- [x] 本轮只验证模块宽度 2/3 与设备下限 1，未宣称覆盖所有机型的完整模块宽度范围。

## R83 本轮：ESC/POS Native Rows 条码矩阵

本轮补齐 ESC/POS Native Rows 模板对 Code39、Code93、Codabar、I2OF5、I2OF5C 和 UCC/EAN-128 的代表性验证。每个条码均通过统一模板能力快照、布局轨迹和原生 `GS k` 输出检查；I2OF5C 验证模板入口的校验位归一化，非法 Codabar 数据按元素路径严格拒绝。

### R83 已完成 TODO

- [x] Native Rows 模板能力快照开放六种原生一维条码。
- [x] 每种条码保留 `SourcePath`、正宽高和页面边界内的布局位置。
- [x] 页面字节验证 `GS k` 69、70、71、72、74 及 I2OF5C 的归一化 payload。
- [x] 非法 Codabar 数据在协议输出前按模板元素路径严格抛错。
- [x] `TemplateEscPosNativeRowsBarcodeMatrixTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `3/3` 通过；保留既有 `NU1900` 漏洞源不可访问警告。

### R83 未完成 TODO

- [ ] Native Rows/Sequential 其余条码制式、HRI、静区、旋转、动态尺寸及其组合仍未完成全矩阵验证。
- [ ] 完整目标运行时矩阵、主机字体墨迹和真实设备字高校准仍待补齐。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍待后续或外部验收。

### R83 本轮接受的限制

- [x] 夹具固定为单页 Rows、ASCII 数据和六种条码，只证明模板到 Native Provider 的制式及校验位契约，不推断任意固件的条高、HRI 或扫描结果。
- [x] 本轮不把 I2OF5C 的本地校验位归一化扩展为新的公开 API；继续沿现有统一模板语义输出。

## R82 本轮：ESC/POS Native 扩展条码模板矩阵

本轮把 ESC/POS Native Sequential 模板验证扩展到 GS1 DataBar Omnidirectional、Truncated、Limited、Expanded 和 Code128 auto。固定页眉、表头、重复明细和页脚跨页输出，逐项保留 `SourcePath`、实例索引、预留宽度和页面坐标，并检查 Function B `GS k` 75–79 的长度、数据和 Expanded 括号/FNC1 转义；MSI 仍按元素路径严格拒绝。

### R82 已完成 TODO

- [x] Native Sequential 模板覆盖 GS1 DataBar 四制式与 Code128 auto 的能力快照。
- [x] 模板布局轨迹保留五种条码的来源路径、重复实例索引、正宽高和 `BarcodeMetrics` 预留宽度。
- [x] 页面字节验证 Function B `GS k` 75、76、77、78、79 的制式、长度和 payload；Expanded 验证括号与 ASCII 29 FNC1 转义。
- [x] Native 不支持的 MSI 在模板输出前按元素路径严格抛错。
- [x] `TemplateEscPosNativeExtendedBarcodeTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `3/3` 通过；保留既有 `NU1900` 漏洞源不可访问警告。

### R82 未完成 TODO

- [ ] Native Rows/Sequential 其余条码制式、HRI、静区、旋转、动态尺寸及其组合仍未完成全矩阵验证。
- [ ] 完整目标运行时矩阵、主机字体墨迹和真实设备字高校准仍待补齐。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍待后续或外部验收。

### R82 本轮接受的限制

- [x] 夹具固定为 Native Sequential、两条重复明细和五种 Function B 条码，只证明模板到 Provider 的数据与轨迹契约，不推断任意固件的条码高度、HRI 或扫描结果。
- [x] Code128 auto 模板入口使用统一 ASCII 字符串；需要任意 8-bit 原始数据时仍使用独立命令 API，未把该能力伪装成模板字符串能力。

## R81 本轮：ESC/POS Native 参数矩阵闭环

本轮补齐 Native Rows 模板级条码参数证据。Code128 与 EAN13 开启 HRI 时，模板布局包含显式预留高度，输出保留 `GS H` 和对应 `GS k`；显式零静区继续兼容旧原点语义，非零静区、旋转条码和旋转文字在资源读取前按元素路径严格拒绝。

### R81 已完成 TODO

- [x] Native Rows Code128/EAN13 模板保留 HRI 预留高度、`SourcePath` 和布局高度。
- [x] Native 输出验证 `GS H` 选择和 Code128/EAN13 对应的 `GS k` 制式。
- [x] 显式零静区仍能输出原生条码；非零静区按 Native 能力严格失败。
- [x] Native 旋转条码和旋转文字按元素路径严格失败，且后续图片资源未被打开。
- [x] `TemplateEscPosNativeParameterMatrixTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `6/6` 通过；保留既有 `NU1900` 漏洞源不可访问警告。

### R81 未完成 TODO

- [ ] Native Rows/Sequential 的其余条码制式、HRI/静区/旋转/动态尺寸全组合仍未完整覆盖。
- [ ] 完整目标运行时矩阵、主机字体墨迹和真实设备字高校准仍待补齐。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍待后续或外部验收。

### R81 本轮接受的限制

- [x] 参数夹具固定为 Native Rows、两种 HRI 条码和零/非零静区边界，只证明当前模板参数契约，不推断任意设备固件的 HRI 字形或扫描效果。
- [x] 旋转 Native 元素保持严格拒绝；需要旋转输出时应显式选择支持坐标的 Raster 或标签协议。

## R80 本轮：ESC/POS Native 跨页条码模板闭环

本轮把 ESC/POS Native Sequential 从独立 Provider 用例接入 Version 2 模板分页回归。固定页眉、表头、重复明细和页脚共同输出多页，Code128、Code39、EAN13 保留原生 `GS k` 制式、来源路径和重复实例索引；同时验证能力矩阵开放的 Native 条码与 MSI 不支持时的严格元素路径错误。

### R80 已完成 TODO

- [x] Native Sequential 模板覆盖固定页眉、表头、重复 Body 和页脚，并产生多页输出。
- [x] Code128、Code39、EAN13 在每个重复明细中保留 `SourcePath`、`InstanceIndices`、尺寸和页内坐标。
- [x] 页面字节检查 ESC/POS `GS k` 以及 73、69、67 原生制式标记。
- [x] Native 不支持的 MSI 在资源和输出前按元素路径严格抛出 `TemplateRenderException`。
- [x] `TemplateEscPosNativeCrossPageBarcodeTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `2/2` 通过；保留既有 `NU1900` 漏洞源不可访问警告。

### R80 未完成 TODO

- [x] Native Rows 的代表性 HRI、零/非零静区和旋转严格边界已在 R81 完成；全条码制式和参数组合仍待后续。
- [ ] 完整目标运行时矩阵、主机字体墨迹和真实设备字高校准仍待补齐。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍待后续或外部验收。

### R80 本轮接受的限制

- [x] 夹具固定为 280 点宽、5 条明细和三种 Native 条码，只证明模板到 Sequential Provider 的分页链路，不推断所有制式或设备固件行为。
- [x] 本轮未把 MSI 转为 Raster 降级；Native 不支持能力继续严格失败，调用方可显式选择 Raster 并按其能力矩阵处理。

## R79 本轮：跨页条码复杂布局验证

本轮补齐 Version 2 模板中非 Code128 条码与固定页眉、表头、重复明细、页脚共同分页时的协议链路证据。新增跨页条码夹具，在 CPCL、TSPL、ZPL 和 ESC/POS Raster 中验证 Code39、Code93、Codabar、I2OF5C 的能力预检、布局轨迹、来源路径、明细实例索引和协议字节标记。

### R79 已完成 TODO

- [x] Version 2 `PageHeader`、`TableHeader`、`Body RepeatSource`、`PageFooter` 与四种非 Code128 条码共同触发三页输出。
- [x] 每个条码按 `SourcePath` 保留三次明细实例，`InstanceIndices` 为 `0/1/2`，页内分布不再假设固定数量。
- [x] CPCL、TSPL、ZPL 验证对应原生条码命令；ESC/POS Raster 验证 `GS v 0`、正尺寸和非空光栅数据。
- [x] `TemplateCrossPageBarcodeTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `4/4` 通过；仅保留既有 `NU1900` 漏洞源不可访问警告。

### R79 未完成 TODO

- [x] ESC/POS Native Sequential 的代表性条码跨页组合和 MSI 严格拒绝已在 R80 完成；其余条码、Rows/Sequential 全矩阵和参数组合仍待后续。
- [ ] 完整目标运行时矩阵、主机字体墨迹和真实设备字高校准仍待补齐。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍待后续或外部验收。

### R79 本轮接受的限制

- [x] 固定四种条码和三条明细只证明模板到四协议 Provider 的跨页链路、轨迹及字节标记，不推断所有条码类型、任意数据规模或设备扫描成功率。
- [x] ESC/POS 本轮使用显式 Raster 模式；Native 的能力边界仍由既有能力矩阵和后续组合测试负责。

## R78 本轮：图片长页裁切与复杂分区压力闭环

本轮补齐图片裁切与复杂页眉、表头、重复明细、页脚同时存在时的长页资源压力证据。新增 Version 2 模板夹具，在四个固定分区中使用图片，并在重复明细中使用两个不同源裁切区域；有限 `MaxWorkingBytes` 下验证多页分页、静态资源复用、正文资源逐项回收、裁切缓存隔离和预算失败后的新会话恢复。

### R78 已完成 TODO

- [x] Version 2 `PageHeader`、`TableHeader`、`Body RepeatSource`、`PageFooter` 图片组合触发多页输出，布局轨迹保持可用。
- [x] 正文两个不同裁切区域按明细重新打开，静态分区图片各只打开一次，所有流最终释放。
- [x] 有限工作集预算触发受控拒绝，失败后使用新渲染会话和预算可以恢复完整输出。
- [x] `TemplateImageLongPagePressureTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `3/3` 通过；受影响模板完整套件各 `2676/2676` 通过；保留已有 `NU1900` 漏洞源不可访问警告。

### R78 未完成 TODO

- [x] 代表性条码在跨页和复杂布局中的组合验证已在 R79 完成；完整条码类型覆盖仍待后续。
- [ ] 完整目标运行时矩阵、HRI/静区/旋转组合、主机字体墨迹和真实设备字高校准。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍待后续或外部验收。

### R78 本轮接受的限制

- [x] 长页夹具固定为 7 条明细、128×64 源图和两个 64×64 裁切区域，只证明当前资源生命周期与缓存隔离契约，不推断任意规模的容量上限。
- [x] 预算验证使用引擎声明的 `MaxWorkingBytes`，不能替代进程峰值内存、垃圾回收或真实设备吞吐测量。

## R77 本轮：图片结构化像素诊断闭环

本轮将损坏、未加载和裁切错误的图片诊断从错误文案提升为可消费的结构化元数据。模板异常现在可报告源图像素尺寸、请求裁切区域和处理后的输出尺寸；Designer Host 将这些字段保留在错误 JSON 中，前端规范化并按条件显示。未知尺寸保持 `null`，越界裁切只报告服务端已经识别的源图和请求区域，不猜测解码结果。

### R77 已完成 TODO

- [x] `TemplateRenderException` 增加图片源尺寸、输出尺寸和裁切区域的只读诊断属性，并沿内部异常链合并元数据。
- [x] 图片识别、源区域越界、目标像素预算、缓存预算和解码失败路径附加对应像素元数据；缺失/损坏资源不伪造尺寸。
- [x] Designer Host 错误 JSON 输出八个可空像素/裁切字段；旧调用仍可忽略新增字段。
- [x] Designer 规范化和格式化图片像素诊断，覆盖源图、裁切和输出尺寸，并验证非法值不会变成零。
- [x] 模板四协议图片错误、Host 预览/渲染恢复、缺失/损坏分类回归：`TemplateImageDiagnosticTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `10/10`，Host 精准回归 `7/7`，Designer 诊断单测 `14/14`。

### R77 未完成 TODO

- [x] 图片裁切与复杂页眉/表头/页脚组合的长页压力，以及多种资源键、裁切键并存的生命周期与隔离回归已在 R78 完成；任意规模容量曲线仍待正式门禁。
- [ ] 其他条码在跨页和复杂布局中的组合验证；当前条码夹具未覆盖所有条码类型。
- [ ] 完整目标运行时矩阵、HRI/静区/旋转组合、主机字体墨迹和真实设备字高校准。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍待后续或外部验收。

### R77 本轮接受的限制

- [x] 损坏或未加载资源无法可靠识别源图尺寸，诊断字段保持 `null`；这不是把未知尺寸降级为默认尺寸。
- [x] 像素字段描述解码/布局阶段的逻辑尺寸，不替代打印机 DPI、纸宽、插值质量或真实物理输出验收。

## R76 本轮：多页图片资源生命周期与取消恢复闭环

本轮补齐模板图片在多页分页、资源重复读取和取消失败恢复上的本地证据。测试覆盖页眉图片跨页复用、正文大图按块回收、单图工作集预算下的多页输出，以及资源打开阶段取消后的流释放和下一次渲染恢复。生产实现已经具备页眉缓存、正文块完成后移除非页眉图片、编码资源租约清理和会话 `Dispose` 回收；本轮通过精准回归确认这些路径没有泄漏或跨渲染污染。

### R76 已完成 TODO

- [x] 页眉图片跨多页只打开一次，渲染结束后所有资源流均释放。
- [x] 正文重复大图在约单图工作集预算下逐页回收，四页输出不会累积解码图片。
- [x] 图片资源打开阶段触发取消时，资源流和会话租约释放；使用新取消令牌的后续渲染可以成功恢复。
- [x] `TemplateImageWorkingBudgetTests` 在 `net6.0` 与 `netcoreapp3.1` 各 `8/8` 通过；保留已有 `NU1900` 漏洞源不可访问警告，不把它当作测试失败。

### R76 未完成 TODO

- [x] 设计器对损坏资源、资源未加载和服务端裁切错误的结构化像素尺寸诊断已在 R77 完成；未知尺寸保持空值，非法区域仍由服务端严格拒绝。
- [x] 图片裁切与复杂页眉/表头/页脚组合的长页压力，以及多种不同资源键、不同裁切键并存的生命周期与隔离回归已在 R78 完成；任意规模容量曲线仍待正式门禁。
- [ ] 仍需其他条码在跨页和复杂布局中的组合验证；当前条码夹具未覆盖所有条码类型。
- [ ] 仍需完整运行时矩阵、HRI/静区/旋转组合、主机字体墨迹和真实设备字高校准。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍待后续或外部验收。

### R76 本轮接受的限制

- [x] 正文图片在每个展开块完成后释放；同一资源若不属于页眉/表头，会按工作集策略在后续块重新打开，测试验证其预算和释放行为，不把重复打开次数优化成跨所有正文块的全局缓存契约。
- [x] 取消测试在资源打开回调中触发令牌，验证引擎边界的清理和恢复；不宣称 ImageSharp 内部解码可被任意时点强制中断。

## R75 本轮：模板图片源区域裁切闭环

本轮把图片源区域裁切加入统一模板模型。`BpImageElement.Crop` 使用源图片像素坐标与尺寸，渲染顺序固定为“读取源图、边界校验、裁切、按 Width/Height 缩放、黑白化、协议编码”；裁切区域纳入会话图片缓存键，避免同一资源的不同区域串图。旧模板省略 `Crop` 时保持原有完整源图和字节行为。

### R75 已完成 TODO

- [x] 新增 `BpImageCrop` 与 `BpImageElement.Crop`，支持 JSON 保存/加载；坐标非负、宽高为正，几何错误在打开资源前按元素路径拒绝。
- [x] 实现源图边界校验，先裁切再按目标尺寸缩放；目标高度为零时按裁切区域宽高比计算。
- [x] 扩展图片解码缓存键覆盖裁切区域，验证同一资源不同裁切不会复用错误图像。
- [x] 四协议覆盖裁切后的尺寸、0/90/270 度旋转、协议图片命令标记和页面高度；`TemplateImageRotationTests` 在 `net6.0` 与 `netcoreapp3.1` 各 38/38 通过。
- [x] 设计器支持完整源图/显式区域切换、源像素编辑、模板校验、模板包往返和画布按真实资源尺寸显示裁切区域；Designer 单测 245/245、类型检查和生产构建通过。
- [x] `Bing.EasyPrint.Templates` 在 `netstandard2.0` 与 `net7.0` 编译通过。

### R75 未完成 TODO

- [x] 设计器对损坏资源、资源未加载和服务端裁切错误的结构化像素尺寸诊断已在 R77 完成；未知尺寸保持空值，非法区域仍由服务端严格拒绝。
- [x] 多页大图、资源重复打开的工作集/生命周期压力，以及取消和租约释放专项测试已在 R76 完成；裁切与复杂分区长页组合已在 R78 补充，任意规模容量曲线仍待正式门禁。
- [ ] 仍需其他条码在跨页和复杂布局中的组合验证；当前条码夹具未覆盖所有条码类型。
- [ ] 仍需完整运行时矩阵、HRI/静区/旋转组合、主机字体墨迹和真实设备字高校准。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍待后续或外部验收。

### R75 本轮接受的限制

- [x] 裁切坐标以源图片像素为单位，不提供协议设备坐标或任意插值算法；源区域越界严格失败，不静默截断。
- [x] ESC/POS Raster 仍将结果合成为整页 `GS v 0`；单图裁切结果由布局轨迹和整页像素差异验证，不从整页头部推断单图边界。

## R74 本轮：模板图片显式缩放边界闭环

本轮补齐 R4/R5 中“模板图片缩放边界”的本地协议回归。测试使用固定的非对称 4×3 PNG，分别将显式目标尺寸设为 2×2、4×3、6×5，并覆盖不旋转、90 度和 270 度旋转；同一布局依次渲染 CPCL、TSPL、ZPL 和 ESC/POS Raster，检查缩放后的布局轨迹、旋转后的宽高交换、页面高度以及协议图片命令标记。

### R74 已完成 TODO

- [x] 显式图片宽高在协议编码前生效，覆盖小于、等于和大于源图的目标尺寸。
- [x] 0/90/270 度旋转下的布局轨迹、宽高交换和自动页面高度均有断言。
- [x] CPCL `EG`、TSPL `BITMAP`、ZPL `^GFA` 和 ESC/POS Raster `GS v 0` 均有协议输出标记验证；`TemplateImageRotationTests` 在 `net6.0` 与 `netcoreapp3.1` 各 25/25 通过。

### R74 未完成 TODO

- [ ] 模型仍没有任意源区域/裁切字段；需要先确定裁切语义、越界策略和 Provider 能力契约。
- [ ] 仍需多页大图、资源重复打开和资源生命周期压力验证，以及图片与复杂页眉/表头/页脚混排的长页场景。
- [ ] 仍需其他条码在跨页和复杂布局中的组合验证；当前条码夹具未覆盖所有条码类型。
- [ ] 仍需完整运行时矩阵、HRI/静区/旋转组合、主机字体墨迹和真实设备字高校准。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍待后续或外部验收。

### R74 本轮接受的限制

- [x] 测试固定使用 4×3 源图和 2×2/4×3/6×5 目标尺寸，验证布局与协议字节，不推断任意裁切、插值质量或真实设备物理打印效果。
- [x] ESC/POS Raster 使用整页 `GS v 0` 合成；单图目标尺寸由布局轨迹验证，不能从整页光栅头部推断单图像素边界。

## R73 本轮：Designer 四协议跨页点阵码闭环

本轮关闭 R4/R5 中“DataMatrix/PDF417 与跨页页眉、表头、页脚及重复明细组合”的本地验收缺口。新增 Version 2 模板，分别使用 DataMatrix 和 PDF417，三条重复明细触发多页；依次请求 CPCL、TSPL、ZPL 和 ESC/POS Raster，检查固定区、点阵码布局轨迹、实例顺序和 ZIP 页面归档。

### R73 已完成 TODO

- [x] 新增 DataMatrix/PDF417 跨页 Designer E2E，覆盖 `PageHeader`、`TableHeader`、重复明细、`PageFooter` 和多页分页。
- [x] 两种 Type 10 点阵码在四协议预览中均保留 3 个实例，`sourcePath`、元素类型和 `instanceIndices` 顺序一致。
- [x] 四协议 ZIP 页面数量与预览页数一致，并通过 CPCL `EG`、TSPL `BITMAP`、ZPL `^GFA` 和 Raster `GS v 0` 输出标记；目标用例 `1/1` 通过，Designer 构建通过。

### R73 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需图片/点阵码裁切、缩放边界、多页资源生命周期压力，以及资源与复杂页眉/表头/页脚混排的更多场景。
- [ ] R4/R5 仍需其他条码在跨页和复杂布局中的组合验证；当前条码夹具覆盖 Code 128、二维码、DataMatrix 和 PDF417，仍未覆盖所有条码类型。
- [ ] R5 仍需真实设备对四协议点阵码缩放、DPI、纸宽、走纸、打印和扫描解码结果复核，以及 TSPL DPI/DPL 和其他厂商扩展。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍需后续或外部验收。

### R73 本轮接受的限制

- [x] 点阵码使用固定格式、内容、尺寸和三条明细，只验证跨页布局轨迹与协议归档，不推断任意裁切、缩放质量或真实设备解码效果。
- [x] Type 10 继续使用统一黑白位图输出；本轮不宣称 CPCL、TSPL、ZPL 或 ESC/POS Native 点阵码能力。

## R72 本轮：Designer 四协议跨页条码混排闭环

本轮关闭 R4/R5 中“跨页页眉/表头/页脚与条码组合”的本地验收缺口。新增 Version 2 模板，包含固定页眉、可重复表头、9 条重复明细和固定页脚；每条明细同时包含 Code 128 与二维码，触发多页后分别请求 CPCL、TSPL、ZPL 和 ESC/POS Raster，检查固定区、条码布局轨迹、实例顺序以及 ZIP 页面归档。

### R72 已完成 TODO

- [x] 新增跨页条码混排 Designer E2E，覆盖 `PageHeader`、`TableHeader`、重复明细、`PageFooter` 和多页分页。
- [x] 四协议预览均保留 9 个 Code 128 与 9 个二维码实例，`sourcePath`、元素类型和 `instanceIndices` 顺序一致。
- [x] 四协议 ZIP 页面数量与预览页数一致，并通过 CPCL `B 128`/`B QR`、TSPL `BARCODE`/`QRCODE`、ZPL `^BC`/`^BQ` 和 Raster `GS v 0` 输出标记；目标用例 `1/1` 通过。

### R72 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需图片/点阵码裁切、缩放边界、多页资源生命周期压力，以及资源与复杂页眉/表头/页脚混排的更多场景。
- [ ] R4/R5 仍需其他条码在跨页和复杂布局中的组合验证；当前条码夹具只覆盖 Code 128 与二维码。
- [ ] R5 仍需真实设备对四协议条码/二维码布局、DPI、纸宽、走纸、打印和扫描结果复核，以及 TSPL DPI/DPL 和其他厂商扩展。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍需后续或外部验收。

### R72 本轮接受的限制

- [x] 条码夹具使用固定 Code 128 数据、固定二维码模块尺寸和固定页高，只验证跨页布局轨迹与协议归档，不推断其他条码制式、动态尺寸或真实设备解码效果。
- [x] ESC/POS 使用 Raster 模式验证统一光栅路径；本轮不扩大 Native 条码能力或硬件打印质量结论。

## R71 本轮：Designer 四协议跨页图片资源闭环

本轮关闭 R4/R5 中“跨页重复图片资源和旋转占位”的本地验收缺口。新增模板包夹具，只携带一个非对称 PNG 资源，在 Version 2 重复明细中使用 90 度旋转图片并触发多页；分别请求 CPCL、TSPL、ZPL 和 ESC/POS Raster，检查每页固定区、图片布局轨迹、明细顺序和页面归档。CPCL 夹具显式使用压缩图片，因此断言其 `CG` 命令；其他协议分别验证 `BITMAP`、`^GFA` 和 `GS v 0`。

### R71 已完成 TODO

- [x] 新增模板包资源跨页 Designer E2E，覆盖单资源重复使用、90 度旋转占位、分页和资源导入状态。
- [x] 四协议预览均保留每页页眉/表头/页脚、11 条明细及 11 个 `6×12`/90 度图片占位；导出 ZIP 页面与各协议预览页数连续对应。
- [x] CPCL 压缩图片、TSPL/ZPL 位图和 ESC/POS Raster 输出标记均通过；目标用例 `1/1`、图片旋转回归 `5/5` 通过；Designer 构建通过。

### R71 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需图片/点阵码裁切、缩放边界、多页资源生命周期压力，以及资源与复杂页眉/表头/页脚混排的更多场景。
- [ ] R4/R5 仍需其他条码在跨页和复杂布局中的组合验证；当前跨页图片夹具未覆盖所有条码类型。
- [ ] R5 仍需真实设备对四协议图片旋转、压缩/黑白化、DPI、纸宽、走纸、打印和扫描结果复核，以及 TSPL DPI/DPL 和其他厂商扩展。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍需后续或外部验收。

### R71 本轮接受的限制

- [x] 图片使用固定 2×1 非对称 PNG 和固定 90 度旋转，只验证资源复用、布局占位和协议归档，不推断任意裁切、缩放质量或真实打印机的物理图像效果。
- [x] TSPL/ZPL 粗体、下划线和旋转下划线继续采用显式 `AllowRasterizedTextStyles` 主机位图降级；默认严格拒绝，协议本身没有对应原生样式命令，本轮不伪造 Native 支持。

## R70 本轮：Designer 四协议跨页分区闭环

本轮关闭 R4/R5 中“跨页页眉、表格表头、页脚与重复明细同时输出”的本地验收缺口。新增 Version 2 模板，使用固定页眉文字与水平线、可重复表头、11 条明细和固定页脚文字与矩形，触发多页后分别请求 CPCL、TSPL、ZPL 和 ESC/POS Raster。每个协议都检查每页的页眉/表头/页脚和线框轨迹、明细顺序完整性，并确认 ZIP 页面编号与该协议自身预览页数一致。

### R70 已完成 TODO

- [x] 新增跨页分区 Designer E2E，覆盖 `PageHeader`、`TableHeader`、重复明细、`PageFooter` 和多页分页。
- [x] CPCL、TSPL、ZPL、ESC/POS Raster 每页均保留固定页眉、表头、页脚以及 Type 5/7 线框元素；11 条明细按模板顺序全部出现。
- [x] 四协议 ZIP 均按各自预览页数生成连续页面并通过文本或 `GS v 0` 字节标记校验；目标用例 `1/1`、相关跨协议用例 `8/8`、动态表格回归 `1/1` 通过；Designer 构建通过。

### R70 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需跨页图片/点阵码旋转、裁切、多页资源生命周期，以及这些资源与页眉/表头/页脚混排的更多 E2E。
- [ ] R4/R5 仍需其他条码在跨页和复杂布局中的组合验证；当前跨页夹具只使用文字、线和矩形作为混合元素。
- [ ] R5 仍需真实设备对四协议分页、走纸、切裁、DPI、纸宽、打印和扫描结果复核，以及 TSPL DPI/DPL 和其他厂商扩展。
- [ ] R6 的 TSPL/ZPL 原生内置字体粗体/下划线仍未声明支持；当前只提供显式主机位图降级。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍需后续或外部验收。

### R70 本轮接受的限制

- [x] 夹具使用固定 ASCII 文本、固定行高和简单线框，只验证模板分页轨迹和协议归档映射，不推断中文塑形、动态行高、真实设备走纸或物理页高。
- [x] 四协议页数按各 Provider 自身度量分别验证，不强制不同协议使用相同物理页数；Raster 只验证每页存在光栅命令，不伪造打印机图像质量证据。

## R69 本轮：Designer 四协议混合布局闭环

本轮关闭 R4/R5 中“单页复杂混合布局跨协议输出”的本地验收缺口。新增 Version 2 模板，组合文字、水平线、空心矩形、Code 128 条码和二维码；四种协议均通过 Host 预览保留五种元素的布局轨迹并成功导出。CPCL/TSPL 将空心矩形展开为多条线，ZPL 使用 `^GB`，ESC/POS Raster 使用统一光栅路径。

### R69 已完成 TODO

- [x] 新增混合布局 Designer E2E，覆盖文字、线框、矩形、条码、二维码同页组合和四协议切换。
- [x] CPCL、TSPL、ZPL、ESC/POS Raster 预览均保留 Type 1/3/4/5/7 元素；四个 ZIP 页面均包含对应协议的文字、线框和码图输出标记。
- [x] 目标用例 `1/1` 通过；与二维码、图片、点阵码跨协议用例联合回归 `4/4` 通过；Designer 构建通过。

### R69 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需跨页表头/页脚与混合布局组合、图片/点阵码旋转、裁切、多页资源的更多 Designer E2E。
- [ ] R4/R5 仍需其他条码在多页和复杂布局中的组合验证；现有条码覆盖仍以单页为主。
- [ ] R5 仍需真实设备对混合布局的走纸、裁切、DPI、纸宽、打印和扫描结果复核，以及 TSPL DPI/DPL 和其他厂商扩展。
- [ ] R6 的 TSPL/ZPL 原生内置字体粗体/下划线仍未声明支持；当前只提供显式主机位图降级。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍需后续或外部验收。

### R69 本轮接受的限制

- [x] 夹具是固定单页 Version 2 布局；不推断跨页混合元素的分页、表头/页脚、资源生命周期或真实设备物理位置。
- [x] CPCL/TSPL 空心矩形按线命令展开是当前 Provider 的协议编码策略；E2E 断言布局元素类型和实际命令，不要求协议间命令名称相同。

## R68 本轮：Designer 四协议长文本换行闭环

本轮关闭 R4/R5 中“长文本换行内容在多协议输出中保持完整”的本地验收缺口。新增 Version 2 长文本夹具，使用 240 个 ASCII 字符和 `NeedFeedLine`，分别在 CPCL、TSPL、ZPL 和 ESC/POS Raster 下请求 Host 预览，验证所有文本片段按协议自身字体度量分割后仍能拼回原文，并确认导出 ZIP 的页序与该协议的预览页数一致。

### R68 已完成 TODO

- [x] 新增长文本跨协议 Designer E2E，覆盖文本片段分割、多页预览、协议切换和四种导出按钮。
- [x] CPCL、TSPL、ZPL、ESC/POS Raster 均保留完整 240 字符内容；各协议按自身字体度量分页，导出页名连续且与自身预览页数一致。
- [x] 诊断得到默认夹具页数为 CPCL `6`、TSPL `8`、ZPL `8`、ESC/POS Raster `7`；差异来自协议字体宽度/行高，内容完整性不受影响。目标用例 `1/1` 通过；新旧长文本、跨协议分页和二维码回归 `4/4` 通过；Designer 构建通过。

### R68 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需跨页表头/页脚策略、复杂布局组合，以及图片/点阵码旋转、裁切、多页资源的更多 Designer E2E。
- [ ] R4/R5 仍需其他条码在多页和复杂布局中的组合验证；现有单页条码协议链路不覆盖所有分页组合。
- [ ] R5 仍需真实设备对四协议长文本换行、走纸、裁切、DPI、纸宽、打印和扫描结果复核，以及 TSPL DPI/DPL 和其他厂商扩展。
- [ ] R6 的 TSPL/ZPL 原生内置字体粗体/下划线仍未声明支持；当前只提供显式主机位图降级。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍需后续或外部验收。

### R68 本轮接受的限制

- [x] “一致”定义为每个协议自身的换行轨迹、完整文本和 ZIP 页序一致，不强制不同协议使用相同物理页数；若需要统一页数，应通过目标设备字体校准配置调整。
- [x] E2E 使用固定 ASCII 文本和默认字体配置，不推断中文、复杂文字塑形、双向文字、实际字高校准或目标打印机的物理换行效果。

## R67 本轮：Designer 四协议分页输出闭环

本轮关闭 R4/R5 中“同一重复明细模板跨协议分页导出”的本地验收缺口。新增一个只使用跨协议支持文字元素的 Version 3 模板，固定 13 条重复数据触发多页，依次选择 CPCL、TSPL、ZPL 和 ESC/POS Raster，通过 Host 预览取得页数，再从 Designer 下载 ZIP，校验连续 `page-001.*` 文件、页数与预览一致，以及各协议页面标记。

### R67 已完成 TODO

- [x] 新增跨协议分页 Designer E2E，覆盖重复行、服务端分页预览、协议切换和四种导出按钮。
- [x] 同一模板在 CPCL、TSPL、ZPL、ESC/POS Raster 下保持相同页数；ZIP 页面编号连续且没有缺页、重复页或错误扩展名。
- [x] 每个协议的所有页面均通过最小字节标记校验（CPCL `! 0`、TSPL `SIZE`、ZPL `^XA`、ESC/POS Raster `GS v 0`）。目标用例 `1/1` 通过；收据分页、二维码、图片和点阵码相关回归 `5/5` 通过；Designer 构建通过。

### R67 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需换行边界、跨页表头/页脚策略、复杂布局组合，以及图片/点阵码旋转、裁切、多页资源的更多 Designer E2E。
- [ ] R4/R5 仍需其他条码在多页和复杂布局中的组合验证；现有单页条码协议链路不覆盖所有分页组合。
- [ ] R5 仍需真实设备对四协议分页走纸、裁切、DPI、纸宽、打印和扫描结果复核，以及 TSPL DPI/DPL 和其他厂商扩展。
- [ ] R6 的 TSPL/ZPL 原生内置字体粗体/下划线仍未声明支持；当前只提供显式主机位图降级。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍需后续或外部验收。

### R67 本轮接受的限制

- [x] E2E 使用固定 13 条 ASCII 重复文字验证分页和 ZIP 映射，不推断不同数据量、长文本、混合图形或真实设备的分页物理效果。
- [x] 页面断言验证预览页数、文件编号和协议标记，不伪造打印机走纸、切刀、纸宽、DPI 或硬件打印质量证据。

## R66 本轮：Designer 四协议点阵码输出闭环

本轮关闭 R4/R5 中 `Type: 10` DataMatrix/PDF417 的 Designer 验收缺口。测试使用 Version 2 模板（Version 1 按既有契约禁止 Type 6–10），分别导入 DataMatrix 和 PDF417，依次选择 CPCL、TSPL、ZPL 和 ESC/POS Raster，等待 Host 预览并下载四个 ZIP 页面。两种点阵码均复用统一模板渲染器的受控黑白位图路径，导出断言分别校验 CPCL `EG`、TSPL `BITMAP`、ZPL `^GFA` 和 ESC/POS `GS v 0` 图片命令。

### R66 已完成 TODO

- [x] 新增 DataMatrix/PDF417 的 Designer 跨协议 E2E，覆盖 Type 10 模板导入、格式选择、服务端预览恢复和四协议切换。
- [x] 两种点阵码均通过 Designer `生成 CPCL`、`生成 TSPL`、`生成 ZPL` 和 `生成 ESC/POS` 导出，四个页面文件存在并保留对应图片命令标记。
- [x] 目标用例 `1/1` 通过；与二维码、图片跨协议用例联合回归 `3/3` 通过。首次诊断发现 Version 1 夹具违反模型契约，改为 Version 2 后正式确认通过。

### R66 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需其他条码、换行分页、复杂布局组合和图片/点阵码旋转、裁切、多页资源的更多 Designer E2E。
- [ ] R5 仍需真实设备对 DataMatrix/PDF417 位图缩放、DPI、纸宽、打印和扫描结果复核，以及 TSPL DPI/DPL 和其他厂商扩展。
- [ ] R6 的 TSPL/ZPL 原生内置字体粗体/下划线仍未声明支持；当前只提供显式主机位图降级。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍需后续或外部验收。

### R66 本轮接受的限制

- [x] Type 10 当前按统一位图模型输出，不伪造 TSPL/ZPL/ESC/POS Native 的原生 DataMatrix/PDF417 指令；Native 不支持图形时继续严格拒绝。
- [x] E2E 校验协议图片命令存在，不推断目标设备对点阵码物理尺寸、DPI、扫描质量或固件解码能力的实际结果。

## R65 本轮：Designer 四协议图片输出闭环

本轮关闭 R4/R5 中“真实图片资源经模板包导入后输出多协议字节”的 Designer 验收缺口。测试在浏览器内生成非对称 2×1 PNG，写入标准 `Bing.EasyPrint.TemplateBundle` 的 `resources/` 条目和 `manifest.json`，通过模板导入控件恢复资源，再依次选择 CPCL、TSPL、ZPL 和 ESC/POS Raster，等待 Host 预览并下载四个 ZIP 页面。导出断言分别校验 CPCL `EG`、TSPL `BITMAP`、ZPL `^GFA` 和 ESC/POS `GS v 0` 图片命令。

### R65 已完成 TODO

- [x] 新增图片模板跨协议 Designer E2E，覆盖模板包资源清单、PNG 解码、资源状态、服务端预览恢复和协议切换。
- [x] 同一图片资源通过 Designer `生成 CPCL`、`生成 TSPL`、`生成 ZPL` 和 `生成 ESC/POS` 导出，四个页面文件均存在并保留对应图片命令标记。
- [x] 目标用例 `1/1` 通过；与二维码跨协议、ESC/POS Raster 阈值请求和图片快捷导入/模板包往返回归联合 `5/5` 通过。

### R65 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需其他条码、换行分页、复杂布局组合和图片旋转/裁切/多页资源的更多 Designer E2E。
- [ ] R5 仍需真实设备对四协议图片黑白化阈值、DPI、纸宽、缩放、打印和扫描结果复核，以及 TSPL DPI/DPL 和其他厂商扩展。
- [ ] R6 的 TSPL/ZPL 原生内置字体粗体/下划线仍未声明支持；当前只提供显式主机位图降级。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍需后续或外部验收。

### R65 本轮接受的限制

- [x] E2E 使用固定的 2×1 非对称 PNG 验证资源导入和命令链路，不推断目标设备对大图、旋转、缩放、DPI 或纸宽的物理效果。
- [x] ESC/POS Raster 只断言 `GS v 0` 光栅命令存在；图片像素阈值、走纸和硬件打印质量仍归入 `BLOCKED_EXTERNAL`。

## R64 本轮：Designer 四协议二维码输出闭环

本轮关闭 R4/R5 中“同一模板输出 CPCL、TSPL、ZPL、ESC/POS Raster”的二维码 Designer 验收缺口。测试导入一个包含二维码的模板，依次切换四种输出协议，等待 Host 预览恢复，生成四个 ZIP 页面，并分别校验 CPCL `B QR`、TSPL `QRCODE`、ZPL `^BQ` 和 ESC/POS Raster `GS v 0` 标记及二维码数据。Native ESC/POS 不在本轮混入，因为其布局和二维码命令链路已有独立验收。

### R64 已完成 TODO

- [x] 新增跨协议二维码 Designer E2E，覆盖协议切换、服务端预览、四种导出按钮和四种页面文件。
- [x] 同一模板的二维码数据在 CPCL、TSPL、ZPL 原生命令和 ESC/POS Raster 光栅输出中均被校验；Raster 断言最终 `GS v 0` 字节。
- [x] 目标用例 `1/1` 通过；与既有协议导出、Native 二维码和二维码旋转回归联合 `5/5` 通过。

### R64 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需图片和其他条码的剩余 Designer E2E，以及换行分页和复杂布局组合。
- [ ] R5 仍需真实设备对 CPCL/TSPL/ZPL/ESC/POS 二维码固件、DPI/纸宽、纠错级别和扫描结果复核，以及 TSPL DPI/DPL 和其他厂商扩展。
- [ ] R6 的 TSPL/ZPL 原生内置字体粗体/下划线仍未声明支持；当前只提供显式主机位图降级。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍需后续或外部验收。

### R64 本轮接受的限制

- [x] E2E 使用固定二维码数据和明确尺寸，验证模板到协议字节的转换链路，不推断各打印机对二维码纠错、DPI、纸宽或扫描质量的实际结果。
- [x] ESC/POS Raster 只断言 `GS v 0` 光栅命令存在；位图像素的硬件打印效果仍归入 `BLOCKED_EXTERNAL`。

## R63 本轮：TSPL TELEPEN/TELEPENN/CODE49 Designer E2E 闭环

本轮关闭 R4/R5 中 TSPL 变量比例条码的 Designer 验收缺口。Designer 通过 Host 能力快照显示 TELEPEN、TELEPENN、CODE49，测试确认窄条/宽条模块字段保持 `2/4`，逐项切换三种制式和数据，并从生成 ZIP 中校验最终 `BARCODE` 行。TSPL 模板默认静区会将条码编码区放在 `20,0`，测试保留该布局轨迹而不把它误判为 Provider 偏移。

### R63 已完成 TODO

- [x] 新增 TSPL Designer E2E，确认条码选项 22–24 均可用，变量比例编辑器保留显式窄条 2、宽条 4。
- [x] TELEPEN、TELEPENN、CODE49 均通过 Designer `生成 TSPL` 流程导出，ZIP 中的 `page-001.tspl` 包含正确原生类型、条高、旋转、模块比例和完整数据。
- [x] 目标用例 `1/1` 通过；与既有 TSPL PLESSEY/ITF-14/EAN-14、ZPL DataBar 用例联合回归 `3/3`。

### R63 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需二维码、图片和其他条码的剩余 Designer E2E，以及换行分页和复杂布局组合。
- [ ] R5 仍需真实设备对 TSPL TELEPEN/CODE49 固件、DPI/纸宽、HRI 和扫描结果复核，以及 TSPL DPI/DPL 和其他厂商扩展。
- [ ] R6 的 TSPL/ZPL 原生内置字体粗体/下划线仍未声明支持；当前只提供显式主机位图降级。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍需后续或外部验收。

### R63 本轮接受的限制

- [x] E2E 使用 ASCII 数据和显式模块比例，验证的是模板到 TSPL 原生 `BARCODE` 字节的协议链路，不推断目标 TSC 固件的堆叠行数、物理尺寸或扫描质量。
- [x] 默认静区造成的 `20,0` 编码区原点属于当前统一模板布局语义；本轮不修改静区默认值或 Provider 坐标策略。

## R62 本轮：ESC/POS Native GS1 DataBar Designer E2E 闭环

本轮关闭 R4/R5 中 ESC/POS Native GS1 DataBar 的浏览器验收缺口。Designer 通过 Host 能力快照显示四种 Function B 制式，测试在真实页面中切换 Omnidirectional、Truncated、Limited、Expanded，等待服务端预览恢复，并从生成 ZIP 中校验 `page-001.escpos` 的 Function B 75–78 命令头和数据字节。测试夹具显式声明足够的宽度和窄条模块，避免把 Provider 的保守页面预检误报为 UI 故障。

### R62 已完成 TODO

- [x] 新增 ESC/POS Native Designer E2E，确认能力选项 25–28 均可用，制式切换、内容编辑和引擎预览保持有效。
- [x] 四种 DataBar 类型均通过 Designer `生成 ESC/POS` 流程导出，ZIP 中的 `page-001.escpos` 包含正确 Function B 75、76、77、78 命令头和数据长度/内容。
- [x] 目标用例 `1/1` 通过；与既有 ESC/POS Native 文字契约、TSPL 特殊条码和 ZPL DataBar 用例联合回归 `4/4`；DesignerHost 使用离线安全还原参数构建成功。

### R62 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需二维码、其他条码、图片的剩余 Designer E2E，以及换行分页和复杂布局组合。
- [ ] R5 仍需真实设备对 ESC/POS Function B 固件、HRI、DPI/纸宽和扫描结果复核，以及 TSPL DPI/DPL 和其他厂商扩展。
- [ ] R6 的 TSPL/ZPL 原生内置字体粗体/下划线仍未声明支持；当前只提供显式主机位图降级。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍需后续或外部验收。

### R62 本轮接受的限制

- [x] E2E 使用显式条码宽度和 `NarrowModuleWidth=2`，验证的是模板到 ESC/POS Function B 字节的协议链路，不推断设备的物理尺寸或扫描质量。
- [x] Expanded 使用 `12{` 验证裸大括号数据保留；GS1 AI/FNC1 业务语义、固件差异和真实打印结果仍按 `BLOCKED_EXTERNAL` 处理。

## R61 本轮：ZPL GS1 DataBar Designer E2E 闭环

本轮关闭 R4/R5 中 ZPL GS1 DataBar 的浏览器验收缺口。Designer 通过 Host 能力快照显示四种 `^BR` 制式，测试在真实页面中切换 Omnidirectional、Truncated、Limited、Expanded，等待服务端预览恢复，并从生成的 ZIP 中校验最终 `^BR` 字节。测试夹具显式声明足够的宽度和窄条模块，避免把 Provider 的保守页面预检误报为 UI 故障。

### R61 已完成 TODO

- [x] 新增 Designer E2E，确认 ZPL 能力选项 25–28 均可用，UI 切换和固定文字编辑保持预览有效。
- [x] 四种 DataBar 类型均通过 Designer `生成 ZPL` 流程导出，ZIP 中的 `page-001.zpl` 包含正确 `^BR` 类型、模块宽度、条高和数据字段。
- [x] 使用最新 DesignerHost 二进制完成浏览器验收；相关既有协议/HRI/介质/文字样式测试联合回归 `6/6`。

### R61 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需二维码、其他条码、图片的剩余 Designer E2E，以及换行分页和复杂布局组合。
- [ ] R5 仍需真实设备对 ZPL `^BR` 固件、GS1 业务语义、DPI/纸宽/HRI/扫描结果复核，以及 TSPL DPI/DPL 和其他厂商扩展。
- [ ] R6 的 TSPL/ZPL 原生内置字体粗体/下划线仍未声明支持；当前只提供显式主机位图降级。
- [ ] 正式性能、容量、超时阈值、旧 TFM/包消费者、复杂文字塑形/双向布局和发送中断重试仍需后续或外部验收。

### R61 本轮接受的限制

- [x] E2E 使用显式条码宽度和 `NarrowModuleWidth=2`，验证的是模板到 `^BR` 的协议链路，不推断设备的物理尺寸或扫描质量。
- [x] `^BR` 的 HRI、GS1 AI/FNC1 业务语义和固件兼容性仍按 `BLOCKED_EXTERNAL` 处理。

## R60 本轮：ZPL GS1 DataBar 原生 Provider 闭环

本轮关闭 R5 中一个可在本地完成验收的 ZPL 条码子范围。依据 Zebra `^BR` 语法，将 GS1 DataBar Omnidirectional、Truncated、Limited 和 Expanded 接入独立命令、统一能力矩阵、Provider、模板页面占位和设计器能力快照。Expanded 支持显式 2–22 偶数分段宽度；`^BR` 没有普通一维条码的 HRI 参数，因此统一模板对显式 `HumanReadable=true` 严格报错，未指定时保留设备原生默认行为。

### R60 已完成 TODO

- [x] `ZplPrintCommand` 新增四种 GS1 DataBar 命名入口，支持 N/R/I/B 方向、模块放大倍数、条高和 Expanded 分段宽度；输入、Profile、旋转、尺寸和失败不污染语义在写入前校验。
- [x] ZPL `PrintCapabilities`、Provider 分派、模板能力集合、统一宽度/最小高度预检和页面元素路径错误接入 DataBar 1/2/5/6。
- [x] 明确 `^BR` 无 HRI 参数的严格边界：显式可读文字请求报能力错误，调用方可追加独立文字元素；其他协议保持既有能力矩阵。
- [x] 新增 `ZplDataBarProviderTests`：golden bytes、四角旋转、四种类型、Expanded 分段宽度、非法输入/尺寸失败恢复、显式 HRI 拒绝、Provider 页面包络、模板能力和 CPCL/TSPL 跨协议拒绝；net6.0/netcoreapp3.1 各 `29/29`。
- [x] Core 全量回归：net6.0 `2640/2640`，netcoreapp3.1 `2640/2640`。

### R60 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需二维码、条码、图片的剩余锚点和占用尺寸，换行分页组合，以及更多 Designer E2E；本轮关闭的是 ZPL DataBar 子范围，未增加该条码的浏览器流程测试。
- [ ] R5 仍需真实设备对 ZPL `^BR` 固件版本、GS1 数据语义、DPI/纸宽/HRI/扫描结果复核，以及 TSPL DPI/DPL、ESC/POS/ZPL 其他厂商扩展和 Code49/Telepen 控制字符策略。
- [ ] R6 的 TSPL/ZPL 原生内置字体粗体/下划线仍未声明支持；当前只提供显式主机位图降级，真实设备内置字体效果仍需设备验收。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准；真实 Epson/兼容 ESC/POS、TSC、Zebra 设备仍需硬件验收。
- [ ] 旧 TFM/包消费者、GS1 业务 AI 自动解析、厂商私有 FNC1/代码页、复杂文字塑形/双向布局、跨平台字库 CI、发送中断与重试恢复仍属外部或后续范围。

### R60 本轮接受的限制

- [x] 统一 DataBar 数字制式沿用当前公共模型的 13 位输入和 Limited 首位 0/1 约束；Expanded 保留可安全嵌入 ZPL 字段的 2–255 字符边界，业务 AI/FNC1 组合不自动解析。
- [x] `^BR` 的标准参数没有 HRI 开关；本轮不伪造一个不存在的参数，显式 `HumanReadable=true` 直接报错，未指定值不追加文字。
- [x] 宽度和最小高度使用保守模块上界，真实 Zebra 固件、堆叠布局、DPI、纸宽、打印和扫描结果标记为 `BLOCKED_EXTERNAL`。

## R59 本轮：TSPL/ZPL 文字样式 Designer E2E 闭环

本轮补齐 R58 遗留的 Designer 浏览器验收：真实页面加载带粗体、下划线和 90 度旋转的模板时，TSPL/ZPL 默认保持严格能力错误；分别勾选介质设置中的“文字样式位图”后，页面请求携带对应协议选项，预览恢复，导出包包含 TSPL `BITMAP` 或 ZPL `^GFA`。

### R59 已完成 TODO

- [x] 新增 Designer E2E，覆盖 TSPL/ZPL 设置面板的默认关闭状态、严格错误、开关请求、预览恢复和最终协议字节。
- [x] `pnpm test:e2e -- e2e/designer.spec.ts -g "文字样式位图"` 在预构建 Host 与 Vite 服务下通过，`1/1`；既有 TSPL/ZPL 介质 E2E 同样 `1/1`。
- [x] R58 的本地主机位图降级现在同时具备命令、Provider、模板、Host、项目设置和浏览器 E2E 证据。

### R59 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需二维码、条码、图片的剩余锚点和占用尺寸，换行分页组合，以及其他能力的 Designer E2E。
- [ ] R5 仍需真实设备对 TSPL DPI/DPL 产品语义复核、ZPL/ESC/POS 其他厂商扩展，以及 Code49/Telepen 控制字符转义策略；本地 Provider 映射继续保持严格边界。
- [ ] R6 的 TSPL/ZPL 原生内置字体粗体/下划线仍未声明支持；当前只提供显式主机位图降级，真实设备内置字体效果仍需设备验收。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准；真实 Epson/兼容 ESC/POS、TSC、Zebra 设备的 DPI、纸宽、打印和扫描仍需硬件验收。
- [ ] 旧 TFM/包消费者、GS1 业务 AI 自动解析、厂商私有 FNC1/代码页、复杂文字塑形/双向布局、跨平台字库 CI、发送中断与重试恢复仍属外部或后续范围。

### R59 本轮接受的限制

- [x] E2E 使用预构建且 `--no-build --no-restore` 的本地 Host；普通 Playwright webServer 的在线还原会触发离线 NuGet `NU1900`，不把该环境限制伪装为产品失败。
- [x] 浏览器测试验证协议请求和导出字节，不宣称真实打印机已经接受位图或产生可扫描结果；硬件验收仍为 `BLOCKED_EXTERNAL`。

## R58 本轮：TSPL/ZPL 文字样式显式位图降级与设计器接入

本轮关闭了一个可在本地完整验证的 R6 子范围：TSPL/ZPL 原生 `TEXT` 没有统一的粗体、下划线和旋转下划线语义，因此默认行为继续严格拒绝；调用方显式设置 `TsplMediaOptions.AllowRasterizedTextStyles` 或 `ZplMediaOptions.AllowRasterizedTextStyles` 后，Provider 使用主机字体生成单色位图，分别通过 TSPL `BITMAP` 和 ZPL `^GFA` 输出。四个旋转角度、模板能力预检、元素路径和 Designer 介质设置均复用同一能力开关。

### R58 已完成 TODO

- [x] 新增 TSPL/ZPL `AllowRasterizedTextStyles` 选项，默认 `false`；`Clone`、TemplateRenderer、TemplateProtocolCapabilities、Provider 和 Host 请求契约保持一致。
- [x] 新增 TSPL `TextRasterized`/ZPL `TextRasterized` 显式命令入口，支持粗体、下划线和 0/90/180/270 度旋转；输出前校验图片能力、坐标、编码和页边界，失败不污染动作记录。
- [x] 统一模板在显式开关下按主机字体计算样式墨迹和行高，输出 `BITMAP`/`^GFA`；未开启时仍在预检阶段明确报错，不静默丢失样式。
- [x] Designer API、项目导入/导出、Host DTO 和 TSPL/ZPL 介质设置面板接入该开关；默认关闭，保存后可恢复。
- [x] 新增 TSPL/ZPL 直接命令、Provider 和模板样式回归；net6.0/netcoreapp3.1 分别 `12/12`，模板字体预检分别 `7/7`；Core 全量分别 `2611/2611`；DesignerHost `109/109`；前端 Vitest `243/243`、`vue-tsc`/Vite 构建通过；Core/Templates 五目标框架构建均 0 警告、0 错误。

### R58 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区/旋转组合、主机字体墨迹与真实设备字高校准。
- [ ] R4/R5 仍需二维码、条码、图片的剩余锚点和占用尺寸，换行分页组合，以及更多 Designer E2E（本轮只接入设置字段，未完成完整浏览器流程验收）。
- [ ] R5 仍需真实设备对 TSPL DPI/DPL 产品语义复核、ZPL/ESC/POS 其他厂商扩展，以及 Code49/Telepen 控制字符转义策略；本地 Provider 映射继续保持严格边界。
- [ ] R6 的 TSPL/ZPL 原生内置字体粗体/下划线仍未声明支持；当前只提供显式主机位图降级，真实设备内置字体效果仍需设备验收。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准；真实 Epson/兼容 ESC/POS、TSC、Zebra 设备的 DPI、纸宽、打印和扫描仍需硬件验收。
- [ ] 旧 TFM/包消费者、GS1 业务 AI 自动解析、厂商私有 FNC1/代码页、复杂文字塑形/双向布局、跨平台字库 CI、发送中断与重试恢复仍属外部或后续范围。

### R58 本轮接受的限制

- [x] 位图降级使用调用方提供或当前主机可解析的字体，输出的是图像语义，不宣称等价于 TSPL/ZPL 固件内置字体；需要打印机字体一致性时保持严格拒绝并使用设备字体契约。
- [x] 位图路径要求目标 Profile 允许图片；不支持图片的设备不会被强制降级，仍返回包含协议和元素路径的能力错误。
- [x] 默认配置、原生无样式文字和 CPCL/ESC/POS 行为保持不变；本轮不修改版本号、包发布或真实设备验收状态。

## R57 本轮：ESC/POS Native Expanded 字符契约修正

R56 已将 Epson ESC/POS GS k Function B 的 GS1 DataBar Omnidirectional、Truncated、Limited、Expanded（75–78）以及 Code128 auto（79）接入统一条码模型。本轮依据 [Epson GS k 参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_lk.html) 修正 Expanded 字符契约：官方允许的裸 `{` 保留为字面数据，逻辑括号和 ASCII 29 FNC1 继续编码为 Epson 要求的 `{(`、`{)`、`{1`。

### R57 已完成 TODO

- [x] 新增 BarcodeType.Gs1DataBarOmnidirectional、Gs1DataBarTruncated、Gs1DataBarLimited、Gs1DataBarExpanded、Code128Auto，并保留既有枚举值。
- [x] EscPosPrintCommand 新增四种 DataBar 命名入口和 Code128 auto 字符/原始字节入口；GS k 75–79 的长度、字符集、Limited 首位、Expanded 裸 `{`/括号/FNC1 转义和高度/宽度/可读文字参数在追加前校验，失败不污染动作记录。
- [x] ESC/POS Native PrintCapabilities、Provider 分派、模板能力快照和页面边界预检接入新类型；DataBar 按设备声明的最小条高（33/13/10/34 倍模块宽度）及保守模块数保护占用尺寸。
- [x] Designer 条码选项加入 25–29，其他协议保持严格拒绝；新增 Native golden-byte、原始字节、Expanded 转义、输入失败恢复、能力矩阵和模板输出回归。

### R57 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码/图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R5 仍需真实设备对 TSPL DPI/DPL 产品语义复核、ZPL/ESC/POS 其他厂商扩展和 Code49/Telepen 的原始控制字符转义策略；TSPL DPI/DPL 的本地映射和 ESC/POS 标准 Function B 75–79 已关闭本地缺口。
- [ ] R6 仍需 TSPL/ZPL 内置字体粗体/下划线以及旋转下划线组合。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准；真实 Epson/兼容 ESC/POS 设备的 Function B 支持、HRI、DPI、纸宽、打印和扫描仍需硬件验收。
- [ ] 旧 TFM/包消费者、GS1 业务 AI 自动解析、厂商私有 FNC1/代码页、复杂文字塑形/双向布局、跨平台字库 CI、发送中断与重试恢复仍属外部或后续范围。

### R57 本轮接受的限制

- [x] 统一模板的 Code128 auto 入口以 7-bit ASCII 为边界；需要 0–255 任意字节时使用独立命令 BarcodeCode128Auto(byte[])。
- [x] DataBar Expanded 的逻辑括号和 ASCII 29 FNC1 由命令转成 Epson GS k 78 的 `{(`、`{)` 和 `{1` 字节；官方允许的裸 `{` 原样保留，其他字符仍严格报错。
- [x] 宽度采用防低估的保守模块数，不宣称任意机型的精确物理宽度；真实固件和扫描结果标记为 BLOCKED_EXTERNAL。

## R55 本轮：ZPL Code 49 原生条码

R55 将统一 `BarcodeType.Code49` 接入 Zebra ZPL 原生 `^B4` 路径。独立命令、能力矩阵、ZPL Provider、模板能力快照和页面宽度预检使用同一输入契约：数据接受 1–81 个可安全嵌入 ZPL 字段的 ASCII 字符，条高限制为 1–32000 点，模块宽度限制为 1–10 点，方向映射为 N/R/I/B；可读文字映射到 ZPL `f=B`，起始模式默认 `A`，也可显式指定 `A` 或 `0`–`5`。规范参数依据 [Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)，Code 49 的堆叠行数、固件、HRI、DPI、纸宽和扫描结果仍需实机验收。

### R55 分类后的 TODO

#### 已完成 TODO

- [x] 新增 `ZplPrintCommand.BarcodeCode49` 默认/旋转/起始模式重载，输出 `^B4`，校验数据、坐标、条高、模块宽度、模式、Profile 能力和失败不污染动作记录。
- [x] ZPL `PrintCapabilities`、Provider 分派、模板能力集合和统一宽度预检接入 Code 49；按每行 49 模块保守计算宽度，堆叠行数不虚假推断。
- [x] 新增 ZPL Code 49 golden-byte、四角方向、HRI、显式起始模式、非法输入/尺寸/模式失败恢复、旋转禁用、Profile 禁用、页面边界、模板能力和跨协议矩阵测试；`ZplCode49ProviderTests` 在 net6.0 与 netcoreapp3.1 均 `37/37`，既有 `TsplCode49ProviderTests` 均 `26/26`。
- [x] 更新既有 TSPL Code 49 能力断言，明确 TSPL 与 ZPL 双 Provider 支持，并同步更新协议、模板、设计器和验证文档。

#### 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码/图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R5 仍需 TSPL DPI/DPL 的完整产品语义、ESC/POS 其他原生条码，以及 ZPL 其他未映射类型的规范/Provider/尺寸闭环；Code 49/Telepen 的控制字符转义仍保持严格拒绝，未提供 `^FH` 转义入口。
- [ ] R6 仍需 TSPL/ZPL 内置字体粗体/下划线以及旋转下划线组合。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准；真实热敏/针式设备验收、旧 TFM/包消费者和复杂文字能力继续属于外部或后续范围。

#### 本轮接受的限制

- [x] 统一 ZPL Code 49 入口只接受 32–127 ASCII，明确拒绝 `^`、`~`、引号和控制字符，不在未定义转义语义下静默改写。
- [x] 本地宽度只用于防止页面预检低估，按单行 49 模块和调用方模块宽度计算；Code 49 的 2–8 行堆叠高度由设备选择，本轮不伪造设备精确高度。
- [x] ZPL Code 49 关闭的是本轮本地可执行缺口；真实固件、纸张、DPI、HRI 和扫描门禁仍为 `BLOCKED_EXTERNAL`，不伪装成本地通过。

## R54 本轮：ZPL Plessey 原生条码

R54 将统一 `BarcodeType.Plessey` 接入 Zebra ZPL 原生 `^BP` 路径。独立命令、能力矩阵、ZPL Provider、模板能力快照和页面宽度预检使用同一输入契约：数据只接受非空 ASCII 数字，条高限制为 1–32000 点，窄条宽度限制为 1–10 点，方向映射为 N/R/I/B，`e=N` 不重复请求设备打印校验位；`^BY` 默认 3:1 比例落在 Zebra 文档声明的 Plessey 2:1–3:1 范围内。规范参数依据 [Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)，固件、HRI、条高、DPI、纸宽和扫描结果仍需实机验收。

### R54 分类后的 TODO

#### 已完成 TODO

- [x] 新增 `ZplPrintCommand.BarcodePlessey` 及旋转重载，输出 `^BP`，校验数字数据、坐标、条高、模块宽度、Profile 能力和失败不污染动作记录。
- [x] ZPL `PrintCapabilities`、Provider 分派、模板能力集合和统一宽度预检接入 Plessey；按 `4n+17` 二值单元和 3:1 上界保守计算，四角方向和元素路径保持一致。
- [x] 新增 ZPL Plessey golden-byte、四角方向、HRI、非法输入/尺寸失败恢复、旋转禁用、Profile 禁用、页面边界、模板能力和跨协议矩阵测试；net6.0/netcoreapp3.1 专项均 `26/26`。
- [x] 修正既有 TSPL Plessey 能力测试，明确 TSPL 与 ZPL 双 Provider 支持。

#### 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码/图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R5 仍需 TSPL DPI/DPL 的完整产品语义、ESC/POS 其他原生条码，以及 ZPL 其他未映射类型的规范/Provider/尺寸闭环；Telepen/Code 49 的控制字符转义契约也需继续明确。
- [ ] R6 仍需 TSPL/ZPL 内置字体粗体/下划线以及旋转下划线组合。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准；真实热敏/针式设备验收、旧 TFM/包消费者和复杂文字能力继续属于外部或后续范围。

#### 本轮接受的限制

- [x] 统一 Plessey 入口固定使用数字数据和 `e=N`；需要厂商私有字符集或校验位显示策略时使用受信任的原始 ZPL 入口。
- [x] 本地宽度只用于防止页面预检低估，按 `^BY` 默认 3:1 和起止/校验槽位保守计算，不宣称任意 Zebra 固件的精确模块宽度或扫描结果。
- [x] ZPL Plessey 关闭的是本轮本地可执行缺口；真实固件、纸张、DPI、HRI 和扫描门禁仍为 `BLOCKED_EXTERNAL`，不伪装成本地通过。

## R53 本轮：ZPL Code 11 原生条码

R53 将统一 `BarcodeType.Code11` 接入 Zebra ZPL 原生 `^B1` 路径。独立命令、能力矩阵、ZPL Provider、模板能力快照和页面宽度预检使用同一输入契约：数据只接受非空 ASCII 数字和连字符，设备使用 `e=N` 生成两个 Modulo-11 校验位；高度限制为 1–32000 点，窄条宽度限制为 1–10 点，方向映射为 N/R/I/B。规范参数依据 [Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)，固件是否支持该制式、HRI/校验位显示、条高、DPI、纸宽和扫描结果仍需实机验收。

### R53 分类后的 TODO

#### 已完成 TODO

- [x] 新增 `ZplPrintCommand.BarcodeCode11` 及旋转重载，输出 `^B1`，校验数据、坐标、条高、模块宽度、Profile 能力和失败不污染动作记录。
- [x] ZPL `PrintCapabilities`、Provider 分派、模板能力集合和统一宽度预检接入 Code 11；预检按最多两个校验符号和 3:1 上界保守计算，四角方向和元素路径保持一致。
- [x] 新增 ZPL Code 11 golden-byte、四角方向、HRI、非法输入/尺寸失败恢复、旋转禁用、Profile 禁用、页面边界、模板能力和跨协议矩阵测试；专项 net6.0/netcoreapp3.1 各 `26/26`，Core 全量 net6.0 `2519/2519`。
- [x] 修正既有 TSPL Code 11 能力测试中“TSPL only”的陈旧断言，明确 TSPL 与 ZPL 双 Provider 支持。
- [x] Templates 五目标框架 Debug 构建 `netcoreapp3.1`、`net6.0`、`net7.0`、`netstandard2.0`、`netstandard2.1`：0 警告、0 错误；`git diff --check` 无格式错误。

#### 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码/图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R5 仍需 TSPL DPI/DPL 的完整产品语义、ESC/POS 其他原生条码，以及 ZPL Code 49 和其他未映射类型的规范/Provider/尺寸闭环；Telepen/Code 49 的控制字符转义契约也需继续明确。
- [ ] R6 仍需 TSPL/ZPL 内置字体粗体/下划线以及旋转下划线组合。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准；真实热敏/针式设备验收、旧 TFM/包消费者和复杂文字能力继续属于外部或后续范围。

#### 本轮接受的限制

- [x] `^B1` 统一入口固定使用两个设备校验位（`e=N`），不在主机重复生成或把校验位混入业务数据；需要厂商私有校验策略时使用受信任的原始 ZPL 入口。
- [x] 本地宽度只用于防止页面预检低估，按最多两个校验符号和 3:1 上界保守计算，不宣称任意 Zebra 固件的精确模块宽度或扫描结果。
- [x] ZPL Code 11 关闭的是本轮本地可执行缺口；真实固件、纸张、DPI、HRI 和扫描门禁仍为 `BLOCKED_EXTERNAL`，不伪装成本地通过。

## R52 本轮：TSPL 条码类型专用比例编辑器

R52 将前端条码属性面板从单一 CPCL `Ratio` 下拉扩展为 TSPL TELEPEN/TELEPENN/CODE49 专用比例编辑器。编辑器提供 `1:1`、`1:2`、`1:3`、`2:5`、`3:7` 五档窄条:宽条比例，按当前 `barcodeModuleWidth` 能力范围选取最接近的整数模块对，并把结果写入 `NarrowModuleWidth`/`WideModuleWidth`；不可容纳的比例禁用，手动改出不合法组合则保留输入并在输出前提示。旧 `Ratio` 继续保留为兼容回退。该轮只改变 Designer 配置表达，不改变 Provider 的编码语义。

### R52 分类后的 TODO

#### 已完成 TODO

- [x] 新增 TSPL 变量比例常量、比例识别、旧 `Ratio` 兼容映射和能力范围内整数模块对计算；`2:5`、`3:7` 使用最小整数对缩放，避免用奇数窄条伪造比例。
- [x] Property Inspector 对 TELEPEN、TELEPENN、CODE49 显示专用五档比例选择器，自动同步窄/宽条模块宽度，超范围选项禁用，手动不合法值显示能力警告。
- [x] 新增比例列表、类型识别、模块对缩放、能力上限和旧字段导入测试；前端全量 Vitest `19` 个文件 `243/243`，`vue-tsc -b` 与 Vite 构建通过。

#### 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码/图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R5 仍需 TSPL DPI/DPL 的完整产品语义、其他未映射 ESC/POS/ZPL 类型和控制字符转义契约。
- [ ] R6 仍需 TSPL/ZPL 内置字体粗体/下划线以及旋转下划线组合。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准；真实设备验收、旧 TFM/包消费者和复杂文字能力继续属于外部或后续范围。

#### 本轮接受的限制

- [x] 比例选择器只负责表达合法模块宽度，不推断 Code 49 堆叠行数、最终编码结构或设备扫描宽度；这些继续由 TSPL 固件和实机验收决定。
- [x] 旧 `Ratio` 保留为跨协议兼容字段，TSPL 选定比例时显式模块宽度优先；切换到不支持 TSPL 的协议仍由 Host/后端严格校验。

## R51 本轮：TSPL Code 49 原生条码

R51 将统一 `BarcodeType.Code49=24` 接入 TSPL 原生 `BARCODE ...,"CODE49"` 路径。独立命令、通用入口、Provider、模板度量、能力快照和 Designer 选项使用同一输入与比例契约：CODE49 接受 1–81 个可安全嵌入 TSPL 字段的 ASCII 字符，宽窄比只允许 1:1、1:2、1:3、2:5、3:7；控制字符、引号和换行在 TSPL framing 层明确拒绝。Code 49 是堆叠码，目标固件决定行数和最终符号结构；本地页面预检按 49 单元/内容符号加起止槽位的保守上界计算。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf)，目标固件从 V6.89EZ 起的实际可用性和打印扫描结果仍需设备验收。

### R51 分类后的 TODO

#### 已完成 TODO

- [x] 新增 `BarcodeType.Code49=24`，TSPL 提供 `BarcodeCode49` 独立入口和通用 `BARCODE "CODE49"` 映射；长度、ASCII 字符集、引号/控制字符和五种合法宽窄比在写入前严格校验，失败不污染已有输出。
- [x] TSPL Provider、`PrintCapabilities`、模板能力集合和统一宽度度量接入 Code 49；按数据长度、起止槽位和 49 单元内容槽位保守预留，四角度原点、页面边界、元素路径和 Profile 禁用保持一致。
- [x] Designer `barcodeOptions` 加入 `CODE49` 值 `24`；其他协议继续保持严格拒绝，不把堆叠码静默降级为普通一维码。
- [ ] Designer 当时仍复用旧 CPCL Ratio 下拉；Code 49/Telepen 的五种 TSPL 比例编辑器在 R52 完成。
- [x] 新增 Code 49 golden-byte、通用入口、DEL 字符、非法输入/长度/比例失败恢复、Profile 禁用、五种合法比例、四角度、页面边界、跨协议能力和模板输出测试；net6.0 与 netcoreapp3.1 专项均 `26/26`，Core 全量回归均 `2493/2493`。

#### 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码/图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R5 仍需 TSPL DPI/DPL 的完整产品语义，以及其他仍未映射的 ESC/POS/ZPL 类型；Telepen/Code 49 的原始 ASCII 控制字符转义契约也需在后续明确支持方式。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合；原生不能表达时需要显式 Raster 配置或严格边界。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的 Code 49、Telepen、其他条码、文字、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] TSC 手册声明 CODE49 可用 ASCII 0–127；本轮只接受能安全嵌入 TSPL `BARCODE` 引号字段的可打印 ASCII 与 DEL，NUL、ESC、CR/LF、引号等 framing 冲突字符明确报错，不静默改写。
- [x] Code 49 的堆叠行数、编码校验和实际模块布局交给目标固件；本地 49 单元/符号宽度只用于防止页面预检低估，不宣称任意机型的精确扫描宽度。
- [x] 本轮使用 net6.0 与 netcoreapp3.1 已声明运行时完成专项及全量回归；Templates 五目标框架 Debug 构建 0 警告/0 错误，Designer 全量 Vitest `19` 个文件 `243/243`、`vue-tsc -b` 与 Vite 构建通过；`NU1900` 审计源不可访问、旧 TFM/包消费者和真实硬件验收仍按外部工具链边界记录。

## R50 本轮：TSPL Telepen 原生条码

R50 将统一 `BarcodeType.Telepen=22` 和 `BarcodeType.Telepenn=23` 接入 TSPL 原生 `BARCODE ...,"TELEPEN"`/`"TELEPENN"` 路径。独立命令、通用入口、Provider、模板度量、能力快照和 Designer 选项使用同一输入与比例契约：TELEPEN 接受 1–30 个可安全嵌入 TSPL 字段的 ASCII 字符，TELEPENN 接受 1–60 位 ASCII 数字，宽窄比只允许 1:1、1:2、1:3、2:5、3:7；控制字符、引号和换行在 TSPL framing 层明确拒绝。宽度按每个数据符号及起止符的 16 单元保守上界计算，四方向坐标、页面边界、Profile 禁用和失败恢复保持一致。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf)，目标固件从 V6.89EZ 起的实际可用性仍需设备验收。

### R50 分类后的 TODO

#### 已完成 TODO

- [x] 新增 `BarcodeType.Telepen=22` 与 `BarcodeType.Telepenn=23`，TSPL 提供 `BarcodeTelepen`/`BarcodeTelepenn` 独立入口和通用 `BARCODE` 类型映射；长度、ASCII/数字字符集、引号/控制字符和五种合法宽窄比在写入前严格校验，失败不污染已有输出。
- [x] TSPL Provider、`PrintCapabilities`、模板能力集合和统一宽度度量接入两种 Telepen；使用数据长度加起止符的 16 单元保守上界，四角度原点、页面边界、元素路径和 Profile 禁用保持一致。
- [x] Designer `barcodeOptions` 加入 `TELEPEN` 值 `22` 与 `TELEPENN` 值 `23`；两者不伪造固定宽条比例，继续由统一 `Ratio`/显式宽条输入和后端比例校验决定。
- [x] 新增 Telepen/Telepenn golden-byte、通用入口、DEL 字符、非法输入/长度/比例失败恢复、Profile 禁用、五种合法比例、四角度、页面边界、跨协议能力和模板输出测试；net6.0 与 netcoreapp3.1 专项均 `37/37`，Core 全量回归均 `2467/2467`。
- [x] Core/Templates 五目标框架 Debug 构建通过，0 警告、0 错误；Designer 全量 Vitest `19` 个文件 `240/240`，`vue-tsc -b` 与 Vite 构建通过。

#### 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码/图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] Designer 当前复用旧 CPCL Ratio 下拉；Telepen 的五种 TSPL 比例尚未做成类型专用编辑器，复杂比例仍需显式 `WideModuleWidth` 并由后端校验。
- [ ] R5 仍需 TSPL DPI/DPL 的完整产品语义，以及其他仍未映射的 ESC/POS/ZPL 类型；Telepen/Code 49 的原始 ASCII 控制字符转义契约也需在后续明确支持方式。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合；原生不能表达时需要显式 Raster 配置或严格边界。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的 Telepen、其他条码、文字、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] TSC 手册声明 TELEPEN 可用 ASCII 0–127；本轮只接受能安全嵌入 TSPL `BARCODE` 引号字段的可打印 ASCII 与 DEL，NUL、ESC、CR/LF、引号等 framing 冲突字符明确报错，不静默改写。
- [x] 本地宽度按 16 单元/符号及起止符槽位保守预留，目标固件的实际条形结构、HRI 对齐、条高、DPI、扫描一致性仍需设备验收。
- [x] 本轮使用 net6.0 与 netcoreapp3.1 已声明运行时完成专项及全量回归；`NU1900` 审计源不可访问、旧 TFM/包消费者和真实硬件验收仍按外部工具链边界记录。

## R49 本轮：TSPL China Post 原生条码

R49 将统一 `BarcodeType.ChinaPost=21` 接入 TSPL 原生 `BARCODE ...,"CPOST"` 路径。独立命令和通用入口只接受非空 ASCII 数字，CPOST 固定使用 1:1 窄宽比且不由主机追加校验位；Provider、能力快照、统一宽度度量、四角度坐标、模板预检、Designer 选项和失败恢复使用同一语义。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_3_0.pdf)，具体机型固件是否提供 CPOST 以及最终条形编码仍由 Profile 和实机验收决定。

### R49 分类后的 TODO

#### 已完成 TODO

- [x] 新增 `BarcodeType.ChinaPost=21`、TSPL `BarcodeChinaPost` 独立入口和通用 `BARCODE "CPOST"` 映射；空值、非数字数据和非 1:1 宽条比例在写入前失败且不污染已有输出。
- [x] TSPL Provider、`PrintCapabilities`、模板能力集合和统一宽度度量接入 China Post；按数据符号和起止符槽位保守预留宽度，四角度坐标、页面边界、元素路径和 Profile 禁用保持一致。
- [x] `IPrintCanvas` 在模板省略 `WideModuleWidth` 时将 CPOST 收敛为 1:1；显式不等宽条仍严格拒绝，不静默忽略旧 `Ratio`。
- [x] Designer `barcodeOptions` 加入 China Post 值 `21`，固定宽条倍数为 `1`；前端 Vitest `19` 个文件、`240/240`，`vue-tsc -b` 与 Vite 构建通过。
- [x] 新增 China Post golden-byte、通用入口、非法输入/比例失败恢复、Profile 禁用、四角度、页面边界、跨协议能力、模板输出和默认比例测试；net6.0 与 netcoreapp3.1 专项各 `16/16`，完整回归各 `2430/2430`。
- [x] Core/Templates 五个目标框架 Debug 构建通过，0 警告、0 错误；完整回归过程未改变 CPCL 既有输出契约。

#### 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码/图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R5 其他 TSPL 条码变体（Code 49、DPI/DPL 的完整产品语义）以及仍未映射的 ESC/POS/ZPL 类型，需要逐项确认规范、Provider 映射和统一输入契约。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合；原生不能表达时需要显式 Raster 配置或严格边界。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的 CPOST、其他条码、文字、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] CPOST 只发送调用方提供的数字内容，设备端最终校验/符号编码依赖 TSPL 固件；本地宽度按固定 1:1 模块和起止槽位保守预留，不宣称任意机型的扫描一致性。
- [x] 本轮使用已有声明运行时完成本地验证；`NU1900` 审计源不可访问、旧 TFM/包消费者和真实硬件验收仍按外部边界记录。

## R48 本轮：TSPL Code 11 原生条码

R48 将统一 `BarcodeType.Code11` 接入 TSPL 原生 `BARCODE ...,"11"` 路径。独立命令和通用入口接受非空 ASCII 数字/连字符，Code 11 的一个或两个 Modulo-11 校验位继续由设备生成；Provider、能力快照、宽度度量、四角度坐标、模板预检和失败恢复使用同一语义。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)，目标机型是否提供该固件条码仍由 Profile 和实机验收决定。

### R48 分类后的 TODO

#### 已完成 TODO

- [x] 新增 `BarcodeType.Code11=20`，TSPL 提供 `BarcodeCode11` 独立入口和通用 `BARCODE "11"` 映射；数据只允许非空 ASCII 数字和连字符，空值、非法字符和 Profile 禁用均在写入前失败且不污染已有输出。
- [x] TSPL Provider/`PrintCapabilities`/模板能力集合接入 Code 11；统一宽度按设备补充一个或两个校验位及最大两个宽元素预留，四角度坐标、页面边界和元素路径保持一致。
- [x] Designer `barcodeOptions` 加入 Code 11 值 `20`，Host 能力快照返回支持时可在属性面板选择；前端全量 Vitest `19` 个文件、`240/240`，`vue-tsc -b` 与 Vite 构建通过。
- [x] 新增 Code 11 golden-byte、通用入口、输入失败恢复、Profile 禁用、四角度、页面边界、跨协议能力和模板输出测试；net6.0 与 netcoreapp3.1 专项各 `14/14`，完整回归各 `2414/2414`。
- [x] 修正 ZPL PLANET 历史测试中仍将已实现 TSPL PLANET 断言为不支持的两项基线，ZPL PLANET 专项两个运行时各 `39/39`。

#### 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码/图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R5 其他 TSPL 条码变体（China Post、Telepen、Code 49、DPI/DPL 的完整产品语义）以及仍未映射的 ESC/POS/ZPL 类型，需要逐项确认规范、Provider 映射和统一输入契约。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合；原生不能表达时需要显式 Raster 配置或严格边界。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的条码、文字、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] Code 11 只发送业务数据，设备端校验位和最终符号编码依赖 TSPL 固件；本地宽度度量按最大元素宽度保守预留，不宣称任意机型扫描一致性。
- [x] 本轮使用已有声明运行时完成本地验证；`NU1900` 审计源不可访问和旧 TFM/包消费者问题仍按外部工具链边界记录。

## R47 本轮：Designer HRI 四协议端到端矩阵

R47 在 Designer Host 端到端层补齐同一条码 HRI 模板的 CPCL、TSPL、ZPL 和 ESC/POS Raster 输出验证。用例同时保留 12 点可读文字预留和四边显式静区，并检查各协议实际生成的页面文件和协议特征字节，确保 Designer 的统一属性不会只在单一 TSPL 预览路径上通过。

### R47 分类后的 TODO

#### 已完成 TODO

- [x] 新增 Designer Host HRI 四协议渲染矩阵：CPCL `BT`、TSPL `BARCODE`、ZPL `^BC` 和 ESC/POS Raster `GS v 0` 均完成输出断言。
- [x] 跨协议模板保留 `HumanReadableReserveHeight=12` 和显式四边静区，确认 HRI 预留不会在协议切换时丢失。
- [x] 预编译 Host + 浏览器 E2E：新增用例 `HRI 通过 Designer Host 输出 CPCL、TSPL、ZPL 与 ESC/POS Raster`，执行结果 `1/1`。

#### 未完成 TODO

- [ ] R3 仍需完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码/图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R5 其他 TSPL 条码变体（China Post、Telepen、Code 49、DPI/DPL 的完整产品语义）以及仍未映射的 ESC/POS/ZPL 类型，需要逐项确认规范、Provider 映射和统一输入契约。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合；原生不能表达时需要显式 Raster 配置或严格边界。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的条码、文字、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] 本轮使用预编译 DesignerHost，并通过 `--ignore-failed-sources -p:NuGetAudit=false` 启动；源码默认启动仍受当前 NuGet `NU1900` 审计源不可访问影响。
- [x] E2E 验证协议文件和布局契约，不替代真实设备的 HRI 字高、DPI、纸宽和可扫描性验收。

## R46 本轮：TSPL PLANET 原生条码

R46 将统一 `BarcodeType.Planet` 接入 TSPL 原生 `BARCODE ...,"PLANET"` 路径。独立命令和通用条码入口统一移除连字符、校验 12/14 位数字及末位校验和；Provider、能力快照、宽度度量、四角度坐标、模板预检和输出保持同一语义。协议依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)，实际机型是否启用该条码仍由 Profile 和设备验收决定。

### R46 分类后的 TODO

#### 已完成 TODO

- [x] `TsplPrintCommand.BarcodePlanet` 及通用 `Barcode` 类型映射，包含连字符归一化、长度/数字/校验和校验、原始输出失败恢复。
- [x] TSPL `PrintCapabilities`、Provider 添加 PLANET 能力、宽度度量、旋转原点和统一模板输出。
- [x] 新增 TSPL PLANET golden-byte、非法输入、Profile 禁用、页面边界、四角度和模板能力回归；net6.0 `16/16`、netcoreapp3.1 `16/16`。

#### 未完成 TODO

- [ ] R3 仍需扩展完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码/图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R5 其他 TSPL 条码变体（China Post、Telepen、Code 49、DPI/DPL 的完整产品语义）以及仍未映射的 ESC/POS/ZPL 类型，需要逐项确认规范、Provider 映射和统一输入契约。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合；原生不能表达时需要显式 Raster 配置或严格边界。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的条码、文字、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] PLANET 统一模型按完整 12/14 位含校验位表达；TSPL 设备版本、固件和最大数据长度差异由 Profile/实机验收确认，不在本地测试中冒充兼容。
- [x] 本轮只增加 TSPL 原生 PLANET；未映射的 TSPL 条码继续严格拒绝，不通过通用字符串入口静默降级为其他符号。

## R45 本轮：ESC/POS Raster 旋转下划线回归闭环

R45 对照生产实现和模板回归确认 ESC/POS Raster 的文字旋转下划线已经完整贯通：0/90/180/270 度均沿旋转后的文字掩码绘制下划线，模板按实际墨迹边界排版并输出 `GS v 0` 光栅数据。补齐 net6.0 与 netcoreapp3.1 的专项证据，并修正文档中仍将该能力列为待办的状态。TSPL/ZPL 内置字体无法用统一原生命令表达粗体/下划线，继续保持显式 Raster 或严格拒绝的边界。

### R45 分类后的 TODO

#### 已完成 TODO

- [x] ESC/POS Raster 文字旋转下划线：四角度墨迹、紧致占用边界、模板分页/静区路径和光栅输出保持一致。
- [x] 旋转下划线模板专项回归：net6.0 `12/12`、netcoreapp3.1 `12/12`。
- [x] 同步验证文档与 Open Actionable 清单，避免把已实现能力重复列为未完成。

#### 未完成 TODO

- [ ] R3 仍需扩展完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码/图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合；原生不能表达时需要显式 Raster 配置或严格边界。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的条码、文字、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] 旋转下划线只在 ESC/POS Raster 通过位图表达；ESC/POS Native 的坐标旋转/非 24 点文字，以及 TSPL/ZPL 内置字体的粗体/下划线仍按能力矩阵严格拒绝。
- [x] 本轮测试证明声明运行时与资源预算内的输出契约，不替代真实打印机的 DPI、字高、纸宽和扫描验收。

## R44 本轮：Designer 文字旋转占用端点同步

R44 修正 Designer 已有 `EndCoordinate` 的 Type1 文字切换角度时的属性编辑契约。文字端点和二维码、条码、图片一样表示旋转后的占用框；切换 0/180 与 90/270 之间时，属性面板按旧占用框交换轴并写回端点，0/180 或 90/270 之间保持占用尺寸，`Width` 继续表示旋转前行宽。新增 Designer E2E 覆盖四角度、占用尺寸、旋转前行宽和 JSON 导出。

### R44 分类后的 TODO

#### 已完成 TODO

- [x] Type1 文字属性旋转同步已有 `EndCoordinate`，避免 UI 旋转角度和预览占用框脱节。
- [x] 0/180 与 90/270 之间按旋转轴交换占用尺寸，保持 `Width` 旋转前行宽语义不变。
- [x] 新增四角度文字属性编辑 E2E，并完成前端类型、单测、构建和 Designer 文件级回归。

#### 未完成 TODO

- [ ] R3 仍需扩展完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码/图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合；原生不能表达时需要显式 Raster 配置或严格边界。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的条码、文字、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] Designer 只按已有旋转后 `EndCoordinate` 交换文字占用轴，不在浏览器侧重新测量协议字体字形；实际字宽、换行和设备边界仍由 Host/Provider 严格校验。
- [x] 文件级 E2E 使用预编译 Host 时，既有 ESC/POS Native 条码能力断言出现 1 项环境基线不一致；不影响本轮文字旋转用例，已单独记录。

## R43 本轮：Designer 自动尺寸图片旋转占用同步

R43 修正 Designer 自动尺寸图片（`Width/Height` 未声明、已有 `EndCoordinate`）切换角度时的原始尺寸推断。旋转前先按旧角度还原图片源尺寸，再按新角度重算占用端点；显式图片尺寸和画布局部组旋转保持原有语义。新增图片导入 E2E 覆盖自动尺寸、90 度旋转、属性面板原始尺寸和 JSON 导出端点；图片旋转专项 `5/5`，前端单测 `240/240`、`vue-tsc -b` 和 Vite 构建通过。

### R43 分类后的 TODO

#### 已完成 TODO

- [x] 自动尺寸图片旋转前按旧角度还原源尺寸，避免把旧占用框误当成新方向。
- [x] 自动尺寸图片旋转后同步 `EndCoordinate`，属性面板继续显示原始源宽高。
- [x] 新增图片导入/导出回归，并完整重跑图片旋转专项，确认显式图片和 ESC/POS Raster 边界不回归。

#### 未完成 TODO

- [ ] R3 仍需扩展完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码/图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合；原生不能表达时需要显式 Raster 配置或严格边界。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的条码、文字、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] 自动尺寸图片只有在已有 `EndCoordinate` 时才能从布局占用还原源尺寸；缺少端点时继续依赖资源/Provider 预览，不在设计器侧猜测资源像素尺寸。

## R42 本轮：Designer 自动尺寸条码旋转占用同步

R42 修正 Designer 自动尺寸条码（`Width` 为 0 或未声明、已有服务端 `EndCoordinate`）切换角度时的布局同步。旋转前先从已有外占用框还原条码原始尺寸，再按新角度、HRI 预留和静区重新计算 `EndCoordinate`；显式宽高条码的既有编辑语义保持不变。新增 TSPL 页面 E2E 覆盖真实服务端自动条宽、90 度旋转、属性面板占用尺寸和 JSON 导出端点。前端单测 `240/240`、自动条码/二维码/显式条码 HRI E2E 均通过，`vue-tsc -b` 和 Vite 构建通过。

### R42 分类后的 TODO

#### 已完成 TODO

- [x] 自动尺寸条码旋转前还原已有占用框，旋转后同步更新 `EndCoordinate`。
- [x] 自动尺寸条码的属性面板在 90/270 度显示交换后的占用轴，同时保留自动 `Width` 和声明的 `Height`。
- [x] 新增 TSPL 自动条码旋转 E2E，并复用二维码、显式条码旋转和 HRI 回归确认共享属性面板不回归。

#### 未完成 TODO

- [ ] R3 仍需扩展完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码与图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合；原生不能表达时需要显式 Raster 配置或严格边界。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的条码、文字、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] 自动尺寸条码只在已有 `EndCoordinate` 时可以还原实际占用；缺少端点时继续依赖 Provider 预览，不在设计器侧猜测编码宽度。

## R41 本轮：Designer 二维码旋转占用同步

R41 修正 Designer 二维码属性面板的旋转尺寸契约：切换 0/90/180/270 度时同步重算 `EndCoordinate`，属性面板始终显示旋转后的占用宽高；在 90/270 度下编辑占用尺寸时，再按旋转轴写回模板原始 `Width`/`Height`。新增 TSPL 页面 E2E 覆盖四角度、非正方形二维码、旋转后尺寸编辑和 JSON 导出端点。前端单测 `240/240`、二维码与条码尺寸/旋转 E2E 各 `1/1`、`vue-tsc -b` 和 Vite 构建通过。

### R41 分类后的 TODO

#### 已完成 TODO

- [x] Designer 二维码旋转时保持原始尺寸、旋转后占用尺寸和 `EndCoordinate` 一致。
- [x] Designer 二维码 90/270 度占用尺寸编辑按旋转轴反向写回原始 `Width`/`Height`。
- [x] 新增 TSPL Designer 二维码四角度回归，验证页面预览模型与 JSON 导出端点一致。

#### 未完成 TODO

- [ ] R3 仍需扩展完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 仍需覆盖二维码/条码与图片的剩余锚点、占用尺寸、换行分页组合，以及更多 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合；原生不能表达时需要显式 Raster 配置或严格边界。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的条码、文字、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] 二维码仍按目标协议能力限制旋转角度；本轮只修复设计器在已支持角度下的占用尺寸同步，不改变 Provider 的能力边界。

## R40 本轮：非法 HRI 组合修复与 TSPL/ZPL 运行时矩阵

R40 为 Designer 导入的 `HumanReadable:false` + 正的 `HumanReadableReserveHeight` 组合增加明确警告和清理按钮。清理动作删除无效预留高度、重算条码占用并恢复引擎预览，后端仍保持严格拒绝语义。Core 新增 TSPL/ZPL 的 HRI + 四边静区输出矩阵，确认统一模型、坐标 Provider 和模板布局在两个标签协议上都能渲染。Designer HRI 回归 `2/2`，Core 精准回归 net6.0/netcoreapp3.1 各 `19/19`，前端单测 `240/240`、`vue-tsc -b` 和 Vite 构建通过。

### R40 分类后的 TODO

#### 已完成 TODO

- [x] Designer 导入非法 HRI 预留组合时显示原因，并提供清除无效预留高度的可恢复操作。
- [x] 清理非法组合后重新计算条码占用坐标并恢复服务端严格预览。
- [x] TSPL/ZPL HRI + 显式四边静区模板/Provider 运行时矩阵及双运行时精准回归。

#### 未完成 TODO

- [ ] R3 仍需扩展完整目标运行时矩阵、更多 HRI/静区旋转组合和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和完整 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合；原生不能表达时需要显式 Raster 配置或严格边界。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的条码、文字、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] 非法导入值不会被 UI 静默改写；用户必须显式清除无效预留或重新开启 HRI，后端严格校验保持不变。

## R39 本轮：Designer HRI 与显式静区闭环

R39 修正 Designer 条码属性面板的显式 HRI 关闭语义：将可读文字切换为“关闭”时同步清除 `HumanReadableReserveHeight`，避免留下后端严格禁止的无效组合，并重新计算条码占用坐标。新增 TSPL 页面 E2E 覆盖可读文字开启、预留高度、四边显式静区、按实际 Code128 编码宽度调整固定尺寸、JSON 导出往返和关闭 HRI 后的二次预览；HRI 下拉增加专用可访问名称，避免与预留高度控件发生严格定位歧义。Designer 单测 `240/240`、新增 HRI/静区 E2E `1/1`、`vue-tsc -b` 和 Vite 生产构建均通过；真实设备字高校准仍需外部验收。

### R39 分类后的 TODO

#### 已完成 TODO

- [x] Designer 显式关闭条码 HRI 时清除 `HumanReadableReserveHeight`，并保持静区与占用坐标可继续编辑。
- [x] 新增 TSPL HRI + 预留高度 + 四边显式静区 + 固定尺寸 + 导出/引擎预览往返 E2E，覆盖关闭 HRI 后的严格预检恢复。
- [x] 为 HRI 模式控件增加稳定的可访问标签，避免“可读文字”与“预留高度”控件选择冲突。

#### 未完成 TODO

- [ ] R3 完整协议运行时矩阵、更多 HRI/静区组合、导入非法值提示和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的条码、文字、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] Designer 不自动猜测条码编码宽度；TSPL 固定尺寸仍需用户按内容、模块宽度、HRI 和静区确认，后端严格预检是最终边界。

## R38 本轮：Designer TSPL 条码制式

R38 已完成：Designer 条码制式选择器接入 TSPL `PLESSEY`、`ITF14`、`EAN14` 三个已由后端支持的新枚举值。选择 PLESSEY 或 EAN14 时，设计器按当前窄条点宽自动同步合法的 1:2 或 1:1 宽条；改变窄条或清空宽条输入不会生成后端必然拒绝的固定比例。PLESSEY、ITF-14、EAN-14 的选项能力由 Host 能力快照控制，旧制式和不支持协议继续保留严格禁用。Designer 单测 `2/2`、TSPL 页面交互 E2E `1/1`、`vue-tsc -b` 和 Vite 生产构建均通过；Host 因 NuGet 审计源不可访问需使用 `RestoreIgnoreFailedSources=true` 与 `NuGetAudit=false` 启动。真实打印机和扫描结果仍需外部验收。

### R38 分类后的 TODO

#### 已完成 TODO

- [x] Designer 条码制式下拉新增 TSPL `PLESSEY`、`ITF-14`、`EAN-14`，并遵循 Host 能力快照禁用不支持值。
- [x] Designer 对 PLESSEY/EAN14 自动维护 1:2/1:1 宽条比例，避免编辑器生成必然失败的模板。
- [x] 新增固定比例纯函数单测和真实页面切换、模板导出 E2E。

#### 未完成 TODO

- [ ] R3 HRI 的完整 Designer E2E、静区组合、完整目标运行时矩阵和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的条码、文字、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] 本轮只改善 Designer 创建入口；后端仍是最终能力和比例校验来源，导入外部非法模板不会被 UI 静默改写。

## R37 本轮：TSPL EAN14

R37 已完成：统一 `BarcodeType.Ean14`、TSPL 独立 `BarcodeEan14`、通用 `BARCODE "EAN14"` 映射、统一 Provider、模板度量和能力快照。TSC 手册将 EAN14 列为原生条码类型，输入上限为 13 位，且只允许 1:1 窄宽比，推荐窄条宽度为 8 点；本地严格接受 13 位 ASCII 数字 payload，按完整 GS1-128 符号预留占用宽度，最终校验位由设备完成。模板省略 `WideModuleWidth` 时也会将旧 `Ratio` 默认值收敛为 1:1。支持四方向、页面边界、元素路径、Profile 禁用、比例校验和失败恢复；CPCL、ESC/POS、ZPL 保持严格拒绝。定向回归 net6.0/netcoreapp3.1 各通过 `16/16`；Core 完整回归 net6.0、netcoreapp3.1、netcoreapp2.2 各 `2381/2381`；Core/Templates Release 五个目标框架构建 0 warnings、0 errors。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)；EAN-14 的 GS1-128 业务语义见 [GS1 System Architecture](https://www.gs1.org/standards/gs1-system-architecture-document/current-standard)。

### R37 分类后的 TODO

#### 已完成 TODO

- [x] TSPL `BarcodeEan14` 独立入口、通用 `EAN14` 映射、13 位 payload 校验、1:1 比例校验和失败恢复。
- [x] 统一 `BarcodeType.Ean14`、TSPL Provider、模板度量、能力快照、四方向原点/占用宽度、页面边界、元素路径和 Profile 禁用。
- [x] 模板省略 `WideModuleWidth` 时的旧 `Ratio` 兼容映射保持 EAN14 1:1，不把默认值错误转换为 2:5。
- [x] TSPL EAN14 golden bytes、通用入口、非法输入、比例边界、跨协议能力边界和模板能力/输出定向测试。

#### 未完成 TODO

- [ ] R3 HRI 的 Designer 端到端、静区组合、完整可用运行时矩阵和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的 EAN14、ITF14、PLESSEY、POSTNET、I2OF5G、GS1-128、FIM、IMb、PLANET、条高、模块宽、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] EAN14 的设备端校验位、GS1 AI `01` 的业务语义和实际条形外观由目标 TSPL 固件完成；本地保证 13 位 payload、1:1 命令参数、命令字节、布局边界和失败恢复，真实打印扫描结果仍需目标设备验收。

## R36 本轮：TSPL ITF14

R36 已完成：统一 `BarcodeType.Itf14`、TSPL 独立 `BarcodeItf14`、通用 `BARCODE "ITF14"` 映射、统一 Provider、模板度量和能力快照。TSC 手册规定 ITF14 接收 13 位数字 payload；本地严格校验输入并按完整 14 位交叉二五符号计算占用宽度，最终校验位由设备完成。支持四方向、页面边界、元素路径、Profile 禁用和失败恢复。ITF14 定向回归 net6.0/netcoreapp3.1 各通过 `15/15`；Core 完整回归 net6.0、netcoreapp3.1、netcoreapp2.2 各 `2365/2365`，Core/Templates 五个目标框架构建 0 warnings、0 errors。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。

### R36 分类后的 TODO

#### 已完成 TODO

- [x] TSPL `BarcodeItf14` 独立入口、通用 `ITF14` 映射、13 位 payload 校验和失败恢复。
- [x] 统一 `BarcodeType.Itf14`、TSPL Provider、模板度量、能力快照、四方向原点/占用宽度、页面边界、元素路径和 Profile 禁用。
- [x] TSPL ITF14 golden bytes、通用入口、非法输入、跨协议能力边界和模板能力/输出定向测试。

#### 未完成 TODO

- [ ] R3 HRI 的 Designer 端到端、静区组合、完整可用运行时矩阵和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的 ITF14、PLESSEY、POSTNET、I2OF5G、GS1-128、FIM、IMb、PLANET、条高、模块宽、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] ITF14 的设备端校验位和实际条形外观由目标 TSPL 固件完成；本地保证 13 位 payload、命令字节、布局边界和失败恢复，真实扫描结果仍需目标设备验收。


## R35 本轮：TSPL PLESSEY

R35 已完成：统一 `BarcodeType.Plessey`、TSPL 独立 `BarcodePlessey`、通用 `BARCODE "PLESSEY"` 映射、统一 Provider、模板度量和能力快照。PLESSEY 输入严格限制为非空 ASCII 数字，TSPL 命令固定窄条:宽条为 1:2；设备负责最终条形编码和校验。Provider 按 Plessey 起止、数据、CRC 和终止结构计算占用宽度，支持四方向、页面边界、元素路径、Profile 禁用和失败恢复。TSPL PLESSEY 定向回归 net6.0/netcoreapp3.1 各通过 `14/14`；Core 完整回归 net6.0、netcoreapp3.1、netcoreapp2.2 各 `2350/2350`，Core/Templates 五个目标框架构建 0 warnings、0 errors。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。

### R35 分类后的 TODO

#### 已完成 TODO

- [x] TSPL `BarcodePlessey` 独立入口、通用 `PLESSEY` 映射、数字校验和 1:2 窄宽比校验。
- [x] 统一 `BarcodeType.Plessey`、TSPL Provider、模板度量、能力快照、四方向原点/占用宽度、页面边界、元素路径、Profile 禁用和失败恢复。
- [x] TSPL PLESSEY golden bytes、通用入口、非法输入、跨协议能力边界和模板能力/输出定向测试。

#### 未完成 TODO

- [ ] R3 HRI 的 Designer 端到端、静区组合、完整可用运行时矩阵和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的 PLESSEY、POSTNET、I2OF5G、GS1-128、FIM、IMb、PLANET、条高、模块宽、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] PLESSEY 的设备端校验和实际条形外观由目标 TSPL 固件完成；本地保证统一输入、命令字节、布局边界和失败恢复，真实扫描结果仍需目标设备验收。

## R34 本轮：TSPL POSTNET（POST）

R34 已完成：TSPL 独立 `BarcodePostnet`、通用 `BARCODE "POST"` 映射、统一 Provider、能力快照和模板输出。TSC 手册规定 POSTNET 接收 5、9、11 位数据；主机移除连字符并严格校验数字和长度，最终校验位由 TSPL 设备生成。支持四方向、原点/占用宽度、页面边界、模板元素路径、Profile 禁用和失败恢复。TSPL/ZPL 定向及联合回归 net6.0/netcoreapp3.1 各 `61/61`，Core 完整回归 net6.0、netcoreapp3.1、netcoreapp2.2 各 `2336/2336`；Core/Templates 五目标框架生产构建 0 warnings、0 errors，`git diff --check` 无新增错误（仅既有 CRLF 提示）。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。

### R34 分类后的 TODO

#### 已完成 TODO

- [x] TSPL `BarcodePostnet` 独立入口、`POST` 原生映射、5/9/11 位数字校验和连字符归一化。
- [x] TSPL Provider、`PrintCapabilities`、模板能力快照、四方向原点、占用宽度、页边界、元素路径、Profile 禁用和失败恢复。
- [x] TSPL POSTNET golden bytes、通用 `POST` 入口、模板旋转、跨协议能力边界和 Core 多目标回归。

#### 未完成 TODO

- [ ] R3 HRI 的 Designer 端到端、静区组合、完整可用运行时矩阵和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。
- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。
- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备的 POSTNET、I2OF5G、GS1-128、FIM、IMb、PLANET、条高、模块宽、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。
- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

#### 不适用 TODO

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### 本轮接受的限制

- [x] TSPL `POST` 的最终校验位和条形外观由设备固件完成；本地只保证统一输入语义、命令字节、布局边界和失败恢复，真实扫描结果仍需目标设备验收。

## R33 本轮：TSPL I2OF5G（DPI/DPL）

R33 已完成：TSPL 独立 `BarcodeI2OF5G`、统一 Provider、能力快照和模板输出。TSC 手册将 `DPI`/`DPL` 分别定义为 11/13 位 Deutsche Post 内容；主机按统一 German Post 4/9 规则为 11/13 位输入生成校验位、为 12/14 位输入验证校验位，再按完整长度选择 `DPI`/`DPL` 并发送 11/13 位 payload。支持四方向、页边界、模板元素路径、Profile 禁用和失败恢复。专项及受影响回归 net6.0/netcoreapp3.1 各 `132/132`，Core 完整回归 net6.0、netcoreapp3.1、netcoreapp2.2 各 `2310/2310`；Core/Templates 五目标框架生产构建 0 warnings、0 errors，`git diff --check` 仅保留既有 CRLF 转换提示。规范依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。

### R33 分类后的 TODO

#### Completed

- [x] TSPL `BarcodeI2OF5G` 独立入口、`DPI`/`DPL` 原生映射、主机 4/9 校验位生成/验证和固定 payload 输出。
- [x] TSPL Provider、`PrintCapabilities`、模板能力快照、四方向、页边界、元素路径和 Profile 边界。
- [x] TSPL I2OF5G 精准回归、Provider/模板回归和跨协议能力边界回归。

#### Open Actionable

- [ ] R3 HRI 的 Designer 端到端、静区组合、完整可用运行时矩阵和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。

#### Blocked Approval

- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。

#### Blocked External

- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备上的 I2OF5G、GS1-128、FIM、IMb、PLANET、条高、模块宽、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。

#### Not Applicable

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### Accepted Limitations

- [x] TSPL `DPI`/`DPL` 由设备完成 Deutsche Post 条码最终编码；本轮只保证统一输入的 4/9 校验语义和固定 payload 映射，真实固件行为仍需验收。

#### Verified Boundaries

- [x] 11/13 位追加校验位、12/14 位既有校验位验证、非法长度/字符/校验位、N/R/I/B、模板路径、页边界、失败恢复和 Profile 禁用均有本地回归。

#### Deferred

- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

## R32 本轮：ZPL I2OF5G（German Post 4/9）

R32 已完成：ZPL 独立 `BarcodeI2OF5G`、统一 Provider、能力快照和模板输出。由于 ZPL `^B2` 只表达基础 Interleaved 2 of 5，主机按 CPCL German Post 4/9 规则为 11/13 位输入追加校验位、为 12/14 位输入验证校验位，再以关闭 ZPL Mod10 的 `^B2` 输出完整数据；支持四方向、页边界、模板元素路径、Profile 禁用和失败恢复。官方语义见 [ZPL 条码说明](https://docs.zebra.com/us/en/printers/software/zpl-pg/fonts-and-barcodes/barcodes.html)。I2OF5 相关精准测试 net6.0/netcoreapp3.1 各 `146/146`，ZPL 受影响测试各 `490/490`，Core 完整测试三种可用运行时各 `2285/2285`。

### R32 分类后的 TODO

#### Completed

- [x] ZPL `BarcodeI2OF5G` 独立入口、主机 4/9 校验位生成/验证和 `^B2` 输出。
- [x] ZPL Provider、`PrintCapabilities`、模板能力快照、四方向、页边界、元素路径和 Profile 边界。
- [x] I2OF5 精准回归及 ZPL 受影响协议回归。

#### Open Actionable

- [ ] R3 HRI 的 Designer 端到端、静区组合、完整可用运行时矩阵和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型；TSPL I2OF5G 仍需独立规范确认后实现。

#### Blocked Approval

- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。

#### Blocked External

- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备上的 I2OF5G、GS1-128、FIM、IMb、PLANET、条高、模块宽、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主、旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 和 NuGet `NU1900` 外部工具链问题。

#### Not Applicable

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### Accepted Limitations

- [x] ZPL `^B2` 不原生表达 German Post 4/9；本轮由主机生成/验证校验位后输出基础 ITF，关闭设备 Mod10。真实固件扫描结果仍需验收。

#### Verified Boundaries

- [x] 11/13 位追加校验位、12/14 位既有校验位验证、非法长度/字符/校验位、N/R/I/B、模板路径、页边界、失败恢复和 Profile 禁用均有本地回归。

#### Deferred

- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

## R31 本轮：TSPL GS1-128（UCC/EAN-128）

R31 已完成：TSPL 原生 `EAN128` 命令映射、独立 `BarcodeUccEan128`、统一 Provider、能力快照和模板输出。输入要求以数字应用标识开头，ASCII 29 分隔符转为 TSPL `!102` FNC1 控制码；TSPL `EAN128` 负责首个 FNC1，字面 `!` 严格拒绝以避免控制码歧义，支持四方向、页边界、模板元素路径和失败恢复。协议语义依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。定向测试 net6.0/netcoreapp3.1 各 `15/15`，UCC 相关回归各 `40/40`，Core 完整测试三种可用运行时各 `2274/2274`，生产五目标 TFM 构建 0 警告、0 错误。

### R31 分类后的 TODO

#### Completed

- [x] TSPL `BarcodeUccEan128` 独立入口、`EAN128` 映射、ASCII 29/FNC1 转码和严格输入校验。
- [x] TSPL Provider、`PrintCapabilities`、模板能力快照、四方向、页面边界和元素路径错误。
- [x] TSPL GS1-128 精准回归、Provider/模板回归和 profile 禁用边界已加入本轮验证。

#### Open Actionable

- [ ] R3 HRI 的 Designer 端到端、静区组合、完整可用运行时矩阵和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。

#### Blocked Approval

- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。

#### Blocked External

- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备上的 GS1-128、FIM、IMb、PLANET、条高、模块宽、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主缺少对应 .NET Runtime；旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 仍属外部工具链阻塞。
- [ ] NuGet 漏洞源 `NU1900` 当前不可访问。

#### Not Applicable

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### Accepted Limitations

- [x] TSPL EAN128 负责协议层首个 FNC1 和分隔符 FNC1 编码，不解析 GS1 AI、字段长度或业务校验；调用方需提供合法元素串。字面 `!` 作为控制码前缀被拒绝。

#### Verified Boundaries

- [x] `EAN128` 类型、ASCII 29 到 `!102`、非法字符拒绝、N/R/I/B 方向、模板路径、页边界、能力快照和 profile 禁用均有本地回归。

#### Deferred

- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

## R30 本轮：ZPL GS1-128（UCC/EAN-128）

R30 已完成：ZPL 独立 `BarcodeUccEan128`、统一 Provider、能力快照和模板输出。输入要求以数字应用标识开头，ASCII 29 分隔符转为 ZPL `>8` FNC1，字面 `>` 转为 `>0`；输出使用 `^BC` 的 `>;>8` 子集 C/FNC1 前缀，支持四方向、HRI、页边界、模板元素路径和失败恢复。Zebra 官方语义依据 [`^BC`](https://docs.zebra.com/us/en/printers/software/zpl-pg/c-zpl-zpl-commands/r-zpl-bc.html)。

### R30 分类后的 TODO

#### Completed

- [x] ZPL `BarcodeUccEan128` 独立命令、FNC1/分隔符转码、字面调用码转义、四方向和严格输入校验。
- [x] ZPL 统一 Provider、`PrintCapabilities`、模板能力快照、宽度/页边界和元素路径错误。
- [x] ZPL GS1-128 精准测试 net6.0/netcoreapp3.1 各 15/15；Core 完整测试三种可用运行时各 2260/2260；生产五目标 TFM 构建 0 警告、0 错误。

#### Open Actionable

- [ ] R3 HRI 的 Designer 端到端、静区组合、完整可用运行时矩阵和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合。
- [ ] 其他尚未映射的厂商邮政变体、GS1 变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。

#### Blocked Approval

- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。

#### Blocked External

- [ ] 真实 Zebra/TSPL/ESC/POS 热敏及针式设备上的 GS1-128、FIM、IMb、PLANET、条高、模块宽、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主缺少对应 .NET Runtime；旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 仍属外部工具链阻塞。
- [ ] NuGet 漏洞源 `NU1900` 当前不可访问。

#### Not Applicable

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### Accepted Limitations

- [ ] ZPL GS1-128 只负责协议层 FNC1/调用码编码，不解析 AI、校验位或业务字段长度；调用方按 GS1 规则提供数据。
- [ ] TSPL 对 `UccEan128` 继续严格拒绝；无可靠设备语义时不静默降级为普通 Code 128。

#### Verified Boundaries

- [x] ZPL `>;>8` 初始 FNC1、ASCII 29 后续 FNC1、字面 `>` 转义、非法字符拒绝、N/R/I/B 方向、模板元素路径和能力快照均有本地证据。

#### Deferred

- [ ] GS1 AI 自动解析/可读文本格式化、厂商私有 FNC1 规则、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

## R28 本轮：ESC/POS Raster Intelligent Mail/IMb

R28 已完成：ESC/POS Raster 主机按 USPS-B-3200 生成 65 根四状态 IMb，覆盖 20 位跟踪码及 0/5/9/11 位路由码组合、FCS、5-of-13/2-of-13 码字符、条形映射、连字符归一化、宽度、HRI、旋转、模板、静区、预算和失败恢复；Native 继续严格拒绝。IMb/能力矩阵精准测试 net6.0 95/95；Core 完整测试 net6.0、netcoreapp3.1、netcoreapp2.2 各 2238/2238。

### R28 分类后的 TODO

#### Completed

- [x] ESC/POS Raster Intelligent Mail/IMb 主机编码、四组 USPS-B-3200 官方样例、能力快照、模板接入、宽度/HRI/旋转/静区、预算回滚和失败恢复。

#### Open Actionable

- [ ] R3 HRI 的 Designer 端到端、静区组合、完整可用运行时矩阵和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合。
- [ ] FIM D/E、厂商邮政变体及其他尚未映射的条码，需要逐项确认规范、主机编码算法和统一语义后实现。

#### Blocked Approval

- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。

#### Blocked External

- [ ] 真实热敏/针式打印机的 IMb/FIM/PLANET/GS1-128 条高、模块间距、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主缺少对应 .NET Runtime；旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 仍属外部工具链阻塞。

#### Not Applicable

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### Accepted Limitations

- [ ] IMb 是 ESC/POS Raster 主机图形，不提供 ESC/POS Native 原生 IMb 命令；调用方仍需提供符合 USPS 业务规则的有效跟踪码和路由码。

#### Verified Boundaries

- [x] 20/25/29/31 位输入、路由组合、65 根条宽、连字符归一化、预算回滚、四角旋转和模板路径均有本地回归；官方 USPS 样例全部匹配。

#### Deferred

- [ ] USPS AI/MID/STID 业务字段解析、FIM D/E 和厂商变体、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

## R29 本轮：ESC/POS Raster FIM D/E

R29 已完成：统一 `BarcodeType.Fim` 输入从 A/B/C 扩展为 USPS A–E；新增 D=`111010111`、E=`101000101` 主机模式，接入 ESC/POS Raster 和模板度量/页边界/旋转/HRI/预算恢复，并同步放行 CPCL 模板校验。FIM 精准测试 net6.0/netcoreapp3.1 各 108/108；Core 完整测试三种可用运行时各 2246/2246；生产项目五 TFM 构建 0 警告、0 错误。

### R29 分类后的 TODO

#### Completed

- [x] FIM A/B/C/D/E 模式、USPS 九槽位图形、Raster/模板/CPCL 接入、四角旋转、HRI、页边界、预算回滚和失败恢复。

#### Open Actionable

- [ ] R3 HRI 的 Designer 端到端、静区组合、完整可用运行时矩阵和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合。
- [ ] 其他尚未映射的厂商邮政变体和条码类型，需要逐项确认规范、主机编码算法和统一语义后实现。

#### Blocked Approval

- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。

#### Blocked External

- [ ] 真实热敏/针式打印机的 FIM D/E、IMb、PLANET、GS1-128 条高、条宽、位置、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主缺少对应 .NET Runtime；旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 仍属外部工具链阻塞。

#### Not Applicable

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### Accepted Limitations

- [ ] FIM D/E 只表达 USPS 模式图形，不自动判断 IBI、PC Postage 或 Customized Services 的业务适用性；调用方需按 USPS 规则选择模式。
- [ ] ESC/POS Native 没有 FIM 原生命令，需显式选择 Raster。

#### Verified Boundaries

- [x] A–E 模式均为 9 槽位、固定模块间隙；D/E 官方模式、非法 F 输入、CPCL 模板和 Raster 旋转/预算重试均有回归。

#### Deferred

- [ ] USPS 业务场景自动选择、厂商 FIM 变体、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

R16 已将统一 `BarcodeType.IntelligentMail` 接入 ZPL `^BZ` 类型 3（USPS Intelligent Mail），支持 N/R/I/B 四方向、20/25/29/31 位数字、连字符归一化，完成 Provider/模板能力、严格校验、失败恢复、能力边界和双运行时专项回归；IMb 不含校验位，20 位跟踪码和最多 11 位路由码由调用方按 USPS 业务规则提供。

R17 已将统一 `BarcodeType.UccEan128` 接入 ESC/POS Native `GS k 74`（GS1-128/UCC-EAN-128），新增 `EscPosPrintCommand.BarcodeUccEan128`，支持 2–255 字节 7-bit ASCII、Epson 特殊字符/FNC1 保留、统一模板 Provider、严格边界、能力快照、失败恢复和双运行时回归；AI、FNC1、校验和固件业务语义由调用方按目标 Epson 手册提供。

R18 已将 ESC/POS Raster 主机条码从 Code 128 扩展到 Code39、Code93、EAN8、EAN13、UPC-A、UPC-E、Codabar 和基础 I2OF5；新增类型化画布宽度测量、旋转/HRI 复用、Raster Provider 与统一模板能力快照，并保留 GS1-128、I2OF5C/I2OF5G、MSI、POSTNET、PLANET、Intelligent Mail 等缺少可靠主机编码器的严格拒绝。

R19 已将统一 `BarcodeType.Code93` 接入 TSPL `BARCODE "93"`，新增 TSPL 能力集合、可打印 ASCII 校验、Provider/模板四角度输出、Profile 禁用边界、失败恢复和精确字节回归；Code93 的协议语义依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。

R20 已将统一 `BarcodeType.I2OF5`/`I2OF5C` 接入 TSPL `BARCODE "25"`/`"25C"`，新增数字校验、奇偶长度前导零归一化、Provider/模板四角度输出、Profile 禁用边界、失败恢复、能力边界和精确字节回归；25/25C 的类型与长度规则依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。`I2OF5G` 及其他未映射类型继续严格拒绝。

R21 已将统一 `BarcodeType.I2OF5C` 接入 ZPL `^B2` 的 `e=Y` 校验位路径，新增 `ZplPrintCommand.BarcodeI2OF5C`，偶数输入补前导 `0`、奇数输入保持，完成 Provider/模板能力、四方向输出、Profile 禁用、失败恢复、元素路径预检和跨协议边界；旧 `BarcodeI2OF5` 的参数与字节行为保持兼容。协议语义依据 [Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)，实际打印/扫描仍需目标设备验收。

R15 已将统一 `BarcodeType.Planet` 接入 ZPL `^B5`（PLANET），支持 N/R/I/B 四方向、12/14 位数字、校验位和连字符归一化，完成 Provider/模板能力、严格校验、失败恢复、能力边界、双运行时专项回归和包消费者验证。

R14 已将统一 `BarcodeType.Postnet` 接入 ZPL `^BZ`（POSTNET），支持 N/R/I/B 四方向、5/9/11 位数字和连字符归一化，完成 Provider/模板能力、严格校验、失败恢复、能力边界和双运行时专项回归。

R13 已将统一 `BarcodeType.Msi` 接入 TSPL `BARCODE "MSI"`，保持 1–13 位数字、无校验位语义，完成 Provider/模板四角度锚点、失败恢复、能力边界和双运行时专项回归；`MSIC` 等校验变体继续通过原始指令显式使用。

按既有 T1–T8、S1–S8 分组，旧的 16/16 本地门禁作为固定基线保留，不代表功能百分比或实际设备兼容。R1 图片旋转已独立关闭；R2 填充矩形已关闭；R3 已贯通 CPCL 显式 HRI、ESC/POS Native/Raster 能力描述、Raster 主机文字输出及全局字号配置；R4 已完成 TSPL/ZPL 原生二维码四角度、ESC/POS Raster 文字四角度，以及 ESC/POS Raster Code 128/Code39/Code93/EAN/UPC/Codabar/I2OF5/二维码四角度的统一模板、Provider、Host/Designer 能力链路；此前收紧了 CPCL 统一模型直连条码的 90/180 度边界并完成 Code39/Code93 映射；上一轮新增 ESC/POS Native EAN8、EAN13、UPC-A、UPC-E 的能力集合、GS `k` 65–68 映射、模板校验位规范化、输入校验和 golden-byte 回归；再上一轮新增 Native Codabar/I2OF5 的 GS `k` 71/70 映射、能力集合、模板预检、I2OF5 奇数长度补零和输入校验；前几轮新增 ZPL Code39/Code93 的 `^B3`/`^BA` 独立命令、统一 Provider、能力集合、四角度输出、可选校验位显示、模板预检和非法输入校验；上一轮新增 ZPL EAN8/EAN13/UPC-A/UPC-E 的 `^B8`/`^BE`/`^BU`/`^B9` 独立命令、统一 Provider、能力集合、零售校验位处理、UPC-E 展开、模板预检和非法输入校验；R8 新增 ZPL Codabar/I2OF5 的 `^BK`/`^B2` 独立命令、统一 Provider、能力集合、四角度输出、起止字符/偶数数字校验、模板预检、尺寸度量和 golden-byte 回归；R9 新增 ESC/POS Native 下划线的 `Underline` 独立命令、`ESC - n` 字节、能力快照、Provider 状态切换、模板 Native 预检和跨运行时回归；R10 新增 ESC/POS Native 反白的 `Reverse` 独立命令、`GS B n` 字节、能力快照、Provider 状态切换、模板 `Reverse` 预检/渲染、Designer 开关和跨运行时回归；R11 新增 ESC/POS Native I2OF5C/I2OF5G 的能力集合、3/1 与 4/9 校验位规范化、`GS k 70` 输出、模板预检、失败恢复和跨运行时回归；R12 新增 ZPL Native MSI `^BM` 独立命令、A-D 校验方案、四角度输出、统一 Provider/能力矩阵、模板预检、失败恢复和跨运行时回归，统一模型固定使用 A 方案无校验位以保持现有 MSI 语义，Raster/TSPL/ESC/POS 继续严格拒绝不支持的条码和样式。其他 ESC/POS 条码、旋转样式组合、组合 E2E、完整运行时矩阵和硬件验收仍是后续项。Previous Goal Turn：PROGRESS（R11 ESC/POS Native I2OF5C/I2OF5G 子范围已完成）。本轮 PROGRESS，No-Progress Check：CHANGED。

旧 S5 准入阶段台账保存于 `artifacts/t8-report/previous-progress-s5-admission.md`。历史 TRX、失败诊断、包和截图不覆盖；本文件只描述当前状态。需求对应与命令见 [当前验证报告](../../docs/multi-protocol-verification.md)。

## 当前分组

| ID | 能力 | 状态 |
| --- | --- | --- |
| T1 | 针式 ESC/POS Profile 贯通命令、Provider、模板、Host、设计器和保存 | CLOSED |
| T2 | 重采样高度、图片参数、追加前校验 | CLOSED |
| T3 | 语义字体与旧 CPCL 参数适配 | CLOSED |
| T4 | 未知字体样式静态预检 | CLOSED |
| T5 | 细粒度能力、Host 与设计器过滤 | CLOSED |
| T6 | 重复实例、组件与诊断归属一致性 | CLOSED |
| T7 | CPCL / Profile 横纵 DPI 契约 | CLOSED |
| T8 | 最终候选门禁与报告 | CLOSED：五运行时、Host/HTTP、UI 复用范围和六个隔离包消费者案例已确认；实机为独立外部门禁，R1 图片旋转另行跟踪 |
| S1 | 原生文字契约、Raster 覆盖/固定字体/替代、设备字体校准 | CLOSED |
| S2 | 标签 Profile、ESC/POS / TSPL / ZPL 切刀契约及独立文件 | CLOSED |
| S3 | Job 份数顺序、页/份/任务进纸裁切、单位及输出预算 | CLOSED |
| S4 | 统一静区、边界/相邻检查、画布回滚及设计器保存 | CLOSED |
| S5 | 引擎自有存储预算、取消恢复、Host 单请求预算与全局准入 | CLOSED，第三方边界公开保留 |
| S6 | 元素归属副本与图片数组快照 | CLOSED |
| S7 | 显式 Native 混合收据、游标、占位与模板接入 | CLOSED |
| S8 | Host/设计器版本特性协商、稳定诊断及严格固定选项 | CLOSED |
| R1 | 图片 0/90/180/270 度旋转、四 Provider、模板/设计器预览、预算与消费者验证 | CLOSED：实现、五运行时、Host/HTTP、前端、126 项浏览器、最终视觉 QA 和六消费者均已确认；T8 旧基线仍独立保留 |
| R2 | TSPL/ZPL 填充矩形、公开实心工厂、模板/设计器及严格边界 | CLOSED：五运行时各1555、Host108、前端235、浏览器130、桌面/移动 QA 与六个当前包消费者均通过 |
| R3 | 条码可读文字（HRI） | OPEN_ACTIONABLE：CPCL、TSPL、ZPL、ESC/POS Raster 的 Host/Designer 四协议端到端矩阵已覆盖；仍需完整目标运行时矩阵、更多静区/旋转组合和实机字高校准 |
| R4 | 文字、条码、二维码等其余元素旋转 | OPEN_ACTIONABLE：TSPL/ZPL 二维码、ESC/POS Raster 文字、Code 128 条码和二维码四角度及统一模板/Provider/独立 Raster 命令已实现；CPCL 直连条码 90/180 度已严格拒绝并保留 270 度，剩余组合 E2E 和外部验收仍待完成 |
| R5 | 更多条码类型与跨协议映射 | OPEN_ACTIONABLE：ESC/POS Code39/Code93/EAN/UPC/Codabar/I2OF5/I2OF5C/I2OF5G、TSPL Code93/I2OF5/I2OF5C/MSI/PLANET 及 ZPL Code39/Code93/EAN/UPC/Codabar/I2OF5/MSI/POSTNET/PLANET/Intelligent Mail 子范围 CLOSED；其他 ESC/POS 类型及其余 ZPL/TSPL 类型仍待实现 |
| R6 | 反白、粗体、下划线等文字样式 | OPEN_ACTIONABLE：ESC/POS Native 24 点顺序文字的粗体/单线下划线/反白及 ESC/POS Raster 旋转下划线、模板和 Designer 能力已接入；仍需 TSPL/ZPL 内置字体样式，或固化为严格边界 |

## 已补齐的旧开放项

- [x] S5 自有存储覆盖：命令动作与文本、CPCL 元数据、文档/页/元素/输出集合、模板布局、绑定属性索引、资源/图片/租约索引、页头索引、组件参数和清理临时快照均接入预算。
- [x] S5 预算归属与失败恢复：扩容计入新旧存储，未提交失败归还，结果存储保留至所属作用域结束；第三方内部 scratch、字形/解析缓存、GC 与框架缓冲不属于完整堆保证，见公开边界表。
- [x] S8 版本和 required features 协商、409 与旧 Host 提示；根/固定嵌套请求 DTO 未知字段拒绝，业务 data/resources 自由键保留。
- [x] S8 稳定 code/reason/phase/fieldPath、绑定归属与资源计数贯通 Host、API 和界面；500 不暴露服务器内部异常。
- [x] T8 DejaVu 2.37 常规/真实粗体/替代与 Noto 简体中文 fixture、来源和许可；Core 和 E2E 固定资源读入，中文样本保留。
- [x] T8 默认字体兼容测试显式保留，不用跳过代替。完整套件仍需具备覆盖样本的系统字库，固定资源不意味着所有环境默认输出完全相同。
- [x] T8 五运行时各 1469/1469，零失败/跳过，实际 testhost 目录确认。
- [x] T8 `tools/Verify-ProtocolRuntimes.ps1` 串行运行、180 秒窗口、独立证据目录与运行时检查，不下载或修改系统运行时。
- [x] T8 当前 Host 真实 HTTP 33 项，版本/严格字段/绑定/预算/准入与断开恢复通过，清理仅限本次已核实模块的实例。
- [x] T8 当前两个 NuGet 包的隔离消费者执行与资产选择确认：五个默认目标框架和一个显式 `netstandard2.1` 案例均 PASS；六份报告确认候选归档哈希一致。`net5.0` 默认选中 `netcoreapp3.1`，显式案例确认两个输出 DLL 匹配候选 `netstandard2.1` 资产。
- [x] T8 本地候选综合报告最终确认和状态收口：见 [当前验证报告](../../docs/multi-protocol-verification.md)；历史失败诊断保留，不被正式 PASS 证据覆盖。

## 当前证据

| 门禁 | 证据 |
| --- | --- |
| 五运行时完整 Core / Templates | `artifacts/t8-runtime-tests/20261005-111638-f523cd4f/runtime-verification.json` 和五个 TRX；各 1469/1469 |
| 全声明目标构建 | Solution 串行 build 通过，最新五处断言修复已经编译 |
| Host 完整 | `artifacts/t8-fixed-font-tests/designer-host-net8.trx`，98/98 |
| 最新实际 HTTP | `artifacts/t8-final-smoke.json`，33 项 PASS |
| 前端完整 / 生产构建 | T8 字体阶段 225/225 与 vue-tsc/Vite PASS，随后范围未变复用 |
| 完整 E2E / QA | 外部目录 `t8-fixed-font-full.json` 121/121；`t8-fixed-font-qa.json`、桌面/移动最终截图 |
| 本地包 | `artifacts/t8-fixed-font-pack`，两包均有五个生产 TFM；无字体文件 |
| 隔离包消费者 | 六份 `consumer-verification.json` 均 PASS，覆盖 `net7.0`、`net6.0`、`net5.0`、`netcoreapp3.1`、`netcoreapp2.2` 默认资产和 `net5.0` 显式 `netstandard2.1` 资产；六案例表及复现命令见 [当前验证报告](../../docs/multi-protocol-verification.md#当前包消费者证据) |
| S5 storage 阶段 | 两运行时各 1463；精准 242；`artifacts/s5-storage-smoke.json` PASS |
| S8 阶段 | 两运行时各 1393；Host 98；契约 29、绑定/图片 32；`artifacts/s8-contract-smoke.json` PASS |
| R1 五运行时完整 Core / Templates | 五个实际运行时各 1509 / 1509，零失败/跳过；`artifacts/r1-image-runtime-tests/20261005-121259-edb9a152/runtime-verification.json` |
| R1 Host 完整 | 103 / 103；`artifacts/r1-image-host-confirmed-tests/designer-host-net8.trx` |
| R1 实际 HTTP | 82 项 PASS；`artifacts/r1-image-smoke.json` |
| R1 前端单元与生产构建 | 233 / 233，vue-tsc/Vite PASS；来自本轮真实命令输出 |
| R1 完整浏览器与视觉 QA | `r1-image-full.json` 126 / 126（125 E2E + 1 QA）；`r1-image-qa-confirmed-run.json` 1 / 1、`r1-image-qa-confirmed.json` PASS；确认截图为 `r1-image-desktop-confirmed.png`、`r1-image-mobile-confirmed.png`，实际 URL `http://127.0.0.1:61081`，无错误/横向溢出且模板包重载字节一致 |
| R1 包与消费者 | 两包 `artifacts/r1-image-pack`；六个 `artifacts/r1-image-consumers-confirmed/**/consumer-verification.json` 全部 PASS 且候选归档一致；该包证据属于 R1，不代表 R2 最新候选 |
| R2 精准 / 五运行时 | 113/113，`artifacts/r2-filled-contract-confirmed-tests/r2-filled-contract-net6.trx`；`artifacts/r2-filled-runtime-tests/20261005-132414-8efb89e6/runtime-verification.json` 五实际运行时各1555/1555 |
| R2 Host / 前端 | `artifacts/r2-filled-host-tests/designer-host-net8.trx` 108/108；前端235/235与vue-tsc/Vite通过 |
| R2 浏览器 / 视觉 QA | 外部目录 `r2-filled-full-confirmed.json` 130/130，`r2-filled-qa.json` PASS和桌面/移动截图；URL61081，无错误/页面溢出，项目重载TSPL字节一致 |
| R2 当前包与消费者 | `artifacts/r2-filled-pack-confirmed/package-build-verification.json`确认十个DLL与当前Debug构建一致；`artifacts/r2-filled-consumers-confirmed`六消费者均PASS |
| R3 Core / Templates | net6 精准 HRI 15/15、完整1561/1561；netcoreapp3.1 完整1561/1561；默认 HRI 关闭字节兼容、Raster 预留高度严格拒绝、字号差异与 1–255 边界均有断言 |
| R3 Host / Designer | `DesignerHost.Tests` net8 109/109；Host 能力响应暴露 Raster HRI 字号，前端 Vitest 237/237、vue-tsc/Vite 构建通过，项目校验覆盖 Raster 范围与 Native 作用域 |
| R3 包与消费者 | `artifacts/r3-hri-size-pack` 两包 Debug 打包通过；`artifacts/r3-hri-size-consumers/consumer-net6.0` 与 `consumer-netcoreapp3.1` 均 PASS |
| R4/R5/R6/R7/R8/R9/R10/R11/R12 Core / Templates | 当前完整测试 net6 与 netcoreapp3.1 各 1868/1868；本轮 ZPL MSI 专项各 35/35，R11 ESC/POS Native I2OF5C/I2OF5G 专项各 16/16、联合相关回归各 216/216，反白精准 74/74、下划线精准 74/74，上一轮 ZPL Codabar/I2OF5 专项 43/43、ZPL 零售专项 47/47、ZPL Code93 精准 32/32、Code39 精准 24/24；更早联合协议能力/模板能力回归各 95/95、EAN/UPC 精准 21/21、Code93 ESC/POS 精准 6/6、Code39 精准 8/8；CPCL 角度精准 ProtocolRotation 69/69、Raster 码图旋转双运行时 18/18、模板 Raster 码图/QR 双运行时 3/3；ZPL `^BM`/`^BK`/`^B2`/`^B8`/`^BE`/`^BU`/`^B9`/`^BA`/`^B3` N/R/I/B 字节、MSI A-D 方案与长度校验、零售/Code93/Code39/Codabar/I2OF5 字符校验、ESC/POS Native I2OF5C/I2OF5G 3/1/4/9 校验、下划线/反白状态切换、能力边界、设备 Profile 禁用和模板预检均通过 |
| R4/R5 Host / Designer | `DesignerHost.Tests` net8 109/109；能力响应暴露 `qrCodeRotations` 与 Raster 码图四角度；本轮公共能力变更后 Host 仍 109/109；文字旋转 Designer E2E 1/1；前端 Vitest 238/238，vue-tsc/Vite 构建通过（前端范围复用未变证据） |
| R5 包与消费者 | 当前公共 API 候选 `artifacts/r5-raster-code-pack` 两包 Debug 打包通过；`artifacts/r5-raster-code-consumers-final/consumer-net6.0` 与 `consumer-netcoreapp3.1` 均 PASS；R4/R3 旧候选证据仍保留 |
| R14 包与消费者 | `artifacts/r14-postnet-pack` 两包 Debug 打包通过；`artifacts/r14-postnet-consumers/consumer-net6.0` 与 `consumer-netcoreapp3.1` 均 PASS；保留 `NU5104`/缺少 README 和 EOL TFM 警告，其他三个历史消费者 TFM 未在本轮重跑 |
| R15 PLANET | ZPL PLANET 专项 net6.0/netcoreapp3.1 各 41/41；Core 完整各 1964/1964；DesignerHost 109/109；Core/Templates 五 TFM 构建 0 错误；候选包 `artifacts/r15-planet-pack` 打包通过，net6.0/netcoreapp3.1 消费者在 `artifacts/r15-planet-consumers` PASS |
| R16 Intelligent Mail | ZPL IMb 专项 net6.0/netcoreapp3.1 各 52/52；Core 完整各 2016/2016；DesignerHost 109/109；前端 238/238，vue-tsc/Vite 构建通过；Core/Templates 五 TFM 构建 0 错误；候选包 `artifacts/r16-imb-pack` 打包通过，net6.0/netcoreapp3.1 消费者在 `artifacts/r16-imb-consumers` PASS |
| R17 UCC/EAN-128 | ESC/POS Native 专项 net6.0/netcoreapp3.1 各 13/13；相关 ESC/POS/模板基线双运行时各 109/109；Core 完整各 2028/2028；DesignerHost 109/109；Core/Templates 五 TFM 构建 0 错误；候选包 `artifacts/r17-ucc-pack` 打包通过，net6.0/netcoreapp3.1 消费者在 `artifacts/r17-ucc-consumers` PASS |
| R18 ESC/POS Raster 多条码 | 新增主机 Code39/Code93/EAN8/EAN13/UPC-A/UPC-E/Codabar/I2OF5 编码、类型化宽度测量、Provider/模板能力集合、旋转/HRI 路径；专项 net6.0/netcoreapp3.1 各 15/15，相关既有回归各 176/176，Core 完整各 2043/2043，DesignerHost 109/109，Core/Templates L0 五 TFM 构建通过；候选包 `artifacts/r18-raster-barcode-pack` 和 net6.0/netcoreapp3.1 消费者 `artifacts/r18-raster-barcode-consumers` 均 PASS |
| R19 TSPL Code93 | TSPL `BARCODE "93"` 命令映射、可打印 ASCII 校验、Provider/模板四角度、Profile 禁用及失败恢复；专项 net6.0/netcoreapp3.1 各 22/22，Core 完整各 2069/2069，DesignerHost 109/109，Core/Templates L0 五 TFM 构建 0 警告/错误；候选包 `artifacts/r19-tspl-code93-pack` 和 net6.0/netcoreapp3.1 消费者 `artifacts/r19-tspl-code93-consumers` 均 PASS。保留 NU1900、NU5104、包 README 和消费者 EOL TFM 警告 |
| R20 TSPL I2OF5/I2OF5C | TSPL `BARCODE "25"`/`"25C"` 映射、数字校验、奇偶长度归一化、Provider/模板四角度、Profile 禁用及失败恢复；专项 net6.0/netcoreapp3.1 各 34/34，联合回归各 126/126；协议规则依据 TSC TSPL/TSPL2 手册。 |

UI 证据目录：`C:/Users/jianx/.codex/visualizations/2026/10/03/01a10405-cc4c-7f91-97a5-bc7cdf4c823c`。完整 E2E 的 S5 storage Host 与 T8 相关程序集相同，生产与前端未变范围复用；最终 QA 使用 T8 Host。诊断过渡截图不作为最终视觉证据。

首次 net7.0 完整运行 1464 通过/5 失败，`artifacts/t8-fixed-font-tests/fonts-core-final-net7.trx` 保留为诊断。根因是同值枚举别名的运行时名称选择不同；修复仅让五个诊断断言按对应协议值检查，字节和生产 API 未变。正式通过以新的五运行时目录为准。

Changed Scope：R20 TSPL `BARCODE "25"`/`"25C"` 映射、数字输入校验、奇偶长度前导零归一化、Provider/模板四角度锚点、Profile 能力边界、失败恢复、回归测试及协议/模板说明；R19 Code93、R18 Raster 多条码、R17 UCC/EAN-128、R16 IMb、R15 PLANET、R14 POSTNET、R13 TSPL MSI、R12 ZPL MSI 及此前协议能力保持兼容。RiskLevel：MEDIUM。未改 TFM、依赖、版本、包图、Benchmark Harness；本轮完成 Core/Templates L1/L2，L0/Host/包消费者将在验证阶段记录。保留现有 NU1900、NU5104、包 README 和消费者 EOL 框架提示。

## 分类后的剩余 TODO

### Completed

- [x] T1–T8、S1–S8 的旧本地门禁保持 CLOSED，旧 16/16 计数作为固定基线保留。
- [x] R1 图片旋转的模型、Provider、模板、Host、设计器、四角度字节输出、预算/取消、126 项浏览器自动化、最终视觉 QA 和六个包消费者案例均已完成；初始过渡截图保留为诊断证据。
- [x] R2 填充矩形：TSPL BAR/ZPL GB 原生命令、公开 CreateFilled 精确实心工厂、旧 CPCL IL 兼容、模板与设计器、静态尺寸预检及分页/静区/预算恢复均完成；当前独立门禁闭环。
- [x] R4 旋转增量：TSPL/ZPL 二维码四角度和 ESC/POS Raster 文字四角度已接入统一模型、Provider、模板能力、Host/Designer；固定字体像素、页边界、失败角度和双运行时回归通过。该增量的 Raster 条码/二维码子范围由本轮关闭。
- [x] R5 ESC/POS Raster 码图旋转：Code 128 和二维码按完整渲染位图支持 0/90/180/270 度，条码可读文字预留区随动；新增 `RasterBarcode`/`RasterQrCode` 独立坐标入口，统一能力、页边界、模板渲染、静区占位和 GS v 0 输出均已覆盖。精准测试 258/258，码图旋转双运行时 18/18，模板 Raster 条码/QR 双运行时 3/3。
- [x] R4 Raster 文字边界增量：旋转文字的模板行高按实际墨迹（含下划线）预留；静区校验按 0/90/180/270 度紧致墨迹框计算并覆盖元素先后和贴边冲突；独立 `EscPosPrintCommand.Text` 增加字号、样式和角度重载，旧三参数调用保持兼容；Designer 旋转预览 E2E 1/1 通过。
- [x] R4 CPCL 统一模型直连角度边界：`PrintDocument` 直连 CPCL 条码对 90/180 度抛出带元素路径的能力错误，0/270 保持输出；模板适配层继续将顺时针 270 度转换为原生 VB，旧 `CPCLPrintCommand` 原始 API 不变。ProtocolRotation 精准 69/69；当前 Core/Templates 双运行时完整各 1619/1619（该轮记录时为 1611/1611）。
- [x] R5 Code39 统一条码子范围：ESC/POS Native 的 `PrintCapabilities`、模板预检和 Provider 映射 Code39 到 GS `k` 制式 69；TSPL 统一模型映射到 `BARCODE "39"`；独立命令已有校验继续复用。Native/TSPL 通过 golden-byte 与非法字符测试，Raster/ZPL 能力查询和模板输出严格拒绝；Code39 精准 8/8、协议能力 64/64、模板能力 16/16，Core/Templates net6 与 netcoreapp3.1 各 1619/1619，Host 109/109。
- [x] R5 Code93 统一条码子范围：ESC/POS Native 的 `PrintCapabilities`、模板预检和 Provider 映射 Code93 到 GS `k` 制式 72；独立命令已有 7-bit ASCII 校验继续复用。Native 通过 golden-byte、非 ASCII 输入和模板路径测试，Raster/ZPL 能力查询和模板输出严格拒绝；Code93 精准 6/6，联合协议能力/模板能力回归 86/86，Core/Templates net6 与 netcoreapp3.1 各 1625/1625，Host 109/109。
- [x] R5 EAN/UPC 统一零售条码子范围：ESC/POS Native 的 `PrintCapabilities`、模板预检和 Provider 分别映射 EAN8/EAN13/UPC-A/UPC-E 到 GS `k` 制式 68/67/65/66；模板层复用既有数字校验、缺少校验位补齐和错误校验位拒绝。Native 通过 golden-byte、规范化输入、非法数据和模板路径测试，Raster 严格拒绝；零售码精准 21/21，联合协议能力/模板能力回归 101/101，Core/Templates net6 与 netcoreapp3.1 各 1646/1646，Host 109/109。
- [x] R5 Codabar/I2OF5 统一条码子范围：ESC/POS Native 的 `PrintCapabilities`、模板预检和 Provider 分别映射 Codabar/I2OF5 到 GS `k` 制式 71/70；独立命令复用 Codabar A–D 守卫字符、允许字符和 I2OF5 偶数长度校验，模板层对奇数 I2OF5 输入按既有度量语义前缀补 `0`。Native 通过 golden-byte、非法数据、模板路径和能力边界测试，Raster 严格拒绝；精准 15/15，联合协议能力/模板能力回归 95/95，Core/Templates net6 与 netcoreapp3.1 各 1661/1661，Host 109/109。
- [x] R11 ESC/POS Native I2OF5C/I2OF5G 统一条码子范围：`PrintCapabilities`、模板预检和 Provider 接受 I2OF5C/I2OF5G；I2OF5C 按 3/1 规则在主机端补齐偶数数据并追加 Mod10 校验位，I2OF5G 按 4/9 规则对 11/13 位追加校验位、对 12/14 位校验既有末位，再统一输出 ESC/POS `GS k 70`。Native 通过 golden-byte、非法字符/长度/校验位、模板路径、失败恢复和能力边界测试，Raster/TSPL/ZPL 严格拒绝；专项 net6 与 netcoreapp3.1 各 16/16，联合相关回归各 216/216，完整 Core/Templates 各 1833/1833，Host 109/109。依据 CPCL/ITF 既有校验语义实现，不宣称各固件对 I2OF5G 的兼容一致性。
- [x] R12 ZPL Native MSI 统一条码子范围：`ZplPrintCommand.BarcodeMsi` 输出原生 `^BM`，支持 N/R/I/B 四方向、A-D 校验方案、可读文字标志、模块/高度边界及失败恢复；统一 Provider 与 `PrintCapabilities` 暴露 `BarcodeType.Msi`，按现有 MSI 无校验位语义固定输出 A 方案，模板四方向和元素路径预检已接入，TSPL/ESC/POS Native/Raster 严格拒绝。专项 net6.0 与 netcoreapp3.1 各 35/35，Core/Templates 完整各 1868/1868，DesignerHost 109/109，Core/Templates 五 TFM L0 构建 0 错误；其他 ESC/POS 条码、旋转下划线组合、完整运行时矩阵和实机验收仍保留为后续 TODO。直接命令的 A-D 方案需按目标 Zebra 固件手册和实机扫描结果确认。
- [x] R13 TSPL Native MSI 统一条码子范围：TSPL `BARCODE` 类型映射为 `MSI`，统一能力矩阵、1–13 位数字校验、Provider 页面封装、四角度锚点、模板布局轨迹、失败恢复和 ESC/POS/ZPL 边界均已覆盖；专项 net6.0 与 netcoreapp3.1 各 21/21，Core/Templates 完整各 1888/1888，Core/Templates 五 TFM L0 构建 0 错误。`MSIC` 校验变体仍保留为显式原始指令扩展，最终扫描结果需按 TSC 固件手册与实机确认。
- [x] R14 ZPL Native POSTNET 统一条码子范围：`ZplPrintCommand.BarcodePostnet` 输出原生 `^BZ`，支持 N/R/I/B 四方向、5/9/11 位数字、连字符归一化、模块/高度边界及失败恢复；统一 Provider 与 `PrintCapabilities` 暴露 `BarcodeType.Postnet`，模板四方向、元素路径预检、ZPL Profile 禁用和 TSPL/ESC/POS/CPCL 边界均已覆盖。专项 net6.0 与 netcoreapp3.1 各 35/35，Core/Templates 完整各 1923/1923，DesignerHost 109/109，Core/Templates 五 TFM L0 构建 0 错误。PLANET 已在 R15 另行接入，Intelligent Mail/IMB 等变体仍保留为后续项，实际扫描结果需按目标 Zebra 固件手册和实机确认。
- [x] R15 ZPL Native PLANET 统一条码子范围：`ZplPrintCommand.BarcodePlanet` 输出原生 `^B5`，支持 N/R/I/B 四方向、12/14 位 PLANET 数字、校验和连字符归一化、输入/尺寸边界及失败恢复；统一 Provider 与 `PrintCapabilities` 暴露 `BarcodeType.Planet`，模板四方向、元素路径预检、ZPL Profile 禁用和 TSPL/ESC/POS/CPCL 严格边界均已覆盖。专项 net6.0 与 netcoreapp3.1 各 41/41，Core 完整各 1964/1964，DesignerHost 109/109，Core/Templates 五 TFM L0 构建 0 错误；候选包和 net6.0/netcoreapp3.1 消费者均 PASS。依据 [Zebra Programming Guide](https://cpws.zebra.com/cpws/docs/zpl/zpl_manual.pdf) 与 [USPS PLANET 说明](https://about.usps.com/postal-bulletin/2007/html/pb22214/cc.htm)，实际扫描结果需按目标 Zebra 固件手册和实机确认。
- [x] R16 ZPL Native Intelligent Mail 统一条码子范围：`ZplPrintCommand.BarcodeIntelligentMail` 输出原生 `^BZ` 类型 3，支持 N/R/I/B 四方向、20/25/29/31 位 IMb 数字、连字符归一化、输入/尺寸边界及失败恢复；统一 Provider 与 `PrintCapabilities` 暴露 `BarcodeType.IntelligentMail`，模板四方向、元素路径预检、ZPL Profile 禁用和 TSPL/ESC/POS/CPCL 严格边界均已覆盖。专项 net6.0 与 netcoreapp3.1 各 52/52，Core/Templates 五 TFM L0 生产构建 0 错误；依据 [Zebra Programming Guide](https://cpws.zebra.com/cpws/docs/zpl/zpl-zbi2-pm-en.pdf) 与 [USPS Barcode Standards](https://pe.usps.com/text/dmm300/204.htm)，实际扫描结果需按目标 Zebra 固件手册和实机确认。
- [x] R17 ESC/POS Native UCC/EAN-128 统一条码子范围：`EscPosPrintCommand.BarcodeUccEan128` 输出 Function B `GS k 74`，支持 2–255 字节 7-bit ASCII、Epson 特殊字符/FNC1 数据保留、严格长度和失败恢复；统一 Provider 与 `PrintCapabilities` 暴露 `BarcodeType.UccEan128`，模板 Native 映射、能力快照、Raster/TSPL/ZPL 边界及模板预检均已覆盖。专项 net6.0 与 netcoreapp3.1 各 13/13，相关基线各 109/109，Core 完整各 2028/2028，DesignerHost 109/109，Core/Templates 五 TFM L0 构建 0 错误；候选包与两个隔离消费者均 PASS。依据 [Epson GS k 参考](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_lk.html)，实际业务校验与扫描结果需按目标固件手册和实机确认。
- [x] R18 ESC/POS Raster 主机条码扩展：`EscPosRasterCanvas` 新增类型化编码和宽度测量，Provider/模板能力集合接入 Code39、Code93、EAN8、EAN13、UPC-A、UPC-E、Codabar、基础 I2OF5，`EscPosPrintCommand.RasterBarcode(BarcodeType, ...)` 提供独立坐标入口；保留 GS1-128、I2OF5C/I2OF5G、MSI、POSTNET、PLANET、Intelligent Mail 的严格拒绝。专项 net6.0/netcoreapp3.1 各 15/15，相关回归各 176/176，Core 完整各 2043/2043，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 错误；候选包 `artifacts/r18-raster-barcode-pack` 打包通过，net6.0/netcoreapp3.1 消费者 `artifacts/r18-raster-barcode-consumers` 均 PASS。
- [x] R19 TSPL Code93 原生条码扩展：`TsplPrintCommand` 接入 `BARCODE "93"` 的可打印 ASCII 校验，Provider/模板能力集合支持 Code93 四角度与 HRI，Profile 可禁用原生条码且失败不污染输出；专项 net6.0/netcoreapp3.1 各 22/22，Core 完整各 2069/2069，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 警告/错误；候选包 `artifacts/r19-tspl-code93-pack` 打包通过，net6.0/netcoreapp3.1 消费者 `artifacts/r19-tspl-code93-consumers` 均 PASS。
- [x] R20 TSPL I2OF5/I2OF5C 原生条码扩展：`TsplPrintCommand` 接入 `BARCODE "25"`/`"25C"` 数字校验与奇偶长度前导零归一化，Provider/模板能力集合支持两类条码四角度、Profile 禁用、失败恢复和跨协议边界；`I2OF5G` 继续严格拒绝。专项 net6.0/netcoreapp3.1 各 34/34，联合回归各 126/126；规则依据 [TSC TSPL/TSPL2 Programming Manual](https://fs.tscprinters.com/system/files/31-0000001-00_tspl_tspl2_programming_g.pdf)。
- [x] R21 ZPL I2OF5C 原生条码扩展：`ZplPrintCommand.BarcodeI2OF5C` 接入 `^B2` 的 `e=Y` Mod10 路径，Provider/模板能力集合支持偶数输入补前导 `0`、奇数输入保持、四方向、Profile 禁用、失败恢复和跨协议边界；旧 `BarcodeI2OF5` 入口行为保持兼容。规则依据 [Zebra ZPL II Programming Guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf)。
- [x] R5 ZPL Code39 统一条码子范围：`ZplPrintCommand.Barcode39` 输出原生 `^B3o,e,h,f,g`，统一 Provider 与 `PrintCapabilities` 暴露 Code39，模板层复用基础 Code39 字符集和四角度能力；非法字符、模块/高度边界、失败恢复、设备 Profile 禁用和模板元素路径均已覆盖。ZPL 精准 24/24，Core/Templates net6 与 netcoreapp3.1 完整各 1685/1685，DesignerHost 109/109，解决方案五 TFM 构建 0 错误。ZPL EAN/UPC、Codabar、I2OF5 等仍未接入统一 Provider。
- [x] R6 ZPL Code93 统一条码子范围：`ZplPrintCommand.Barcode93` 输出原生 `^BAo,h,f,g,e`，统一 Provider 与 `PrintCapabilities` 暴露 Code93；支持 N/R/I/B 四方向、可读文字和显式校验位显示，严格校验可打印 ASCII、模块/高度、Profile 能力和失败恢复，模板四角度与元素路径均已覆盖。专项 32/32，Core/Templates net6 与 netcoreapp3.1 完整各 1717/1717，DesignerHost 109/109，解决方案五 TFM 构建 0 错误。ZPL EAN/UPC、Codabar、I2OF5 等仍保留为后续项。
- [x] R7 ZPL EAN/UPC 统一零售条码子范围：`ZplPrintCommand.BarcodeEan8`/`BarcodeEan13`/`BarcodeUpcA`/`BarcodeUpcE` 输出原生 `^B8`/`^BE`/`^BU`/`^B9`，统一 Provider 与 `PrintCapabilities` 暴露四类零售码；支持 N/R/I/B 四方向、7/8、12/13、11/12 位校验位规范化、UPC-E 6/7 位压缩数据展开、模块/高度、Profile 能力、失败恢复和模板路径预检。专项 47/47，Core/Templates net6 与 netcoreapp3.1 完整各 1764/1764，DesignerHost 109/109，解决方案五 TFM 串行构建 0 错误。ZPL Codabar/I2OF5 及 I2OF5C/I2OF5G 保留为后续项。
- [x] R8 ZPL Codabar/I2OF5 统一条码子范围：`ZplPrintCommand.BarcodeCodabar`/`BarcodeI2OF5`（含 `BarcodeI2of5` 别名和旋转重载）输出原生 `^BK`/`^B2`，统一 Provider 与 `PrintCapabilities` 暴露两类条码；支持 N/R/I/B 四方向、Codabar A–D 起止字符拆分、I2OF5 偶数数字和可选 Mod10 标志、模块/高度、Profile 能力、失败恢复、模板路径预检和协议宽度度量。专项 43/43，Core/Templates net6 与 netcoreapp3.1 完整各 1807/1807，DesignerHost 109/109，解决方案五 TFM 串行构建 0 错误。I2OF5C/I2OF5G 和其他 ESC/POS 条码仍保留为后续项。
- [x] R9 ESC/POS Native 文字下划线子范围：`EscPosPrintCommand.Underline` 输出 `ESC - n` 的启用/关闭字节；Native `PrintCapabilities` 和模板能力快照暴露下划线，Provider 在每段 24 点顺序文字前后显式切换状态，模板 `FontStyle=4` 在预检阶段通过并保持 Raster、TSPL、ZPL 的既有边界。精准回归 net6.0 与 netcoreapp3.1 各 73/73，Core/Templates 完整各 1811/1811，DesignerHost 109/109，Core/Templates 五 TFM L0 构建 0 错误；反白、旋转下划线和 TSPL/ZPL 内置字体样式仍保留为后续项。
- [x] R10 ESC/POS Native 文字反白子范围：`EscPosPrintCommand.Reverse` 输出 `GS B n` 的启用/关闭字节；Native `PrintCapabilities` 和模板能力快照暴露反白，Provider 在每段文字前后显式切换状态，模板 `BpFontElement.Reverse=true` 通过预检并写入统一模型，Designer 增加能力受控开关；Raster、TSPL、ZPL 继续在预检阶段严格拒绝。精准回归 net6.0 与 netcoreapp3.1 各 74/74，Core/Templates 完整各 1817/1817，DesignerHost 109/109，前端精准 46/46、vue-tsc/Vite 构建通过，Core/Templates 五 TFM L0 构建 0 错误；旋转下划线组合和 TSPL/ZPL 内置字体样式仍保留为后续项。

### Open Actionable

R51 已将 TSPL 统一条码集合扩展为 Code128、Code39、Code93、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、I2OF5C、MSI、PLANET、PLESSEY、ITF14、EAN14、Code11、China Post、Telepen、Telepenn、Code49；Code 11、China Post、两种 Telepen 和 Code 49 的输入、比例/设备校验语义、宽度预留和页面边界由 Provider 严格执行，DPI/DPL 等仍拒绝。

- [ ] R3 条码可读文字（HRI）：CPCL 与 ESC/POS Native/Raster 输出、Raster 全局字号（1–255，默认 12）、Host/设计器能力和项目往返已接入；待设计器端到端/完整目标运行时矩阵、静区联合校验覆盖和实机字高校准。
- [ ] R4/R5 收口：ESC/POS Raster 文字、Code 128/Code39/Code93/EAN/UPC/Codabar/I2OF5 条码和二维码四角度及 CPCL 直连条码角度边界已完成；仍需剩余锚点/占用尺寸/换行分页组合、完整设计器端到端覆盖和外部设备方向验证。Native 不可表达时必须显式转 Raster 或严格报错。
- [ ] R5 更多条码类型：ESC/POS Native Code39、Code93、EAN/UPC、Codabar/I2OF5/I2OF5C/I2OF5G/UCC-EAN-128、ESC/POS Raster Code39、Code93、EAN/UPC、Codabar/I2OF5、TSPL I2OF5/I2OF5C/MSI/PLANET/PLESSEY/ITF14/EAN14/Code11/China Post/Telepen/Telepenn/Code49 及 ZPL Code39/Code93/EAN/UPC/Codabar/I2OF5/MSI/POSTNET/PLANET/Intelligent Mail 子范围已完成；仍需评估其他 ESC/POS 类型及剩余 ZPL/TSPL 类型（DPI/DPL），并补齐统一语义、各协议映射、编码校验、尺寸度量、扫码静区和 golden-byte 覆盖，不能只扩展枚举而缺少 Provider 实现。
- [ ] R6 文字样式：ESC/POS Native 粗体、单线下划线和反白及模板/Designer 能力已完成；仍需旋转下划线组合、TSPL/ZPL 内置字体样式，无法原生表达时保持显式 Raster 配置，禁止静默降级。

### Blocked Approval

- [ ] 正式性能、容量和超时阈值：需要项目负责人批准工作负载、预算、超时和发布回归标准；当前测试只证明已声明边界，不替代阈值审批。

### Blocked External

- [ ] 真实热敏/针式机验收：型号、固件、实际协议、代码页、DPI、纸宽、字距、打印与扫描结果、进纸和裁切；按 `docs/protocols.md` 实机清单执行。
- [ ] 条件项：针式设备若是 ESC/P、ESC/P2 或私有协议，取得手册/样本后另建 Provider；不能从 ESC/POS Profile 推断兼容。

### Not Applicable

- NOT_APPLICABLE：连接管理、端口、驱动安装和设备发现不属于本组件的字节生成范围；由发送方或设备集成层负责。

### Accepted Limitations

R15 ZPL PLANET 统一语义为 `^B5` 的 12/14 位 ASCII 数字，要求包含使总和对 10 取模为 0 的校验位，允许连字符分组并在编码前归一化，支持 N/R/I/B 四方向；R16 已将 Intelligent Mail/IMb 接入 ZPL `^BZ` 类型 3，接受 20/25/29/31 位数字且不追加校验位，20 位跟踪码和最多 11 位路由码由调用方按 USPS 业务规则提供。R14 ZPL POSTNET 统一语义为 `^BZ` 的 5/9/11 位数字。R13 TSPL MSI 统一语义为 `BARCODE "MSI"` 的 1–13 位数字且不追加校验位；`MSIC` 等固件扩展不纳入统一能力矩阵，调用方必须显式使用原始 TSPL 指令并按目标 TSC 手册确认。TSPL 其他未映射制式、ESC/POS Raster 的 Native-only 条码和不支持的样式仍严格拒绝。

当前统一 ESC/POS、TSPL 与 ZPL 的模板条码集合仍有限；ESC/POS Native 已增加 Code39、Code93、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、I2OF5C、I2OF5G、UCC/EAN-128，TSPL 已支持 Code128、Code39、Code93、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、I2OF5C、I2OF5G、MSI、POSTNET、PLANET、PLESSEY、ITF14、EAN14、Code11、China Post，ZPL 已增加 Code39、Code93、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、I2OF5C、MSI、POSTNET、PLANET、Intelligent Mail；ESC/POS Raster 已支持主机 Code39/Code93/EAN/UPC/Codabar/I2OF5，仍严格拒绝 GS1-128、I2OF5C/I2OF5G 和邮政码等缺少可靠主机编码器的类型。ESC/POS GS1-128 使用 `GS k 74`，独立入口接受 2–255 字节 7-bit ASCII，统一模板保留数字应用标识开头语义，AI、FNC1、校验和固件业务规则由调用方负责。ESC/POS I2OF5C/I2OF5G 在 Provider 内按 3/1、4/9 规则完成主机校验位规范化后统一使用 `GS k 70`，不代表所有固件对 I2OF5G 的处理一致。TSPL CPOST 使用原生 `BARCODE "CPOST"`，只接受非空 ASCII 数字并固定 1:1 宽条；校验和、最终符号编码及固件可用性由目标设备负责。ZPL MSI 统一模型固定使用 `^BMA` 无校验位以保持现有 `BarcodeType.Msi` 语义，直接命令可显式选择 A-D 方案；最终校验位显示、算法和扫描结果仍需按目标固件手册确认。ZPL UPC-E 仅接受号码系统 0 的统一 6/7 位压缩数据，并在 Provider 内展开为 `^B9` 所需的 10 位字段；ZPL Codabar 要求 A–D 起止字符并拆分为 `^BK` 参数，ZPL I2OF5/I2OF5C 分别要求偶数位数字和 `e=Y` 校验位路径，其中 I2OF5C 偶数输入补前导 `0`。ZPL POSTNET、PLANET 和 Intelligent Mail 分别使用 `^BZ`、`^B5` 和 `^BZ` 类型 3；IMb 严格接受 20/25/29/31 位数字，不追加校验位。ESC/POS Native 现在支持 24 点顺序文字的粗体、单线下划线和反白，坐标旋转和非 24 点字号仍严格拒绝；ESC/POS Raster、TSPL/ZPL 内置字体的反白及 TSPL/ZPL 粗体/下划线也保持严格拒绝。R3–R49 已关闭的子范围保持稳定；剩余未支持的条码、旋转和样式按协议能力严格拒绝。Native 只接受可表达的顺序文字/码图和校准制表布局，继续拒绝图形和填充。斜线光栅化仅在显式配置时启用。

第三方同步操作只能在调用边界检查取消，内部对象与缓冲未完全计入预算；当前计数不等于实际进程堆上限。固定字体不混合逐字替代、不合成粗体，不证明复杂塑形/双向布局；入口/归档容量不同，超限严格拒绝。静区和声明占位不证明固件实际尺寸或可扫描性。发送中断和 raw 绕过后的设备状态由发送方管理。

### Verified Boundaries

- [x] R1 旋转图片的临时缓冲预算、取消恢复、源 padding 清除、Native 图片拒绝及静态资源预检已由独立测试覆盖；这些证据不宣称完整进程堆上限或实机能力。
- [x] R2 填充的正尺寸/溢出/页边界、ZPL 原生上限、图形与图片能力独立、静区占位、取消/预算恢复和 Native 拒绝已覆盖。
- [x] T8 五运行时、Host、HTTP、包消费者和固定字体 UI 门禁作为历史固定基线保存，未被 R1 报告覆盖范围之外的文档修改破坏。

### Deferred

- [ ] Profile 细化：按机型补充可用条码、HRI、文字样式、二维码方向等组合能力；依赖 R2–R17 的语义矩阵。
- [ ] GS1/FNC1 业务语义：确定输入表达、校验、编码和扫描器返回值，不能只验证打印字节。
- [ ] 复杂文字与字体范围：双向文字、塑形、组合字符、TTC、缺字替代和真实粗体策略。
- [ ] 日文及厂商代码页：补充设备字符集、代码页、设备字体和固件差异样本。
- [ ] 跨平台 CI 与默认系统字库：补充 Linux/macOS 运行时、字体和输出差异验证。
- [ ] 发送方恢复规范：明确分段顺序、中断、重试重复打印和设备状态恢复；不扩大本组件的连接管理范围。

未修改版本号、提交 Git 或发布 NuGet。无历史同构性能比较，Before=NOT_APPLICABLE。真实设备缺口不通过重复本地测试填补。

R15 包证据：候选包在 `artifacts/r15-planet-pack` 打包，`net6.0` 和 `netcoreapp3.1` 隔离消费者在 `artifacts/r15-planet-consumers` 通过；其他消费者 TFM 未在本轮重跑。R16 包证据：候选包在 `artifacts/r16-imb-pack` 打包，`net6.0` 和 `netcoreapp3.1` 隔离消费者在 `artifacts/r16-imb-consumers` 通过；R17 包证据：候选包在 `artifacts/r17-ucc-pack` 打包，`net6.0` 和 `netcoreapp3.1` 隔离消费者在 `artifacts/r17-ucc-consumers` 通过；保留 NU5104、缺少 README 和 EOL TFM 警告。R14 包证据保留在 `artifacts/r14-postnet-pack` 与 `artifacts/r14-postnet-consumers`。

Next Action：继续补齐剩余旋转锚点/分页组合与 Designer E2E，随后评估其他 ESC/POS 原生条码和旋转下划线组合；在每次扩展时保持能力集合、输入校验、尺寸度量和 Provider 严格一致。Implementation Status：R1/R2 CLOSED；R3–R6 OPEN_ACTIONABLE，R4/R5/R6/R7/R8/R9/R10/R11/R12/R13/R14 的 QR、Raster 文字、Raster Code128、CPCL 直连条码角度边界、ESC/POS Code39/Code93/EAN/UPC/Codabar/I2OF5/I2OF5C/I2OF5G、ESC/POS Native 下划线/反白、ZPL Code39/Code93/EAN/UPC/Codabar/I2OF5/MSI/POSTNET 和 TSPL MSI 子范围已实现但不代表整体闭环，不将状态换算为功能百分比。Test Status：Core/Templates net6 与 netcoreapp3.1 完整各1923/1923，TSPL MSI 专项各21/21、ZPL MSI 专项各35/35、ZPL POSTNET 专项各35/35、ESC/POS Native I2OF5C/I2OF5G 专项各16/16、联合相关回归各216/216、反白精准74/74、下划线精准74/74、ZPL Codabar/I2OF5 专项43/43、ZPL 零售专项47/47、ZPL Code93 精准32/32、ZPL Code39 精准24/24、Codabar/I2OF5 精准15/15、联合协议能力/模板能力95/95、EAN/UPC 精准21/21、ESC/POS Code93 精准6/6、Code39 精准8/8、ProtocolRotation69/69、码图旋转双运行时18/18、模板 Raster 码图双运行时3/3、Host109/109；Core/Templates 五 TFM L0 构建0错误，保留两个运行时测试的 NU1900 漏洞源不可访问警告；前端精准46/46、vue-tsc/Vite 构建通过；包消费者本轮 R14 重跑 net6.0/netcoreapp3.1 并通过，其他 TFM 复用既有证据。External Gate Status：BLOCKED_EXTERNAL。Goal Status：active。Previous Goal Turn：PROGRESS（R13 TSPL MSI 子范围已完成）；本轮 PROGRESS，No-Progress Check：CHANGED，未将实机验收标记完成。

R15 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 ZPL PLANET 子范围，不代表整体闭环。Test Status：Core/Templates net6 与 netcoreapp3.1 完整各 1964/1964，PLANET 专项各 41/41，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 错误。候选包 `artifacts/r15-planet-pack` 与 net6.0/netcoreapp3.1 消费者 `artifacts/r15-planet-consumers` 均通过；保留 NU1900、NU5104、README 和 EOL TFM 警告。External Gate Status：BLOCKED_EXTERNAL。Goal Status：active。Previous Goal Turn：PROGRESS（R14 ZPL POSTNET 子范围已完成）；本轮 PROGRESS，No-Progress Check：CHANGED。

R16 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 ZPL Intelligent Mail（IMb）子范围，不代表整体闭环。Test Status：IMb 专项 net6.0/netcoreapp3.1 各 52/52，Core 完整各 2016/2016，DesignerHost 109/109，前端 238/238 与 vue-tsc/Vite 构建通过；Core/Templates 五 TFM L0 构建 0 错误；候选包 `artifacts/r16-imb-pack` 打包通过，net6.0/netcoreapp3.1 消费者 `artifacts/r16-imb-consumers` 均 PASS。保留 NU1900、NU5104、缺少 README、EOL TFM 和前端 chunk 警告。External Gate Status：BLOCKED_EXTERNAL。Next Action：继续剩余旋转锚点/分页组合与 Designer E2E，随后实现 ESC/POS Native UCC/EAN-128（GS `k` 74）或完成其机型能力 Profile 设计；IMb 实际扫描结果仍需目标 Zebra 设备验收。Goal Status：active。Previous Goal Turn：PROGRESS（R15 ZPL PLANET 子范围已完成）；本轮 PROGRESS，No-Progress Check：CHANGED。

R17 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 ESC/POS Native UCC/EAN-128 子范围，不代表整体闭环。Test Status：UCC/EAN-128 专项 net6.0/netcoreapp3.1 各 13/13，相关 ESC/POS/模板基线双运行时各 109/109，Core 完整各 2028/2028，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 错误；候选包 `artifacts/r17-ucc-pack` 打包通过，net6.0/netcoreapp3.1 消费者 `artifacts/r17-ucc-consumers` 均 PASS。保留 NU1900、NU5104、缺少 README 和 EOL TFM 警告。External Gate Status：BLOCKED_EXTERNAL。Next Action：继续剩余旋转锚点/分页组合、Designer E2E 和其他 ESC/POS/ZPL/TSPL 条码；GS1-128 实际扫描与固件差异仍需目标 Epson 设备验收。Goal Status：active。Previous Goal Turn：PROGRESS（R16 ZPL Intelligent Mail 子范围已完成）；本轮 PROGRESS，No-Progress Check：CHANGED。
R18 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 ESC/POS Raster 主机 Code39/Code93/EAN/UPC/Codabar/I2OF5 子范围，不代表整体闭环。Test Status：Raster 条码专项 net6.0/netcoreapp3.1 各 15/15，相关 ESC/POS/模板回归双运行时各 176/176，Core 完整各 2043/2043，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 错误；候选包 `artifacts/r18-raster-barcode-pack` 打包通过，net6.0/netcoreapp3.1 消费者 `artifacts/r18-raster-barcode-consumers` 均 PASS。保留 NU1900、NU5104、缺少 README 和 EOL TFM 警告。外部仍需真实 ESC/POS 设备确认不同 ZXing 符号的扫描质量、纸宽和 DPI。External Gate Status：BLOCKED_EXTERNAL。Next Action：继续剩余旋转锚点/分页组合、Designer E2E、R6 旋转下划线组合，并评估 MSI/POSTNET/PLANET/IMb 等没有可靠 Raster 编码器的类型；保持未表达能力严格报错。Goal Status：active。Previous Goal Turn：PROGRESS（R17 ESC/POS Native UCC/EAN-128 子范围已完成）；本轮 PROGRESS，No-Progress Check：CHANGED。
R19 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 TSPL Code93 原生条码子范围，不代表整体闭环。Test Status：TSPL Code93 专项 net6.0/netcoreapp3.1 各 22/22，Core 完整各 2069/2069，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 警告/错误；候选包 `artifacts/r19-tspl-code93-pack` 打包通过，net6.0/netcoreapp3.1 消费者 `artifacts/r19-tspl-code93-consumers` 均 PASS。保留 NU1900、NU5104、缺少 README 和 EOL TFM 警告；TSPL Code93 实际打印/扫描仍需目标 TSC 设备验收。External Gate Status：BLOCKED_EXTERNAL。Next Action：继续剩余旋转锚点/分页组合、Designer E2E、R6 旋转下划线组合和其他未映射 ESC/POS/ZPL/TSPL 条码；保持未表达能力严格报错。Goal Status：active。Previous Goal Turn：PROGRESS（R18 ESC/POS Raster 多条码子范围已完成）；本轮 PROGRESS，No-Progress Check：CHANGED。
R20 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 TSPL I2OF5/I2OF5C 原生条码子范围，不代表整体闭环。Test Status：TSPL I2OF5/I2OF5C 专项 net6.0/netcoreapp3.1 各 34/34，联合相关回归各 126/126，Core 完整各 2108/2108，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 警告/错误；候选包 `artifacts/r20-tspl-i2of5-pack` 打包通过，net6.0/netcoreapp3.1 消费者 `artifacts/r20-tspl-i2of5-consumers` 均 PASS。保留 NU1900、NU5104、缺少 README 和消费者 EOL 框架提示。External Gate Status：BLOCKED_EXTERNAL。Next Action：继续剩余旋转锚点/分页组合、Designer E2E、R6 旋转下划线组合和其他未映射 ESC/POS/ZPL/TSPL 条码；保持未表达能力严格报错。Goal Status：active。Previous Goal Turn：PROGRESS（R19 TSPL Code93 子范围已完成）；本轮 PROGRESS，No-Progress Check：CHANGED。

R21 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 ZPL I2OF5C 原生条码子范围，不代表整体闭环。Test Status：ZPL I2OF5C 专项、受影响 Core/Templates 双运行时、L0 构建、DesignerHost 和候选包消费者验证将在本轮完成后记录；保留 NU1900、NU5104、缺少 README 和消费者 EOL 框架提示。External Gate Status：BLOCKED_EXTERNAL。Next Action：继续剩余旋转锚点/分页组合、Designer E2E、R6 旋转下划线组合和其他未映射 ESC/POS/ZPL/TSPL 条码；保持未表达能力严格报错。Goal Status：active。Previous Goal Turn：PROGRESS（R20 TSPL I2OF5/I2OF5C 子范围已完成）；本轮 PROGRESS，No-Progress Check：CHANGED。
R21 验证补充：ZPL I2OF5C 专项及受影响 Core/Templates 双运行时各 208/208，Core 完整双运行时各 2133/2133，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 警告/错误；候选包 `artifacts/r21-zpl-i2of5c-pack` 打包通过，隔离消费者 `artifacts/r21-zpl-i2of5c-consumers-net6fresh`（net6.0）与 `artifacts/r21-zpl-i2of5c-consumers-netcorefresh`（netcoreapp3.1）均 PASS。
R22 当前状态：Implementation Status 为 R1/R2 CLOSED、R3–R6 OPEN_ACTIONABLE；本轮完成 ESC/POS Raster `I2OF5C`/`I2OF5G` 子范围，不代表整体闭环。Test Status：Raster I2OF5C/I2OF5G 专项及受影响 Core/Templates 双运行时各 71/71，Core 完整双运行时各 2140/2140，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 警告/错误；候选包 `artifacts/r22-escpos-raster-i2of5-pack` 打包通过，隔离消费者 `artifacts/r22-escpos-raster-i2of5-consumers-net6`（net6.0）与 `artifacts/r22-escpos-raster-i2of5-consumers-netcore`（netcoreapp3.1）均 PASS。保留 NU1900、NU5104、缺少 README 和消费者 EOL 框架警告。External Gate Status：BLOCKED_EXTERNAL。Next Action：继续 R3/R4/R5/R6 剩余旋转、HRI、Designer E2E、其他条码和样式；保持未表达能力严格报错。Goal Status：active。Previous Goal Turn：PROGRESS（R21 ZPL I2OF5C 子范围已完成）；本轮 PROGRESS，No-Progress Check：CHANGED。

R22 能力修正：ESC/POS Raster 已从基础 I2OF5 扩展到 I2OF5C/I2OF5G，主机负责 3/1、4/9 校验位规范化后复用 ITF；R18 历史段中“Raster 严格拒绝 I2OF5C/I2OF5G”的描述仅代表当时状态，当前 TODO 以本轮状态和能力矩阵为准。

R23 当前状态：Implementation Status 仍为 R1/R2 CLOSED、R3-R6 OPEN_ACTIONABLE；本轮完成 ESC/POS Raster MSI 子范围与 Raster 旋转下划线测试覆盖，不代表整体闭环。MSI 使用 ZXing 主机光栅编码，统一输入为 1-13 位数字且不追加校验位；Native MSI、GS1/邮政等没有可靠主机编码器的类型继续严格拒绝。Test Status：MSI/相关能力精确测试 net6.0 与 netcoreapp3.1 各 81/81，旋转下划线精确测试各 12/12，Core/Templates 完整各 2155/2155，DesignerHost 109/109；Core/Templates 五 TFM L0 构建 0 警告/错误；候选包 `artifacts/r23-escpos-raster-msi-pack` 打包通过，隔离消费者 `artifacts/r23-escpos-raster-msi-consumers-net6fresh` 与 `artifacts/r23-escpos-raster-msi-consumers-netcorefresh` 均 PASS。保留 NU1900、NU5104、缺少 README 和消费者 EOL 框架警告。External Gate Status：BLOCKED_EXTERNAL；Goal Status：active。

R23 已完成 TODO：ESC/POS Raster MSI 主机编码、1-13 位数字输入校验、不追加校验位、宽度/HRI/旋转/页边界复用、失败恢复、能力矩阵、模板 Provider 与双运行时回归；Raster 旋转下划线四角度墨迹/占位验证及 Native、CPCL、TSPL、ZPL 严格拒绝测试；五目标框架 L0、DesignerHost、候选包和双运行时消费者证据。

R24 已完成 TODO：ESC/POS Raster POSTNET 主机编码，支持 5/9/11 位数字和连字符归一化，主机追加校验位并生成 USPS 全高/半高条，支持宽度测量、四角旋转、HRI 数据、模板页面边界、失败恢复和能力快照；Native ESC/POS、TSPL、ZPL 的 POSTNET 边界保持各自既有语义。

R24 未完成 TODO（OPEN_ACTIONABLE）：R3 HRI 的 Designer 端到端、完整目标运行时矩阵、静区组合和实机字高校准；R4/R5 剩余锚点、占用尺寸、换行分页组合与 Designer E2E；ESC/POS Raster 仍未映射 GS1-128、PLANET、Intelligent Mail、FIM 等条码及其业务语义；TSPL/ZPL 内置字体粗体/下划线、其他未映射条码与完整协议 Profile 仍需逐项评估；R6 旋转下划线之外的文字样式组合仍需继续覆盖。

R24 验证：POSTNET 专项及相关条码回归 net6.0 与 netcoreapp3.1 各 76/76；Core/Templates 完整各 2170/2170；DesignerHost 109/109；Core 与 Templates 五目标框架 L0 构建 0 警告、0 错误。候选包 `artifacts/r24-escpos-raster-postnet-pack` 打包通过，保留 NU5104、缺少 README 和消费者 EOL 框架警告；net6.0 隔离消费者 `artifacts/r24-escpos-raster-postnet-consumers-net6fresh` PASS，netcoreapp3.1 消费者在当前 SDK 的旧 TFM CopyLocal 阶段因 `MSB3030` 报缺失传递 DLL（文件实际存在）而标记 BLOCKED_EXTERNAL。另保留 NU1900 漏洞源不可访问警告；本轮未修改版本号、提交 Git 或发布 NuGet。真实热敏/针式打印机的条高、DPI、纸宽、打印和扫描仍属于 BLOCKED_EXTERNAL。

R23 欠缺考虑与边界：BLOCKED_EXTERNAL 为真实热敏/针式机型、固件、DPI、代码页、纸宽、打印/扫描、进纸和裁切验收；BLOCKED_APPROVAL 为正式性能、容量和超时阈值；NOT_APPLICABLE 为连接、端口、驱动和设备发现。Accepted Limitations：MSI 仅提供 ESC/POS Raster 主机编码，Native MSI 不静默降级；统一 MSI 不自动生成校验位；ZXing 光栅结果、HRI 字高和设备扫码质量不等于固件验收。Deferred 保留 GS1/FNC1 业务语义、复杂文字塑形/双向布局、日文/厂商代码页、跨平台字库 CI、发送中断与重试恢复规范。

R24 欠缺考虑与边界：POSTNET 主机编码采用固定 USPS 五条模式、主机校验位和半高条，不宣称任意厂商邮政扩展、设备原生 GS `k` 或扫描器验收；模块间距、条高、DPI、纸宽和 HRI 字高需按真实设备校准。netcoreapp3.1 包消费者的 `MSB3030` 传递 DLL CopyLocal 问题属于当前 SDK/旧 TFM 工具链外部阻塞，未用本地文件存在性检查冒充 PASS。其余 BLOCKED_EXTERNAL、BLOCKED_APPROVAL、NOT_APPLICABLE 和 Deferred 边界沿 R23 保持。

## R25 本轮：ESC/POS Raster PLANET

R25 已完成的 TODO：

- [x] ESC/POS Raster 主机 PLANET 编码：支持 12/14 位 ASCII 数字、连字符归一化和数字和校验；使用 USPS 规定的 POSTNET 反相五条模式，保留全高起止框条，短条按基线对齐并占请求高度的一半。
- [x] 统一 Provider 与模板接入：`SupportsBarcodeType`、宽度测量、页面预检、四角旋转、HRI 数据、元素路径错误和失败后重试均复用既有 Raster 路径；`SupportsCommandBarcodeType` 仍为 false，避免把主机光栅能力误报为 ESC/POS 原生命令。
- [x] PLANET 回归覆盖：模式条高/静区、12/14 位和连字符、四角旋转、非法长度/字符/校验位、资源预算恢复、模板输出及 ZPL/ESC/POS 能力矩阵同步。
- [x] 本地验证：PLANET/POSTNET/ZPL 相关精准测试 net6.0 与 netcoreapp3.1 各 75/75；Core/Templates 完整测试 net6.0、netcoreapp3.1、netcoreapp2.2 各 2188/2188；DesignerHost 109/109；Core/Templates 五目标框架 L0 构建 0 警告、0 错误；`git diff --check` 通过。

### R25 分类后的 TODO

#### Completed

- [x] ESC/POS Raster PLANET 主机编码、规范化、校验、光栅条高、宽度、旋转、模板和能力快照。
- [x] 受影响 Core/Templates、DesignerHost 和可用旧 TFM 的回归证据。

#### Open Actionable

- [ ] R3 HRI 的 Designer 端到端、静区组合、完整可用运行时矩阵和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E；需继续保持 Native 不可表达时显式 Raster 或严格报错。
- [ ] ESC/POS Raster 仍未接入 GS1-128/UCC-EAN-128、Intelligent Mail/IMb、FIM 等缺少可靠主机编码器的类型；其余协议未映射条码也需逐项评估。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合，以及对应模板/设计器能力。

#### Blocked Approval

- [ ] 正式性能、容量和超时阈值仍需项目负责人批准；本轮只验证既有资源预算边界。

#### Blocked External

- [ ] 真实热敏/针式打印机的 PLANET 条高、模块间距、DPI、纸宽、打印/扫描、进纸和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主缺少对应 .NET Runtime；R24 的 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 也仍属当前工具链/外部环境阻塞。

#### Not Applicable

- [ ] 连接管理、端口、驱动安装和设备发现仍不属于字节生成组件范围。

#### Accepted Limitations

- [ ] PLANET 当前是 ESC/POS Raster 主机光栅能力，不提供 ESC/POS Native `GS k` 命令；不自动把该能力降级为设备原生命令。
- [ ] 固定 USPS 模式和校验不覆盖厂商私有邮政扩展；HRI 字高、设备 DPI 和可扫描性必须实机校准。

#### Verified Boundaries

- [x] 在当前可用运行时和声明资源预算内，PLANET 的错误输入会在输出前失败，预算释放后可重试，四角旋转和模板页面边界均有回归。

#### Deferred

- [ ] GS1/FNC1 业务语义、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

R25 状态：Implementation Status 为本轮 PLANET 子范围 CLOSED；整体目标仍有 R3/R4/R5/R6 的 OPEN_ACTIONABLE，Goal Status 保持 active。Release Gate Status 仍为 BLOCKED_EXTERNAL；未修改版本号、提交 Git、发布 NuGet 或伪造实机验收。net7.0/net5.0 因本机缺少运行时未执行测试，不影响五 TFM 生产构建证据。

## R26 本轮：ESC/POS Raster FIM A/B/C

R26 已完成的 TODO：

- [x] ESC/POS Raster 主机 FIM 编码：支持统一模型已有的 A、B、C 三种 USPS 九位模式，按每个模式位绘制条/空并保留稳定的 9 个条槽和 8 个间隙。
- [x] 统一 Provider 与模板接入：FIM 加入 Raster 能力快照、类型化宽度测量、页边界预检、四角旋转、HRI、静区布局和失败恢复；原生命令能力保持 false。
- [x] 严格边界：D/E、大小写、长度错误在输出前拒绝；与 CPCL/模板已有 FIM A/B/C 语义保持一致。
- [x] 本地验证：FIM 及 POSTNET/PLANET/既有 Raster 条码精准回归 net6.0 与 netcoreapp3.1 各 135/135；Core/Templates 完整测试 net6.0、netcoreapp3.1、netcoreapp2.2 各 2206/2206；DesignerHost 109/109；Core/Templates 五目标框架 L0 构建 0 警告、0 错误；`git diff --check` 通过。

### R26 分类后的 TODO

#### Completed

- [x] ESC/POS Raster FIM A/B/C 主机编码、9 位模式、宽度、旋转、模板、HRI、静区和能力快照。
- [x] FIM 非法模式、预算恢复、逐像素条空和可用运行时回归。

#### Open Actionable

- [ ] R3 HRI 的 Designer 端到端、静区组合、完整可用运行时矩阵和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E；继续保持 Native 不可表达时显式 Raster 或严格报错。
- [ ] ESC/POS Raster 仍未接入 GS1-128/UCC-EAN-128、Intelligent Mail/IMb；FIM D/E 及其他厂商邮政变体保持明确拒绝。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合，以及对应模板/设计器能力。

#### Blocked Approval

- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。

#### Blocked External

- [ ] 真实热敏/针式打印机的 FIM/PLANET 条高、模块间距、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主缺少对应 .NET Runtime；R24 的 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 仍属外部工具链阻塞。

#### Not Applicable

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### Accepted Limitations

- [ ] FIM 仅开放统一模型已有的 A/B/C 模式；D/E 未纳入统一输入契约，继续严格报错。
- [ ] FIM 当前为 ESC/POS Raster 主机光栅能力，不提供 ESC/POS Native 原生命令，也不覆盖 USPS 之外的厂商扩展。

#### Verified Boundaries

- [x] A/B/C 模式在声明资源预算内逐像素输出；错误输入在输出前失败，预算释放后可重试，四角旋转和模板静区均有回归。

#### Deferred

- [ ] GS1/FNC1 业务语义、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

R26 状态：Implementation Status 为本轮 FIM A/B/C 子范围 CLOSED；整体目标仍有 R3/R4/R5/R6 的 OPEN_ACTIONABLE，Goal Status 保持 active。Release Gate Status 仍为 BLOCKED_EXTERNAL；未修改版本号、提交 Git、发布 NuGet 或伪造实机验收。

## R27 本轮：ESC/POS Raster GS1-128/UCC-EAN-128

R27 已完成的 TODO：

- [x] ESC/POS Raster 主机 GS1-128 编码：复用 ZXing Code 128，启用 `GS1_FORMAT`，把 ASCII 29 分隔符编码为 FNC1，并保留数字应用标识开头的统一输入契约。
- [x] 统一 Provider 与模板接入：UCC/EAN-128 加入 Raster 能力矩阵、实际符号宽度测量、页面边界、四角旋转、HRI、模板静区和资源预算恢复。
- [x] 严格边界：空值、非数字起始应用标识、非 7-bit ASCII/非 ASCII 29 字符在输出前拒绝；HRI 去除不可打印的分隔符后再绘制。
- [x] 本地验证：GS1 Raster/Native 相关精准测试 net6.0 与 netcoreapp3.1 各 80/80，netcoreapp2.2 专项 28/28；Core 完整测试 net6.0、netcoreapp3.1、netcoreapp2.2 各 2221/2221；DesignerHost 109/109；Core/Templates 五目标框架 L0 构建 0 警告、0 错误；`git diff --check` 无错误。

### R27 分类后的 TODO

#### Completed

- [x] ESC/POS Raster GS1-128/UCC-EAN-128 主机编码、FNC1、ASCII 29、宽度、HRI、旋转、模板、静区、预算和能力快照。
- [x] 旧 ESC/POS Native `GS k 74` UCC/EAN-128 行为及 TSPL/ZPL/IMb/FIM 边界回归。

#### Open Actionable

- [ ] R3 HRI 的 Designer 端到端、静区组合、完整可用运行时矩阵和真实设备字高校准。
- [ ] R4/R5 剩余旋转锚点、占用尺寸、换行分页组合和 Designer E2E；继续保持 Native 不可表达时显式 Raster 或严格报错。
- [ ] ESC/POS Raster Intelligent Mail/IMb 主机编码；FIM D/E、厂商邮政变体以及其他未映射条码仍需产品范围决策或逐项实现。
- [ ] R6 TSPL/ZPL 内置字体粗体/下划线和其他文字样式组合，以及对应模板/设计器能力。

#### Blocked Approval

- [ ] 正式性能、容量和超时阈值仍需项目负责人批准。

#### Blocked External

- [ ] 真实热敏/针式打印机的 GS1-128/FIM/PLANET 条高、模块间距、DPI、纸宽、打印/扫描和固件兼容验收。
- [ ] net7.0、net5.0 测试宿主缺少对应 .NET Runtime；旧 netcoreapp3.1 包消费者 CopyLocal `MSB3030` 仍属外部工具链阻塞。

#### Not Applicable

- [ ] 连接管理、端口、驱动安装和设备发现不属于字节生成组件范围。

#### Accepted Limitations

- [ ] Raster GS1-128 只承诺 ZXing 0.16.11 的 Code 128/GS1/FNC1 主机编码；AI 长度、字段语义、业务校验和目标设备扫描结果仍由调用方与实机确认。
- [ ] FIM 当前仍仅支持 A/B/C；D/E 和厂商扩展继续严格报错。

#### Verified Boundaries

- [x] ASCII 29 分隔符、无效字符和不完整起始应用标识在输出前失败；预算失败后可重试，四角旋转和模板页边界均已验证。

#### Deferred

- [ ] GS1 AI 自动解析/校验、复杂文字塑形/双向布局、厂商代码页、跨平台字库 CI、发送中断与重试恢复规范继续延期。

R27 状态：Implementation Status 为本轮 GS1-128 Raster 子范围 CLOSED；整体目标仍有 R3/R4/R5/R6 的 OPEN_ACTIONABLE，Goal Status 保持 active。Release Gate Status 仍为 BLOCKED_EXTERNAL；未修改版本号、提交 Git、发布 NuGet 或伪造实机验收。
