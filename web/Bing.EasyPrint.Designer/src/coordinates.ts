import { MAX_QUIET_ZONE_DOTS, type QuietZone, type RotationAngle, type TemplateElement } from './model'

export const PRINT_DPI = 200
export const DOTS_PER_MM = PRINT_DPI / 25.4

function dotsPerMm(dpi: number): number {
  if (!Number.isFinite(dpi) || dpi <= 0) throw new Error('DPI 必须是有限正数。')
  return dpi / 25.4
}

export function dotsToMm(dots: number, dpi = PRINT_DPI): number {
  return Math.round((dots / dotsPerMm(dpi)) * 100) / 100
}

export function mmToDots(mm: number, dpi = PRINT_DPI): number {
  if (!Number.isFinite(mm)) throw new Error('毫米值必须是有限数字。')
  return Math.round(mm * dotsPerMm(dpi))
}

export type BarcodeSize = { width: number; height: number }

/** 按顺时针旋转后的占位框尺寸；输入宽高始终是旋转前尺寸。 */
export function rotatedSize(width: number, height: number, rotation: RotationAngle = 0): BarcodeSize {
  return rotation === 90 || rotation === 270
    ? { width: height, height: width }
    : { width, height }
}

export type BarcodeQuietZoneContext = {
  barcodeType?: number
  lineWidth?: number
  narrowModuleWidth?: number
  protocol?: 'Cpcl' | 'EscPos' | 'Tspl' | 'Zpl'
  reserveHeight?: number
  /** 已有 EndCoordinate 是已包含 HRI 的外占位框。 */
  preserveExistingHri?: boolean
}

/** 将条码原始宽高换算为画布/属性面板使用的旋转后占位尺寸。 */
function quietZoneSide(value: number | undefined): number {
  return Number.isInteger(value) && value! >= 0 && value! <= MAX_QUIET_ZONE_DOTS ? value! : 0
}

export function quietZoneHorizontalExtent(quietZone: QuietZone | null | undefined): number {
  if (!quietZone) return 0
  return quietZoneSide(quietZone.Left) + quietZoneSide(quietZone.Right)
}

export function quietZoneVerticalExtent(quietZone: QuietZone | null | undefined): number {
  if (!quietZone) return 0
  return quietZoneSide(quietZone.Top) + quietZoneSide(quietZone.Bottom)
}

function effectiveModuleWidth(context: BarcodeQuietZoneContext): number {
  const lineWidth = Number.isInteger(context.lineWidth) && (context.lineWidth ?? 0) > 0 ? context.lineWidth! : 2
  if (context.protocol === 'Cpcl') return lineWidth + 1
  if (Number.isInteger(context.narrowModuleWidth) && (context.narrowModuleWidth ?? 0) > 0)
    return context.narrowModuleWidth!
  return lineWidth + 1
}

/** 返回协议按制式计算的条码左右默认静区，单位为打印点。 */
export function barcodeDefaultQuietZone(context: BarcodeQuietZoneContext = {}): QuietZone {
  const narrow = effectiveModuleWidth(context)
  const sides: Record<number, [number, number]> = {
    4: [7, 7], // EAN-8 商品条码
    5: [11, 7], // EAN-13 商品条码
    6: [9, 9], // UPC-A 商品条码
    7: [9, 7], // UPC-E 商品条码
  }
  const [left, right] = sides[context.barcodeType ?? 0] ?? [10, 10]
  return { Left: left * narrow, Right: right * narrow, Top: 0, Bottom: 0 }
}

/** 返回二维码传统默认静区，每边为四个模块。 */
export function qrDefaultQuietZone(unitWidth = 4): QuietZone {
  const moduleWidth = Number.isInteger(unitWidth) && unitWidth > 0 ? unitWidth : 4
  const side = 4 * moduleWidth
  return { Left: side, Top: side, Right: side, Bottom: side }
}

/** 比较两组静区配置；null/undefined 使用传入的传统默认值。 */
export function quietZoneExtentDelta(previous: QuietZone | null | undefined,
  current: QuietZone | null | undefined, legacyDefault: QuietZone = {}): BarcodeSize {
  const previousZone = previous === undefined || previous === null ? legacyDefault : previous
  const currentZone = current === undefined || current === null ? legacyDefault : current
  return {
    width: quietZoneHorizontalExtent(currentZone) - quietZoneHorizontalExtent(previousZone),
    height: quietZoneVerticalExtent(currentZone) - quietZoneVerticalExtent(previousZone),
  }
}

/** 将旋转后的静区差量应用到指定的自动尺寸轴。 */
export function applyEndCoordinateDelta(end: { X: number; Y: number }, delta: BarcodeSize,
  updateWidth: boolean, updateHeight: boolean): void {
  if (updateWidth) end.X += delta.width
  if (updateHeight) end.Y += delta.height
}

/** 重算条码端点时只更新已声明的轴，自动轴继续沿用已有外占位。 */
export function applyDeclaredBarcodeEndCoordinate(start: { X: number; Y: number }, end: { X: number; Y: number },
  occupied: BarcodeSize, widthAuto: boolean, heightAuto: boolean, rotation: RotationAngle = 0): void {
  const quarterTurn = rotation === 90 || rotation === 270
  const updateX = quarterTurn ? !heightAuto : !widthAuto
  const updateY = quarterTurn ? !widthAuto : !heightAuto
  if (updateX) end.X = start.X + occupied.width
  if (updateY) end.Y = start.Y + occupied.height
}

export function barcodeOccupiedSize(width: number, height: number, reserveHeight = 0,
  rotation: RotationAngle = 0, quietZone?: QuietZone | null): BarcodeSize {
  const reserve = Math.max(0, reserveHeight)
  // 未配置静区时保留旧模板的旋转前尺寸规则：旧布局只在旋转条码时追加 HRI。
  const legacyReserve = quietZone === undefined || quietZone === null ? (rotation === 0 ? 0 : reserve) : reserve
  const occupiedHeight = height + legacyReserve + quietZoneVerticalExtent(quietZone)
  return rotation === 90 || rotation === 270
    ? { width: occupiedHeight, height: width }
    : { width, height: occupiedHeight }
}

/** 将旋转后占位尺寸还原为模板条码的原始宽高；调用方负责应用自身的最小尺寸约束。 */
export function barcodeRawSizeFromOccupied(width: number, height: number, reserveHeight = 0,
  rotation: RotationAngle = 0, quietZone?: QuietZone | null): BarcodeSize {
  const reserve = Math.max(0, reserveHeight)
  const extraHeight = (quietZone === undefined || quietZone === null ? (rotation === 0 ? 0 : reserve) : reserve) +
    quietZoneVerticalExtent(quietZone)
  return rotation === 90 || rotation === 270
    ? { width: height, height: width - extraHeight }
    : { width, height: height - extraHeight }
}

/** 自动尺寸没有模板 Width 时，把显式左右静区加到编码体宽度；已配置 Width 时不重复增加。 */
export function barcodeAutoOccupiedSize(width: number, height: number, reserveHeight = 0,
  rotation: RotationAngle = 0, quietZone?: QuietZone | null, widthIncludesQuiet = false,
  context: BarcodeQuietZoneContext = {}): BarcodeSize {
  const legacyZone = quietZone === undefined || quietZone === null ? barcodeDefaultQuietZone(context) : quietZone
  const reservedWidth = widthIncludesQuiet ? width : width + quietZoneHorizontalExtent(legacyZone)
  return barcodeOccupiedSize(reservedWidth, height, reserveHeight, rotation, quietZone)
}

/** 计算静区修改对已有旋转后外占位框的增量，供自动尺寸元素写回 EndCoordinate。 */
export function barcodeQuietZoneOccupiedDelta(rotation: RotationAngle,
  previous: QuietZone | null | undefined, current: QuietZone | null | undefined, widthAuto = true,
  context: BarcodeQuietZoneContext = {}): BarcodeSize {
  const legacyZone = barcodeDefaultQuietZone(context)
  const quietDelta = quietZoneExtentDelta(previous, current, legacyZone)
  const reserve = Math.max(0, context.reserveHeight ?? 0)
  const legacyHri = context.preserveExistingHri
    ? reserve
    : previous === undefined || previous === null ? (rotation === 0 ? 0 : reserve) : reserve
  const currentHri = context.preserveExistingHri
    ? reserve
    : current === undefined || current === null ? (rotation === 0 ? 0 : reserve) : reserve
  const widthDelta = widthAuto ? quietDelta.width : 0
  const heightDelta = quietDelta.height + currentHri - legacyHri
  return rotation === 90 || rotation === 270
    ? { width: heightDelta, height: widthDelta }
    : { width: widthDelta, height: heightDelta }
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
