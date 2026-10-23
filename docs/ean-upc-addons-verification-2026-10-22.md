# EAN/UPC Composite 附加码阶段验收

本阶段开始于 2026-10-22，验证跨至 2026-10-23（Asia/Shanghai），文件名及证据目录沿用启动日。基线为 `dc79eab9b27aaa53fd89f887b55613d777b64184`，起点工作区干净。未提交、推送、发布或改包版本；本地候选包仍为 1.0.0。五目标框架与 .NET 公共签名保持不变。

本轮补齐 EAN-8/EAN-13/UPC-A/UPC-E Composite 的严格 2/5 位附加码。输入使用 `主码+附加码`，不修剪、不补零；既有主码校验及 UPC-E 零压缩继续生效。普通 Type=4 条码不扩展附加码契约。实施清单见[计划](../ai_docs/plans/ean-upc-addons-2026-10-22.md)，完整用法见[附加码指南](gs1-composite-ean-upc-addons.md)。

## 功能覆盖矩阵

| 范围 | 实现和证据 | 状态 |
| --- | --- | --- |
| 四类主码 × 2/5 位附加码 × A/B | [内部编码](../src/Bing.EasyPrint/Protocols/Gs1CompositeEncoder.EanUpc.cs)；[8组独立完整矩阵](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeEanUpcAddonVectors.cs)逐模块一致，保留Zint来源与许可 | CLOSED |
| 奇偶、校验位与严格字符 | [核心测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeEanUpcTests.cs)；独立 ZXing 解码器恢复全部100个两位值与覆盖10种奇偶模式的五位值；主码补校验与已有校验一致；非法长度、空白、全角、重复加号拒绝 | CLOSED |
| 尺寸、分隔与四协议 | 主码分隔位置保持，附加间距7X/9X；四角旋转/非对称静区逐像素比较，CPCL/ESC/POS Raster/TSPL/ZPL输出与参考图片一致，精确贴边与越界拒绝 | CLOSED |
| 资源、取消与恢复 | 预取消、容量回退、预算拒绝、非法输入归零，失败渲染不返回部分页面；同一Provider继续成功渲染，作用域结束归还文档/Provider/输出预留 | CLOSED |
| 模板与Host | V2/V3字段及JSON往返保留，Host四协议绑定数据、预览、ZIP输出；非法附加码保留Primary和元素路径 | CLOSED |
| 设计器 | 输入校验、提示、编辑、实际尺寸、导出、项目重载；纯主码仍兼容旧Host，固定附加值要求独立feature；三项/四项早期primaryFormats数组继续兼容 | CLOSED |
| 案例和包消费 | 新增四类附加码C#/JSON案例；全部16类×4协议64组C#/JSON/往返一致；本地候选包net6.0消费完整执行 | CLOSED |
| 当前文档与注释 | 公共XML契约、README、开发/核心/模板/设计器/Host指南及状态导航更新；历史验收记录保留原日期、原计数 | CLOSED |

Host 追加 `gs1-composite-ean-upc-addons` 与 `ean-upc-digits-with-addon`，线性类型数组仍为12项。浏览器只对固定 `Field.Value` 的附加码声明该必需特性；动态 `Field.Name` 在Host绑定后验证，旧Host的既有纯数字预检明确拒绝，不在浏览器复制模板绑定引擎。画布仍为布局占位，Host返回实际编码尺寸；不宣称可扫描位图预览。

## 本次验证

证据目录为 [artifacts/ean-upc-addons-20261022](../artifacts/ean-upc-addons-20261022)。后端命令顺序保存在 [final-backend.ps1](../artifacts/ean-upc-addons-20261022/final-backend.ps1)，日志见 [backend-final.log](../artifacts/ean-upc-addons-20261022/backend-final.log)。XML输出写入隔离目录，未覆盖跟踪中的XML。

| 验证命令 / 范围 | 框架、结果与证据 |
| --- | --- |
| `dotnet test ... -c Release -f net6.0 --filter FullyQualifiedName~Gs1Composite` | 332通过，0失败/跳过：[targeted-final.trx](../artifacts/ean-upc-addons-20261022/targeted-final.trx)；后加独立解码测试1项覆盖110输入，单独通过：[addon-decode.trx](../artifacts/ean-upc-addons-20261022/addon-decode.trx) |
| Host `--filter FullyQualifiedName~Gs1CompositeHostTests` | net8.0，72通过，0失败/跳过：[host-targeted.trx](../artifacts/ean-upc-addons-20261022/host-targeted.trx) |
| `tools/Verify-XmlDocumentation.ps1` | Core/Templates × net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1，10/10通过，缺失/无效XML诊断0：[verification.json](../artifacts/xml-documentation-verification/20261023-000735-611-f0644433/verification.json) |
| `Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1` | 各3722通过、0失败/跳过；实际运行时6.0.36、3.1.32，无跨运行时滚动替代：[runtime-verification.json](../artifacts/ean-upc-addons-20261022/runtimes-final/20261022-160756-1e25a700/runtime-verification.json) |
| `tools/Verify-DesignerHost.ps1` 默认参数 | net8.0，199通过、0失败/跳过：[designer-host-net8.trx](../artifacts/ean-upc-addons-20261022/host-final/designer-host-net8.trx) |
| `npm test -- --reporter=json` | 388通过、0失败/跳过：[web-unit-final.json](../artifacts/ean-upc-addons-20261022/web-unit-final.json) |
| `npm run build`（含vue-tsc） | 类型检查和生产构建通过：[web-build-final.log](../artifacts/ean-upc-addons-20261022/web-build-final.log) |
| `npx playwright test eanUpcComposite.spec.ts --workers=1 --retries=0` | Chromium定向8通过、0失败/跳过/flaky：[web-e2e-targeted.json](../artifacts/ean-upc-addons-20261022/web-e2e-targeted.json) |
| 完整Chromium E2E，`--workers=1 --retries=0` | 177通过，0失败/跳过/flaky，首次完整运行即通过：[web-e2e-final.json](../artifacts/ean-upc-addons-20261022/web-e2e-final.json) |
| `dotnet run --project samples/Templates/Templates.csproj -c Release` | 16类Composite×4协议64组C#/JSON/往返字节一致，新附加码16组，未以文件存在代替运行 |
| 本地 `dotnet pack` + `Verify-PackageConsumer.ps1 -TargetFramework net6.0` | Core/Templates候选包消费PASS，解析到本地候选包及net6.0资产，实际6.0.36：[consumer-verification.json](../artifacts/ean-upc-addons-20261022/consumer-rc/consumer-net6.0/consumer-verification.json) |
| README及新指南实际C#片段 | net6.0成功执行，分别输出14/5392字节：[README日志](../artifacts/ean-upc-addons-20261022/readme-smoke.log)、[指南日志](../artifacts/ean-upc-addons-20261022/docs-smoke.log) |
| 桌面与移动视觉检查 | 1440×900/390×844，无控制台错误、框架覆盖层或横向溢出，截图已查看：[visual-qa.json](../artifacts/ean-upc-addons-20261022/visual-qa.json) |
| 差异、UTF-8、链接、生成物 | 44个变更文本文件UTF-8有效、相对链接目标存在；`git diff --check`通过，跟踪XML未修改：[static-checks.json](../artifacts/ean-upc-addons-20261022/static-checks.json) |

首轮与第二轮核心定向测试均为328通过、4失败、0跳过，分别保留 [targeted-initial.trx](../artifacts/ean-upc-addons-20261022/targeted-initial.trx) 和 [targeted-corrected.trx](../artifacts/ean-upc-addons-20261022/targeted-corrected.trx)。失败源于新增测试把构造文档、构造Provider的正常保留预算也要求归零；现按所有权在请求作用域内构造，断言失败操作不增加预算、作用域释放后父级归零。生产实现未为此放宽预算。随后定向及双运行时全量均通过。

前端初次完整单测384通过；最终审阅又发现 JavaScript `$` 可在末尾换行之前匹配，已改为明确字符与长度检查，新增4项换行回归，最终388通过。未通过增加等待、重试或删除断言掩盖失败。

## 剩余事项与停止条件

- `NOT_APPLICABLE`：实机验收按用户要求排除；本轮不声明字体、DPI、进纸、裁切、扫码或设备恢复已验收。
- `ACCEPTED_LIMITATION`：设备驱动/传输/队列/状态查询、完整协议插件工厂扩展仍不在范围内；普通Type4附加码及Composite内置HRI未增加，应用可另放文字。
- `DEFERRED`：ExpandedStacked自定义列数、真实码图预览、完整GS1 AI字典、复杂字体排版，均为后续独立能力；不影响本阶段明确支持范围。
- `DEFERRED`：ImageSharp大版本迁移继续保留旧框架兼容；现有5项ImageSharp安全公告、旧测试运行时依赖/EOL告警未因本次附加码实现消除。Vite产物732.90kB的chunk提示、包消费既有CS8600告警仍存在，未调整阈值或关闭诊断。

最终 `IMPLEMENTATION_STATUS=PASS`、`TEST_STATUS=PASS`，A1–A5全部 `CLOSED`，`OPEN_ACTIONABLE=0`。本轮停止；未执行发布，也未运行无关大型性能矩阵。后续能力保留在独立清单中。汇总证据见 [verification.json](../artifacts/ean-upc-addons-20261022/verification.json)。
