import { describe, expect, it } from 'vitest'
import { cloneBatchEditorState, emptyBatchEditorState } from './batchEditorState'

describe('batch editor export state', () => {
  it('defaults to legacy output and preserves explicit export choices across snapshots', () => {
    const empty = emptyBatchEditorState()
    expect(empty.exportFormats).toEqual([])

    const state = { ...empty, exportFormats: ['pdf', 'svg'] as const }
    const snapshot = cloneBatchEditorState(state)
    expect(snapshot.exportFormats).toEqual(['pdf', 'svg'])
    expect(snapshot.exportFormats).not.toBe(state.exportFormats)
  })
})
