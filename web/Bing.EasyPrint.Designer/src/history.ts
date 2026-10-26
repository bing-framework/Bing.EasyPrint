import { cloneTemplate } from './model'

export type SnapshotHistoryOptions<T> = {
  limit?: number
  maxBytes?: number
  clone?: (value: T) => T
  equals?: (left: T, right: T) => boolean
  measureBytes?: (snapshots: readonly T[]) => number
}

export class SnapshotHistory<T> {
  private snapshots: T[]
  private cursor = 0
  private readonly limit: number
  private readonly maxBytes: number
  private readonly cloneValue: (value: T) => T
  private readonly equals: (left: T, right: T) => boolean
  private readonly measureBytes: (snapshots: readonly T[]) => number

  constructor(initial: T, limitOrOptions: number | SnapshotHistoryOptions<T> = 80) {
    const options = typeof limitOrOptions === 'number' ? { limit: limitOrOptions } : limitOrOptions
    this.limit = options.limit ?? 80
    this.maxBytes = options.maxBytes ?? Number.POSITIVE_INFINITY
    this.cloneValue = options.clone ?? cloneTemplate
    this.equals = options.equals ?? ((left, right) => JSON.stringify(left) === JSON.stringify(right))
    this.measureBytes = options.measureBytes ?? (() => 0)
    this.snapshots = [this.cloneValue(initial)]
  }

  get canUndo(): boolean { return this.cursor > 0 }
  get canRedo(): boolean { return this.cursor < this.snapshots.length - 1 }
  get length(): number { return this.snapshots.length }

  record(value: T): void {
    const snapshot = this.cloneValue(value)
    if (this.equals(snapshot, this.snapshots[this.cursor]!)) return
    this.snapshots = this.snapshots.slice(0, this.cursor + 1)
    this.snapshots.push(snapshot)
    if (this.snapshots.length > this.limit) this.snapshots.shift()
    this.cursor = this.snapshots.length - 1
    while (this.snapshots.length > 1 && this.measureBytes(this.snapshots) > this.maxBytes) {
      this.snapshots.shift()
      this.cursor -= 1
    }
  }

  undo(): T | undefined {
    if (!this.canUndo) return undefined
    this.cursor -= 1
    return this.cloneValue(this.snapshots[this.cursor]!)
  }

  redo(): T | undefined {
    if (!this.canRedo) return undefined
    this.cursor += 1
    return this.cloneValue(this.snapshots[this.cursor]!)
  }
}
