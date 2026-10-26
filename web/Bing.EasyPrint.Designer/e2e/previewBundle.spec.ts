import { expect, test } from '@playwright/test'

const repeatedTemplate = {
  Version: 3, Width: 400, Height: 60,
  Rows: [{
    Height: 60, RepeatSource: 'Items', Cells: [{ Elements: [{
      Type: 4, BarcodeType: 0, Height: 30, NarrowModuleWidth: 2, HumanReadable: false,
      Field: { Name: 'Value' },
    }] }],
  }],
}

test('合并预览每次只渲染当前页且页切换只请求目标页图片', async ({ page }) => {
  const bundleRequests: Array<Record<string, unknown>> = []
  let elementPngRequests = 0
  page.on('request', (request) => {
    if (request.url().includes('/api/designer/preview-bundle')) {
      bundleRequests.push(request.postDataJSON() as Record<string, unknown>)
    }
    if (request.url().includes('/api/designer/element-png')) elementPngRequests += 1
  })

  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.getByTestId('template-import').setInputFiles({
    name: 'preview-bundle-pages.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(repeatedTemplate)),
  })
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('EscPosRaster')
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  await page.getByRole('textbox', { name: '业务数据 JSON' }).fill(JSON.stringify({
    Items: [{ Value: '12345' }, { Value: '67890' }],
  }))
  await expect(page.locator('.page-pager')).toContainText('1 / 2')
  await expect.poll(() => bundleRequests.filter((body) => body.pageIndex === 0).length)
    .toBeGreaterThan(0)
  expect(elementPngRequests).toBe(0)
  expect(bundleRequests.every((body) => Array.isArray(body.imageTypes))).toBe(true)

  const beforeSwitch = bundleRequests.length
  const nextPageResponse = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview-bundle') &&
    response.request().postDataJSON()?.pageIndex === 1)
  await page.getByTitle('下一页').click()
  await nextPageResponse
  await expect(page.locator('.page-pager')).toContainText('2 / 2')
  expect(elementPngRequests).toBe(0)
  expect(bundleRequests).toHaveLength(beforeSwitch + 1)
  expect(bundleRequests.at(-1)?.pageIndex).toBe(1)
})
