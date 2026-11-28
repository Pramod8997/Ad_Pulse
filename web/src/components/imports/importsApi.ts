import { getOidcAccessToken } from '../../api/token-provider';

export interface ImportPolicy {
  maxFileBytes: number;
  maxRows: number;
  templateFileName: string;
  dateSemantics: string;
  currencySemantics: string;
  requiredColumns: readonly string[];
}

// Mirrors the accepted import contract. The API has no separate policy route;
// the template and server validation remain the source of truth.
export const IMPORT_POLICY: ImportPolicy = {
  maxFileBytes: 5 * 1024 * 1024,
  maxRows: 100_000,
  templateFileName: 'adpulse-daily-metrics-template.csv',
  dateSemantics: 'YYYY-MM-DD reporting-calendar dates.',
  currencySemantics: 'Three uppercase ISO-4217 letters; money is non-negative with up to 4 decimals.',
  requiredColumns: [
    'campaign_id',
    'metric_date',
    'currency',
    'spend',
    'revenue',
    'impressions',
    'clicks',
    'conversions',
  ],
};

export interface ImportRowRejection {
  rowNumber: number;
  errorCode: string;
  message: string;
}

export type ImportPreviewStatus = 'validated' | 'failed';

export interface ImportPreview {
  previewId: string;
  fileName: string;
  status: ImportPreviewStatus;
  totalRows: number;
  acceptedRows: number;
  rejectedRows: number;
  rejections: readonly ImportRowRejection[];
  rejectionsTruncated: boolean;
  uploadedAt: string | null;
  expiresAt: string | null;
}

export type ImportResultStatus = 'completed' | 'partial' | 'failed';

export interface ImportResult {
  importId: string;
  fileName: string;
  status: ImportResultStatus;
  totalRows: number;
  acceptedRows: number;
  rejectedRows: number;
  rejections: readonly ImportRowRejection[];
  rejectionsTruncated: boolean;
  completedAt: string | null;
}

export interface ImportHistoryItem {
  id: string;
  fileName: string;
  uploadedAt: string | null;
  status: ImportResultStatus | 'validated';
  acceptedRows: number;
  rejectedRows: number;
  totalRows: number;
}

export interface ImportHistoryPage {
  items: ImportHistoryItem[];
  nextCursor: string | null;
  hasMore: boolean;
}

export interface TemplateDownload {
  blob: Blob;
  fileName: string;
}

export interface ImportsApi {
  downloadTemplate(): Promise<TemplateDownload>;
  preview(file: File): Promise<ImportPreview>;
  confirm(previewId: string): Promise<ImportResult>;
  list(cursor?: string | null): Promise<ImportHistoryPage>;
  get(importId: string): Promise<ImportResult>;
}

export interface ImportsApiErrorOptions {
  status?: number;
  correlationId?: string;
}

export class ImportsApiError extends Error {
  readonly status?: number;
  readonly correlationId?: string;

  constructor(message: string, options: ImportsApiErrorOptions = {}) {
    super(message);
    this.name = 'ImportsApiError';
    this.status = options.status;
    this.correlationId = options.correlationId;
  }
}

interface ProblemDetails {
  title?: unknown;
  detail?: unknown;
  message?: unknown;
  correlationId?: unknown;
}

const configuredBaseUrl =
  typeof import.meta !== 'undefined' && import.meta.env?.VITE_API_BASE_URL
    ? String(import.meta.env.VITE_API_BASE_URL).replace(/\/$/, '')
    : '';
const DEFAULT_BASE_PATH = `${configuredBaseUrl}/app/imports`;
const MAX_SAFE_TEXT_LENGTH = 500;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function requiredString(value: unknown, field: string): string {
  if (typeof value !== 'string' || value.trim().length === 0) {
    throw new ImportsApiError(`The import response is missing ${field}.`);
  }

  return value.trim();
}

function boundedString(value: unknown, fallback: string): string {
  if (typeof value !== 'string' || value.trim().length === 0) {
    return fallback;
  }

  return value.trim().slice(0, MAX_SAFE_TEXT_LENGTH);
}

function nonNegativeInteger(value: unknown, field: string): number {
  if (typeof value !== 'number' || !Number.isInteger(value) || value < 0) {
    throw new ImportsApiError(`The import response contains an invalid ${field}.`);
  }

  return value;
}

function parseRejections(value: unknown): ImportRowRejection[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.flatMap((item) => {
    if (!isRecord(item)) {
      return [];
    }

    const rowNumber = item.rowNumber;
    if (typeof rowNumber !== 'number' || !Number.isInteger(rowNumber) || rowNumber < 1) {
      return [];
    }

    return [{
      rowNumber,
      errorCode: boundedString(item.errorCode, 'invalid_row'),
      message: boundedString(item.message, 'This row was rejected by the server.'),
    }];
  });
}

function parsePreview(value: unknown): ImportPreview {
  if (!isRecord(value)) {
    throw new ImportsApiError('The preview response was invalid.');
  }

  const previewId = value.id ?? value.importBatchId ?? value.previewId;
  const acceptedRows = nonNegativeInteger(value.acceptedRows, 'accepted row count');
  const rejectedRows = nonNegativeInteger(value.rejectedRows, 'rejected row count');
  const rawStatus = value.status;
  const status: ImportPreviewStatus =
    rawStatus === 'failed' || rawStatus === 'rejected' ? 'failed' : 'validated';
  const uploadedAt = typeof value.uploadedAt === 'string'
    ? value.uploadedAt
    : typeof value.expiresAt === 'string'
      ? value.expiresAt
      : null;

  return {
    previewId: requiredString(previewId, 'preview identifier'),
    fileName: boundedString(value.fileName, 'selected CSV file'),
    status,
    totalRows: nonNegativeInteger(value.totalRows ?? acceptedRows + rejectedRows, 'total row count'),
    acceptedRows,
    rejectedRows,
    rejections: parseRejections(value.errors ?? value.rejections),
    rejectionsTruncated: value.rejectionsTruncated === true,
    uploadedAt,
    expiresAt: typeof value.expiresAt === 'string' ? value.expiresAt : null,
  };
}

function parseResult(value: unknown): ImportResult {
  if (!isRecord(value)) {
    throw new ImportsApiError('The import result was invalid.');
  }

  const rawStatus = value.status;
  if (
    rawStatus !== 'completed'
    && rawStatus !== 'validated'
    && rawStatus !== 'failed'
    && rawStatus !== 'rejected'
    && rawStatus !== 'conflict'
  ) {
    throw new ImportsApiError('The import result contains an invalid status.');
  }

  const acceptedRows = nonNegativeInteger(value.acceptedRows, 'accepted row count');
  const rejectedRows = nonNegativeInteger(value.rejectedRows, 'rejected row count');
  const status: ImportResultStatus = rawStatus === 'completed'
    ? (rejectedRows > 0 ? 'partial' : 'completed')
    : rawStatus === 'failed' || rawStatus === 'rejected' || rawStatus === 'conflict'
      ? 'failed'
      : rejectedRows > 0
        ? 'partial'
        : 'completed';
  const completedAtRaw = value.uploadedAt ?? value.completedAt;

  return {
    importId: requiredString(value.id ?? value.importBatchId ?? value.importId, 'import identifier'),
    fileName: boundedString(value.fileName, 'selected CSV file'),
    status,
    totalRows: acceptedRows + rejectedRows,
    acceptedRows,
    rejectedRows,
    rejections: parseRejections(value.errors ?? value.rejections),
    rejectionsTruncated: value.rejectionsTruncated === true,
    completedAt: typeof completedAtRaw === 'string' ? completedAtRaw : null,
  };
}

function parseHistoryItem(value: unknown): ImportHistoryItem {
  if (!isRecord(value)) {
    throw new ImportsApiError('The import history item was invalid.');
  }

  const acceptedRows = nonNegativeInteger(value.acceptedRows, 'accepted row count');
  const rejectedRows = nonNegativeInteger(value.rejectedRows, 'rejected row count');
  const rawStatus = value.status;
  let status: ImportHistoryItem['status'];
  if (rawStatus === 'validated') {
    status = 'validated';
  } else if (rawStatus === 'completed') {
    status = rejectedRows > 0 ? 'partial' : 'completed';
  } else if (rawStatus === 'failed' || rawStatus === 'rejected' || rawStatus === 'conflict') {
    status = 'failed';
  } else {
    throw new ImportsApiError('The import history item contains an invalid status.');
  }

  const uploadedAtRaw = value.uploadedAt ?? value.completedAt;
  return {
    id: requiredString(value.id ?? value.importBatchId ?? value.importId, 'import identifier'),
    fileName: boundedString(value.fileName, 'selected CSV file'),
    uploadedAt: typeof uploadedAtRaw === 'string' ? uploadedAtRaw : null,
    status,
    acceptedRows,
    rejectedRows,
    totalRows: acceptedRows + rejectedRows,
  };
}

function parseHistoryPage(value: unknown): ImportHistoryPage {
  if (Array.isArray(value)) {
    return {
      items: value.map(parseHistoryItem),
      nextCursor: null,
      hasMore: false,
    };
  }

  if (!isRecord(value)) {
    throw new ImportsApiError('The import history response was invalid.');
  }

  const rawItems = Array.isArray(value.items) ? value.items : [];
  return {
    items: rawItems.map(parseHistoryItem),
    nextCursor: typeof value.nextCursor === 'string' && value.nextCursor.length > 0 ? value.nextCursor : null,
    hasMore: value.hasMore === true,
  };
}

function fileNameFromContentDisposition(value: string | null): string | undefined {
  if (!value) {
    return undefined;
  }

  const match = value.match(/filename\*?=(?:UTF-8''|"?)([^";]+)"?/i);
  const fileName = match?.[1]?.trim();
  return fileName ? fileName.replace(/[\\/:*?"<>|\u0000-\u001f]/g, '_') : undefined;
}

async function responseBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text) {
    return undefined;
  }

  try {
    return JSON.parse(text) as unknown;
  } catch {
    return undefined;
  }
}

async function throwApiError(response: Response): Promise<never> {
  const body = await responseBody(response);
  const problem = isRecord(body) ? body as ProblemDetails : {};
  const rawMessage = problem.detail ?? problem.title ?? problem.message;
  const title = typeof rawMessage === 'string' && rawMessage.trim().length > 0
    ? rawMessage.trim().slice(0, 200)
    : 'The import request could not be completed.';
  const headerCorrelationId = response.headers.get('x-correlation-id') ?? undefined;
  const bodyCorrelationId = typeof problem.correlationId === 'string' ? problem.correlationId : undefined;

  throw new ImportsApiError(title, {
    status: response.status,
    correlationId: headerCorrelationId ?? bodyCorrelationId,
  });
}

export function createImportsApi(
  options: { basePath?: string; fetcher?: typeof fetch } = {},
): ImportsApi {
  const fetcher = options.fetcher ?? fetch;
  const basePath = (options.basePath ?? DEFAULT_BASE_PATH).replace(/\/$/, '');

  async function requestHeaders(accept: string): Promise<Headers> {
    const headers = new Headers({ Accept: accept });
    const token = await getOidcAccessToken();
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }

    return headers;
  }

  return {
    async downloadTemplate(): Promise<TemplateDownload> {
      const response = await fetcher(`${basePath}/template`, {
        headers: await requestHeaders('text/csv, application/problem+json'),
        credentials: 'same-origin',
      });

      if (!response.ok) {
        await throwApiError(response);
      }

      const blob = await response.blob();
      if (blob.size === 0) {
        throw new ImportsApiError('The CSV template was empty.');
      }

      return {
        blob,
        fileName: fileNameFromContentDisposition(response.headers.get('content-disposition')) ?? IMPORT_POLICY.templateFileName,
      };
    },

    async preview(file: File): Promise<ImportPreview> {
      const body = new FormData();
      body.append('file', file, file.name);
      const response = await fetcher(`${basePath}`, {
        method: 'POST',
        body,
        headers: await requestHeaders('application/json, application/problem+json'),
        credentials: 'same-origin',
      });

      if (!response.ok) {
        await throwApiError(response);
      }

      return parsePreview(await responseBody(response));
    },

    async confirm(previewId: string): Promise<ImportResult> {
      const response = await fetcher(`${basePath}/${encodeURIComponent(previewId)}/confirm`, {
        method: 'POST',
        headers: await requestHeaders('application/json, application/problem+json'),
        credentials: 'same-origin',
      });

      if (!response.ok) {
        await throwApiError(response);
      }

      return parseResult(await responseBody(response));
    },

    async list(cursor?: string | null): Promise<ImportHistoryPage> {
      const params = new URLSearchParams({ limit: '15' });
      if (cursor) {
        params.set('cursor', cursor);
      }

      const response = await fetcher(`${basePath}?${params.toString()}`, {
        headers: await requestHeaders('application/json, application/problem+json'),
        credentials: 'same-origin',
      });

      if (!response.ok) {
        await throwApiError(response);
      }

      return parseHistoryPage(await responseBody(response));
    },

    async get(importId: string): Promise<ImportResult> {
      const response = await fetcher(`${basePath}/${encodeURIComponent(importId)}`, {
        headers: await requestHeaders('application/json, application/problem+json'),
        credentials: 'same-origin',
      });

      if (!response.ok) {
        await throwApiError(response);
      }

      return parseResult(await responseBody(response));
    },
  };
}
