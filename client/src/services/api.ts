import type { SupportedVoice, TurnDetectionType } from '../types/realtime'

export interface SessionResponse {
  ephemeralKey: string
  expiresAt: number // Unix timestamp in seconds
  businessSessionId: string // Local application business session identifier
  azureSessionId?: string // Actual Azure session identifier from session.id
  endpoint: string
}

export interface CreateSessionOptions {
  mode: 'adult' | 'child'
  voice?: SupportedVoice
  turnDetectionType?: TurnDetectionType
  expiresAfterSeconds?: number
}

/**
 * Creates a new Realtime session via the ASP.NET Core backend.
 * The backend provisions an ephemeral client secret (ek_...) scoped to this session.
 */
export async function createSession(options: CreateSessionOptions): Promise<SessionResponse> {
  const res = await fetch('/api/realtime/session', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(options),
  })

  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new Error(`Failed to create session: HTTP ${res.status} - ${text}`)
  }

  return res.json() as Promise<SessionResponse>
}
