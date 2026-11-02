import { getOidcAccessToken, type OidcTokenProvider } from './token-provider'

export interface ApiClientOptions {
  baseUrl?: string
  tokenProvider?: OidcTokenProvider
  fetchImpl?: typeof fetch
}

export interface ApiRequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
}

export interface ApiProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
}

export class ApiClientError extends Error {
  readonly status: number | null
  readonly correlationId: string | null

  constructor(message: string, status: number | null, correlationId: string | null) {
    super(message)
    this.name = 'ApiClientError'
    this.status = status
    this.correlationId = correlationId
  }
}

export class ApiProblemError extends ApiClientError {
  readonly problem: ApiProblemDetails

  constructor(problem: ApiProblemDetails, correlationId: string | null) {
    super(problem.detail ?? problem.title ?? 'The request could not be completed.', problem.status ?? null, correlationId)
    this.name = 'ApiProblemError'
    this.problem = problem
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

function parseProblem(value: unknown, status: number): ApiProblemDetails {
  if (!isRecord(value)) {
    return { status, title: 'The request could not be completed.' }
  }

  return {
    type: typeof value.type === 'string' ? value.type : undefined,
    title: typeof value.title === 'string' ? value.title : undefined,
    status: typeof value.status === 'number' ? value.status : status,
    detail: typeof value.detail === 'string' ? value.detail : undefined,
    instance: typeof value.instance === 'string' ? value.instance : undefined,
  }
}

function parseJson(text: string): unknown {
  if (!text) {
    return undefined
  }

  try {
    return JSON.parse(text) as unknown
  } catch {
    return undefined
  }
}

export class ApiClient {
  private readonly baseUrl: string
  private readonly tokenProvider: OidcTokenProvider
  private readonly fetchImpl: typeof fetch

  constructor(options: ApiClientOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.tokenProvider = options.tokenProvider ?? { getAccessToken: getOidcAccessToken }
    this.fetchImpl = options.fetchImpl ?? fetch
  }

  async request<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
    if (!path.startsWith('/')) {
      throw new ApiClientError('API paths must be rooted at the configured API base URL.', null, null)
    }

    const token = await this.tokenProvider.getAccessToken()
    const headers = new Headers({ Accept: 'application/json' })
    const hasBody = options.body !== undefined

    if (hasBody) {
      headers.set('Content-Type', 'application/json')
    }
    if (token) {
      headers.set('Authorization', `Bearer ${token}`)
    }

    let response: Response
    try {
      response = await this.fetchImpl(`${this.baseUrl}${path}`, {
        method: options.method ?? 'GET',
        headers,
        body: hasBody ? JSON.stringify(options.body) : undefined,
        signal: options.signal,
      })
    } catch {
      throw new ApiClientError('The API could not be reached.', null, null)
    }

    const correlationId = response.headers.get('x-correlation-id') ?? response.headers.get('traceparent')
    const payload = parseJson(await response.text())

    if (!response.ok) {
      throw new ApiProblemError(parseProblem(payload, response.status), correlationId)
    }

    if (response.status === 204 || payload === undefined) {
      return undefined as T
    }

    return payload as T
  }

  get<T>(path: string, signal?: AbortSignal): Promise<T> {
    return this.request<T>(path, { signal })
  }

  post<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
    return this.request<T>(path, { method: 'POST', body, signal })
  }

  put<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
    return this.request<T>(path, { method: 'PUT', body, signal })
  }

  patch<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
    return this.request<T>(path, { method: 'PATCH', body, signal })
  }

  delete<T>(path: string, signal?: AbortSignal): Promise<T> {
    return this.request<T>(path, { method: 'DELETE', signal })
  }
}
