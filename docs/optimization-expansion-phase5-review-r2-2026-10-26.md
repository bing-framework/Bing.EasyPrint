# 第五阶段修正 1/3 独立复核

日期：2026-10-26。已读取 Codex 实现线程的完成结果及当前执行报告，未将 completed 通知当成验收。主代理检查实现和真实反例；luna_worker 独立核对日志。本轮仅修改复核文档/计划，生产代码与测试继续由委派线程负责。

## 当前结论

F5 实现关闭，F6 仍需修正；整体 **9/10**。R1、R2 已 `CLOSED`；R3 的编组身份/锁与 R4 的导入历史、独立历史提交已修复，但排列入口的锁保护与有效布局仍有可执行缺口。E1 仅剩根 README 快速示例的直接运行证据。下一步为第五阶段修正 **2/3**，不重开前四阶段，不重跑未变范围的全量门禁。

## 验证及证据范围

影响范围：Host 导出、Vue 排列/身份/历史、BatchConsumer 案例。生产库五 TFM、Core/Templates、版本、公共 API 和包未改变；原 Core 三运行时、XML、包消费证据可按范围复用。

独立盘点确认：默认 Host `368/368`、定向 Host `11/11`，0 失败/跳过；Vitest 27 文件 `476/476`，Chromium `227/227`（workers=1、retries=0）。BatchConsumer opt-in 实际执行 `2/2`、2 页、63698 bytes，ZIP/manifest 对应 2 PDF、4 SVG。Host testhost 为 .NET 10.0.11，消费者为 .NET 6.0.36。证据见执行报告的 `phase5/integration-r1/` 明确路径；默认 Host 当前 TRX 在 `artifacts/designer-host-verification-tests/designer-host-net10.trx`，不要把旧输出副本中的 244 项 net8 结果混入。

主代理 L1/L3：从 `artifacts/designer-host-verification-bin` 启动独立临时 Host，地址 `http://127.0.0.1:5193`；使用项目 Vite 在 `http://127.0.0.1:5189` 与该 Host 联动。使用已有 Playwright/Chromium，未安装依赖；Browser plugin skill 未列出，按 frontend-testing-debugging 技能采用 Playwright。两项浏览器探针运行真实 UI、1440×900，未替换业务实现。临时服务在复核后停止。

本轮证据：[main-review-r2](../artifacts/optimization-expansion-20261025/phase5/main-review-r2/)。未重复整个单元/浏览器矩阵。首轮探针沿用测试中的 FontSize=18，CPCL 拒绝后无法取得有效预览；最终锁反例使用合法 FontSize=24、文字 A，不以夹具失败证明产品缺陷。

## 已关闭范围

### R1：Code49 SVG 几何 — CLOSED

`Export.cs` 已将外部静区与内部 leading padding 分开。8 项回归覆盖四旋转和默认/显式静区，检查所有矩形在 viewBox 内，并与共享整页光栅的实际码图区域逐 dot 比较。主代理重放旧 `code49-request.json` 得到 HTTP 200，输出 `code49.svg` 不再含超出 162 dot viewBox 的条纹。未扩大 viewBox 掩盖错误。

### R2：原协议布局与 DPI — CLOSED

强制 ESC/POS 选项转换已移除。单条 SVG 用来源选项渲染，批量直接使用与打印相同的 `printResult`，DPI 从来源选项获取。主代理重放 TSPL 两页校准请求，第二页导出 HTTP 200；旧 300 DPI 批量请求返回宽 `22.352mm`、高 `1.693333mm`，与单条一致。输出保存在 `tspl-pagination.svg`、`batch-300.zip/svg`。Host 368 项与 11 项新回归为本轮修正后证据。

### R3/R4 的已修复子项

元素编组已检查选中子树；结构事务避免同步 reconcile 中间态清锁；行编组保留原对象。已有身份单测与真实浏览器验证支撑这些具体路径。JSON/ZIP 导入不再重置历史，排列前后提交历史快照；这些子项不再要求重复修改。

E1 的默认 Host 入口、opt-in 批量案例、运行时归属、SVG 向量/解码样本计量口径已修正。根 README 示例不在这些证据范围内。

## 剩余 OPEN_ACTIONABLE

### R4-B / P1：连续同轴排列仍使用旧预览，坐标错误

位置：`web/Bing.EasyPrint.Designer/src/App.vue` 的 `arrangementEnabled`（约 712）、`arrangeSelection`（约 2112）、`schedulePreview/invalidatePreview`。

触发：两个绝对定位元素初始 X=70/10，宽 40/30；左对齐后，立即右对齐。预览合并请求尚未返回时，第二次排列仍从旧 `currentPage` 计算 delta，再将 delta 加到已经修改过的模板坐标。

主代理浏览器证据：`arrangement-probe.cjs`、`arrangement-result.json`、`arrangement.png`。为了稳定表达“预览未返回”，探针对预览网络延迟 1500ms（未修改产品超时/重试）；实际得到 X=10/70、右边界 50/100，正确结果应为 X=10/20、右边界均 50。页面标题正确、非空、无 Vite 错误层，pageerror 为空。

现有新 E2E 左对齐后上对齐分别修改 X/Y，不能暴露同轴旧坐标重复偏移。历史记录虽然变成独立快照，记录的第二次排列仍可能是错误几何。

修正建议：排列必须绑定当前模板/数据/输出配置的有效布局版本；旧预览或请求在途时，获取有效布局后操作，或明确禁用。不能只看 previewBusy，因为 schedulePreview 的去抖期间会将其设回 false；不能用额外等待或只刷新测试掩盖入口问题。

验收：左→右、上→下、同一排列重复执行均几何正确/幂等；预览等待和失败时不使用旧几何；每次成功操作仍为单独撤销事务。真实浏览器测试显式控制响应时序，断言最终坐标，而非只断言 undo 次数。

### R3-B / P1：排列入口仍可移动含锁定子节点的组合

位置：同上 `arrangementEnabled/arrangeSelection`；已有 `canModifySubtree` 在其他移动入口使用，但排列仍只调用顶层 `isNodeLocked(path,path)`。

触发：未锁定组合内的文字 A 已锁定，选择组合和同级元素右对齐。

证据：`nested-lock-arrange.cjs/json`。有效 CPCL 预览下排列按钮 enabled=true；组合从 `(20,20)` 移至 `(100,20)`，锁定子节点的页面位置也随之移动，保存项目仍显示子节点 locked=true。这与普通拖动入口的子树锁保护不一致。编组的上一修复没有覆盖排列入口。

验收：排列检查所有实际受影响节点/实例的锁，包含组内子节点与锁定祖先；被拒绝时模板和历史不变，界面给出明确原因。未锁定对象正常排列，现有编组/身份已通过范围保持。

### E1-README / P2：执行了另一个 README，根快速示例仍未验证

`README.md:39` 的原示例为注册 CodePages、`new EscPosPrintCommand(Encoding.GetEncoding("gbk"))`、Initialize、Text("订单完成")、Feed(2)、Build/GetBytes。当前 `readme-batch-example-r1-green` 执行的是 BatchConsumer README；原片段仍无直接构建/执行记录。

验收：在独立生成目录原样执行根 README 片段，记录命令、实际框架/运行时、退出码，并断言实际字节/编码与示例相符；无需改写已正确的根 README，也不需再次运行批量案例。执行报告不得再将 BatchConsumer 结果代替此项。

## 状态与停止规则

- Completed：O1—O4、F1—F5，9/10；本轮 R1/R2 和上述子项关闭。
- Open Actionable：R3-B、R4-B、E1-README。
- Blocked Approval：无。
- Blocked External：Windows/Linux Actions 实际运行。
- Not Applicable：实机验收，用户已排除；未发布。
- Accepted Limitations：SVG 不含设备 HRI；未声明的软件码制明确拒绝。
- Verified Boundaries：此前预算/取消门禁按未变范围复用。
- Deferred：原计划传输、复杂字体、图片大版本等。
- No-Progress Check：CHANGED，导出/编组/历史已有实际进展；剩余排列反例有明确根因和验收。不是仅因状态未满而继续。
- Next Action：同一 Codex/gpt-6-luna/max 线程修正 2/3，仅上述三项；先定向浏览器回归，再按改动范围执行前端完整门禁。Host/Core/XML/包未变则复用，不额外跑全矩阵。
- Goal Status：RUNNING。
