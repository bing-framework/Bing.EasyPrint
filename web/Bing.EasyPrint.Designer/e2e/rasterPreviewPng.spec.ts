import { expect, test } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'

type PngSize = { width: number; height: number }

function pngSize(buffer: Buffer): PngSize {
  expect(buffer.subarray(0, 8)).toEqual(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]))
  return { width: buffer.readUInt32BE(16), height: buffer.readUInt32BE(20) }
}

function ordinaryTemplate(height = 30) {
  return {
    Version: 2, Width: 400, Height: 200,
    Rows: [{ Cells: [{ Elements: [{
      Type: 4, BarcodeType: 0, Height: height, NarrowModuleWidth: 2, HumanReadable: false,
      Field: { Value: '12345' },
    }] }] }],
  }
}

async function downloadElement(page: import('@playwright/test').Page): Promise<Buffer> {
  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: '下载码图' }).click()
  return readFile(await (await download).path())
}

test('真实码图预览随参数更新，元素/页面 PNG 可解码且保存导入后一致', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await expect(page.getByRole('button', { name: '下载码图' })).toBeVisible()
  await expect(page.getByRole('button', { name: '下载页面 PNG' })).toBeVisible()

  await page.getByTestId('template-import').setInputFiles({
    name: 'raster-preview.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(ordinaryTemplate(30))),
  })
  await expect(page.locator('.tree-row.tree-element').first()).toBeVisible()
  const firstImageResponse = page.waitForResponse((response) =>
    response.url().includes('/api/designer/element-png') && response.status() === 200)
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  const firstImage = await firstImageResponse
  expect(firstImage.request().headers()['x-easyprint-required-features']).toContain('raster-element-png')
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  const firstSize = pngSize(await downloadElement(page))
  expect(firstSize.width).toBeGreaterThan(10)
  expect(firstSize.height).toBeGreaterThan(0)

  const pageDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '下载页面 PNG' }).click()
  const pageFile = await readFile(await (await pageDownload).path())
  expect(pngSize(pageFile)).toEqual({ width: 400, height: 200 })

  const changedImageResponse = page.waitForResponse((response) =>
    response.url().includes('/api/designer/element-png') && response.status() === 200 &&
    response.request().postDataJSON()?.template?.Rows?.[0]?.Cells?.[0]?.Elements?.[0]?.Height === 60)
  await page.getByTestId('template-import').setInputFiles({
    name: 'raster-preview-tall.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(ordinaryTemplate(60))),
  })
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await changedImageResponse
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  const changedSize = pngSize(await downloadElement(page))
  expect(changedSize.height).toBeGreaterThan(firstSize.height)

  const projectDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const project = await readFile(await (await projectDownload).path())
  await page.reload()
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  const restoredImageResponse = page.waitForResponse((response) =>
    response.url().includes('/api/designer/element-png') && response.status() === 200)
  await page.getByTestId('template-import').setInputFiles({
    name: 'raster-preview-project.json', mimeType: 'application/json', buffer: project,
  })
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await expect(page.locator('.tree-row.tree-element').first()).toBeVisible()
  await restoredImageResponse
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  const restoredSize = pngSize(await downloadElement(page))
  expect(restoredSize).toEqual(changedSize)

  await page.setViewportSize({ width: 390, height: 844 })
  await expect(page.locator('.canvas-workspace')).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true)
})

test('多元素矩形与条码的码图选择不串位', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  const template = {
    Version: 2, Width: 400, Height: 200,
    Rows: [{ Cells: [{ Elements: [
      { Type: 7, Width: 40, Height: 30 },
      { Type: 4, BarcodeType: 0, Height: 40, NarrowModuleWidth: 2, HumanReadable: false,
        Field: { Value: '12345' } },
    ] }] }],
  }
  await page.getByTestId('template-import').setInputFiles({
    name: 'rectangle-then-barcode.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(template)),
  })
  const rasterPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.status() === 200 &&
    response.request().postDataJSON()?.escPosLayoutMode === 'Raster')
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await rasterPreview
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const rows = page.locator('.tree-row.tree-element .tree-select')
  await expect(rows).toHaveCount(2)
  await rows.nth(0).click()
  expect(pngSize(await downloadElement(page))).toEqual({ width: 40, height: 30 })
  await rows.nth(1).click()
  const barcode = pngSize(await downloadElement(page))
  expect(barcode.width).toBeGreaterThan(20)
  expect(barcode.height).toBeGreaterThanOrEqual(40)
})
test('非 Raster 的 1D 元素不再宣称准确码图并保持布局示意', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.getByTestId('template-import').setInputFiles({
    name: 'cpcl-barcode.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(ordinaryTemplate(30))),
  })
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  await expect(page.getByRole('button', { name: '下载码图' })).toBeDisabled()
  await expect(page.getByRole('button', { name: '下载码图' }))
    .toHaveAttribute('title', /没有准确/)
  await expect(page.locator('.tree-row.tree-element .tree-select').first()).toBeVisible()
})
test('多页页面 PNG 下载为每页 PNG 的 ZIP', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  const repeated = {
    Version: 3, Width: 400, Height: 60,
    Rows: [{
      Height: 60, RepeatSource: 'Items', Cells: [{ Elements: [{
        Type: 4, BarcodeType: 0, Height: 30, NarrowModuleWidth: 2, HumanReadable: false,
        Field: { Name: 'Value' },
      }] }],
    }],
  }
  await page.getByTestId('template-import').setInputFiles({
    name: 'raster-pages.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(repeated)),
  })
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  const preview = page.waitForResponse((response) => response.url().includes('/api/designer/preview') &&
    response.status() === 200)
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify({
    Items: [{ Value: '12345' }, { Value: '67890' }],
  }))
  await preview
  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: '下载页面 PNG' }).click()
  const zipFile = await readFile(await (await download).path())
  const zip = await JSZip.loadAsync(zipFile)
  const entries = Object.values(zip.files).filter((entry) => entry.name.endsWith('.png'))
  expect(entries.length).toBe(2)
  for (const entry of entries) expect(pngSize(Buffer.from(await entry.async('uint8array')))).toEqual({ width: 400, height: 60 })
})

test('旧 Host 缺少 PNG 能力时保留布局预览并禁用下载', async ({ page }) => {
  await page.route('**/api/designer/**', async (route) => {
    const response = await route.fetch()
    const headers = response.headers()
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter((feature) => feature.trim() !== 'raster-element-png' && feature.trim() !== 'raster-page-png')
      .join(',')
    if (route.request().url().includes('/capabilities')) {
      const json = await response.json()
      delete json.rasterPreview
      await route.fulfill({ response, headers, json })
    } else await route.fulfill({ response, headers })
  })
  let imageRequests = 0
  page.on('request', (request) => {
    if (request.url().includes('/api/designer/element-png') ||
      request.url().includes('/api/designer/page-png')) imageRequests += 1
  })
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.getByTestId('template-import').setInputFiles({
    name: 'raster-preview-old-host.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(ordinaryTemplate(30))),
  })
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await expect(page.getByRole('button', { name: '下载码图' })).toBeDisabled()
  await expect(page.getByRole('button', { name: '下载页面 PNG' })).toBeDisabled()
  await expect.poll(() => imageRequests).toBe(0)
})
