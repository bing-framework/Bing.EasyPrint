import { describe, expect, it } from 'vitest'
import { dotsToMm, fitScale, getAlignmentSnap, getNudgeDelta, materializeElementPosition, mmToDots, snapCoordinate, snapDeltaToGrid, translateElement } from './coordinates'
import type { TemplateElement } from './model'

describe('printer coordinate conversion', () => {
  it('uses 200 DPI and round-trips common dimensions', () => {
    expect(dotsToMm(540)).toBe(68.58)
    expect(mmToDots(70)).toBe(551)
    expect(mmToDots(dotsToMm(671))).toBe(671)
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
