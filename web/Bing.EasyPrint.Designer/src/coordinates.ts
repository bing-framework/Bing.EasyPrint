import type { TemplateElement } from './model'

export const PRINT_DPI = 200
export const DOTS_PER_MM = PRINT_DPI / 25.4

export function dotsToMm(dots: number): number {
  return Math.round((dots / DOTS_PER_MM) * 100) / 100
}

export function mmToDots(mm: number): number {
  if (!Number.isFinite(mm)) throw new Error('毫米值必须是有限数字。')
  return Math.round(mm * DOTS_PER_MM)
}

export function fitScale(width: number, height: number, maxWidth: number, maxHeight: number, padding = 80): number {
  if ([width, height, maxWidth, maxHeight].some((value) => !Number.isFinite(value) || value <= 0)) return 1
  return Math.max(0.12, Math.min(1.5, (maxWidth - padding) / width, (maxHeight - padding) / height))
}

export function snapCoordinate(value: number, step = 4): number {
  return Math.round(value / step) * step
}

export function snapDeltaToGrid(position: number, delta: number, step = 4): number {
  return snapCoordinate(position + delta, step) - position
}

export function getNudgeDelta(key: string, step = 1): { dx: number; dy: number } | undefined {
  if (key === 'ArrowLeft') return { dx: -step, dy: 0 }
  if (key === 'ArrowRight') return { dx: step, dy: 0 }
  if (key === 'ArrowUp') return { dx: 0, dy: -step }
  if (key === 'ArrowDown') return { dx: 0, dy: step }
  return undefined
}

export function translateElement(element: TemplateElement, dx: number, dy: number): boolean {
  if (!Number.isInteger(dx) || !Number.isInteger(dy)) return false
  const start = element.StartCoordinate ?? { X: 0, Y: 0 }
  const end = element.Type === 5 ? element.LineEndCoordinate : undefined
  const appliedX = Math.max(dx, -Math.min(start.X, end?.X ?? start.X))
  const appliedY = Math.max(dy, -Math.min(start.Y, end?.Y ?? start.Y))
  if (!appliedX && !appliedY) return false
  element.StartCoordinate = { X: start.X + appliedX, Y: start.Y + appliedY }
  if (element.EndCoordinate) {
    element.EndCoordinate.X += appliedX
    element.EndCoordinate.Y += appliedY
  }
  if (end) {
    end.X += appliedX
    end.Y += appliedY
  }
  return true
}

export function materializeElementPosition(element: TemplateElement,
  rendered: { x: number; y: number; width: number }, container: { x: number; y: number; width: number }, horizontalMove = false): boolean {
  if (element.StartCoordinate) return true
  const margin = element.Margin
  let x = rendered.x - container.x - (margin?.Left ?? 0)
  const y = rendered.y - container.y - (margin?.Top ?? 0)
  if (element.Type === 1 && element.TextAlignment && !horizontalMove) x = 0
  if (![x, y, container.width].every(Number.isInteger) || x < 0 || y < 0 || container.width <= 0) return false
  if (element.Type === 1 && element.TextAlignment && horizontalMove) element.TextAlignment = 0
  element.StartCoordinate = { X: x, Y: y }
  return true
}

export type AlignmentBounds = { x: number; y: number; width: number; height: number }

export function getAlignmentSnap(moving: AlignmentBounds, others: AlignmentBounds[], threshold = 6): {
  dx: number; dy: number; x?: number; y?: number
} {
  const nearest = (movingPoints: number[], targetPoints: number[]) => {
    let result: { delta: number; target: number } | undefined
    for (const target of targetPoints) for (const point of movingPoints) {
      const delta = target - point
      if (Math.abs(delta) <= threshold && (!result || Math.abs(delta) < Math.abs(result.delta))) result = { delta, target }
    }
    return result
  }
  const x = nearest(
    [moving.x, moving.x + moving.width / 2, moving.x + moving.width],
    others.flatMap((item) => [item.x, item.x + item.width / 2, item.x + item.width]),
  )
  const y = nearest(
    [moving.y, moving.y + moving.height / 2, moving.y + moving.height],
    others.flatMap((item) => [item.y, item.y + item.height / 2, item.y + item.height]),
  )
  return { dx: x?.delta ?? 0, dy: y?.delta ?? 0, x: x?.target, y: y?.target }
}
