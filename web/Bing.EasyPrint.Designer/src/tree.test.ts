import { describe, expect, it } from 'vitest'
import { findElement, listTemplateTree } from './tree'
import { createReceiptSample } from './samples'
import { buildLocalLayout } from './layout'

describe('template structure paths', () => {
  it('lists rows, cells and elements using stable BpTemplate paths', () => {
    const template = createReceiptSample()
    const tree = listTemplateTree(template)
    expect(tree.map((entry) => entry.path)).toContain('Rows[1].Cells[0].Elements[0]')
    expect(findElement(template, 'Rows[1].Cells[0].Elements[0]')?.Field?.Name).toBe('Name')
  })

  it('maps repeated preview instances to the same editable definition path', () => {
    const template = createReceiptSample()
    const result = buildLocalLayout(template, { Items: [{ Name: 'A', Quantity: 1 }, { Name: 'B', Quantity: 2 }] })
    const repeated = result.pages[0].elements.filter((element) => element.sourcePath === 'Rows[1].Cells[0].Elements[0]')
    expect(repeated.map((element) => element.instanceIndices)).toEqual([[0], [1]])
    expect(findElement(template, repeated[1].sourcePath)?.Field?.Name).toBe('Name')
  })
})
