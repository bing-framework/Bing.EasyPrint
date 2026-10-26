import { expect, test, type Route } from '@playwright/test'
import JSZip from 'jszip'

test('业务数据失效时取消正在执行的预览，迟到结果不能覆盖错误状态', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.getByRole('button', { name: 'JSON', exact: true }).click()

  let receiveRoute!: (route: Route) => void
  const pendingRoute = new Promise<Route>((resolve) => { receiveRoute = resolve })
  let releaseRoute!: () => void
  const release = new Promise<void>((resolve) => { releaseRoute = resolve })
  await page.route('**/api/designer/preview-bundle', async (route) => {
    receiveRoute(route)
    await release
    const zip = new JSZip()
    zip.file('manifest.json', JSON.stringify({
      layout: { pageIndex: 0, pageCount: 1, pages: [{ width: 777, height: 100, elements: [] }] },
      images: [],
      diagnostics: [],
    }))
    const body = await zip.generateAsync({ type: 'uint8array' })
    await route.fulfill({ contentType: 'application/zip', body: Buffer.from(body) })
  }, { times: 1 })

  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill('{"Pending":true}')
  const route = await pendingRoute
  const canceled = page.waitForEvent('requestfailed', {
    predicate: (request) => request === route.request(),
  })
  try {
    await page.getByRole('textbox', { name: '业务数据 JSON' }).fill('{')
    expect((await canceled).failure()?.errorText).toMatch(/ABORTED|cancel/i)
    await expect(page.locator('.status-left')).toContainText('业务数据有误')
    await expect(page.locator('.status-error')).toContainText('业务数据 JSON 无效')
  } finally {
    releaseRoute()
  }
})

const repeatedBundleTemplate = {
  Version: 3, Width: 400, Height: 60,
  Rows: [{
    Height: 60, RepeatSource: 'Items', Cells: [{ Elements: [{
      Type: 4, BarcodeType: 0, Height: 30, NarrowModuleWidth: 2, HumanReadable: false,
      Field: { Name: 'Value' },
    }] }],
  }],
}

async function preparePagedPreview(page: import('@playwright/test').Page, values: string[]) {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.getByTestId('template-import').setInputFiles({
    name: 'preview-race.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(repeatedBundleTemplate)),
  })
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify({
    Items: values.map((Value) => ({ Value })),
  }))
  await expect(page.locator('.page-pager')).toContainText(`1 / ${values.length}`)
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
}

async function fillItems(page: import('@playwright/test').Page, values: string[]) {
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify({
    Items: values.map((Value) => ({ Value })),
  }))
}

test('整页刷新在途时翻页，迟到的整页结果不覆盖目标页', async ({ page }) => {
  await preparePagedPreview(page, ['A', 'B'])
  let delayed = false
  let releaseFull!: () => void
  const fullGate = new Promise<void>((resolve) => { releaseFull = resolve })
  let receivedFull!: () => void
  const fullArrived = new Promise<void>((resolve) => { receivedFull = resolve })
  await page.route('**/api/designer/preview-bundle', async (route) => {
    const body = route.request().postDataJSON() as { pageIndex?: number }
    if (!delayed && body.pageIndex === 0) {
      delayed = true
      receivedFull()
      await fullGate
    }
    try { await route.continue() } catch { /* aborted by a newer request */ }
  })

  await fillItems(page, ['A', 'B', 'C'])
  await fullArrived
  const pageTwo = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview-bundle') && response.status() === 200 &&
    response.request().postDataJSON()?.pageIndex === 1)
  await page.getByTitle('下一页').click()
  await pageTwo
  await expect(page.locator('.page-pager')).toContainText('2 / 3')
  releaseFull()
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await expect(page.locator('.page-pager')).toContainText('2 / 3')
})

test('连续翻页取消旧页面请求，迟到的页面结果不覆盖最终页', async ({ page }) => {
  await preparePagedPreview(page, ['A', 'B', 'C'])
  let delayed = false
  let releasePage!: () => void
  const pageGate = new Promise<void>((resolve) => { releasePage = resolve })
  let receivedPage!: () => void
  const pageArrived = new Promise<void>((resolve) => { receivedPage = resolve })
  await page.route('**/api/designer/preview-bundle', async (route) => {
    const body = route.request().postDataJSON() as { pageIndex?: number }
    if (!delayed && body.pageIndex === 1) {
      delayed = true
      receivedPage()
      await pageGate
    }
    try { await route.continue() } catch { /* aborted by a newer request */ }
  })

  await page.getByTitle('下一页').click()
  await pageArrived
  const pageThree = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview-bundle') && response.status() === 200 &&
    response.request().postDataJSON()?.pageIndex === 2)
  await page.getByTitle('下一页').click()
  await pageThree
  await expect(page.locator('.page-pager')).toContainText('3 / 3')
  releasePage()
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await expect(page.locator('.page-pager')).toContainText('3 / 3')
})

test('旧 Host 缩页时恢复第一页并继续显示引擎预览和当前页图片', async ({ page }) => {
  await page.route('**/api/designer/capabilities', async (route) => {
    const response = await route.fetch()
    const headers = response.headers()
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter((feature) => feature.trim() !== 'preview-bundle').join(',')
    await route.fulfill({ response, headers })
  })
  await preparePagedPreview(page, ['A', 'B'])
  await page.getByTitle('下一页').click()
  await expect(page.locator('.page-pager')).toContainText('2 / 2')

  const recoveredImage = page.waitForResponse((response) =>
    response.url().includes('/api/designer/element-png') && response.status() === 200 &&
    response.request().postDataJSON()?.pageIndex === 0)
  await fillItems(page, ['A'])
  await recoveredImage
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await expect(page.locator('.status-left')).toContainText('引擎预览 · 1 页')
  await expect(page.locator('.page-pager')).toHaveCount(0)
})
