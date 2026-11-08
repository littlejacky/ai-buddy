import { createSession } from './api'
import { timeoutError, toUserError } from './errors'
import type { SessionState, StartOptions, TurnPatch, VoiceClient, VoiceClientEvents } from './types'

const SDP_TIMEOUT_MS = 15_000 // 只覆盖“握手 + 数据通道打开”，不包含用户点“允许麦克风”的时间
const RECONNECT_GRACE_MS = 8_000 // WebRTC 的 disconnected 往往能自愈，给它一点时间
const THINKING_FALLBACK_MS = 8_000
const SPEAK_END_GRACE_MS = 300 // output_audio_buffer.stopped 之后留一点播放尾巴

/**
 * 基于 WebRTC 的 Azure OpenAI Realtime 客户端。
 * 对 UI 只暴露 VoiceClient 接口，UI 不接触 RTCPeerConnection。
 */
export class RealtimeClient implements VoiceClient {
  private ev: VoiceClientEvents

  private pc: RTCPeerConnection | null = null
  private dc: RTCDataChannel | null = null
  private mic: MediaStream | null = null
  private micTrack: MediaStreamTrack | null = null
  private audioEl: HTMLAudioElement | null = null
  private ctx: AudioContext | null = null
  private micAnalyser: AnalyserNode | null = null
  private remoteAnalyser: AnalyserNode | null = null
  private wakeLock: WakeLockSentinel | null = null

  private state: SessionState = 'idle'
  private muted = false
  private runId = 0 // 每次 start 自增，用来丢弃过期回调

  private levelTimer: ReturnType<typeof setInterval> | null = null
  private debugTimer: ReturnType<typeof setInterval> | null = null
  private connectTimer: ReturnType<typeof setTimeout> | null = null
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null
  private thinkingTimer: ReturnType<typeof setTimeout> | null = null
  private speakEndTimer: ReturnType<typeof setTimeout> | null = null

  private assistantText = new Map<string, string>()

  constructor(events: VoiceClientEvents) {
    this.ev = events
  }

  // ───────────────────────── public API ─────────────────────────

  async start(opts: StartOptions): Promise<void> {
    if (!['idle', 'ended', 'error'].includes(this.state)) return
    const run = ++this.runId
    const alive = () => run === this.runId

    this.assistantText.clear()
    this.setState('connecting')

    // 必须在用户手势的同步部分创建音频元素 / AudioContext，否则 iOS Safari 会拦截自动播放。
    this.audioEl = document.createElement('audio')
    this.audioEl.autoplay = true
    this.audioEl.setAttribute('playsinline', '')
    this.ctx = new (window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext)()
    void this.ctx.resume()
    void this.acquireWakeLock()

    try {
      const session = await createSession({ mode: opts.mode, voice: opts.voice })
      if (!alive()) return
      this.ev.onSessionInfo({
        maxSeconds: session.maxSessionSeconds ?? 0,
        applicationSessionId: session.applicationSessionId,
      })

      // 麦克风授权弹窗没有时限，所以放在连接超时计时之前。
      this.mic = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
      })
      if (!alive()) {
        this.mic.getTracks().forEach((t) => t.stop())
        return
      }
      this.micTrack = this.mic.getAudioTracks()[0]
      this.micTrack.enabled = false // 数据通道打开前保持静音，避免开头的话被吞
      this.attachMicAnalyser(this.mic)

      this.connectTimer = setTimeout(() => {
        if (alive() && this.state === 'connecting') this.fail(timeoutError('WebRTC 握手超时'))
      }, SDP_TIMEOUT_MS)

      const pc = new RTCPeerConnection()
      this.pc = pc
      pc.addTrack(this.micTrack, this.mic)

      pc.ontrack = (e) => {
        if (!alive() || !this.audioEl) return
        this.audioEl.srcObject = e.streams[0]
        this.attachRemoteAnalyser(e.streams[0])
        this.tryPlay()
      }

      pc.onconnectionstatechange = () => {
        if (!alive()) return
        const s = pc.connectionState
        if (s === 'disconnected') this.beginReconnectGrace()
        else if (s === 'connected') this.endReconnectGrace()
        else if (s === 'failed') this.fail(new TypeError('webrtc failed'))
      }

      const dc = pc.createDataChannel('oai-events')
      this.dc = dc
      dc.onopen = () => {
        if (!alive()) return
        this.clearTimer('connectTimer')
        if (this.micTrack) this.micTrack.enabled = !this.muted
        this.setState('listening')
        this.startLevelLoop()
        if (this.debugRequested()) this.startDebugLoop()
      }
      dc.onclose = () => {
        if (alive() && this.state !== 'ended' && this.state !== 'error') this.stop()
      }
      dc.onmessage = (m) => alive() && this.onMessage(m)

      const offer = await pc.createOffer()
      await pc.setLocalDescription(offer)

      const res = await fetch(`${session.endpoint.replace(/\/+$/, '')}/openai/v1/realtime/calls?webrtcfilter=on`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${session.ephemeralKey}`, 'Content-Type': 'application/sdp' },
        body: offer.sdp,
      })
      if (!alive()) return
      if (res.status !== 201) {
        const body = await res.text().catch(() => '')
        throw new Error(`/calls HTTP ${res.status} ${body}`.trim())
      }
      await pc.setRemoteDescription({ type: 'answer', sdp: await res.text() })
    } catch (e) {
      if (alive()) this.fail(e)
    }
  }

  stop(): void {
    this.runId++
    this.teardown()
    this.setState('ended')
  }

  /** 手动打断：清空服务端已排队的音频并取消当前回复。不要 pause() 音频元素，否则后续回复会静音。 */
  interrupt(): void {
    if (this.state !== 'speaking' && this.state !== 'thinking') return
    this.send({ type: 'response.cancel' })
    this.send({ type: 'output_audio_buffer.clear' })
    this.clearTimer('speakEndTimer')
    this.clearTimer('thinkingTimer')
    this.setState('listening')
  }

  setMuted(muted: boolean): void {
    this.muted = muted
    if (this.micTrack && this.dc?.readyState === 'open') this.micTrack.enabled = !muted
  }

  wrapUp(): void {
    this.send({
      type: 'conversation.item.create',
      item: {
        type: 'message',
        role: 'system',
        content: [
          {
            type: 'input_text',
            text: '时间快到了。请用一两句话自然、温和地收尾，说一句简短的再见，不要再提出新问题。',
          },
        ],
      },
    })
    this.send({ type: 'response.create' })
  }

  resumeAudio(): void {
    void this.ctx?.resume()
    this.tryPlay()
  }

  // ───────────────────────── 事件处理 ─────────────────────────

  private onMessage(msg: MessageEvent): void {
    let evt: Record<string, any>
    try {
      evt = JSON.parse(msg.data)
    } catch {
      return
    }

    switch (evt.type as string) {
      case 'input_audio_buffer.speech_started': {
        this.clearTimer('thinkingTimer')
        this.clearTimer('speakEndTimer')
        this.setState('user_speaking')
        if (evt.item_id) this.turn({ id: evt.item_id, role: 'user', text: '', pending: true, at: Date.now() })
        break
      }
      case 'input_audio_buffer.speech_stopped': {
        this.setState('thinking')
        this.clearTimer('thinkingTimer')
        // 万一服务端没有产生回复，别让界面永远停在“想一想…”
        this.thinkingTimer = setTimeout(() => {
          if (this.state === 'thinking') this.setState('listening')
        }, THINKING_FALLBACK_MS)
        break
      }
      case 'conversation.item.input_audio_transcription.completed': {
        if (evt.item_id) {
          this.turn({ id: evt.item_id, role: 'user', text: String(evt.transcript ?? '').trim(), pending: false })
        }
        break
      }
      case 'output_audio_buffer.started': {
        this.clearTimer('thinkingTimer')
        this.clearTimer('speakEndTimer')
        this.setState('speaking')
        break
      }
      case 'output_audio_buffer.stopped':
      case 'output_audio_buffer.cleared': {
        this.clearTimer('speakEndTimer')
        this.speakEndTimer = setTimeout(() => {
          if (this.state === 'speaking') this.setState('listening')
        }, SPEAK_END_GRACE_MS)
        break
      }
      case 'response.output_audio_transcript.delta': {
        if (!evt.item_id || !evt.delta) break
        const text = (this.assistantText.get(evt.item_id) ?? '') + evt.delta
        this.assistantText.set(evt.item_id, text)
        this.turn({ id: evt.item_id, role: 'assistant', text, at: Date.now() })
        break
      }
      case 'response.output_audio_transcript.done': {
        if (evt.item_id && typeof evt.transcript === 'string') {
          this.assistantText.set(evt.item_id, evt.transcript)
          this.turn({ id: evt.item_id, role: 'assistant', text: evt.transcript })
        }
        break
      }
      case 'response.done': {
        if (evt.response?.status === 'cancelled') {
          for (const item of evt.response.output ?? []) {
            if (item?.id) this.turn({ id: item.id, role: 'assistant', interrupted: true })
          }
        }
        break
      }
      case 'error': {
        // 数据通道上的错误多为非致命（如取消一个已结束的回复），记录即可。
        console.warn('[realtime]', evt.error)
        break
      }
    }
  }

  // ───────────────────────── 内部工具 ─────────────────────────

  private turn(p: Omit<TurnPatch, 'role'> & { role: TurnPatch['role'] }): void {
    this.ev.onTurn(p)
  }

  private send(payload: object): void {
    if (this.dc?.readyState === 'open') {
      try {
        this.dc.send(JSON.stringify(payload))
      } catch (e) {
        console.warn('[realtime] send failed', e)
      }
    }
  }

  private setState(s: SessionState): void {
    if (this.state === s) return
    this.state = s
    this.ev.onState(s)
  }

  private fail(err: unknown): void {
    this.runId++
    console.error('[realtime]', err)
    this.teardown()
    this.ev.onError(toUserError(err))
    this.setState('error')
  }

  private beginReconnectGrace(): void {
    if (this.state === 'ended' || this.state === 'error') return
    this.setState('reconnecting')
    this.clearTimer('reconnectTimer')
    this.reconnectTimer = setTimeout(() => this.fail(new TypeError('webrtc disconnected')), RECONNECT_GRACE_MS)
  }

  private endReconnectGrace(): void {
    this.clearTimer('reconnectTimer')
    if (this.state === 'reconnecting') this.setState('listening')
  }

  private tryPlay(): void {
    if (!this.audioEl) return
    this.audioEl.play().then(
      () => this.ev.onAudioBlocked(false),
      () => this.ev.onAudioBlocked(true),
    )
  }

  private attachMicAnalyser(stream: MediaStream): void {
    if (!this.ctx) return
    this.micAnalyser = this.ctx.createAnalyser()
    this.micAnalyser.fftSize = 512
    this.ctx.createMediaStreamSource(stream).connect(this.micAnalyser)
  }

  private attachRemoteAnalyser(stream: MediaStream): void {
    if (!this.ctx) return
    this.remoteAnalyser = this.ctx.createAnalyser()
    this.remoteAnalyser.fftSize = 512
    this.ctx.createMediaStreamSource(stream).connect(this.remoteAnalyser)
  }

  private static rms(a: AnalyserNode | null, buf: Uint8Array<ArrayBuffer>): number {
    if (!a) return 0
    a.getByteTimeDomainData(buf)
    let sum = 0
    for (let i = 0; i < buf.length; i++) {
      const v = (buf[i] - 128) / 128
      sum += v * v
    }
    // 语音 RMS 通常在 0~0.3，放大到 0~1 便于驱动动画
    return Math.min(1, Math.sqrt(sum / buf.length) * 3.2)
  }

  private startLevelLoop(): void {
    const buf: Uint8Array<ArrayBuffer> = new Uint8Array(new ArrayBuffer(512))
    let mic = 0
    let remote = 0
    this.levelTimer = setInterval(() => {
      // 简单的指数平滑，避免动画抖动
      mic += (RealtimeClient.rms(this.micAnalyser, buf) - mic) * 0.5
      remote += (RealtimeClient.rms(this.remoteAnalyser, buf) - remote) * 0.5
      this.ev.onLevel({ mic: this.muted ? 0 : mic, remote })
    }, 60)
  }

  private debugRequested(): boolean {
    return !!this.ev.onDebug
  }

  private startDebugLoop(): void {
    this.debugTimer = setInterval(async () => {
      if (!this.pc) return
      let rttMs = 0
      let jitterMs = 0
      let packetsLost = 0
      const stats = await this.pc.getStats().catch(() => null)
      stats?.forEach((r) => {
        if (r.type === 'candidate-pair' && r.state === 'succeeded') rttMs = Math.round((r.currentRoundTripTime || 0) * 1000)
        if (r.type === 'inbound-rtp' && r.kind === 'audio') {
          jitterMs = Math.round((r.jitter || 0) * 1000)
          packetsLost = r.packetsLost || 0
        }
      })
      this.ev.onDebug?.({ rttMs, jitterMs, packetsLost })
    }, 2000)
  }

  private async acquireWakeLock(): Promise<void> {
    try {
      this.wakeLock = (await navigator.wakeLock?.request('screen')) ?? null
    } catch {
      /* 不支持或被拒绝：忽略 */
    }
  }

  private clearTimer(name: 'connectTimer' | 'reconnectTimer' | 'thinkingTimer' | 'speakEndTimer'): void {
    const t = this[name]
    if (t) clearTimeout(t)
    this[name] = null
  }

  private teardown(): void {
    this.clearTimer('connectTimer')
    this.clearTimer('reconnectTimer')
    this.clearTimer('thinkingTimer')
    this.clearTimer('speakEndTimer')
    if (this.levelTimer) clearInterval(this.levelTimer)
    if (this.debugTimer) clearInterval(this.debugTimer)
    this.levelTimer = this.debugTimer = null

    try {
      this.dc?.close()
    } catch {
      /* noop */
    }
    this.dc = null
    this.mic?.getTracks().forEach((t) => t.stop())
    this.mic = null
    this.micTrack = null
    try {
      this.pc?.close()
    } catch {
      /* noop */
    }
    this.pc = null
    void this.ctx?.close().catch(() => {})
    this.ctx = null
    this.micAnalyser = this.remoteAnalyser = null
    if (this.audioEl) this.audioEl.srcObject = null
    this.audioEl = null
    void this.wakeLock?.release().catch(() => {})
    this.wakeLock = null
    this.ev.onAudioBlocked(false)
    this.ev.onLevel({ mic: 0, remote: 0 })
  }
}
