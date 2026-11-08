import { ApiError, timeoutError } from './errors'
import type { Mode, Voice } from './types'

export interface SessionResponse {
  ephemeralKey: string
  /** Unix 秒 */
  expiresAt: number
  businessSessionId: string
  azureSessionId?: string
  applicationSessionId?: string
  endpoint: string
  /** 服务端对本次会话的时长上限（秒），0/缺省 = 不限 */
  maxSessionSeconds?: number
}

/** 向后端申请一次性临时密钥。真正的 API Key 永远不会到达浏览器。 */
export async function createSession(
  body: { mode: Mode; voice: Voice },
  timeoutMs = 12_000,
): Promise<SessionResponse> {
  const ctrl = new AbortController()
  const timer = setTimeout(() => ctrl.abort(), timeoutMs)
  try {
    const res = await fetch('/api/realtime/session', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
      signal: ctrl.signal,
    })
    if (!res.ok) {
      const text = await res.text().catch(() => res.statusText)
      throw new ApiError(res.status, text)
    }
    return (await res.json()) as SessionResponse
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') {
      throw timeoutError('创建会话超时')
    }
    throw e
  } finally {
    clearTimeout(timer)
  }
}
