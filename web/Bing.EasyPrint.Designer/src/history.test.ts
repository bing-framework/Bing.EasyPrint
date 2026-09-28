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
})
