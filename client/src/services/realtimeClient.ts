/**
 * Re-export the canonical RealtimeClient implementation to avoid dual-track maintenance.
 * Canonical implementation: client/src/lib/realtimeClient.ts
 */
export { RealtimeClient } from '../lib/realtimeClient'
export type { VoiceClient, VoiceClientEvents, SessionState } from '../lib/types'
