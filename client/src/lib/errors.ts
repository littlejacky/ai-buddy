import type { UserFacingError } from './types'

export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

/** 把各种底层异常翻成用户能看懂、能行动的提示。 */
export function toUserError(err: unknown): UserFacingError {
  const detail = err instanceof Error ? `${err.name}: ${err.message}` : String(err)

  if (err instanceof DOMException) {
    if (err.name === 'NotAllowedError' || err.name === 'SecurityError') {
      return {
        code: 'mic_denied',
        title: '没有麦克风权限',
        hint: '请在浏览器地址栏旁（或系统设置）里允许使用麦克风，然后再点“开始聊天”。',
        detail,
      }
    }
    if (err.name === 'NotFoundError' || err.name === 'OverconstrainedError') {
      return {
        code: 'mic_missing',
        title: '找不到麦克风',
        hint: '请确认设备已连接麦克风，或换一个浏览器再试。',
        detail,
      }
    }
  }

  if (err instanceof ApiError) {
    if (err.status === 429) {
      return { code: 'rate_limited', title: '请求有点频繁', hint: '休息一分钟再试试。', detail }
    }
    return { code: 'server', title: '服务暂时不可用', hint: '不是你的问题，稍后再试一次。', detail }
  }

  if (err instanceof Error && err.name === 'TimeoutError') {
    return { code: 'timeout', title: '连接超时', hint: '网络可能不太好，换个网络或稍后再试。', detail }
  }

  if (err instanceof TypeError) {
    // fetch 网络失败通常是 TypeError
    return { code: 'network', title: '连不上服务', hint: '请检查网络连接后重试。', detail }
  }

  return { code: 'unknown', title: '出了点小问题', hint: '请重试一次；如果反复出现，请联系管理员。', detail }
}

export function timeoutError(message: string): Error {
  const e = new Error(message)
  e.name = 'TimeoutError'
  return e
}
