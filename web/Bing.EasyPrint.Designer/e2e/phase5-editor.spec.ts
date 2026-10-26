import { expect, test } from '@playwright/test'
import JSZip from 'jszip'
import { readFile, writeFile } from 'node:fs/promises'

const textTemplate = {
  Version: 1,
  Width: 160,
  Height: 60,
  Rows: [{ Height: 60, Cells: [{ Width: 160, Elements: [{
    Type: 1, FontSize: 24, Field: { Value: 'Product.Name' },
    StartCoordinate: { X: 8, Y: 8 }, EndCoordinate: { X: 150, Y: 42 },
  }] }] }],
}

const barcodeTemplate = {
  Version: 2,
  Width: 400,
  Height: 400,
  Rows: [{ Height: 400, Cells: [{ Width: 400, Elements: [{
    Type: 4, BarcodeType: 0, Height: 40, NarrowModuleWidth: 2,
    HumanReadable: true, HumanReadableReserveHeight: 16,
    Field: { Value: 'F5-12345' },
  }] }] }],
}

const componentTemplate = {
  Version: 2,
  Width: 300,
  Height: 100,
  Components: [{ Id: 'badge', Width: 100, Height: 40, Elements: [{
    Type: 1, FontSize: 18, Field: { Value: 'Badge' }, StartCoordinate: { X: 4, Y: 4 },
  }] }],
  Rows: [{ Height: 100, Cells: [{ Width: 300, Elements: [{
    Type: 6, ComponentId: 'badge', Width: 100, Height: 40, StartCoordinate: { X: 20, Y: 20 },
  }] }] }],
}

async function openTextTemplate(page: import('@playwright/test').Page) {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'phase5-label.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(textTemplate)),
  })
  await expect(page.locator('.tree-select').filter({ hasText: 'Product.Name' })).toHaveCount(1)
}

async function openBarcodeTemplate(page: import('@playwright/test').Page) {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'phase5-barcode.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(barcodeTemplate)),
  })
  await page.getByLabel('输出协议').selectOption('EscPosRaster')
  await expect(page.getByLabel('输出协议')).toHaveValue('EscPosRaster')
  await expect(page.locator('.tree-select').filter({ hasText: 'F5-12345' })).toHaveCount(1)
  await page.locator('.tree-select').filter({ hasText: 'F5-12345' }).click()
  await expect(page.getByTestId('export-current-pdf')).toBeEnabled()
  await expect(page.getByTestId('export-element-svg')).toBeEnabled()
}

async function openComponentTemplate(page: import('@playwright/test').Page) {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'phase5-component.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(componentTemplate)),
  })
  await expect(page.locator('.tree-component').filter({ hasText: '组件 · badge' })).toHaveCount(1)
}

async function exportCurrentTemplate(page: import('@playwright/test').Page) {
  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  return JSON.parse((await readFile(await (await download).path())).toString('utf8')) as typeof componentTemplate
}

async function saveCurrentProject(page: import('@playwright/test').Page) {
  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  return JSON.parse((await readFile(await (await download).path())).toString('utf8')) as {
    template: typeof componentTemplate
    editorState: { nodeStates: Record<string, { id: string; locked: boolean }> }
  }
}

test('project v2 preserves locked node identity and unlocks from the tree', async ({ page }) => {
  await openTextTemplate(page)
  const nameRow = page.locator('.tree-row').filter({ hasText: 'Product.Name' }).first()
  await nameRow.getByRole('button', { name: /锁定/ }).click()
  await nameRow.getByRole('button', { name: /解锁/ }).waitFor()
  await nameRow.locator('.tree-select').click()
  await expect(page.locator('.inspector-edit-scope input').first()).toBeDisabled()

  const firstDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const saved = await readFile(await (await firstDownload).path())
  const firstProject = JSON.parse(saved.toString('utf8')) as {
    editorState: { nodeStates: Record<string, { id: string; locked: boolean }> }
  }
  const sourcePath = 'Rows[0].Cells[0].Elements[0]'
  expect(firstProject.editorState.nodeStates[sourcePath].locked).toBe(true)
  const stableId = firstProject.editorState.nodeStates[sourcePath].id

  await page.locator('input[type="file"]').first().setInputFiles({
    name: 'locked-project.json', mimeType: 'application/json', buffer: saved,
  })
  await expect(page.locator('.inspector-edit-scope input').first()).toBeDisabled()
  await expect(page.getByRole('button', { name: /解锁.*Product.Name/ })).toBeEnabled()
  await page.keyboard.press('Delete')
  await expect(page.locator('.tree-row').filter({ hasText: 'Product.Name' })).toHaveCount(1)

  const secondDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const reloaded = JSON.parse((await readFile(await (await secondDownload).path())).toString('utf8')) as {
    editorState: { nodeStates: Record<string, { id: string; locked: boolean }> }
  }
  expect(reloaded.editorState.nodeStates[sourcePath]).toEqual({ id: stableId, locked: true })

  await page.getByRole('button', { name: /解锁.*Product.Name/ }).click()
  await expect(page.getByRole('button', { name: /锁定.*Product.Name/ })).toBeEnabled()
  await expect(page.locator('.inspector-edit-scope input').first()).toBeEnabled()
})

test('component rename respects a locked definition', async ({ page }) => {
  await openComponentTemplate(page)
  await page.getByRole('button', { name: '组件与表格' }).click()
  const definition = page.locator('.tree-component').filter({ hasText: '组件 · badge' })
  await definition.getByRole('button', { name: '锁定 组件 · badge' }).click()
  await expect(page.getByRole('button', { name: '重命名组件 badge' })).toBeDisabled()
  const exported = await exportCurrentTemplate(page)
  expect(exported.Components?.[0]?.Id).toBe('badge')
  expect(exported.Rows[0].Cells?.[0].Elements?.[0]).toMatchObject({ ComponentId: 'badge' })
})

test('component rename respects a locked reference', async ({ page }) => {
  await openComponentTemplate(page)
  await page.getByRole('button', { name: '组件与表格' }).click()
  const reference = page.locator('.tree-element').filter({ hasText: '组件 · badge' })
  await reference.getByRole('button', { name: '锁定 组件 · badge' }).click()
  await expect(page.getByRole('button', { name: '重命名组件 badge' })).toBeDisabled()
  const exported = await exportCurrentTemplate(page)
  expect(exported.Components?.[0]?.Id).toBe('badge')
  expect(exported.Rows[0].Cells?.[0].Elements?.[0]).toMatchObject({ ComponentId: 'badge' })
})

test('component rename is blocked when a referenced node is locked during editing', async ({ page }) => {
  await openComponentTemplate(page)
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.getByRole('button', { name: '重命名组件 badge' }).click()
  await page.getByRole('textbox', { name: '新组件标识' }).fill('renamedBadge')
  const reference = page.locator('.tree-element').filter({ hasText: '组件 · badge' })
  await reference.getByRole('button', { name: '锁定 组件 · badge' }).click()
  await expect(page.getByRole('button', { name: '保存名称' })).toBeDisabled()
  const exported = await exportCurrentTemplate(page)
  expect(exported.Components?.[0]?.Id).toBe('badge')
  expect(exported.Rows[0].Cells?.[0].Elements?.[0]).toMatchObject({ ComponentId: 'badge' })
})

test('unlocked component rename updates references and preserves node identities', async ({ page }) => {
  await openComponentTemplate(page)
  await page.getByRole('button', { name: '组件与表格' }).click()
  const before = await saveCurrentProject(page)
  const definitionId = before.editorState.nodeStates['Components[0]']?.id
  const referencePath = 'Rows[0].Cells[0].Elements[0]'
  const referenceId = before.editorState.nodeStates[referencePath]?.id

  await page.getByRole('button', { name: '重命名组件 badge' }).click()
  await page.getByRole('textbox', { name: '新组件标识' }).fill('renamedBadge')
  await page.getByRole('button', { name: '保存名称' }).click()
  await expect(page.getByRole('button', { name: '重命名组件 renamedBadge' })).toBeVisible()

  const exported = await exportCurrentTemplate(page)
  expect(exported.Components?.[0]?.Id).toBe('renamedBadge')
  expect(exported.Rows[0].Cells?.[0].Elements?.[0]).toMatchObject({ ComponentId: 'renamedBadge' })
  const after = await saveCurrentProject(page)
  expect(after.editorState.nodeStates['Components[0]']?.id).toBe(definitionId)
  expect(after.editorState.nodeStates[referencePath]?.id).toBe(referenceId)
})

test('undo and redo restore invalid business JSON without normalizing its raw text', async ({ page }) => {
  await openTextTemplate(page)
  const tabs = page.locator('.data-panel-tabs')
  if (!(await tabs.isVisible())) await page.getByRole('button', { name: '切换业务数据面板' }).click()
  await page.locator('.data-panel-tabs').getByRole('button', { name: 'JSON' }).click()
  const textarea = page.getByRole('textbox', { name: '业务数据 JSON' })
  const valid = await textarea.inputValue()
  const invalid = '{"raw": [1, 2'
  await textarea.fill(invalid)
  await expect(page.getByText('JSON 语法错误')).toBeVisible()
  await page.waitForTimeout(350)
  await page.keyboard.press('Control+z')
  await expect(textarea).toHaveValue(valid)
  await page.keyboard.press('Control+y')
  await expect(textarea).toHaveValue(invalid)
  await expect(page.getByText('JSON 语法错误')).toBeVisible()
})

test('batch mapping and record selection participate in undo history', async ({ page }) => {
  await openTextTemplate(page)
  await page.getByTestId('batch-toggle').click()
  await page.getByTestId('batch-file-input').setInputFiles({
    name: 'history.csv', mimeType: 'text/csv',
    buffer: Buffer.from('ProductName,Price\nAlpha,10\nBeta,20\nGamma,30', 'utf8'),
  })
  const mapping = page.getByTestId('batch-map-ProductName')
  await expect(mapping).toHaveValue('ProductName')
  await expect(page.getByTestId('batch-records').locator('input[type="checkbox"]')).toHaveCount(3)
  await mapping.fill('Name')
  await mapping.press('Tab')
  await page.waitForTimeout(350)
  await page.keyboard.press('Control+z')
  await expect(mapping).toHaveValue('ProductName')

  const records = page.getByTestId('batch-records').locator('input[type="checkbox"]')
  await records.nth(1).uncheck()
  await page.waitForTimeout(350)
  await page.keyboard.press('Control+z')
  await expect(page.locator('.batch-selection').nth(1)).toContainText('3 / 3')
})

test('downloads selected-code SVG and selected or all-page PDF through independent endpoints', async ({ page }, testInfo) => {
  const pdfRequests: Record<string, unknown>[] = []
  const svgRequests: Record<string, unknown>[] = []
  await page.route('**/api/designer/page-pdf', async route => {
    pdfRequests.push(route.request().postDataJSON() as Record<string, unknown>)
    await route.continue()
  })
  await page.route('**/api/designer/element-svg', async route => {
    svgRequests.push(route.request().postDataJSON() as Record<string, unknown>)
    await route.continue()
  })
  await openBarcodeTemplate(page)

  const currentPdf = page.waitForEvent('download')
  await page.getByTestId('export-current-pdf').click()
  const currentPdfDownload = await currentPdf
  expect(currentPdfDownload.suggestedFilename()).toMatch(/easyprint-page-1\.pdf$/)
  await currentPdfDownload.saveAs(testInfo.outputPath('current-page.pdf'))
  const allPdf = page.waitForEvent('download')
  await page.getByTestId('export-all-pdf').click()
  const allPdfDownload = await allPdf
  expect(allPdfDownload.suggestedFilename()).toBe('easyprint-pages.pdf')
  await allPdfDownload.saveAs(testInfo.outputPath('all-pages.pdf'))
  const rasterPng = page.waitForEvent('download')
  await page.getByRole('button', { name: '下载页面 PNG' }).click()
  await (await rasterPng).saveAs(testInfo.outputPath('printed-page.png'))
  const svg = page.waitForEvent('download')
  await page.getByTestId('export-element-svg').click()
  const svgDownload = await svg
  expect(svgDownload.suggestedFilename()).toMatch(/easyprint-page-1-element-1\.svg$/)
  await svgDownload.saveAs(testInfo.outputPath('barcode.svg'))

  expect(pdfRequests).toHaveLength(2)
  expect(pdfRequests[0]).toMatchObject({ protocol: 'EscPos', escPosLayoutMode: 'Raster', pageIndex: 0 })
  expect(pdfRequests[1]).not.toHaveProperty('pageIndex')
  expect(svgRequests).toHaveLength(1)
  expect(svgRequests[0]).toMatchObject({
    pageIndex: 0, elementIndex: 0, sourcePath: 'Rows[0].Cells[0].Elements[0]',
  })
  const svgBase64 = (await readFile(testInfo.outputPath('barcode.svg'))).toString('base64')
  const svgRasterBase64 = await page.evaluate(async (encodedSvg) => {
    const image = new Image()
    image.src = `data:image/svg+xml;base64,${encodedSvg}`
    await image.decode()
    const markup = atob(encodedSvg)
    const svgDocument = new DOMParser().parseFromString(markup, 'image/svg+xml')
    const viewBox = svgDocument.documentElement.getAttribute('viewBox')!.split(/\s+/).map(Number)
    const width = viewBox[2]!
    const height = viewBox[3]!
    const canvas = window.document.createElement('canvas')
    canvas.width = width
    canvas.height = height
    const context = canvas.getContext('2d')!
    context.fillStyle = '#fff'
    context.fillRect(0, 0, width, height)
    context.drawImage(image, 0, 0, width, height)
    return canvas.toDataURL('image/png').split(',')[1]!
  }, svgBase64)
  await writeFile(testInfo.outputPath('chromium-svg-raster.png'), Buffer.from(svgRasterBase64, 'base64'))
})

test('batch exports are opt-in, included in the ZIP manifest, and preserve selections on an older Host', async ({ page }, testInfo) => {
  const pageErrors: string[] = []
  page.on('pageerror', error => pageErrors.push(error.message))
  const batchRequests: Record<string, unknown>[] = []
  let omitExportFeatures = false
  await page.route('**/api/designer/capabilities', async route => {
    const response = await route.fetch()
    if (!omitExportFeatures) return route.fulfill({ response })
    const headers = { ...response.headers() }
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '')
      .split(',').filter(feature => feature !== 'page-pdf-export' && feature !== 'barcode-svg-export').join(',')
    await route.fulfill({ response, headers })
  })
  await page.route('**/api/designer/batch', async route => {
    batchRequests.push(route.request().postDataJSON() as Record<string, unknown>)
    await route.continue()
  })
  await openBarcodeTemplate(page)
  await page.getByTestId('batch-toggle').click()
  await page.getByTestId('batch-file-input').setInputFiles({
    name: 'exports.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify([{ Name: 'Batch', Value: 'F5-12345' }])),
  })
  await expect(page.getByTestId('batch-export-pdf')).toBeEnabled()
  await expect(page.getByTestId('batch-export-svg')).toBeEnabled()
  await page.getByTestId('batch-export-pdf').check()
  await page.getByTestId('batch-export-svg').check()
  await page.getByRole('button', { name: '批量生成' }).click()
  await expect(page.getByRole('button', { name: /下载结果/ })).toBeVisible({ timeout: 30000 })
  expect(batchRequests).toHaveLength(1)
  expect(batchRequests[0]!.exportFormats).toEqual(['pdf', 'svg'])

  const archiveDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: /下载结果/ }).click()
  const archiveDownloadResult = await archiveDownload
  await archiveDownloadResult.saveAs(testInfo.outputPath('batch-exports.zip'))
  const archive = await JSZip.loadAsync(await readFile(testInfo.outputPath('batch-exports.zip')))
  const manifest = JSON.parse(await archive.file('manifest.json')!.async('string')) as {
    records: Array<{ files: Array<{ format?: string; pageCount?: number; copies?: number; elementIndex?: number }> }>
  }
  const files = manifest.records[0]!.files
  expect(files.some(file => file.format === 'pdf' && file.pageCount === 1 && file.copies === 1)).toBe(true)
  expect(files.some(file => file.format === 'svg' && file.elementIndex === 0)).toBe(true)

  omitExportFeatures = true
  await page.getByLabel('输出协议').selectOption('Cpcl')
  await page.getByLabel('输出协议').selectOption('EscPosRaster')
  await expect(page.getByTestId('batch-export-pdf')).toBeChecked()
  await expect(page.getByTestId('batch-export-svg')).toBeChecked()
  await expect(page.getByTestId('batch-export-pdf')).toBeDisabled()
  await expect(page.getByTestId('batch-export-svg')).toBeDisabled()
  await page.screenshot({ path: testInfo.outputPath('phase5-old-host-export-options.png'), fullPage: false })
  expect(pageErrors).toEqual([])
})
