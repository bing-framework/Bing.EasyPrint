import { describe, expect, it } from 'vitest'
import { assertTemplate, cloneTemplate, type BpTemplate, type TemplateElement } from './model'

function baseTemplate(): BpTemplate {
  return { Version: 1, Width: 100, Height: 100, Rows: [] }
}

describe('template validation', () => {
  it('rejects excessive nesting before tree rendering', () => {
    const template = baseTemplate() as BpTemplate & Record<string, unknown>
    let root: Record<string, unknown> = {}
    const nested = root
    for (let depth = 0; depth < 145; depth += 1) {
      const child: Record<string, unknown> = {}
      root.child = child
      root = child
    }
    template.extra = nested
    expect(() => assertTemplate(template)).toThrow('嵌套深度')
  })

  it('rejects unknown element types and cyclic objects', () => {
    const invalid = baseTemplate()
    invalid.Rows.push({ Cells: [{ Elements: [{ Type: 11 } as never] }] })
    expect(() => assertTemplate(invalid)).toThrow('不支持的元素类型')

    const cyclic = baseTemplate() as BpTemplate & Record<string, unknown>
    cyclic.self = cyclic
    expect(() => assertTemplate(cyclic)).toThrow('循环引用')
  })

  it('accepts supported text and barcode rotations and validates text line width', () => {
    const template = baseTemplate()
    template.Version = 2
    template.Rows = [{ Cells: [{ Elements: [
      { Type: 1, Rotation: 90, Width: 0, Field: { Value: 'TEXT' } },
      { Type: 2, Rotation: 270, Width: 80, Height: 40, Field: { Name: 'image' } },
      { Type: 3, Rotation: 180, Width: 80, Height: 80, Field: { Value: 'QR' } },
      { Type: 4, Rotation: 270, Width: 80, Height: 40, Field: { Value: '1234' } },
    ] }] }]
    expect(() => assertTemplate(template)).not.toThrow()
    ;(template.Rows[0].Cells![0].Elements![0] as Extract<TemplateElement, { Type: 1 }>).Width = -1
    expect(() => assertTemplate(template)).toThrow('Width')
    ;(template.Rows[0].Cells![0].Elements![0] as Extract<TemplateElement, { Type: 1 }>).Width = 12.5
    expect(() => assertTemplate(template)).toThrow('Width')
  })

  it('accepts explicit QR/barcode quiet zones and preserves null default compatibility', () => {
    const template = baseTemplate()
    template.Version = 2
    template.Rows = [{ Cells: [{ Elements: [
      { Type: 3, Width: 120, Height: 120, QuietZone: {}, Field: { Value: 'QR' } },
      { Type: 4, Width: 220, Height: 46, QuietZone: { Left: 2, Top: 3, Right: 4, Bottom: 5 }, Field: { Value: '1234' } },
    ] }] }]
    expect(() => assertTemplate(template)).not.toThrow()
    ;(template.Rows[0].Cells![0].Elements![0] as Extract<TemplateElement, { Type: 3 }>).QuietZone = null
    expect(() => assertTemplate(template)).not.toThrow()
    expect(cloneTemplate(template)).toEqual(template)
  })

  it('accepts image source crops and preserves them through cloning', () => {
    const template = baseTemplate()
    template.Version = 2
    const image: Extract<TemplateElement, { Type: 2 }> = { Type: 2, Width: 80, Height: 60, Crop: { X: 12, Y: 8, Width: 40, Height: 24 }, Field: { Value: 'photo' } }
    template.Rows = [{ Cells: [{ Elements: [image] }] }]

    expect(() => assertTemplate(template)).not.toThrow()
    expect(cloneTemplate(template)).toEqual(template)

    image.Crop = null
    expect(() => assertTemplate(template)).not.toThrow()
    expect(cloneTemplate(template)).toEqual(template)
  })

  it('rejects malformed image source crops with source-pixel constraints', () => {
    const template = baseTemplate()
    template.Version = 2
    const image: Extract<TemplateElement, { Type: 2 }> = { Type: 2, Width: 80, Height: 60, Crop: { X: 0, Y: 0, Width: 20, Height: 20 }, Field: { Value: 'photo' } }
    template.Rows = [{ Cells: [{ Elements: [image] }] }]

    image.Crop = { X: -1, Y: 0, Width: 20, Height: 20 }
    expect(() => assertTemplate(template)).toThrow('Crop.X')
    image.Crop = { X: 0, Y: 0, Width: 0, Height: 20 }
    expect(() => assertTemplate(template)).toThrow('Crop.Width')
    image.Crop = { X: 0, Y: 0, Width: 20, Height: 20, Extra: 1 } as never
    expect(() => assertTemplate(template)).toThrow('Crop.Extra')
    ;(image as Record<string, unknown>).Crop = { X: 0, Y: 0, Width: 20 }
    expect(() => assertTemplate(template)).toThrow('Crop.Height')
  })

  it('rejects malformed quiet zones and quiet zones on other element types', () => {
    const template = baseTemplate()
    template.Version = 2
    const element = { Type: 3, Width: 120, Height: 120, Field: { Value: 'QR' } } as Extract<TemplateElement, { Type: 3 }>
    template.Rows = [{ Cells: [{ Elements: [element] }] }]
    element.QuietZone = { Left: -1 }
    expect(() => assertTemplate(template)).toThrow('非负整数')
    element.QuietZone = { Top: 1.5 }
    expect(() => assertTemplate(template)).toThrow('非负整数')
    element.QuietZone = { Left: 2_147_483_648 }
    expect(() => assertTemplate(template)).toThrow('非负整数')
    element.QuietZone = { Unknown: 1 } as never
    expect(() => assertTemplate(template)).toThrow('不受支持')
    element.QuietZone = [] as never
    expect(() => assertTemplate(template)).toThrow('对象或 null')
    template.Rows[0].Cells![0].Elements![0] = { Type: 1, QuietZone: {} } as never
    expect(() => assertTemplate(template)).toThrow('仅支持二维码')
  })

  it('rejects invalid angles and nonzero rotation on unsupported elements', () => {
    const template = baseTemplate()
    template.Version = 2
    template.Rows = [{ Cells: [{ Elements: [{ Type: 1, Rotation: 45 as never, Field: { Value: 'TEXT' } }] }] }]
    expect(() => assertTemplate(template)).toThrow('Rotation')

    template.Rows[0].Cells![0].Elements![0] = { Type: 7, Width: 20, Height: 20, Rotation: 90 } as never
    expect(() => assertTemplate(template)).toThrow('仅支持文字、二维码、一维条码')

    template.Rows[0].Cells![0].Elements![0] = { Type: 2, Width: 20, Height: 20, Rotation: 1 } as never
    expect(() => assertTemplate(template)).toThrow('Rotation')

    template.Rows[0].Cells![0].Elements![0] = { Type: 2, Width: 20, Height: 40, Rotation: 90 } as never
    expect(() => assertTemplate(template)).not.toThrow()
    ;(template.Rows[0].Cells![0].Elements![0] as Extract<TemplateElement, { Type: 2 }>).Width = -1
    expect(() => assertTemplate(template)).toThrow('Width')
    ;(template.Rows[0].Cells![0].Elements![0] as Extract<TemplateElement, { Type: 2 }>).Width = 20.5
    expect(() => assertTemplate(template)).toThrow('Width')
  })

  it('accepts boolean Filled and rejects non-default values on other elements', () => {
    const template = baseTemplate()
    template.Version = 2
    const rectangle: Extract<TemplateElement, { Type: 7 }> = { Type: 7, Width: 20, Height: 20, Filled: true }
    template.Rows = [{ Cells: [{ Elements: [rectangle] }] }]
    expect(() => assertTemplate(template)).not.toThrow()
    rectangle.Filled = false
    expect(() => assertTemplate(template)).not.toThrow()

    for (const value of ['true', 1, null] as unknown[]) {
      ;(rectangle as Record<string, unknown>).Filled = value
      expect(() => assertTemplate(template)).toThrow('Filled')
    }

    template.Rows[0].Cells![0].Elements![0] = { Type: 1, Filled: true, Field: { Value: 'TEXT' } } as never
    expect(() => assertTemplate(template)).toThrow('Filled')
    template.Rows[0].Cells![0].Elements![0] = { Type: 1, Filled: false, Field: { Value: 'TEXT' } } as never
    expect(() => assertTemplate(template)).not.toThrow()
  })

  it('accepts V2 groups but keeps V1 extensions invalid', () => {
    const extended = baseTemplate()
    extended.Version = 2
    extended.Components = [{ Id: 'badge', Width: 20, Height: 20, Elements: [{ Type: 7, Width: 20, Height: 20 }] }]
    extended.Rows = [{ Section: 'Body', Cells: [{ Elements: [{ Type: 6, ComponentId: 'badge' }] }] }]
    expect(() => assertTemplate(extended)).not.toThrow()
    extended.Version = 1
    expect(() => assertTemplate(extended)).toThrow('Version: 1')
  })

  it('accepts V3 row groups, component rows, and static object parameters', () => {
    const template: BpTemplate = {
      Version: 3,
      Width: 100,
      Height: 0,
      Components: [{
        Id: 'list',
        Width: 0,
        Height: 0,
        Parameters: { options: { Required: true } },
        Rows: [{ Cells: [{ Elements: [{ Type: 1, Field: { Name: '$param.options.label' } }] }] }],
      }],
      Rows: [{ Cells: [{ Elements: [{
        Type: 6,
        ComponentId: 'list',
        DataPath: 'Order',
        Pagination: 'Split',
        Parameters: { options: { Value: { Type: 99, Rows: [{ name: 'sample' }] } } },
      }] }] }],
    }
    expect(() => assertTemplate(template)).not.toThrow()
  })

  it('allows automatic component dimensions only in V3', () => {
    const template: BpTemplate = {
      Version: 3,
      Width: 100,
      Height: 100,
      Components: [{ Id: 'flow', Rows: [{ Cells: [{ Elements: [{ Type: 1, Field: { Value: 'line' } }] }] }] }],
      Rows: [{ Cells: [{ Elements: [{ Type: 6, ComponentId: 'flow' }] }] }],
    }

    expect(() => assertTemplate(template)).not.toThrow()
    template.Components![0].Width = 0
    template.Components![0].Height = 0
    expect(() => assertTemplate(template)).not.toThrow()
    template.Components![0].Width = -1
    expect(() => assertTemplate(template)).toThrow('组件定义无效')

    template.Version = 2
    template.Components![0].Width = 0
    template.Components![0].Height = 20
    expect(() => assertTemplate(template)).toThrow('组件定义无效')
    delete template.Components![0].Width
    template.Components![0].Height = 20
    expect(() => assertTemplate(template)).toThrow('组件定义无效')
  })

  it('rejects V3-only fields before Version 3 and conflicting component content', () => {
    const template: BpTemplate = {
      Version: 2,
      Width: 100,
      Height: 100,
      Components: [{ Id: 'badge', Width: 20, Height: 20, Elements: [] }],
      Rows: [{ Cells: [{ Elements: [{ Type: 6, Rows: [] }] }] }],
    }
    expect(() => assertTemplate(template)).toThrow('V3')

    template.Version = 3
    const group = template.Rows[0].Cells![0].Elements![0] as Extract<TemplateElement, { Type: 6 }>
    group.Elements = []
    expect(() => assertTemplate(template)).toThrow('不能同时指定')
    delete group.Elements
    group.ComponentId = 'badge'
    group.Rows = []
    expect(() => assertTemplate(template)).toThrow('不能同时包含内联内容')
  })

  it('validates parameter sources, enum values, and component row definitions', () => {
    const template: BpTemplate = {
      Version: 3,
      Width: 100,
      Height: 100,
      Components: [{ Id: 'row', Width: 10, Height: 0, Rows: [] }],
      Rows: [{ Cells: [{ Elements: [{ Type: 6, ComponentId: 'row', Parameters: { value: {} } }] }] }],
    }
    expect(() => assertTemplate(template)).toThrow('Path 或 Value')
    const group = template.Rows[0].Cells![0].Elements![0] as Extract<TemplateElement, { Type: 6 }>
    group.Parameters = { value: { Path: 'Name', Value: 'ignored' } }
    expect(() => assertTemplate(template)).toThrow('Path 或 Value')
    group.Parameters = { value: { Value: Number.NaN } }
    expect(() => assertTemplate(template)).toThrow('有限数字')
    group.Parameters = { value: { Path: 'Name' } }
    group.Pagination = 'Shrink' as never
    expect(() => assertTemplate(template)).toThrow('Pagination')
  })

  it('rejects component reference cycles before the structure tree expands', () => {
    const template: BpTemplate = { Version: 2, Width: 100, Height: 100,
      Components: [{ Id: 'loop', Width: 20, Height: 20, Elements: [{ Type: 6, ComponentId: 'loop' }] }],
      Rows: [{ Cells: [{ Elements: [{ Type: 6, ComponentId: 'loop' }] }] }] }
    expect(() => assertTemplate(template)).toThrow('循环引用')
    template.Components![0].Elements = [{ Type: 6, ComponentId: 'missing' }]
    expect(() => assertTemplate(template)).toThrow('组件不存在')
  })

  it('requires a table header to precede a repeated body row', () => {
    const template: BpTemplate = { Version: 2, Width: 100, Height: 100,
      Rows: [{ Section: 'TableHeader', Cells: [] }] }
    expect(() => assertTemplate(template)).toThrow('Rows[0]')
    template.Rows.push({ Section: 'PageFooter', RepeatSource: 'Items', Cells: [] })
    expect(() => assertTemplate(template)).toThrow('正文重复明细')
    template.Rows[1].Section = 'Body'
    expect(() => assertTemplate(template)).not.toThrow()
  })

  it('allows table headers inside V3 row components and rejects other nested sections', () => {
    const template: BpTemplate = {
      Version: 3,
      Width: 100,
      Height: 100,
      Components: [{
        Id: 'table', Width: 100, Height: 0,
        Rows: [
          { Section: 'TableHeader', Cells: [] },
          { Section: 'Body', RepeatSource: 'Items', Cells: [] },
        ],
      }],
      Rows: [{ Cells: [{ Elements: [{ Type: 6, ComponentId: 'table' }] }] }],
    }
    expect(() => assertTemplate(template)).not.toThrow()
    template.Components![0].Rows![0].Section = 'PageHeader'
    expect(() => assertTemplate(template)).toThrow('仅支持 Body 或 TableHeader')
  })

  it('validates semantic font sources and preserves them through cloning', () => {
    const template = baseTemplate()
    template.Rows = [{ Cells: [{ Elements: [{
      Type: 1,
      FontSize: 24,
      Font: { Kind: 2, Name: 'Microsoft YaHei' },
      Field: { Value: 'TEXT' },
    }] }] }]
    expect(() => assertTemplate(template)).not.toThrow()
    expect(cloneTemplate(template)).toEqual(template)

    const element = () => template.Rows[0].Cells![0].Elements![0] as Extract<TemplateElement, { Type: 1 }>
    element().Font = { Kind: 0 }
    expect(() => assertTemplate(template)).not.toThrow()
    element().Font = { Kind: 0, Name: '' }
    expect(() => assertTemplate(template)).toThrow('不能用于协议默认字体')
    element().Font = { Kind: 0, Name: null as never }
    expect(() => assertTemplate(template)).toThrow('不能用于协议默认字体')
    element().Font = { Kind: 1, Name: '  ' }
    expect(() => assertTemplate(template)).toThrow('Name')
    element().Font = { Kind: 1, Name: 'A', Extra: true } as never
    expect(() => assertTemplate(template)).toThrow('不受支持')
    element().Font = { Name: 'A' } as never
    expect(() => assertTemplate(template)).toThrow('Kind')
    element().Font = { Kind: 1.5, Name: 'A' } as never
    expect(() => assertTemplate(template)).toThrow('Kind')
    element().Font = { Kind: 3 as never, Name: 'A' }
    expect(() => assertTemplate(template)).toThrow('Kind')
    element().Font = { Kind: 1, Name: '1' }
    element().FontName = 55
    expect(() => assertTemplate(template)).toThrow('旧字体字段')
    element().FontName = 0
    element().FontFamily = 'Arial'
    expect(() => assertTemplate(template)).toThrow('旧字体字段')
    element().FontFamily = ' '
    expect(() => assertTemplate(template)).toThrow('旧字体字段')
    delete element().FontFamily
    element().DeviceFontName = ''
    expect(() => assertTemplate(template)).toThrow('旧字体字段')
  })
})
