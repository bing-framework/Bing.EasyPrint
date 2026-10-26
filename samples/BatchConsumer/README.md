# BatchConsumer 批量 PDF/SVG 案例

此 .NET 6 命令行示例按 25 条记录分块调用 `POST /api/designer/batch`，并把各块结果合并为一个 ZIP。示例请求显式发送 `exportFormats: ["pdf", "svg"]`，所以它演示的是可选导出；不传 `exportFormats` 的其他批量调用继续使用原有默认输出。

## 运行

先启动启用了 `batch-export` 能力的 DesignerHost。消费者为每个批量请求显式选择 ESC/POS Raster，因为 PDF 目前只支持该协议与布局；需要 SVG 的模板还必须包含至少一个软件可编码的条码元素。下面使用仓库中的 `samples/Phase5/export-template.json`，它包含 Code128 和 QR。

消费者通过 `X-EasyPrint-Required-Features: batch-export` 与 Host 协商。旧 Host 不提供该能力时会拒绝请求，消费者报告响应错误并以非零状态退出；不会把请求静默降级为普通输出。`pdf` 和 `svg` 则由请求体中的 `exportFormats` 显式选择，不改变未请求这些格式时的旧批量默认行为。请求同时显式发送 `protocol: "EscPos"` 和 `escPosLayoutMode: "Raster"`，以满足 PDF 布局约束。

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

命令成功后，ZIP 包含 Host 返回的 `records/...` 文件、合并后的 `manifest.json` 和 `errors.csv`。消费者失败时返回非零退出码；Host 的错误行仍保存在 manifest 与 CSV 中。

## Manifest

`manifest.json` 保留 Host 的逐记录结果及每条记录原有的 `files` 数组，并增加顶层 `files` 汇总 PDF/SVG。顶层每个文件项复制 Host 文件项字段，并增加 `recordIndex`，其值取自对应 Host 记录的 `index`，用于直接关联 ZIP 文件与记录。

Host 文件项可包含 `file`、`format`、`pageIndex`、`sourcePageIndex`、`elementIndex`、`copyIndex`、`pageCount`、`copies`、`segmentIndex`、`kind` 和 `placement`。格式不同，字段会不同：PDF 项返回 `format`、`pageCount` 和 `copies`；SVG 项返回 `format`、页/来源页、元素及副本索引。记录级 `copies` 和 `pageCount` 仍在对应的 `records[]` 项中。消费者保留这些 Host 字段和值，不为缺省字段推算替代值。

顶层汇总中的 `file` 是 ZIP 内相对路径，例如 `records/000000/exports/pdf/record.pdf` 或 `records/000000/exports/svg/copy-0001/page-0001-element-0002.svg`；具体 SVG 项数取决于模板中的软件条码元素、页数和份数。PDF 是页面光栅导出，SVG 只包含软件条码矢量，不含设备 HRI。
