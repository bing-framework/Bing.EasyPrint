export type BatchEditorState = {
  fileName: string
  rawText: string
  format: 'csv' | 'json' | 'jsonl'
  mapping: Readonly<Record<string, string>>
  selectedIndices: readonly number[]
  copiesField: string
  exportFormats: readonly ('pdf' | 'svg')[]
}

export function emptyBatchEditorState(): BatchEditorState {
  return { fileName: '', rawText: '', format: 'csv', mapping: {}, selectedIndices: [], copiesField: '', exportFormats: [] }
}

export function cloneBatchEditorState(value: BatchEditorState): BatchEditorState {
  return { ...value, mapping: value.mapping, selectedIndices: value.selectedIndices,
    exportFormats: [...value.exportFormats] }
}
