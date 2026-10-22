import { expect, test } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'

const compositeCases = [
  { name: 'Truncated', type: 26, feature: 'gs1-composite-truncated', width: 300, minimumHeight: 39 },
  { name: 'Stacked Omnidirectional', type: 32, feature: 'gs1-composite-stacked-omnidirectional', width: 168, minimumHeight: 99 },
] as const

for (const item of compositeCases) {
  test(`GS1 DataBar ${item.name} Composite 编辑、尺寸、预览、输出与项目重载`, async ({ page }) => {
    await page.goto('/')
    await expect(page.locator('.capability-note')).toHaveClass(/ready/)
    await page.locator('.sample-picker select').selectOption('blank')
    await page.getByRole('button', { name: '组件与表格' }).click()
    await page.getByRole('button', { name: 'GS1 Composite', exact: true }).click()

    await page.getByLabel('Composite 线性条高', { exact: true }).fill('1')
    await page.getByLabel('Composite 线性组件', { exact: true }).selectOption(String(item.type))
    await expect(page.getByLabel('Composite 线性条高', { exact: true })).toHaveValue(String(item.type === 26 ? 26 : 66))
    await page.getByLabel('Composite 模块宽度', { exact: true }).fill('3')
    await expect(page.getByLabel('Composite 线性条高', { exact: true })).toHaveValue(String(item.minimumHeight))
    await page.getByLabel('Composite 二维模式', { exact: true }).selectOption('2')
    await expect(page.getByLabel('Composite 二维模式', { exact: true }).locator('option')).toHaveText(['Auto', 'CC-A', 'CC-B'])
    await page.getByLabel('Composite 旋转', { exact: true }).selectOption('0')
    await page.getByLabel('显示内容', { exact: true }).fill('(01)09506000134352')
    const preview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
      response.status() === 200 && JSON.stringify(response.request().postDataJSON()?.template).includes('LOT-STACKED-OMNI'))
    await page.getByLabel('Composite 次数据', { exact: true }).fill('(10)LOT-STACKED-OMNI')
    const response = await preview
    expect(response.request().headers()['x-easyprint-required-features']).toContain(item.feature)
    expect(response.request().postDataJSON()?.template).toMatchObject({ Rows: [{ Cells: [{ Elements: [{
      Type: 11, LinearType: item.type, Mode: 2, ModuleWidth: 3, LinearBarHeight: item.minimumHeight,
      Field: { Value: '(01)09506000134352' }, SecondaryField: { Value: '(10)LOT-STACKED-OMNI' },
    }] }] }] })
    const layout = await response.json()
    const composite = layout.pages.flatMap((value: { elements: Array<Record<string, unknown>> }) => value.elements)
      .find((value: Record<string, unknown>) => value.type === 11)
    expect(composite).toMatchObject({ width: item.width, height: expect.any(Number) })
    expect(composite.height).toBeGreaterThan(item.minimumHeight)
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

    const output = page.waitForEvent('download')
    await page.getByRole('button', { name: '生成 CPCL' }).click()
    const archive = await JSZip.loadAsync(await readFile(await (await output).path()))
    const bytes = Buffer.from(await archive.file('page-001.cpcl')!.async('uint8array'))
    const header = Buffer.from(`CG ${Math.ceil(composite.width / 8)} ${composite.height} ${composite.x} ${composite.y} `)
    expect(bytes.indexOf(header)).toBeGreaterThanOrEqual(0)
    expect(bytes.length).toBeGreaterThan(Math.ceil(composite.width / 8) * composite.height)

    const savedDownload = page.waitForEvent('download')
    await page.getByRole('button', { name: '保存项目' }).click()
    const saved = await readFile(await (await savedDownload).path())
    const project = JSON.parse(saved.toString())
    expect(project.template.Rows[0].Cells[0].Elements[0]).toMatchObject({
      Type: 11, LinearType: item.type, Mode: 2, ModuleWidth: 3, LinearBarHeight: item.minimumHeight,
      Field: { Value: '(01)09506000134352' }, SecondaryField: { Value: '(10)LOT-STACKED-OMNI' },
    })

    await page.reload()
    await expect(page.locator('.capability-note')).toHaveClass(/ready/)
    const restoredPreview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
      response.status() === 200 && response.request().postDataJSON()?.template.Rows[0].Cells[0].Elements[0].LinearType === item.type)
    await page.getByTestId('template-import').setInputFiles({
      name: `${item.name.toLowerCase().replaceAll(' ', '-')}.json`, mimeType: 'application/json', buffer: saved,
    })
    expect((await (await restoredPreview).json()).pages).toEqual(layout.pages)
    await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
    await expect(page.getByLabel('Composite 线性组件', { exact: true })).toHaveValue(String(item.type))
    await expect(page.getByLabel('Composite 二维模式', { exact: true })).toHaveValue('2')
    await expect(page.getByLabel('Composite 模块宽度', { exact: true })).toHaveValue('3')
    await expect(page.getByLabel('Composite 线性条高', { exact: true })).toHaveValue(String(item.minimumHeight))
    await expect(page.getByLabel('显示内容', { exact: true })).toHaveValue('(01)09506000134352')
    await expect(page.getByLabel('Composite 次数据', { exact: true })).toHaveValue('(10)LOT-STACKED-OMNI')
  })
}

test('旧 Host 缺少两个独立特性时禁用对应 Composite 并拒绝导入预览', async ({ page }) => {
  let requestedFeatures = ''
  page.on('request', request => {
    if (request.url().includes('/api/designer/preview'))
      requestedFeatures = request.headers()['x-easyprint-required-features'] ?? ''
  })
  await page.route('**/api/designer/**', async route => {
    const response = await route.fetch()
    const headers = response.headers()
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter(feature => feature.trim() !== 'gs1-composite-truncated' &&
        feature.trim() !== 'gs1-composite-stacked-omnidirectional').join(',')
    if (route.request().url().includes('/capabilities')) {
      const json = await response.json()
      if (json.gs1Composite) json.gs1Composite.linearTypes = json.gs1Composite.linearTypes
        .filter((type: string) => type !== 'Gs1DataBarTruncated' && type !== 'Gs1DataBarStackedOmnidirectional')
      if (Array.isArray(json.barcodeTypes)) json.barcodeTypes = json.barcodeTypes.filter((type: number) => type !== 32)
      await route.fulfill({ response, headers, json })
    } else await route.fulfill({ response, headers })
  })

  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.locator('.sample-picker select').selectOption('blank')
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.getByRole('button', { name: 'GS1 Composite', exact: true }).click()

  for (const item of compositeCases) {
    const template = { Version: 3, Width: 400, Height: 300, Rows: [{ Cells: [{ Elements: [{
      Type: 11, LinearType: item.type, Mode: 1, Field: { Value: '(01)09506000134352' },
      SecondaryField: { Value: '(10)OLD-HOST' }, ModuleWidth: 3, LinearBarHeight: item.minimumHeight,
    }] }] }] }
    await page.getByTestId('template-import').setInputFiles({
      name: `old-host-${item.type}.json`, mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)),
    })
    await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
    const option = page.getByLabel('Composite 线性组件', { exact: true }).locator(`option[value="${item.type}"]`)
    await expect(option).toHaveJSProperty('disabled', true)
    await expect(page.getByText(item.type === 26
      ? '当前 Host 未协商 GS1 DataBar Truncated Composite；请更新 Host 后再预览或输出。'
      : '当前 Host 未协商 GS1 DataBar Stacked Omnidirectional Composite；请更新 Host 后再预览或输出。'))
      .toBeVisible()
    await expect(page.getByRole('region', { name: '打印诊断' })).toContainText(
      `打印服务缺少 GS1 DataBar ${item.name} Composite 功能，请更新 Host。 · 代码：ContractFeatureUnavailable`)
    expect(requestedFeatures).toContain(item.feature)
  }
})

test('普通 Stacked Omnidirectional 只在 Raster 可用，并保持每行高度和符号尺寸', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  const template = { Version: 3, Width: 300, Height: 260, Rows: [{ Cells: [{ Elements: [{
    Type: 4, BarcodeType: 0, NarrowModuleWidth: 2, Height: 40, HumanReadable: true, HumanReadableReserveHeight: 20,
    Rotation: 0, Field: { Value: '0950600013435' },
  }] }] }] }
  await page.getByTestId('template-import').setInputFiles({
    name: 'ordinary-stacked-omni.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)),
  })
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  const typeSelect = page.getByRole('combobox', { name: '制式', exact: true })
  await expect(typeSelect.locator('option[value="32"]')).toHaveJSProperty('disabled', true)

  const rasterPreview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && response.request().postDataJSON()?.protocol === 'EscPos' &&
    response.request().postDataJSON()?.escPosLayoutMode === 'Raster')
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await rasterPreview
  await expect(typeSelect.locator('option[value="32"]')).toHaveJSProperty('disabled', false)

  const preview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && response.request().postDataJSON()?.template.Rows[0].Cells[0].Elements[0].BarcodeType === 32)
  await typeSelect.selectOption('32')
  const response = await preview
  const requestElement = response.request().postDataJSON()?.template.Rows[0].Cells[0].Elements[0]
  expect(requestElement).toMatchObject({ BarcodeType: 32, NarrowModuleWidth: 2, Height: 66, HumanReadable: true,
    HumanReadableReserveHeight: 20, Field: { Value: '0950600013435' } })
  const layout = await response.json()
  const barcode = layout.pages.flatMap((value: { elements: Array<Record<string, unknown>> }) => value.elements)
    .find((value: Record<string, unknown>) => value.type === 4)
  expect(barcode).toMatchObject({ width: 140, height: 158 })
  // 普通模板保留左右各10X默认静区；符号本体仍为50X。
  expect(barcode.width - 2 * 10 * 2).toBe(50 * 2)
  expect(barcode.height - 20).toBe(2 * 66 + 3 * 2)
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  const dimensions = page.locator('.inspector-section').filter({ hasText: '位置与尺寸' }).locator('.numeric-grid input')
  await expect(dimensions.nth(3)).toHaveValue('66')
  await expect(dimensions.nth(3)).toHaveAttribute('min', '66')
  await dimensions.nth(3).fill('65')
  const resizedPreview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && response.request().postDataJSON()?.template.Rows[0].Cells[0].Elements[0].Height === 70)
  await dimensions.nth(3).fill('70')
  await expect(dimensions.nth(3)).toHaveValue('70')
  const resizedLayout = await (await resizedPreview).json()
  const resizedBarcode = resizedLayout.pages.flatMap((value: { elements: Array<Record<string, unknown>> }) => value.elements)
    .find((value: Record<string, unknown>) => value.type === 4)
  expect(resizedBarcode).toMatchObject({ width: 140, height: 166 })
  expect(resizedBarcode.height - 20).toBe(2 * 70 + 3 * 2)

  const savedDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const saved = JSON.parse((await readFile(await (await savedDownload).path())).toString())
  expect(saved.Rows[0].Cells[0].Elements[0]).toMatchObject({
    Type: 4, BarcodeType: 32, NarrowModuleWidth: 2, Height: 70, HumanReadable: true, HumanReadableReserveHeight: 20,
  })
  const restoredPreview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && response.request().postDataJSON()?.template.Rows[0].Cells[0].Elements[0].BarcodeType === 32)
  await page.getByTestId('template-import').setInputFiles({
    name: 'ordinary-stacked-omni-restored.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(saved)),
  })
  // 原始模板 JSON 不携带输出协议；重新导入后恢复默认 CPCL，需显式选择 Raster。
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('Cpcl')
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  expect((await (await restoredPreview).json()).pages).toEqual(resizedLayout.pages)
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  await expect(typeSelect).toHaveValue('32')
  await expect(dimensions.nth(3)).toHaveValue('70')
})
