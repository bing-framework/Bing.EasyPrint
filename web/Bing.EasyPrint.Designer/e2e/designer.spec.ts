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
  const ratio = page.locator('.inspector-section').filter({ hasText: '条码参数' }).locator('select').nth(1)
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

  const originalCpclDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 CPCL' }).click()
  const originalCpclDownload = await originalCpclDownloadPromise
  const originalCpclArchive = await JSZip.loadAsync(await readFile(await originalCpclDownload.path()))
  const originalCpcl = await originalCpclArchive.file('page-001.cpcl')!.async('base64')

  const packageDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '模板包' }).click()
  const packageDownload = await packageDownloadPromise
  const packageBytes = await readFile(await packageDownload.path())
  await page.locator('input[type="file"]').first().setInputFiles({ name: 'roundtrip.zip', mimeType: 'application/zip', buffer: packageBytes })
  await expect(page.locator('.toast-message')).toContainText('已导入模板包 · 1 个资源')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

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

test('循环组件导入被拒绝且当前模板仍可编辑', async ({ page }) => {
  await openBlankTemplate(page)
  const invalid = { Version: 2, Width: 100, Height: 100,
    Components: [{ Id: 'loop', Width: 20, Height: 20, Elements: [{ Type: 6, ComponentId: 'loop' }] }],
    Rows: [{ Height: 100, Cells: [{ Width: 100, Elements: [{ Type: 6, ComponentId: 'loop' }] }] }] }
  await page.locator('input[accept^=".json,.zip"]').setInputFiles({ name: 'cycle.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(invalid)) })
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
  await page.locator('input[accept^=".json,.zip"]').setInputFiles({
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
  await page.locator('input[accept^=".json,application/json"]').setInputFiles({
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
