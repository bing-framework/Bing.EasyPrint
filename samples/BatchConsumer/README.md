# BatchConsumer 批量 PDF/SVG 案例

此 .NET 6 命令行示例按 25 条记录分块调用 `POST /api/designer/batch`，并把各块结果合并为一个 ZIP。示例请求显式发送 `exportFormats: ["pdf", "svg"]`，所以它演示的是可选导出；不传 `exportFormats` 的其他批量调用继续使用原有默认输出。

## 运行

先启动同时提供 `batch-export`、`page-pdf-export`、`barcode-svg-export` 三项能力的 DesignerHost。消费者为每个批量请求显式选择 ESC/POS Raster，因为 PDF 目前只支持该协议与布局；需要 SVG 的模板还必须包含至少一个软件可编码的条码元素。下面使用仓库中的 `samples/Phase5/export-template.json`，它包含 Code128 和 QR。

每个分块请求只发送一次 `X-EasyPrint-Required-Features: batch-export,page-pdf-export,barcode-svg-export`。成功响应还必须声明 `X-EasyPrint-Contract-Version: 1`，且 `X-EasyPrint-Features` 必须包含全部三项能力；因此忽略请求头的旧 Host 也会被拒绝。`pdf` 和 `svg` 仍由请求体中的 `exportFormats` 显式选择，不改变未请求这些格式时的旧批量默认行为。请求同时显式发送 `protocol: "EscPos"` 和 `escPosLayoutMode: "Raster"`，以满足 PDF 布局约束。

在仓库根目录的 PowerShell 中准备两条数据并运行消费者。将 Host 地址改为当前监听地址：

```powershell
$recordsPath = Join-Path $env:TEMP 'easyprint-phase5-records.json'
@'
[
  { "Code": "BATCH-001", "DigitalLink": "https://id.gs1.org/01/09520123456788/10/LOT-2026-A" },
  { "Code": "BATCH-002", "DigitalLink": "https://id.gs1.org/01/09520123456788/10/LOT-2026-B" }
]
'@ | Set-Content -LiteralPath $recordsPath -Encoding utf8

dotnet run --project samples/BatchConsumer/BatchConsumer.csproj -- `
  http://127.0.0.1:5187 `
  samples/Phase5/export-template.json `
  $recordsPath `
  (Join-Path $PWD 'phase5-batch-result.zip')
```

在发布结果 ZIP 前，消费者会校验每个分块的 manifest 记录数；每条 `completed` 记录必须列出至少一个 PDF 和 SVG，且对应 ZIP 项必须存在、非空。失败记录不要求导出。协议或文件校验失败会返回非零状态，不打印 `completed`，并保留已有目标文件；新结果只在所有分块校验成功后替换目标路径。

校验通过后，ZIP 包含 Host 返回的 `records/...` 文件、合并后的 `manifest.json` 和 `errors.csv`。`partial` 与 `all_failed` 仍会生成结果 ZIP 并返回非零退出码；Host 的错误行仍保存在 manifest 与 CSV 中。只有所有记录均完成时消费者才返回 0。

## 回归验证

从仓库根目录运行。脚本会构建样例（可用 `-NoBuild` 复用刚完成的构建），启动本地假 Host，并以独立进程执行 BatchConsumer：

```powershell
dotnet build samples/BatchConsumer/BatchConsumer.csproj --configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Verify-BatchConsumer.ps1 -NoBuild
```

回归覆盖旧 Host 明确拒绝、Host 忽略请求头且缺少响应能力、错误契约版本、缺失或未知记录状态、缺失或为空的 PDF/SVG 与 ZIP 项、26 条记录跨 25 条分块边界、`partial` 和 `all_failed`。请求头、进程输出、生成 ZIP 和运行摘要保存在 `artifacts/optimization-expansion-20261025/post-review-20261026/batch-consumer/` 下的独立运行目录中。

## Manifest

`manifest.json` 保留 Host 的逐记录结果及每条记录原有的 `files` 数组，并增加顶层 `files` 汇总 PDF/SVG。顶层每个文件项复制 Host 文件项字段，并增加 `recordIndex`，其值取自对应 Host 记录的 `index`，用于直接关联 ZIP 文件与记录。

Host 文件项可包含 `file`、`format`、`pageIndex`、`sourcePageIndex`、`elementIndex`、`copyIndex`、`pageCount`、`copies`、`segmentIndex`、`kind` 和 `placement`。格式不同，字段会不同：PDF 项返回 `format`、`pageCount` 和 `copies`；SVG 项返回 `format`、页/来源页、元素及副本索引。记录级 `copies` 和 `pageCount` 仍在对应的 `records[]` 项中。消费者保留这些 Host 字段和值，不为缺省字段推算替代值。

顶层汇总中的 `file` 是 ZIP 内相对路径，例如 `records/000000/exports/pdf/record.pdf` 或 `records/000000/exports/svg/copy-0001/page-0001-element-0002.svg`；具体 SVG 项数取决于模板中的软件条码元素、页数和份数。PDF 是页面光栅导出，SVG 只包含软件条码矢量，不含设备 HRI。
