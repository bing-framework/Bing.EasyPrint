export type ArrangementMode = 'align-left' | 'align-right' | 'align-center-x' | 'align-top' | 'align-bottom' | 'align-center-y' | 'distribute-x' | 'distribute-y'

export type ArrangementItem = {
  path: string
  containerPath: string
  x: number
  y: number
  width: number
  height: number
  absolute: boolean
}

export type ArrangementPosition = { path: string; x: number; y: number }

export function arrangeAbsoluteSelection(items: ArrangementItem[], mode: ArrangementMode): ArrangementPosition[] {
  if (items.length < (mode.startsWith('distribute-') ? 3 : 2))
    throw new Error(mode.startsWith('distribute-') ? '均分至少需要三个元素。' : '对齐至少需要两个元素。')
  const container = items[0]!.containerPath
  if (!container || items.some((item) => !item.absolute || item.containerPath !== container))
    throw new Error('只能排列同一容器内的绝对定位元素。')
  if (items.some((item) => !item.path || ![item.x, item.y, item.width, item.height].every(Number.isFinite) ||
    item.width <= 0 || item.height <= 0)) throw new Error('排列元素的占位框无效。')

  const positions = new Map(items.map((item) => [item.path, { x: item.x, y: item.y }]))
  const horizontal = mode === 'align-left' || mode === 'align-right' || mode === 'align-center-x' || mode === 'distribute-x'
  const start = horizontal ? 'x' : 'y'
  const extent = horizontal ? 'width' : 'height'
  const end = (item: ArrangementItem) => item[start] + item[extent]

  if (mode.startsWith('align-')) {
    const low = Math.min(...items.map((item) => item[start]))
    const high = Math.max(...items.map(end))
    const target = mode === 'align-left' || mode === 'align-top' ? low
      : mode === 'align-right' || mode === 'align-bottom' ? high
        : (low + high) / 2
    for (const item of items) {
      const value = mode === 'align-left' || mode === 'align-top' ? target
        : mode === 'align-right' || mode === 'align-bottom' ? target - item[extent]
          : target - item[extent] / 2
      positions.get(item.path)![start] = Math.round(value)
    }
    return items.map((item) => ({ path: item.path, ...positions.get(item.path)! }))
  }

  const sorted = [...items].sort((left, right) => left[start] - right[start] || left.path.localeCompare(right.path))
  const first = sorted[0]!
  const last = sorted[sorted.length - 1]!
  const gap = (end(last) - first[start] - sorted.reduce((sum, item) => sum + item[extent], 0)) / (sorted.length - 1)
  let precedingExtent = first[extent]
  for (let index = 1; index < sorted.length - 1; index += 1) {
    const item = sorted[index]!
    positions.get(item.path)![start] = Math.round(first[start] + precedingExtent + gap * index)
    precedingExtent += item[extent]
  }
  return items.map((item) => ({ path: item.path, ...positions.get(item.path)! }))
}
