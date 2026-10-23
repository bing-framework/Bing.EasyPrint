import { expect, test } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'

test('普通 Expanded Stacked 在 Raster 模式按每行高度编辑并保留真实多行布局', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  const template = { Version: 3, Width: 300, Height: 300, Rows: [{ Cells: [{ Elements: [
    { Type: 4, BarcodeType: 31, Height: 34, NarrowModuleWidth: 2, HumanReadable: false,
      Rotation: 0, Field: { Value: '(01)09501101530003(17)140704(10)AB-123' } },
  ] }] }] }
  const initial = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && JSON.stringify(response.request().postDataJSON()?.template).includes('AB-123'))
  await page.getByTestId('template-import').setInputFiles({ name: 'ordinary-expanded-stacked.json',
    mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)) })
  await expect(page.getByRole('button', { name: '页面 300 × 300' })).toBeVisible()
  // Plain template imports use the default CPCL output settings; select Raster after import.
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await initial
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  await expect(page.getByRole('combobox', { name: '制式', exact: true })).toHaveValue('31')
  const dimensions = page.locator('.inspector-section').filter({ hasText: '位置与尺寸' }).locator('.numeric-grid input')
  await expect(dimensions.nth(3)).toHaveValue('34')
  const preview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && response.request().postDataJSON()?.template.Rows[0].Cells[0].Elements[0].Height === 35)
  await dimensions.nth(3).fill('35')
  const layout = await (await preview).json()
  const barcode = layout.pages[0].elements.find((element: { type: number }) => element.type === 4)
  expect(barcode.height).toBe(117)
  expect(barcode.width).toBeGreaterThanOrEqual(204)
  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const saved = JSON.parse((await readFile(await (await download).path())).toString())
  expect(saved.Rows[0].Cells[0].Elements[0]).toMatchObject({ BarcodeType: 31, Height: 35, NarrowModuleWidth: 2 })
  // Rotation and quiet zones must not turn the per-row height into total occupied height.
  const rotated = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && response.request().postDataJSON()?.template.Rows[0].Cells[0].Elements[0].Rotation === 90)
  await page.getByLabel('元素旋转').selectOption('90')
  const rotatedLayout = await (await rotated).json()
  expect(rotatedLayout.pages[0].elements.find((element: { type: number }) => element.type === 4).width).toBe(117)
  await expect(dimensions.nth(3)).toHaveValue('35')
})

test('GS1 DataBar Expanded Stacked 编辑、预览、输出和项目往返', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.locator('.sample-picker select').selectOption('blank')
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.getByRole('button', { name: 'GS1 Composite', exact: true }).click()
  await page.getByLabel('Composite 线性组件', { exact: true }).selectOption('31')
  await expect(page.getByLabel('Composite 二维模式', { exact: true }).locator('option')).toHaveText(['Auto', 'CC-A', 'CC-B'])
  await page.getByLabel('Composite 旋转', { exact: true }).selectOption('0')
  await page.getByLabel('Composite 模块宽度', { exact: true }).fill('2')
  await page.getByLabel('Composite 线性条高', { exact: true }).fill('34')
  await page.getByLabel('显示内容', { exact: true }).fill('(01)09506000134352(10)LOT-A')
  await page.getByLabel('Composite 次数据', { exact: true }).fill('(91)GS1-EXP-STK(17)270101')

  const preview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && JSON.stringify(response.request().postDataJSON()?.template).includes('(91)GS1-EXP-STK'))
  await page.getByLabel('Composite 二维模式', { exact: true }).selectOption('2')
  const response = await preview
  expect(response.request().headers()['x-easyprint-required-features']).toContain('gs1-composite-expanded-stacked')
  expect(response.request().postDataJSON()?.template).toMatchObject({ Rows: [{ Cells: [{ Elements: [{
    LinearType: 31, Mode: 2, ModuleWidth: 2, LinearBarHeight: 34,
    Field: { Value: '(01)09506000134352(10)LOT-A' },
    SecondaryField: { Value: '(91)GS1-EXP-STK(17)270101' },
  }] }] }] })
  const layout = await response.json()
  const element = layout.pages.flatMap((p: { elements: any[] }) => p.elements)
    .find((candidate: { type: number }) => candidate.type === 11)
  expect(element.width).toBeGreaterThan(0)
  expect(element.height).toBeGreaterThanOrEqual(34)
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')

  const output = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 CPCL' }).click()
  const archive = await JSZip.loadAsync(await readFile(await (await output).path()))
  const bytes = Buffer.from(await archive.file('page-001.cpcl')!.async('uint8array'))
  const header = Buffer.from(`CG ${Math.ceil(element.width / 8)} ${element.height} ${element.x} ${element.y} `)
  expect(bytes.indexOf(header)).toBeGreaterThanOrEqual(0)
  expect(bytes.length).toBeGreaterThan(Math.ceil(element.width / 8) * element.height)

  const savedDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const saved = await readFile(await (await savedDownload).path())
  const project = JSON.parse(saved.toString())
  expect(project.template.Rows[0].Cells[0].Elements[0]).toMatchObject({
    Type: 11, LinearType: 31, Mode: 2, ModuleWidth: 2, LinearBarHeight: 34,
    Field: { Value: '(01)09506000134352(10)LOT-A' },
    SecondaryField: { Value: '(91)GS1-EXP-STK(17)270101' },
  })

  await page.reload()
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  const restoredPreview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && JSON.stringify(response.request().postDataJSON()?.template).includes('(91)GS1-EXP-STK'))
  await page.getByTestId('template-import').setInputFiles({
    name: 'expanded-stacked.json', mimeType: 'application/json', buffer: saved,
  })
  expect((await (await restoredPreview).json()).pages).toEqual(layout.pages)
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
  await expect(page.getByLabel('Composite 线性组件', { exact: true })).toHaveValue('31')
  await expect(page.getByLabel('Composite 二维模式', { exact: true })).toHaveValue('2')
  await expect(page.getByLabel('Composite 模块宽度', { exact: true })).toHaveValue('2')
  await expect(page.getByLabel('Composite 线性条高', { exact: true })).toHaveValue('34')
  await expect(page.getByLabel('显示内容', { exact: true })).toHaveValue('(01)09506000134352(10)LOT-A')
  await expect(page.getByLabel('Composite 次数据', { exact: true })).toHaveValue('(91)GS1-EXP-STK(17)270101')
})

test('旧 Host 缺少 Expanded Stacked 特性时禁用制式并说明降级原因', async ({ page }) => {
  await page.route('**/api/designer/**', async route => {
    const response = await route.fetch()
    const headers = response.headers()
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter(feature => !['gs1-databar-expanded-stacked-columns', 'gs1-databar-expanded-stacked-rows'].includes(feature.trim())).join(',')
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter(feature => feature.trim() !== 'gs1-composite-expanded-stacked' && feature.trim() !== 'gs1-composite-ean-upc' &&
        feature.trim() !== 'gs1-composite-ean-upc-addons' &&
        feature.trim() !== 'gs1-composite-truncated' && feature.trim() !== 'gs1-composite-stacked-omnidirectional').join(',')
    if (route.request().url().includes('/capabilities')) {
      const json = await response.json()
      delete json.expandedStackedColumns
      delete json.expandedStackedRows
      if (json.gs1Composite) delete json.gs1Composite.expandedStackedColumns
      if (json.gs1Composite) delete json.gs1Composite.expandedStackedRows
      if (json.gs1Composite) json.gs1Composite.linearTypes = json.gs1Composite.linearTypes
        .filter((type: string) => type !== 'Gs1DataBarExpandedStacked' &&
          !['Ean8', 'Ean13', 'UpcA', 'UpcE', 'Gs1DataBarTruncated', 'Gs1DataBarStackedOmnidirectional'].includes(type))
      if (json.gs1Composite?.primaryFormats) json.gs1Composite.primaryFormats = json.gs1Composite.primaryFormats
        .filter((format: string) => format !== 'ean-upc-digits-with-addon')
      await route.fulfill({ response, headers, json })
    } else await route.fulfill({ response, headers })
  })

  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.locator('.sample-picker select').selectOption('blank')
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.getByRole('button', { name: 'GS1 Composite', exact: true }).click()
  const option = page.getByLabel('Composite 线性组件', { exact: true }).locator('option[value="31"]')
  await expect(option).toHaveJSProperty('disabled', true)
  await expect(option).toHaveAttribute('title', '当前 Host 未协商 GS1 DataBar Expanded Stacked Composite')

  const template = { Version: 3, Width: 600, Height: 300, Rows: [{ Cells: [{ Elements: [
    { Type: 11, LinearType: 31, Mode: 1, Field: { Value: '(01)09506000134352(10)LOT-A' },
      SecondaryField: { Value: '(91)ABC' }, ModuleWidth: 2, LinearBarHeight: 34 },
  ] }] }] }
  await page.getByTestId('template-import').setInputFiles({ name: 'expanded-stacked.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(template)) })
  await expect(page.getByText('打印服务缺少 GS1 DataBar Expanded Stacked Composite 功能，请更新 Host。', { exact: false }).first())
    .toBeVisible()
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
  await expect(page.getByText('当前 Host 未协商 GS1 DataBar Expanded Stacked Composite；请更新 Host 后再预览或输出。'))
    .toBeVisible()
})
