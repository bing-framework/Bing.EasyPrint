import { describe, expect, it } from 'vitest'
import { buildLocalLayout } from './layout'
import type { BpTemplate, TemplateElement } from './model'

function layoutElement(element: TemplateElement) {
  const template: BpTemplate = {
    Version: 1,
    Width: 400,
    Height: 400,
    Rows: [{ Height: 400, Cells: [{ Width: 400, Elements: [element] }] }],
  }
  return buildLocalLayout(template, {}).pages[0].elements[0]
}

describe('local rotated layout dimensions', () => {
  it.each([
    [0, 80, 40], [90, 40, 80], [180, 80, 40], [270, 40, 80],
  ] as const)('keeps a non-square image origin and swaps only its occupied box at %d degrees', (rotation, width, height) => {
    const element = layoutElement({
      Type: 2,
      Width: 80,
      Height: 40,
      Rotation: rotation,
      Field: { Name: 'image' },
      StartCoordinate: { X: 17, Y: 23 },
    })
    expect(element).toMatchObject({ rotation, x: 17, y: 23, width, height })
  })

  it('recovers raw image dimensions from a rotated EndCoordinate in offline layout', () => {
    const element = layoutElement({
      Type: 2,
      Rotation: 90,
      Field: { Name: 'image' },
      StartCoordinate: { X: 17, Y: 23 },
      EndCoordinate: { X: 57, Y: 103 },
    })
    expect(element).toMatchObject({ rotation: 90, x: 17, y: 23, width: 40, height: 80 })
  })

  it('uses an existing text EndCoordinate as the post-rotation box', () => {
    const element = layoutElement({
      Type: 1,
      FontSize: 24,
      Width: 80,
      Rotation: 90,
      Field: { Value: 'AB' },
      StartCoordinate: { X: 10, Y: 10 },
      EndCoordinate: { X: 30, Y: 90 },
    })
    expect(element).toMatchObject({ rotation: 90, width: 20, height: 80 })
  })

  it('estimates and rotates a text block when no EndCoordinate exists', () => {
    const element = layoutElement({
      Type: 1,
      FontSize: 24,
      Width: 80,
      Rotation: 90,
      Field: { Value: 'AB' },
      StartCoordinate: { X: 10, Y: 10 },
    })
    expect(element).toMatchObject({ rotation: 90, width: 26, height: 80 })
  })

  it('adds barcode reserve height only for nonzero rotation without an EndCoordinate', () => {
    expect(layoutElement({
      Type: 4, Width: 220, Height: 46, Rotation: 0, HumanReadableReserveHeight: 12,
      Field: { Value: '1234' }, StartCoordinate: { X: 10, Y: 10 },
    })).toMatchObject({ rotation: 0, width: 220, height: 46 })
    expect(layoutElement({
      Type: 4, Width: 220, Height: 46, Rotation: 90, HumanReadableReserveHeight: 12,
      Field: { Value: '1234' }, StartCoordinate: { X: 10, Y: 10 },
    })).toMatchObject({ rotation: 90, width: 58, height: 220 })
    expect(layoutElement({
      Type: 4, Width: 220, Height: 46, Rotation: 180, HumanReadableReserveHeight: 12,
      Field: { Value: '1234' }, StartCoordinate: { X: 10, Y: 10 },
    })).toMatchObject({ rotation: 180, width: 220, height: 58 })
  })

  it('uses an existing barcode EndCoordinate directly as the post-rotation box', () => {
    const element = layoutElement({
      Type: 4,
      Width: 220,
      Height: 46,
      Rotation: 90,
      HumanReadableReserveHeight: 12,
      Field: { Value: '1234' },
      StartCoordinate: { X: 10, Y: 10 },
      EndCoordinate: { X: 70, Y: 210 },
    })
    expect(element).toMatchObject({ rotation: 90, width: 60, height: 200 })
  })

  it('uses the outer barcode reservation for explicit quiet zones and clockwise rotation', () => {
    const quietZone = { Left: 2, Top: 3, Right: 4, Bottom: 5 }
    expect(layoutElement({
      Type: 4, Width: 220, Height: 46, Rotation: 0, HumanReadableReserveHeight: 12, QuietZone: quietZone,
      Field: { Value: '1234' }, StartCoordinate: { X: 10, Y: 10 },
    })).toMatchObject({ rotation: 0, x: 10, y: 10, width: 220, height: 66 })
    expect(layoutElement({
      Type: 4, Width: 220, Height: 46, Rotation: 90, HumanReadableReserveHeight: 12, QuietZone: quietZone,
      Field: { Value: '1234' }, StartCoordinate: { X: 10, Y: 10 },
    })).toMatchObject({ rotation: 90, x: 10, y: 10, width: 66, height: 220 })
  })

  it('adds explicit quiet zones to automatic QR/barcode estimates without double-counting an end box', () => {
    const quietZone = { Left: 2, Top: 3, Right: 4, Bottom: 5 }
    expect(layoutElement({
      Type: 3, Width: 0, Height: 0, UnitWidth: 3, QuietZone: quietZone,
      Field: { Value: 'QR' }, StartCoordinate: { X: 10, Y: 10 },
    })).toMatchObject({ width: 118, height: 120 })
    expect(layoutElement({
      Type: 4, Width: 0, Height: 46, QuietZone: quietZone,
      Field: { Value: '1234' }, StartCoordinate: { X: 10, Y: 10 },
    })).toMatchObject({ width: 118, height: 54 })
    expect(layoutElement({
      Type: 3, Width: 0, Height: 0, QuietZone: quietZone,
      Field: { Value: 'QR' }, StartCoordinate: { X: 10, Y: 10 }, EndCoordinate: { X: 30, Y: 50 },
    })).toMatchObject({ width: 20, height: 40 })
  })
})
