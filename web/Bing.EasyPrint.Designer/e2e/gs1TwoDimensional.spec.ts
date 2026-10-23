import { expect, test } from '@playwright/test'
import { readFile } from 'node:fs/promises'

const qrTemplate = {
  Version: 2, Width: 400, Height: 400,
  Rows: [{ Cells: [{ Elements: [{
    Type: 3, UnitWidth: 4, ErrorLevel: 'M', Gs1: true,
    Field: { Value: '(01)12345678901231(10)LOT%20A' },
  }] }] }],
}

test('GS1 QR 与 DataMatrix 可创建、预览、下载并保存项目', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)

  await page.getByRole('button', { name: '二维码', exact: true }).first().click()
  const qrRow = page.locator('.tree-row.tree-element .tree-select').filter({ hasText: '二维码' }).first()
  await qrRow.click()
  await page.getByLabel('显示内容').fill('(01)12345678901231(10)LOT%20A')
  const qrImage = page.waitForResponse((response) =>
    response.url().includes('/api/designer/element-png') && response.status() === 200)
  await page.getByLabel('GS1 QR Code').check()
  await qrImage

  await page.locator('.advanced-toggle').click()
  await page.getByRole('button', { name: 'PDF417 / DM', exact: true }).first().click()
  const matrixRow = page.locator('.tree-row.tree-element .tree-select').filter({ hasText: '二维点阵' }).first()
  await matrixRow.click()
  await page.getByLabel('显示内容').fill('(01)12345678901231(10)LOT%20A')
  const matrixImage = page.waitForResponse((response) =>
    response.url().includes('/api/designer/element-png') && response.status() === 200)
  await page.getByLabel('GS1 DataMatrix').check()
  await matrixImage
  await expect(page.getByLabel('GS1 DataMatrix')).toBeChecked()

  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const saved = JSON.parse(await readFile(await (await download).path(), 'utf8'))
  const elements = saved.Rows[0].Cells[0].Elements
  expect(elements.find((item: { Type: number }) => item.Type === 3)).toMatchObject({ Gs1: true })
  expect(elements.find((item: { Type: number }) => item.Type === 10)).toMatchObject({ Gs1: true, Format: 'DataMatrix' })

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const pageDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '下载页面 PNG' }).click()
  expect((await pageDownload).suggestedFilename()).toMatch(/easyprint-page-1.png|easyprint-pages-png.zip/)

  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Cpcl')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const renderResponse = page.waitForResponse((response) =>
    response.url().includes('/api/designer/render') && response.request().method() === 'POST')
  const protocolDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 CPCL' }).click()
  expect((await renderResponse).status()).toBe(200)
  expect((await protocolDownload).suggestedFilename()).toContain('.zip')

  const projectDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const project = await readFile(await (await projectDownload).path())
  await page.reload()
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.getByTestId('template-import').setInputFiles({
    name: 'gs1-project.json', mimeType: 'application/json', buffer: project,
  })
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: '二维点阵' }).first().click()
  await expect(page.getByLabel('GS1 DataMatrix')).toBeChecked()
})

test('旧 Host 缺少 GS1 二维能力时禁用选项并说明原因', async ({ page }) => {
  await page.route('**/api/designer/capabilities', async (route) => {
    const response = await route.fetch()
    const body = await response.json()
    const headers = response.headers()
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter((feature) => feature.trim() !== 'gs1-two-dimensional' && feature.trim() !== 'gs1-association-validation')
      .join(',')
    await route.fulfill({ response, headers, json: body })
  })
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.getByTestId('template-import').setInputFiles({
    name: 'gs1-qr.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(qrTemplate)),
  })
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: '二维码' }).first().click()
  await expect(page.getByLabel('GS1 QR Code')).toBeDisabled()
  await expect(page.locator('.field-note').filter({ hasText: 'GS1 QR Code' })).toContainText('不支持')
})

test('严格 GS1 关联校验拒绝缺失关系并保留默认模式', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.getByTestId('template-import').setInputFiles({
    name: 'gs1-invalid.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify({
      Version: 2, Width: 400, Height: 300,
      Rows: [{ Cells: [{ Elements: [{
        Type: 3, UnitWidth: 4, ErrorLevel: 'M', Gs1: true,
        Field: { Value: '(02)12345678901231' },
      }] }] }],
    })),
  })
  await page.locator('.output-settings-button').click()
  await page.getByLabel('严格 GS1 关联校验').check()
  await expect(page.locator('.diagnostic-panel')).toContainText('37')
})

test('严格关联对 DataMatrix 拒绝缺 37 并可在修正后通过', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.getByTestId('template-import').setInputFiles({
    name: 'gs1-dm-invalid.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify({
      Version: 2, Width: 400, Height: 300,
      Rows: [{ Cells: [{ Elements: [{
        Type: 10, Format: 'DataMatrix', Gs1: true, Width: 96, Height: 96,
        Field: { Value: '(02)12345678901231' },
      }] }] }],
    })),
  })
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: '二维点阵' }).first().click()
  await page.locator('.output-settings-button').click()
  await page.getByLabel('严格 GS1 关联校验').check()
  await expect(page.locator('.diagnostic-panel')).toContainText('37')

  const fixed = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByLabel('显示内容').fill('(00)123456789012345675(02)12345678901231(37)12')
  expect((await fixed).status()).toBe(200)
  await expect(page.locator('.diagnostic-panel')).toHaveCount(0)
})
