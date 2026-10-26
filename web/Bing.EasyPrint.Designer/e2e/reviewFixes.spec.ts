import { expect, test } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'
import { readPreviewLayout } from './previewResponse'

test('五种输出模式接受真实 Host 的空 HRI 位置契约并启用编辑', async ({ page }) => {
  await page.goto('/')
  for (const target of ['Cpcl', 'EscPosNative', 'EscPosRaster', 'Tspl', 'Zpl']) {
    await page.getByRole('combobox', { name: '输出协议' }).selectOption(target)
    await expect(page.locator('.capability-note')).toHaveClass(/ready/)
    await expect(page.locator('.element-tools button').filter({ hasText: '单行文字' })).toBeEnabled()
  }
  await page.getByRole('button', { name: '输出校准设置' }).click()
  const orientation = page.getByLabel('ZPL 方向')
  await expect(orientation.locator('option')).toHaveCount(3)
  for (const value of ['', 'None', 'Down']) await expect(orientation.locator(`option[value="${value}"]`)).toHaveCount(1)
})

test('GS1 Composite 真实 Host 编辑预览导出及项目往返保持编码参数', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.locator('.sample-picker select').selectOption('blank')
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.getByRole('button', { name: 'GS1 Composite', exact: true }).click()
  await page.getByLabel('Composite 模块宽度').fill('3')
  await page.getByLabel('Composite 线性条高').fill('60')
  const preview = page.waitForResponse((response) => {
    if (!response.url().includes('/api/designer/preview') || response.status() !== 200) return false
    const body = response.request().postDataJSON()
    return JSON.stringify(body?.template).includes('(17)010615(10)A123456')
  })
  await page.getByLabel('Composite 次数据').fill('(17)010615(10)A123456')
  await page.getByLabel('Composite 次数据').press('Tab')
  const layout = await readPreviewLayout(await preview)
  const encoded = layout.pages.flatMap((item: { elements: Array<{ type: number; width: number; height: number }> }) => item.elements)
    .find((item: { type: number }) => item.type === 11)
  expect(encoded).toBeDefined()
  expect(encoded.width).toBeGreaterThan(0)
  expect(encoded.height).toBeGreaterThan(0)
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await expect(page.locator('.status-spinner')).toHaveCount(0)
  await expect.poll(() => page.evaluate(() => {
    const root = document.querySelector('.page-frame')!.firstElementChild as HTMLElement & { __vueParentComponent?: any }
    const stage = root.__vueParentComponent?.exposed?.getNode?.()
    return (stage?.find('Image').length ?? 0) > 0
  })).toBe(true)

  const jsonDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const template = JSON.parse(await readFile(await (await jsonDownload).path(), 'utf8'))
  const element = template.Rows[0].Cells[0].Elements.find((item: { Type: number }) => item.Type === 11)
  expect(element).toMatchObject({ ModuleWidth: 3, LinearBarHeight: 60,
    Field: { Value: '12345678901231' }, SecondaryField: { Value: '(17)010615(10)A123456' } })

  const renderDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 CPCL' }).click()
  const archive = await JSZip.loadAsync(await readFile(await (await renderDownload).path()))
  const cpcl = Buffer.from(await archive.file('page-001.cpcl')!.async('uint8array'))
  const graphicHeader = Buffer.from(`CG ${Math.ceil(encoded.width / 8)} ${encoded.height} ${encoded.x} ${encoded.y} `, 'ascii')
  expect(cpcl.indexOf(graphicHeader)).toBeGreaterThanOrEqual(0)
  expect(cpcl.length).toBeGreaterThan(graphicHeader.length + Math.ceil(encoded.width / 8) * encoded.height)

  const projectDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const saved = await readFile(await (await projectDownload).path())
  expect(JSON.parse(saved.toString()).template).toEqual(template)
  await page.reload()
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  const restoredPreview = page.waitForResponse((response) => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && JSON.stringify(response.request().postDataJSON()?.template).includes('(17)010615(10)A123456'))
  await page.getByTestId('template-import').setInputFiles({ name: 'composite-project.json', mimeType: 'application/json', buffer: saved })
  const restored = await readPreviewLayout(await restoredPreview)
  expect(restored.pages).toEqual(layout.pages)
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
  await expect(page.getByLabel('Composite 模块宽度')).toHaveValue('3')
  await expect(page.getByLabel('Composite 线性条高')).toHaveValue('60')
  await expect(page.getByLabel('Composite 次数据')).toHaveValue('(17)010615(10)A123456')
})

test('GS1 Composite 在旧 Host 缺少协商特性时禁用并说明原因', async ({ page }) => {
  await page.route('**/api/designer/capabilities', async (route) => {
    const response = await route.fetch()
    const body = await response.json()
    delete body.expandedStackedColumns
    delete body.expandedStackedRows
    if (body.gs1Composite) delete body.gs1Composite.expandedStackedColumns
    if (body.gs1Composite) delete body.gs1Composite.expandedStackedRows
    expect(body.elementTypes).toContain(11)
    const headers = response.headers()
    headers['x-easyprint-features'] = headers['x-easyprint-features']!.split(',')
      .filter((feature) => !['gs1-databar-expanded-stacked-columns', 'gs1-databar-expanded-stacked-rows', 'gs1-composite', 'gs1-composite-cc-c', 'gs1-composite-expanded',
        'gs1-composite-stacked', 'gs1-composite-expanded-stacked', 'gs1-composite-ean-upc',
        'gs1-composite-ean-upc-addons', 'gs1-composite-truncated', 'gs1-composite-stacked-omnidirectional'].includes(feature.trim())).join(',')
    if (body.gs1Composite) {
      body.gs1Composite.modes = ['Auto', 'CcA', 'CcB']
      body.gs1Composite.linearTypes = ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited']
      if (body.gs1Composite.primaryFormats) body.gs1Composite.primaryFormats = body.gs1Composite.primaryFormats
        .filter((format: string) => format !== 'ean-upc-digits-with-addon')
    }
    body.barcodeTypes = Array.isArray(body.barcodeTypes)
      ? body.barcodeTypes.filter((type: number) => type !== 32)
      : body.barcodeTypes
    await route.fulfill({ response, headers, json: body })
  })
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.getByRole('button', { name: '组件与表格' }).click()
  const tool = page.getByRole('button', { name: 'GS1 Composite', exact: true })
  await expect(tool).toBeDisabled()
  await expect(tool).toHaveAttribute('title', /不支持|未确认|未声明|更新/)
})
