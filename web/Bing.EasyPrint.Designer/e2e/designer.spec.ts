import { expect, test } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'

async function openBlankTemplate(page: import('@playwright/test').Page) {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.locator('.sample-picker select').selectOption('blank')
  await expect(page.locator('.canvas-breadcrumb strong')).toHaveText('576 × 360 点')
}

async function exportTemplateJson(page: import('@playwright/test').Page) {
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const download = await downloadPromise
  return readFile(await download.path())
}

async function exportFirstCpclPage(page: import('@playwright/test').Page) {
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 CPCL' }).click()
  const download = await downloadPromise
  const archive = await JSZip.loadAsync(await readFile(await download.path()))
  const firstPage = archive.file('page-001.cpcl')
  expect(firstPage).not.toBeNull()
  return firstPage!.async('uint8array')
}

async function exportCpclPages(page: import('@playwright/test').Page) {
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 CPCL' }).click()
  const download = await downloadPromise
  const archive = await JSZip.loadAsync(await readFile(await download.path()))
  const names = Object.keys(archive.files).filter((name) => /^page-\d{3}\.cpcl$/.test(name)).sort()
  return Promise.all(names.map(async (name) => archive.file(name)!.async('uint8array')))
}

async function reloadTemplateJson(page: import('@playwright/test').Page, contents: Buffer, name = 'roundtrip.json') {
  const previewPromise = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await page.locator('input[type="file"]').first().setInputFiles({
    name,
    mimeType: 'application/json',
    buffer: contents,
  })
  await previewPromise
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await expect(page.getByRole('button', { name: '生成 CPCL' })).toBeEnabled()
}

const repeatedTsplDiagnosticTemplate = {
  Version: 3,
  Width: 160,
  Height: 48,
  Components: [{
    Id: 'label',
    Width: 160,
    Elements: [{ Type: 1, FontSize: 24, Field: { Name: 'Name' } }],
  }],
  Rows: [{
    RepeatSource: 'Items',
    Height: 24,
    Cells: [{ Width: 160, Elements: [{ Type: 6, ComponentId: 'label' }] }],
  }],
}

const repeatedTsplDiagnosticData = {
  Items: [
    { Name: 'OK 1', Label: 'OK 1' },
    { Name: 'OK 2', Label: 'OK 2' },
    { Name: 'OK 3', Label: 'OK 3' },
    { Name: 'OK 4', Label: 'OK 4' },
    { Name: 'BAD"TEXT', Label: 'BAD"TEXT' },
  ],
}

const crossProtocolPaginationTemplate = {
  Version: 3,
  Width: 240,
  Height: 48,
  Rows: [{
    RepeatSource: 'Items',
    Height: 24,
    Cells: [{ Width: 240, Elements: [{ Type: 1, FontSize: 24, Field: { Name: 'Name' } }] }],
  }],
}

const crossProtocolPaginationData = {
  Items: Array.from({ length: 13 }, (_, index) => ({ Name: `ROW-${String(index + 1).padStart(2, '0')}` })),
}

function designerProject(template: object, outputTarget: 'Tspl' | 'Cpcl' = 'Tspl') {
  return {
    format: 'Bing.EasyPrint.DesignerProject',
    version: 1,
    template,
    outputSettings: {
      outputTarget,
      allowRasterizedLines: false,
      imageThreshold: 128,
      encoding: 'Ascii',
      deviceFontCalibrations: { Tspl: {}, Zpl: {} },
      requireDeclaredNativeBounds: false,
      escPosDevice: {},
      tsplMedia: {},
      zplMedia: {},
    },
  }
}

const s1TextTemplate = {
  Version: 1,
  Width: 240,
  Height: 100,
  Rows: [{ Height: 100, Cells: [{ Width: 240, Elements: [{
    Type: 1, FontSize: 24, Field: { Name: 'Title' },
  }] }] }],
}

const s1GbkTextProfile = {
  encodingCodePage: 936,
  codePageIndex: 15,
  characterMode: 'FsKanji',
  characterRanges: [{ first: 0, last: 127 }, { first: 19968, last: 40959 }],
  halfCellWidthDots: 12,
  fullCellWidthDots: 24,
  lineHeightDots: 24,
}

const s1EscPosProfile = {
  name: 'S1 GBK Native',
  imageCommand: 'RasterGsV0',
  bitImageMode: 'DoubleDensity24',
  horizontalDpi: 203,
  verticalDpi: 203,
  motionUnitsPerInch: 203,
  supportsCut: true,
  supportsNativeBarcode: true,
  supportsNativeQrCode: true,
  supportsPrintAreaWidth: true,
  supportsTextSize: true,
  unidirectionalBitImage: false,
  text: s1GbkTextProfile,
}

const rasterFontTemplate = {
  Version: 1,
  Width: 240,
  Height: 100,
  Rows: [{ Height: 100, Cells: [{ Width: 240, Elements: [{
    Type: 1, FontSize: 24, Bold: false,
    Font: { Kind: 2, Name: 'FixtureMono' }, Field: { Name: 'Title' },
  }] }] }],
}

async function readFixtureFontBase64(fileName: string) {
  return (await readFile(new URL(`../../../tests/Fixtures/Fonts/${fileName}`, import.meta.url))).toString('base64')
}

async function importDesignerProject(page: import('@playwright/test').Page, project: ReturnType<typeof designerProject>, name: string) {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const previewPromise = page.waitForResponse((response) => response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.locator('[data-testid="template-import"]').setInputFiles({
    name, mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(project)),
  })
  await previewPromise
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue(project.outputSettings.outputTarget)
}

async function setDesignerData(page: import('@playwright/test').Page, data: object) {
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  const previewPromise = page.waitForResponse((response) => response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify(data))
  await previewPromise
}

function dimensions(page: import('@playwright/test').Page) {
  return page.locator('.inspector-section').filter({ hasText: '位置与尺寸' }).locator('input')
}

async function canvasGroupCount(page: import('@playwright/test').Page) {
  return page.evaluate(() => {
    const root = document.querySelector('.page-frame')!.firstElementChild as HTMLElement & { __vueParentComponent?: any }
    const stage = root.__vueParentComponent?.exposed?.getNode?.()
    const visit = (node: any): number => (node.getClassName?.() === 'Group' ? 1 : 0) + (node.getChildren?.().reduce((count: number, child: any) => count + visit(child), 0) ?? 0)
    return stage ? visit(stage) : 0
  })
}

async function canvasGuideCount(page: import('@playwright/test').Page, axis: 'x' | 'y') {
  return page.evaluate((direction) => {
    const root = document.querySelector('.page-frame')!.firstElementChild as HTMLElement & { __vueParentComponent?: any }
    const stage = root.__vueParentComponent?.exposed?.getNode?.()
    return stage?.find(`.alignment-guide-${direction}`).length ?? 0
  }, axis)
}

async function canvasGroupBounds(page: import('@playwright/test').Page) {
  return page.evaluate(() => {
    const root = document.querySelector('.page-frame')!.firstElementChild as HTMLElement & { __vueParentComponent?: any }
    const stage = root.__vueParentComponent?.exposed?.getNode?.()
    return stage?.find('Group').map((group: any) => ({ x: group.x(), y: group.y(), width: group.width(), height: group.height() })) ?? []
  })
}

test('从空白标签添加、拖动并撤销重做后导出位置', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('button', { name: '单行文字', exact: true }).click()
  const position = dimensions(page)
  const before = [Number(await position.nth(0).inputValue()), Number(await position.nth(1).inputValue())]
  const frame = await page.locator('.page-frame').boundingBox()
  expect(frame).not.toBeNull()
  const scale = 0.72
  const elementHeight = Number(await position.nth(3).inputValue())
  const startX = frame!.x + (before[0] + 24) * scale
  const startY = frame!.y + (before[1] + elementHeight / 2) * scale
  await expect.poll(() => canvasGroupCount(page)).toBeGreaterThan(0)

  await page.mouse.move(startX, startY)
  await page.mouse.down()
  await page.mouse.move(startX + 50, startY + 40, { steps: 8 })
  await page.mouse.up()
  await expect.poll(async () => Number(await position.nth(0).inputValue())).toBeGreaterThan(before[0])
  await expect.poll(async () => Number(await position.nth(1).inputValue())).toBeGreaterThan(before[1])
  const after = [Number(await position.nth(0).inputValue()), Number(await position.nth(1).inputValue())]
  expect(after[0] % 4).toBe(0)
  expect(after[1] % 4).toBe(0)

  await page.locator('.element-tools button').filter({ hasText: '线段' }).click()
  await expect.poll(() => canvasGroupCount(page)).toBe(2)
  await page.getByRole('button', { name: '放大画布' }).first().click()
  const linePosition = dimensions(page)
  const lineBefore = [Number(await linePosition.nth(0).inputValue()), Number(await linePosition.nth(1).inputValue())]
  const zoom = 0.72 * 1.1
  const zoomedFrame = await page.locator('.page-frame').boundingBox()
  expect(zoomedFrame).not.toBeNull()
  await page.mouse.move(zoomedFrame!.x + 80, zoomedFrame!.y + lineBefore[1] * zoom)
  await page.mouse.down()
  await page.mouse.move(zoomedFrame!.x + 110, zoomedFrame!.y + lineBefore[1] * zoom + 23, { steps: 8 })
  await page.mouse.up()
  await expect.poll(async () => Number(await linePosition.nth(0).inputValue())).toBeGreaterThan(lineBefore[0])
  await expect.poll(async () => Number(await linePosition.nth(1).inputValue())).toBeGreaterThan(lineBefore[1])
  const lineAfter = [Number(await linePosition.nth(0).inputValue()), Number(await linePosition.nth(1).inputValue())]

  await page.getByRole('button', { name: '撤销 Ctrl+Z' }).click()
  await expect(linePosition.nth(0)).toHaveValue(String(lineBefore[0]))
  await page.getByRole('button', { name: '重做 Ctrl+Y' }).click()
  await expect(linePosition.nth(0)).toHaveValue(String(lineAfter[0]))

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const template = JSON.parse(await readFile(await (await downloadPromise).path(), 'utf8'))
  const elements = template.Rows[0].Cells[0].Elements
  expect(elements[0].StartCoordinate).toEqual({ X: after[0], Y: after[1] })
  expect(elements.find((element: { Type: number }) => element.Type === 5).StartCoordinate)
    .toEqual({ X: lineAfter[0], Y: lineAfter[1] })
})

test('设计器项目保存输出设置，撤销重做与旧模板导入保持一致', async ({ page }) => {
  await openBlankTemplate(page)
  await expect(page.locator('.dirty-dot')).toHaveCount(0)

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  await page.locator('.output-settings-button').click()
  await page.getByRole('spinbutton', { name: '打印份数' }).fill('3')
  await expect(page.locator('.dirty-dot')).toHaveCount(1)

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const saved = await readFile(await (await downloadPromise).path())
  const project = JSON.parse(saved.toString('utf8'))
  expect(project.format).toBe('Bing.EasyPrint.DesignerProject')
  expect(project.outputSettings).toMatchObject({ outputTarget: 'Tspl', copies: 3 })
  await expect(page.locator('.dirty-dot')).toHaveCount(0)

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Zpl')
  await expect(page.locator('.dirty-dot')).toHaveCount(1)
  await page.getByRole('button', { name: '撤销 Ctrl+Z' }).click()
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('Tspl')
  await expect(page.locator('.dirty-dot')).toHaveCount(0)
  await page.getByRole('button', { name: '重做 Ctrl+Y' }).click()
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('Zpl')

  await page.locator('[data-testid="template-import"]').setInputFiles({
    name: 'saved-project.json', mimeType: 'application/json', buffer: saved,
  })
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('Tspl')
  await expect(page.getByRole('spinbutton', { name: '打印份数' })).toHaveValue('3')
  await expect(page.locator('.dirty-dot')).toHaveCount(0)

  const legacy = Buffer.from(JSON.stringify(project.template))
  await page.locator('[data-testid="template-import"]').setInputFiles({
    name: 'legacy.json', mimeType: 'application/json', buffer: legacy,
  })
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('Cpcl')
  await expect(page.getByRole('spinbutton', { name: '打印份数' })).toHaveValue('')
})

test('逐字体校准按协议隔离并随项目保存', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  await page.locator('.output-settings-button').click()

  await page.getByLabel('逐字体校准字体编号').fill('1')
  await page.getByLabel('逐字体半角字宽').fill('16')
  await page.getByLabel('逐字体全角字宽').fill('32')
  await page.getByLabel('逐字体设备行高').fill('48')
  const tsplRequest = page.waitForRequest((request) => request.url().includes('/api/designer/preview'))
  await page.getByRole('button', { name: '保存当前字体' }).click()
  expect((await tsplRequest).postDataJSON().deviceFontCalibrations).toEqual({
    '1': { halfCellWidthAt24: 16, fullCellWidthAt24: 32, lineHeightAt24: 48 },
  })

  const protocolPreview = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Zpl')
  await protocolPreview
  await page.getByLabel('逐字体校准字体编号').fill('1')
  await expect(page.getByLabel('逐字体半角字宽')).toHaveValue('')
  await page.getByLabel('逐字体半角字宽').fill('18')
  await page.getByLabel('逐字体全角字宽').fill('36')
  await page.getByLabel('逐字体设备行高').fill('24')
  const zplRequest = page.waitForRequest((request) => request.url().includes('/api/designer/preview'))
  await page.getByRole('button', { name: '保存当前字体' }).click()
  expect((await zplRequest).postDataJSON().deviceFontCalibrations).toEqual({
    '1': { halfCellWidthAt24: 18, fullCellWidthAt24: 36, lineHeightAt24: 24 },
  })

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const project = JSON.parse((await readFile(await (await downloadPromise).path())).toString('utf8'))
  expect(project.outputSettings.deviceFontCalibrations).toEqual({
    Tspl: { '1': { halfCellWidthAt24: 16, fullCellWidthAt24: 32, lineHeightAt24: 48 } },
    Zpl: { '1': { halfCellWidthAt24: 18, fullCellWidthAt24: 36, lineHeightAt24: 24 } },
  })
})

test('V2 分页小票展示变量、动态明细和页脚', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.locator('.sample-picker select').selectOption('receipt-v2')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await expect(page.locator('.page-pager')).toBeVisible()
  await page.locator('.page-pager button[title="下一页"]').click()
  await expect(page.locator('.page-pager')).toContainText('2 /')
  await page.locator('.page-tree-row').click()
  await expect(page.locator('.inspector-panel')).toContainText('shop')
})

test('工具拖入画布后可组合、保存并复用组件', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.locator('.advanced-tools button').filter({ hasText: '矩形' }).dragTo(page.locator('.page-frame'))
  await expect(page.locator('.tree-row').filter({ hasText: '矩形' })).toBeVisible()
  await page.locator('.advanced-tools button').filter({ hasText: '组合元素' }).click()
  await expect(page.locator('.tree-row').filter({ hasText: '组合' }).first()).toBeVisible()
  await page.getByRole('textbox', { name: '组件标识' }).fill('badge')
  await page.getByRole('button', { name: '保存组件' }).click()
  await page.locator('.component-library button').filter({ hasText: 'badge' }).click()
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const template = JSON.parse(await readFile(await (await downloadPromise).path(), 'utf8'))
  expect(template.Version).toBe(2)
  expect(template.Components).toHaveLength(1)
  expect(template.Rows[0].Cells[0].Elements.filter((item: { Type: number }) => item.Type === 6)).toHaveLength(2)
})

test('V3 流式组合调整偏移后仍保持流式布局', async ({ page }) => {
  await page.goto('/')
  const text = (value: string) => ({ Type: 1, FontSize: 24, Field: { Value: value } })
  const row = (value: string) => ({ Height: 28, Cells: [{ Elements: [text(value)] }] })
  const fixture = { Version: 3, Width: 240, Height: 120, Rows: [row('TOP'),
    { Cells: [{ Elements: [{ Type: 6, Pagination: 'Split', Rows: [row('MIDDLE')] }] }] }, row('BOTTOM')] }
  await reloadTemplateJson(page, Buffer.from(JSON.stringify(fixture)))
  await page.locator('.tree-row').filter({ hasText: '组合' }).first().locator('.tree-select').click()
  await expect(page.getByLabel('位置模式')).toHaveValue('flow')
  const y = page.locator('.inspector-section').filter({ hasText: '位置与尺寸' }).getByLabel('Y')
  await y.fill('12')
  await y.press('Tab')
  const saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  const group = saved.Rows[1].Cells[0].Elements[0]
  expect(group.StartCoordinate).toBeUndefined()
  expect(group.Margin.Top).toBe(12)
  expect(group.Rows[0].Cells[0].Elements[0].Field.Value).toBe('MIDDLE')
  await page.setViewportSize({ width: 390, height: 844 })
  await expect(page.locator('.canvas-breadcrumb strong')).toHaveText('240 × 120 点')
})

test('V3 元素流式组合调整位置后仍保持自动布局', async ({ page }) => {
  await page.goto('/')
  const fixture = { Version: 3, Width: 240, Height: 120,
    Rows: [{ Cells: [{ Elements: [{ Type: 6, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'FLOW' } }] }] }] }] }
  await reloadTemplateJson(page, Buffer.from(JSON.stringify(fixture)))
  await page.locator('.tree-row').filter({ hasText: '组合' }).first().locator('.tree-select').click()
  await expect(page.getByLabel('位置模式')).toHaveValue('flow')
  const y = page.locator('.inspector-section').filter({ hasText: '位置与尺寸' }).getByLabel('Y')
  await y.fill('12')
  await y.press('Tab')
  const saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  const group = saved.Rows[0].Cells[0].Elements[0]
  expect(group.StartCoordinate).toBeUndefined()
  expect(group.Margin.Top).toBe(12)
  expect(new TextDecoder().decode(await exportFirstCpclPage(page))).toContain('FLOW')
})

test('保存带参数的内联组合后保留组件绑定并继续渲染', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify({ Name: 'TEA', Product: { Name: 'PRODUCT' } }))
  const fixture = { Version: 3, Width: 240, Height: 120,
    Rows: [{ Cells: [{ Elements: [{ Type: 6, Width: 120, Height: 32,
      StartCoordinate: { X: 8, Y: 8 }, DataPath: 'Product', Parameters: { caption: { Path: 'Name' } },
      Elements: [{ Type: 1, FontSize: 24, Field: { Name: '$param.caption' }, StartCoordinate: { X: 0, Y: 0 } }],
    }] }] }] }
  await reloadTemplateJson(page, Buffer.from(JSON.stringify(fixture)))
  await page.locator('.tree-select[title="Rows[0].Cells[0].Elements[0]"]').click()
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.getByRole('textbox', { name: '组件标识' }).fill('productCaption')
  await page.getByRole('button', { name: '保存组件' }).click()

  const saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(saved.Components[0].Parameters).toEqual({ caption: { Required: true } })
  expect(saved.Rows[0].Cells[0].Elements[0]).toMatchObject({ ComponentId: 'productCaption',
    DataPath: 'Product', Parameters: { caption: { Path: 'Name' } } })
  expect(new TextDecoder().decode(await exportFirstCpclPage(page))).toContain('TEA')
})

test('V3 自动尺寸组件可导入、预览并往返导出 JSON', async ({ page }) => {
  await page.goto('/')
  const fixture = {
    Version: 3,
    Width: 240,
    Height: 120,
    Components: [{ Id: 'flow', Rows: [{ Cells: [{ Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'AUTO' } }] }] }] }],
    Rows: [{ Cells: [{ Elements: [{ Type: 6, ComponentId: 'flow' }] }] }],
  }

  await reloadTemplateJson(page, Buffer.from(JSON.stringify(fixture)))
  const saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(saved.Components[0].Width).toBeUndefined()
  expect(saved.Components[0].Height).toBeUndefined()
  expect(saved.Rows[0].Cells[0].Elements[0].ComponentId).toBe('flow')

  await reloadTemplateJson(page, Buffer.from(JSON.stringify(saved)))
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
})

test('V3 自动尺寸组件库可导入旧模板并保持可渲染', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('button', { name: '组件与表格' }).click()
  const library = {
    Version: 3,
    Width: 576,
    Height: 360,
    Rows: [],
    Components: [{ Id: 'autoBadge', Width: 0, Height: 0,
      Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'AUTO' }, StartCoordinate: { X: 0, Y: 0 } }] }],
  }
  await page.locator('[data-testid="component-import"]').setInputFiles({
    name: 'auto-components.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(library)),
  })
  const insertButton = page.locator('.component-entry button[title="插入组件 autoBadge"]')
  await expect(insertButton).toBeVisible()

  const template = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(template.Version).toBe(3)
  expect(template.Components[0]).toMatchObject({ Id: 'autoBadge', Width: 0, Height: 0 })

  await insertButton.click()
  const cpcl = await exportFirstCpclPage(page)
  expect(Buffer.from(cpcl).toString('latin1')).toContain('AUTO')
})

test('V3 嵌套行可组合并复制，撤销重做保持结构', async ({ page }) => {
  await page.goto('/')
  const text = (value: string) => ({ Type: 1, FontSize: 24, Field: { Value: value } })
  const row = (value: string) => ({ Height: 28, Cells: [{ Elements: [text(value)] }] })
  const fixture = { Version: 3, Width: 240, Height: 120,
    Rows: [{ Cells: [{ Rows: [row('A'), row('B'), row('TAIL')] }] }] }
  await reloadTemplateJson(page, Buffer.from(JSON.stringify(fixture)))
  await page.locator('.tree-select[title="Rows[0].Cells[0].Rows[0]"]').click()
  await page.locator('.tree-select[title="Rows[0].Cells[0].Rows[1]"]').click({ modifiers: ['Control'] })
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.locator('.advanced-tools button').filter({ hasText: '组合行' }).click()
  const groupPath = 'Rows[0].Cells[0].Rows[0].Cells[0].Elements[0]'
  await expect(page.locator(`.tree-select[title="${groupPath}"]`)).toBeVisible()
  await page.getByRole('button', { name: '复制所选元素' }).first().click()
  const saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(saved.Rows[0].Cells[0].Rows[0].Cells[0].Elements).toHaveLength(2)
  await page.getByRole('button', { name: /撤销/ }).click()
  await expect(page.locator('.tree-select[title="Rows[0].Cells[0].Rows[0].Cells[0].Elements[1]"]')).toHaveCount(0)
  await page.getByRole('button', { name: /重做/ }).click()
  await expect(page.locator('.tree-select[title="Rows[0].Cells[0].Rows[0].Cells[0].Elements[1]"]')).toBeVisible()
})

test('V3 共享内容选择定位定义，实例参数仍独立', async ({ page }) => {
  await page.goto('/')
  const fixture = { Version: 3, Width: 240, Height: 120,
    Components: [{ Id: 'badge', Width: 120, Height: 24,
      Elements: [{ Type: 1, FontSize: 24, Field: { Name: 'Name' } }] }],
    Rows: [{ Cells: [{ Elements: [
      { Type: 6, ComponentId: 'badge', DataPath: 'First' },
      { Type: 6, ComponentId: 'badge', DataPath: 'Second' },
    ] }] }] }
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify({ First: { Name: 'A' }, Second: { Name: 'B' } }))
  await reloadTemplateJson(page, Buffer.from(JSON.stringify(fixture)))
  const shared = page.locator('.tree-select[title="编辑共享定义 Components[0].Elements[0]"]').first()
  await expect(shared).toContainText('共享')
  await shared.click()
  await expect(page.locator('.inspector-selection')).toContainText('Components[0].Elements[0]')
  const fieldPaths = await page.locator('#business-field-paths option').evaluateAll((options) =>
    options.map((option) => (option as HTMLOptionElement).value))
  expect(fieldPaths).toContain('Name')
  expect(fieldPaths).toContain('$root.First.Name')
  const saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(saved.Rows[0].Cells[0].Elements.map((item: { DataPath: string }) => item.DataPath)).toEqual(['First', 'Second'])
})

test('V3 实例参数独立编辑且共享定义修改影响全部实例', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify({ First: { Name: 'A' }, Second: { Name: 'B' } }))
  const fixture = { Version: 3, Width: 240, Height: 120,
    Components: [{ Id: 'badge', Width: 120, Height: 24,
      Parameters: { caption: { Required: true } },
      Elements: [{ Type: 1, FontSize: 24, Field: { Name: '$param.caption' } }] }],
    Rows: [{ Cells: [{ Elements: [
      { Type: 6, ComponentId: 'badge', DataPath: 'First', Parameters: { caption: { Value: 'ONE' } } },
      { Type: 6, ComponentId: 'badge', DataPath: 'Second', StartCoordinate: { X: 0, Y: 30 },
        Parameters: { caption: { Value: 'TWO' } } },
    ] }] }] }
  await reloadTemplateJson(page, Buffer.from(JSON.stringify(fixture)))
  await page.locator('.tree-select[title="Rows[0].Cells[0].Elements[0]"]').click()
  await page.getByRole('textbox', { name: 'caption 静态 JSON' }).fill('"CHANGED"')
  await page.getByRole('textbox', { name: 'caption 静态 JSON' }).press('Tab')
  let saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(saved.Rows[0].Cells[0].Elements[0].Parameters.caption.Value).toBe('CHANGED')
  expect(saved.Rows[0].Cells[0].Elements[1].Parameters.caption.Value).toBe('TWO')
  const output = new TextDecoder().decode(await exportFirstCpclPage(page))
  expect(output).toContain('CHANGED')
  expect(output).toContain('TWO')

  await page.locator('.tree-select[title="编辑共享定义 Components[0].Elements[0]"]').first().click()
  const paths = await page.locator('#business-field-paths option').evaluateAll((options) =>
    options.map((option) => (option as HTMLOptionElement).value))
  expect(paths).toContain('$param.caption')
  await page.locator('.inspector-section').filter({ hasText: '字段路径' }).getByRole('button', { name: '固定文字' }).click()
  await page.getByLabel('显示内容').fill('GLOBAL')
  saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(saved.Components[0].Elements[0].Field.Value).toBe('GLOBAL')
  expect(new TextDecoder().decode(await exportFirstCpclPage(page)).match(/GLOBAL/g)).toHaveLength(2)
  await page.getByRole('button', { name: /撤销/ }).click()
  await expect.poll(async () => JSON.parse((await exportTemplateJson(page)).toString('utf8')).Components[0].Elements[0].Field.Name)
    .toBe('$param.caption')
  await page.getByRole('button', { name: /重做/ }).click()
  await expect.poll(async () => JSON.parse((await exportTemplateJson(page)).toString('utf8')).Components[0].Elements[0].Field.Value)
    .toBe('GLOBAL')
})

test('V3 组合跨页片段在画布上定位同一模板节点', async ({ page }) => {
  await page.goto('/')
  const data = { Items: Array.from({ length: 9 }, (_, index) => ({ Name: `ITEM ${index + 1}` })) }
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify(data))
  const sourcePath = 'Rows[0].Cells[0].Elements[0].Rows[0].Cells[0].Elements[0]'
  const fixture = { Version: 3, Width: 240, Height: 90,
    Rows: [{ Cells: [{ Elements: [{ Type: 6, Rows: [{ RepeatSource: 'Items', Height: 28,
      Cells: [{ Elements: [{ Type: 1, FontSize: 24, Field: { Name: 'Name' } }] }] }] }] }] }] }
  await reloadTemplateJson(page, Buffer.from(JSON.stringify(fixture)))
  const response = await page.request.post('/api/designer/preview', { data: { template: fixture, data } })
  expect(response.ok()).toBe(true)
  const layout = await response.json()
  expect(layout.pages.length).toBeGreaterThan(1)
  const fragment = layout.pages[1].elements.find((element: { sourcePath: string; type: number }) =>
    element.sourcePath === sourcePath && element.type === 1)
  expect(fragment).toBeDefined()
  await page.locator('.page-pager button[title="下一页"]').click()
  await page.locator('.page-tree-row').click()
  const frame = await page.locator('.page-frame').boundingBox()
  expect(frame).not.toBeNull()
  await page.mouse.click(frame!.x + (fragment.x + 12) * 0.72, frame!.y + (fragment.y + 12) * 0.72)
  await expect(page.locator('.inspector-selection')).toContainText(sourcePath)
  expect(JSON.parse((await exportTemplateJson(page)).toString('utf8'))).toEqual(fixture)
})

test('二进制元素尺寸写回打印字段且条码宽窄比只开放有效值', async ({ page }) => {
  await openBlankTemplate(page)
  await page.locator('.element-tools button').filter({ hasText: '二维码' }).click()
  const qrDimensions = dimensions(page)
  await qrDimensions.nth(2).fill('140')
  await qrDimensions.nth(2).press('Tab')
  await qrDimensions.nth(3).fill('120')
  await qrDimensions.nth(3).press('Tab')
  await expect(qrDimensions.nth(2)).toHaveValue('140')
  await expect(qrDimensions.nth(3)).toHaveValue('120')

  await page.locator('.element-tools button').filter({ hasText: '一维条码' }).click()
  const ratio = page.locator('.inspector-section').filter({ hasText: '条码参数' })
    .locator('label').filter({ hasText: '宽窄比' }).locator('select')
  const allowedRatios = await ratio.locator('option').evaluateAll((options) => options.map((option) => (option as HTMLOptionElement).value))
  expect(allowedRatios).not.toContain('5')
  expect(allowedRatios).not.toContain('19')
  await ratio.selectOption('20')

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const template = JSON.parse(await readFile(await (await downloadPromise).path(), 'utf8'))
  const elements = template.Rows[0].Cells[0].Elements
  expect(elements.find((element: { Type: number }) => element.Type === 3)).toMatchObject({ Width: 140, Height: 120 })
  expect(elements.find((element: { Type: number }) => element.Type === 4).Ratio).toBe(20)
})

test('旋转条码尺寸面板按旋转后占位编辑并写回原始宽高', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  await page.locator('.element-tools button').filter({ hasText: '一维条码' }).click()

  const rotation = page.getByLabel('元素旋转')
  await rotation.selectOption('90')
  const barcodeSettings = page.locator('.inspector-section').filter({ hasText: '条码参数' })
  await barcodeSettings.getByLabel('可读文字预留高度').fill('12')
  await barcodeSettings.getByLabel('可读文字预留高度').press('Tab')

  const position = dimensions(page)
  await expect(position.nth(2)).toHaveValue('58')
  await expect(position.nth(3)).toHaveValue('220')
  await position.nth(2).fill('70')
  await position.nth(2).press('Tab')
  await position.nth(3).fill('240')
  await position.nth(3).press('Tab')

  const saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  const element = saved.Rows[0].Cells[0].Elements.find((item: { Type: number }) => item.Type === 4)
  expect(element).toMatchObject({ Rotation: 90, Width: 240, Height: 58, HumanReadableReserveHeight: 12 })
  expect(element.EndCoordinate.X - element.StartCoordinate.X).toBe(70)
  expect(element.EndCoordinate.Y - element.StartCoordinate.Y).toBe(240)
})

test('旋转二维码同步占用端点并按旋转轴写回原始尺寸', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  await page.locator('.element-tools button').filter({ hasText: '二维码' }).click()

  const position = dimensions(page)
  await position.nth(2).fill('140')
  await position.nth(2).press('Tab')
  await position.nth(3).fill('80')
  await position.nth(3).press('Tab')

  const rotation = page.getByLabel('元素旋转')
  await rotation.selectOption('90')
  await expect(position.nth(2)).toHaveValue('80')
  await expect(position.nth(3)).toHaveValue('140')
  await position.nth(2).fill('64')
  await position.nth(2).press('Tab')
  await position.nth(3).fill('156')
  await position.nth(3).press('Tab')

  const assertRotation = async (angle: 0 | 90 | 180 | 270, width: number, height: number) => {
    await rotation.selectOption(String(angle))
    await expect(position.nth(2)).toHaveValue(String(width))
    await expect(position.nth(3)).toHaveValue(String(height))
    const saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
    const element = saved.Rows[0].Cells[0].Elements.find((item: { Type: number }) => item.Type === 3)
    expect(element).toMatchObject({ Rotation: angle, Width: 156, Height: 64 })
    expect(element.EndCoordinate.X - element.StartCoordinate.X).toBe(width)
    expect(element.EndCoordinate.Y - element.StartCoordinate.Y).toBe(height)
  }

  await assertRotation(90, 64, 156)
  await assertRotation(180, 156, 64)
  await assertRotation(270, 64, 156)
  await assertRotation(0, 156, 64)
})

test('旋转自动尺寸条码同步服务端占用端点', async ({ page, request }) => {
  await openBlankTemplate(page)
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  const autoTemplate = {
    Version: 1,
    Width: 576,
    Height: 160,
    Rows: [{ Height: 160, Cells: [{ Width: 576, Elements: [{
      Type: 4,
      BarcodeType: 0,
      LineWidth: 1,
      Width: 0,
      Height: 24,
      HumanReadable: false,
      Field: { Value: '1234' },
    }] }] }],
  }
  const autoPreviewResponse = await request.post('/api/designer/preview', {
    data: { template: autoTemplate, data: {}, protocol: 'Tspl' },
  })
  expect(autoPreviewResponse.ok(), await autoPreviewResponse.text()).toBe(true)
  const autoPreview = await autoPreviewResponse.json() as {
    pages: Array<{ elements: Array<{ sourcePath: string; type: number; width: number; height: number }> }>
  }
  const layout = autoPreview.pages.flatMap((part) => part.elements)
    .find((element) => element.sourcePath === 'Rows[0].Cells[0].Elements[0]')
  expect(layout).toMatchObject({ type: 4, height: 24 })
  expect(layout?.width).toBeGreaterThan(0)

  const template = {
    ...autoTemplate,
    Rows: [{ Height: 160, Cells: [{ Width: 576, Elements: [{
      ...autoTemplate.Rows[0].Cells[0].Elements[0],
      StartCoordinate: { X: 24, Y: 40 },
      EndCoordinate: { X: 24 + layout!.width, Y: 40 + layout!.height },
    }] }] }],
  }
  const previewPromise = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await page.locator('[data-testid="template-import"]').setInputFiles({
    name: 'auto-barcode-rotation.json',
    mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(template)),
  })
  await previewPromise
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.locator('.tree-row.tree-element .tree-select').click()

  const position = dimensions(page)
  await expect(position.nth(2)).toHaveValue(String(layout!.width))
  await expect(position.nth(3)).toHaveValue(String(layout!.height))
  await page.getByLabel('元素旋转').selectOption('90')
  await expect(position.nth(2)).toHaveValue(String(layout!.height))
  await expect(position.nth(3)).toHaveValue(String(layout!.width))

  const saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  const element = saved.Rows[0].Cells[0].Elements[0]
  expect(element).toMatchObject({ Rotation: 90, Width: 0, Height: 24 })
  expect(element.EndCoordinate.X - element.StartCoordinate.X).toBe(layout!.height)
  expect(element.EndCoordinate.Y - element.StartCoordinate.Y).toBe(layout!.width)
})

test('条码 HRI 与显式静区可往返，并在关闭 HRI 时清理预留高度', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  await page.locator('.element-tools button').filter({ hasText: '一维条码' }).click()

  const barcodeSettings = page.locator('.inspector-section').filter({ hasText: '条码参数' })
  await page.getByLabel('显示内容').fill('12345')
  await page.getByLabel('显示内容').press('Tab')
  await barcodeSettings.getByLabel('条码可读文字模式').selectOption('on')
  await barcodeSettings.getByLabel('可读文字预留高度').fill('12')
  await barcodeSettings.getByLabel('可读文字预留高度').press('Tab')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

  const quietZone = page.getByLabel('静区配置方式')
  await quietZone.selectOption('explicit')
  await page.getByLabel('静区左侧点数').fill('8')
  await page.getByLabel('静区左侧点数').press('Tab')
  await page.getByLabel('静区右侧点数').fill('12')
  await page.getByLabel('静区右侧点数').press('Tab')
  await page.getByLabel('静区上侧点数').fill('4')
  await page.getByLabel('静区上侧点数').press('Tab')
  await page.getByLabel('静区下侧点数').fill('6')
  await page.getByLabel('静区下侧点数').press('Tab')
  const occupiedDimensions = dimensions(page)
  await occupiedDimensions.nth(2).fill('320')
  await occupiedDimensions.nth(2).press('Tab')
  await occupiedDimensions.nth(3).fill('78')
  await occupiedDimensions.nth(3).press('Tab')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

  let saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  let element = saved.Rows[0].Cells[0].Elements.find((item: { Type: number }) => item.Type === 4)
  expect(element).toMatchObject({ HumanReadable: true, HumanReadableReserveHeight: 12,
    QuietZone: { Left: 8, Top: 4, Right: 12, Bottom: 6 } })
  const readablePreview = await page.request.post('/api/designer/preview', {
    data: { template: saved, data: {}, protocol: 'Tspl' },
  })
  expect(readablePreview.ok(), await readablePreview.text()).toBe(true)

  await barcodeSettings.getByLabel('条码可读文字模式').selectOption('off')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  element = saved.Rows[0].Cells[0].Elements.find((item: { Type: number }) => item.Type === 4)
  expect(element).toMatchObject({ HumanReadable: false,
    QuietZone: { Left: 8, Top: 4, Right: 12, Bottom: 6 } })
  expect(element.HumanReadableReserveHeight).toBeUndefined()
  const disabledPreview = await page.request.post('/api/designer/preview', {
    data: { template: saved, data: {}, protocol: 'Tspl' },
  })
  expect(disabledPreview.ok()).toBe(true)
})

test('HRI 通过 Designer Host 输出 CPCL、TSPL、ZPL 与 ESC/POS Raster', async ({ page }) => {
  await openBlankTemplate(page)
  const template = {
    Version: 1,
    Width: 500,
    Height: 120,
    Rows: [{ Height: 120, Cells: [{ Width: 500, Elements: [{
      Type: 4,
      BarcodeType: 0,
      Width: 320,
      Height: 40,
      HumanReadable: true,
      HumanReadableReserveHeight: 12,
      QuietZone: { Left: 8, Top: 4, Right: 12, Bottom: 6 },
      Field: { Value: '12345' },
    }] }] }],
  }
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const render = async (options: Record<string, unknown>, extension: string) => {
    const response = await page.request.post(`${apiUrl}/api/designer/render`, {
      data: { template, data: {}, ...options },
    })
    expect(response.ok(), await response.text()).toBe(true)
    const archive = await JSZip.loadAsync(await response.body())
    const file = archive.file(`page-001.${extension}`)
    expect(file).not.toBeNull()
    return file!.async('uint8array')
  }

  const cpcl = new TextDecoder('ascii').decode(await render(
    { protocol: 'Cpcl', encoding: 'Ascii', feedLines: 0, cutAfterPrint: false }, 'cpcl'))
  expect(cpcl).toContain('BT 0 24 0')
  expect(cpcl).toContain('BT OFF')

  const tspl = new TextDecoder('ascii').decode(await render(
    { protocol: 'Tspl', encoding: 'Ascii', feedLines: 0, cutAfterPrint: false }, 'tspl'))
  expect(tspl).toMatch(/BARCODE .*?,40,1,/)

  const zpl = new TextDecoder('ascii').decode(await render(
    { protocol: 'Zpl', encoding: 'Ascii', feedLines: 0, cutAfterPrint: false }, 'zpl'))
  expect(zpl).toContain('^BCN,40,Y')

  const raster = await render(
    { protocol: 'EscPos', escPosLayoutMode: 'Raster', encoding: 'Ascii', feedLines: 0, cutAfterPrint: false },
    'escpos')
  expect(Array.from(raster)).toContain(0x1d)
  expect(Array.from(raster)).toContain(0x76)
  expect(Array.from(raster)).toContain(0x30)
})

test('导入 HRI 关闭且残留预留高度时可在属性面板修复', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  const invalidTemplate = {
    Version: 1,
    Width: 576,
    Height: 360,
    Rows: [{ Cells: [{ Elements: [{
      Type: 4,
      BarcodeType: 0,
      LineWidth: 2,
      Ratio: 2,
      Width: 320,
      Height: 56,
      Field: { Value: '12345' },
      StartCoordinate: { X: 24, Y: 36 },
      EndCoordinate: { X: 344, Y: 114 },
      HumanReadable: false,
      HumanReadableReserveHeight: 12,
    }] }] }],
  }
  await page.locator('[data-testid="template-import"]').setInputFiles({
    name: 'invalid-hri.json',
    mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(invalidTemplate)),
  })
  await expect(page.locator('.status-error')).toBeVisible()
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  await expect(page.getByText('条码可读文字已关闭，当前预留高度无效；清除后才能通过严格预览。')).toBeVisible()
  await page.getByRole('button', { name: '清除无效的可读文字预留高度' }).click()
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  const element = saved.Rows[0].Cells[0].Elements[0]
  expect(element.HumanReadable).toBe(false)
  expect(element.HumanReadableReserveHeight).toBeUndefined()
})

test('属性面板和方向键移动组件时保持完整尺寸与端点', async ({ page }) => {
  await openBlankTemplate(page)
  await page.locator('.element-tools button').filter({ hasText: '文字' }).click()
  const textPosition = dimensions(page)
  const originalTextWidth = Number(await textPosition.nth(2).inputValue())
  const originalTextHeight = Number(await textPosition.nth(3).inputValue())
  await textPosition.nth(0).fill('100')
  await textPosition.nth(0).press('Tab')
  await expect(textPosition.nth(0)).toHaveValue('100')
  await expect(textPosition.nth(2)).toHaveValue(String(originalTextWidth))
  await expect(textPosition.nth(3)).toHaveValue(String(originalTextHeight))

  await page.locator('.designer-app').focus()
  await page.keyboard.press('ArrowDown')
  await expect(textPosition.nth(1)).toHaveValue('37')
  await expect(textPosition.nth(3)).toHaveValue(String(originalTextHeight))

  await page.locator('.element-tools button').filter({ hasText: '线段' }).click()
  const linePosition = dimensions(page)
  const startY = Number(await linePosition.nth(1).inputValue())
  const endX = Number(await linePosition.nth(2).inputValue())
  const endY = Number(await linePosition.nth(3).inputValue())
  await linePosition.nth(0).fill('100')
  await linePosition.nth(0).press('Tab')
  await expect(linePosition.nth(0)).toHaveValue('100')
  await expect(linePosition.nth(2)).toHaveValue(String(endX + 76))
  await expect(linePosition.nth(1)).toHaveValue(String(startY))
  await expect(linePosition.nth(3)).toHaveValue(String(endY))

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const template = JSON.parse(await readFile(await (await downloadPromise).path(), 'utf8'))
  const elements = template.Rows[0].Cells[0].Elements
  expect(elements[0].StartCoordinate).toEqual({ X: 100, Y: 37 })
  expect(elements[0].EndCoordinate).toEqual({ X: 320, Y: 73 })
  expect(elements.find((element: { Type: number }) => element.Type === 5)).toMatchObject({
    StartCoordinate: { X: 100, Y: startY },
    LineEndCoordinate: { X: endX + 76, Y: endY },
  })
})

test('价签和旧版重制样例由服务端模板提供', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const picker = page.locator('.sample-picker select')
  await expect(picker.locator('option[value="service:price-label-15"]')).toHaveCount(1)
  await expect(picker.locator('option[value="service:price-label-70"]')).toHaveCount(1)
  await expect(picker.locator('option[value="service:price-label-90"]')).toHaveCount(1)
  await expect(picker.locator('option[value="service:pm0025"]')).toHaveCount(1)
  await expect(picker.locator('option[value="service:pm0034"]')).toHaveCount(1)
  await expect(picker.locator('option[value="15"]')).toHaveCount(0)
  await expect(picker.locator('option[value="pm0025"]')).toHaveCount(0)

  for (const [id, size] of [
    ['price-label-15', '159 × 159 点'],
    ['price-label-70', '540 × 300 点'],
    ['price-label-90', '671 × 320 点'],
    ['pm0025', '671 × 360 点'],
    ['pm0034', '600 × 320 点'],
  ]) {
    await picker.selectOption(`service:${id}`)
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
    await expect(page.locator('.canvas-breadcrumb strong')).toHaveText(size)
  }
})

test('重复明细引擎预览跨页并在每页保留表头', async ({ page }) => {
  await page.goto('/')
  const picker = page.locator('.sample-picker select')
  await expect(picker.locator('option[value="service:receipt"]')).toHaveCount(1)
  await picker.selectOption('service:receipt')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await expect(page.locator('.file-state')).toContainText('分页小票')
  await page.getByRole('button', { name: 'JSON', exact: true }).click()

  const data = {
    Title: '分页验收小票',
    Items: Array.from({ length: 40 }, (_, index) => ({ Name: `商品-${index + 1}`, Quantity: index === 0 ? 0 : 1 })),
  }
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify(data))
  await expect(page.locator('.page-pager span')).toContainText('/ ')
  await expect(page.locator('.page-pager span')).not.toHaveText('1 / 1')

  const samplesResponse = await page.request.get('/api/designer/samples')
  const samplesBody = await samplesResponse.json()
  const receipt = samplesBody.samples.find((sample: { id: string }) => sample.id === 'receipt')
  const previewResponse = await page.request.post('/api/designer/preview', { data: { template: receipt.template, data } })
  expect(previewResponse.ok()).toBe(true)
  const preview = await previewResponse.json()
  expect(preview.pages.length).toBeGreaterThan(1)
  expect(preview.pages.every((part: { elements: Array<{ sourcePath: string; text?: string }> }) =>
    part.elements.some((element) => element.sourcePath === 'Rows[0].Cells[0].Elements[0]' && element.text === '分页验收小票'))).toBe(true)
  const indices = [...new Set(preview.pages.flatMap((part: { elements: Array<{ sourcePath: string; instanceIndices?: number[] }> }) =>
    part.elements.filter((element) => element.sourcePath === 'Rows[1].Cells[0].Elements[0]').flatMap((element) => element.instanceIndices ?? [])))].sort((a, b) => a - b)
  expect(indices).toEqual(Array.from({ length: 39 }, (_, index) => index + 1))
  const cpclResponse = await page.request.post('/api/designer/render', { data: { template: receipt.template, data } })
  expect(cpclResponse.ok()).toBe(true)
  const cpclArchive = await JSZip.loadAsync(await cpclResponse.body())
  expect(Object.keys(cpclArchive.files).filter((name) => /^page-\d{3}\.cpcl$/.test(name))).toHaveLength(preview.pages.length)
})

test('错误预览显示对应模板路径且 CPCL 导出可 JSON 往返', async ({ page }) => {
  await openBlankTemplate(page)
  await page.locator('.element-tools button').filter({ hasText: '线段' }).click()
  const linePosition = dimensions(page)
  await linePosition.nth(2).fill('600')
  await linePosition.nth(2).press('Tab')
  await expect(page.locator('.status-error')).toContainText('Rows[0].Cells[0].Elements[0]')
  await expect(page.locator('.page-pager')).toHaveCount(0)

  await page.locator('.editor-toolbar .tool-delete').click()
  await page.locator('.element-tools button').filter({ hasText: '文字' }).click()
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await expect(page.getByRole('button', { name: '生成 CPCL' })).toBeEnabled()

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 CPCL' }).click()
  const download = await downloadPromise
  const archive = await JSZip.loadAsync(await readFile(await download.path()))
  const firstPage = archive.file('page-001.cpcl')
  expect(firstPage).not.toBeNull()
  const firstPageContents = await firstPage!.async('string')
  expect(firstPageContents).toContain('! 0')

  const jsonDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const exportedJson = await readFile(await (await jsonDownloadPromise).path())
  await page.locator('input[type="file"]').first().setInputFiles({ name: 'roundtrip.json', mimeType: 'application/json', buffer: exportedJson })
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

  const repeatedDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 CPCL' }).click()
  const repeatedDownload = await repeatedDownloadPromise
  const repeatedArchive = await JSZip.loadAsync(await readFile(await repeatedDownload.path()))
  expect(await repeatedArchive.file('page-001.cpcl')!.async('string')).toBe(firstPageContents)
})

test('协议选择可导出 TSPL 和 ESC/POS Raster 页面', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  await expect(page.getByRole('button', { name: '生成 TSPL' })).toBeEnabled()
  let downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 TSPL' }).click()
  let archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
  expect(archive.file('page-001.tspl')).not.toBeNull()

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await expect(page.getByRole('button', { name: '生成 ESC/POS' })).toBeEnabled()
  downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 ESC/POS' }).click()
  archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
  expect(archive.file('page-001.escpos')).not.toBeNull()
})

test('文字旋转保留旋转前宽度、TSPL 占位和 JSON 往返，并支持 ESC/POS Raster 非零旋转', async ({ page }) => {
  await page.goto('/')
  const template = {
    Version: 1,
    Width: 240,
    Height: 120,
    Rows: [{ Height: 100, Cells: [{ Width: 240, Elements: [{
      Type: 1,
      FontSize: 24,
      Width: 80,
      Field: { Value: 'AB' },
      StartCoordinate: { X: 8, Y: 8 },
    }] }] }],
  }
  const tsplPreview = async (value: typeof template) => {
    const response = await page.request.post('/api/designer/preview', { data: { template: value, data: {}, protocol: 'Tspl' } })
    expect(response.ok()).toBe(true)
    return response.json()
  }

  const baseLayout = await tsplPreview(template)
  const baseElement = baseLayout.pages[0].elements.find((item: { type: number }) => item.type === 1)
  expect(baseElement).toMatchObject({ rotation: 0 })

  const rotated = JSON.parse(JSON.stringify(template)) as typeof template & { Rows: Array<{ Cells: Array<{ Elements: Array<Record<string, unknown>> }> }> }
  rotated.Rows[0].Cells[0].Elements[0].Rotation = 90
  const rotatedLayout = await tsplPreview(rotated)
  const rotatedElement = rotatedLayout.pages[0].elements.find((item: { type: number }) => item.type === 1)
  expect(rotatedElement).toMatchObject({ rotation: 90 })
  expect(rotatedElement.width).toBe(baseElement.height)
  expect(rotatedElement.height).toBe(baseElement.width)

  await reloadTemplateJson(page, Buffer.from(JSON.stringify(rotated)), 'rotated-text.json')
  const saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(saved.Rows[0].Cells[0].Elements[0]).toMatchObject({ Rotation: 90, Width: 80 })
  await reloadTemplateJson(page, Buffer.from(JSON.stringify(saved)), 'rotated-text-roundtrip.json')
  const reloaded = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(reloaded.Rows[0].Cells[0].Elements[0]).toMatchObject({ Rotation: 90, Width: 80 })

  const escPosResponse = await page.request.post('/api/designer/preview', {
    data: { template: rotated, data: {}, protocol: 'EscPos', escPosLayoutMode: 'Raster' },
  })
  expect(escPosResponse.ok()).toBe(true)
  const escPosLayout = await escPosResponse.json()
  const escPosElement = escPosLayout.pages[0].elements.find((item: { type: number }) => item.type === 1)
  expect(escPosElement).toMatchObject({ rotation: 90 })
})

test('文字属性旋转同步已有占用端点并保留旋转前行宽', async ({ page }) => {
  await page.goto('/')
  const template = {
    Version: 1,
    Width: 240,
    Height: 120,
    Rows: [{ Height: 100, Cells: [{ Width: 240, Elements: [{
      Type: 1,
      FontSize: 24,
      Width: 80,
      Field: { Value: 'AB' },
      StartCoordinate: { X: 12, Y: 16 },
      EndCoordinate: { X: 92, Y: 40 },
    }] }] }],
  }
  await reloadTemplateJson(page, Buffer.from(JSON.stringify(template)), 'text-rotation-end-coordinate.json')
  await page.locator('.tree-select[title="Rows[0].Cells[0].Elements[0]"]').click()
  const position = dimensions(page)
  const rotation = page.getByLabel('元素旋转')
  const assertRotation = async (angle: 0 | 90 | 180 | 270, width: number, height: number) => {
    await rotation.selectOption(String(angle))
    await expect(position.nth(2)).toHaveValue(String(width))
    await expect(position.nth(3)).toHaveValue(String(height))
    const saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
    const element = saved.Rows[0].Cells[0].Elements[0]
    expect(element).toMatchObject({ Rotation: angle, Width: 80 })
    expect(element.EndCoordinate).toEqual({ X: 12 + width, Y: 16 + height })
  }

  await assertRotation(90, 24, 80)
  await assertRotation(180, 80, 24)
  await assertRotation(270, 24, 80)
  await assertRotation(0, 80, 24)
})

test('协议选择可导出 ZPL 和 ESC/POS Native 页面，并拒绝 Native 图形布局', async ({ page }) => {
  await openBlankTemplate(page)

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Zpl')
  await expect(page.getByRole('button', { name: '生成 ZPL' })).toBeEnabled()
  let downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 ZPL' }).click()
  let archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
  const zpl = archive.file('page-001.zpl')
  expect(zpl).not.toBeNull()
  expect(await zpl!.async('string')).toContain('^XA')
  expect(await zpl!.async('string')).toContain('^XZ')

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosNative')
  await expect(page.getByRole('button', { name: '生成 ESC/POS' })).toBeEnabled()
  downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 ESC/POS' }).click()
  archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
  const native = archive.file('page-001.escpos')
  expect(native).not.toBeNull()
  const nativeBytes = await native!.async('uint8array')
  expect(nativeBytes.length).toBeGreaterThan(0)
  expect(nativeBytes[0]).toBe(0x1b)

  const unsupported = {
    Version: 1,
    Width: 240,
    Height: 120,
    Rows: [{ Cells: [{ Elements: [{
      Type: 5,
      LineWidth: 2,
      StartCoordinate: { X: 8, Y: 8 },
      LineEndCoordinate: { X: 120, Y: 8 },
    }] }] }],
  }
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Cpcl')
  await reloadTemplateJson(page, Buffer.from(JSON.stringify(unsupported)), 'native-line.json')
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosNative')
  const statusError = page.locator('.status-error')
  await expect(statusError).toContainText('Rows[0].Cells[0].Elements[0]')
  await expect(statusError).toContainText('Native')
  await expect(page.locator('.page-pager')).toHaveCount(0)

  await page.getByRole('button', { name: '生成 ESC/POS' }).click()
  await expect(page.locator('.toast-message')).toContainText('Native')
  await expect(page.locator('.toast-message')).toContainText('不支持')
})

test('同一二维码模板可在 Designer 导出 CPCL、TSPL、ZPL 和 ESC/POS Raster', async ({ page }) => {
  test.setTimeout(90_000)
  const template = {
    Version: 1,
    Width: 240,
    Height: 160,
    Rows: [{ Height: 160, Cells: [{ Width: 240, Elements: [{
      Type: 3,
      UnitWidth: 4,
      ErrorLevel: 'M',
      Width: 120,
      Height: 120,
      Field: { Value: 'QR-CROSS-PROTOCOL' },
    }] }] }],
  }
  await importDesignerProject(page, designerProject(template, 'Cpcl'), 'cross-protocol-qr.json')

  const protocol = page.getByRole('combobox', { name: '输出协议' })
  const cases = [
    { value: 'Cpcl', button: '生成 CPCL', file: 'page-001.cpcl', kind: 'text', marker: 'B QR' },
    { value: 'Tspl', button: '生成 TSPL', file: 'page-001.tspl', kind: 'text', marker: 'QRCODE' },
    { value: 'Zpl', button: '生成 ZPL', file: 'page-001.zpl', kind: 'text', marker: '^BQ' },
    { value: 'EscPosRaster', button: '生成 ESC/POS', file: 'page-001.escpos', kind: 'bytes', marker: [0x1D, 0x76, 0x30] },
  ] as const

  for (const [index, item] of cases.entries()) {
    if (index > 0) {
      const previewResponse = page.waitForResponse((response) =>
        response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
      await protocol.selectOption(item.value)
      await previewResponse
    }
    await expect(protocol).toHaveValue(item.value)
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

    const downloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: item.button }).click()
    const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
    const entry = archive.file(item.file)
    expect(entry).not.toBeNull()
    if (item.kind === 'text') {
      const output = await entry!.async('string')
      expect(output).toContain(item.marker)
      expect(output).toContain('QR-CROSS-PROTOCOL')
    } else {
      const bytes = await entry!.async('uint8array')
      expect(Array.from(bytes).some((_, offset) => item.marker.every((value, index) => bytes[offset + index] === value))).toBe(true)
    }
  }
})

test('同一分页模板可在 Designer 导出四种协议并保持页数与 ZIP 映射一致', async ({ page }) => {
  test.setTimeout(120_000)
  await importDesignerProject(page, designerProject(crossProtocolPaginationTemplate, 'Cpcl'), 'cross-protocol-pagination.json')
  await setDesignerData(page, crossProtocolPaginationData)
  await expect(page.locator('.page-pager span')).not.toHaveText('1 / 1')

  const protocol = page.getByRole('combobox', { name: '输出协议' })
  const cases = [
    { value: 'Cpcl', protocol: 'Cpcl', button: '生成 CPCL', extension: 'cpcl', kind: 'text', marker: '! 0' },
    { value: 'Tspl', protocol: 'Tspl', button: '生成 TSPL', extension: 'tspl', kind: 'text', marker: 'SIZE' },
    { value: 'Zpl', protocol: 'Zpl', button: '生成 ZPL', extension: 'zpl', kind: 'text', marker: '^XA' },
    { value: 'EscPosRaster', protocol: 'EscPos', escPosLayoutMode: 'Raster', button: '生成 ESC/POS', extension: 'escpos', kind: 'bytes', marker: [0x1D, 0x76, 0x30] },
  ] as const

  let expectedPageCount: number | undefined
  for (const [index, item] of cases.entries()) {
    if (index > 0) {
      const previewResponse = page.waitForResponse((response) =>
        response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
      await protocol.selectOption(item.value)
      await previewResponse
    }
    await expect(protocol).toHaveValue(item.value)
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

    const payload = {
      template: crossProtocolPaginationTemplate,
      data: crossProtocolPaginationData,
      encoding: 'Ascii',
      protocol: item.protocol,
      ...(item.escPosLayoutMode ? { escPosLayoutMode: item.escPosLayoutMode } : {}),
    }
    const previewResponse = await page.request.post('/api/designer/preview', { data: payload })
    expect(previewResponse.status()).toBe(200)
    const preview = await previewResponse.json()
    expect(preview.pages.length).toBeGreaterThan(1)
    if (expectedPageCount === undefined) expectedPageCount = preview.pages.length
    expect(preview.pages.length).toBe(expectedPageCount)

    const downloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: item.button }).click()
    const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
    const pageNames = Object.keys(archive.files)
      .filter((name) => new RegExp(`^page-\\d{3}\\.${item.extension}$`).test(name))
      .sort()
    expect(pageNames).toEqual(Array.from({ length: expectedPageCount }, (_, pageIndex) =>
      `page-${String(pageIndex + 1).padStart(3, '0')}.${item.extension}`))

    for (const pageName of pageNames) {
      const entry = archive.file(pageName)
      expect(entry).not.toBeNull()
      if (item.kind === 'text') {
        expect(await entry!.async('string')).toContain(item.marker)
      } else {
        const bytes = await entry!.async('uint8array')
        expect(Array.from(bytes).some((_, offset) => item.marker.every((value, byteIndex) =>
          bytes[offset + byteIndex] === value))).toBe(true)
      }
    }
  }
})

test('同一长文本模板可在 Designer 保持四协议换行内容并导出分页页面', async ({ page }) => {
  test.setTimeout(120_000)
  const text = '0123456789'.repeat(24)
  const template = {
    Version: 2,
    Width: 180,
    Height: 80,
    Rows: [{ Height: 0, Cells: [{ Width: 180, Elements: [
      { Type: 1, FontSize: 24, Field: { Value: text, NeedFeedLine: true }, StartCoordinate: { X: 0, Y: 0 },
    }] }] }],
  }
  await importDesignerProject(page, designerProject(template, 'Cpcl'), 'cross-protocol-long-text.json')

  const protocol = page.getByRole('combobox', { name: '输出协议' })
  const cases = [
    { value: 'Cpcl', protocol: 'Cpcl', button: '生成 CPCL', extension: 'cpcl', kind: 'text' },
    { value: 'Tspl', protocol: 'Tspl', button: '生成 TSPL', extension: 'tspl', kind: 'text' },
    { value: 'Zpl', protocol: 'Zpl', button: '生成 ZPL', extension: 'zpl', kind: 'text' },
    { value: 'EscPosRaster', protocol: 'EscPos', escPosLayoutMode: 'Raster', button: '生成 ESC/POS', extension: 'escpos', kind: 'bytes' },
  ] as const

  for (const [index, item] of cases.entries()) {
    if (index > 0) {
      const previewWait = page.waitForResponse((response) =>
        response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
      await protocol.selectOption(item.value)
      await previewWait
    }
    const payload = {
      template,
      data: {},
      encoding: 'Ascii',
      protocol: item.protocol,
      ...(item.escPosLayoutMode ? { escPosLayoutMode: item.escPosLayoutMode } : {}),
    }
    const previewResponse = await page.request.post('/api/designer/preview', { data: payload })
    expect(previewResponse.status()).toBe(200)
    const preview = await previewResponse.json()
    expect(preview.pages.length).toBeGreaterThan(1)
    const expectedPageCount = preview.pages.length

    const fragments = preview.pages.flatMap((part: { elements: Array<{ sourcePath: string; text?: string }> }) => part.elements)
      .filter((element: { sourcePath: string; text?: string }) => element.sourcePath === 'Rows[0].Cells[0].Elements[0]' && element.text)
      .map((element: { text?: string }) => element.text)
    expect(fragments.length).toBeGreaterThan(1)
    expect(fragments.join('')).toBe(text)

    const downloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: item.button }).click()
    const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
    const pageNames = Object.keys(archive.files)
      .filter((name) => new RegExp(`^page-\\d{3}\\.${item.extension}$`).test(name))
      .sort()
    expect(pageNames).toEqual(Array.from({ length: expectedPageCount }, (_, pageIndex) =>
      `page-${String(pageIndex + 1).padStart(3, '0')}.${item.extension}`))

    if (item.kind === 'text') {
      const output = (await Promise.all(pageNames.map(async (name) => archive.file(name)!.async('string')))).join('\n')
      expect(output).toContain(text.slice(0, 10))
      expect(output).toContain(text.slice(-10))
    } else {
      for (const pageName of pageNames) {
        const bytes = await archive.file(pageName)!.async('uint8array')
        expect(Array.from(bytes).some((_, offset) => bytes[offset] === 0x1D && bytes[offset + 1] === 0x76 && bytes[offset + 2] === 0x30)).toBe(true)
      }
    }
  }
})

test('同一混合布局模板可在 Designer 导出四种协议并保留全部元素轨迹', async ({ page }) => {
  test.setTimeout(120_000)
  const template = {
    Version: 2,
    Width: 500,
    Height: 320,
    Rows: [{ Height: 320, Cells: [{ Width: 500, Elements: [
      { Type: 1, FontSize: 24, Field: { Value: 'MIXED-LAYOUT' }, StartCoordinate: { X: 16, Y: 12 } },
      { Type: 5, LineWidth: 2, StartCoordinate: { X: 8, Y: 52 }, LineEndCoordinate: { X: 492, Y: 52 } },
      { Type: 7, Width: 280, Height: 48, LineWidth: 2, Filled: false, StartCoordinate: { X: 8, Y: 68 } },
      { Type: 4, BarcodeType: 0, LineWidth: 1, Width: 260, Height: 60, HumanReadable: false,
        Field: { Value: '1234567890' }, StartCoordinate: { X: 16, Y: 140 } },
      { Type: 3, UnitWidth: 4, ErrorLevel: 'M', Width: 120, Height: 120,
        Field: { Value: 'MIXED-QR' }, StartCoordinate: { X: 340, Y: 120 } },
    ] }] }],
  }
  await importDesignerProject(page, designerProject(template, 'Cpcl'), 'cross-protocol-mixed-layout.json')

  const protocol = page.getByRole('combobox', { name: '输出协议' })
  const cases = [
    { value: 'Cpcl', protocol: 'Cpcl', button: '生成 CPCL', file: 'page-001.cpcl', kind: 'text', markers: ['MIXED-LAYOUT', 'L ', 'B 128', 'B QR'] },
    { value: 'Tspl', protocol: 'Tspl', button: '生成 TSPL', file: 'page-001.tspl', kind: 'text', markers: ['MIXED-LAYOUT', 'BAR ', 'BARCODE', 'QRCODE'] },
    { value: 'Zpl', protocol: 'Zpl', button: '生成 ZPL', file: 'page-001.zpl', kind: 'text', markers: ['MIXED-LAYOUT', '^GB', '^BC', '^BQ'] },
    { value: 'EscPosRaster', protocol: 'EscPos', escPosLayoutMode: 'Raster', button: '生成 ESC/POS', file: 'page-001.escpos', kind: 'bytes', markers: [[0x1D, 0x76, 0x30]] },
  ] as const

  for (const [index, item] of cases.entries()) {
    if (index > 0) {
      const previewWait = page.waitForResponse((response) =>
        response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
      await protocol.selectOption(item.value)
      await previewWait
    }
    const payload = {
      template,
      data: {},
      encoding: 'Ascii',
      protocol: item.protocol,
      ...(item.escPosLayoutMode ? { escPosLayoutMode: item.escPosLayoutMode } : {}),
    }
    const previewResponse = await page.request.post('/api/designer/preview', { data: payload })
    expect(previewResponse.status()).toBe(200)
    const preview = await previewResponse.json()
    expect(preview.pages).toHaveLength(1)
    expect(preview.pages[0].elements.map((element: { type: number }) => element.type))
      .toEqual(expect.arrayContaining([1, 3, 4, 5, 7]))

    const downloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: item.button }).click()
    const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
    const entry = archive.file(item.file)
    expect(entry).not.toBeNull()
    if (item.kind === 'text') {
      const output = await entry!.async('string')
      for (const marker of item.markers) expect(output).toContain(marker)
    } else {
      const bytes = await entry!.async('uint8array')
      for (const marker of item.markers) {
        expect(Array.from(bytes).some((_, offset) => marker.every((value, byteIndex) =>
          bytes[offset + byteIndex] === value))).toBe(true)
      }
    }
  }
})

test('同一跨页表头模板可在 Designer 保留四协议页眉表头页脚与明细轨迹', async ({ page }) => {
  test.setTimeout(120_000)
  const template = {
    Version: 2,
    Width: 320,
    Height: 120,
    Rows: [
      { Section: 'PageHeader', Height: 24, Cells: [{ Width: 320, Elements: [
        { Type: 1, FontSize: 24, Field: { Value: 'PAGE-HEADER' }, StartCoordinate: { X: 8, Y: 1 } },
        { Type: 5, LineWidth: 1, StartCoordinate: { X: 8, Y: 22 }, LineEndCoordinate: { X: 312, Y: 22 } },
      ] }] },
      { Section: 'TableHeader', Height: 24, Cells: [{ Width: 320, Elements: [
        { Type: 1, FontSize: 24, Field: { Value: 'TABLE-HEADER' }, StartCoordinate: { X: 8, Y: 1 } },
      ] }] },
      { RepeatSource: 'Items', Height: 24, Cells: [{ Width: 320, Elements: [
        { Type: 1, FontSize: 24, Field: { Name: 'Name' }, StartCoordinate: { X: 8, Y: 1 } },
      ] }] },
      { Section: 'PageFooter', Height: 24, Cells: [{ Width: 320, Elements: [
        { Type: 1, FontSize: 24, Field: { Value: 'PAGE-FOOTER' }, StartCoordinate: { X: 8, Y: 1 } },
        { Type: 7, Width: 88, Height: 20, LineWidth: 1, Filled: false, StartCoordinate: { X: 224, Y: 1 } },
      ] }] },
    ],
  }
  const data = { Items: Array.from({ length: 11 }, (_, index) => ({ Name: `DETAIL-${String(index + 1).padStart(2, '0')}` })) }
  await importDesignerProject(page, designerProject(template, 'Cpcl'), 'cross-protocol-page-sections.json')
  await setDesignerData(page, data)

  const protocol = page.getByRole('combobox', { name: '输出协议' })
  const cases = [
    { value: 'Cpcl', protocol: 'Cpcl', button: '生成 CPCL', extension: 'cpcl', kind: 'text' },
    { value: 'Tspl', protocol: 'Tspl', button: '生成 TSPL', extension: 'tspl', kind: 'text' },
    { value: 'Zpl', protocol: 'Zpl', button: '生成 ZPL', extension: 'zpl', kind: 'text' },
    { value: 'EscPosRaster', protocol: 'EscPos', escPosLayoutMode: 'Raster', button: '生成 ESC/POS', extension: 'escpos', kind: 'bytes' },
  ] as const

  for (const [index, item] of cases.entries()) {
    if (index > 0) {
      const previewWait = page.waitForResponse((response) =>
        response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
      await protocol.selectOption(item.value)
      await previewWait
    }
    const payload = {
      template,
      data,
      encoding: 'Ascii',
      protocol: item.protocol,
      ...(item.escPosLayoutMode ? { escPosLayoutMode: item.escPosLayoutMode } : {}),
    }
    const previewResponse = await page.request.post('/api/designer/preview', { data: payload })
    expect(previewResponse.status()).toBe(200)
    const preview = await previewResponse.json() as {
      pages: Array<{ elements: Array<{ sourcePath: string; type: number; text?: string; instanceIndices?: number[] }> }>
    }
    expect(preview.pages.length).toBeGreaterThan(1)
    for (const renderedPage of preview.pages) {
      expect(renderedPage.elements.some((element) => element.text === 'PAGE-HEADER')).toBe(true)
      expect(renderedPage.elements.some((element) => element.text === 'TABLE-HEADER')).toBe(true)
      expect(renderedPage.elements.some((element) => element.text === 'PAGE-FOOTER')).toBe(true)
      expect(renderedPage.elements.some((element) => element.type === 5)).toBe(true)
      expect(renderedPage.elements.some((element) => element.type === 7)).toBe(true)
    }

    const detailPath = 'Rows[2].Cells[0].Elements[0]'
    const renderedItems = preview.pages.flatMap((renderedPage) => renderedPage.elements)
      .filter((element) => element.sourcePath === detailPath)
      .sort((left, right) => (left.instanceIndices?.[0] ?? -1) - (right.instanceIndices?.[0] ?? -1))
    expect(renderedItems.map((element) => element.text)).toEqual(data.Items.map((item) => item.Name))

    const downloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: item.button }).click()
    const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
    const pageNames = Object.keys(archive.files)
      .filter((name) => new RegExp(`^page-\\d{3}\\.${item.extension}$`).test(name))
      .sort()
    expect(pageNames).toEqual(Array.from({ length: preview.pages.length }, (_, pageIndex) =>
      `page-${String(pageIndex + 1).padStart(3, '0')}.${item.extension}`))

    for (const pageName of pageNames) {
      const entry = archive.file(pageName)
      expect(entry).not.toBeNull()
      if (item.kind === 'text') {
        const output = await entry!.async('string')
        expect(output).toContain('PAGE-HEADER')
        expect(output).toContain('TABLE-HEADER')
        expect(output).toContain('PAGE-FOOTER')
      } else {
        const bytes = await entry!.async('uint8array')
        expect(Array.from(bytes).some((_, offset) => bytes[offset] === 0x1D &&
          bytes[offset + 1] === 0x76 && bytes[offset + 2] === 0x30)).toBe(true)
      }
    }
  }
})

test('同一跨页图片资源模板可在 Designer 保留旋转占位并导出四协议页面', async ({ page }) => {
  test.setTimeout(120_000)
  const template = {
    Version: 2,
    Width: 160,
    Height: 200,
    Rows: [
      { Section: 'PageHeader', Height: 20, Cells: [{ Width: 160, Elements: [
        { Type: 1, FontSize: 24, Field: { Value: 'IMG-PAGE' }, StartCoordinate: { X: 4, Y: 0 } },
      ] }] },
      { Section: 'TableHeader', Height: 20, Cells: [{ Width: 160, Elements: [
        { Type: 1, FontSize: 24, Field: { Value: 'IMG-HEAD' }, StartCoordinate: { X: 4, Y: 0 } },
      ] }] },
      { RepeatSource: 'Items', Height: 24, Cells: [{ Width: 160, Elements: [
        { Type: 2, Width: 12, Height: 6, Rotation: 90, IsCompress: true, Field: { Value: 'logo' }, StartCoordinate: { X: 4, Y: 2 } },
        { Type: 1, FontSize: 24, Field: { Name: 'Name' }, StartCoordinate: { X: 24, Y: 0 } },
      ] }] },
      { Section: 'PageFooter', Height: 20, Cells: [{ Width: 160, Elements: [
        { Type: 1, FontSize: 24, Field: { Value: 'IMG-FOOT' }, StartCoordinate: { X: 4, Y: 0 } },
      ] }] },
    ],
  }
  const data = { Items: Array.from({ length: 11 }, (_, index) => ({ Name: `I-${String(index + 1).padStart(2, '0')}` })) }
  const dataUrl = await page.evaluate(() => {
    const canvas = document.createElement('canvas')
    canvas.width = 2
    canvas.height = 1
    const context = canvas.getContext('2d')
    if (!context) throw new Error('浏览器不支持 Canvas 2D。')
    context.fillStyle = '#000'
    context.fillRect(0, 0, 1, 1)
    context.fillStyle = '#fff'
    context.fillRect(1, 0, 1, 1)
    return canvas.toDataURL('image/png')
  })
  const encoded = dataUrl.split(',')[1]
  if (!encoded) throw new Error('Canvas PNG 数据为空。')

  const bundle = new JSZip()
  bundle.file('template.json', JSON.stringify(template))
  bundle.file('resources/001-logo.png', Buffer.from(encoded, 'base64'))
  bundle.file('manifest.json', JSON.stringify({
    format: 'Bing.EasyPrint.TemplateBundle',
    version: 1,
    resources: [{ key: 'logo', path: 'resources/001-logo.png', fileName: 'logo.png', mimeType: 'image/png' }],
  }))
  const bundleBytes = await bundle.generateAsync({ type: 'nodebuffer', compression: 'DEFLATE' })

  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const importPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByTestId('template-import').setInputFiles({
    name: 'cross-page-image-resource.zip', mimeType: 'application/zip', buffer: bundleBytes,
  })
  await importPreview
  await expect(page.locator('.toast-message')).toContainText('已导入模板包 · 1 个资源')
  await setDesignerData(page, data)

  const protocol = page.getByRole('combobox', { name: '输出协议' })
  const cases = [
    { value: 'Cpcl', protocol: 'Cpcl', button: '生成 CPCL', extension: 'cpcl', kind: 'text', marker: 'CG ' },
    { value: 'Tspl', protocol: 'Tspl', button: '生成 TSPL', extension: 'tspl', kind: 'text', marker: 'BITMAP ' },
    { value: 'Zpl', protocol: 'Zpl', button: '生成 ZPL', extension: 'zpl', kind: 'text', marker: '^GFA' },
    { value: 'EscPosRaster', protocol: 'EscPos', escPosLayoutMode: 'Raster', button: '生成 ESC/POS', extension: 'escpos', kind: 'bytes', marker: [0x1D, 0x76, 0x30] },
  ] as const

  for (const [index, item] of cases.entries()) {
    if (index > 0) {
      const previewWait = page.waitForResponse((response) =>
        response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
      await protocol.selectOption(item.value)
      await previewWait
    }
    const payload = {
      template,
      data,
      resources: { logo: encoded },
      encoding: 'Ascii',
      protocol: item.protocol,
      ...(item.escPosLayoutMode ? { escPosLayoutMode: item.escPosLayoutMode } : {}),
    }
    const previewResponse = await page.request.post('/api/designer/preview', { data: payload })
    expect(previewResponse.status()).toBe(200)
    const preview = await previewResponse.json() as {
      pages: Array<{ elements: Array<{ sourcePath: string; type: number; text?: string; width: number; height: number; rotation?: number; instanceIndices?: number[] }> }>
    }
    expect(preview.pages.length).toBeGreaterThan(1)
    for (const [pageIndex, renderedPage] of preview.pages.entries()) {
      expect(renderedPage.elements.some((element) => element.text === 'IMG-PAGE'), `${item.protocol} page ${pageIndex + 1} header`).toBe(true)
      expect(renderedPage.elements.some((element) => element.text === 'IMG-HEAD'), `${item.protocol} page ${pageIndex + 1} table header`).toBe(true)
      expect(renderedPage.elements.some((element) => element.text === 'IMG-FOOT'), `${item.protocol} page ${pageIndex + 1} footer`).toBe(true)
    }

    const imagePath = 'Rows[2].Cells[0].Elements[0]'
    const images = preview.pages.flatMap((renderedPage) => renderedPage.elements)
      .filter((element) => element.sourcePath === imagePath)
      .sort((left, right) => (left.instanceIndices?.[0] ?? -1) - (right.instanceIndices?.[0] ?? -1))
    expect(images).toHaveLength(data.Items.length)
    expect(images.map((image) => [image.width, image.height, image.rotation])).toEqual(
      data.Items.map(() => [6, 12, 90]))

    const detailPath = 'Rows[2].Cells[0].Elements[1]'
    const details = preview.pages.flatMap((renderedPage) => renderedPage.elements)
      .filter((element) => element.sourcePath === detailPath)
      .sort((left, right) => (left.instanceIndices?.[0] ?? -1) - (right.instanceIndices?.[0] ?? -1))
    expect(details.map((element) => element.text)).toEqual(data.Items.map((item) => item.Name))

    const downloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: item.button }).click()
    const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
    const pageNames = Object.keys(archive.files)
      .filter((name) => new RegExp(`^page-\\d{3}\\.${item.extension}$`).test(name))
      .sort()
    expect(pageNames).toEqual(Array.from({ length: preview.pages.length }, (_, pageIndex) =>
      `page-${String(pageIndex + 1).padStart(3, '0')}.${item.extension}`))
    for (const pageName of pageNames) {
      const entry = archive.file(pageName)
      expect(entry).not.toBeNull()
      if (item.kind === 'text') {
        expect(await entry!.async('string')).toContain(item.marker as string)
      } else {
        const bytes = await entry!.async('uint8array')
        expect(Array.from(bytes).some((_, offset) => item.marker.every((value, byteIndex) =>
          bytes[offset + byteIndex] === value))).toBe(true)
      }
    }
  }
})

test('同一跨页条码混排模板可在 Designer 保留页眉表头页脚和四协议条码轨迹', async ({ page }) => {
  test.setTimeout(120_000)
  const template = {
    Version: 2,
    Width: 420,
    Height: 240,
    Rows: [
      { Section: 'PageHeader', Height: 24, Cells: [{ Width: 420, Elements: [
        { Type: 1, FontSize: 24, Field: { Value: 'BAR-PAGE' }, StartCoordinate: { X: 8, Y: 0 } },
      ] }] },
      { Section: 'TableHeader', Height: 24, Cells: [{ Width: 420, Elements: [
        { Type: 1, FontSize: 24, Field: { Value: 'BAR-HEAD' }, StartCoordinate: { X: 8, Y: 0 } },
      ] }] },
      { Section: 'Body', RepeatSource: 'Items', Height: 104, Cells: [{ Width: 420, Elements: [
        { Type: 4, BarcodeType: 0, LineWidth: 1, Width: 350, Height: 60, HumanReadable: false,
          Field: { Name: 'Code' }, StartCoordinate: { X: 8, Y: 8 } },
        { Type: 3, UnitWidth: 3, ErrorLevel: 'M', Width: 88, Height: 88,
          Field: { Name: 'Qr' }, StartCoordinate: { X: 288, Y: 8 } },
      ] }] },
      { Section: 'PageFooter', Height: 24, Cells: [{ Width: 420, Elements: [
        { Type: 1, FontSize: 24, Field: { Value: 'BAR-FOOT' }, StartCoordinate: { X: 8, Y: 0 } },
      ] }] },
    ],
  }
  const data = {
    Items: Array.from({ length: 9 }, (_, index) => ({
      Code: '1234567890',
      Qr: `QR-${String(index + 1).padStart(2, '0')}`,
    })),
  }
  await importDesignerProject(page, designerProject(template, 'Cpcl'), 'cross-protocol-barcode-sections.json')
  await setDesignerData(page, data)

  const protocol = page.getByRole('combobox', { name: '输出协议' })
  const cases = [
    { value: 'Cpcl', protocol: 'Cpcl', button: '生成 CPCL', extension: 'cpcl', kind: 'text', markers: ['B 128', 'B QR'] },
    { value: 'Tspl', protocol: 'Tspl', button: '生成 TSPL', extension: 'tspl', kind: 'text', markers: ['BARCODE', 'QRCODE'] },
    { value: 'Zpl', protocol: 'Zpl', button: '生成 ZPL', extension: 'zpl', kind: 'text', markers: ['^BC', '^BQ'] },
    { value: 'EscPosRaster', protocol: 'EscPos', escPosLayoutMode: 'Raster', button: '生成 ESC/POS', extension: 'escpos', kind: 'bytes', markers: [[0x1D, 0x76, 0x30]] },
  ] as const

  let expectedPageCount: number | undefined
  for (const [index, item] of cases.entries()) {
    if (index > 0) {
      const previewWait = page.waitForResponse((response) =>
        response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
      await protocol.selectOption(item.value)
      await previewWait
    }
    const payload = {
      template,
      data,
      encoding: 'Ascii',
      protocol: item.protocol,
      ...(item.escPosLayoutMode ? { escPosLayoutMode: item.escPosLayoutMode } : {}),
    }
    const previewResponse = await page.request.post('/api/designer/preview', { data: payload })
    expect(previewResponse.status()).toBe(200)
    const preview = await previewResponse.json() as {
      pages: Array<{ elements: Array<{ sourcePath: string; type: number; text?: string; instanceIndices?: number[] }> }>
    }
    expect(preview.pages.length).toBeGreaterThan(1)
    if (expectedPageCount === undefined) expectedPageCount = preview.pages.length
    expect(preview.pages.length).toBe(expectedPageCount)

    for (const [pageIndex, renderedPage] of preview.pages.entries()) {
      expect(renderedPage.elements.some((element) => element.text === 'BAR-PAGE'), `${item.protocol} page ${pageIndex + 1} header`).toBe(true)
      expect(renderedPage.elements.some((element) => element.text === 'BAR-HEAD'), `${item.protocol} page ${pageIndex + 1} table header`).toBe(true)
      expect(renderedPage.elements.some((element) => element.text === 'BAR-FOOT'), `${item.protocol} page ${pageIndex + 1} footer`).toBe(true)
    }

    const codePath = 'Rows[2].Cells[0].Elements[0]'
    const codeElements = preview.pages.flatMap((renderedPage) => renderedPage.elements)
      .filter((element) => element.sourcePath === codePath)
      .sort((left, right) => (left.instanceIndices?.[0] ?? -1) - (right.instanceIndices?.[0] ?? -1))
    expect(codeElements).toHaveLength(data.Items.length)
    expect(codeElements.every((element) => element.type === 4)).toBe(true)
    expect(codeElements.map((element) => element.instanceIndices?.[0])).toEqual(
      data.Items.map((_, itemIndex) => itemIndex))

    const qrPath = 'Rows[2].Cells[0].Elements[1]'
    const qrElements = preview.pages.flatMap((renderedPage) => renderedPage.elements)
      .filter((element) => element.sourcePath === qrPath)
      .sort((left, right) => (left.instanceIndices?.[0] ?? -1) - (right.instanceIndices?.[0] ?? -1))
    expect(qrElements).toHaveLength(data.Items.length)
    expect(qrElements.every((element) => element.type === 3)).toBe(true)
    expect(qrElements.map((element) => element.instanceIndices?.[0])).toEqual(
      data.Items.map((_, itemIndex) => itemIndex))

    const downloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: item.button }).click()
    const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
    const pageNames = Object.keys(archive.files)
      .filter((name) => new RegExp(`^page-\\d{3}\\.${item.extension}$`).test(name))
      .sort()
    expect(pageNames).toEqual(Array.from({ length: expectedPageCount }, (_, pageIndex) =>
      `page-${String(pageIndex + 1).padStart(3, '0')}.${item.extension}`))

    for (const pageName of pageNames) {
      const entry = archive.file(pageName)
      expect(entry).not.toBeNull()
      if (item.kind === 'text') {
        const output = await entry!.async('string')
        for (const marker of item.markers) expect(output).toContain(marker)
      } else {
        const bytes = await entry!.async('uint8array')
        for (const marker of item.markers) {
          expect(Array.from(bytes).some((_, offset) => marker.every((value, byteIndex) =>
            bytes[offset + byteIndex] === value))).toBe(true)
        }
      }
    }
  }
})

test('同一图片模板可在 Designer 导出 CPCL、TSPL、ZPL 和 ESC/POS Raster', async ({ page }) => {
  test.setTimeout(90_000)
  const template = {
    Version: 1,
    Width: 240,
    Height: 120,
    Rows: [{ Height: 120, Cells: [{ Width: 240, Elements: [{
      Type: 2,
      Width: 80,
      Height: 40,
      Field: { Value: 'logo' },
      StartCoordinate: { X: 16, Y: 20 },
    }] }] }],
  }
  const dataUrl = await page.evaluate(() => {
    const canvas = document.createElement('canvas')
    canvas.width = 2
    canvas.height = 1
    const context = canvas.getContext('2d')
    if (!context) throw new Error('浏览器不支持 Canvas 2D。')
    context.fillStyle = '#000'
    context.fillRect(0, 0, 1, 1)
    context.fillStyle = '#fff'
    context.fillRect(1, 0, 1, 1)
    return canvas.toDataURL('image/png')
  })
  const encoded = dataUrl.split(',')[1]
  if (!encoded) throw new Error('Canvas PNG 数据为空。')

  const bundle = new JSZip()
  bundle.file('template.json', JSON.stringify(template))
  bundle.file('resources/001-logo.png', Buffer.from(encoded, 'base64'))
  bundle.file('manifest.json', JSON.stringify({
    format: 'Bing.EasyPrint.TemplateBundle',
    version: 1,
    resources: [{ key: 'logo', path: 'resources/001-logo.png', fileName: 'logo.png', mimeType: 'image/png' }],
  }))
  const bundleBytes = await bundle.generateAsync({ type: 'nodebuffer', compression: 'DEFLATE' })

  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const importPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByTestId('template-import').setInputFiles({
    name: 'cross-protocol-image.zip', mimeType: 'application/zip', buffer: bundleBytes,
  })
  await importPreview
  await expect(page.locator('.toast-message')).toContainText('已导入模板包 · 1 个资源')
  await expect(page.locator('.tree-row.tree-element .tree-select')).toHaveCount(1)

  const protocol = page.getByRole('combobox', { name: '输出协议' })
  const cases = [
    { value: 'Cpcl', button: '生成 CPCL', file: 'page-001.cpcl', kind: 'text', marker: 'EG ' },
    { value: 'Tspl', button: '生成 TSPL', file: 'page-001.tspl', kind: 'text', marker: 'BITMAP ' },
    { value: 'Zpl', button: '生成 ZPL', file: 'page-001.zpl', kind: 'text', marker: '^GFA' },
    { value: 'EscPosRaster', button: '生成 ESC/POS', file: 'page-001.escpos', kind: 'bytes', marker: [0x1D, 0x76, 0x30] },
  ] as const

  for (const [index, item] of cases.entries()) {
    if (index > 0) {
      const previewResponse = page.waitForResponse((response) =>
        response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
      await protocol.selectOption(item.value)
      await previewResponse
    }
    await expect(protocol).toHaveValue(item.value)
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

    const downloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: item.button }).click()
    const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
    const entry = archive.file(item.file)
    expect(entry).not.toBeNull()
    if (item.kind === 'text') {
      const output = await entry!.async('string')
      expect(output).toContain(item.marker)
    } else {
      const bytes = await entry!.async('uint8array')
      expect(Array.from(bytes).some((_, offset) => item.marker.every((value, byteIndex) => bytes[offset + byteIndex] === value))).toBe(true)
    }
  }
})

test('同一跨页点阵码模板可在 Designer 保留页眉表头页脚和四协议点阵轨迹', async ({ page }) => {
  test.setTimeout(180_000)
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

  const matrices = [
    { format: 'DataMatrix', width: 160, height: 160, valuePrefix: 'DM' },
    { format: 'PDF417', width: 320, height: 160, valuePrefix: 'PDF' },
  ] as const
  const cases = [
    { value: 'Cpcl', protocol: 'Cpcl', button: '生成 CPCL', extension: 'cpcl', kind: 'text', marker: 'EG ' },
    { value: 'Tspl', protocol: 'Tspl', button: '生成 TSPL', extension: 'tspl', kind: 'text', marker: 'BITMAP ' },
    { value: 'Zpl', protocol: 'Zpl', button: '生成 ZPL', extension: 'zpl', kind: 'text', marker: '^GFA' },
    { value: 'EscPosRaster', protocol: 'EscPos', escPosLayoutMode: 'Raster', button: '生成 ESC/POS', extension: 'escpos', kind: 'bytes', marker: [0x1D, 0x76, 0x30] },
  ] as const

  for (const matrix of matrices) {
    const template = {
      Version: 2,
      Width: 400,
      Height: 320,
      Rows: [
        { Section: 'PageHeader', Height: 24, Cells: [{ Width: 400, Elements: [
          { Type: 1, FontSize: 24, Field: { Value: 'MATRIX-PAGE' }, StartCoordinate: { X: 8, Y: 0 } },
        ] }] },
        { Section: 'TableHeader', Height: 24, Cells: [{ Width: 400, Elements: [
          { Type: 1, FontSize: 24, Field: { Value: 'MATRIX-HEAD' }, StartCoordinate: { X: 8, Y: 0 } },
        ] }] },
        { RepeatSource: 'Items', Height: 180, Cells: [{ Width: 400, Elements: [
          { Type: 10, Format: matrix.format, Width: matrix.width, Height: matrix.height,
            Field: { Name: 'Code' }, StartCoordinate: { X: 8, Y: 8 } },
        ] }] },
        { Section: 'PageFooter', Height: 24, Cells: [{ Width: 400, Elements: [
          { Type: 1, FontSize: 24, Field: { Value: 'MATRIX-FOOT' }, StartCoordinate: { X: 8, Y: 0 } },
        ] }] },
      ],
    }
    const data = {
      Items: Array.from({ length: 3 }, (_, index) => ({ Code: `${matrix.valuePrefix}-${String(index + 1).padStart(2, '0')}` })),
    }
    await reloadTemplateJson(page, Buffer.from(JSON.stringify(template)), `${matrix.format.toLowerCase()}-cross-page.json`)
    await setDesignerData(page, data)

    const protocol = page.getByRole('combobox', { name: '输出协议' })
    for (const [index, item] of cases.entries()) {
      if (index > 0) {
        const previewWait = page.waitForResponse((response) =>
          response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
        await protocol.selectOption(item.value)
        await previewWait
      }
      await expect(protocol).toHaveValue(item.value)
      const payload = {
        template,
        data,
        encoding: 'Ascii',
        protocol: item.protocol,
        ...(item.escPosLayoutMode ? { escPosLayoutMode: item.escPosLayoutMode } : {}),
      }
      const previewResponse = await page.request.post('/api/designer/preview', { data: payload })
      expect(previewResponse.status()).toBe(200)
      const preview = await previewResponse.json() as {
        pages: Array<{ elements: Array<{ sourcePath: string; type: number; text?: string; instanceIndices?: number[] }> }>
      }
      expect(preview.pages.length).toBeGreaterThan(1)
      for (const [pageIndex, renderedPage] of preview.pages.entries()) {
        expect(renderedPage.elements.some((element) => element.text === 'MATRIX-PAGE'), `${matrix.format}/${item.protocol} page ${pageIndex + 1} header`).toBe(true)
        expect(renderedPage.elements.some((element) => element.text === 'MATRIX-HEAD'), `${matrix.format}/${item.protocol} page ${pageIndex + 1} table header`).toBe(true)
        expect(renderedPage.elements.some((element) => element.text === 'MATRIX-FOOT'), `${matrix.format}/${item.protocol} page ${pageIndex + 1} footer`).toBe(true)
      }

      const matrixPath = 'Rows[2].Cells[0].Elements[0]'
      const elements = preview.pages.flatMap((renderedPage) => renderedPage.elements)
        .filter((element) => element.sourcePath === matrixPath)
        .sort((left, right) => (left.instanceIndices?.[0] ?? -1) - (right.instanceIndices?.[0] ?? -1))
      expect(elements).toHaveLength(data.Items.length)
      expect(elements.every((element) => element.type === 10)).toBe(true)
      expect(elements.map((element) => element.instanceIndices?.[0])).toEqual(data.Items.map((_, itemIndex) => itemIndex))

      const downloadPromise = page.waitForEvent('download')
      await page.getByRole('button', { name: item.button }).click()
      const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
      const pageNames = Object.keys(archive.files)
        .filter((name) => new RegExp(`^page-\\d{3}\\.${item.extension}$`).test(name))
        .sort()
      expect(pageNames).toEqual(Array.from({ length: preview.pages.length }, (_, pageIndex) =>
        `page-${String(pageIndex + 1).padStart(3, '0')}.${item.extension}`))
      for (const pageName of pageNames) {
        const entry = archive.file(pageName)
        expect(entry).not.toBeNull()
        if (item.kind === 'text') {
          expect(await entry!.async('string')).toContain(item.marker as string)
        } else {
          const bytes = await entry!.async('uint8array')
          expect(Array.from(bytes).some((_, offset) => item.marker.every((value, byteIndex) =>
            bytes[offset + byteIndex] === value))).toBe(true)
        }
      }
    }
  }
})

test('同一点阵模板可在 Designer 导出 CPCL、TSPL、ZPL 和 ESC/POS Raster', async ({ page }) => {
  test.setTimeout(120_000)
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

  const templates = [
    { format: 'DataMatrix', value: 'DM-CROSS-PROTOCOL', width: 160, height: 160 },
    { format: 'PDF417', value: 'PDF417-CROSS-PROTOCOL', width: 320, height: 160 },
  ] as const
  const cases = [
    { value: 'Cpcl', button: '生成 CPCL', file: 'page-001.cpcl', kind: 'text', marker: 'EG ' },
    { value: 'Tspl', button: '生成 TSPL', file: 'page-001.tspl', kind: 'text', marker: 'BITMAP ' },
    { value: 'Zpl', button: '生成 ZPL', file: 'page-001.zpl', kind: 'text', marker: '^GFA' },
    { value: 'EscPosRaster', button: '生成 ESC/POS', file: 'page-001.escpos', kind: 'bytes', marker: [0x1D, 0x76, 0x30] },
  ] as const

  for (const matrix of templates) {
    const template = {
      Version: 2,
      Width: 400,
      Height: 180,
      Rows: [{ Height: 180, Cells: [{ Width: 400, Elements: [{
        Type: 10,
        Format: matrix.format,
        Width: matrix.width,
        Height: matrix.height,
        Field: { Value: matrix.value },
        StartCoordinate: { X: 16, Y: 8 },
      }] }] }],
    }
    await reloadTemplateJson(page, Buffer.from(JSON.stringify(template)), `${matrix.format.toLowerCase()}-cross-protocol.json`)
    const protocol = page.getByRole('combobox', { name: '输出协议' })

    for (const [index, item] of cases.entries()) {
      if (index > 0) {
        const previewResponse = page.waitForResponse((response) =>
          response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
        await protocol.selectOption(item.value)
        await previewResponse
      }
      await expect(protocol).toHaveValue(item.value)
      await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

      const downloadPromise = page.waitForEvent('download')
      await page.getByRole('button', { name: item.button }).click()
      const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
      const entry = archive.file(item.file)
      expect(entry).not.toBeNull()
      if (item.kind === 'text') {
        const output = await entry!.async('string')
        expect(output).toContain(item.marker)
      } else {
        const bytes = await entry!.async('uint8array')
        expect(Array.from(bytes).some((_, offset) => item.marker.every((value, byteIndex) => bytes[offset + byteIndex] === value))).toBe(true)
      }
    }
  }
})

test('ESC/POS 请求省略布局模式时默认使用严格 Native', async ({ page }) => {
  await page.goto('/')
  const template = {
    Version: 1,
    Width: 240,
    Height: 120,
    Rows: [{ Cells: [{ Elements: [{
      Type: 5,
      LineWidth: 2,
      StartCoordinate: { X: 8, Y: 8 },
      LineEndCoordinate: { X: 120, Y: 8 },
    }] }] }],
  }

  const nativeResponse = await page.request.post('/api/designer/preview', {
    data: { template, data: {}, protocol: 'EscPos' },
  })
  expect(nativeResponse.status()).toBe(400)
  const nativeError = await nativeResponse.json()
  expect(nativeError.errors[0].sourcePath).toBe('Rows[0].Cells[0].Elements[0]')
  expect(nativeError.errors[0].message).toContain('Native')

  const rasterResponse = await page.request.post('/api/designer/preview', {
    data: { template, data: {}, protocol: 'EscPos', escPosLayoutMode: 'Raster' },
  })
  expect(rasterResponse.ok()).toBe(true)
})

test('TSPL 和 ZPL 斜线仅在请求显式允许时光栅化', async ({ page }) => {
  await page.goto('/')
  const template = {
    Version: 1,
    Width: 240,
    Height: 120,
    Rows: [{ Cells: [{ Elements: [{
      Type: 5,
      LineWidth: 2,
      StartCoordinate: { X: 8, Y: 8 },
      LineEndCoordinate: { X: 120, Y: 80 },
    }] }] }],
  }

  for (const protocol of ['Tspl', 'Zpl']) {
    const strictResponse = await page.request.post('/api/designer/preview', {
      data: { template, data: {}, protocol },
    })
    expect(strictResponse.status()).toBe(400)

    const rasterizedResponse = await page.request.post('/api/designer/preview', {
      data: { template, data: {}, protocol, allowRasterizedLines: true },
    })
    expect(rasterizedResponse.ok()).toBe(true)
  }
})

test('位图斜线复选框同时传递给预览和导出请求', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  const previewRequest = page.waitForRequest((request) => request.url().includes('/api/designer/preview') &&
    request.postDataJSON()?.allowRasterizedLines === true)
  await page.getByRole('checkbox', { name: '允许斜线转换为位图' }).check()
  expect((await previewRequest).postDataJSON().allowRasterizedLines).toBe(true)

  const renderRequest = page.waitForRequest((request) => request.url().includes('/api/designer/render'))
  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 TSPL' }).click()
  expect((await renderRequest).postDataJSON().allowRasterizedLines).toBe(true)
  await download
})

test('TSPL 输出校准面板将半角和全角字宽传递给预览请求', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  await page.getByRole('button', { name: '输出校准设置' }).click()

  const previewRequest = page.waitForRequest((request) => {
    if (!request.url().includes('/api/designer/preview')) return false
    const body = request.postDataJSON() as Record<string, unknown> | null
    return body?.protocol === 'Tspl' && body?.deviceHalfCellWidthAt24 === 10.5 && body?.deviceFullCellWidthAt24 === 21
  })
  await page.getByLabel('半角字宽', { exact: true }).fill('10.5')
  await page.getByLabel('全角字宽', { exact: true }).fill('21')
  const request = await previewRequest
  expect(request.postDataJSON()).toMatchObject({
    protocol: 'Tspl',
    deviceHalfCellWidthAt24: 10.5,
    deviceFullCellWidthAt24: 21,
  })
})

test('Host 预览使用逐字体校准计算 TSPL 文字占位', async ({ page }) => {
  await page.goto('/')
  const template = {
    Version: 1,
    Width: 200,
    Height: 100,
    Rows: [{ Height: 80, Cells: [{ Width: 200, Elements: [{
      Type: 1,
      FontSize: 24,
      DeviceFontName: '1',
      Field: { Value: 'AB' },
      StartCoordinate: { X: 4, Y: 4 },
    }] }] }],
  }
  const response = await page.request.post('/api/designer/preview', {
    data: {
      template,
      data: {},
      protocol: 'Tspl',
      deviceFontCalibrations: {
        '1': { halfCellWidthAt24: 16, fullCellWidthAt24: 32, lineHeightAt24: 48 },
      },
    },
  })
  expect(response.ok()).toBe(true)
  const body = await response.json()
  const element = body.pages[0].elements.find((item: { type: number }) => item.type === 1)
  expect(element).toMatchObject({ text: 'AB', width: 32, height: 48 })
})

test('ESC/POS Raster 输出校准面板将图片黑白阈值传递给预览请求', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await page.getByRole('button', { name: '输出校准设置' }).click()

  const previewRequest = page.waitForRequest((request) => {
    if (!request.url().includes('/api/designer/preview')) return false
    const body = request.postDataJSON() as Record<string, unknown> | null
    return body?.protocol === 'EscPos' && body?.escPosLayoutMode === 'Raster' && body?.imageThreshold === 200
  })
  await page.getByLabel('图片黑白阈值').fill('200')
  const request = await previewRequest
  expect(request.postDataJSON()).toMatchObject({
    protocol: 'EscPos',
    escPosLayoutMode: 'Raster',
    imageThreshold: 200,
  })
})

test('页面设置按协议显示并严格保留 TSPL 裁切请求字段', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('button', { name: '输出校准设置' }).click()

  await expect(page.getByLabel('打印份数')).toBeVisible()
  await expect(page.getByLabel('额外进纸行数')).toHaveCount(0)
  await expect(page.getByLabel('打印后切纸')).toHaveCount(0)

  const copiesRequest = page.waitForRequest((request) => {
    if (!request.url().includes('/api/designer/preview')) return false
    const body = request.postDataJSON() as Record<string, unknown> | null
    return body?.protocol === 'Cpcl' && body?.copies === 2
  })
  await page.getByLabel('打印份数').fill('2')
  await copiesRequest

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await expect(page.getByLabel('额外进纸行数')).toBeVisible()
  await expect(page.getByLabel('打印后切纸')).toBeVisible()

  const feedRequest = page.waitForRequest((request) => {
    if (!request.url().includes('/api/designer/preview')) return false
    const body = request.postDataJSON() as Record<string, unknown> | null
    return body?.protocol === 'EscPos' && body?.escPosLayoutMode === 'Raster' && body?.copies === 2 && body?.feedLines === 4
  })
  await page.getByLabel('额外进纸行数').fill('4')
  await feedRequest

  const cutRequest = page.waitForRequest((request) => {
    if (!request.url().includes('/api/designer/preview')) return false
    const body = request.postDataJSON() as Record<string, unknown> | null
    return body?.protocol === 'EscPos' && body?.cutAfterPrint === true
  })
  await page.getByLabel('打印后切纸').selectOption('true')
  await cutRequest

  const tsplRejectedResponse = page.waitForResponse((response) => {
    if (!response.url().includes('/api/designer/preview') || response.request().method() !== 'POST') return false
    const body = response.request().postDataJSON() as Record<string, unknown> | null
    return body?.protocol === 'Tspl' && !('feedLines' in (body ?? {})) && body?.cutAfterPrint === true
  })
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  expect((await tsplRejectedResponse).status()).toBe(400)
  await expect(page.getByLabel('额外进纸行数')).toHaveCount(0)
  await expect(page.getByLabel('打印后切纸')).toBeVisible()

  const tsplWithoutCutResponse = page.waitForResponse((response) => {
    if (!response.url().includes('/api/designer/preview') || response.request().method() !== 'POST') return false
    const body = response.request().postDataJSON() as Record<string, unknown> | null
    return body?.protocol === 'Tspl' && body?.cutAfterPrint === false
  })
  await page.getByLabel('打印后切纸').selectOption('false')
  const responseWithoutCut = await tsplWithoutCutResponse
  expect(responseWithoutCut.status()).toBe(200)
  expect(responseWithoutCut.request().postDataJSON()).toMatchObject({
    protocol: 'Tspl',
    cutAfterPrint: false,
  })

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Zpl')
  await expect(page.getByLabel('打印后切纸')).toBeVisible()
  await expect(page.getByLabel('额外进纸行数')).toHaveCount(0)
})

test('页面设置改变 TSPL 份数和 ESC/POS 进纸切纸字节', async ({ page }) => {
  const template = {
    Version: 1, Width: 240, Height: 100,
    Rows: [{ Cells: [{ Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'OK' } }] }] }],
  }
  const render = async (options: Record<string, unknown>, extension: string) => {
    const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
    const response = await page.request.post(`${apiUrl}/api/designer/render`, {
      data: { template, data: {}, ...options },
    })
    expect(response.ok()).toBe(true)
    const archive = await JSZip.loadAsync(await response.body())
    return archive.file(`page-001.${extension}`)!.async('uint8array')
  }

  const tspl = new TextDecoder('ascii').decode(await render({ protocol: 'Tspl', copies: 2 }, 'tspl'))
  expect(tspl).toContain('PRINT 1,2\r\n')

  const defaultEscPos = await render({ protocol: 'EscPos', escPosLayoutMode: 'Native' }, 'escpos')
  const withoutFeedOrCut = await render({
    protocol: 'EscPos', escPosLayoutMode: 'Native', feedLines: 0, cutAfterPrint: false,
  }, 'escpos')
  expect(Array.from(defaultEscPos.slice(-6))).toEqual([0x1b, 0x64, 1, 0x1d, 0x56, 0])
  expect(withoutFeedOrCut.length).toBe(defaultEscPos.length - 6)
})

test('TSPL 和 ZPL 介质设置按协议传递并省略不适用字段', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('button', { name: '输出校准设置' }).click()

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  await expect(page.getByLabel('TSPL DPI')).toBeVisible()
  await expect(page.getByLabel('ZPL DPI')).toHaveCount(0)
  const tsplRequest = page.waitForRequest((request) => {
    if (!request.url().includes('/api/designer/preview')) return false
    const body = request.postDataJSON() as Record<string, unknown> | null
    const media = body?.tsplMedia as Record<string, unknown> | undefined
    return body?.protocol === 'Tspl' && media?.dpi === 300 && media?.gapDistance === 2 &&
      media?.gapOffset === 1 && media?.direction === 1 && media?.referenceX === 3 &&
      media?.referenceY === 4 && media?.speed === 5 && media?.density === 8 &&
      !('zplMedia' in (body ?? {}))
  })
  await page.getByLabel('TSPL DPI').fill('300')
  await page.getByLabel('TSPL 标签间隙距离').fill('2')
  await page.getByLabel('TSPL 标签间隙偏移').fill('1')
  await page.getByLabel('TSPL 打印方向').selectOption('1')
  await page.getByLabel('TSPL 参考点 X').fill('3')
  await page.getByLabel('TSPL 参考点 Y').fill('4')
  await page.getByLabel('TSPL 打印速度').fill('5')
  await page.getByLabel('TSPL 打印浓度').fill('8')
  expect((await tsplRequest).postDataJSON().tsplMedia).toEqual({
    dpi: 300, gapDistance: 2, gapOffset: 1, direction: 1,
    referenceX: 3, referenceY: 4, speed: 5, density: 8,
  })

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Zpl')
  await expect(page.getByLabel('TSPL DPI')).toHaveCount(0)
  await expect(page.getByLabel('ZPL DPI')).toBeVisible()
  const zplRequest = page.waitForRequest((request) => {
    if (!request.url().includes('/api/designer/preview')) return false
    const body = request.postDataJSON() as Record<string, unknown> | null
    const media = body?.zplMedia as Record<string, unknown> | undefined
    return body?.protocol === 'Zpl' && media?.dpi === 300 && media?.printSpeed === 6 &&
      media?.orientation === 'Right' && media?.mediaTracking === 'M' && media?.darkness === -4 &&
      !('tsplMedia' in (body ?? {}))
  })
  await page.getByLabel('ZPL DPI').fill('300')
  await page.getByLabel('ZPL 打印速度').fill('6')
  await page.getByLabel('ZPL 方向').selectOption('Right')
  await page.getByLabel('ZPL 介质跟踪').selectOption('M')
  await page.getByLabel('ZPL 打印浓度').fill('-4')
  expect((await zplRequest).postDataJSON().zplMedia).toEqual({
    dpi: 300, printSpeed: 6, orientation: 'Right', mediaTracking: 'M', darkness: -4,
  })
})

test('切换 300 DPI 后毫米显示和编辑使用目标 DPI 且保持模板点值', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('button', { name: '单行文字', exact: true }).click()
  const position = dimensions(page)
  await expect(position.nth(0)).toHaveValue('24')

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await page.getByLabel('TSPL DPI').fill('300')
  await page.locator('.unit-selector select').selectOption('mm')

  await expect(page.locator('.canvas-breadcrumb .dimension-conversion')).toHaveText('48.77 × 30.48 mm')
  await expect(page.locator('.status-right')).toContainText('300 DPI')
  await expect(position.nth(0)).toHaveValue('2.03')

  await position.nth(0).fill('5.08')
  await position.nth(0).press('Tab')
  await expect(position.nth(0)).toHaveValue('5.08')

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Cpcl')
  await expect(page.locator('.canvas-breadcrumb .dimension-conversion')).toHaveText('73.15 × 45.72 mm')
  await expect(page.locator('.status-right')).toContainText('200 DPI')
  await expect(position.nth(0)).toHaveValue('7.62')

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const saved = JSON.parse(await readFile(await (await downloadPromise).path(), 'utf8'))
  expect(saved.Rows[0].Cells[0].Elements[0].StartCoordinate.X).toBe(60)
})

test('TSPL 严格设备边界设置传递文字行高和开关', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')

  await expect(page.getByLabel('设备行高', { exact: true })).toBeVisible()
  await expect(page.getByLabel('严格设备边界')).not.toBeChecked()
  const request = page.waitForRequest((candidate) => {
    if (!candidate.url().includes('/api/designer/preview')) return false
    const body = candidate.postDataJSON() as Record<string, unknown> | null
    return body?.protocol === 'Tspl' && body?.deviceHalfCellWidthAt24 === 12 &&
      body?.deviceFullCellWidthAt24 === 24 && body?.deviceLineHeightAt24 === 28 &&
      body?.requireDeclaredNativeBounds === true
  })
  await page.getByLabel('半角字宽', { exact: true }).fill('12')
  await page.getByLabel('全角字宽', { exact: true }).fill('24')
  await page.getByLabel('设备行高', { exact: true }).fill('28')
  await page.getByLabel('严格设备边界').check()
  await request
})

test('TSPL 和 ZPL 介质设置写入最终协议字节', async ({ page }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const template = {
    Version: 1, Width: 240, Height: 100,
    Rows: [{ Cells: [{ Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'MEDIA' } }] }] }],
  }
  const render = async (options: Record<string, unknown>, extension: string) => {
    const response = await page.request.post(`${apiUrl}/api/designer/render`, {
      data: { template, data: {}, ...options },
    })
    expect(response.ok()).toBe(true)
    const archive = await JSZip.loadAsync(await response.body())
    const file = archive.file(`page-001.${extension}`)
    expect(file).not.toBeNull()
    return file!.async('string')
  }

  const tspl = await render({
    protocol: 'Tspl',
    tsplMedia: { dpi: 300, gapDistance: 2, gapOffset: 1, direction: 1,
      referenceX: 3, referenceY: 4, speed: 5, density: 8 },
  }, 'tspl')
  expect(tspl).toContain('GAP 2,1\r\n')
  expect(tspl).toContain('DIRECTION 1\r\n')
  expect(tspl).toContain('REFERENCE 3,4\r\n')
  expect(tspl).toContain('SPEED 5\r\n')
  expect(tspl).toContain('DENSITY 8\r\n')

  const zpl = await render({
    protocol: 'Zpl',
    zplMedia: { dpi: 300, printSpeed: 6, orientation: 'Right', mediaTracking: 'M', darkness: -4 },
  }, 'zpl')
  expect(zpl).toContain('^PR6\r\n')
  expect(zpl).toContain('^POR\r\n')
  expect(zpl).toContain('^MNM\r\n')
  expect(zpl).toContain('^MD-4\r\n')
})

test('TSPL 和 ZPL 文字样式位图开关恢复预览并写入位图字节', async ({ page }) => {
  const template = {
    Version: 1, Width: 240, Height: 100,
    Rows: [{ Cells: [{ Elements: [{
      Type: 1, FontSize: 24, FontStyle: 5, Rotation: 90, Field: { Value: 'STYLE' },
    }] }] }],
  }
  await openBlankTemplate(page)
  await reloadTemplateJson(page, Buffer.from(JSON.stringify(template)), 'rasterized-text-styles.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  await expect(page.getByLabel('允许 TSPL 文字样式位图')).not.toBeChecked()
  await expect(page.locator('.diagnostic-panel')).toContainText('Tspl')

  const tsplPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST' && response.status() === 200)
  const tsplRequest = page.waitForRequest((request) => {
    if (!request.url().includes('/api/designer/preview')) return false
    const body = request.postDataJSON() as Record<string, unknown> | null
    const media = body?.tsplMedia as Record<string, unknown> | undefined
    return body?.protocol === 'Tspl' && media?.allowRasterizedTextStyles === true
  })
  await page.getByLabel('允许 TSPL 文字样式位图').check()
  await tsplRequest
  await tsplPreview

  const tsplDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 TSPL' }).click()
  const tsplArchive = await JSZip.loadAsync(await readFile(await (await tsplDownload).path()))
  const tsplBytes = await tsplArchive.file('page-001.tspl')!.async('string')
  expect(tsplBytes).toContain('BITMAP ')

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Zpl')
  await expect(page.getByLabel('允许 ZPL 文字样式位图')).not.toBeChecked()
  await expect(page.locator('.diagnostic-panel')).toContainText('Zpl')

  const zplPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST' && response.status() === 200)
  const zplRequest = page.waitForRequest((request) => {
    if (!request.url().includes('/api/designer/preview')) return false
    const body = request.postDataJSON() as Record<string, unknown> | null
    const media = body?.zplMedia as Record<string, unknown> | undefined
    return body?.protocol === 'Zpl' && media?.allowRasterizedTextStyles === true
  })
  await page.getByLabel('允许 ZPL 文字样式位图').check()
  await zplRequest
  await zplPreview

  const zplDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 ZPL' }).click()
  const zplArchive = await JSZip.loadAsync(await readFile(await (await zplDownload).path()))
  const zplBytes = await zplArchive.file('page-001.zpl')!.async('string')
  expect(zplBytes).toContain('^GFA,')
})

test('输出编码和 ESC/POS 设备设置按协议同步到预览与导出请求', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await expect(page.getByLabel('输出编码')).toBeVisible()

  const encodingRequest = page.waitForRequest((request) => {
    if (!request.url().includes('/api/designer/preview')) return false
    const body = request.postDataJSON() as Record<string, unknown> | null
    return body?.protocol === 'Cpcl' && body?.encoding === 'Gbk'
  })
  await page.getByLabel('输出编码').selectOption('Gbk')
  await encodingRequest

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosNative')
  await expect(page.getByLabel('ESC/POS 设备 DPI')).toBeVisible()
  await expect(page.getByLabel('ESC/POS 原生 Tab 宽度')).toBeVisible()
  const nativeRequest = page.waitForRequest((request) => {
    if (!request.url().includes('/api/designer/preview')) return false
    const body = request.postDataJSON() as Record<string, unknown> | null
    const device = body?.escPosDevice as Record<string, unknown> | undefined
    return body?.protocol === 'EscPos' && body?.escPosLayoutMode === 'Native' && body?.encoding === 'Gbk' &&
      device?.dpi === 300 && device?.codePageIndex === 17 && device?.printAreaWidthUnits === 576 &&
      device?.nativeTabWidthDots === 32
  })
  await page.getByLabel('ESC/POS 设备 DPI').fill('300')
  await page.getByLabel('ESC/POS 代码页索引').fill('17')
  await page.getByLabel('ESC/POS 打印区域宽度').fill('576')
  await page.getByLabel('ESC/POS 原生 Tab 宽度').fill('32')
  const native = await nativeRequest
  expect(native.postDataJSON()).toMatchObject({
    protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Gbk',
    escPosDevice: { dpi: 300, codePageIndex: 17, printAreaWidthUnits: 576, nativeTabWidthDots: 32 },
  })

  const renderRequest = page.waitForRequest((request) => {
    if (!request.url().includes('/api/designer/render')) return false
    const body = request.postDataJSON() as Record<string, unknown> | null
    return body?.protocol === 'EscPos' && body?.escPosLayoutMode === 'Native' && body?.encoding === 'Gbk' &&
      (body?.escPosDevice as Record<string, unknown> | undefined)?.nativeTabWidthDots === 32
  })
  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 ESC/POS' }).click()
  expect((await renderRequest).postDataJSON()).toMatchObject({
    encoding: 'Gbk', escPosDevice: { dpi: 300, codePageIndex: 17, printAreaWidthUnits: 576, nativeTabWidthDots: 32 },
  })
  await download

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await expect(page.getByLabel('ESC/POS 原生 Tab 宽度')).toHaveCount(0)
  const rasterRequest = page.waitForRequest((request) => {
    if (!request.url().includes('/api/designer/preview')) return false
    const body = request.postDataJSON() as Record<string, unknown> | null
    const device = body?.escPosDevice as Record<string, unknown> | undefined
    return body?.protocol === 'EscPos' && body?.escPosLayoutMode === 'Raster' &&
      device?.dpi === 300 && device?.codePageIndex === 17 && device?.printAreaWidthUnits === 576 &&
      !('nativeTabWidthDots' in (device ?? {}))
  })
  await page.getByLabel('输出编码').selectOption('Utf8')
  const raster = await rasterRequest
  expect(raster.postDataJSON()).toMatchObject({
    protocol: 'EscPos', escPosLayoutMode: 'Raster', encoding: 'Utf8',
    escPosDevice: { dpi: 300, codePageIndex: 17, printAreaWidthUnits: 576 },
  })

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Cpcl')
  await page.getByLabel('输出编码').selectOption('Gbk')
  const zplRequest = page.waitForRequest((request) => {
    if (!request.url().includes('/api/designer/preview')) return false
    const body = request.postDataJSON() as Record<string, unknown> | null
    return body?.protocol === 'Zpl' && !('encoding' in (body ?? {})) && !('escPosDevice' in (body ?? {}))
  })
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Zpl')
  await expect(page.getByLabel('输出编码').locator('option[value="Gbk"]')).toHaveCount(0)
  await expect(page.getByLabel('ESC/POS 设备 DPI')).toHaveCount(0)
  await zplRequest
})

test('ESC/POS 设备编码和打印区域设置写入 ESC t 与 GS W 字节', async ({ page }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const template = {
    Version: 1, Width: 240, Height: 100,
    Rows: [{ Cells: [{ Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'DEVICE' } }] }] }],
  }
  const response = await page.request.post(`${apiUrl}/api/designer/render`, {
    data: {
      template, data: {}, protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Ascii',
      escPosDevice: { dpi: 300, codePageIndex: 17, printAreaWidthUnits: 576 },
    },
  })
  expect(response.ok()).toBe(true)
  const archive = await JSZip.loadAsync(await response.body())
  const bytes = await archive.file('page-001.escpos')!.async('uint8array')
  expect(Array.from(bytes.slice(0, 9))).toEqual([0x1B, 0x40, 0x1B, 0x74, 17, 0x1D, 0x57, 0x40, 0x02])
})

test('ESC/POS 机型配置传递 ESC * 并严格拒绝不支持的切纸', async ({ page }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const template = {
    Version: 1, Width: 240, Height: 100,
    Rows: [{ Cells: [{ Elements: [{
      Type: 2, Width: 32, Height: 32, Field: { Value: 'logo' },
      StartCoordinate: { X: 8, Y: 8 },
    }] }] }],
  }
  const image = await readFile('../../tests/Bing.EasyPrint.Tests/Biz/Images/logo.jpg')
  const escPosDevice = {
    profile: {
      name: '8-dot impact', imageCommand: 'BitImageEscStar', bitImageMode: 'DoubleDensity8',
      horizontalDpi: 180, verticalDpi: 60, motionUnitsPerInch: 144, printableWidthDots: 576,
      supportsCut: false, supportsNativeBarcode: false, supportsNativeQrCode: false,
      supportsPrintAreaWidth: false, supportsTextSize: false, unidirectionalBitImage: true,
    },
  }
  const payload = {
    template, data: {}, protocol: 'EscPos', escPosLayoutMode: 'Raster', encoding: 'Ascii', cutAfterPrint: false,
    resources: { logo: image.toString('base64') }, escPosDevice,
  }
  const response = await page.request.post(`${apiUrl}/api/designer/render`, { data: payload })
  expect(response.ok()).toBe(true)
  const archive = await JSZip.loadAsync(await response.body())
  const bytes = await archive.file('page-001.escpos')!.async('uint8array')
  expect(bytes.some((value, index) => value === 0x2A && bytes[index - 1] === 0x1B)).toBe(true)

  const cutResponse = await page.request.post(`${apiUrl}/api/designer/preview`, {
    data: { ...payload, cutAfterPrint: true },
  })
  expect(cutResponse.status()).toBe(400)
  const error = await cutResponse.json()
  expect(error.errors[0].message).toMatch(/切纸|ESC\/POS/)

  const unknownProfileFieldResponse = await page.request.post(`${apiUrl}/api/designer/preview`, {
    data: {
      ...payload,
      escPosDevice: { profile: { ...escPosDevice.profile, typoImageCommand: true } },
    },
  })
  expect(unknownProfileFieldResponse.status()).toBe(400)

  const numericImageCommandResponse = await page.request.post(`${apiUrl}/api/designer/preview`, {
    data: {
      ...payload,
      escPosDevice: { profile: { ...escPosDevice.profile, imageCommand: 0 } },
    },
  })
  expect(numericImageCommandResponse.status()).toBe(400)
})

test('ESC/POS Native 文字契约校验能力、中文字节、缺字路径与 Raster 例外', async ({ request }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const nativePayload = {
    template: s1TextTemplate,
    data: { Title: '中文ABC' },
    protocol: 'EscPos',
    escPosLayoutMode: 'Native',
    encoding: 'Gbk',
    feedLines: 0,
    cutAfterPrint: false,
    escPosDevice: { dpi: 203, codePageIndex: 15, profile: s1EscPosProfile },
  }
  const capabilityResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: {
      protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Gbk',
      escPosDevice: nativePayload.escPosDevice,
    },
  })
  expect(capabilityResponse.status()).toBe(200)
  const capabilities = await capabilityResponse.json()
  expect(capabilities.nativeText).toEqual(s1GbkTextProfile)

  const { text: _text, ...legacyProfile } = s1EscPosProfile
  const legacyCapabilitiesResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: {
      protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Gbk',
      escPosDevice: { profile: legacyProfile },
    },
  })
  expect(legacyCapabilitiesResponse.status()).toBe(200)
  expect((await legacyCapabilitiesResponse.json()).nativeText).toBeNull()

  const renderResponse = await request.post(`${apiUrl}/api/designer/render`, { data: nativePayload })
  expect(renderResponse.status()).toBe(200)
  const archive = await JSZip.loadAsync(await renderResponse.body())
  const page = archive.file('page-001.escpos')
  expect(page).not.toBeNull()
  const bytes = await page!.async('uint8array')
  expect(Array.from(bytes.slice(0, 7))).toEqual([0x1B, 0x40, 0x1B, 0x74, 15, 0x1C, 0x26])
  expect(Array.from(bytes.slice(7, 15))).toEqual([0xD6, 0xD0, 0xCE, 0xC4, 0x41, 0x42, 0x43, 0x0A])

  const coverageProfile = {
    ...s1EscPosProfile,
    text: { ...s1GbkTextProfile, characterRanges: [{ first: 0, last: 127 }] },
  }
  const coverageResponse = await request.post(`${apiUrl}/api/designer/preview`, {
    data: { ...nativePayload, escPosDevice: { ...nativePayload.escPosDevice, profile: coverageProfile } },
  })
  expect(coverageResponse.status()).toBe(400)
  const coverageError = await coverageResponse.json()
  expect(coverageError.errors[0]).toMatchObject({ sourcePath: 'Rows[0].Cells[0].Elements[0]' })
  expect(JSON.stringify(coverageError.errors[0])).toMatch(/字库|覆盖|U\+/)

  const mismatchResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: {
      protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Utf8',
      escPosDevice: nativePayload.escPosDevice,
    },
  })
  expect(mismatchResponse.status()).toBe(400)
  expect(JSON.stringify(await mismatchResponse.json())).toMatch(/代码页|编码/)

  const rasterCapabilitiesResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: {
      protocol: 'EscPos', escPosLayoutMode: 'Raster', encoding: 'Utf8',
      escPosDevice: { profile: coverageProfile },
    },
  })
  expect(rasterCapabilitiesResponse.status()).toBe(200)
  expect((await rasterCapabilitiesResponse.json()).nativeText).toBeNull()
  const rasterResponse = await request.post(`${apiUrl}/api/designer/preview`, {
    data: {
      ...nativePayload,
      escPosLayoutMode: 'Raster',
      encoding: 'Utf8',
      escPosDevice: { profile: coverageProfile },
    },
  })
  expect(rasterResponse.status()).toBe(200)

  const device437Profile = {
    ...s1EscPosProfile,
    text: {
      encodingCodePage: 437,
      codePageIndex: 15,
      characterMode: 'SingleByte',
      characterRanges: [{ first: 0, last: 127 }],
      halfCellWidthDots: 12,
      fullCellWidthDots: 24,
      lineHeightDots: 24,
    },
  }
  const deviceCapabilityResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: {
      protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Device',
      escPosDevice: { codePageIndex: 15, profile: device437Profile },
    },
  })
  expect(deviceCapabilityResponse.status()).toBe(200)
  expect((await deviceCapabilityResponse.json()).nativeText).toMatchObject({
    encodingCodePage: 437, codePageIndex: 15, characterMode: 'SingleByte',
  })
  const deviceRenderResponse = await request.post(`${apiUrl}/api/designer/render`, {
    data: {
      ...nativePayload,
      data: { Title: 'ABC' },
      encoding: 'Device',
      escPosDevice: { codePageIndex: 15, profile: device437Profile },
    },
  })
  expect(deviceRenderResponse.status()).toBe(200)
  const deviceArchive = await JSZip.loadAsync(await deviceRenderResponse.body())
  const deviceBytes = await deviceArchive.file('page-001.escpos')!.async('uint8array')
  expect(Array.from(deviceBytes.slice(0, 7))).toEqual([0x1B, 0x40, 0x1B, 0x74, 15, 0x1C, 0x2E])
  expect(Array.from(deviceBytes.slice(7, 11))).toEqual([0x41, 0x42, 0x43, 0x0A])
})

test('ESC/POS Native 文字契约项目导入显示并保存重载后字节一致', async ({ page }) => {
  const project = {
    format: 'Bing.EasyPrint.DesignerProject',
    version: 1,
    template: s1TextTemplate,
    outputSettings: {
      outputTarget: 'EscPosNative',
      allowRasterizedLines: false,
      imageThreshold: 128,
      encoding: 'Gbk',
      deviceFontCalibrations: { Tspl: {}, Zpl: {} },
      requireDeclaredNativeBounds: false,
      escPosDevice: { dpi: 203, codePageIndex: 15, profile: s1EscPosProfile },
      tsplMedia: {},
      zplMedia: {},
    },
  }
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const importPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByTestId('template-import').setInputFiles({
    name: 'native-text-contract-project.json',
    mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(project)),
  })
  await importPreview
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('EscPosNative')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await setDesignerData(page, { Title: '中文ABC' })
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await page.getByRole('button', { name: '编辑机型参数' }).click()
  await expect(page.getByLabel('ESC/POS Native 文字编码代码页')).toHaveValue('936')
  await expect(page.getByLabel('ESC/POS Native 文字代码页索引')).toHaveValue('15')
  await expect(page.getByLabel('ESC/POS Native 文字字符模式')).toHaveValue('FsKanji')
  await expect(page.getByLabel('ESC/POS Native Unicode 文字范围')).toHaveValue('0-127, 19968-40959')
  await expect(page.getByLabel('ESC/POS Native 文字行高')).toHaveValue('24')

  const firstDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 ESC/POS' }).click()
  const firstDownload = await firstDownloadPromise
  const firstArchive = await JSZip.loadAsync(await readFile(await firstDownload.path()))
  const firstPage = firstArchive.file('page-001.escpos')
  expect(firstPage).not.toBeNull()
  const firstBytes = await firstPage!.async('uint8array')

  const savePromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const savedBytes = await readFile(await (await savePromise).path())
  const savedProject = JSON.parse(savedBytes.toString('utf8'))
  expect(savedProject.outputSettings).toMatchObject({
    outputTarget: 'EscPosNative',
    encoding: 'Gbk',
    escPosDevice: { codePageIndex: 15, profile: { text: s1GbkTextProfile } },
  })

  const reloadPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByTestId('template-import').setInputFiles({
    name: 'native-text-contract-roundtrip.json', mimeType: 'application/json', buffer: savedBytes,
  })
  await reloadPreview
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('EscPosNative')
  await expect(page.getByLabel('ESC/POS Native 文字编码代码页')).toHaveValue('936')
  await expect(page.getByLabel('ESC/POS Native 文字代码页索引')).toHaveValue('15')
  await expect(page.getByLabel('ESC/POS Native 文字字符模式')).toHaveValue('FsKanji')

  const secondDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 ESC/POS' }).click()
  const secondDownload = await secondDownloadPromise
  const secondArchive = await JSZip.loadAsync(await readFile(await secondDownload.path()))
  const secondPage = secondArchive.file('page-001.escpos')
  expect(secondPage).not.toBeNull()
  expect(await secondPage!.async('uint8array')).toEqual(firstBytes)
})

test('ESC/POS Raster 固定字库配置、能力脱敏与字体校验', async ({ request }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const regularData = await readFixtureFontBase64('DejaVuSansMono.ttf')
  const boldData = await readFixtureFontBase64('DejaVuSansMono-Bold.ttf')
  const rasterFonts = {
    defaultFont: 'FixtureMono',
    fallbackFonts: [],
    fonts: [{ name: 'FixtureMono', version: 'dejavu-2.37', regularData, boldData }],
  }
  const regularOnlyRasterFonts = {
    defaultFont: 'FixtureMono',
    fallbackFonts: [],
    fonts: [{ name: 'FixtureMono', version: 'dejavu-2.37', regularData }],
  }
  const capabilityResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: {
      protocol: 'EscPos', escPosLayoutMode: 'Raster', encoding: 'Utf8',
      escPosDevice: { rasterFonts },
    },
  })
  expect(capabilityResponse.status()).toBe(200)
  const capabilities = await capabilityResponse.json()
  expect(capabilities.nativeText).toBeNull()
  expect(capabilities.rasterFonts).toEqual({
    defaultFont: 'FixtureMono', fallbackFonts: [],
    fonts: [{ name: 'FixtureMono', version: 'dejavu-2.37', hasBold: true }],
  })
  expect(capabilities.rasterFonts.fonts[0]).not.toHaveProperty('regularData')
  expect(capabilities.rasterFonts.fonts[0]).not.toHaveProperty('boldData')

  const absentResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: { protocol: 'EscPos', escPosLayoutMode: 'Raster', encoding: 'Utf8' },
  })
  expect(absentResponse.status()).toBe(200)
  expect((await absentResponse.json()).rasterFonts).toBeNull()

  const nativeResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: {
      protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Ascii',
      escPosDevice: { rasterFonts },
    },
  })
  expect(nativeResponse.status()).toBe(400)
  expect(JSON.stringify(await nativeResponse.json())).toMatch(/Raster|固定字体/)

  const unknownFieldResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: {
      protocol: 'EscPos', escPosLayoutMode: 'Raster', encoding: 'Utf8',
      escPosDevice: {
        rasterFonts: {
          ...rasterFonts,
          fonts: [{ ...rasterFonts.fonts[0], unknownField: true }],
        },
      },
    },
  })
  expect(unknownFieldResponse.status()).toBe(400)

  const invalidFontResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: {
      protocol: 'EscPos', escPosLayoutMode: 'Raster', encoding: 'Utf8',
      escPosDevice: {
        rasterFonts: {
          defaultFont: 'FixtureMono', fallbackFonts: [],
          fonts: [{ name: 'FixtureMono', version: 'invalid', regularData: 'not-base64' }],
        },
      },
    },
  })
  expect(invalidFontResponse.status()).toBe(400)
  expect(JSON.stringify(await invalidFontResponse.json())).toMatch(/Base64|字体/)

  const validRenderResponse = await request.post(`${apiUrl}/api/designer/render`, {
    data: {
      template: rasterFontTemplate, data: { Title: 'ABC' }, protocol: 'EscPos',
      escPosLayoutMode: 'Raster', encoding: 'Utf8', escPosDevice: { rasterFonts },
    },
  })
  expect(validRenderResponse.status()).toBe(200)
  const validArchive = await JSZip.loadAsync(await validRenderResponse.body())
  const validPage = validArchive.file('page-001.escpos')
  expect(validPage).not.toBeNull()
  expect((await validPage!.async('uint8array')).length).toBeGreaterThan(0)

  const missingGlyphResponse = await request.post(`${apiUrl}/api/designer/preview`, {
    data: {
      template: rasterFontTemplate, data: { Title: '中文' }, protocol: 'EscPos',
      escPosLayoutMode: 'Raster', encoding: 'Utf8', escPosDevice: { rasterFonts },
    },
  })
  expect(missingGlyphResponse.status()).toBe(400)
  const missingGlyphError = await missingGlyphResponse.json()
  expect(missingGlyphError.errors[0]).toMatchObject({ sourcePath: 'Rows[0].Cells[0].Elements[0]' })
  expect(JSON.stringify(missingGlyphError.errors[0])).toMatch(/U\+|固定字体|字形/)

  const fakeBoldTemplate = JSON.parse(JSON.stringify(rasterFontTemplate)) as typeof rasterFontTemplate
  fakeBoldTemplate.Rows[0].Cells[0].Elements[0].Bold = true
  const fakeBoldResponse = await request.post(`${apiUrl}/api/designer/preview`, {
    data: {
      template: fakeBoldTemplate, data: { Title: 'ABC' }, protocol: 'EscPos',
      escPosLayoutMode: 'Raster', encoding: 'Utf8', escPosDevice: { rasterFonts: regularOnlyRasterFonts },
    },
  })
  expect(fakeBoldResponse.status()).toBe(400)
  const fakeBoldError = await fakeBoldResponse.json()
  expect(fakeBoldError.errors[0]).toMatchObject({ sourcePath: 'Rows[0].Cells[0].Elements[0]' })
  expect(JSON.stringify(fakeBoldError.errors[0])).toMatch(/Bold|粗体/)
})

test('ESC/POS Raster 固定字库项目导入显示并保存重载后字节一致', async ({ page }) => {
  const regularData = await readFixtureFontBase64('DejaVuSansMono.ttf')
  const boldData = await readFixtureFontBase64('DejaVuSansMono-Bold.ttf')
  const rasterFonts = {
    defaultFont: 'FixtureMono',
    fallbackFonts: [],
    fonts: [{ name: 'FixtureMono', version: 'dejavu-2.37', regularData, boldData }],
  }
  const project = {
    format: 'Bing.EasyPrint.DesignerProject',
    version: 1,
    template: rasterFontTemplate,
    outputSettings: {
      outputTarget: 'EscPosRaster',
      allowRasterizedLines: false,
      imageThreshold: 128,
      encoding: 'Utf8',
      deviceFontCalibrations: { Tspl: {}, Zpl: {} },
      requireDeclaredNativeBounds: false,
      escPosDevice: { rasterFonts },
      tsplMedia: {},
      zplMedia: {},
    },
  }
  const importBundle = new JSZip()
  importBundle.file('template.json', JSON.stringify(rasterFontTemplate))
  importBundle.file('manifest.json', JSON.stringify({
    format: 'Bing.EasyPrint.TemplateBundle', version: 1, resources: [], outputSettings: project.outputSettings,
  }))
  const importBundleBytes = await importBundle.generateAsync({ type: 'nodebuffer' })
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const importPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByTestId('template-import').setInputFiles({
    name: 'raster-font-project.zip', mimeType: 'application/zip', buffer: importBundleBytes,
  })
  await importPreview
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('EscPosRaster')
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await expect(page.getByText('固定 Raster 字体', { exact: true })).toBeVisible()
  await expect(page.getByLabel('Raster 默认字体')).toHaveValue('FixtureMono')
  await expect(page.getByText('FixtureMono · dejavu-2.37 · 已就绪 · 粗体已就绪', { exact: true })).toBeVisible()
  await setDesignerData(page, { Title: 'ABC' })

  const firstDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 ESC/POS' }).click()
  const firstDownload = await firstDownloadPromise
  const firstArchive = await JSZip.loadAsync(await readFile(await firstDownload.path()))
  const firstPage = firstArchive.file('page-001.escpos')
  expect(firstPage).not.toBeNull()
  const firstBytes = await firstPage!.async('uint8array')

  const savePromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const savedBytes = await readFile(await (await savePromise).path())
  const savedArchive = await JSZip.loadAsync(savedBytes)
  const manifest = JSON.parse(await savedArchive.file('manifest.json')!.async('string'))
  expect(manifest.outputSettings.escPosDevice.rasterFonts).toEqual(rasterFonts)

  const reloadPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByTestId('template-import').setInputFiles({
    name: 'raster-font-project-roundtrip.zip', mimeType: 'application/zip', buffer: savedBytes,
  })
  await reloadPreview
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('EscPosRaster')
  await expect(page.getByLabel('Raster 默认字体')).toHaveValue('FixtureMono')
  await expect(page.getByText('FixtureMono · dejavu-2.37 · 已就绪 · 粗体已就绪', { exact: true })).toBeVisible()

  const secondDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 ESC/POS' }).click()
  const secondDownload = await secondDownloadPromise
  const secondArchive = await JSZip.loadAsync(await readFile(await secondDownload.path()))
  const secondPage = secondArchive.file('page-001.escpos')
  expect(secondPage).not.toBeNull()
  expect(await secondPage!.async('uint8array')).toEqual(firstBytes)
})

test('ESC/POS Raster 固定字库通过 UI 上传并导出后与直接配置一致', async ({ page }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const regularBytes = await readFile(new URL('../../../tests/Fixtures/Fonts/DejaVuSansMono.ttf', import.meta.url))
  const boldBytes = await readFile(new URL('../../../tests/Fixtures/Fonts/DejaVuSansMono-Bold.ttf', import.meta.url))
  const regularData = regularBytes.toString('base64')
  const boldData = boldBytes.toString('base64')
  const rasterFonts = {
    defaultFont: 'UploadedMono',
    fallbackFonts: [],
    fonts: [{ name: 'UploadedMono', version: 'upload-1', regularData, boldData }],
  }
  const uploadTemplate = JSON.parse(JSON.stringify(rasterFontTemplate)) as typeof rasterFontTemplate
  uploadTemplate.Rows[0].Cells[0].Elements[0].Font = { Kind: 2, Name: 'UploadedMono' }

  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.getByTestId('template-import').setInputFiles({
    name: 'raster-upload-template.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(uploadTemplate)),
  })
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await page.getByRole('button', { name: '启用固定字体' }).click()
  await page.getByLabel('Raster 字体资源别名').fill('UploadedMono')
  await page.getByLabel('Raster 字体资源版本').fill('upload-1')
  await page.getByTestId('raster-font-regular-input').setInputFiles({
    name: 'DejaVuSansMono.ttf', mimeType: 'font/ttf', buffer: regularBytes,
  })
  await page.getByTestId('raster-font-bold-input').setInputFiles({
    name: 'DejaVuSansMono-Bold.ttf', mimeType: 'font/ttf', buffer: boldBytes,
  })
  await expect(page.getByText('DejaVuSansMono.ttf', { exact: true })).toBeVisible()
  await expect(page.getByText('DejaVuSansMono-Bold.ttf', { exact: true })).toBeVisible()

  const capabilityResponse = page.waitForResponse((response) => {
    if (!response.url().includes('/api/designer/capabilities') || response.request().method() !== 'POST') return false
    try {
      const body = JSON.parse(response.request().postData() ?? '{}') as { escPosDevice?: { rasterFonts?: { fonts?: Array<{ name?: string }> } } }
      return body.escPosDevice?.rasterFonts?.fonts?.some((font) => font.name === 'UploadedMono') === true
    } catch { return false }
  })
  await page.getByRole('button', { name: '添加字体资源' }).click()
  const capability = await capabilityResponse
  expect(capability.ok()).toBe(true)
  expect((await capability.json()).rasterFonts).toEqual({
    defaultFont: 'UploadedMono', fallbackFonts: [],
    fonts: [{ name: 'UploadedMono', version: 'upload-1', hasBold: true }],
  })
  await expect(page.getByText('UploadedMono · upload-1 · 已就绪 · 粗体已就绪', { exact: true })).toBeVisible()
  await setDesignerData(page, { Title: 'ABC' })

  const uiDownloadPromise = page.waitForEvent('download')
  await expect(page.getByRole('button', { name: '生成 ESC/POS' })).toBeEnabled()
  await page.getByRole('button', { name: '生成 ESC/POS' }).click()
  const uiArchive = await JSZip.loadAsync(await readFile(await (await uiDownloadPromise).path()))
  const uiPage = uiArchive.file('page-001.escpos')
  expect(uiPage).not.toBeNull()
  const uiBytes = await uiPage!.async('uint8array')

  const directResponse = await page.request.post(`${apiUrl}/api/designer/render`, {
    data: {
      template: uploadTemplate, data: { Title: 'ABC' }, protocol: 'EscPos',
      escPosLayoutMode: 'Raster', encoding: 'Utf8', escPosDevice: { rasterFonts },
    },
  })
  expect(directResponse.status()).toBe(200)
  const directArchive = await JSZip.loadAsync(await directResponse.body())
  expect(await directArchive.file('page-001.escpos')!.async('uint8array')).toEqual(uiBytes)

  const savePromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const savedArchive = await JSZip.loadAsync(await readFile(await (await savePromise).path()))
  const manifest = JSON.parse(await savedArchive.file('manifest.json')!.async('string'))
  expect(manifest.outputSettings.escPosDevice.rasterFonts).toEqual(rasterFonts)
})

test('ESC/POS Native 能力返回 Rows、Sequential，并拒绝 Raster 搭配 Sequential', async ({ page }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const rowsResponse = await page.request.post(`${apiUrl}/api/designer/capabilities`, {
    data: {
      protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Ascii',
      escPosDevice: { nativeFlow: 'Rows' },
    },
  })
  expect(rowsResponse.status()).toBe(200)
  expect(((await rowsResponse.json()) as Record<string, unknown>).nativeFlow).toBe('Rows')

  const sequentialResponse = await page.request.post(`${apiUrl}/api/designer/capabilities`, {
    data: {
      protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Ascii',
      escPosDevice: { nativeFlow: 'Sequential' },
    },
  })
  expect(sequentialResponse.status()).toBe(200)
  expect(((await sequentialResponse.json()) as Record<string, unknown>).nativeFlow).toBe('Sequential')

  const rasterResponse = await page.request.post(`${apiUrl}/api/designer/capabilities`, {
    data: { protocol: 'EscPos', escPosLayoutMode: 'Raster', encoding: 'Ascii' },
  })
  expect(rasterResponse.status()).toBe(200)
  expect(((await rasterResponse.json()) as Record<string, unknown>).nativeFlow).toBeNull()

  const invalidRasterResponse = await page.request.post(`${apiUrl}/api/designer/capabilities`, {
    data: {
      protocol: 'EscPos', escPosLayoutMode: 'Raster', encoding: 'Ascii',
      escPosDevice: { nativeFlow: 'Sequential' },
    },
  })
  expect(invalidRasterResponse.status()).toBe(400)
  expect(JSON.stringify(await invalidRasterResponse.json())).toMatch(/Native|Raster|顺序/)
})

test('ESC/POS Native Sequential 混排项目保存重载后字节保持一致', async ({ page }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const template = {
    Version: 3,
    Width: 320,
    Height: 0,
    AutoHeight: true,
    Rows: [
      { Height: 26, Cells: [{ Width: 320, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'TITLE' } }] }] },
      { Height: 26, Cells: [
        { Width: 160, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'ITEM' } }] },
        { Width: 160, Elements: [{ Type: 1, FontSize: 24, Field: { Value: '1' } }] },
      ] },
      { Height: 60, Cells: [{ Width: 320, Elements: [{
        Type: 4, BarcodeType: 0, LineWidth: 1, Ratio: 2, Width: 250, Height: 60,
        HumanReadable: false, Field: { Value: '1234' },
      }] }] },
      { Height: 84, Cells: [{ Width: 320, Elements: [{
        Type: 3, UnitWidth: 4, ErrorLevel: 'M', Width: 84, Height: 84,
        QuietZone: { Left: 0, Top: 0, Right: 0, Bottom: 0 }, Field: { Value: 'QR' },
      }] }] },
      { Height: 26, Cells: [{ Width: 320, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'TOTAL' } }] }] },
    ],
  }
  const project = {
    format: 'Bing.EasyPrint.DesignerProject',
    version: 1,
    template,
    outputSettings: {
      outputTarget: 'EscPosNative',
      allowRasterizedLines: false,
      imageThreshold: 128,
      encoding: 'Ascii',
      copies: 1,
      feedLines: 0,
      cutAfterPrint: false,
      deviceFontCalibrations: { Tspl: {}, Zpl: {} },
      requireDeclaredNativeBounds: false,
      escPosDevice: { nativeFlow: 'Sequential', nativeTabWidthDots: 160 },
      tsplMedia: {},
      zplMedia: {},
    },
  }

  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const initialPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByTestId('template-import').setInputFiles({
    name: 'sequential-receipt-project.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(project)),
  })
  await initialPreview
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('EscPosNative')
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await expect(page.getByLabel('ESC/POS Native 排版流')).toHaveValue('Sequential')

  const firstDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 ESC/POS' }).click()
  const firstDownload = await firstDownloadPromise
  const firstArchive = await JSZip.loadAsync(await readFile(await firstDownload.path()))
  const firstPage = firstArchive.file('page-001.escpos')
  expect(firstPage).not.toBeNull()
  const firstBytes = await firstPage!.async('uint8array')

  const savePromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const savedBytes = await readFile(await (await savePromise).path())
  const savedProject = JSON.parse(savedBytes.toString('utf8')) as {
    outputSettings: { outputTarget: string; escPosDevice: { nativeFlow: string } }
  }
  expect(savedProject.outputSettings).toMatchObject({
    outputTarget: 'EscPosNative', escPosDevice: { nativeFlow: 'Sequential' },
  })

  const reloadPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByTestId('template-import').setInputFiles({
    name: 'sequential-receipt-roundtrip.json', mimeType: 'application/json', buffer: savedBytes,
  })
  await reloadPreview
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('EscPosNative')
  await expect(page.getByLabel('ESC/POS Native 排版流')).toHaveValue('Sequential')

  const secondDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 ESC/POS' }).click()
  const secondDownload = await secondDownloadPromise
  const secondArchive = await JSZip.loadAsync(await readFile(await secondDownload.path()))
  const secondPage = secondArchive.file('page-001.escpos')
  expect(secondPage).not.toBeNull()
  expect(await secondPage!.async('uint8array')).toEqual(firstBytes)

  const preview = await page.request.post(`${apiUrl}/api/designer/preview`, {
    data: {
      template,
      data: {},
      protocol: 'EscPos',
      escPosLayoutMode: 'Native',
      encoding: 'Ascii',
      copies: 1,
      feedLines: 0,
      cutAfterPrint: false,
      escPosDevice: { nativeFlow: 'Sequential', nativeTabWidthDots: 160 },
    },
  })
  expect(preview.status()).toBe(200)
  const layout = await preview.json() as { pages: Array<{ height: number; elements: Array<{ type: number; y: number }> }> }
  expect(layout.pages[0].height).toBe(222)
  expect(layout.pages[0].elements.filter((element) => element.type !== 0).map((element) => element.y))
    .toEqual([0, 26, 26, 52, 112, 196])
})

test('缺失图片和超出资源数量上限时返回明确错误且没有部分页', async ({ page }) => {
  await page.goto('/')
  const template = {
    Version: 1,
    Width: 576,
    Height: 360,
    Rows: [{ Cells: [{ Width: 576, Elements: [{
      Type: 2,
      Width: 80,
      Height: 80,
      IsCompress: false,
      Field: { Name: 'resources/missing.png' },
      StartCoordinate: { X: 24, Y: 24 },
      EndCoordinate: { X: 104, Y: 104 },
    }] }] }],
  }
  const missingResourceResponse = await page.request.post('/api/designer/preview', { data: { template, data: {} } })
  expect(missingResourceResponse.status()).toBe(400)
  const missingResourceError = await missingResourceResponse.json()
  expect(missingResourceError.errors[0].sourcePath).toBe('Rows[0].Cells[0].Elements[0]')
  expect(missingResourceError.pages).toBeUndefined()

  const resources = Object.fromEntries(Array.from({ length: 65 }, (_, index) => [`resource-${index}`, 'eA==']))
  const tooManyResourcesResponse = await page.request.post('/api/designer/preview', { data: { template, data: {}, resources } })
  expect(tooManyResourcesResponse.status()).toBe(400)
  const tooManyResourcesError = await tooManyResourcesResponse.json()
  expect(tooManyResourcesError.errors[0].message).toContain('资源数量不能超过 64 个')
  expect(tooManyResourcesError.pages).toBeUndefined()
})

test('可添加嵌套行、查看业务字段并使用工具快捷键', async ({ page }) => {
  await openBlankTemplate(page)
  await page.locator('.tree-cell .tree-select').first().click()
  await page.getByRole('button', { name: '在此列中添加嵌套行' }).click()
  await expect(page.locator('.inspector-selection small')).toContainText('Rows[0].Cells[0].Rows[0]')

  await page.locator('.sample-picker select').selectOption('product')
  await page.locator('.tree-select').filter({ hasText: 'Product.Name' }).click()
  await expect(page.locator('datalist#business-field-paths option[value="Product.Name"]')).toHaveCount(1)
  await page.getByRole('button', { name: '字段', exact: true }).click()
  const fieldPath = page.getByLabel('字段路径')
  await fieldPath.fill('Product.ManualName')
  await fieldPath.press('Tab')
  await expect(fieldPath).toHaveValue('Product.ManualName')
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill('{"Product":{"ManualName":"字段绑定成功"}}')
  await page.locator('.designer-app').focus()
  await page.keyboard.press('q')
  await expect(page.locator('.toast-message')).toContainText('已添加二维码')
  await page.keyboard.press('t')
  await page.keyboard.press('b')
  await page.keyboard.press('l')
  await expect(page.locator('.tree-element')).toHaveCount(12)
})

test('拖动重复明细中的实例会同步修改该模板定义', async ({ page }) => {
  await page.goto('/')
  const picker = page.locator('.sample-picker select')
  await expect(picker.locator('option[value="service:receipt"]')).toHaveCount(1)
  await picker.selectOption('service:receipt')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await expect(page.locator('.file-state')).toContainText('分页小票')
  const response = await page.request.get('/api/designer/samples')
  const samples = await response.json()
  const receipt = samples.samples.find((sample: { id: string }) => sample.id === 'receipt')
  const layoutResponse = await page.request.post('/api/designer/preview', { data: { template: receipt.template, data: receipt.data } })
  const layout = await layoutResponse.json()
  const repeated = layout.pages.flatMap((part: { elements: Array<{ sourcePath: string; instanceIndices: number[]; x: number; y: number; width: number; height: number }> }) => part.elements)
    .filter((element: { sourcePath: string; instanceIndices: number[] }) => element.sourcePath === 'Rows[1].Cells[0].Elements[0]' && element.instanceIndices.length > 0)
  expect(repeated.length).toBeGreaterThan(1)
  const beforeX = repeated[0].x
  const canvasElements = layout.pages[0].elements.filter((element: { kind?: string; type: number }) => element.kind !== 'row' && element.kind !== 'cell' && element.type >= 1 && element.type <= 5)
  const targetIndex = canvasElements.findIndex((element: { sourcePath: string; instanceIndices: number[] }) =>
    element.sourcePath === 'Rows[1].Cells[0].Elements[0]' && element.instanceIndices[0] === 0)
  expect(targetIndex).toBeGreaterThanOrEqual(0)
  await expect.poll(async () => (await canvasGroupBounds(page)).length).toBeGreaterThan(targetIndex)
  const target = (await canvasGroupBounds(page))[targetIndex]
  const frame = await page.locator('.page-frame').boundingBox()
  expect(frame).not.toBeNull()
  const startX = frame!.x + target.x + target.width / 2
  const startY = frame!.y + target.y + target.height / 2
  await page.mouse.move(startX, startY)
  await page.mouse.down()
  await page.mouse.move(startX + 42, startY, { steps: 6 })
  await page.mouse.up()

  const position = dimensions(page)
  await expect.poll(async () => Number(await position.nth(0).inputValue())).toBeGreaterThan(beforeX)
  const jsonDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const updatedTemplate = JSON.parse(await readFile(await (await jsonDownloadPromise).path(), 'utf8'))
  const updatedResponse = await page.request.post('/api/designer/preview', { data: { template: updatedTemplate, data: receipt.data } })
  expect(updatedResponse.ok()).toBe(true)
  const updatedLayout = await updatedResponse.json()
  const updatedInstances = updatedLayout.pages.flatMap((part: { elements: Array<{ sourcePath: string; instanceIndices: number[]; x: number }> }) => part.elements)
    .filter((element: { sourcePath: string; instanceIndices: number[] }) => element.sourcePath === 'Rows[1].Cells[0].Elements[0]' && element.instanceIndices.length > 0)
  expect(updatedInstances.length).toBe(repeated.length)
  const updatedX = Number(await position.nth(0).inputValue())
  expect(updatedInstances.every((element: { x: number }) => element.x === updatedX)).toBe(true)
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
})

test('对齐辅助线在拖动时显示，图片快捷键可导入并随模板包往返', async ({ page }) => {
  await openBlankTemplate(page)
  await page.locator('.element-tools button').filter({ hasText: '文字' }).click()
  await page.locator('.element-tools button').filter({ hasText: '文字' }).click()
  await expect.poll(async () => (await canvasGroupBounds(page)).length).toBe(2)
  const frame = await page.locator('.page-frame').boundingBox()
  expect(frame).not.toBeNull()
  const groups = await canvasGroupBounds(page)
  expect(groups).toHaveLength(2)
  const target = groups[1]
  const startX = frame!.x + target.x + target.width / 2
  const startY = frame!.y + target.y + target.height / 2
  await page.mouse.move(startX, startY)
  await page.mouse.down()
  await page.mouse.move(startX + 4, startY, { steps: 3 })
  await expect.poll(() => canvasGuideCount(page, 'x')).toBeGreaterThan(0)
  await page.mouse.up()

  await page.locator('.designer-app').focus()
  await page.keyboard.press('i')
  const image = await readFile('../../tests/Bing.EasyPrint.Tests/Biz/Images/logo.jpg')
  await page.locator('input[type="file"]').nth(1).setInputFiles({ name: 'logo.jpg', mimeType: 'image/jpeg', buffer: image })
  await expect(page.locator('.toast-message')).toContainText('图片资源已导入')
  await expect(page.getByRole('button', { name: '模板包' })).toBeVisible()
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const resourceKey = await page.getByLabel('显示内容').inputValue()
  expect(resourceKey).toMatch(/^resources\/.*-logo\.jpg$/)
  await expect(page.locator('.resource-status')).toContainText('1 个资源')
  const imageDimensions = dimensions(page)
  await imageDimensions.nth(2).fill('96')
  await imageDimensions.nth(2).press('Tab')
  await imageDimensions.nth(3).fill('72')
  await imageDimensions.nth(3).press('Tab')
  await expect(imageDimensions.nth(2)).toHaveValue('96')
  await expect(imageDimensions.nth(3)).toHaveValue('72')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

  await page.locator('.output-settings-button').click()
  await page.getByRole('spinbutton', { name: '打印份数' }).fill('2')

  const originalCpclDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 CPCL' }).click()
  const originalCpclDownload = await originalCpclDownloadPromise
  const originalCpclArchive = await JSZip.loadAsync(await readFile(await originalCpclDownload.path()))
  const originalCpcl = await originalCpclArchive.file('page-001.cpcl')!.async('base64')

  const packageDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '模板包' }).click()
  const packageDownload = await packageDownloadPromise
  const packageBytes = await readFile(await packageDownload.path())
  const savedPackage = await JSZip.loadAsync(packageBytes)
  const manifest = JSON.parse(await savedPackage.file('manifest.json')!.async('string'))
  expect(manifest.outputSettings).toMatchObject({ outputTarget: 'Cpcl', copies: 2 })
  await page.locator('input[type="file"]').first().setInputFiles({ name: 'roundtrip.zip', mimeType: 'application/zip', buffer: packageBytes })
  await expect(page.locator('.toast-message')).toContainText('已导入模板包 · 1 个资源')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await expect(page.getByRole('spinbutton', { name: '打印份数' })).toHaveValue('2')

  const cpclDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 CPCL' }).click()
  const cpclDownload = await cpclDownloadPromise
  const cpclArchive = await JSZip.loadAsync(await readFile(await cpclDownload.path()))
  expect(cpclArchive.file('page-001.cpcl')).not.toBeNull()
  expect(await cpclArchive.file('page-001.cpcl')!.async('base64')).toBe(originalCpcl)
})

test('窄屏可打开结构树和属性面板', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

  const layers = page.locator('.layers-shell')
  await expect(layers).not.toHaveClass(/mobile-open/)
  await page.getByRole('button', { name: '打开结构树' }).click()
  await expect(layers).toHaveClass(/mobile-open/)
  await expect(layers).toHaveCSS('transform', 'matrix(1, 0, 0, 1, 0, 0)')
  await page.screenshot({ path: 'test-results/designer-mobile-layers.png' })
  await page.locator('.mobile-scrim').click({ position: { x: 350, y: 150 } })
  await expect(layers).not.toHaveClass(/mobile-open/)

  const inspector = page.locator('.inspector-shell')
  await page.getByRole('button', { name: '打开属性' }).click()
  await expect(inspector).toHaveClass(/mobile-open/)
  await expect(inspector).toHaveCSS('transform', 'matrix(1, 0, 0, 1, 0, 0)')
  await page.screenshot({ path: 'test-results/designer-mobile-inspector.png' })
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
})

test('窄屏可编辑组件参数并导入组件定义', async ({ page }) => {
  await page.goto('/')
  const fixture = { Version: 3, Width: 240, Height: 120,
    Components: [{ Id: 'badge', Width: 120, Height: 32, Parameters: { caption: { Required: true } },
      Elements: [{ Type: 1, FontSize: 24, Field: { Name: '$param.caption' }, StartCoordinate: { X: 0, Y: 0 } }] }],
    Rows: [{ Cells: [{ Elements: [{ Type: 6, ComponentId: 'badge', Parameters: { caption: { Value: 'OLD' } },
      StartCoordinate: { X: 8, Y: 8 } }] }] }] }
  await reloadTemplateJson(page, Buffer.from(JSON.stringify(fixture)))
  await page.setViewportSize({ width: 390, height: 844 })
  await page.getByRole('button', { name: '打开结构树' }).click()
  await page.locator('.tree-select[title="Rows[0].Cells[0].Elements[0]"]').click()
  await page.getByRole('button', { name: '打开属性' }).click()
  const parameter = page.getByLabel('caption 静态 JSON')
  await expect(parameter).toBeVisible()
  await parameter.fill('"NEW"')
  await parameter.press('Tab')
  await page.setViewportSize({ width: 1440, height: 900 })
  const saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(saved.Rows[0].Cells[0].Elements[0].Parameters.caption.Value).toBe('NEW')

  await page.setViewportSize({ width: 390, height: 844 })
  await page.getByTestId('component-import').setInputFiles({
    name: 'component.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify({ Version: 3, Width: 240, Height: 120, Rows: [], Components: [{ Id: 'extra', Width: 20, Height: 20, Elements: [] }] })),
  })
  await expect(page.locator('.toast-message')).toContainText('已导入 1 个组件')
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
})

test('循环组件导入被拒绝且当前模板仍可编辑', async ({ page }) => {
  await openBlankTemplate(page)
  const invalid = { Version: 2, Width: 100, Height: 100,
    Components: [{ Id: 'loop', Width: 20, Height: 20, Elements: [{ Type: 6, ComponentId: 'loop' }] }],
    Rows: [{ Height: 100, Cells: [{ Width: 100, Elements: [{ Type: 6, ComponentId: 'loop' }] }] }] }
  await page.getByTestId('template-import').setInputFiles({ name: 'cycle.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(invalid)) })
  await expect(page.locator('.toast-message')).toContainText('循环引用')
  await expect(page.locator('.canvas-breadcrumb strong')).toHaveText('576 × 360 点')
  await page.locator('.element-tools button').filter({ hasText: '文字' }).click()
  await expect(page.locator('.tree-row.tree-element')).toHaveCount(1)
})

test('多选编组、拆组与动态表格配置写回 JSON', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.locator('.advanced-tools button').filter({ hasText: '矩形' }).click()
  await page.locator('.advanced-tools button').filter({ hasText: '椭圆' }).click()
  await page.getByRole('button', { name: '单行文字', exact: true }).click()
  const entries = page.locator('.tree-row.tree-element .tree-select')
  await entries.nth(0).click()
  await entries.nth(1).click({ modifiers: ['Control'] })
  await page.locator('.advanced-tools button').filter({ hasText: '组合元素' }).click()
  await expect(page.locator('.tree-row.tree-element')).toHaveCount(4)
  await page.locator('.advanced-tools button').filter({ hasText: '拆开组合' }).click()
  await expect(page.locator('.tree-row.tree-element')).toHaveCount(3)
  await page.locator('.advanced-tools button').filter({ hasText: '动态表格' }).click()
  await page.getByRole('textbox', { name: '第 1 列标题' }).fill('商品')
  await page.getByLabel('第 1 列字段').fill('Name')
  await page.getByRole('spinbutton', { name: '第 1 列宽' }).fill('360')
  await page.getByLabel('第 2 列字段').fill('Amount')
  await page.getByRole('button', { name: '创建表格' }).click()
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const value = JSON.parse(await readFile(await (await downloadPromise).path(), 'utf8'))
  expect(value.Rows[0].Cells[0].Elements).toHaveLength(3)
  expect(value.Rows.at(-2).Cells[0].Width).toBe(360)
  expect(value.Rows.at(-1).Cells[1].Elements[0].Field.Name).toBe('Amount')
})

test('画布尺寸手柄调整矩形并保存打印点尺寸', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.locator('.advanced-tools button').filter({ hasText: '矩形' }).click()
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await expect.poll(() => page.evaluate(() => {
    const root = document.querySelector('.page-frame')!.firstElementChild as HTMLElement & { __vueParentComponent?: any }
    return root.__vueParentComponent?.exposed?.getNode?.().find('Circle').length ?? 0
  })).toBe(1)
  const inputs = dimensions(page)
  const before = [Number(await inputs.nth(2).inputValue()), Number(await inputs.nth(3).inputValue())]
  const frame = await page.locator('.page-frame').boundingBox()
  expect(frame).not.toBeNull()
  const x = frame!.x + (Number(await inputs.nth(0).inputValue()) + before[0]) * .72
  const y = frame!.y + (Number(await inputs.nth(1).inputValue()) + before[1]) * .72
  await page.mouse.move(x, y)
  await page.mouse.down()
  await page.mouse.move(x + 29, y + 22, { steps: 6 })
  await page.mouse.up()
  await expect.poll(async () => Number(await inputs.nth(2).inputValue())).toBeGreaterThan(before[0])
  await expect.poll(async () => Number(await inputs.nth(3).inputValue())).toBeGreaterThan(before[1])
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const value = JSON.parse(await readFile(await (await downloadPromise).path(), 'utf8'))
  expect(value.Rows[0].Cells[0].Elements[0].Width).toBe(Number(await inputs.nth(2).inputValue()))
})

test('自动排列文字编组后保留位置并继续通过引擎预览', async ({ page }) => {
  await openBlankTemplate(page)
  const template = { Version: 2, Width: 160, Height: 120, Rows: [{ Height: 120, Cells: [{ Width: 160,
    Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'FIRST' } },
      { Type: 1, FontSize: 24, Field: { Value: 'SECOND' } }] }] }] }
  const beforeResponse = await page.request.post('/api/designer/render', {
    data: { template, data: {}, resources: {} },
  })
  expect(beforeResponse.ok()).toBe(true)
  const beforeArchive = await JSZip.loadAsync(await beforeResponse.body())
  const beforeCpcl = await beforeArchive.file('page-001.cpcl')!.async('uint8array')
  await page.getByTestId('template-import').setInputFiles({
    name: 'flow.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)),
  })
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.getByRole('button', { name: '组件与表格' }).click()
  const entries = page.locator('.tree-row.tree-element .tree-select')
  await entries.nth(0).click()
  await entries.nth(1).click({ modifiers: ['Control'] })
  await page.locator('.advanced-tools button').filter({ hasText: '组合元素' }).click()
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const saved = JSON.parse(await readFile(await (await downloadPromise).path(), 'utf8'))
  const group = saved.Rows[0].Cells[0].Elements[0]
  expect(group).toMatchObject({ Type: 6, Width: 160, Height: 48 })
  expect(group.Elements.map((element: { StartCoordinate: { Y: number } }) => element.StartCoordinate.Y)).toEqual([0, 24])
  const afterResponse = await page.request.post('/api/designer/render', {
    data: { template: saved, data: {}, resources: {} },
  })
  expect(afterResponse.ok()).toBe(true)
  const afterArchive = await JSZip.loadAsync(await afterResponse.body())
  expect(await afterArchive.file('page-001.cpcl')!.async('uint8array')).toEqual(beforeCpcl)
})

test('非法模板结构与元素在预览响应中带有节点路径', async ({ page }) => {
  const response = await page.request.post('/api/designer/preview', { data: {
    template: { Version: 2, Width: 120, Height: 100, Rows: [{ Cells: [{ Elements: [
      { Type: 7, Width: 0, Height: 20 },
    ] }] }] }, data: {}, resources: {},
  } })
  expect(response.status()).toBe(400)
  expect((await response.json()).errors[0].sourcePath).toBe('Rows[0].Cells[0].Elements[0]')
})

test('组件库导出后可在新模板导入并插入实例', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.locator('.advanced-tools button').filter({ hasText: '矩形' }).click()
  await page.locator('.advanced-tools button').filter({ hasText: '组合元素' }).click()
  await expect(page.getByRole('textbox', { name: '组件标识' })).toBeVisible()
  await page.getByRole('textbox', { name: '组件标识' }).fill('badge')
  await page.getByRole('button', { name: '保存组件' }).click()
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出组件' }).click()
  const library = await readFile(await (await downloadPromise).path())
  await page.locator('.sample-picker select').selectOption('blank')
  await page.getByTestId('component-import').setInputFiles({
    name: 'components.json', mimeType: 'application/json', buffer: library,
  })
  await expect(page.locator('.component-library')).toContainText('badge')
  await page.locator('.component-entry button[title="插入组件 badge"]').click()
  const templateDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const saved = JSON.parse(await readFile(await (await templateDownload).path(), 'utf8'))
  expect(saved.Components).toHaveLength(1)
  expect(saved.Rows[0].Cells[0].Elements[0]).toMatchObject({ Type: 6, ComponentId: 'badge' })
})

test('嵌套组件和图片经组件库 ZIP 往返后 CPCL 保持一致', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('button', { name: '组件与表格' }).click()
  const image = await readFile('../../tests/Bing.EasyPrint.Tests/Biz/Images/logo.jpg')
  const fixture = { Version: 3, Width: 576, Height: 360, Rows: [], Components: [
    { Id: 'picture', Width: 120, Height: 80,
      Elements: [{ Type: 2, Width: 96, Height: 72, Field: { Value: 'resources/logo.jpg' } }] },
    { Id: 'badge', Width: 120, Height: 80, Elements: [{ Type: 6, ComponentId: 'picture' }] },
  ] }
  const zip = new JSZip()
  zip.file('template.json', JSON.stringify(fixture))
  zip.file('resources/001-logo.jpg', image)
  zip.file('manifest.json', JSON.stringify({ format: 'Bing.EasyPrint.TemplateBundle', version: 1,
    resources: [{ key: 'resources/logo.jpg', path: 'resources/001-logo.jpg', fileName: 'logo.jpg', mimeType: 'image/jpeg' }] }))
  const importedBundle = await zip.generateAsync({ type: 'nodebuffer' })
  await page.getByTestId('component-import').setInputFiles({
    name: 'components.zip', mimeType: 'application/zip', buffer: importedBundle,
  })
  await expect(page.locator('.component-library')).toContainText('badge')
  await page.locator('.component-entry button[title="插入组件 badge"]').click()
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const before = await exportFirstCpclPage(page)
  const componentDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出组件' }).click()
  const library = await readFile(await (await componentDownload).path())
  const archive = await JSZip.loadAsync(library)
  const exported = JSON.parse(await archive.file('template.json')!.async('string'))
  expect(exported.Components.map((component: { Id: string }) => component.Id)).toEqual(['picture', 'badge'])
  expect(archive.file('resources/001-logo.jpg')).not.toBeNull()

  await page.locator('.sample-picker select').selectOption('blank')
  await page.getByTestId('component-import').setInputFiles({
    name: 'components.zip', mimeType: 'application/zip', buffer: library,
  })
  await page.locator('.component-entry button[title="插入组件 badge"]').click()
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  expect(await exportFirstCpclPage(page)).toEqual(before)
})

test('页面边框、文字样式与元素间距可编辑并保存', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('button', { name: '单行文字', exact: true }).click()
  await page.getByLabel('下划线').check()
  await page.locator('.inspector-section').filter({ hasText: '元素间距' }).getByLabel('左').fill('6')
  await page.locator('.page-tree-row').click()
  await page.locator('.inspector-section').filter({ hasText: '页面边框' }).getByLabel('右').selectOption('1')
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const saved = JSON.parse(await readFile(await (await downloadPromise).path(), 'utf8'))
  expect(saved.Border.RightStyle).toBe(1)
  expect(saved.Rows[0].Cells[0].Elements[0].FontStyle & 4).toBe(4)
  expect(saved.Rows[0].Cells[0].Elements[0].Margin.Left).toBe(6)
})

test('文字字体来源在旧版字段与语义字体之间切换并往返保存', async ({ page }) => {
  await openBlankTemplate(page)
  await page.getByRole('button', { name: '单行文字', exact: true }).click()
  const fontSize = page.getByLabel('字号')
  const beforeFontSize = await fontSize.inputValue()

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.getByLabel('字体来源').selectOption('host')
  await page.getByLabel('主机字体名称').fill('Microsoft YaHei')
  const hostDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const hostTemplate = JSON.parse((await readFile(await (await hostDownload).path())).toString('utf8'))
  const hostElement = hostTemplate.Rows[0].Cells[0].Elements[0]
  expect(hostElement.Font).toEqual({ Kind: 2, Name: 'Microsoft YaHei' })
  expect(hostElement).not.toHaveProperty('FontName')
  expect(hostElement).not.toHaveProperty('FontFamily')
  expect(hostElement).not.toHaveProperty('DeviceFontName')
  expect(await fontSize.inputValue()).toBe(beforeFontSize)

  await page.getByLabel('字体来源').selectOption('default')
  await expect(page.getByLabel('主机字体名称')).toHaveCount(0)
  const defaultDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const defaultTemplate = JSON.parse((await readFile(await (await defaultDownload).path())).toString('utf8'))
  expect(defaultTemplate.Rows[0].Cells[0].Elements[0].Font).toEqual({ Kind: 0 })

  await page.getByLabel('字体来源').selectOption('legacy')
  await expect(page.getByLabel('字体编号')).toBeVisible()
  await expect(page.getByLabel('系统字体族')).toBeVisible()
  await expect(page.getByLabel('设备字体')).toBeVisible()
  const legacyDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const legacyTemplate = JSON.parse((await readFile(await (await legacyDownload).path())).toString('utf8'))
  expect(legacyTemplate.Rows[0].Cells[0].Elements[0]).not.toHaveProperty('Font')
  expect(await fontSize.inputValue()).toBe(beforeFontSize)
})

test('Host 语义字体贯通四协议预览导出并严格拒绝旧字库及非法 JSON', async ({ request }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const template = (font: object) => ({
    Version: 1, Width: 240, Height: 100,
    Rows: [{ Cells: [{ Width: 240, Elements: [{ Type: 1, FontSize: 24,
      Field: { Value: 'ABC' }, ...font }] }] }],
  })
  const cases = [
    { protocol: 'Cpcl', font: { Kind: 1, Name: '24' }, file: 'page-001.cpcl', marker: 'T 24 0 0 0 ABC' },
    { protocol: 'Tspl', font: { Kind: 1, Name: '1' }, file: 'page-001.tspl', marker: 'TEXT 0,0,"1"' },
    { protocol: 'Zpl', font: { Kind: 1, Name: 'A' }, file: 'page-001.zpl', marker: '^AAN' },
    { protocol: 'EscPos', font: { Kind: 0 }, escPosLayoutMode: 'Native', file: 'page-001.escpos', marker: 'ABC' },
    { protocol: 'EscPos', font: { Kind: 0 }, escPosLayoutMode: 'Raster', file: 'page-001.escpos' },
  ]
  for (const item of cases) {
    const payload = { template: template({ Font: item.font }), data: {}, encoding: 'Ascii',
      protocol: item.protocol, escPosLayoutMode: item.escPosLayoutMode }
    const preview = await request.post(`${apiUrl}/api/designer/preview`, { data: payload })
    expect(preview.status()).toBe(200)
    expect((await preview.json()).pages[0].elements.some((element: { text?: string }) => element.text === 'ABC')).toBe(true)
    const render = await request.post(`${apiUrl}/api/designer/render`, { data: payload })
    expect(render.status()).toBe(200)
    const archive = await JSZip.loadAsync(await render.body())
    const entry = archive.file(item.file)
    expect(entry).not.toBeNull()
    if (item.marker) expect(await entry!.async('string')).toContain(item.marker)
    else {
      const bytes = await entry!.async('uint8array')
      expect(bytes.some((value, index) => value === 0x76 && bytes[index - 1] === 0x1D && bytes[index + 1] === 0x30)).toBe(true)
    }
  }

  const rejected = await request.post(`${apiUrl}/api/designer/preview`, {
    data: { template: template({ FontName: 24 }), data: {}, protocol: 'Tspl', encoding: 'Ascii' },
  })
  expect(rejected.status()).toBe(400)
  const error = (await rejected.json()).errors[0]
  expect(error.sourcePath).toBe('Rows[0].Cells[0].Elements[0]')
  expect(error.message).toContain('FontName=24')

  const lossy = await request.post(`${apiUrl}/api/designer/preview`, {
    data: { template: template({ Font: { Kind: 1.5, Name: '1' } }), data: {}, protocol: 'Tspl' },
  })
  expect(lossy.status()).toBe(400)
})

test('五种业务价签样例可编辑、保存、重载并保持 CPCL 字节一致', async ({ page }) => {
  test.setTimeout(120_000)
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const picker = page.locator('.sample-picker select')
  const samples = [
    ['price-label-15', '159 × 159 点'],
    ['price-label-70', '540 × 300 点'],
    ['price-label-90', '671 × 320 点'],
    ['pm0025', '671 × 360 点'],
    ['pm0034', '600 × 320 点'],
  ] as const

  for (const [id, size] of samples) {
    await picker.selectOption(`service:${id}`)
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
    await expect(page.locator('.canvas-breadcrumb strong')).toHaveText(size)
    await expect(page.getByRole('button', { name: '生成 CPCL' })).toBeEnabled()
    const originalCpcl = await exportFirstCpclPage(page)

    await page.locator('.tree-row.tree-element .tree-select').first().click()
    const previewPromise = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
    await page.getByRole('button', { name: '右对齐' }).click()
    await previewPromise
    await expect(page.getByRole('button', { name: '右对齐' })).toHaveClass(/active/)
    const editedCpcl = await exportFirstCpclPage(page)
    expect(editedCpcl).not.toEqual(originalCpcl)

    const templateBytes = await exportTemplateJson(page)
    const saved = JSON.parse(templateBytes.toString('utf8'))
    const firstFont = saved.Rows.flatMap((row: any) => row.Cells.flatMap((cell: any) => cell.Elements))
      .find((element: { Type: number }) => element.Type === 1)
    expect(firstFont.TextAlignment).toBe(2)
    await reloadTemplateJson(page, templateBytes, `${id}.json`)
    await expect(page.locator('.canvas-breadcrumb strong')).toHaveText(size)
    expect(await exportFirstCpclPage(page)).toEqual(editedCpcl)
  }
})

test('动态表格在设计器中分页预览且保存重载后 CPCL 不变', async ({ page }) => {
  test.setTimeout(90_000)
  await openBlankTemplate(page)
  const data = { Items: Array.from({ length: 27 }, (_, index) => ({
    Name: `商品-${String(index + 1).padStart(2, '0')}`,
    Amount: `${index + 1}.00`,
  })) }
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify(data))

  await page.locator('.tree-row.tree-row .tree-select').first().click()
  const rowHeight = page.getByLabel('行高')
  await rowHeight.fill('0')
  await rowHeight.press('Tab')
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.locator('.advanced-tools button').filter({ hasText: '动态表格' }).click()
  await page.getByLabel('第 1 列标题').fill('商品')
  await page.getByLabel('第 1 列字段').fill('Name')
  await page.getByLabel('第 1 列宽').fill('400')
  await page.getByLabel('第 2 列标题').fill('金额')
  await page.getByLabel('第 2 列字段').fill('Amount')
  const previewPromise = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await page.getByRole('button', { name: '创建表格' }).click()
  await previewPromise
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await expect(page.locator('.page-pager')).toBeVisible()
  await expect(page.locator('.page-pager')).not.toContainText('1 / 1')

  const templateBytes = await exportTemplateJson(page)
  const saved = JSON.parse(templateBytes.toString('utf8'))
  const headerIndex = saved.Rows.findIndex((row: { Section?: string }) => row.Section === 'TableHeader')
  const detailIndex = saved.Rows.findIndex((row: { RepeatSource?: string }) => row.RepeatSource === 'Items')
  expect(headerIndex).toBeGreaterThanOrEqual(0)
  expect(detailIndex).toBeGreaterThan(headerIndex)
  const response = await page.request.post('/api/designer/preview', { data: { template: saved, data } })
  expect(response.ok()).toBe(true)
  const preview = await response.json()
  expect(preview.pages.length).toBeGreaterThan(1)
  const headerPath = `Rows[${headerIndex}].Cells[0].Elements[0]`
  for (const renderedPage of preview.pages) {
    expect(renderedPage.elements.some((element: { sourcePath: string; text?: string }) =>
      element.sourcePath === headerPath && element.text === '商品')).toBe(true)
  }
  const detailPath = `Rows[${detailIndex}].Cells[0].Elements[0]`
  const renderedItems = preview.pages.flatMap((renderedPage: { elements: Array<{ sourcePath: string; text?: string; instanceIndices?: number[] }> }) => renderedPage.elements)
    .filter((element: { sourcePath: string }) => element.sourcePath === detailPath)
    .sort((left: { instanceIndices?: number[] }, right: { instanceIndices?: number[] }) =>
      (left.instanceIndices?.[0] ?? -1) - (right.instanceIndices?.[0] ?? -1))
  expect(renderedItems.map((element: { text?: string }) => element.text)).toEqual(data.Items.map((item) => item.Name))

  const beforeReload = await exportFirstCpclPage(page)
  await reloadTemplateJson(page, templateBytes, 'dynamic-table.json')
  expect(await exportFirstCpclPage(page)).toEqual(beforeReload)
})

test('变量路径可在属性面板修改并影响保存重载后的绑定输出', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.locator('.sample-picker select').selectOption('receipt-v2')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.locator('.page-tree-row').click()
  await page.getByLabel('变量 shop 的字段路径').fill('Store.PrintName')
  const data = {
    Store: { PrintName: '变量绑定输出成功' },
    OrderNo: '订单 #E2E',
    Items: [{ Name: '样例商品', Amount: '12.00' }],
    Total: '合计 12.00',
  }
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  const previewPromise = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify(data))
  await previewPromise
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

  const templateBytes = await exportTemplateJson(page)
  const saved = JSON.parse(templateBytes.toString('utf8'))
  expect(saved.Variables.shop).toBe('Store.PrintName')
  const beforeReload = await exportFirstCpclPage(page)
  await reloadTemplateJson(page, templateBytes, 'variable.json')
  expect(await exportFirstCpclPage(page)).toEqual(beforeReload)

  const response = await page.request.post('/api/designer/preview', { data: { template: saved, data } })
  expect(response.ok()).toBe(true)
  const preview = await response.json()
  const shop = preview.pages.flatMap((renderedPage: { elements: Array<{ sourcePath: string; text?: string }> }) => renderedPage.elements)
    .find((element: { sourcePath: string }) => element.sourcePath === 'Rows[0].Cells[0].Elements[0]')
  expect(shop?.text).toBe('变量绑定输出成功')
})

test('CSV 与 TSV 可通过文字转表格渲染并往返保存', async ({ page }) => {
  test.setTimeout(90_000)
  const cases = [
    { name: 'quoted.csv', source: '名称,规格\n商品 A,250g\n"苹果,特级",500g', cells: ['名称', '规格', '商品 A', '250g', '苹果,特级', '500g'] },
    { name: 'inventory.tsv', source: '条码\t数量\n6900001\t3\n6900002\t5', cells: ['条码', '数量', '6900001', '3', '6900002', '5'] },
  ]

  for (const item of cases) {
    await openBlankTemplate(page)
    await page.getByRole('button', { name: '组件与表格' }).click()
    await page.locator('.advanced-tools button').filter({ hasText: '文字转表格' }).click()
    await page.getByLabel('粘贴表格文字').fill(item.source)
    const previewPromise = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
    await page.getByRole('button', { name: '插入表格' }).click()
    await previewPromise
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

    const templateBytes = await exportTemplateJson(page)
    const saved = JSON.parse(templateBytes.toString('utf8'))
    expect(saved.Version).toBe(2)
    const tableRows = saved.Rows.slice(-(item.cells.length / 2))
    const values = tableRows.flatMap((row: any) => row.Cells.map((cell: any) => cell.Elements[0].Field.Value))
    expect(values).toEqual(item.cells)

    const response = await page.request.post('/api/designer/preview', { data: { template: saved, data: {} } })
    expect(response.ok()).toBe(true)
    const preview = await response.json()
    const renderedValues = preview.pages.flatMap((renderedPage: { elements: Array<{ text?: string }> }) => renderedPage.elements)
      .map((element: { text?: string }) => element.text).filter((value: string | undefined) => value !== undefined)
    for (const value of item.cells) expect(renderedValues).toContain(value)

    const beforeReload = await exportFirstCpclPage(page)
    await reloadTemplateJson(page, templateBytes, item.name)
    expect(await exportFirstCpclPage(page)).toEqual(beforeReload)
  }
})

test('超高元素预览与 CPCL 导出返回相同错误节点路径', async ({ page }) => {
  const template = {
    Version: 2,
    Width: 100,
    Height: 100,
    Rows: [{ Height: 100, Cells: [{ Width: 100, Elements: [{ Type: 7, Width: 20, Height: 120 }] }] }],
  }
  const payload = { template, data: {}, resources: {} }
  const previewResponse = await page.request.post('/api/designer/preview', { data: payload })
  expect(previewResponse.status()).toBe(400)
  const previewError = await previewResponse.json()
  expect(previewError.errors[0].sourcePath).toBe('Rows[0].Cells[0].Elements[0]')

  const renderResponse = await page.request.post('/api/designer/render', { data: payload })
  expect(renderResponse.status()).toBe(400)
  const renderError = await renderResponse.json()
  expect(renderError.errors[0].sourcePath).toBe(previewError.errors[0].sourcePath)
})

test('方向键首次移动自动排列文字时保留实际预览坐标', async ({ page }) => {
  await openBlankTemplate(page)
  const template = {
    Version: 2,
    Width: 160,
    Height: 120,
    Rows: [{ Height: 120, Cells: [{ Width: 160, Elements: [
      { Type: 1, FontSize: 24, Field: { Value: 'FIRST' } },
      { Type: 1, FontSize: 24, Field: { Value: 'SECOND' } },
    ] }] }],
  }
  const uploadPromise = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'auto-flow.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)),
  })
  await uploadPromise
  const beforeResponse = await page.request.post('/api/designer/preview', { data: { template, data: {} } })
  expect(beforeResponse.ok()).toBe(true)
  const beforeLayout = await beforeResponse.json()
  const original = beforeLayout.pages.flatMap((renderedPage: { elements: Array<{ sourcePath: string; kind?: string; y: number }> }) => renderedPage.elements)
    .find((element: { sourcePath: string; kind?: string }) => element.sourcePath === 'Rows[0].Cells[0].Elements[0]' && element.kind === 'element')
  expect(original).toBeTruthy()

  await page.locator('.tree-row.tree-element .tree-select').first().click()
  await page.locator('.designer-app').focus()
  const previewPromise = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await page.keyboard.press('ArrowDown')
  await previewPromise
  const position = dimensions(page)
  await expect(position.nth(1)).toHaveValue(String(original.y + 1))
  const templateBytes = await exportTemplateJson(page)
  const saved = JSON.parse(templateBytes.toString('utf8'))
  expect(saved.Rows[0].Cells[0].Elements[0].StartCoordinate).toMatchObject({ X: original.x, Y: original.y + 1 })
})

test('自动排列文本拆组后保留顺序、坐标并维持 CPCL 输出', async ({ page }) => {
  await openBlankTemplate(page)
  const template = {
    Version: 2,
    Width: 160,
    Height: 120,
    Rows: [{ Height: 120, Cells: [{ Width: 160, Elements: [
      { Type: 1, FontSize: 24, Field: { Value: 'FIRST' } },
      { Type: 1, FontSize: 24, Field: { Value: 'SECOND' } },
    ] }] }],
  }
  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'auto-flow-ungroup.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)),
  })
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const originalLayoutResponse = await page.request.post('/api/designer/preview', { data: { template, data: {} } })
  expect(originalLayoutResponse.ok()).toBe(true)
  const originalLayout = await originalLayoutResponse.json()
  const originalPositions = originalLayout.pages.flatMap((renderedPage: { elements: Array<{ sourcePath: string; kind?: string; text?: string; x: number; y: number; width: number; height: number }> }) => renderedPage.elements)
    .filter((element: { sourcePath: string; kind?: string }) => element.kind === 'element' && element.sourcePath.startsWith('Rows[0].Cells[0].Elements['))
    .map((element: { text?: string; x: number; y: number; width: number; height: number }) => ({ text: element.text, x: element.x, y: element.y, width: element.width, height: element.height }))
  expect(originalPositions.map((element: { text?: string }) => element.text)).toEqual(['FIRST', 'SECOND'])
  const originalCpcl = await exportFirstCpclPage(page)

  await page.getByRole('button', { name: '组件与表格' }).click()
  const entries = page.locator('.tree-row.tree-element .tree-select')
  await entries.nth(0).click()
  await entries.nth(1).click({ modifiers: ['Control'] })
  const groupPreview = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await page.locator('.advanced-tools button').filter({ hasText: '组合元素' }).click()
  await groupPreview
  const ungroupPreview = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await page.locator('.advanced-tools button').filter({ hasText: '拆开组合' }).click()
  await ungroupPreview

  const savedBytes = await exportTemplateJson(page)
  const saved = JSON.parse(savedBytes.toString('utf8'))
  const elements = saved.Rows[0].Cells[0].Elements
  expect(elements.map((element: { Field?: { Value?: string } }) => element.Field?.Value)).toEqual(['FIRST', 'SECOND'])
  expect(elements[0].StartCoordinate).toBeTruthy()
  expect(elements[1].StartCoordinate.Y).toBeGreaterThan(elements[0].StartCoordinate.Y)
  const restoredResponse = await page.request.post('/api/designer/preview', { data: { template: saved, data: {} } })
  expect(restoredResponse.ok()).toBe(true)
  const restoredLayout = await restoredResponse.json()
  const restoredPositions = restoredLayout.pages.flatMap((renderedPage: { elements: Array<{ sourcePath: string; kind?: string; text?: string; x: number; y: number; width: number; height: number }> }) => renderedPage.elements)
    .filter((element: { sourcePath: string; kind?: string }) => element.kind === 'element' && element.sourcePath.startsWith('Rows[0].Cells[0].Elements['))
    .map((element: { text?: string; x: number; y: number; width: number; height: number }) => ({ text: element.text, x: element.x, y: element.y, width: element.width, height: element.height }))
  expect(restoredPositions).toEqual(originalPositions)
  expect(await exportFirstCpclPage(page)).toEqual(originalCpcl)
})

test('多元素组合可保存为模板组件、插入复用并在重载后保持 CPCL', async ({ page }) => {
  await openBlankTemplate(page)
  const template = {
    Version: 2,
    Width: 240,
    Height: 120,
    Rows: [{ Height: 120, Cells: [{ Width: 240, Elements: [
      { Type: 1, FontSize: 24, Field: { Name: 'Badge.Title' }, StartCoordinate: { X: 8, Y: 8 }, EndCoordinate: { X: 184, Y: 36 } },
      { Type: 1, FontSize: 16, Field: { Value: 'MODEL-X' }, StartCoordinate: { X: 8, Y: 46 }, EndCoordinate: { X: 184, Y: 68 } },
    ] }] }],
  }
  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'badge-source.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)),
  })
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  const data = { Badge: { Title: 'FRESH GOODS' } }
  const dataPreview = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify(data))
  await dataPreview

  await page.getByRole('button', { name: '组件与表格' }).click()
  const elements = page.locator('.tree-row.tree-element .tree-select')
  await elements.nth(0).click()
  await elements.nth(1).click({ modifiers: ['Control'] })
  const groupPreview = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await page.locator('.advanced-tools button').filter({ hasText: '组合元素' }).click()
  await groupPreview
  await page.getByRole('textbox', { name: '组件标识' }).fill('twoLineBadge')
  await page.getByRole('button', { name: '保存组件' }).click()
  await page.locator('.component-entry button[title="插入组件 twoLineBadge"]').click()

  const templateBytes = await exportTemplateJson(page)
  const saved = JSON.parse(templateBytes.toString('utf8'))
  expect(saved.Components).toHaveLength(1)
  expect(saved.Components[0].Elements.map((element: { Type: number }) => element.Type)).toEqual([1, 1])
  const references = saved.Rows[0].Cells[0].Elements.filter((element: { Type: number }) => element.Type === 6)
  expect(references).toHaveLength(2)
  expect(references.every((element: { ComponentId: string }) => element.ComponentId === 'twoLineBadge')).toBe(true)

  const previewResponse = await page.request.post('/api/designer/preview', { data: { template: saved, data } })
  expect(previewResponse.ok()).toBe(true)
  const preview = await previewResponse.json()
  const renderedText = preview.pages.flatMap((part: { elements: Array<{ text?: string }> }) => part.elements)
    .map((element: { text?: string }) => element.text).filter((value: string | undefined) => value !== undefined)
  expect(renderedText.filter((value: string) => value === 'FRESH GOODS')).toHaveLength(2)
  expect(renderedText.filter((value: string) => value === 'MODEL-X')).toHaveLength(2)

  const beforeReload = await exportCpclPages(page)
  expect(beforeReload).toHaveLength(1)
  await reloadTemplateJson(page, templateBytes, 'two-line-badge.json')
  expect(await exportCpclPages(page)).toEqual(beforeReload)
})

test('嵌套重复订单明细按外层订单和内层行项目渲染并往返 CPCL', async ({ page }) => {
  await openBlankTemplate(page)
  const template = {
    Version: 2,
    Width: 320,
    Height: 240,
    AutoHeight: true,
    Rows: [{ RepeatSource: 'Orders', Cells: [{ Width: 320, Rows: [
      { Height: 26, Cells: [{ Width: 320, Elements: [
        { Type: 1, FontSize: 16, Field: { Name: 'OrderNo' }, StartCoordinate: { X: 8, Y: 0 }, EndCoordinate: { X: 200, Y: 22 } },
      ] }] },
      { RepeatSource: 'Lines', Height: 24, Cells: [{ Width: 320, Elements: [
        { Type: 1, FontSize: 16, Field: { Name: 'Name' }, StartCoordinate: { X: 28, Y: 0 }, EndCoordinate: { X: 210, Y: 22 } },
        { Type: 1, FontSize: 16, Field: { Name: 'Quantity' }, StartCoordinate: { X: 230, Y: 0 }, EndCoordinate: { X: 310, Y: 22 } },
      ] }] },
    ] }] }],
  }
  const data = { Orders: [
    { OrderNo: 'ORDER-A', Lines: [{ Name: 'APPLE', Quantity: 2 }, { Name: 'BREAD', Quantity: 1 }] },
    { OrderNo: 'ORDER-B', Lines: [{ Name: 'COFFEE', Quantity: 3 }] },
  ] }
  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'nested-orders.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)),
  })
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  const updatePromise = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify(data))
  await updatePromise

  const templateBytes = await exportTemplateJson(page)
  const saved = JSON.parse(templateBytes.toString('utf8'))
  expect(saved.Rows[0].RepeatSource).toBe('Orders')
  expect(saved.Rows[0].Cells[0].Rows[1].RepeatSource).toBe('Lines')
  const previewResponse = await page.request.post('/api/designer/preview', { data: { template: saved, data } })
  expect(previewResponse.ok()).toBe(true)
  const preview = await previewResponse.json()
  const elements = preview.pages.flatMap((part: { elements: Array<{ sourcePath: string; text?: string; instanceIndices?: number[] }> }) => part.elements)
  const orders = elements.filter((element: { sourcePath: string }) => element.sourcePath === 'Rows[0].Cells[0].Rows[0].Cells[0].Elements[0]')
  expect(orders.map((element: { text?: string }) => element.text)).toEqual(['ORDER-A', 'ORDER-B'])
  expect(orders.map((element: { instanceIndices?: number[] }) => element.instanceIndices)).toEqual([[0], [1]])
  const details = elements.filter((element: { sourcePath: string }) => element.sourcePath === 'Rows[0].Cells[0].Rows[1].Cells[0].Elements[0]')
  expect(details.map((element: { text?: string }) => element.text)).toEqual(['APPLE', 'BREAD', 'COFFEE'])
  expect(details.map((element: { instanceIndices?: number[] }) => element.instanceIndices)).toEqual([[0, 0], [0, 1], [1, 0]])

  const beforeReload = await exportCpclPages(page)
  expect(beforeReload).toHaveLength(1)
  const outputResponse = await page.request.post('/api/designer/render', { data: { template: saved, data } })
  expect(outputResponse.ok()).toBe(true)
  const outputArchive = await JSZip.loadAsync(await outputResponse.body())
  const output = await outputArchive.file('page-001.cpcl')!.async('string')
  for (const value of ['ORDER-A', 'APPLE', 'BREAD', 'ORDER-B', 'COFFEE']) expect(output).toContain(value)
  await reloadTemplateJson(page, templateBytes, 'nested-orders-roundtrip.json')
  expect(await exportCpclPages(page)).toEqual(beforeReload)
})

test('条件编辑器创建 AND/OR 组合并让预览与 CPCL 跟随数据显隐', async ({ page }) => {
  await openBlankTemplate(page)
  const template = {
    Version: 2,
    Width: 180,
    Height: 80,
    Rows: [{ Height: 80, Cells: [{ Width: 180, Elements: [
      { Type: 1, FontSize: 24, Field: { Value: 'PROMOTION' }, StartCoordinate: { X: 8, Y: 8 }, EndCoordinate: { X: 168, Y: 36 } },
    ] }] }],
  }
  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'conditional-label.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)),
  })
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify({ Enabled: true, Stock: 1 }))
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  const conditionSection = page.locator('.condition-section').filter({ hasText: '条件显示' })
  await conditionSection.locator('input[type="checkbox"]').check()
  const editor = conditionSection.locator(':scope > .condition-editor')
  await editor.locator('select.condition-op').first().selectOption('and')
  await editor.getByRole('button', { name: '添加子条件' }).click()
  await editor.getByRole('button', { name: '添加子条件' }).click()
  const children = editor.locator('.condition-children > .condition-editor')
  await expect(children).toHaveCount(2)
  await children.nth(0).locator('input.condition-path').fill('Enabled')
  await children.nth(0).locator('select.condition-op').selectOption('eq')
  await children.nth(0).locator('input.condition-value').fill('true')
  await children.nth(1).locator('input.condition-path').fill('Stock')
  await children.nth(1).locator('select.condition-op').selectOption('gt')
  await children.nth(1).locator('input.condition-value').fill('2')

  const andData = { Enabled: true, Stock: 1 }
  const hiddenUpdate = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify(andData))
  const hiddenPreview = await (await hiddenUpdate).json()
  expect(hiddenPreview.pages.flatMap((part: { elements: Array<{ text?: string }> }) => part.elements)
    .some((element: { text?: string }) => element.text === 'PROMOTION')).toBe(false)
  const savedAndBytes = await exportTemplateJson(page)
  const savedAnd = JSON.parse(savedAndBytes.toString('utf8'))
  expect(savedAnd.Rows[0].Cells[0].Elements[0].Condition.Operator).toBe('and')

  const andVisibleData = { Enabled: true, Stock: 3 }
  const andVisibleResponse = await page.request.post('/api/designer/preview', { data: { template: savedAnd, data: andVisibleData } })
  expect(andVisibleResponse.ok()).toBe(true)
  expect((await andVisibleResponse.json()).pages.flatMap((part: { elements: Array<{ text?: string }> }) => part.elements)
    .some((element: { text?: string }) => element.text === 'PROMOTION')).toBe(true)
  const andRender = await page.request.post('/api/designer/render', { data: { template: savedAnd, data: andVisibleData } })
  expect(andRender.ok()).toBe(true)
  const andArchive = await JSZip.loadAsync(await andRender.body())
  expect(await andArchive.file('page-001.cpcl')!.async('string')).toContain('PROMOTION')

  await editor.locator('select.condition-op').first().selectOption('or')
  const savedOrBytes = await exportTemplateJson(page)
  const savedOr = JSON.parse(savedOrBytes.toString('utf8'))
  expect(savedOr.Rows[0].Cells[0].Elements[0].Condition.Operator).toBe('or')
  const orVisibleData = { Enabled: false, Stock: 3 }
  const orResponse = await page.request.post('/api/designer/preview', { data: { template: savedOr, data: orVisibleData } })
  expect(orResponse.ok()).toBe(true)
  expect((await orResponse.json()).pages.flatMap((part: { elements: Array<{ text?: string }> }) => part.elements)
    .some((element: { text?: string }) => element.text === 'PROMOTION')).toBe(true)
  const orHiddenResponse = await page.request.post('/api/designer/preview', { data: { template: savedOr, data: { Enabled: false, Stock: 1 } } })
  expect(orHiddenResponse.ok()).toBe(true)
  expect((await orHiddenResponse.json()).pages.flatMap((part: { elements: Array<{ text?: string }> }) => part.elements)
    .some((element: { text?: string }) => element.text === 'PROMOTION')).toBe(false)
})

test('单个长文字跨多页时内容完整且 JSON 重载后逐页 CPCL 一致', async ({ page }) => {
  await openBlankTemplate(page)
  const text = '0123456789'.repeat(24)
  const template = {
    Version: 2,
    Width: 180,
    Height: 80,
    Rows: [{ Height: 0, Cells: [{ Width: 180, Elements: [
      { Type: 1, FontSize: 24, Field: { Value: text, NeedFeedLine: true }, StartCoordinate: { X: 0, Y: 0 } },
    ] }] }],
  }
  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'long-text.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)),
  })
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const templateBytes = await exportTemplateJson(page)
  const saved = JSON.parse(templateBytes.toString('utf8'))
  const previewResponse = await page.request.post('/api/designer/preview', { data: { template: saved, data: {} } })
  expect(previewResponse.ok()).toBe(true)
  const preview = await previewResponse.json()
  expect(preview.pages.length).toBeGreaterThan(1)
  const fragments = preview.pages.flatMap((part: { elements: Array<{ sourcePath: string; text?: string }> }) => part.elements)
    .filter((element: { sourcePath: string; text?: string }) => element.sourcePath === 'Rows[0].Cells[0].Elements[0]' && element.text)
    .map((element: { text?: string }) => element.text)
  expect(fragments.length).toBeGreaterThan(1)
  expect(fragments.join('')).toBe(text)

  const beforeReload = await exportCpclPages(page)
  expect(beforeReload.length).toBe(preview.pages.length)
  await reloadTemplateJson(page, templateBytes, 'long-text-roundtrip.json')
  expect(await exportCpclPages(page)).toEqual(beforeReload)
})

test('自动排列文字修改 X 坐标后保留预览中的 Y 坐标', async ({ page }) => {
  await openBlankTemplate(page)
  const template = {
    Version: 2,
    Width: 180,
    Height: 100,
    Rows: [{ Height: 100, Cells: [{ Width: 180, Elements: [
      { Type: 1, FontSize: 24, Field: { Value: 'FIRST' } },
      { Type: 1, FontSize: 24, Field: { Value: 'SECOND' } },
    ] }] }],
  }
  const uploadPromise = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'auto-position.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)),
  })
  await uploadPromise
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const originalResponse = await page.request.post('/api/designer/preview', { data: { template, data: {} } })
  expect(originalResponse.ok()).toBe(true)
  const original = await originalResponse.json()
  const target = original.pages.flatMap((part: { elements: Array<{ sourcePath: string; text?: string; x: number; y: number }> }) => part.elements)
    .find((element: { sourcePath: string }) => element.sourcePath === 'Rows[0].Cells[0].Elements[1]')
  expect(target).toBeTruthy()
  expect(target.y).toBe(24)

  await page.locator('.tree-row.tree-element .tree-select').nth(1).click()
  const position = dimensions(page)
  const previewPromise = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
  await position.nth(0).fill('10')
  await position.nth(0).press('Tab')
  await previewPromise
  await expect(position.nth(0)).toHaveValue('10')
  await expect(position.nth(1)).toHaveValue('24')
  const saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(saved.Rows[0].Cells[0].Elements[1].StartCoordinate).toEqual({ X: 10, Y: 24 })
})

test('自动定位与显式定位的组合拆开后保留子元素坐标和后续文字流位置', async ({ page }) => {
  const scenarios = [
    { name: 'auto', groupCoordinate: undefined, before: true },
    { name: 'explicit', groupCoordinate: { X: 12, Y: 40 }, before: false },
  ] as const

  for (const scenario of scenarios) {
    await openBlankTemplate(page)
    const elements = [
      ...(scenario.before ? [{ Type: 1, FontSize: 24, Field: { Value: 'LEAD' }, StartCoordinate: { X: 8, Y: 0 }, EndCoordinate: { X: 160, Y: 24 } }] : []),
      {
        Type: 6,
        Width: 160,
        ...(scenario.groupCoordinate ? { StartCoordinate: scenario.groupCoordinate } : {}),
        Elements: [
          { Type: 1, FontSize: 24, Field: { Value: 'CHILD-ONE' } },
          { Type: 1, FontSize: 24, Field: { Value: 'CHILD-TWO' } },
        ],
      },
      { Type: 1, FontSize: 24, Field: { Value: 'TRAILING' } },
    ]
    const template = { Version: 2, Width: 180, Height: 160, Rows: [{ Height: 160, Cells: [{ Width: 180, Elements: elements }] }] }
    await page.locator('input[type="file"]').first().setInputFiles({
      name: `ungroup-${scenario.name}.json`, mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)),
    })
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
    const beforeResponse = await page.request.post('/api/designer/preview', { data: { template, data: {} } })
    expect(beforeResponse.ok()).toBe(true)
    const before = await beforeResponse.json()
    const positions = (layout: typeof before) => layout.pages.flatMap((part: { elements: Array<{ text?: string; x: number; y: number; width: number; height: number }> }) => part.elements)
      .filter((element: { text?: string }) => ['LEAD', 'CHILD-ONE', 'CHILD-TWO', 'TRAILING'].includes(element.text ?? ''))
      .map((element: { text?: string; x: number; y: number; width: number; height: number }) => ({ text: element.text, x: element.x, y: element.y, width: element.width, height: element.height }))
    const beforePositions = positions(before)
    expect(beforePositions.map((item: { text?: string }) => item.text)).toEqual([
      ...(scenario.before ? ['LEAD'] : []), 'CHILD-ONE', 'CHILD-TWO', 'TRAILING',
    ])
    const originalCpcl = await exportFirstCpclPage(page)

    const groupIndex = scenario.before ? 1 : 0
    await page.locator('.tree-row.tree-element .tree-select').nth(groupIndex).click()
    await page.getByRole('button', { name: '组件与表格' }).click()
    const ungroupResponse = page.waitForResponse((response) => response.url().includes('/api/designer/preview'))
    await page.locator('.advanced-tools button').filter({ hasText: '拆开组合' }).click()
    await ungroupResponse
    const savedBytes = await exportTemplateJson(page)
    const saved = JSON.parse(savedBytes.toString('utf8'))
    expect(saved.Rows[0].Cells[0].Elements.some((element: { Type: number }) => element.Type === 6)).toBe(false)
    const afterResponse = await page.request.post('/api/designer/preview', { data: { template: saved, data: {} } })
    expect(afterResponse.ok()).toBe(true)
    const after = await afterResponse.json()
    expect(positions(after)).toEqual(beforePositions)
    expect(await exportFirstCpclPage(page)).toEqual(originalCpcl)
  }
})

test('能力接口在没有模板和数据时返回默认协议能力快照', async ({ request }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const response = await request.post(`${apiUrl}/api/designer/capabilities`, { data: {} })
  expect(response.status()).toBe(200)
  const capabilities = await response.json()

  expect(capabilities).toMatchObject({ protocol: 'Cpcl', escPosLayoutMode: null, dpi: 200 })
  expect(capabilities.elementTypes).toEqual(expect.arrayContaining([1, 3, 4, 6]))
  expect(capabilities.fontKinds).toEqual(expect.arrayContaining([0, 1]))
  expect(capabilities.textSizes).toEqual(expect.arrayContaining([24]))
  expect(capabilities.barcodeRotations).toEqual([0, 270])
  expect(capabilities.qrModuleSize).toMatchObject({ minimum: 1, maximum: 32 })
  expect(capabilities.deviceFonts.find((font: { name: string }) => font.name === '24')).toMatchObject({
    name: '24', size: { minimum: 1, maximum: null }, sizes: expect.arrayContaining([24]),
  })
})

test('能力接口区分禁用原生码图的 ESC/POS Native 与 Raster', async ({ request }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const profile = { supportsNativeBarcode: false, supportsNativeQrCode: false }
  const nativeResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: { protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Ascii', escPosDevice: { profile } },
  })
  expect(nativeResponse.status()).toBe(200)
  const native = await nativeResponse.json()
  expect(native.protocol).toBe('EscPos')
  expect(native.escPosLayoutMode).toBe('Native')
  expect(native.elementTypes).not.toEqual(expect.arrayContaining([3, 4]))
  expect(native.barcodeTypes).toEqual([])
  expect(native.qrModuleSize).toBeNull()

  const rasterResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: { protocol: 'EscPos', escPosLayoutMode: 'Raster', encoding: 'Ascii', escPosDevice: { profile } },
  })
  expect(rasterResponse.status()).toBe(200)
  const raster = await rasterResponse.json()
  expect(raster.escPosLayoutMode).toBe('Raster')
  expect(raster.elementTypes).toEqual(expect.arrayContaining([3, 4]))
  expect(raster.barcodeTypes).toEqual(expect.arrayContaining([0]))
  expect(raster.qrModuleSize).toMatchObject({ minimum: 1, maximum: 32 })
})

test('TSPL Designer exposes PLESSEY/ITF-14/EAN-14 and keeps fixed ratios valid', async ({ page }) => {
  const template = {
    Version: 2,
    Width: 600,
    Height: 100,
    Rows: [{ Height: 100, Cells: [{ Width: 600, Elements: [{
      Type: 4, BarcodeType: 18, LineWidth: 2, Height: 40, Field: { Value: '1234567890123' },
    }] }] }],
  }
  await importDesignerProject(page, designerProject(template, 'Tspl'), 'tspl-special-barcodes.json')
  await page.locator('.tree-row.tree-element .tree-select').first().click()

  const barcodeType = page.getByLabel('制式')
  await expect(barcodeType.locator('option[value="17"]')).toBeEnabled()
  await expect(barcodeType.locator('option[value="18"]')).toBeEnabled()
  await expect(barcodeType.locator('option[value="19"]')).toBeEnabled()

  await barcodeType.selectOption('17')
  let saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(saved.Rows[0].Cells[0].Elements[0]).toMatchObject({ BarcodeType: 17, WideModuleWidth: 6 })

  await barcodeType.selectOption('19')
  saved = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(saved.Rows[0].Cells[0].Elements[0]).toMatchObject({ BarcodeType: 19, WideModuleWidth: 3 })
})

test('TSPL Designer exposes TELEPEN/TELEPENN/CODE49 and exports native BARCODE rows', async ({ page }) => {
  test.setTimeout(90_000)
  const template = {
    Version: 1,
    Width: 2000,
    Height: 200,
    Rows: [{ Height: 200, Cells: [{ Width: 2000, Elements: [{
      Type: 4,
      BarcodeType: 22,
      LineWidth: 1,
      NarrowModuleWidth: 2,
      WideModuleWidth: 4,
      Width: 1800,
      Height: 40,
      HumanReadable: false,
      Field: { Value: '12345' },
    }] }] }],
  }
  await importDesignerProject(page, designerProject(template, 'Tspl'), 'tspl-variable-barcodes.json')
  await page.locator('.tree-row.tree-element .tree-select').first().click()

  const barcodeType = page.getByLabel('制式')
  for (const value of ['22', '23', '24']) {
    await expect(barcodeType.locator(`option[value="${value}"]`)).toBeEnabled()
  }
  await expect(page.getByLabel('显式窄条')).toHaveValue('2')
  await expect(page.getByLabel('宽条模块')).toHaveValue('4')

  const content = page.getByLabel('显示内容')
  const cases = [
    { value: '22', type: 'TELEPEN', data: '12345' },
    { value: '23', type: 'TELEPENN', data: '12345' },
    { value: '24', type: 'CODE49', data: 'ABC123' },
  ]

  for (const item of cases) {
    const previewResponse = page.waitForResponse((response) =>
      response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
    await barcodeType.selectOption(item.value)
    await content.fill(item.data)
    await content.press('Tab')
    await previewResponse
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

    const downloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: '生成 TSPL' }).click()
    const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
    const entry = archive.file('page-001.tspl')
    expect(entry).not.toBeNull()
    const tspl = await entry!.async('string')
    expect(tspl).toContain(`BARCODE 20,0,"${item.type}",40,0,0,2,4,"${item.data}"`)
  }
})

test('ZPL Designer exposes GS1 DataBar types and exports ^BR bytes', async ({ page }) => {
  test.setTimeout(90_000)
  const template = {
    Version: 1,
    Width: 800,
    Height: 140,
    Rows: [{ Height: 140, Cells: [{ Width: 800, Elements: [{
      Type: 4,
      BarcodeType: 27,
      LineWidth: 2,
      NarrowModuleWidth: 2,
      Width: 760,
      Height: 40,
      HumanReadable: false,
      Field: { Value: '0123456789012' },
    }] }] }],
  }
  await importDesignerProject(page, designerProject(template, 'Zpl'), 'zpl-databar.json')
  await page.locator('.tree-row.tree-element .tree-select').first().click()

  const barcodeType = page.getByLabel('制式')
  for (const value of ['25', '26', '27', '28']) {
    await expect(barcodeType.locator(`option[value="${value}"]`)).toBeEnabled()
  }

  const content = page.getByLabel('显示内容')
  const cases = [
    { value: '25', data: '1234567890123', symbology: 1 },
    { value: '26', data: '1234567890123', symbology: 2 },
    { value: '27', data: '0123456789012', symbology: 5 },
    { value: '28', data: '12{', symbology: 6 },
  ]

  for (const item of cases) {
    const previewResponse = page.waitForResponse((response) =>
      response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
    await barcodeType.selectOption(item.value)
    await content.fill(item.data)
    await content.press('Tab')
    await previewResponse
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

    const downloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: '生成 ZPL' }).click()
    const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
    const entry = archive.file('page-001.zpl')
    expect(entry).not.toBeNull()
    const zpl = await entry!.async('string')
    expect(zpl).toContain(`^BRN,${item.symbology},2,1,40`)
    expect(zpl).toContain(`^FD${item.data}^FS`)
  }
})

test('ESC/POS Native Designer exposes GS1 DataBar types and exports Function B bytes', async ({ page }) => {
  test.setTimeout(90_000)
  const template = {
    Version: 1,
    Width: 800,
    Height: 140,
    Rows: [{ Height: 140, Cells: [{ Width: 800, Elements: [{
      Type: 4,
      BarcodeType: 27,
      LineWidth: 1,
      NarrowModuleWidth: 2,
      Width: 760,
      Height: 40,
      HumanReadable: false,
      Field: { Value: '0123456789012' },
    }] }] }],
  }
  const project = {
    format: 'Bing.EasyPrint.DesignerProject',
    version: 1,
    template,
    outputSettings: {
      outputTarget: 'EscPosNative',
      allowRasterizedLines: false,
      imageThreshold: 128,
      encoding: 'Ascii',
      deviceFontCalibrations: { Tspl: {}, Zpl: {} },
      requireDeclaredNativeBounds: false,
      escPosDevice: {},
      tsplMedia: {},
      zplMedia: {},
    },
  }
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const importPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.locator('[data-testid="template-import"]').setInputFiles({
    name: 'escpos-native-databar.json',
    mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(project)),
  })
  await importPreview
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('EscPosNative')
  await page.locator('.tree-row.tree-element .tree-select').first().click()

  const barcodeType = page.getByLabel('制式')
  for (const value of ['25', '26', '27', '28']) {
    await expect(barcodeType.locator(`option[value="${value}"]`)).toBeEnabled()
  }

  const content = page.getByLabel('显示内容')
  const cases = [
    { value: '25', data: '1234567890123', symbology: 75 },
    { value: '26', data: '1234567890123', symbology: 76 },
    { value: '27', data: '0123456789012', symbology: 77 },
    { value: '28', data: '12{', symbology: 78 },
  ]

  const containsSequence = (bytes: Uint8Array, sequence: number[]) =>
    Array.from(bytes).some((_, index) => sequence.every((value, offset) => bytes[index + offset] === value))

  for (const item of cases) {
    const previewResponse = page.waitForResponse((response) =>
      response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
    await barcodeType.selectOption(item.value)
    await content.fill(item.data)
    await content.press('Tab')
    await previewResponse
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

    const downloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: '生成 ESC/POS' }).click()
    const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
    const entry = archive.file('page-001.escpos')
    expect(entry).not.toBeNull()
    const bytes = await entry!.async('uint8array')
    expect(containsSequence(bytes, [
      0x1D, 0x6B, item.symbology, item.data.length,
      ...Array.from(Buffer.from(item.data, 'ascii')),
    ])).toBe(true)
  }
})

test('能力接口拒绝未知协议和不匹配的机型 profile', async ({ request }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const unknownProtocol = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: { protocol: 'UnknownProtocol' },
  })
  expect(unknownProtocol.status()).toBe(400)
  expect((await unknownProtocol.json()).errors).toHaveLength(1)

  const mismatchedProfile = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: { protocol: 'Cpcl', escPosDevice: { profile: { supportsNativeBarcode: false } } },
  })
  expect(mismatchedProfile.status()).toBe(400)
  expect((await mismatchedProfile.json()).errors).toHaveLength(1)
})

test('ESC/POS Native 能力过滤工具并保留不支持值直到用户修正', async ({ page }) => {
  await page.goto('/')
  const template = {
    Version: 1,
    Width: 576,
    Height: 360,
    Rows: [{ Height: 360, Cells: [{ Width: 576, Elements: [
      { Type: 1, FontSize: 24, Rotation: 90, Font: { Kind: 2, Name: 'Microsoft YaHei' }, Field: { Value: 'HOST' } },
      { Type: 2, Width: 32, Height: 32, Field: { Value: 'missing-resource' } },
      { Type: 5, LineWidth: 1, StartCoordinate: { X: 8, Y: 8 }, LineEndCoordinate: { X: 120, Y: 8 } },
      { Type: 4, BarcodeType: 1, LineWidth: 1, Height: 48, Width: 200, Field: { Value: 'ABC' } },
    ] }] }],
  }
  const project = {
    format: 'Bing.EasyPrint.DesignerProject',
    version: 1,
    template,
    outputSettings: {
      outputTarget: 'Cpcl', allowRasterizedLines: false, imageThreshold: 128,
      deviceFontCalibrations: { Tspl: {}, Zpl: {} }, requireDeclaredNativeBounds: false,
      escPosDevice: {}, tsplMedia: {}, zplMedia: {},
    },
  }
  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'native-capability-values.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(project)),
  })

  const protocol = page.getByRole('combobox', { name: '输出协议' })
  await expect(protocol).toHaveValue('Cpcl')
  await protocol.selectOption('EscPosNative')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)

  const tools = page.locator('.element-tools button')
  await expect(tools.filter({ hasText: '图片' })).toBeDisabled()
  await expect(tools.filter({ hasText: '线段' })).toBeDisabled()

  const entries = page.locator('.tree-row.tree-element .tree-select')
  await expect(entries).toHaveCount(4)
  await entries.nth(0).click()
  const fontSource = page.getByLabel('字体来源')
  const fontRotation = page.getByLabel('元素旋转')
  await expect(fontSource).toHaveValue('host')
  await expect(fontSource.locator('option[value="host"]')).toHaveAttribute('disabled', '')
  await expect(page.getByLabel('主机字体名称')).toHaveValue('Microsoft YaHei')
  await expect(page.getByLabel('主机字体名称')).toBeDisabled()
  await expect(fontRotation).toHaveValue('90')
  await expect(fontRotation.locator('option[value="90"]')).toHaveAttribute('disabled', '')

  await entries.nth(3).click()
  const barcodeType = page.getByLabel('制式')
  await expect(barcodeType).toHaveValue('1')
  await expect(barcodeType.locator('option[value="1"]')).toHaveAttribute('disabled', '')

  const retained = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(retained.Rows[0].Cells[0].Elements[0]).toMatchObject({ Rotation: 90, Font: { Kind: 2, Name: 'Microsoft YaHei' } })
  expect(retained.Rows[0].Cells[0].Elements[3]).toMatchObject({ BarcodeType: 1 })

  await entries.nth(0).click()
  await fontSource.selectOption('default')
  await fontRotation.selectOption('0')
  await entries.nth(3).click()
  await barcodeType.selectOption('0')
  const corrected = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(corrected.Rows[0].Cells[0].Elements[0]).toMatchObject({ Rotation: 0, Font: { Kind: 0 } })
  expect(corrected.Rows[0].Cells[0].Elements[3]).toMatchObject({ BarcodeType: 0 })
})

test('ESC/POS Native 机型关闭原生命令后切 Raster 恢复码图工具', async ({ page }) => {
  await openBlankTemplate(page)
  const protocol = page.getByRole('combobox', { name: '输出协议' })
  await protocol.selectOption('EscPosNative')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.locator('.output-settings-button').click()
  await page.getByRole('button', { name: '新建设备配置' }).click()

  const nativeBarcode = page.getByLabel('ESC/POS 支持原生条码')
  const nativeQr = page.getByLabel('ESC/POS 支持原生二维码')
  await expect(nativeBarcode).toBeChecked()
  await expect(nativeQr).toBeChecked()
  await nativeBarcode.uncheck()
  await nativeQr.uncheck()
  await expect(nativeBarcode).not.toBeChecked()
  await expect(nativeQr).not.toBeChecked()
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)

  const tools = page.locator('.element-tools button')
  await expect(tools.filter({ hasText: '二维码' })).toBeDisabled()
  await expect(tools.filter({ hasText: '一维条码' })).toBeDisabled()

  await protocol.selectOption('EscPosRaster')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await expect(tools.filter({ hasText: '二维码' })).toBeEnabled()
  await expect(tools.filter({ hasText: '一维条码' })).toBeEnabled()
})

test('Host Native 单条码与单二维码预览导出保持原生命令链路', async ({ request }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const templateFor = (element: object) => ({
    Version: 1,
    Width: 576,
    Height: 360,
    Rows: [{ Height: 360, Cells: [{ Width: 576, Elements: [element] }] }],
  })
  const cases = [
    {
      type: 4,
      template: templateFor({ Type: 4, BarcodeType: 0, LineWidth: 1, Height: 48, Width: 500, Field: { Value: '123456' } }),
      marker: [0x1D, 0x6B],
    },
    {
      type: 3,
      template: templateFor({ Type: 3, UnitWidth: 4, Field: { Value: 'Q' } }),
      marker: [0x1D, 0x28, 0x6B],
    },
  ]

  for (const item of cases) {
    const payload = {
      template: item.template,
      data: {},
      encoding: 'Ascii',
      protocol: 'EscPos',
      escPosLayoutMode: 'Native',
    }
    const preview = await request.post(`${apiUrl}/api/designer/preview`, { data: payload })
    expect(preview.status()).toBe(200)
    const previewBody = await preview.json()
    expect(previewBody.pages[0].elements.some((element: { type: number; x: number; y: number }) =>
      element.type === item.type && element.x === 0 && element.y === 0)).toBe(true)

    const render = await request.post(`${apiUrl}/api/designer/render`, { data: payload })
    expect(render.status()).toBe(200)
    const archive = await JSZip.loadAsync(await render.body())
    const entry = archive.file('page-001.escpos')
    expect(entry).not.toBeNull()
    const bytes = await entry!.async('uint8array')
    const contains = (marker: number[]) => bytes.some((_, index) =>
      marker.every((value, offset) => bytes[index + offset] === value))
    expect(contains(item.marker)).toBe(true)
  }
})

test('TSPL 重复共享组件诊断保留第 5 项和第 3 页并贯穿导出', async ({ page, request }) => {
  test.setTimeout(90_000)
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const payload = {
    template: repeatedTsplDiagnosticTemplate,
    data: repeatedTsplDiagnosticData,
    encoding: 'Ascii',
    protocol: 'Tspl',
  }

  const previewResponse = await request.post(`${apiUrl}/api/designer/preview`, { data: payload })
  expect(previewResponse.status()).toBe(400)
  const previewError = await previewResponse.json()
  expect(previewError.errors).toHaveLength(1)
  expect(previewError.errors[0]).toMatchObject({
    sourcePath: 'Rows[0].Cells[0].Elements[0].Elements[0]',
    definitionPath: 'Components[0].Elements[0]',
    componentInstancePath: 'Rows[0].Cells[0].Elements[0]',
    instanceIndices: [4],
    pageIndex: 2,
    elementIndex: 0,
  })

  const renderResponse = await request.post(`${apiUrl}/api/designer/render`, { data: payload })
  expect(renderResponse.status()).toBe(400)
  const renderError = await renderResponse.json()
  expect(renderError.errors).toEqual(previewError.errors)

  await importDesignerProject(page, designerProject(repeatedTsplDiagnosticTemplate), 'tspl-repeat-diagnostic.json')
  await setDesignerData(page, repeatedTsplDiagnosticData)
  const article = page.locator('.diagnostic-panel article').first()
  await expect(article).toContainText('Components[0].Elements[0]')
  await expect(article).toContainText('Rows[0].Cells[0].Elements[0]')
  await expect(article).toContainText(/第\s*5\s*项/)
  await expect(article).toContainText(/第\s*3\s*页/)

  const exportResponsePromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/render') && response.request().method() === 'POST')
  const exportButton = page.getByRole('button', { name: '生成 TSPL' })
  await expect(exportButton).toBeEnabled()
  await exportButton.click()
  const failedExport = await exportResponsePromise
  expect(failedExport.status()).toBe(400)
  await expect(article).toContainText('Components[0].Elements[0]')
  await expect(article).toContainText('Rows[0].Cells[0].Elements[0]')
  await expect(article).toContainText(/第\s*5\s*项/)
  await expect(article).toContainText(/第\s*3\s*页/)
})

test('诊断定位节点、组件定义和组件实例后仍能编辑对应共享模板节点', async ({ page }) => {
  test.setTimeout(90_000)
  await importDesignerProject(page, designerProject(repeatedTsplDiagnosticTemplate), 'tspl-repeat-diagnostic-actions.json')
  await setDesignerData(page, repeatedTsplDiagnosticData)

  const article = page.locator('.diagnostic-panel article').first()
  const locateNode = article.getByRole('button', { name: '定位节点' })
  const locateDefinition = article.getByRole('button', { name: '定位组件定义' })
  const locateInstance = article.getByRole('button', { name: '定位组件实例' })
  await expect(locateNode).toBeEnabled()
  await expect(locateDefinition).toBeEnabled()
  await expect(locateInstance).toBeEnabled()

  await locateNode.click()
  await expect(page.locator('.inspector-selection small')).toContainText('Components[0].Elements[0]')
  const fieldPath = page.getByLabel('字段路径', { exact: true })
  await expect(fieldPath).toHaveValue('Name')
  await fieldPath.fill('Label')
  const afterNodeEdit = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(afterNodeEdit.Components[0].Elements[0].Field.Name).toBe('Label')

  await locateDefinition.click()
  await expect(page.locator('.inspector-selection small')).toContainText('Components[0].Elements[0]')
  await expect(page.getByLabel('字段路径', { exact: true })).toHaveValue('Label')

  await locateInstance.click()
  await expect(page.locator('.inspector-selection small')).toContainText('Rows[0].Cells[0].Elements[0]')
  const positionMode = page.getByLabel('位置模式')
  await expect(positionMode).toHaveValue('flow')
  await positionMode.selectOption('fixed')
  const afterInstanceEdit = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(afterInstanceEdit.Rows[0].Cells[0].Elements[0].StartCoordinate).toEqual({ X: 0, Y: 0 })
})

test('静态未展开组件能力诊断只提供定义路径而不猜测组件实例', async ({ page, request }) => {
  test.setTimeout(90_000)
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const template = {
    Version: 3,
    Width: 160,
    Height: 48,
    Components: [{
      Id: 'label',
      Width: 160,
      Elements: [{ Type: 1, FontSize: 24, Bold: true, Field: { Value: 'Bold' } }],
    }],
    Rows: [{ Height: 24, Cells: [{ Width: 160, Elements: [{ Type: 6, ComponentId: 'label' }] }] }],
  }
  const payload = { template, data: {}, encoding: 'Ascii', protocol: 'Tspl' }
  const response = await request.post(`${apiUrl}/api/designer/preview`, { data: payload })
  expect(response.status()).toBe(400)
  const error = await response.json()
  expect(error.errors).toHaveLength(1)
  expect(error.errors[0]).toMatchObject({
    sourcePath: 'Components[0].Elements[0]',
    definitionPath: 'Components[0].Elements[0]',
    componentInstancePath: null,
    instanceIndices: null,
    pageIndex: null,
    elementIndex: null,
  })

  await importDesignerProject(page, designerProject(template), 'tspl-static-component-diagnostic.json')
  const article = page.locator('.diagnostic-panel article').first()
  await expect(article).toContainText('Components[0].Elements[0]')
  await expect(article).not.toContainText('Rows[0].Cells[0].Elements[0]')
  await expect(article.getByRole('button', { name: '定位节点' })).toBeEnabled()
  await expect(article.getByRole('button', { name: '定位组件定义' })).toBeEnabled()
  await expect(article.getByRole('button', { name: '定位组件实例' })).toHaveCount(0)

  await article.getByRole('button', { name: '定位组件定义' }).click()
  await expect(page.locator('.inspector-selection small')).toContainText('Components[0].Elements[0]')
  await expect(page.getByLabel('显示内容', { exact: true })).toHaveValue('Bold')
})

test('静区能力接口按 Native 与 Raster 区分并保留错误元素路径', async ({ request }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const nativeCapabilities = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: { protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Ascii' },
  })
  expect(nativeCapabilities.status()).toBe(200)
  expect((await nativeCapabilities.json()).supportsExplicitQuietZone).toBe(false)

  const rasterCapabilities = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: { protocol: 'EscPos', escPosLayoutMode: 'Raster', encoding: 'Ascii' },
  })
  expect(rasterCapabilities.status()).toBe(200)
  expect((await rasterCapabilities.json()).supportsExplicitQuietZone).toBe(true)

  const template = {
    Version: 3,
    Width: 260,
    Height: 120,
    Rows: [{ Height: 120, Cells: [{ Width: 260, Elements: [{
      Type: 4, BarcodeType: 0, LineWidth: 1, Height: 24, Width: 220,
      HumanReadable: false, Field: { Value: '1234' },
      QuietZone: { Left: 2, Top: 3, Right: 4, Bottom: 5 },
    }] }] }],
  }
  const nativePreview = await request.post(`${apiUrl}/api/designer/preview`, {
    data: { template, data: {}, encoding: 'Ascii', protocol: 'EscPos', escPosLayoutMode: 'Native' },
  })
  expect(nativePreview.status()).toBe(400)
  const nativeError = await nativePreview.json()
  expect(JSON.stringify(nativeError.errors)).toContain('Rows[0].Cells[0].Elements[0]')
  expect(JSON.stringify(nativeError.errors)).toContain('静区')

  const rasterPreview = await request.post(`${apiUrl}/api/designer/preview`, {
    data: { template, data: {}, encoding: 'Ascii', protocol: 'EscPos', escPosLayoutMode: 'Raster' },
  })
  expect(rasterPreview.status()).toBe(200)

  const invalidTemplate = {
    ...template,
    Rows: [{ Height: 120, Cells: [{ Width: 260, Elements: [{
      Type: 4, BarcodeType: 0, LineWidth: 1, Height: 24, Width: 180,
      HumanReadable: false, Field: { Value: '1234' }, QuietZone: { Left: -1 },
    }] }] }],
  }
  const invalid = await request.post(`${apiUrl}/api/designer/preview`, {
    data: { template: invalidTemplate, data: {}, encoding: 'Ascii', protocol: 'Cpcl' },
  })
  expect(invalid.status()).toBe(400)
  const invalidError = await invalid.json()
  expect(invalidError.errors).toHaveLength(1)
  expect(invalidError.errors[0]).toMatchObject({
    sourcePath: 'Rows[0].Cells[0].Elements[0].QuietZone.Left',
    pageIndex: null,
    elementIndex: null,
  })

  const invalidFontTemplate = {
    ...template,
    Rows: [{ Height: 120, Cells: [{ Width: 260, Elements: [{
      Type: 1, FontSize: 24, Field: { Value: 'bad' }, QuietZone: { Left: -1 },
    }] }] }],
  }
  const invalidFont = await request.post(`${apiUrl}/api/designer/preview`, {
    data: { template: invalidFontTemplate, data: {}, encoding: 'Ascii', protocol: 'Cpcl' },
  })
  expect(invalidFont.status()).toBe(400)
  const invalidFontError = await invalidFont.json()
  expect(invalidFontError.errors).toHaveLength(1)
  expect(invalidFontError.errors[0]).toMatchObject({
    sourcePath: 'Rows[0].Cells[0].Elements[0].QuietZone',
    pageIndex: null,
    elementIndex: null,
  })
})

test('静区四边设置可预览、保存和导出，Native 保留值并可恢复协议默认', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const template = {
    Version: 3,
    Width: 576,
    Height: 360,
    Rows: [{ Height: 360, Cells: [{ Width: 576, Elements: [{
      Type: 4, BarcodeType: 0, LineWidth: 1, Height: 24, Width: 220,
      HumanReadable: false, Field: { Value: '1234' },
      StartCoordinate: { X: 24, Y: 40 }, EndCoordinate: { X: 244, Y: 64 },
    }] }] }],
  }
  await reloadTemplateJson(page, Buffer.from(JSON.stringify(template)), 'quiet-zone-ui.json')
  await page.locator('.tree-row.tree-element .tree-select').first().click()

  const quiet = page.locator('.inspector-section').filter({ hasText: '静区' }).last()
  const mode = quiet.getByLabel('静区配置方式')
  await expect(mode).toHaveValue('default')
  await mode.selectOption('explicit')
  const sides = [
    ['静区左侧点数', '2'], ['静区上侧点数', '3'],
    ['静区右侧点数', '4'], ['静区下侧点数', '5'],
  ] as const
  for (const [label, value] of sides) {
    const previewPromise = page.waitForResponse((response) =>
      response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
    const input = quiet.getByLabel(label)
    await input.fill(value)
    await input.press('Tab')
    await previewPromise
    await expect(input).toHaveValue(value)
  }

  const exported = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(exported.Rows[0].Cells[0].Elements[0].QuietZone).toEqual({ Left: 2, Top: 3, Right: 4, Bottom: 5 })

  const savePromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const saved = JSON.parse((await readFile(await (await savePromise).path())).toString('utf8'))
  expect(saved.template.Rows[0].Cells[0].Elements[0].QuietZone).toEqual({ Left: 2, Top: 3, Right: 4, Bottom: 5 })

  const protocol = page.getByRole('combobox', { name: '输出协议' })
  await protocol.selectOption('EscPosNative')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await expect(mode).toBeDisabled()
  for (const [label, value] of sides) await expect(quiet.getByLabel(label)).toHaveValue(value)
  await expect(quiet.getByRole('button', { name: '恢复协议默认静区' })).toBeEnabled()

  await protocol.selectOption('EscPosRaster')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await expect(mode).toBeEnabled()
  for (const [label, value] of sides) await expect(quiet.getByLabel(label)).toHaveValue(value)

  await protocol.selectOption('EscPosNative')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  const clearPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await quiet.getByRole('button', { name: '恢复协议默认静区' }).click()
  await clearPreview
  await expect(mode).toHaveValue('default')
  await expect(quiet.getByRole('button', { name: '恢复协议默认静区' })).toHaveCount(0)
  const cleared = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  expect(cleared.Rows[0].Cells[0].Elements[0].QuietZone).toBeUndefined()
})

test('自动条码和二维码静区从默认到显式再清除保持预览坐标契约', async ({ page, request }) => {
  await openBlankTemplate(page)
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('Cpcl')
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const autoTemplate = {
    Version: 3,
    Width: 576,
    Height: 220,
    Rows: [
      { Height: 80, Cells: [{ Width: 576, Elements: [{
        Type: 4, BarcodeType: 0, LineWidth: 1, Height: 24, Width: 0,
        HumanReadable: false, Field: { Value: '1234' },
      }] }] },
      { Height: 120, Cells: [{ Width: 576, Elements: [{
        Type: 3, UnitWidth: 4, ErrorLevel: 'M', Width: 0, Height: 0,
        Field: { Value: 'QR' },
      }] }] },
    ],
  }
  const autoPreviewResponse = await request.post(`${apiUrl}/api/designer/preview`, {
    data: { template: autoTemplate, data: {}, encoding: 'Ascii', protocol: 'Cpcl' },
  })
  expect(autoPreviewResponse.ok()).toBe(true)
  const autoPreview = await autoPreviewResponse.json() as {
    pages: Array<{ elements: Array<{ sourcePath: string; type: number; width: number; height: number }> }>
  }
  const autoElements = autoPreview.pages.flatMap((part) => part.elements)
  const barcodeLayout = autoElements.find((element) => element.sourcePath === 'Rows[0].Cells[0].Elements[0]')
  const qrLayout = autoElements.find((element) => element.sourcePath === 'Rows[1].Cells[0].Elements[0]')
  expect(barcodeLayout).toMatchObject({ type: 4, width: expect.any(Number), height: 24 })
  expect(qrLayout).toMatchObject({ type: 3, width: expect.any(Number), height: expect.any(Number) })
  expect(barcodeLayout).toBeDefined()
  expect(qrLayout).toBeDefined()

  const template = {
    ...autoTemplate,
    Rows: [
      { Height: 80, Cells: [{ Width: 576, Elements: [{
        Type: 4, BarcodeType: 0, LineWidth: 1, Height: 24, Width: 0,
        HumanReadable: false, Field: { Value: '1234' },
        StartCoordinate: { X: 0, Y: 0 },
        EndCoordinate: { X: barcodeLayout!.width, Y: barcodeLayout!.height },
      }] }] },
      { Height: 120, Cells: [{ Width: 576, Elements: [{
        Type: 3, UnitWidth: 4, ErrorLevel: 'M', Width: 0, Height: 0,
        Field: { Value: 'QR' },
        StartCoordinate: { X: 0, Y: 0 },
        EndCoordinate: { X: qrLayout!.width, Y: qrLayout!.height },
      }] }] },
    ],
  }
  const boundedPreviewResponse = await request.post(`${apiUrl}/api/designer/preview`, {
    data: { template, data: {}, encoding: 'Ascii', protocol: 'Cpcl' },
  })
  expect(boundedPreviewResponse.ok()).toBe(true)
  const boundedPreview = await boundedPreviewResponse.json() as {
    pages: Array<{ elements: Array<{ sourcePath: string; width: number; height: number }> }>
  }
  const boundedElements = boundedPreview.pages.flatMap((part) => part.elements)
  expect(boundedElements.find((element) => element.sourcePath === 'Rows[0].Cells[0].Elements[0]'))
    .toMatchObject({ width: barcodeLayout!.width, height: barcodeLayout!.height })
  expect(boundedElements.find((element) => element.sourcePath === 'Rows[1].Cells[0].Elements[0]'))
    .toMatchObject({ width: qrLayout!.width, height: qrLayout!.height })

  await reloadTemplateJson(page, Buffer.from(JSON.stringify(template)), 'quiet-zone-auto.json')

  const initial = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
  const barcodeBefore = initial.Rows[0].Cells[0].Elements[0]
  const qrBefore = initial.Rows[1].Cells[0].Elements[0]
  expect(barcodeBefore.Width).toBe(0)
  expect(barcodeBefore.Height).toBe(24)
  expect(barcodeBefore.EndCoordinate).toEqual({ X: barcodeLayout!.width, Y: barcodeLayout!.height })
  expect(qrBefore.Width).toBe(0)
  expect(qrBefore.Height).toBe(0)
  expect(qrBefore.EndCoordinate).toEqual({ X: qrLayout!.width, Y: qrLayout!.height })
  const barcodeEndBefore = barcodeBefore.EndCoordinate
  const qrEndBefore = qrBefore.EndCoordinate

  const elements = page.locator('.tree-row.tree-element .tree-select')
  await elements.nth(0).click()
  let quiet = page.locator('.inspector-section').filter({ hasText: '静区' }).last()
  let mode = quiet.getByLabel('静区配置方式')
  await expect(mode).toHaveValue('default')
  let previewPromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await mode.selectOption('explicit')
  await previewPromise
  const barcodeSides = [
    ['静区左侧点数', '2'], ['静区上侧点数', '3'],
    ['静区右侧点数', '4'], ['静区下侧点数', '5'],
  ] as const
  for (const [label, value] of barcodeSides) {
    previewPromise = page.waitForResponse((response) =>
      response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
    await quiet.getByLabel(label).fill(value)
    await previewPromise
  }
  const barcodeExplicit = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
    .Rows[0].Cells[0].Elements[0]
  expect(barcodeExplicit.QuietZone).toEqual({ Left: 2, Top: 3, Right: 4, Bottom: 5 })
  expect(barcodeExplicit.Width).toBe(0)
  expect(barcodeExplicit.Height).toBe(24)
  expect(barcodeExplicit.EndCoordinate.X).toBe(barcodeEndBefore.X - 40 + 6)
  expect(barcodeExplicit.EndCoordinate.Y).toBe(barcodeEndBefore.Y + 8)
  const barcodeExplicitPreviewResponse = await request.post(`${apiUrl}/api/designer/preview`, {
    data: { template: JSON.parse((await exportTemplateJson(page)).toString('utf8')), data: {}, encoding: 'Ascii', protocol: 'Cpcl' },
  })
  expect(barcodeExplicitPreviewResponse.ok()).toBe(true)
  const barcodeExplicitPreview = await barcodeExplicitPreviewResponse.json() as {
    pages: Array<{ elements: Array<{ sourcePath: string; width: number; height: number }> }>
  }
  const barcodeExplicitLayout = barcodeExplicitPreview.pages.flatMap((part) => part.elements)
    .find((element) => element.sourcePath === 'Rows[0].Cells[0].Elements[0]')
  expect(barcodeExplicitLayout).toMatchObject({
    width: barcodeExplicit.EndCoordinate.X - barcodeExplicit.StartCoordinate.X,
    height: barcodeExplicit.EndCoordinate.Y - barcodeExplicit.StartCoordinate.Y,
  })

  const clearBarcodePreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await quiet.getByRole('button', { name: '恢复协议默认静区' }).click()
  await clearBarcodePreview
  const barcodeCleared = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
    .Rows[0].Cells[0].Elements[0]
  expect(barcodeCleared.QuietZone).toBeUndefined()
  expect(barcodeCleared.Width).toBe(0)
  expect(barcodeCleared.Height).toBe(24)
  expect(barcodeCleared.EndCoordinate).toEqual(barcodeEndBefore)

  await elements.nth(1).click()
  quiet = page.locator('.inspector-section').filter({ hasText: '静区' }).last()
  mode = quiet.getByLabel('静区配置方式')
  await expect(mode).toHaveValue('default')
  previewPromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await mode.selectOption('explicit')
  await previewPromise
  for (const [label, value] of [['静区左侧点数', '1'], ['静区上侧点数', '1'],
    ['静区右侧点数', '1'], ['静区下侧点数', '1']] as const) {
    previewPromise = page.waitForResponse((response) =>
      response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
    await quiet.getByLabel(label).fill(value)
    await previewPromise
  }
  const qrExplicit = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
    .Rows[1].Cells[0].Elements[0]
  expect(qrExplicit.QuietZone).toEqual({ Left: 1, Top: 1, Right: 1, Bottom: 1 })
  expect(qrExplicit.Width).toBe(0)
  expect(qrExplicit.Height).toBe(0)
  expect(qrExplicit.EndCoordinate.X).toBe(qrEndBefore.X - 30)
  expect(qrExplicit.EndCoordinate.Y).toBe(qrEndBefore.Y - 30)
  const qrExplicitPreviewResponse = await request.post(`${apiUrl}/api/designer/preview`, {
    data: { template: JSON.parse((await exportTemplateJson(page)).toString('utf8')), data: {}, encoding: 'Ascii', protocol: 'Cpcl' },
  })
  expect(qrExplicitPreviewResponse.ok()).toBe(true)
  const qrExplicitPreview = await qrExplicitPreviewResponse.json() as {
    pages: Array<{ elements: Array<{ sourcePath: string; width: number; height: number }> }>
  }
  const qrExplicitLayout = qrExplicitPreview.pages.flatMap((part) => part.elements)
    .find((element) => element.sourcePath === 'Rows[1].Cells[0].Elements[0]')
  expect(qrExplicitLayout).toMatchObject({
    width: qrExplicit.EndCoordinate.X - qrExplicit.StartCoordinate.X,
    height: qrExplicit.EndCoordinate.Y - qrExplicit.StartCoordinate.Y,
  })

  const clearQrPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await quiet.getByRole('button', { name: '恢复协议默认静区' }).click()
  await clearQrPreview
  const qrCleared = JSON.parse((await exportTemplateJson(page)).toString('utf8'))
    .Rows[1].Cells[0].Elements[0]
  expect(qrCleared.QuietZone).toBeUndefined()
  expect(qrCleared.Width).toBe(0)
  expect(qrCleared.Height).toBe(0)
  expect(qrCleared.EndCoordinate).toEqual(qrEndBefore)
})

test('任务级能力、物理页预览和 ZIP 清单映射保持一致', async ({ request }) => {
  const apiUrl = process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
  const rowsResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: { protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Ascii', escPosDevice: { nativeFlow: 'Rows' } },
  })
  expect(rowsResponse.ok()).toBe(true)
  const rows = await rowsResponse.json()
  expect(rows.nativeFlow).toBe('Rows')
  expect(rows.jobFeedUnits).toEqual(expect.arrayContaining(['Lines', 'MotionUnits']))

  const sequentialResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: { protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Ascii', escPosDevice: { nativeFlow: 'Sequential' } },
  })
  expect(sequentialResponse.ok()).toBe(true)
  const sequential = await sequentialResponse.json()
  expect(sequential.nativeFlow).toBe('Sequential')

  const rasterSequentialResponse = await request.post(`${apiUrl}/api/designer/capabilities`, {
    data: { protocol: 'EscPos', escPosLayoutMode: 'Raster', encoding: 'Ascii', escPosDevice: { nativeFlow: 'Sequential' } },
  })
  expect(rasterSequentialResponse.status()).toBe(400)

  const template = {
    Version: 3,
    Width: 320,
    Height: 48,
    Rows: [
      { Height: 24, Cells: [{ Width: 320, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'ALPHA' } }] }] },
      { Height: 24, Cells: [{ Width: 320, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'BRAVO' } }] }] },
      { Height: 24, Cells: [{ Width: 320, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'CHARLIE' } }] }] },
    ],
  }
  const job = {
    copies: 2, copyOrder: 'Uncollated', feedScope: 'None', feedUnit: 'Dots', feedAmount: 0, cutScope: 'None',
  }
  const payload = { template, data: {}, protocol: 'Tspl', encoding: 'Ascii', job }
  const previewResponse = await request.post(`${apiUrl}/api/designer/preview`, { data: payload })
  expect(previewResponse.ok()).toBe(true)
  const preview = await previewResponse.json()
  expect(preview.pages).toHaveLength(4)
  expect(preview.pages.map((page: { sourcePageIndex: number; copyIndex: number }) =>
    [page.sourcePageIndex, page.copyIndex])).toEqual([[0, 0], [0, 1], [1, 0], [1, 1]])

  const renderResponse = await request.post(`${apiUrl}/api/designer/render`, { data: payload })
  expect(renderResponse.ok()).toBe(true)
  const archive = await JSZip.loadAsync(await renderResponse.body())
  const pageNames = Object.keys(archive.files).filter((name) => /^page-\d{3}\.tspl$/.test(name)).sort()
  expect(pageNames).toEqual(['page-001.tspl', 'page-002.tspl', 'page-003.tspl', 'page-004.tspl'])
  const manifestEntry = archive.file('job.json')
  expect(manifestEntry).not.toBeNull()
  const manifest = JSON.parse(await manifestEntry!.async('text'))
  expect(manifest.protocol).toBe('Tspl')
  expect(manifest.pages.map((page: { file: string; sourcePageIndex: number; copyIndex: number }) =>
    [page.file, page.sourcePageIndex, page.copyIndex])).toEqual([
    ['page-001.tspl', 0, 0], ['page-002.tspl', 0, 1], ['page-003.tspl', 1, 0], ['page-004.tspl', 1, 1],
  ])

  const escPosRender = await request.post(`${apiUrl}/api/designer/render`, {
    data: { template, data: {}, protocol: 'EscPos', encoding: 'Ascii', job: {
      copies: 2, copyOrder: 'Collated', feedScope: 'JobEnd', feedUnit: 'Lines', feedAmount: 2, cutScope: 'JobEnd',
    } },
  })
  expect(escPosRender.ok()).toBe(true)
  const escPosArchive = await JSZip.loadAsync(await escPosRender.body())
  const escPosManifest = JSON.parse(await escPosArchive.file('job.json')!.async('text'))
  expect(escPosManifest.protocol).toBe('EscPos')
  expect(escPosManifest.pages.map((part: { sourcePageIndex: number; copyIndex: number }) =>
    [part.sourcePageIndex, part.copyIndex])).toEqual([[0, 0], [1, 0], [0, 1], [1, 1]])
  expect(escPosManifest.pages.map((part: { feedAfterPrint: boolean; cutAfterPrint: boolean }) =>
    [part.feedAfterPrint, part.cutAfterPrint])).toEqual([[false, false], [false, false], [false, false], [true, true]])
  const escPosLast = await escPosArchive.file('page-004.escpos')!.async('uint8array')
  expect(Array.from(escPosLast.slice(-6))).toEqual([0x1b, 0x64, 2, 0x1d, 0x56, 0])
})

test('任务级输出设置保存重载后 TSPL 页面字节保持一致', async ({ page }) => {
  const template = {
    Version: 3,
    Width: 320,
    Height: 48,
    Rows: [
      { Height: 24, Cells: [{ Width: 320, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'ALPHA' } }] }] },
      { Height: 24, Cells: [{ Width: 320, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'BRAVO' } }] }] },
      { Height: 24, Cells: [{ Width: 320, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'CHARLIE' } }] }] },
    ],
  }
  const project = {
    ...designerProject(template, 'Tspl'),
    outputSettings: {
      ...designerProject(template, 'Tspl').outputSettings,
      job: { copies: 2, copyOrder: 'Uncollated', feedScope: 'None', feedUnit: 'Dots', feedAmount: 0, cutScope: 'None' },
    },
  }
  await importDesignerProject(page, project, 'job-roundtrip.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await expect(page.getByLabel('启用任务级输出')).toBeChecked()
  await expect(page.getByLabel('任务打印份数')).toHaveValue('2')
  await expect(page.getByLabel('任务份数顺序')).toHaveValue('Uncollated')

  const beforeDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 TSPL' }).click()
  const beforeArchive = await JSZip.loadAsync(await readFile(await (await beforeDownload).path()))
  const beforeNames = Object.keys(beforeArchive.files).filter((name) => /^page-\d{3}\.tspl$/.test(name)).sort()
  const beforePages = await Promise.all(beforeNames.map(async (name) =>
    Array.from(await beforeArchive.file(name)!.async('uint8array'))))

  const saveDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const saved = JSON.parse((await readFile(await (await saveDownload).path())).toString('utf8'))
  expect(saved.outputSettings.job).toEqual(project.outputSettings.job)

  const reloadPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.locator('[data-testid="template-import"]').setInputFiles({
    name: 'job-roundtrip-saved.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(saved)),
  })
  await reloadPreview
  await expect(page.getByLabel('启用任务级输出')).toBeChecked()

  const afterDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 TSPL' }).click()
  const afterArchive = await JSZip.loadAsync(await readFile(await (await afterDownload).path()))
  const afterNames = Object.keys(afterArchive.files).filter((name) => /^page-\d{3}\.tspl$/.test(name)).sort()
  const afterPages = await Promise.all(afterNames.map(async (name) =>
    Array.from(await afterArchive.file(name)!.async('uint8array'))))

  expect(afterNames).toEqual(beforeNames)
  expect(afterPages).toEqual(beforePages)

  const invalidPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.status() === 400)
  await page.getByLabel('任务进纸数量').fill('4')
  await invalidPreview
  await page.getByRole('button', { name: '保存项目' }).click()
  await expect(page.getByText(/outputSettings\.job\.feedScope.*必须指定进纸范围/)).toBeVisible()

  const recoveredPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.ok())
  await page.getByLabel('任务进纸范围').selectOption('JobEnd')
  await recoveredPreview
  const recoveredDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const recovered = JSON.parse((await readFile(await (await recoveredDownload).path())).toString('utf8'))
  expect(recovered.outputSettings.job).toMatchObject({ feedAmount: 4, feedScope: 'JobEnd' })
})
