# 单行 Expanded Composite 实施与验收

2026-10-22，基线 `c230b9f`。本阶段接续 CC-C，实现单行 DataBar Expanded + CC-A/CC-B；初始工作区干净，未提交、推送或发布包。最终代码和文档保留在工作区。Stacked Composite 仍属后续阶段。

## 实现与契约

| 范围 | 本次结果 | 证据入口 |
| --- | --- | --- |
| 统一模型 | 既有 `Gs1DataBarExpanded=28` 支持 Auto/A/B，拒绝 C；成员签名、枚举数值和 Type=11 不变 | [模型](../src/Bing.EasyPrint/Printing/PrintGs1CompositeElement.cs) |
| 编码 | 标准连接标志、压缩方法选择、分隔图案、对齐；普通 Expanded 历史字节保持 | [线性编码](../src/Bing.EasyPrint/Protocols/EscPosRasterCanvas.cs)、[组合编码](../src/Bing.EasyPrint/Protocols/Gs1CompositeEncoder.cs) |
| 输入/错误 | 括号 AI、首个 `(01)` 校验位、主次容量、明确参数及模板字段路径 | [核心测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeExpandedTests.cs)、[Host 测试](../tests/DesignerHost.Tests/Gs1CompositeHostTests.cs) |
| 布局/资源 | 行高 2X、分隔 X、四方向和静区、超界拒绝、取消及预算释放后可复用 | 核心像素测试、[Provider 测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeProviderTests.cs) |
| 模板 | V2/V3 字段与模式保存重载；四协议字节一致 | [模板测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeTemplateTests.cs)、[案例](../samples/Templates/Gs1CompositeSamples.cs) |
| Host | 增量 `gs1-composite-expanded`；能力数组追加类型，路由及格式不变 | [契约](../tools/DesignerHost/DesignerContract.cs)、[能力](../tools/DesignerHost/DesignerRequestProcessor.cs) |
| 设计器 | 合法模式、保留已有主数据、编辑/预览/导出/重载；旧 Host 明确禁用 | [浏览器测试](../web/Bing.EasyPrint.Designer/e2e/expanded.spec.ts)、[契约测试](../web/Bing.EasyPrint.Designer/src/gs1Composite.test.ts) |
| 文档/案例 | README、开发/使用手册、设计器说明、中文公开注释、JSON 和包消费同步 | [使用说明](gs1-composite-expanded.md)、[JSON](../samples/Templates/gs1-composite-expanded.json)、[包消费](../tests/PackageConsumer/Program.cs) |

模块归类保持原结构：标准编码在核心内部，模板复用模型和绑定，Host 发布能力，前端负责编辑与协商；没有增加公共编码入口、协议驱动或传输依赖。

## 验证结果

证据总目录：[artifacts/composite-expanded-20261022](../artifacts/composite-expanded-20261022/)，汇总见 [verification.json](../artifacts/composite-expanded-20261022/verification.json)。以下是本阶段最终候选结果；早期失败另列，未使用重试掩盖失败。

| 命令或入口 | 框架/范围 | 通过 | 失败 | 跳过 | 结果 |
| --- | --- | ---: | ---: | ---: | --- |
| `Verify-XmlDocumentation.ps1` | Core/Templates × 五 TFM | 10 次构建 | 0 | 0 | PASS；缺失/无效 XML 诊断作为错误 |
| `Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1` | net6.0，实际 .NET 6.0.36 | 3478 | 0 | 0 | PASS |
| 同上 | netcoreapp3.1，实际 .NET 3.1.32 | 3478 | 0 | 0 | PASS，无滚动替代 |
| `Verify-DesignerHost.ps1` 默认命令 | net8.0 Host，保留生成物 | 155 | 0 | 0 | PASS |
| `npm run build` | vue-tsc + Vite 生产构建 | 1 | 0 | 0 | PASS |
| `npm test -- --reporter=json …` | 完整 Vitest | 294 | 0 | 0 | PASS |
| `playwright test --workers=1 --retries=0 --reporter=json` | 完整 Chromium | 158 | 0 | 0 | PASS，flaky=0，retries=0 |
| `dotnet run --project samples/Templates/Templates.csproj -c Release -- …/samples` | CC-C 与 Expanded，各四协议 C#/JSON/往返 | 8 组 | 0 | 0 | PASS |
| 从 README 和 Expanded 手册提取 C# 执行 | net6.0 | 2 | 0 | 0 | PASS，分别生成 14 / 4815 字节 |
| 两库 `dotnet pack -c Release -m:1 -o …/packages-rc` + `Verify-PackageConsumer.ps1 … -TargetFramework net6.0` | 本地候选包 1.0.0 | 1 流程 | 0 | 0 | PASS，候选包匹配；许可含 rss.c |

后端完整命令保存在 [final-backend.ps1](../artifacts/composite-expanded-20261022/final-backend.ps1)，日志为 [final-backend.log](../artifacts/composite-expanded-20261022/final-backend.log)。严格 XML 的独立清单在 [verification.json](../artifacts/xml-documentation-verification/20261022-183814-727-eb353413/verification.json)，未覆盖跟踪中的 XML 文件。浏览器地址为 `http://127.0.0.1:16173`，真实 Host 为 `http://127.0.0.1:16187`；构建目标文档路径由 artifact 下 `Documentation.targets` 隔离。

初始红灯为 4 个独立向量全部因不支持 Expanded 而失败；实现过程中修正了连接、压缩与定位符差异。最终 40 个 Expanded 专项测试覆盖 4 组独立完整矩阵（Zint 2.15.0 测试 14/30/53/57，含最大 32 行 CC-B）、23 组独立线性解码、FNC1 位流、四向像素、非法输入及资源恢复。旧 Composite 与普通 Expanded 定向回归通过，并纳入上述完整核心测试。

首次新浏览器用例因示例符号旋转后超出单元格而失败（1 通过 / 1 失败）；修正验收对象为 0°、1 dot 模块并显式编辑数据后，两项均通过。生产边界校验保持。首次报告 [expanded-e2e-first-failure.json](../artifacts/composite-expanded-20261022/expanded-e2e-first-failure.json) 与修正后报告 [expanded-e2e-final.json](../artifacts/composite-expanded-20261022/expanded-e2e-final.json) 分别保留。

## 浏览器可视检查

流程：空白模板 → Composite → Expanded → 编辑主次数据/模块宽度 → 引擎预览；另由 E2E 验证 CPCL 和项目保存导入。当前会话未提供 Browser 插件，使用项目已有 Playwright，未安装新依赖。

| 检查 | 结果 |
| --- | --- |
| 地址/标题、非空页面 | 正确，`Bing.EasyPrint 模板设计器` |
| 框架错误覆盖层 | 0 |
| 页面/控制台错误与警告 | 应用错误 0、警告 0；测试终端有既有 NO_COLOR 提示 |
| 桌面 1440×900 / 手机 390×844 | 属性选项可见，手机属性抽屉可打开，无横向溢出 |
| 实际交互 | Host 能力加载成功，模块宽度 1，实际尺寸预览成功 |

[可视检查记录](../artifacts/composite-expanded-20261022/visual-qa.json) 指向工作区外的 desktop/mobile PNG。画布展示布局占位，不是可扫码位图；像素正确性由核心矩阵和光栅测试承担。

## 边界与后续状态

| 状态 | 事项、证据和后续验收 |
| --- | --- |
| CLOSED | E1–E5：编码、模板/Provider、Host/设计器、案例文档及最终集成均已通过 |
| BLOCKED_EXTERNAL | 真实打印机：按型号/固件/DPI 验证字体、纸宽与静区、进纸、切纸、扫码主次数据、取消后重印和断连恢复。无设备证据，本地通过不表示完成 |
| ACCEPTED_LIMITATION | 不实现设备传输、驱动、状态查询或完整 Provider 插件扩展；ESC/POS Native 不支持 Composite |
| DEFERRED | Stacked Composite、多行高度规则和其他线性组件为下一阶段；不将单行 Expanded 的通过外推到它们 |
| DEFERRED | 保留五 TFM，未升级图片依赖大版本；构建中的 ImageSharp 与旧 Microsoft.NETCore.App/TFM 告警仍在 |
| DEFERRED | 普通 Expanded 保留历史压缩首位和五字符定位符输出；本次通过证据只确认新增 Composite 的标准路径，普通入口的兼容性迁移需另行评估 |
| ACCEPTED_LIMITATION | ZXing 0.16.11 对字母模式 FNC1 后的数字模式恢复存在解码限制：`(10)ABC(21)123` 读回截断。该输入仍以完整标准位流与括号等价测试验证；未为适应解码器改写生产码图。完整扫码还需独立设备 |

参考：[固定 Zint 源码](https://github.com/zint/zint/tree/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend)、[ZXing.Net GeneralAppIdDecoder](https://github.com/micjahn/ZXing.Net/blob/master/Source/lib/oned/rss/expanded/decoders/GeneralAppIdDecoder.cs)。完整浏览器报告见 [web-e2e-final.json](../artifacts/composite-expanded-20261022/web-e2e-final.json)。差异、UTF-8 和相对链接检查通过；无跟踪 XML 或生成物被覆盖。本计划 OPEN_ACTIONABLE=0，停止本阶段实现，未运行大型性能矩阵。
