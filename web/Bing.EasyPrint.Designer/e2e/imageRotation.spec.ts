import { expect, test, type Page } from '@playwright/test'
import { readFile } from 'node:fs/promises'

// 在浏览器内生成 2×1 非对称 PNG，避免静态资源损坏或正方形资源掩盖轴交换错误。
async function createAsymmetricPng(page: Page) {
  const dataUrl = await page.evaluate(() => {
    const canvas = document.createElement('canvas')
    canvas.width = 2
    canvas.height = 1
    const context = canvas.getContext('2d')
    if (!context) throw new Error('浏览器不支持 Canvas 2D。')
    context.fillStyle = '#000'
    context.fillRect(0, 0, 1, 1)
    context.fillStyle = '#fff'
    context.fillRect(1, 0, 1, 1)
    return canvas.toDataURL('image/png')
  })
  const encoded = dataUrl.split(',')[1]
  if (!encoded) throw new Error('Canvas PNG 数据为空。')
  return Buffer.from(encoded, 'base64')
}

function imageTemplate(rotation: 0 | 90 | 180 | 270) {
  return {
    Version: 1,
    Width: 240,
    Height: 120,
    Rows: [{ Height: 120, Cells: [{ Width: 240, Elements: [{
      Type: 2,
      Width: 80,
      Height: 40,
      Rotation: rotation,
      StartCoordinate: { X: 17, Y: 23 },
      Field: { Value: 'logo' },
    }] }] }],
  }
}

test('图片旋转在四个角度保持左上角并交换非方形预览占位', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  const asymmetricPng = await createAsymmetricPng(page)

  const layouts: Array<{ rotation: 0 | 90 | 180 | 270; width: number; height: number }> = [
    { rotation: 0, width: 80, height: 40 },
    { rotation: 90, width: 40, height: 80 },
    { rotation: 180, width: 80, height: 40 },
    { rotation: 270, width: 40, height: 80 },
  ]
  for (const expected of layouts) {
    const response = await page.request.post('/api/designer/preview', {
      data: {
        template: imageTemplate(expected.rotation),
        data: {},
        protocol: 'Tspl',
        resources: { logo: asymmetricPng.toString('base64') },
      },
    })
    expect(response.ok()).toBe(true)
    const preview = await response.json()
    const image = preview.pages[0].elements.find((element: { type: number }) => element.type === 2)
    expect(image).toMatchObject({ rotation: expected.rotation, x: 17, y: 23, width: expected.width, height: expected.height })
  }
})

test('ESC/POS Native 禁止图片元素，Raster 保留图片旋转能力', async ({ page }) => {
  await page.goto('/')
  const asymmetricPng = await createAsymmetricPng(page)
  const template = imageTemplate(90)
  const native = await page.request.post('/api/designer/preview', {
    data: { template, data: {}, protocol: 'EscPos', escPosLayoutMode: 'Native', resources: { logo: asymmetricPng.toString('base64') } },
  })
  expect(native.status()).toBe(400)
  const nativeError = await native.json()
  expect(nativeError.errors?.[0]?.sourcePath).toBe('Rows[0].Cells[0].Elements[0]')
  expect(nativeError.errors?.[0]?.message).toMatch(/图片|Native|ESC\/POS/)

  const raster = await page.request.post('/api/designer/preview', {
    data: { template, data: {}, protocol: 'EscPos', escPosLayoutMode: 'Raster', resources: { logo: asymmetricPng.toString('base64') } },
  })
  expect(raster.ok()).toBe(true)
  const rasterPreview = await raster.json()
  expect(rasterPreview.pages[0].elements.find((element: { type: number }) => element.type === 2))
    .toMatchObject({ rotation: 90, x: 17, y: 23, width: 40, height: 80 })
})

test('图片旋转通过设计器 JSON 导入导出保持原始宽高', async ({ page }) => {
  await page.goto('/')
  const template = imageTemplate(270)
  await page.locator('[data-testid="template-import"]').setInputFiles({
    name: 'rotated-image.json',
    mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(template)),
  })
  await expect(page.locator('.tree-row.tree-element .tree-select')).toHaveCount(1)
  await page.locator('.tree-row.tree-element .tree-select').click()
  const rotation = page.getByLabel('元素旋转')
  await expect(rotation).toHaveValue('270')
  await expect(page.locator('.inspector-section').filter({ hasText: '位置与尺寸' }).locator('input').nth(2)).toHaveValue('80')
  await expect(page.locator('.inspector-section').filter({ hasText: '位置与尺寸' }).locator('input').nth(3)).toHaveValue('40')

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const download = await downloadPromise
  const exported = JSON.parse(await readFile(await download.path(), 'utf8'))
  expect(exported.Rows[0].Cells[0].Elements[0]).toMatchObject({ Type: 2, Rotation: 270, Width: 80, Height: 40 })
})

test('自动图片旋转按已有端点还原原始尺寸并同步占用框', async ({ page }) => {
  await page.goto('/')
  const template = {
    Version: 1,
    Width: 240,
    Height: 120,
    Rows: [{ Height: 120, Cells: [{ Width: 240, Elements: [{
      Type: 2,
      StartCoordinate: { X: 17, Y: 23 },
      EndCoordinate: { X: 97, Y: 63 },
      Field: { Value: 'missing-image' },
    }] }] }],
  }
  await page.locator('[data-testid="template-import"]').setInputFiles({
    name: 'auto-rotated-image.json',
    mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(template)),
  })
  await expect(page.locator('.tree-row.tree-element .tree-select')).toHaveCount(1)
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.locator('.tree-row.tree-element .tree-select').click()

  const position = page.locator('.inspector-section').filter({ hasText: '位置与尺寸' }).locator('input')
  await expect(position.nth(2)).toHaveValue('80')
  await expect(position.nth(3)).toHaveValue('40')
  await page.getByLabel('元素旋转').selectOption('90')
  await expect(position.nth(2)).toHaveValue('80')
  await expect(position.nth(3)).toHaveValue('40')

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  const download = await downloadPromise
  const exported = JSON.parse(await readFile(await download.path(), 'utf8'))
  const element = exported.Rows[0].Cells[0].Elements[0]
  expect(element).toMatchObject({ Type: 2, Rotation: 90 })
  expect(element.Width).toBeUndefined()
  expect(element.Height).toBeUndefined()
  expect(element.EndCoordinate.X - element.StartCoordinate.X).toBe(40)
  expect(element.EndCoordinate.Y - element.StartCoordinate.Y).toBe(80)
})

test('画布在原始图片尺寸的局部组内旋转，不二次交换图像尺寸', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.locator('.sample-picker select').selectOption('blank')
  await expect(page.locator('.canvas-breadcrumb strong')).toHaveText('576 × 360 点')
  const asymmetricPng = await createAsymmetricPng(page)
  await page.locator('input[type="file"]').nth(1).setInputFiles({
    name: 'asymmetric.png', mimeType: 'image/png', buffer: asymmetricPng,
  })
  await expect(page.locator('.resource-status')).toContainText('图片包已包含')

  const position = page.locator('.inspector-section').filter({ hasText: '位置与尺寸' }).locator('input')
  await position.nth(2).fill('80')
  await position.nth(2).press('Tab')
  await position.nth(3).fill('40')
  await position.nth(3).press('Tab')
  await page.getByLabel('元素旋转').selectOption('90')

  const readCanvasImages = () => page.evaluate(() => {
    const root = document.querySelector('.page-frame')!.firstElementChild as HTMLElement & { __vueParentComponent?: any }
    const stage = root.__vueParentComponent?.exposed?.getNode?.()
    return stage?.find('Image').map((image: any) => ({
      width: image.width(), height: image.height(),
      parentRotation: image.getParent()?.rotation?.(),
    })) ?? []
  })
  await expect.poll(readCanvasImages).toEqual(expect.arrayContaining([expect.objectContaining({ parentRotation: 90 })]))
  const imageNodes = await readCanvasImages()
  expect(imageNodes).toEqual(expect.arrayContaining([expect.objectContaining({ parentRotation: 90 })]))
  const image = imageNodes.find((item: { parentRotation: number }) => item.parentRotation === 90)
  expect(image?.width).toBeGreaterThan(image?.height ?? 0)

  const packageDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '模板包' }).click()
  const packageDownload = await packageDownloadPromise
  const packageBytes = await readFile(await packageDownload.path())
  await page.locator('[data-testid="template-import"]').setInputFiles({
    name: 'rotated-image-package.zip', mimeType: 'application/zip', buffer: packageBytes,
  })
  await expect(page.locator('.toast-message')).toContainText('已导入模板包 · 1 个资源')
  await expect(page.locator('.tree-row.tree-element .tree-select')).toHaveCount(1)
  await page.locator('.tree-row.tree-element .tree-select').click()
  await expect(page.getByLabel('元素旋转')).toHaveValue('90')
  await expect(position.nth(2)).toHaveValue('80')
  await expect(position.nth(3)).toHaveValue('40')
})
