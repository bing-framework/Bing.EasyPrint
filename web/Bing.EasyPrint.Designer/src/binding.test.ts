import { describe, expect, it } from 'vitest'
import { bindImageResource, collectArrayPaths, collectBindingPaths, collectFieldPaths, conditionMatches, resolvePath, setPathValue, valueForField } from './binding'

describe('field binding and conditions', () => {
  const context = { root: { enabled: true, order: { total: 12 }, rows: [{ name: 'A' }] }, item: { name: 'A', count: 2 }, index: 3 }

  it('resolves nested, root, item, and index paths', () => {
    expect(resolvePath('$root.order.total', context)).toBe(12)
    expect(resolvePath('$item.name', context)).toBe('A')
    expect(resolvePath('$root.enabled', context)).toBe(true)
    expect(resolvePath('$index', context)).toBe(3)
    expect(valueForField({ Name: 'count' }, context)).toBe('2')
    expect(valueForField({ Value: 'static' }, context)).toBe('static')
  })

  it('evaluates typed comparisons and boolean condition groups', () => {
    expect(conditionMatches({ Path: 'count', Operator: 'gt', Value: 1 }, context)).toBe(true)
    expect(conditionMatches({ Operator: 'and', Conditions: [
      { Path: 'count', Operator: 'ge', Value: 2 },
      { Path: '$root.enabled', Operator: 'eq', Value: true },
    ] }, context)).toBe(true)
    expect(conditionMatches({ Path: 'count', Operator: 'eq', Value: '2' }, context)).toBe(false)
    expect(conditionMatches({ Path: '$root.enabled', Operator: 'eq', Value: true }, context)).toBe(true)
    expect(conditionMatches({ Path: '$root.enabled', Operator: 'eq', Value: 'true' }, context)).toBe(false)
  })

  it('preserves booleans written by binary data controls', () => {
    const value: Record<string, unknown> = {}
    setPathValue(value, 'IsPromotion', true)
    expect(value.IsPromotion).toBe(true)
    expect(conditionMatches({ Path: 'IsPromotion', Operator: 'eq', Value: true }, { root: value, item: value, index: -1 })).toBe(true)
  })

  it('throws for a missing field instead of silently changing the binding', () => {
    expect(() => resolvePath('missing', context)).toThrow('字段不存在')
  })

  it('stores selected image resources as static resource keys', () => {
    expect(bindImageResource({ Name: 'Product.Image', Fit: true }, 'resources/photo.png')).toEqual({ Value: 'resources/photo.png', Fit: true })
  })

  it('collects nested fields and repeat-item fields from sample data', () => {
    expect(collectFieldPaths({ Product: { Name: 'Tea', Price: 2 }, Items: [{ Name: 'Cup', Quantity: 1 }] }))
      .toEqual(['Name', 'Product.Name', 'Product.Price', 'Quantity'])
  })

  it('keeps root and detail suggestions in their actual scopes', () => {
    const data = { Store: { Name: 'North' }, Order: { Items: [{ Name: 'Tea', Quantity: 2 }] } }
    expect(collectArrayPaths(data)).toEqual(['Order.Items'])
    const paths = collectBindingPaths(data, 'Order.Items')
    expect(paths).toContain('Name')
    expect(paths).toContain('$item.Name')
    expect(paths).toContain('$root.Store.Name')
    expect(paths).not.toContain('$root.Quantity')
    expect(paths).not.toContain('$root.Name')
    expect(collectBindingPaths(data)).toContain('Store.Name')
    expect(collectBindingPaths(data, '$var.lines', { lines: 'Order.Items' })).toContain('$item.Quantity')
  })

  it('resolves nested repeat sources from each parent item', () => {
    const data = { Store: { Name: 'North' }, Orders: [{ Number: 'A-1', Lines: [{ Sku: 'TEA', Quantity: 2 }] }] }
    const paths = collectBindingPaths(data, ['Orders', 'Lines'])
    expect(paths).toContain('Sku')
    expect(paths).toContain('$item.Quantity')
    expect(paths).toContain('$root.Store.Name')
    expect(paths).not.toContain('Number')
    expect(collectBindingPaths(data, ['Orders', '$var.lines'], { lines: 'Orders' })).toContain('Number')
  })
})
