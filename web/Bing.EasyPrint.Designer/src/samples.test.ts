import { describe, expect, it } from 'vitest'
import { makeSample, sampleData, type SampleKey } from './samples'
import type { BpTemplate, FontElement, Row, TemplateElement } from './model'

const supportedFontSizes = new Set([16, 24, 32, 48, 64, 72, 80, 96, 112, 120, 128, 144, 160, 168, 176, 192, 208, 216, 224, 240, 256, 264, 288, 312, 336, 360, 384])

function collectElements(template: BpTemplate): TemplateElement[] {
  const result: TemplateElement[] = []
  const visitRows = (rows: Row[]) => rows.forEach((row) => (row.Cells ?? []).forEach((cell) => {
    result.push(...(cell.Elements ?? []))
    visitRows(cell.Rows ?? [])
  }))
  visitRows(template.Rows)
  return result
}

describe('built-in designer samples', () => {
  it('starts a manual design with an editable empty cell', () => {
    const blank = makeSample('blank')
    expect(blank).toMatchObject({ Version: 1, Width: 576, Height: 360 })
    expect(blank.Rows[0].Cells?.[0]).toMatchObject({ Width: 576, Elements: [] })
  })

  it('uses only renderer-supported font sizes', () => {
    const keys: SampleKey[] = ['product', 'receipt', 'receipt-v2']
    const unsupported = keys.flatMap((key) => collectElements(makeSample(key))
      .filter((element): element is FontElement => element.Type === 1)
      .filter((element) => !supportedFontSizes.has(element.FontSize || 24))
      .map((element) => `${key}:${element.FontSize}`))
    expect(unsupported).toEqual([])
  })

  it('uses a checked EAN-13 value in the product sample', () => {
    const value = sampleData.product.Product.Barcode
    const sum = [...value.slice(0, 12)].reduce((total, digit, index) => total + Number(digit) * (index % 2 ? 3 : 1), 0)
    expect(value).toHaveLength(13)
    expect(Number(value[12])).toBe((10 - sum % 10) % 10)
    expect(collectElements(makeSample('product')).find((element) => element.Type === 4)?.BarcodeType).toBe(5)
  })
})
