import { expect, test, type Route } from '@playwright/test'

test('业务数据失效时取消正在执行的预览，迟到结果不能覆盖错误状态', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await page.getByRole('button', { name: 'JSON', exact: true }).click()

  let receiveRoute!: (route: Route) => void
  const pendingRoute = new Promise<Route>((resolve) => { receiveRoute = resolve })
  let releaseRoute!: () => void
  const release = new Promise<void>((resolve) => { releaseRoute = resolve })
  await page.route('**/api/designer/preview', async (route) => {
    receiveRoute(route)
    await release
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({ pages: [{ width: 777, height: 100, elements: [] }], errors: [] }),
    })
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
