import { expect, test } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'
import { readPreviewLayout } from './previewResponse'

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
    const layout = await readPreviewLayout(response)
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
    expect((await readPreviewLayout(await restoredPreview)).pages).toEqual(layout.pages)
    await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
    await expect(page.getByLabel('Composite 线性组件', { exact: true })).toHaveValue(String(item.type))
    await expect(page.getByLabel('Composite 二维模式', { exact: true })).toHaveValue(String(item.mode))
    await expect(page.getByLabel('显示内容', { exact: true })).toHaveValue(item.primary)
    await expect(page.getByLabel('Composite 次数据', { exact: true })).toHaveValue('(91)LOT-EANUPC')
  })
}

function waitForCompositePreview(page: import('@playwright/test').Page, primary: string, mode: number) {
  return page.waitForResponse(response => {
    if (!response.url().includes('/api/designer/preview') || response.status() !== 200) return false
    const element = response.request().postDataJSON()?.template.Rows[0].Cells[0].Elements[0]
    return element?.Type === 11 && element.Field?.Value === primary && element.Mode === mode
  })
}

function compositeLayout(layout: { pages: Array<{ elements: Array<Record<string, unknown>> }> }) {
  return layout.pages.flatMap(page => page.elements).find(element => element.type === 11)!
}

for (const item of [
  { name: 'EAN-8', type: 4, base: '12345670', addon2Width: 196, addon5Width: 250 },
  { name: 'UPC-A', type: 6, base: '012345678905', addon2Width: 254, addon5Width: 308 },
] as const) {
  test(`${item.name} Composite 附加码编辑、预览、导出与项目重载`, async ({ page }) => {
    await page.goto('/')
    await expect(page.locator('.capability-note')).toHaveClass(/ready/)
    await page.locator('.sample-picker select').selectOption('blank')
    await page.getByRole('button', { name: '组件与表格' }).click()
    await page.getByRole('button', { name: 'GS1 Composite', exact: true }).click()
    await page.getByLabel('Composite 线性组件', { exact: true }).selectOption(String(item.type))
    await page.getByLabel('Composite 旋转', { exact: true }).selectOption('0')
    await page.getByLabel('Composite 模块宽度', { exact: true }).fill('2')
    await page.getByLabel('Composite 线性条高', { exact: true }).fill('38')
    await page.getByLabel('显示内容', { exact: true }).fill(item.base)
    await page.getByLabel('Composite 次数据', { exact: true }).fill('(91)LOT-ADDON')

    const basePreview = waitForCompositePreview(page, item.base, 1)
    await page.getByLabel('Composite 二维模式', { exact: true }).selectOption('1')
    const baseLayout = await readPreviewLayout(await basePreview)
    const baseComposite = compositeLayout(baseLayout)

    const addon2 = `${item.base}+12`
    const addon2Preview = waitForCompositePreview(page, addon2, 1)
    await page.getByLabel('显示内容', { exact: true }).fill(addon2)
    const addon2Layout = await readPreviewLayout(await addon2Preview)
    const addon2Composite = compositeLayout(addon2Layout)
    expect(addon2Composite.width).toBe(item.addon2Width)
    expect(addon2Composite.height).toBe(baseComposite.height)

    if (item.name === 'EAN-8') {
      const switchedPreview = waitForCompositePreview(page, addon2, 2)
      await page.getByLabel('Composite 二维模式', { exact: true }).selectOption('2')
      const switchedLayout = await readPreviewLayout(await switchedPreview)
      expect(compositeLayout(switchedLayout).width).toBe(216)

      const addon5 = `${item.base}+51234`
      const addon5Preview = waitForCompositePreview(page, addon5, 2)
      await page.getByLabel('显示内容', { exact: true }).fill(addon5)
      const layout = await readPreviewLayout(await addon5Preview)
      const composite = compositeLayout(layout)
      expect(composite.width).toBe(270)
      expect(composite.height).toBe(compositeLayout(switchedLayout).height)
      await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
      expect((await addon5Preview).request().headers()['x-easyprint-required-features']).toContain('gs1-composite-ean-upc-addons')

      const output = page.waitForEvent('download')
      await page.getByRole('button', { name: '生成 CPCL' }).click()
      const archive = await JSZip.loadAsync(await readFile(await (await output).path()))
      const pageFile = archive.file('page-001.cpcl')
      expect(pageFile).not.toBeNull()
      const bytes = Buffer.from(await pageFile!.async('uint8array'))
      const header = Buffer.from(`CG ${Math.ceil(Number(composite.width) / 8)} ${composite.height} ${composite.x} ${composite.y} `)
      expect(bytes.indexOf(header)).toBeGreaterThanOrEqual(0)
      expect(bytes.length).toBeGreaterThan(Math.ceil(Number(composite.width) / 8) * Number(composite.height))

      const savedDownload = page.waitForEvent('download')
      await page.getByRole('button', { name: '保存项目' }).click()
      const saved = await readFile(await (await savedDownload).path())
      expect(JSON.parse(saved.toString()).template.Rows[0].Cells[0].Elements[0]).toMatchObject({
        Type: 11, LinearType: item.type, Mode: 2, ModuleWidth: 2, LinearBarHeight: 38,
        Field: { Value: addon5 }, SecondaryField: { Value: '(91)LOT-ADDON' },
      })

      await page.reload()
      await expect(page.locator('.capability-note')).toHaveClass(/ready/)
      const restoredPreview = waitForCompositePreview(page, addon5, 2)
      await page.getByTestId('template-import').setInputFiles({
        name: 'ean-8-addon.json', mimeType: 'application/json', buffer: saved,
      })
      expect((await readPreviewLayout(await restoredPreview)).pages).toEqual(layout.pages)
      await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
      await expect(page.getByLabel('显示内容', { exact: true })).toHaveValue(addon5)
      await expect(page.getByLabel('Composite 二维模式', { exact: true })).toHaveValue('2')
      return
    }

    const addon5 = `${item.base}+51234`
    const addon5Preview = waitForCompositePreview(page, addon5, 1)
    await page.getByLabel('显示内容', { exact: true }).fill(addon5)
    const layout = await readPreviewLayout(await addon5Preview)
    const composite = compositeLayout(layout)
    expect(composite.width).toBe(item.addon5Width)
    expect(composite.height).toBe(addon2Composite.height)
    expect((await addon5Preview).request().headers()['x-easyprint-required-features']).toContain('gs1-composite-ean-upc-addons')

    const output = page.waitForEvent('download')
    await page.getByRole('button', { name: '生成 CPCL' }).click()
    const archive = await JSZip.loadAsync(await readFile(await (await output).path()))
    expect(archive.file('page-001.cpcl')).not.toBeNull()

    const savedDownload = page.waitForEvent('download')
    await page.getByRole('button', { name: '保存项目' }).click()
    const saved = await readFile(await (await savedDownload).path())
    expect(JSON.parse(saved.toString()).template.Rows[0].Cells[0].Elements[0].Field.Value).toBe(addon5)
    await page.reload()
    await expect(page.locator('.capability-note')).toHaveClass(/ready/)
    const restoredPreview = waitForCompositePreview(page, addon5, 1)
    await page.getByTestId('template-import').setInputFiles({
      name: 'upc-a-addon.json', mimeType: 'application/json', buffer: saved,
    })
    expect((await readPreviewLayout(await restoredPreview)).pages).toEqual(layout.pages)
    await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
    await expect(page.getByLabel('显示内容', { exact: true })).toHaveValue(addon5)
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
      if (json.gs1Composite?.primaryFormats) json.gs1Composite.primaryFormats = json.gs1Composite.primaryFormats
        .filter((format: string) => format !== 'ean-upc-digits-with-addon')
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

test('旧 Host 保留 EAN/UPC 主码能力，但明确拒绝附加码', async ({ page }) => {
  let requestedFeatures = ''
  page.on('request', request => {
    if (request.url().includes('/api/designer/preview'))
      requestedFeatures = request.headers()['x-easyprint-required-features'] ?? ''
  })
  await page.route('**/api/designer/**', async route => {
    const response = await route.fetch()
    const headers = response.headers()
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter(feature => feature.trim() !== 'gs1-composite-ean-upc-addons').join(',')
    if (route.request().url().includes('/capabilities')) {
      const json = await response.json()
      if (json.gs1Composite?.primaryFormats) json.gs1Composite.primaryFormats = json.gs1Composite.primaryFormats
        .filter((format: string) => format !== 'ean-upc-digits-with-addon')
      await route.fulfill({ response, headers, json })
    } else await route.fulfill({ response, headers })
  })

  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.locator('.sample-picker select').selectOption('blank')
  await page.getByTestId('template-import').setInputFiles({
    name: 'ean-upc-addon-old-host.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify({ Version: 3, Width: 400, Height: 240, Rows: [{ Cells: [{ Elements: [{
      Type: 11, LinearType: 4, Mode: 1, Field: { Value: '12345670+12' }, SecondaryField: { Value: '(91)OLD-HOST' },
      ModuleWidth: 2, LinearBarHeight: 38,
    }] }] }] })),
  })
  await expect(page.getByRole('region', { name: '打印诊断' })).toContainText(
    '打印服务缺少 EAN/UPC Composite 附加码功能，请更新 Host。 · 代码：ContractFeatureUnavailable')
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
  await expect(page.getByLabel('显示内容', { exact: true })).toHaveValue('12345670+12')
  await expect(page.getByText('当前 Host 未协商 EAN/UPC Composite 附加码；原输入会保留，但带附加码的预览与输出不可用。'))
    .toBeVisible()
  expect(requestedFeatures).toContain('gs1-composite')
  expect(requestedFeatures).toContain('gs1-composite-ean-upc')
  expect(requestedFeatures).toContain('gs1-composite-ean-upc-addons')

  const noAddonPreview = waitForCompositePreview(page, '12345670', 1)
  await page.getByLabel('显示内容', { exact: true }).fill('12345670')
  await noAddonPreview
  await expect(page.getByText('当前 Host 未协商 EAN/UPC Composite 附加码；原输入会保留，但带附加码的预览与输出不可用。'))
    .toHaveCount(0)
})
