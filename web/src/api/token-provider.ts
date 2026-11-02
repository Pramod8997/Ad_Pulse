export interface OidcTokenProvider {
  getAccessToken(): Promise<string | null>
}

export type OidcAccessTokenReader = () => Promise<string | null>

const anonymousTokenProvider: OidcTokenProvider = {
  async getAccessToken() {
    return null
  },
}

let configuredTokenProvider: OidcTokenProvider = anonymousTokenProvider

export function createOidcTokenProvider(reader: OidcAccessTokenReader): OidcTokenProvider {
  return Object.freeze({
    getAccessToken: reader,
  })
}

export function configureOidcTokenProvider(provider: OidcTokenProvider): void {
  configuredTokenProvider = provider
}

export function getOidcAccessToken(): Promise<string | null> {
  return configuredTokenProvider.getAccessToken()
}

export function resetOidcTokenProvider(): void {
  configuredTokenProvider = anonymousTokenProvider
}
