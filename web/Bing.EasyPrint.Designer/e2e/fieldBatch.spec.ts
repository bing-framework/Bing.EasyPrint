import { expect, test } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'

const template = {
  Version: 2,
  Width: 240,
  Height: 80,
  Rows: [{ Height: 80, Cells: [{ Width: 240, Elements: [{
    Type: 1, FontSize: 24, Field: { Name: 'Name' },
  }] }] }],
}

async function importTemplate(page: import('@playwright/test').Page) {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const preview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.locator('[data-testid="template-import"]').setInputFiles({
    name: 'field-format-template.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)),
  })
  await preview
}

async function setRawData(page: import('@playwright/test').Page, text: string) {
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  const preview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(text)
  await preview
}

test('field format is edited in Designer, rendered by Host and saved as JSON', async ({ page }) => {
  test.setTimeout(90_000)
  await importTemplate(page)
  await setRawData(page, '{"Name":123456789012345.6789}')
  await page.locator('.tree-row.tree-element .tree-select').first().click()
  await expect(page.getByTestId('field-format-editor')).toBeVisible()
  await page.getByTestId('field-format-add').click()
  await page.getByTestId('field-format-kind').selectOption('Number')
  await page.getByTestId('field-format-pattern').fill('0.0000')
  await page.getByTestId('field-format-culture').fill('de-DE')
  const preview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST' &&
    response.ok())
  await page.getByTestId('field-format-culture').press('Tab')
  const response = await preview
  const archive = await JSZip.loadAsync(await response.body())
  const manifest = JSON.parse(await archive.file('manifest.json')!.async('text'))
  const element = manifest.layout.pages[0].elements.find((item: { type: number }) => item.type === 1)
  expect(element.displayText).toBe('123456789012345,6789')

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const saved = JSON.parse((await readFile(await (await downloadPromise).path())).toString('utf8'))
  expect(saved.Rows[0].Cells[0].Elements[0].Field.Format).toMatchObject({ Kind: 'Number', Pattern: '0.0000', Culture: 'de-DE' })

  const reloaded = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.locator('[data-testid="template-import"]').setInputFiles({
    name: 'field-format-saved.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(saved)),
  })
  const reloadedArchive = await JSZip.loadAsync(await (await reloaded).body())
  const reloadedManifest = JSON.parse(await reloadedArchive.file('manifest.json')!.async('text'))
  const reloadedElement = reloadedManifest.layout.pages[0].elements
    .find((item: { type: number }) => item.type === 1)
  expect(reloadedElement.displayText).toBe('123456789012345,6789')
})

test('batch UI imports CSV, maps a field, renders a real Host ZIP and reports failures', async ({ page }) => {
  test.setTimeout(120_000)
  await importTemplate(page)
  await page.getByTestId('batch-toggle').click()
  const autoPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByTestId('batch-file-input').setInputFiles({
    name: 'batch.csv', mimeType: 'text/csv', buffer: Buffer.from('Code,Name\n00123,Tea\n00456,Coffee\n'),
  })
  await autoPreview
  await expect(page.getByTestId('batch-records').locator('.batch-record')).toHaveCount(2)
  await page.getByTestId('batch-map-Name').fill('Unused')
  await page.getByTestId('batch-map-Name').press('Tab')
  await page.getByTestId('batch-map-Code').fill('Name')
  await page.getByTestId('batch-map-Code').press('Tab')
  const mappedPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByRole('button', { name: '预览选中' }).click()
  await mappedPreview

  const batchResponse = page.waitForResponse((response) => response.url().includes('/api/designer/batch'))
  await page.getByRole('button', { name: '批量生成' }).click()
  expect((await batchResponse).status()).toBe(200)
  await expect(page.getByTestId('batch-status')).toContainText('完成')

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '下载结果', exact: true }).click()
  const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
  const manifest = JSON.parse(await archive.file('manifest.json')!.async('text'))
  expect(manifest.status).toBe('completed')
  expect(manifest.succeeded).toBe(2)
  const first = await archive.file('records/000000/page-0001.cpcl')!.async('uint8array')
  expect(Buffer.from(first).toString('latin1')).toContain('00123')
})

test('batch UI sends 26 records in two real chunks', async ({ page }) => {
  test.setTimeout(120_000)
  await importTemplate(page)
  await page.getByTestId('batch-toggle').click()
  let batchCalls = 0
  page.on('request', (request) => { if (request.url().includes('/api/designer/batch')) batchCalls++ })
  const rows = ['Name', ...Array.from({ length: 26 }, (_, index) => `value-${index}`)]
  const autoPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByTestId('batch-file-input').setInputFiles({
    name: 'batch-26.csv', mimeType: 'text/csv', buffer: Buffer.from(rows.join('\n') + '\n'),
  })
  await autoPreview
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '批量生成' }).click()
  await expect(page.getByTestId('batch-status')).toContainText('完成')
  await page.getByRole('button', { name: '下载结果', exact: true }).click()
  expect(batchCalls).toBe(2)
  const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
  const manifest = JSON.parse(await archive.file('manifest.json')!.async('text'))
  expect(manifest.physicalPages).toBe(26)
  expect(manifest.records).toHaveLength(26)
})
test('batch cancellation during the second real chunk invalidates stale results and allows a restart', async ({ page }) => {
  test.setTimeout(120_000)
  await importTemplate(page)
  await page.getByTestId('batch-toggle').click()
  const rows = ['Name', ...Array.from({ length: 26 }, (_, index) => `cancel-${index}`)]
  const autoPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByTestId('batch-file-input').setInputFiles({
    name: 'batch-cancel.csv', mimeType: 'text/csv', buffer: Buffer.from(rows.join('\n') + '\n'),
  })
  await autoPreview
  let calls = 0
  let releaseSecond!: () => void
  let secondStarted!: () => void
  const secondGate = new Promise<void>((resolve) => { releaseSecond = resolve })
  const secondRequest = new Promise<void>((resolve) => { secondStarted = resolve })
  await page.route('**/api/designer/batch', async (route) => {
    calls++
    if (calls === 2) {
      secondStarted()
      await secondGate
    }
    await route.continue().catch(() => {})
  })
  await page.getByRole('button', { name: '批量生成' }).click()
  await secondRequest
  await page.getByRole('button', { name: '取消' }).click()
  releaseSecond()
  await expect(page.getByTestId('batch-error')).toContainText('已取消')
  await expect(page.getByRole('button', { name: '下载结果', exact: true })).toHaveCount(0)
  await page.waitForTimeout(200)
  expect(calls).toBe(2)
  await page.unroute('**/api/designer/batch')
  await page.getByRole('button', { name: '批量生成' }).click()
  await expect(page.getByTestId('batch-status')).toContainText('完成')
  await expect(page.getByRole('button', { name: '下载结果', exact: true })).toBeVisible()
})
test('batch UI reports partial failure and does not mark all records successful', async ({ page }) => {
  test.setTimeout(120_000)
  await importTemplate(page)
  await page.getByTestId('batch-toggle').click()
  const autoPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByTestId('batch-file-input').setInputFiles({
    name: 'batch.jsonl', mimeType: 'application/x-ndjson',
    buffer: Buffer.from('{"Name":"ok"}\n{}\n'),
  })
  await autoPreview
  await expect(page.getByTestId('batch-records').locator('.batch-record')).toHaveCount(2)

  const batchResponse = page.waitForResponse((response) => response.url().includes('/api/designer/batch'))
  await page.getByRole('button', { name: '批量生成' }).click()
  expect((await batchResponse).status()).toBe(200)
  await expect(page.getByTestId('batch-status')).toContainText('部分完成')
  await expect(page.getByTestId('batch-status')).toContainText('失败 1')

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '下载结果（部分完成）' }).click()
  const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
  const manifest = JSON.parse(await archive.file('manifest.json')!.async('text'))
  expect(manifest.status).toBe('partial')
  expect(manifest.errors[0].recordIndex).toBe(1)
  expect(await archive.file('records/000001/page-0001.cpcl')).toBeNull()
  expect(await archive.file('errors.csv')!.async('text')).toContain('Name')
})
