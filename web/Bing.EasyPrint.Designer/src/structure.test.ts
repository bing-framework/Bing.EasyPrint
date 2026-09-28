import { describe, expect, it } from 'vitest'
import type { BpTemplate } from './model'
import { addNestedRow } from './structure'

function createTemplate(): BpTemplate {
  return {
    Version: 1,
    Width: 600,
    Height: 800,
    Rows: [{
      Cells: [{
        Width: 240,
        Rows: [{ Cells: [{ Width: 120 }] }],
      }, { Width: 0 }],
    }],
  }
}

describe('addNestedRow', () => {
  it('appends a full-width row with an empty element list to a cell', () => {
    const template = createTemplate()

    const path = addNestedRow(template, 'Rows[0].Cells[0]')

    expect(path).toBe('Rows[0].Cells[0].Rows[1]')
    expect(template.Rows[0].Cells?.[0].Rows?.[1]).toEqual({
      Cells: [{ Width: 240, Elements: [] }],
    })
  })

  it('supports nested cell paths and uses automatic width when the parent width is zero', () => {
    const template = createTemplate()

    const path = addNestedRow(template, 'Rows[0].Cells[1]')

    expect(path).toBe('Rows[0].Cells[1].Rows[0]')
    expect(template.Rows[0].Cells?.[1].Rows?.[0].Cells).toEqual([{ Width: 0, Elements: [] }])
  })

  it('uses automatic width when the parent width is undefined', () => {
    const template = createTemplate()
    template.Rows[0].Cells![0].Rows![0].Cells![0].Width = undefined

    const path = addNestedRow(template, 'Rows[0].Cells[0].Rows[0].Cells[0]')

    expect(path).toBe('Rows[0].Cells[0].Rows[0].Cells[0].Rows[0]')
    expect(template.Rows[0].Cells?.[0].Rows?.[0].Cells?.[0].Rows?.[0].Cells).toEqual([{ Width: 0, Elements: [] }])
  })

  it.each([
    '',
    'Rows[0]',
    'Rows[0].Cells[0].Elements[0]',
    'Rows[8].Cells[0]',
    'Rows[0].Cells[8]',
    'Rows[0].Cells[0].Rows[4].Cells[0]',
    'Rows[0].Cells[0].trailing',
  ])('returns undefined for an invalid cell path: %s', (path) => {
    expect(addNestedRow(createTemplate(), path)).toBeUndefined()
  })
})
