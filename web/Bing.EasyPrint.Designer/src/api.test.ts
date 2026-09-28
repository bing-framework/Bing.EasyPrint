import { afterEach, describe, expect, it, vi } from 'vitest'
import { DesignerValidationError, previewTemplate } from './api'
import { createBlankSample } from './samples'

afterEach(() => vi.unstubAllGlobals())

describe('designer preview errors', () => {
  it('preserves the server node path and reason for an invalid layout', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      errors: [{ sourcePath: 'Rows[0].Cells[0].Elements[0]', message: '元素超出单元格。' }],
    }), { status: 400, headers: { 'Content-Type': 'application/json' } })))

    const preview = previewTemplate(createBlankSample(), {}, [])
    await expect(preview).rejects.toThrow(DesignerValidationError)
    await expect(preview).rejects.toThrow('Rows[0].Cells[0].Elements[0]: 元素超出单元格。')
  })
})
