import { expect, test } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'
import { readPreviewLayout } from './previewResponse'

test('GS1 DataBar Stacked Composite 编辑、预览、输出和项目往返', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.locator('.sample-picker select').selectOption('blank')
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.getByRole('button', { name: 'GS1 Composite', exact: true }).click()
  await page.getByLabel('Composite 线性组件', { exact: true }).selectOption('30')
  await expect(page.getByLabel('Composite 二维模式', { exact: true })).toHaveValue('0')
  await expect(page.getByLabel('Composite 二维模式', { exact: true }).locator('option')).toHaveText(['Auto', 'CC-A', 'CC-B'])
  await expect(page.getByLabel('Composite 线性条高', { exact: true })).toHaveAttribute('min', '24')
  await page.getByLabel('Composite 模块宽度', { exact: true }).fill('3')
  await page.getByLabel('Composite 线性条高', { exact: true }).fill('48')
  await expect(page.getByLabel('Composite 线性条高', { exact: true })).toHaveAttribute('min', '36')
  await page.getByLabel('Composite 旋转', { exact: true }).selectOption('0')
  await page.getByLabel('显示内容', { exact: true }).fill('12345678901231')

  const preview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && JSON.stringify(response.request().postDataJSON()?.template).includes('(10)LOT-STACKED'))
  await page.getByLabel('Composite 二维模式', { exact: true }).selectOption('2')
  await page.getByLabel('Composite 次数据', { exact: true }).fill('(10)LOT-STACKED(17)270101')
  const response = await preview
  expect(response.request().headers()['x-easyprint-required-features']).toContain('gs1-composite-stacked')
  expect(response.request().postDataJSON()?.template).toMatchObject({ Rows: [{ Cells: [{ Elements: [{
    LinearType: 30, Mode: 2, ModuleWidth: 3, LinearBarHeight: 48,
    Field: { Value: '12345678901231' },
    SecondaryField: { Value: '(10)LOT-STACKED(17)270101' },
  }] }] }] })
  const layout = await readPreviewLayout(response)
  const element = layout.pages.flatMap((p: { elements: any[] }) => p.elements).find((e: { type: number }) => e.type === 11)
  expect(element.width).toBe(56 * 3)
  expect(element.height).toBeGreaterThan(48)
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await expect(page.locator('.status-spinner')).toHaveCount(0)

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
  expect(project.template.Rows[0].Cells[0].Elements[0]).toMatchObject({ Type: 11, LinearType: 30, Mode: 2,
    ModuleWidth: 3, LinearBarHeight: 48, Field: { Value: '12345678901231' },
    SecondaryField: { Value: '(10)LOT-STACKED(17)270101' } })

  await page.reload()
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  const restoredPreview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && JSON.stringify(response.request().postDataJSON()?.template).includes('(10)LOT-STACKED'))
  await page.getByTestId('template-import').setInputFiles({ name: 'stacked.json', mimeType: 'application/json', buffer: saved })
  expect((await readPreviewLayout(await restoredPreview)).pages).toEqual(layout.pages)
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
  await expect(page.getByLabel('Composite 线性组件', { exact: true })).toHaveValue('30')
  await expect(page.getByLabel('Composite 二维模式', { exact: true })).toHaveValue('2')
  await expect(page.getByLabel('Composite 模块宽度', { exact: true })).toHaveValue('3')
  await expect(page.getByLabel('Composite 线性条高', { exact: true })).toHaveValue('48')
  await expect(page.getByLabel('显示内容', { exact: true })).toHaveValue('12345678901231')
  await expect(page.getByLabel('Composite 次数据', { exact: true })).toHaveValue('(10)LOT-STACKED(17)270101')
})

test('旧 Host 缺少 Stacked feature 和线性制式时禁用选项并拒绝预览', async ({ page }) => {
  await page.route('**/api/designer/**', async route => {
    const response = await route.fetch()
    const headers = response.headers()
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter(feature => !['gs1-databar-expanded-stacked-columns', 'gs1-databar-expanded-stacked-rows'].includes(feature.trim())).join(',')
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter(feature => feature.trim() !== 'gs1-composite-stacked' && feature.trim() !== 'gs1-composite-expanded-stacked' &&
        feature.trim() !== 'gs1-composite-ean-upc' && feature.trim() !== 'gs1-composite-truncated' &&
        feature.trim() !== 'gs1-composite-ean-upc-addons' &&
        feature.trim() !== 'gs1-composite-stacked-omnidirectional').join(',')
    if (route.request().url().includes('/capabilities')) {
      const json = await response.json()
      delete json.expandedStackedColumns
      delete json.expandedStackedRows
      if (json.gs1Composite) delete json.gs1Composite.expandedStackedColumns
      if (json.gs1Composite) delete json.gs1Composite.expandedStackedRows
      if (json.gs1Composite) json.gs1Composite.linearTypes = json.gs1Composite.linearTypes
        .filter((type: string) => type !== 'Gs1DataBarStacked' && type !== 'Gs1DataBarExpandedStacked' &&
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
  const stackedOption = page.getByLabel('Composite 线性组件', { exact: true }).locator('option[value="30"]')
  await expect(stackedOption).toHaveJSProperty('disabled', true)
  await expect(stackedOption).toHaveAttribute('title', '当前 Host 未协商 GS1 DataBar Stacked Composite')

  const template = { Version: 3, Width: 600, Height: 300, Rows: [{ Cells: [{ Elements: [
    { Type: 11, LinearType: 30, Mode: 1, Field: { Value: '12345678901231' },
      SecondaryField: { Value: '(10)LOT-STACKED' }, ModuleWidth: 2, LinearBarHeight: 40 },
  ] }] }] }
  await page.getByTestId('template-import').setInputFiles({ name: 'stacked.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(template)) })
  await expect(page.getByText('打印服务缺少 GS1 DataBar Stacked Composite 功能，请更新 Host。', { exact: false }).first()).toBeVisible()
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
  await expect(page.getByLabel('Composite 线性组件', { exact: true })).toHaveValue('30')
  await expect(page.getByText('当前 Host 未协商 GS1 DataBar Stacked Composite；请更新 Host 后再预览或输出。')).toBeVisible()
})
