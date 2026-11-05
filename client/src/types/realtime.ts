// ── Session State (v2.8 Production & Observable Model) ───────────
export type SessionState =
  | 'idle'
  | 'connecting'
  | 'listening'
  | 'user_speaking'
  | 'waiting_for_response' // Production mode: client-inferred state after speech_stopped
  | 'waiting_for_turn_decision' // Optional/debug state
  | 'thinking'
  | 'speaking'
  | 'interrupted'
  | 'reconnecting'
  | 'ended'
  | 'error'

// ── Official GA Supported Voices ──────────────────────────────
export const SUPPORTED_VOICES = [
  'alloy',
  'ash',
  'ballad',
  'coral',
  'cedar',
  'echo',
  'marin',
  'sage',
  'shimmer',
  'verse',
] as const

export type SupportedVoice = typeof SUPPORTED_VOICES[number]

// ── Turn Detection Modes ──────────────────────────────────────
export type TurnDetectionType = 'semantic_vad' | 'server_vad'

// ── Base Event ────────────────────────────────────────────────
export interface RealtimeEvent {
  event_id?: string
  type: string
}

// ── Session Events ────────────────────────────────────────────
export interface SessionCreatedEvent extends RealtimeEvent {
  type: 'session.created'
  session: {
    id: string
    model?: string
    output_modalities?: string[]
    instructions?: string
    audio?: {
      output?: { voice?: string }
      input?: {
        turn_detection?: Record<string, unknown>
        transcription?: Record<string, unknown>
      }
    }
    [key: string]: unknown
  }
}

export interface SessionUpdatedEvent extends RealtimeEvent {
  type: 'session.updated'
  session: Record<string, unknown>
}

// ── Input Audio Buffer Events (Server VAD) ─────────────────────
// Note: Documented primarily for server_vad. In semantic_vad mode,
// actual event delivery will be experimentally captured in Phase 0.
export interface InputAudioBufferSpeechStartedEvent extends RealtimeEvent {
  type: 'input_audio_buffer.speech_started'
  audio_start_ms: number
  item_id: string
}

export interface InputAudioBufferSpeechStoppedEvent extends RealtimeEvent {
  type: 'input_audio_buffer.speech_stopped'
  audio_end_ms: number
  item_id: string
}

export interface InputAudioBufferCommittedEvent extends RealtimeEvent {
  type: 'input_audio_buffer.committed'
  item_id?: string
}

export interface InputAudioBufferClearedEvent extends RealtimeEvent {
  type: 'input_audio_buffer.cleared'
}

// ── Conversation Events (Whitelisted in webrtcfilter) ─────────
export interface ConversationItemCreatedEvent extends RealtimeEvent {
  type: 'conversation.item.created'
  previous_item_id?: string | null
  item: {
    id: string
    type: string
    role?: 'user' | 'assistant' | 'system'
    content?: Array<{
      type: string
      text?: string
      transcript?: string
    }>
    [key: string]: unknown
  }
}

export interface ConversationItemAddedEvent extends RealtimeEvent {
  type: 'conversation.item.added'
  previous_item_id?: string | null
  item: {
    id: string
    type: string
    role?: 'user' | 'assistant' | 'system'
    content?: Array<{
      type: string
      text?: string
      transcript?: string
    }>
    [key: string]: unknown
  }
}

export interface InputAudioTranscriptionCompletedEvent extends RealtimeEvent {
  type: 'conversation.item.input_audio_transcription.completed'
  item_id: string
  content_index: number
  transcript: string
}

export interface ConversationItemTruncatedEvent extends RealtimeEvent {
  type: 'conversation.item.truncated'
  item_id: string
  content_index: number
  audio_end_ms: number
}

// ── Response Generation Events ────────────────────────────────
export interface ResponseCreatedEvent extends RealtimeEvent {
  type: 'response.created'
  response: {
    id: string
    status: 'in_progress' | 'completed' | 'cancelled' | 'failed' | 'incomplete'
    [key: string]: unknown
  }
}

// Audio Transcript Streaming
export interface ResponseOutputAudioTranscriptDeltaEvent extends RealtimeEvent {
  type: 'response.output_audio_transcript.delta' | 'response.audio_transcript.delta'
  response_id: string
  item_id: string
  output_index: number
  content_index: number
  delta: string
}

export interface ResponseOutputAudioTranscriptDoneEvent extends RealtimeEvent {
  type: 'response.output_audio_transcript.done' | 'response.audio_transcript.done'
  response_id: string
  item_id: string
  output_index: number
  content_index: number
  transcript: string
}

// Text Output Streaming
export interface ResponseOutputTextDeltaEvent extends RealtimeEvent {
  type: 'response.output_text.delta' | 'response.text.delta'
  response_id: string
  item_id: string
  output_index: number
  content_index: number
  delta: string
}

export interface ResponseOutputTextDoneEvent extends RealtimeEvent {
  type: 'response.output_text.done' | 'response.text.done'
  response_id: string
  item_id: string
  output_index: number
  content_index: number
  text: string
}

// Response Completion (LLM generation done)
export interface ResponseDoneEvent extends RealtimeEvent {
  type: 'response.done'
  response: {
    id: string
    status: 'completed' | 'cancelled' | 'failed' | 'incomplete'
    status_details?: {
      type?: 'cancelled' | 'incomplete'
      reason?: string
    }
    output?: unknown[]
    usage?: {
      total_tokens?: number
      input_tokens?: number
      output_tokens?: number
      input_token_details?: {
        text_tokens?: number
        audio_tokens?: number
      }
      output_token_details?: {
        text_tokens?: number
        audio_tokens?: number
      }
    }
  }
}

// ── Output Audio Playback Events ──────────────────────────────
export interface OutputAudioBufferStartedEvent extends RealtimeEvent {
  type: 'output_audio_buffer.started'
  response_id?: string
}

export interface OutputAudioBufferStoppedEvent extends RealtimeEvent {
  type: 'output_audio_buffer.stopped'
  response_id?: string
}

export interface OutputAudioBufferServerClearedEvent extends RealtimeEvent {
  type: 'output_audio_buffer.cleared'
  response_id?: string
}

// ── Error Event ───────────────────────────────────────────────
export interface ErrorEvent extends RealtimeEvent {
  type: 'error'
  error: {
    type: string
    code?: string
    message: string
    param?: string | null
    event_id?: string
  }
}

// ── Client-to-Server Events ───────────────────────────────────
export interface ClientResponseCancelEvent {
  type: 'response.cancel'
  event_id?: string
}

export interface ClientOutputAudioBufferClearEvent {
  type: 'output_audio_buffer.clear'
  event_id?: string
}

export interface ClientConversationItemTruncateEvent {
  type: 'conversation.item.truncate'
  event_id?: string
  item_id: string
  content_index: number
  audio_end_ms: number
}

export interface ClientConversationItemCreateEvent {
  type: 'conversation.item.create'
  event_id?: string
  previous_item_id?: string | null
  item: {
    type: 'message'
    role: 'user' | 'assistant' | 'system'
    content: Array<{
      type: 'input_text' | 'input_audio' | 'text'
      text?: string
      audio?: string
    }>
  }
}

export interface ClientSessionUpdateEvent {
  type: 'session.update'
  event_id?: string
  session: Record<string, unknown>
}

// ── Union of all Server Events ────────────────────────────────
export type KnownServerEvent =
  | SessionCreatedEvent
  | SessionUpdatedEvent
  | InputAudioBufferSpeechStartedEvent
  | InputAudioBufferSpeechStoppedEvent
  | InputAudioBufferCommittedEvent
  | InputAudioBufferClearedEvent
  | ConversationItemCreatedEvent
  | ConversationItemTruncatedEvent
  | InputAudioTranscriptionCompletedEvent
  | ResponseCreatedEvent
  | ResponseOutputAudioTranscriptDeltaEvent
  | ResponseOutputAudioTranscriptDoneEvent
  | ResponseOutputTextDeltaEvent
  | ResponseOutputTextDoneEvent
  | ResponseDoneEvent
  | OutputAudioBufferStartedEvent
  | OutputAudioBufferStoppedEvent
  | OutputAudioBufferServerClearedEvent
  | ErrorEvent
