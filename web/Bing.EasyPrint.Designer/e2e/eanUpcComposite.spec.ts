import { expect, test } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'

const cases = [
  { name: 'EAN-8', type: 4, primary: '96385074', mode: 1, width: 144 },
  { name: 'EAN-13', type: 5, primary: '4006381333931', mode: 2, width: 198 },
  { name: 'UPC-A', type: 6, primary: '036000291452', mode: 1, width: 198 },
  { name: 'UPC-E', type: 7, primary: '04252614', mode: 2, width: 110 },
] as const

for (const item of cases) {
  test(`${item.name} Composite 编辑、预览、导出与项目重载`, async ({ page }) => {
    await page.goto('/')
    await expect(page.locator('.capability-note')).toHaveClass(/ready/)
    await page.locator('.sample-picker select').selectOption('blank')
    await page.getByRole('button', { name: '组件与表格' }).click()
    await page.getByRole('button', { name: 'GS1 Composite', exact: true }).click()
    await page.getByLabel('Composite 线性组件', { exact: true }).selectOption(String(item.type))
    await page.getByLabel('Composite 旋转', { exact: true }).selectOption('0')
    await page.getByLabel('Composite 模块宽度', { exact: true }).fill('2')
    await page.getByLabel('Composite 线性条高', { exact: true }).fill('38')
    await page.getByLabel('显示内容', { exact: true }).fill(item.primary)
    await page.getByLabel('Composite 次数据', { exact: true }).fill('(91)LOT-EANUPC')
    await expect(page.getByLabel('Composite 二维模式', { exact: true }).locator('option'))
      .toHaveText(['Auto', 'CC-A', 'CC-B'])

    const preview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
      response.status() === 200 && response.request().postDataJSON()?.template.Rows[0].Cells[0].Elements[0].Mode === item.mode)
    await page.getByLabel('Composite 二维模式', { exact: true }).selectOption(String(item.mode))
    const response = await preview
    expect(response.request().headers()['x-easyprint-required-features']).toContain('gs1-composite-ean-upc')
    expect(response.request().postDataJSON()?.template).toMatchObject({ Rows: [{ Cells: [{ Elements: [{
      Type: 11, LinearType: item.type, Mode: item.mode, ModuleWidth: 2, LinearBarHeight: 38,
      Field: { Value: item.primary }, SecondaryField: { Value: '(91)LOT-EANUPC' },
    }] }] }] })
    const layout = await response.json()
    const composite = layout.pages.flatMap((value: { elements: Array<Record<string, unknown>> }) => value.elements)
      .find((value: Record<string, unknown>) => value.type === 11)
    expect(composite).toMatchObject({ width: expect.any(Number), height: expect.any(Number) })
    expect(composite.width).toBe(item.width)
    expect(composite.height).toBeGreaterThan(38)
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

    const output = page.waitForEvent('download')
    await page.getByRole('button', { name: '生成 CPCL' }).click()
    const archive = await JSZip.loadAsync(await readFile(await (await output).path()))
    const pageFile = archive.file('page-001.cpcl')
    expect(pageFile).not.toBeNull()
    const bytes = Buffer.from(await pageFile!.async('uint8array'))
    const header = Buffer.from(`CG ${Math.ceil(composite.width / 8)} ${composite.height} ${composite.x} ${composite.y} `)
    expect(bytes.indexOf(header)).toBeGreaterThanOrEqual(0)
    expect(bytes.length).toBeGreaterThan(Math.ceil(composite.width / 8) * composite.height)

    const savedDownload = page.waitForEvent('download')
    await page.getByRole('button', { name: '保存项目' }).click()
    const saved = await readFile(await (await savedDownload).path())
    const project = JSON.parse(saved.toString())
    expect(project.template.Rows[0].Cells[0].Elements[0]).toMatchObject({
      Type: 11, LinearType: item.type, Mode: item.mode, ModuleWidth: 2, LinearBarHeight: 38,
      Field: { Value: item.primary }, SecondaryField: { Value: '(91)LOT-EANUPC' },
    })

    await page.reload()
    await expect(page.locator('.capability-note')).toHaveClass(/ready/)
    const restoredPreview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
      response.status() === 200 && response.request().postDataJSON()?.template.Rows[0].Cells[0].Elements[0].LinearType === item.type)
    await page.getByTestId('template-import').setInputFiles({
      name: `${item.name.toLowerCase()}.json`, mimeType: 'application/json', buffer: saved,
    })
    expect((await (await restoredPreview).json()).pages).toEqual(layout.pages)
    await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
    await expect(page.getByLabel('Composite 线性组件', { exact: true })).toHaveValue(String(item.type))
    await expect(page.getByLabel('Composite 二维模式', { exact: true })).toHaveValue(String(item.mode))
    await expect(page.getByLabel('显示内容', { exact: true })).toHaveValue(item.primary)
    await expect(page.getByLabel('Composite 次数据', { exact: true })).toHaveValue('(91)LOT-EANUPC')
  })
}

test('旧 Host 缺少 EAN/UPC 特性时禁用四种制式并拒绝预览', async ({ page }) => {
  let requestedFeatures = ''
  page.on('request', request => {
    if (request.url().includes('/api/designer/preview'))
      requestedFeatures = request.headers()['x-easyprint-required-features'] ?? ''
  })
  await page.route('**/api/designer/**', async route => {
    const response = await route.fetch()
    const headers = response.headers()
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter(feature => feature.trim() !== 'gs1-composite-ean-upc' && feature.trim() !== 'gs1-composite-truncated' &&
        feature.trim() !== 'gs1-composite-stacked-omnidirectional').join(',')
    if (route.request().url().includes('/capabilities')) {
      const json = await response.json()
      if (json.gs1Composite) json.gs1Composite.linearTypes = json.gs1Composite.linearTypes
        .filter((type: string) => !['Ean8', 'Ean13', 'UpcA', 'UpcE', 'Gs1DataBarTruncated',
          'Gs1DataBarStackedOmnidirectional'].includes(type))
      await route.fulfill({ response, headers, json })
    } else await route.fulfill({ response, headers })
  })

  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.locator('.sample-picker select').selectOption('blank')
  await page.getByTestId('template-import').setInputFiles({
    name: 'ean-upc-old-host.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify({ Version: 3, Width: 400, Height: 240, Rows: [{ Cells: [{ Elements: [{
      Type: 11, LinearType: 4, Mode: 1, Field: { Value: '12345670' }, SecondaryField: { Value: '(91)OLD-HOST' },
      ModuleWidth: 2, LinearBarHeight: 38,
    }] }] }] })),
  })
  await expect(page.getByRole('region', { name: '打印诊断' })).toContainText(
    '打印服务缺少 EAN/UPC Composite 功能，请更新 Host。 · 代码：ContractFeatureUnavailable')
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
  for (const type of [4, 5, 6, 7]) {
    const option = page.getByLabel('Composite 线性组件', { exact: true }).locator(`option[value="${type}"]`)
    await expect(option).toHaveJSProperty('disabled', true)
    await expect(option).toHaveAttribute('title', '当前 Host 未协商 EAN/UPC Composite 功能')
  }
  await expect(page.getByText('当前 Host 未协商 EAN/UPC Composite；请更新 Host 后再预览或输出。'))
    .toBeVisible()
  expect(requestedFeatures).toContain('gs1-composite-ean-upc')
})
