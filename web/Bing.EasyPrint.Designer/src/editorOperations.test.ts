import { describe, expect, test } from 'vitest'
import { duplicateElement, groupRowsAtPaths, offsetFlowGroup, reorderFlowGroup, setFlowGroupPosition } from './editorOperations'
import type { BpTemplate, GroupElement } from './model'

function nestedTemplate(): BpTemplate {
  return { Version: 2, Width: 160, Height: 100, Rows: [{ Cells: [{ Rows: [
    { Cells: [{ Elements: [{ Type: 1, Field: { Value: 'TITLE' } }] }] },
    { RepeatSource: 'Items', Cells: [{ Elements: [{ Type: 1, Field: { Name: 'Name' } }] }] },
    { Cells: [{ Elements: [{ Type: 1, Field: { Value: 'END' } }] }] },
  ] }] }] }
}

describe('V3 editor operations', () => {
  test('groups consecutive nested rows and reorders the flow container', () => {
    const template = nestedTemplate()
    const first = 'Rows[0].Cells[0].Rows[0]'
    const second = 'Rows[0].Cells[0].Rows[1]'
    const path = groupRowsAtPaths(template, [first, second])
    expect(template.Version).toBe(3)
    expect(path).toBe('Rows[0].Cells[0].Rows[0].Cells[0].Elements[0]')
    expect(template.Rows[0].Cells?.[0].Rows?.[0].Cells?.[0].Elements?.[0]).toMatchObject({ Type: 6, Pagination: 'Split' })
    expect(reorderFlowGroup(template, path, 1)).toBe('Rows[0].Cells[0].Rows[1].Cells[0].Elements[0]')
    expect(template.Rows[0].Cells?.[0].Rows?.[0].Cells?.[0].Elements?.[0].Field?.Value).toBe('END')
  })

  test('rejects unrelated row selections without changing the template', () => {
    const template = nestedTemplate()
    const before = JSON.stringify(template)
    expect(() => groupRowsAtPaths(template, ['Rows[0].Cells[0].Rows[0]', 'Rows[0]'])).toThrow()
    expect(JSON.stringify(template)).toBe(before)
  })

  test('does not separate a table header from its repeated detail', () => {
    const template = nestedTemplate()
    const groupPath = groupRowsAtPaths(template, ['Rows[0].Cells[0].Rows[0]'])
    const rows = template.Rows[0].Cells?.[0].Rows
    rows![1] = { Section: 'TableHeader', Cells: [{ Elements: [{ Type: 1, Field: { Value: 'HEADER' } }] }] }
    expect(reorderFlowGroup(template, groupPath, 1)).toBeUndefined()
    expect(rows?.[0].Cells?.[0].Elements?.[0].Type).toBe(6)
    rows![1] = { RepeatSource: 'Items', Cells: [{ Elements: [{ Type: 1, Field: { Name: 'Name' } }] }] }
    rows!.splice(1, 0, { Section: 'TableHeader', Cells: [{ Elements: [{ Type: 1, Field: { Value: 'HEADER' } }] }] })
    expect(reorderFlowGroup(template, groupPath, 1)).toBeUndefined()
  })

  test('duplicates nested image references without sharing mutable fields', () => {
    const template: BpTemplate = { Version: 3, Width: 160, Height: 100,
      Rows: [{ Cells: [{ Elements: [{ Type: 6, Elements: [{ Type: 2, Width: 20, Height: 20,
        Field: { Value: 'resources/logo.png' }, StartCoordinate: { X: 4, Y: 5 } }] }] }] }] }
    const path = duplicateElement(template, 'Rows[0].Cells[0].Elements[0].Elements[0]')
    expect(path).toBe('Rows[0].Cells[0].Elements[0].Elements[1]')
    const content = template.Rows[0].Cells?.[0].Elements?.[0]
    if (content?.Type !== 6) throw new Error('组合缺失。')
    expect(content.Elements?.[1]).toMatchObject({ Type: 2, Field: { Value: 'resources/logo.png' }, StartCoordinate: { X: 12, Y: 13 } })
    content.Elements![1].Field!.Value = 'resources/other.png'
    expect(content.Elements?.[0].Field?.Value).toBe('resources/logo.png')
  })

  test('moves flowing element groups by margins without assigning absolute coordinates', () => {
    const group: GroupElement = { Type: 6, Elements: [{ Type: 1, Field: { Value: 'TITLE' } }] }

    expect(offsetFlowGroup(group, 12, 8)).toBe(true)
    expect(group).toMatchObject({ Margin: { Left: 12, Top: 8 } })
    expect(group.StartCoordinate).toBeUndefined()
    expect(setFlowGroupPosition(group, 'Y', 24)).toBe(true)
    expect(group.Margin?.Top).toBe(24)
    expect(group.StartCoordinate).toBeUndefined()
  })

  test('reorders a flow group among sibling elements without moving other rows', () => {
    const template: BpTemplate = { Version: 3, Width: 200, Height: 120, Rows: [
      { Cells: [{ Elements: [{ Type: 1, Field: { Value: 'A' } }] }, { Elements: [
        { Type: 1, Field: { Value: 'prefix' } }, { Type: 6, Rows: [], Pagination: 'Split' },
        { Type: 1, Field: { Value: 'suffix' } },
      ] }] },
      { Cells: [{ Elements: [{ Type: 1, Field: { Value: 'B' } }] }, { Elements: [
        { Type: 1, Field: { Value: 'other-row' } },
      ] }] },
    ] }
    const path = 'Rows[0].Cells[1].Elements[1]'
    expect(reorderFlowGroup(template, path, 1)).toBe('Rows[0].Cells[1].Elements[2]')
    expect(template.Rows[0].Cells?.[0].Elements?.[0].Field?.Value).toBe('A')
    expect(template.Rows[1].Cells?.[0].Elements?.[0].Field?.Value).toBe('B')
    expect(template.Rows[0].Cells?.[1].Elements?.map((element) => element.Field?.Value)).toEqual(['prefix', 'suffix', undefined])
  })
})
