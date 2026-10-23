import { expect, test } from '@playwright/test'
import { readFile } from 'node:fs/promises'

const longExpandedData = '(91)1234567890123456789012345678901234'
const secondaryData = '(20)12'

function previewElement(layout: { pages: Array<{ elements: Array<{ type: number; width: number; height: number }> }> }, type: number) {
  const element = layout.pages.flatMap((page) => page.elements).find((candidate) => candidate.type === type)
  expect(element).toBeDefined()
  return element!
}

test('普通 Expanded Stacked 自定义列数改变真实尺寸并通过导出和保存导入往返', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  const template = { Version: 2, Width: 640, Height: 400, Rows: [{ Cells: [{ Elements: [
    { Type: 4, BarcodeType: 31, Height: 35, NarrowModuleWidth: 2, HumanReadable: false,
      Rotation: 0, Field: { Value: longExpandedData } },
  ] }] }] }
  await page.getByTestId('template-import').setInputFiles({ name: 'expanded-stacked-columns.json',
    mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)) })
  await expect(page.locator('.tree-row.tree-element').first()).toContainText(longExpandedData)
  const baselineResponse = page.waitForResponse((response) => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && response.request().postDataJSON()?.protocol === 'EscPos' &&
    response.request().postDataJSON()?.escPosLayoutMode === 'Raster' &&
    JSON.stringify(response.request().postDataJSON()?.template).includes(longExpandedData))
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  const baseline = previewElement(await (await baselineResponse).json(), 4)
  await expect(page.getByLabel('Expanded Stacked 每行列数', { exact: true })).toHaveValue('default')

  const configuredResponse = page.waitForResponse((response) => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && response.request().postDataJSON()?.template.Rows[0].Cells[0].Elements[0]
      .ExpandedStackedColumns === 3)
  await page.getByLabel('Expanded Stacked 每行列数', { exact: true }).selectOption('3')
  const configured = previewElement(await (await configuredResponse).json(), 4)
  expect(baseline).toMatchObject({ width: 244, height: 117 })
  expect(configured).toMatchObject({ width: 342, height: 76 })

  const jsonDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const exported = JSON.parse((await readFile(await (await jsonDownload).path())).toString())
  expect(exported.Rows[0].Cells[0].Elements[0]).toMatchObject({ BarcodeType: 31, ExpandedStackedColumns: 3 })

  const projectDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const project = await readFile(await (await projectDownload).path())
  expect(JSON.parse(project.toString()).template.Rows[0].Cells[0].Elements[0])
    .toMatchObject({ BarcodeType: 31, ExpandedStackedColumns: 3 })

  await page.reload()
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.getByTestId('template-import').setInputFiles({ name: 'expanded-stacked-columns-project.json',
    mimeType: 'application/json', buffer: project })
  await expect(page.locator('.tree-row.tree-element').first()).toContainText(longExpandedData)
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  await expect(page.getByLabel('Expanded Stacked 每行列数', { exact: true })).toHaveValue('3')
})

test('Expanded Stacked Composite 自定义列数改变真实尺寸并通过导出和项目往返', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await page.locator('.sample-picker select').selectOption('blank')
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.getByRole('button', { name: 'GS1 Composite', exact: true }).click()
  await page.getByLabel('Composite 线性组件', { exact: true }).selectOption('31')
  await page.getByLabel('Composite 旋转', { exact: true }).selectOption('0')
  await page.getByLabel('Composite 模块宽度', { exact: true }).fill('2')
  await page.getByLabel('Composite 线性条高', { exact: true }).fill('35')
  await page.getByLabel('显示内容', { exact: true }).fill(longExpandedData)
  await page.getByLabel('Composite 次数据', { exact: true }).fill(secondaryData)

  const baselineResponse = page.waitForResponse((response) => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && JSON.stringify(response.request().postDataJSON()?.template).includes(secondaryData) &&
    response.request().postDataJSON()?.template.Rows[0].Cells[0].Elements[0].Mode === 1)
  await page.getByLabel('Composite 二维模式', { exact: true }).selectOption('1')
  const baselineReply = await baselineResponse
  expect(baselineReply.request().headers()['x-easyprint-required-features'])
    .toContain('gs1-composite-expanded-stacked')
  const baseline = previewElement(await baselineReply.json(), 11)
  await expect(page.getByLabel('Composite Expanded Stacked 每行列数', { exact: true })).toHaveValue('default')

  const configuredResponse = page.waitForResponse((response) => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && response.request().postDataJSON()?.template.Rows[0].Cells[0].Elements[0]
      .ExpandedStackedColumns === 3)
  await page.getByLabel('Composite Expanded Stacked 每行列数', { exact: true }).selectOption('3')
  const configuredReply = await configuredResponse
  expect(configuredReply.request().headers()['x-easyprint-required-features'])
    .toContain('gs1-databar-expanded-stacked-columns')
  const configured = previewElement(await configuredReply.json(), 11)
  expect(baseline).toMatchObject({ width: 204, height: 131 })
  expect(configured).toMatchObject({ width: 302, height: 90 })

  const jsonDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const exported = JSON.parse((await readFile(await (await jsonDownload).path())).toString())
  expect(exported.Rows[0].Cells[0].Elements[0]).toMatchObject({ LinearType: 31, ExpandedStackedColumns: 3 })
  const projectDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const project = await readFile(await (await projectDownload).path())
  expect(JSON.parse(project.toString()).template.Rows[0].Cells[0].Elements[0])
    .toMatchObject({ LinearType: 31, ExpandedStackedColumns: 3 })

  await page.reload()
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.getByTestId('template-import').setInputFiles({ name: 'expanded-stacked-composite-project.json',
    mimeType: 'application/json', buffer: project })
  await expect(page.locator('.tree-row.tree-element').first()).toContainText('GS1 Composite')
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
  await expect(page.getByLabel('Composite Expanded Stacked 每行列数', { exact: true })).toHaveValue('3')
})

test('旧 Host 的默认两列仍可用，导入显式列数时保留配置并提示缺失能力', async ({ page }) => {
  await page.route('**/api/designer/**', async (route) => {
    const response = await route.fetch()
    const headers = response.headers()
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter((feature) => feature.trim() !== 'gs1-databar-expanded-stacked-columns').join(',')
    if (route.request().url().includes('/capabilities')) {
      const json = await response.json()
      delete json.expandedStackedColumns
      if (json.gs1Composite) delete json.gs1Composite.expandedStackedColumns
      await route.fulfill({ response, headers, json })
    } else await route.fulfill({ response, headers })
  })
  let previewRequests = 0
  page.on('request', (request) => {
    if (request.url().includes('/api/designer/preview')) previewRequests += 1
  })

  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  const ordinary = (columns?: number) => ({ Version: 2, Width: 640, Height: 400, Rows: [{ Cells: [{ Elements: [
    { Type: 4, BarcodeType: 31, ...(columns === undefined ? {} : { ExpandedStackedColumns: columns }),
      Height: 35, NarrowModuleWidth: 2, Field: { Value: longExpandedData } },
  ] }] }] })
  await page.getByTestId('template-import').setInputFiles({ name: 'expanded-stacked-default.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(ordinary())) })
  await expect(page.locator('.tree-row.tree-element').first()).toContainText(longExpandedData)
  const rasterPreview = page.waitForResponse((response) => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && response.request().postDataJSON()?.protocol === 'EscPos' &&
    response.request().postDataJSON()?.escPosLayoutMode === 'Raster')
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await rasterPreview
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  await expect(page.getByLabel('Expanded Stacked 每行列数', { exact: true })).toHaveValue('default')

  const beforeExplicitImport = previewRequests
  await page.getByTestId('template-import').setInputFiles({ name: 'expanded-stacked-configured.json',
    mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(ordinary(4))) })
  await expect(page.locator('.tree-row.tree-element').first()).toContainText(longExpandedData)
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  await expect(page.getByLabel('Expanded Stacked 每行列数', { exact: true })).toHaveValue('4')
  await expect(page.getByText('当前 Host 未协商 Expanded Stacked 自定义列数功能', { exact: false })).toBeVisible()
  await expect.poll(() => previewRequests).toBe(beforeExplicitImport)

  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const exported = JSON.parse((await readFile(await (await download).path())).toString())
  expect(exported.Rows[0].Cells[0].Elements[0].ExpandedStackedColumns).toBe(4)
})
