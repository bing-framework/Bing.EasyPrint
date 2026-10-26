import { describe, expect, it } from 'vitest'
import { SnapshotHistory } from './history'

describe('snapshot history', () => {
  it('undoes, redoes, and discards future edits after a branch', () => {
    const history = new SnapshotHistory({ x: 1 })
    history.record({ x: 2 })
    history.record({ x: 3 })
    expect(history.undo()).toEqual({ x: 2 })
    history.record({ x: 4 })
    expect(history.canRedo).toBe(false)
    expect(history.undo()).toEqual({ x: 2 })
    expect(history.undo()).toEqual({ x: 1 })
  })

  it('stores detached snapshots rather than live object references', () => {
    const source = { rows: [{ height: 10 }] }
    const history = new SnapshotHistory(source)
    source.rows[0].height = 50
    history.record(source)
    expect(history.undo()).toEqual({ rows: [{ height: 10 }] })
  })

  it('uses immutable shared payloads and evicts oldest snapshots at the byte limit', () => {
    type Value = { template: { x: number }; raw: string; selection: number[] }
    const clone = (value: Value): Value => ({ template: structuredClone(value.template), raw: value.raw, selection: value.selection })
    const measureBytes = (snapshots: readonly Value[]) => snapshots.slice(0, -1)
      .reduce((total, value) => total + value.raw.length + value.selection.length * 8 + 8, 0)
    const history = new SnapshotHistory<Value>({ template: { x: 0 }, raw: 'x'.repeat(12), selection: [1, 2] }, {
      limit: 80, maxBytes: 40, clone, measureBytes,
      equals: (left, right) => left.template.x === right.template.x && left.raw === right.raw && left.selection === right.selection,
    })
    const sharedSelection = [1, 2]
    history.record({ template: { x: 1 }, raw: 'y'.repeat(12), selection: sharedSelection })
    history.record({ template: { x: 2 }, raw: 'z'.repeat(12), selection: sharedSelection })

    expect(history.length).toBeLessThanOrEqual(3)
    expect(history.undo()?.template.x).toBe(1)
  })
})
