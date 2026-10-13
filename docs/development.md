# 开发与验证

## 项目与依赖

| 项目 | 目标框架 | 主要依赖 |
| --- | --- | --- |
| `src/Bing.EasyPrint` | net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1 | ImageSharp、ImageSharp.Drawing、ZXing.Net |
| `src/Bing.EasyPrint.Templates` | 同核心库 | 核心库、Newtonsoft.Json、ZXing.Net |
| `tools/DesignerHost` | net8.0 | Templates、ASP.NET Core |
| `web/Bing.EasyPrint.Designer` | Node/Vite | Vue、Konva、JSZip、Lucide |

测试项目位于 `tests/Bing.EasyPrint.Tests`、`tests/DesignerHost.Tests` 和 `tests/PackageConsumer`；性能/资源探针位于 `tools/PerformanceProbe`。

## 常用命令

```powershell
dotnet build src/Bing.EasyPrint/Bing.EasyPrint.csproj
dotnet build src/Bing.EasyPrint.Templates/Bing.EasyPrint.Templates.csproj
dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f net6.0
./tools/Verify-DesignerHost.ps1
dotnet run --project samples/Templates -- artifacts/template-samples
```

```powershell
cd web/Bing.EasyPrint.Designer
npm ci
npm run build
npm run test
npm run test:e2e
```

具体改动应优先运行对应的定向测试，再按影响范围扩展到项目测试和多目标框架构建。文档改动使用 Markdown、链接和编码检查即可。

`Verify-DesignerHost.ps1` 将 Host 测试输出和 TRX 写入独立的 `artifacts` 目录，并串行执行构建。直接运行 `dotnet test tests/DesignerHost.Tests/DesignerHost.Tests.csproj` 也受支持，但工作区内已有嵌套 Host 生成物时，Web SDK 的默认内容 glob 可能产生重复内容错误。

## 验证重点

- 协议命令：精确字节、输入校验、Build 幂等、失败后的状态恢复和取消。
- 统一模型：能力预检、元素路径、旋转与页面边界、输出预算。
- 模板：版本往返、绑定、分页、资源、布局轨迹和四协议输出。
- Host：严格 DTO、契约协商、结构化错误、请求/工作内存/并发限制。
- Web：Vitest 覆盖状态和归档逻辑；Playwright 覆盖 Host 联动流程。
- 包：`tests/PackageConsumer` 验证 NuGet 消费面。

## 生成物与环境注意事项

- `obj`、`bin`、`output`、`artifacts`、`node_modules`、`.pnpm-store` 和 `.tmp-*` 不属于源文档或功能代码，不应纳入提交。
- 前端仓库已跟踪 `package-lock.json`，使用 npm 和 `npm ci`；不要同时提交其他包管理器生成的锁文件。
- 旧目标框架可能需要本机安装相应 targeting pack/runtime。
- NuGet 漏洞索引不可用时可能出现 `NU1900`；不能把网络失败当作测试通过或功能失败。
- `tools/DesignerHost` 下遗留的嵌套 `artifacts` 可能被 Web SDK 默认内容 glob 扫描并导致重复内容错误；应清理生成物或在一次性诊断命令中禁用默认内容项。
- 历史结果见[多协议验证记录](multi-protocol-verification.md)，当前分支状态必须以本次实际命令为准。
- 真实打印机联调不属于本地自动化测试结果。
