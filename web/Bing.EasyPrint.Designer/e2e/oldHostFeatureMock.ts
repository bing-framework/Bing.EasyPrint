import type { Page } from '@playwright/test'

type OldHostFeatureMock = {
  unavailableFeatures: string[]
  unavailableLinearTypes?: string[]
  removeCapabilityProperties?: string[]
  removeEanUpcAddonFormat?: boolean
}

export async function installOldHostFeatureMock(page: Page, mock: OldHostFeatureMock): Promise<void> {
  await page.addInitScript((configuration) => {
    const nativeFetch = window.fetch.bind(window)
    window.fetch = async (input, init) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url
      const response = await nativeFetch(input, init)
      if (!url.includes('/api/designer/')) return response

      const headers = new Headers(response.headers)
      headers.set('x-easyprint-features', (headers.get('x-easyprint-features') ?? '').split(',')
        .map((feature) => feature.trim()).filter((feature) => feature && !configuration.unavailableFeatures.includes(feature))
        .join(','))

      let body: BodyInit | null = await response.arrayBuffer()
      if (url.includes('/capabilities')) {
        const json = JSON.parse(new TextDecoder().decode(body as ArrayBuffer))
        for (const property of configuration.removeCapabilityProperties ?? []) {
          delete json[property]
          if (json.gs1Composite) delete json.gs1Composite[property]
        }
        if (json.gs1Composite && configuration.unavailableLinearTypes?.length) {
          json.gs1Composite.linearTypes = json.gs1Composite.linearTypes
            .filter((type: string) => !configuration.unavailableLinearTypes!.includes(type))
        }
        if (configuration.removeEanUpcAddonFormat && json.gs1Composite?.primaryFormats) {
          json.gs1Composite.primaryFormats = json.gs1Composite.primaryFormats
            .filter((format: string) => format !== 'ean-upc-digits-with-addon')
        }
        body = JSON.stringify(json)
      }

      headers.delete('content-length')
      headers.delete('content-encoding')
      return new Response(response.status === 204 || response.status === 205 || response.status === 304 ? null : body,
        { status: response.status, statusText: response.statusText, headers })
    }
  }, mock)
}
