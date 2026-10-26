import { expect, test } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import { readPreviewLayout } from './previewResponse'

const qrTemplate = {
  Version: 2,
  Width: 320,
  Height: 240,
  Rows: [{ Cells: [{ Elements: [{
    Type: 3,
    UnitWidth: 4,
    ErrorLevel: 'M',
    Gs1: false,
    Field: { Value: 'legacy value' },
  }] }] }],
}

const repeatedGs1Template = {
  Version: 2,
  Width: 320,
  Height: 240,
  Rows: [{
    RepeatSource: 'Items',
    Cells: [{
      Width: 320,
      Elements: [{
        Type: 3,
        UnitWidth: 4,
        ErrorLevel: 'M',
        Gs1: false,
        Field: {
          Gs1Input: {
            Mode: 1,
            BaseUri: 'https://id.gs1.org',
            Items: [{ Ai: '01', Name: '$item.Gtin' }],
          },
        },
      }],
    }],
  }],
}

async function openGs1QrEditor(page: import('@playwright/test').Page, sourceTemplate: unknown = qrTemplate) {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.getByTestId('template-import').setInputFiles({
    name: 'gs1-ai-editor.json',
    mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(sourceTemplate)),
  })
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: '二维码' }).first().click()
  await expect(page.getByTestId('gs1-ai-editor')).toBeVisible()
  const enable = page.getByTestId('gs1-enable')
  if (await enable.count()) await expect(enable).toBeEnabled()
  else await expect(page.getByLabel('GS1 输出模式')).toBeVisible()
}

test('AI editor builds Digital Link QR and preserves ordered extension parameters', async ({ page }) => {
  await openGs1QrEditor(page)
  await page.getByTestId('gs1-enable').click()
  await page.getByLabel('选择 GS1 AI').selectOption('01')
  await page.getByLabel('添加 AI').click()
  await page.getByLabel('AI 01 固定值').fill('09520123456788')
  await expect(page.locator('.gs1-validation')).toContainText('Host 校验有效')

  await page.getByLabel('GS1 输出模式').selectOption('1')
  await expect(page.getByLabel('GS1 QR Code')).not.toBeChecked()
  await page.getByLabel('Digital Link HTTPS 基址').fill('https://id.gs1.org')
  await page.getByLabel('添加查询参数').click()
  await page.getByLabel('查询键 1').fill('source')
  await page.getByLabel('查询值 1').fill('label')
  await page.getByLabel('添加查询参数').click()
  await page.getByLabel('查询键 2').fill('source')
  await page.getByLabel('查询值 2').fill('scan')
  await expect(page.locator('.gs1-validation')).toContainText('Host 校验有效')

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const download = await downloadPromise
  const saved = JSON.parse(await readFile(await download.path(), 'utf8'))
  const qr = saved.Rows[0].Cells[0].Elements[0]
  expect(qr.Gs1).toBe(false)
  expect(qr.Field.Gs1Input).toMatchObject({ Mode: 1, BaseUri: 'https://id.gs1.org' })
  expect(qr.Field.Gs1Input.QueryParameters).toEqual([
    { Key: 'source', Value: 'label' },
    { Key: 'source', Value: 'scan' },
  ])

  await page.locator('.gs1-converter summary').click()
  await page.getByLabel('待转换 GS1 内容').fill('https://id.gs1.org/01/123')
  await page.getByTestId('gs1-convert').click()
  await expect(page.getByRole('alert')).toContainText('14 位')
  await expect(page.getByLabel('AI 01 固定值')).toHaveValue('09520123456788')
  await expect(page.getByLabel('Digital Link HTTPS 基址')).toHaveValue('https://id.gs1.org')
})

test('AI editor binds a catalog AI to a business field and revalidates current data', async ({ page }) => {
  await openGs1QrEditor(page)
  await page.getByTestId('gs1-enable').click()
  await page.getByLabel('选择 GS1 AI').selectOption('01')
  await page.getByLabel('添加 AI').click()
  await page.getByRole('button', { name: '字段绑定' }).click()
  await page.getByLabel('AI 01 字段路径').fill('Product.Gtin')
  await page.getByRole('button', { name: /打开完整数据/ }).click()
  await page.getByLabel('业务数据 JSON').fill('{"Product":{"Gtin":"09520123456788"}}')
  await expect(page.locator('.gs1-validation')).toContainText('Host 校验有效')

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const download = await downloadPromise
  const saved = JSON.parse(await readFile(await download.path(), 'utf8'))
  expect(saved.Rows[0].Cells[0].Elements[0].Field.Gs1Input.Items[0]).toMatchObject({
    Ai: '01', Name: 'Product.Gtin',
  })
})

test('pasted Digital Link keeps source URI semantics through export, reload, and engine preview', async ({ page }) => {
  const uri = 'https://scan.example/tenant/01/09520123456788/10/LOT-A?campaign=A&campaign=B#scan-v1'
  await openGs1QrEditor(page)
  await page.getByTestId('gs1-enable').click()
  await page.getByLabel('选择 GS1 AI').selectOption('01')
  await page.getByLabel('添加 AI').click()
  await page.getByLabel('AI 01 固定值').fill('09520123456788')
  await page.getByLabel('GS1 输出模式').selectOption('1')
  await page.getByLabel('Digital Link HTTPS 基址').fill('https://stale.example/old')
  await page.getByLabel('Fragment').fill('stale')
  await page.getByLabel('添加查询参数').click()
  await page.getByLabel('查询键 1').fill('campaign')
  await page.getByLabel('查询值 1').fill('stale')

  await page.locator('.gs1-converter summary').click()
  await page.getByLabel('待转换 GS1 内容').fill(uri)
  const conversionResponsePromise = page.waitForResponse(response => response.url().includes('/gs1-convert'))
  await page.getByTestId('gs1-convert').click()
  const conversionResponse = await conversionResponsePromise
  expect(conversionResponse.ok()).toBeTruthy()
  const conversion = await conversionResponse.json()
  expect(conversion.output).toBe(uri)
  expect(conversion.structuredInput.items.find((item: { ai: string }) => item.ai === '10').placement).toBe(1)
  await expect(page.getByLabel('AI 10 位置')).toHaveValue('1')
  await expect(page.locator('.gs1-validation')).toContainText('Host 校验有效')

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const download = await downloadPromise
  const saved = JSON.parse(await readFile(await download.path(), 'utf8'))
  const input = saved.Rows[0].Cells[0].Elements[0].Field.Gs1Input
  expect(input.BaseUri).toBe('https://scan.example/tenant')
  expect(input.Fragment).toBe('scan-v1')
  expect(input.Items.find((item: { Ai: string }) => item.Ai === '10').Placement).toBe(1)
  expect(input.QueryParameters).toEqual([
    { Key: 'campaign', Value: 'A' },
    { Key: 'campaign', Value: 'B' },
  ])

  await page.reload()
  const reloadedValidationPromise = page.waitForResponse(response =>
    response.url().includes('/gs1-validate') && response.request().postData()?.includes('scan.example'))
  const reloadedPreviewPromise = page.waitForResponse(response =>
    response.url().includes('/api/designer/preview') && response.status() === 200 &&
    response.request().postData()?.includes('scan.example'))
  await openGs1QrEditor(page, saved)
  const reloadedValidation = await reloadedValidationPromise
  const validation = await reloadedValidation.json()
  expect(validation.items.map((item: { output: string }) => item.output)).toContain(uri)
  const rendered = (await readPreviewLayout(await reloadedPreviewPromise)).pages.flatMap(page => page.elements)
    .find(element => element.type === 3 && element.sourcePath === 'Rows[0].Cells[0].Elements[0]')
  expect(rendered?.text).toBe(uri)
  await expect(page.locator('.preview-source')).toContainText('引擎预览')
  await expect(page.locator('.gs1-validation')).toContainText('Host 校验有效')
})

test('pasted Digital Link only uses editor settings when the override is selected', async ({ page }) => {
  const uri = 'https://scan.example/tenant/01/09520123456788/10/LOT-A?campaign=A&campaign=B#scan-v1'
  await openGs1QrEditor(page)
  await page.getByTestId('gs1-enable').click()
  await page.getByLabel('选择 GS1 AI').selectOption('01')
  await page.getByLabel('添加 AI').click()
  await page.getByLabel('AI 01 固定值').fill('09520123456788')
  await page.getByLabel('GS1 输出模式').selectOption('1')
  await page.getByLabel('Digital Link HTTPS 基址').fill('https://stale.example/old')
  await page.getByLabel('Fragment').fill('stale')
  await page.getByLabel('添加查询参数').click()
  await page.getByLabel('查询键 1').fill('campaign')
  await page.getByLabel('查询值 1').fill('stale')

  await page.locator('.gs1-converter summary').click()
  await page.getByLabel('待转换 GS1 内容').fill(uri)
  await page.getByLabel('应用当前设置覆盖来源 URI').check()
  const conversionResponsePromise = page.waitForResponse(response => response.url().includes('/gs1-convert'))
  await page.getByTestId('gs1-convert').click()
  const response = await conversionResponsePromise

  expect(response.ok()).toBeTruthy()
  expect((await response.json()).output)
    .toBe('https://stale.example/old/01/09520123456788/10/LOT-A?campaign=stale#stale')
})

test('AI availability follows the selected GS1 mode and preserves an unsupported Digital Link AI', async ({ page }) => {
  await openGs1QrEditor(page)
  await page.getByTestId('gs1-enable').click()
  await page.getByLabel('搜索 GS1 AI').fill('8200')
  await page.getByLabel('选择 GS1 AI').selectOption('8200')
  await page.getByLabel('添加 AI').click()
  await page.getByLabel('AI 8200 固定值').fill('https://example.test/product')
  await expect(page.locator('.gs1-validation')).toContainText('Host 校验有效')

  await page.getByLabel('GS1 输出模式').selectOption('1')
  await expect(page.getByLabel('AI 8200 固定值')).toHaveValue('https://example.test/product')
  await expect(page.locator('.gs1-validation')).toContainText('8200')
  await page.getByLabel('GS1 输出模式').selectOption('0')
  await expect(page.getByLabel('AI 8200 固定值')).toHaveValue('https://example.test/product')
  await expect(page.locator('.gs1-validation')).toContainText('Host 校验有效')
})

test('AI editor reports pending without data and recovers when data arrives', async ({ page }) => {
  await openGs1QrEditor(page, repeatedGs1Template)
  await page.getByRole('button', { name: /打开完整数据/ }).click()
  const data = page.getByLabel('业务数据 JSON')

  const missingDataResponsePromise = page.waitForResponse(response => response.url().includes('/gs1-validate'))
  await data.fill('')
  const missingDataResponse = await missingDataResponsePromise
  const missingDataRequest = JSON.parse(missingDataResponse.request().postData() ?? '{}')
  expect(missingDataRequest).not.toHaveProperty('data')
  expect((await missingDataResponse.json()).status).toBe('pending')
  await expect(page.locator('.gs1-validation')).toContainText('等待绑定数据')

  await data.fill('{"Items":[]}')
  await expect(page.locator('.gs1-validation')).toHaveClass(/state-pending/)
  await expect(page.locator('.gs1-validation')).toContainText('等待绑定数据')

  await data.fill('{"Items":[{"Gtin":"09520123456788"}]}')
  await expect(page.locator('.gs1-validation')).toContainText('Host 校验有效')
})

test('AI editor gives mixed repeated instance failures precedence and preserves their location', async ({ page }) => {
  await openGs1QrEditor(page, repeatedGs1Template)
  await page.getByRole('button', { name: /打开完整数据/ }).click()
  const data = page.getByLabel('业务数据 JSON')

  await data.fill('{"Items":[{"Gtin":"09520123456788"},{"Gtin":"INVALID"}]}')
  await expect(page.locator('.gs1-validation')).toHaveClass(/state-invalid/)
  await expect(page.locator('.gs1-validation')).toContainText('实例 2')
  await expect(page.locator('.gs1-validation')).not.toContainText('Host 校验有效')

  await data.fill('{"Items":[{"Gtin":"09520123456788"},{"Gtin":"09520123456788"}]}')
  await expect(page.locator('.gs1-validation')).toContainText('Host 校验有效')
  await expect(page.locator('.gs1-validation')).not.toContainText('实例 2')
})
