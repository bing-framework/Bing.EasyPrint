import { describe, expect, it } from 'vitest'
import {
  applyDeclaredBarcodeEndCoordinate, applyEndCoordinateDelta, barcodeAutoOccupiedSize, barcodeDefaultQuietZone, barcodeOccupiedSize,
  barcodeQuietZoneOccupiedDelta, barcodeRawSizeFromOccupied, dotsToMm, fitScale, getAlignmentSnap,
  getNudgeDelta, materializeElementPosition, mmToDots, qrDefaultQuietZone, quietZoneExtentDelta,
  rotatedSize, snapCoordinate, snapDeltaToGrid, translateElement,
} from './coordinates'
import type { TemplateElement } from './model'

describe('printer coordinate conversion', () => {
  it('uses 200 DPI and round-trips common dimensions', () => {
    expect(dotsToMm(540)).toBe(68.58)
    expect(mmToDots(70)).toBe(551)
    expect(mmToDots(dotsToMm(671))).toBe(671)
  })

  it.each([203, 300, 600])('uses the selected %d DPI without changing point values', (dpi) => {
    expect(dotsToMm(dpi, dpi)).toBe(25.4)
    expect(mmToDots(25.4, dpi)).toBe(dpi)
    expect(mmToDots(dotsToMm(671, dpi), dpi)).toBe(671)
  })

  it('rejects invalid explicit DPI values', () => {
    expect(() => dotsToMm(200, 0)).toThrow('DPI 必须是有限正数')
    expect(() => mmToDots(10, Number.NaN)).toThrow('DPI 必须是有限正数')
  })

  it('converts rotated barcode occupied dimensions and reverses the conversion', () => {
    expect(barcodeOccupiedSize(220, 46, 12, 0)).toEqual({ width: 220, height: 46 })
    expect(barcodeOccupiedSize(220, 46, 12, 90)).toEqual({ width: 58, height: 220 })
    expect(barcodeOccupiedSize(220, 46, 12, 270)).toEqual({ width: 58, height: 220 })
    expect(barcodeOccupiedSize(220, 46, 12, 180)).toEqual({ width: 220, height: 58 })
    expect(barcodeRawSizeFromOccupied(58, 220, 12, 90)).toEqual({ width: 220, height: 46 })
    expect(barcodeRawSizeFromOccupied(220, 58, 12, 180)).toEqual({ width: 220, height: 46 })
  })

  it('keeps image source dimensions separate from the rotated occupied box', () => {
    expect(rotatedSize(80, 40, 0)).toEqual({ width: 80, height: 40 })
    expect(rotatedSize(80, 40, 90)).toEqual({ width: 40, height: 80 })
    expect(rotatedSize(80, 40, 180)).toEqual({ width: 80, height: 40 })
    expect(rotatedSize(80, 40, 270)).toEqual({ width: 40, height: 80 })
  })

  it('includes explicit asymmetric barcode quiet zones before clockwise rotation', () => {
    const quietZone = { Left: 2, Top: 3, Right: 4, Bottom: 5 }
    expect(barcodeOccupiedSize(220, 46, 12, 0, quietZone)).toEqual({ width: 220, height: 66 })
    expect(barcodeOccupiedSize(220, 46, 12, 90, quietZone)).toEqual({ width: 66, height: 220 })
    expect(barcodeOccupiedSize(220, 46, 12, 270, quietZone)).toEqual({ width: 66, height: 220 })
    expect(barcodeRawSizeFromOccupied(66, 220, 12, 90, quietZone)).toEqual({ width: 220, height: 46 })
    expect(barcodeRawSizeFromOccupied(220, 66, 12, 180, quietZone)).toEqual({ width: 220, height: 46 })
  })

  it('uses protocol barcode defaults and reverses explicit quiet-zone deltas', () => {
    const quietZone = { Left: 2, Top: 3, Right: 4, Bottom: 5 }
    expect(barcodeAutoOccupiedSize(112, 46, 12, 0, quietZone)).toEqual({ width: 118, height: 66 })
    expect(barcodeAutoOccupiedSize(112, 46, 12, 0, quietZone, true)).toEqual({ width: 112, height: 66 })
    expect(barcodeQuietZoneOccupiedDelta(0, quietZone, undefined)).toEqual({ width: 54, height: -8 })
    expect(barcodeQuietZoneOccupiedDelta(90, undefined, quietZone)).toEqual({ width: 8, height: -54 })
  })

  it('matches BarcodeMetrics quiet-zone defaults for retail formats and protocol module widths', () => {
    expect(barcodeDefaultQuietZone({ barcodeType: 5, lineWidth: 2, protocol: 'Cpcl' })).toMatchObject({ Left: 33, Right: 21 })
    expect(barcodeDefaultQuietZone({ barcodeType: 4, lineWidth: 2, narrowModuleWidth: 4, protocol: 'Tspl' })).toMatchObject({ Left: 28, Right: 28 })
    expect(barcodeDefaultQuietZone({ barcodeType: 7, lineWidth: 2, narrowModuleWidth: 5, protocol: 'Zpl' })).toMatchObject({ Left: 45, Right: 35 })
  })

  it('accounts for explicit zero and the 0-degree HRI mode transition', () => {
    const context = { barcodeType: 0, lineWidth: 2, reserveHeight: 12 }
    expect(barcodeQuietZoneOccupiedDelta(0, null, {}, true, context)).toEqual({ width: -60, height: 12 })
    expect(barcodeQuietZoneOccupiedDelta(0, {}, null, true, context)).toEqual({ width: 60, height: -12 })
    expect(barcodeQuietZoneOccupiedDelta(90, null, {}, true, context)).toEqual({ width: 0, height: -60 })
    expect(barcodeQuietZoneOccupiedDelta(90, {}, null, true, context)).toEqual({ width: 0, height: 60 })
    expect(barcodeQuietZoneOccupiedDelta(0, null, {}, true, { ...context, preserveExistingHri: true }))
      .toEqual({ width: -60, height: 0 })
  })

  it('keeps a real old outer EndCoordinate while changing QR quiet zones on one automatic axis', () => {
    const end = { X: 140, Y: 210 }
    const legacy = qrDefaultQuietZone(3)
    const explicit = { Left: 2, Top: 0, Right: 4, Bottom: 0 }
    const toExplicit = quietZoneExtentDelta(legacy, explicit, legacy)
    applyEndCoordinateDelta(end, toExplicit, true, false)
    expect(end).toEqual({ X: 122, Y: 210 })
    const toDefault = quietZoneExtentDelta(explicit, null, legacy)
    applyEndCoordinateDelta(end, toDefault, true, false)
    expect(end).toEqual({ X: 140, Y: 210 })
  })

  it('maps asymmetric barcode quiet-zone deltas through a quarter turn and back', () => {
    const end = { X: 70, Y: 210 }
    const context = { barcodeType: 5, lineWidth: 2, reserveHeight: 12, preserveExistingHri: true }
    const explicit = { Left: 2, Top: 3, Right: 4, Bottom: 5 }
    const toExplicit = barcodeQuietZoneOccupiedDelta(90, null, explicit, true, context)
    applyEndCoordinateDelta(end, toExplicit, true, true)
    expect(end).toEqual({ X: 78, Y: 162 })
    const toDefault = barcodeQuietZoneOccupiedDelta(90, explicit, null, true, context)
    applyEndCoordinateDelta(end, toDefault, true, true)
    expect(end).toEqual({ X: 70, Y: 210 })

    const configuredWidthEnd = { X: 70, Y: 210 }
    const configuredWidthDelta = barcodeQuietZoneOccupiedDelta(90, null, explicit, false, context)
    applyEndCoordinateDelta(configuredWidthEnd, configuredWidthDelta, true, true)
    expect(configuredWidthEnd).toEqual({ X: 78, Y: 210 })
  })

  it('keeps an existing auto-width endpoint while editing the declared barcode height/HRI', () => {
    const start = { X: 20, Y: 30 }
    const end = { X: 240, Y: 76 }
    const occupied = barcodeOccupiedSize(0, 46, 12, 0, {})
    applyDeclaredBarcodeEndCoordinate(start, end, occupied, true, false)
    expect(end).toEqual({ X: 240, Y: 88 })

    const declaredWidthEnd = { X: 240, Y: 76 }
    applyDeclaredBarcodeEndCoordinate(start, declaredWidthEnd, barcodeOccupiedSize(220, 46, 12, 0, {}), false, true)
    expect(declaredWidthEnd).toEqual({ X: 240, Y: 76 })
  })

  it('maps declared-height edits back to the X axis for a quarter-turned auto-width barcode', () => {
    const start = { X: 20, Y: 30 }
    const end = { X: 240, Y: 210 }
    const occupied = barcodeOccupiedSize(0, 46, 12, 90, {})
    applyDeclaredBarcodeEndCoordinate(start, end, occupied, true, false, 90)
    expect(end).toEqual({ X: 78, Y: 210 })
  })

  it('fits the canvas inside a bounded viewport', () => {
    expect(fitScale(600, 300, 1000, 700)).toBe(1.5)
    expect(fitScale(600, 1200, 1000, 700)).toBeCloseTo(0.5166, 3)
  })

  it('snaps to a stable printer-point grid', () => {
    expect(snapCoordinate(15, 4)).toBe(16)
    expect(snapCoordinate(-7, 4)).toBe(-8)
    expect(snapDeltaToGrid(5, 6, 4)).toBe(7)
    expect(snapDeltaToGrid(8, 2, 4)).toBe(4)
  })

  it('maps arrow keys to one-point and large-step nudges', () => {
    expect(getNudgeDelta('ArrowLeft')).toEqual({ dx: -1, dy: 0 })
    expect(getNudgeDelta('ArrowDown', 10)).toEqual({ dx: 0, dy: 10 })
    expect(getNudgeDelta('Enter')).toBeUndefined()
  })

  it('moves a text definition and its layout limit together while clamping at zero', () => {
    const text = { Type: 1 as const, StartCoordinate: { X: 10, Y: 20 }, EndCoordinate: { X: 110, Y: 50 } }
    expect(translateElement(text, -30, 8)).toBe(true)
    expect(text.StartCoordinate).toEqual({ X: 0, Y: 28 })
    expect(text.EndCoordinate).toEqual({ X: 100, Y: 58 })
  })

  it('moves both line endpoints and preserves the direction of a reversed line', () => {
    const line = { Type: 5 as const, StartCoordinate: { X: 80, Y: 30 }, EndCoordinate: { X: 20, Y: 10 }, LineEndCoordinate: { X: 20, Y: 10 } }
    expect(translateElement(line, -40, -20)).toBe(true)
    expect(line.StartCoordinate).toEqual({ X: 60, Y: 20 })
    expect(line.LineEndCoordinate).toEqual({ X: 0, Y: 0 })
    expect(line.EndCoordinate).toEqual({ X: 0, Y: 0 })
  })

  it('materializes an automatic element from its rendered position before moving', () => {
    const text: TemplateElement = { Type: 1, FontSize: 24, Field: { Value: 'SECOND' } }
    expect(materializeElementPosition(text, { x: 0, y: 24, width: 72 }, { x: 0, y: 0, width: 120 })).toBe(true)
    translateElement(text, 0, 4)
    expect(text.StartCoordinate).toEqual({ X: 0, Y: 28 })
  })

  it('snaps an element edge and baseline to nearby element guides', () => {
    expect(getAlignmentSnap(
      { x: 18, y: 34, width: 40, height: 20 },
      [{ x: 64, y: 56, width: 30, height: 24 }],
    )).toEqual({ dx: 6, dy: 2, x: 64, y: 56 })
    expect(getAlignmentSnap(
      { x: 18, y: 34, width: 40, height: 20 },
      [{ x: 90, y: 80, width: 30, height: 24 }],
    )).toEqual({ dx: 0, dy: 0, x: undefined, y: undefined })
  })
})
