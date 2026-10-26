import { afterEach, describe, expect, it, vi } from 'vitest'
import { releasePreviewImages } from './previewLifecycle'

afterEach(() => vi.restoreAllMocks())

describe('preview lifecycle image ownership', () => {
  it('releases every discarded object URL', () => {
    const revoke = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
    releasePreviewImages({ first: 'blob:first', second: 'blob:second' })
    expect(revoke.mock.calls.map(([url]) => url)).toEqual(['blob:first', 'blob:second'])
  })

  it('accepts missing images without touching the object URL registry', () => {
    const revoke = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
    releasePreviewImages(undefined)
    expect(revoke).not.toHaveBeenCalled()
  })
})
