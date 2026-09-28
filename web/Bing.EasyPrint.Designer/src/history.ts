import { cloneTemplate } from './model'

export class SnapshotHistory<T> {
  private snapshots: T[]
  private cursor = 0

  constructor(initial: T, private readonly limit = 80) {
    this.snapshots = [cloneTemplate(initial)]
  }

  get canUndo(): boolean { return this.cursor > 0 }
  get canRedo(): boolean { return this.cursor < this.snapshots.length - 1 }

  record(value: T): void {
    const snapshot = cloneTemplate(value)
    if (JSON.stringify(snapshot) === JSON.stringify(this.snapshots[this.cursor])) return
    this.snapshots = this.snapshots.slice(0, this.cursor + 1)
    this.snapshots.push(snapshot)
    if (this.snapshots.length > this.limit) this.snapshots.shift()
    this.cursor = this.snapshots.length - 1
  }

  undo(): T | undefined {
    if (!this.canUndo) return undefined
    this.cursor -= 1
    return cloneTemplate(this.snapshots[this.cursor])
  }

  redo(): T | undefined {
    if (!this.canRedo) return undefined
    this.cursor += 1
    return cloneTemplate(this.snapshots[this.cursor])
  }
}
