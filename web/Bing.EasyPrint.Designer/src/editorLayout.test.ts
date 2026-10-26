import { describe, expect, it } from 'vitest'
import { arrangeAbsoluteSelection } from './editorLayout'

describe('absolute editor arrangement', () => {
  it('rejects selections across containers or flow items', () => {
    const selection = [
      { path: 'Rows[0].Cells[0].Elements[0]', containerPath: 'Rows[0].Cells[0]', x: 4, y: 5, width: 20, height: 12, absolute: true },
      { path: 'Rows[0].Cells[1].Elements[0]', containerPath: 'Rows[0].Cells[1]', x: 40, y: 8, width: 16, height: 22, absolute: true },
    ]

    expect(() => arrangeAbsoluteSelection(selection, 'distribute-x')).toThrow()
    expect(() => arrangeAbsoluteSelection(selection.map((item) => ({ ...item, absolute: false })), 'align-left')).toThrow()
  })

  it('distributes rotated occupied boxes with fixed endpoints and no cumulative rounding drift', () => {
    const selection = [
      { path: 'Rows[0].Cells[0].Elements[0]', containerPath: 'Rows[0].Cells[0]', x: 0, y: 3, width: 10, height: 30, absolute: true },
      { path: 'Rows[0].Cells[0].Elements[1]', containerPath: 'Rows[0].Cells[0]', x: 15, y: 6, width: 20, height: 12, absolute: true },
      { path: 'Rows[0].Cells[0].Elements[2]', containerPath: 'Rows[0].Cells[0]', x: 101, y: 9, width: 10, height: 30, absolute: true },
    ]

    const result = arrangeAbsoluteSelection(selection, 'distribute-x')

    expect(result.map((item) => item.x)).toEqual([0, 46, 101])
    expect(result[0]!.x).toBe(selection[0]!.x)
    expect(result[2]!.x).toBe(selection[2]!.x)
  })

  it('aligns horizontal selections on occupied bounds instead of changing their y positions', () => {
    const selection = [
      { path: 'a', containerPath: 'cell', x: 10, y: 4, width: 20, height: 10, absolute: true },
      { path: 'b', containerPath: 'cell', x: 40, y: 18, width: 30, height: 14, absolute: true },
    ]

    expect(arrangeAbsoluteSelection(selection, 'align-left')).toEqual([
      { path: 'a', x: 10, y: 4 }, { path: 'b', x: 10, y: 18 },
    ])
  })
})
