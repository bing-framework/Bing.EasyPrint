# Phase 5 案例

这些文件分别演示 Host 可直接消费的打印模板，以及设计器保存的 v2 项目：

- export-template.json 是 400 × 400 dots 的裸打印模板：标题位于 (20,20)，Code 128 位于 (20,80) 且 `NarrowModuleWidth=2`；承载 GS1 Digital Link URI 的普通 QR 位于 (20,180) 且 `UnitWidth=2`，与 Code 128 上下分行。
- project-v2-editor-state.json 是与打印模板内容相同的 DesignerProject v2 envelope，含每个节点的稳定 ID 与锁映射；标题节点已锁定。
- [Phase 5 导出与编辑状态指南](../../docs/phase5-export-editor-guide.md)包含 PDF/SVG、能力协商和批量请求示例。

PDF 示例必须将协议设为 EscPos、布局模式设为 Raster。SVG 示例应导出软件可编码的条码/码图元素；SVG 是按共享静区和旋转生成的矢量码图，不含设备 HRI。批量 exportFormats 是 opt-in，省略时不增加导出文件；记录级混合失败会在 manifest 中标为 partial/all_failed，取消或累计预算拒绝会中止归档。单独导出模板时不保存 editorState；只有项目 v2 保存编辑器身份和锁。

## 静态检查

2026-10-26 执行以下 PowerShell 检查：

~~~powershell
$ErrorActionPreference = 'Stop'
$template = Get-Content samples/Phase5/export-template.json -Raw | ConvertFrom-Json -AsHashtable
$project = Get-Content samples/Phase5/project-v2-editor-state.json -Raw | ConvertFrom-Json -AsHashtable
$sameTemplate = (ConvertTo-Json -InputObject $template -Depth 100 -Compress) -ceq (ConvertTo-Json -InputObject $project.template -Depth 100 -Compress)
$paths = @('Rows[0]', 'Rows[0].Cells[0]', 'Rows[0].Cells[0].Elements[0]', 'Rows[0].Cells[0].Elements[1]', 'Rows[0].Cells[0].Elements[2]')
$difference = @(Compare-Object -ReferenceObject @($paths | Sort-Object) -DifferenceObject @($project.editorState.nodeStates.Keys | Sort-Object))
$ids = @($project.editorState.nodeStates.Values | ForEach-Object { $_.id } | Sort-Object -Unique)
$locked = @($project.editorState.nodeStates.Values | Where-Object { $_.locked }).Count
if (-not $sameTemplate -or $difference.Count -ne 0 -or $ids.Count -ne 5 -or $locked -ne 1) { throw 'Phase 5 JSON check failed' }
foreach ($state in $project.editorState.nodeStates.Values) {
  if ($state.id -isnot [string] -or $state.locked -isnot [bool]) { throw 'Invalid node state' }
}
'PASS: JSON files=2; traversable nodes={0}; unique IDs={1}; locked nodes={2}' -f $paths.Count, $ids.Count, $locked
~~~

输出：PASS，JSON files=2；traversable nodes=5；unique IDs=5；locked nodes=1。另将 PDF、SVG 和批量请求对象序列化后重新解析，3 种请求形状均通过。该检查不启动 Host 或前端测试。
