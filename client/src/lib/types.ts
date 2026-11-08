// 客户端共享类型。保持小而稳定：UI 只依赖这里，不依赖 WebRTC 细节。

export type Mode = 'adult' | 'child'

export const VOICES = ['marin', 'cedar', 'alloy', 'ash', 'ballad', 'coral', 'echo', 'sage', 'shimmer', 'verse'] as const
export type Voice = (typeof VOICES)[number]
/** 官方推荐的两款高质量音色，设置里标“推荐”。 */
export const RECOMMENDED_VOICES: readonly Voice[] = ['marin', 'cedar']

export type SessionState =
  | 'idle'
  | 'connecting'
  | 'listening'
  | 'user_speaking'
  | 'thinking'
  | 'speaking'
  | 'reconnecting'
  | 'ended'
  | 'error'

export interface Turn {
  id: string
  role: 'user' | 'assistant'
  text: string
  /** 用户语音已检测到，但转写还没回来 */
  pending?: boolean
  /** AI 的这句话被打断了 */
  interrupted?: boolean
  at: number
}

export type TurnPatch = Pick<Turn, 'id' | 'role'> & Partial<Omit<Turn, 'id' | 'role'>>

export type ErrorCode =
  | 'mic_denied'
  | 'mic_missing'
  | 'network'
  | 'timeout'
  | 'rate_limited'
  | 'server'
  | 'unknown'

/** 给人看的错误：说清发生了什么、下一步怎么办；技术细节单独放 detail。 */
export interface UserFacingError {
  code: ErrorCode
  title: string
  hint: string
  detail?: string
}

export interface StartOptions {
  mode: Mode
  voice: Voice
}

export interface SessionInfo {
  /** 服务端下发的本次会话时长上限（秒），0 = 不限 */
  maxSeconds: number
  applicationSessionId?: string
}

export interface DebugStats {
  rttMs: number
  jitterMs: number
  packetsLost: number
}

export interface VoiceClientEvents {
  onState(state: SessionState): void
  onTurn(patch: TurnPatch): void
  onLevel(level: { mic: number; remote: number }): void
  onError(error: UserFacingError): void
  onSessionInfo(info: SessionInfo): void
  /** 浏览器拦截了自动播放，需要用户点一下 */
  onAudioBlocked(blocked: boolean): void
  onDebug?(stats: DebugStats): void
}

export interface VoiceClient {
  start(options: StartOptions): Promise<void>
  stop(): void
  interrupt(): void
  setMuted(muted: boolean): void
  /** 让 AI 自然收尾（时长将到时使用） */
  wrapUp(): void
  resumeAudio(): void
}
