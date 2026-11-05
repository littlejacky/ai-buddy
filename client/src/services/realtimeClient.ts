import type { SessionState, SupportedVoice, TurnDetectionType } from '../types/realtime'
import { createSession } from './api'

export interface RealtimeTelemetry {
  rttMs: number
  packetsSent: number
  packetsReceived: number
  bytesSent: number
  bytesReceived: number
  packetsLost: number
  jitterMs: number
  state: SessionState
}

export interface RealtimeClientCallbacks {
  onStateChange: (state: SessionState) => void
  onSubtitleDelta: (text: string, isFinal: boolean) => void
  onUserTranscript: (text: string) => void
  onTelemetry: (telemetry: RealtimeTelemetry) => void
  onError: (error: string) => void
}

export class RealtimeClient {
  private pc: RTCPeerConnection | null = null
  private dc: RTCDataChannel | null = null
  private localStream: MediaStream | null = null
  private audioTrack: MediaStreamTrack | null = null
  private remoteAudioEl: HTMLAudioElement | null = null
  private audioContext: AudioContext | null = null
  private analyser: AnalyserNode | null = null
  private statsTimer: any = null
  private silenceCheckTimer: any = null
  private connectionTimeoutTimer: any = null
  private responseTimeoutTimer: any = null

  private currentState: SessionState = 'idle'
  private currentSubtitle = ''
  private serverOutputDrained = false

  private callbacks: RealtimeClientCallbacks

  constructor(callbacks: RealtimeClientCallbacks) {
    this.callbacks = callbacks
  }

  public getState(): SessionState {
    return this.currentState
  }

  private setState(state: SessionState) {
    if (this.currentState === state) return
    this.currentState = state
    this.callbacks.onStateChange(state)
  }

  /**
   * Starts a real-time voice session via ASP.NET Core backend.
   */
  public async start(options: {
    mode: 'adult' | 'child'
    voice?: SupportedVoice
    turnDetectionType?: TurnDetectionType
  }): Promise<void> {
    if (this.currentState !== 'idle' && this.currentState !== 'ended' && this.currentState !== 'error') {
      console.warn('Session already running or connecting.')
      return
    }

    this.setState('connecting')
    this.currentSubtitle = ''
    this.serverOutputDrained = false

    // 10s connection timeout fallback
    this.connectionTimeoutTimer = setTimeout(() => {
      if (this.currentState === 'connecting') {
        this.handleError('连接超时：云端服务在 10 秒内未完成 WebRTC 协商')
        this.stop()
      }
    }, 10000)

    try {
      // Step 1: Request Ephemeral Client Secret from ASP.NET Core Backend
      const sessionData = await createSession({
        mode: options.mode,
        voice: options.voice || 'alloy',
        turnDetectionType: options.turnDetectionType || 'server_vad',
      })

      const ephemeralKey = sessionData.ephemeralKey
      const endpoint = sessionData.endpoint.replace(/\/+$/, '')

      // Step 2: Acquire microphone with AEC/NS/AGC
      this.localStream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true,
        },
      })

      this.audioTrack = this.localStream.getAudioTracks()[0]
      // v3.0 Gating: keep track muted until DataChannel opens
      this.audioTrack.enabled = false

      // Step 3: Setup WebRTC PeerConnection
      this.pc = new RTCPeerConnection()
      this.pc.addTrack(this.audioTrack, this.localStream)

      // Remote Audio setup
      this.remoteAudioEl = document.createElement('audio')
      this.remoteAudioEl.autoplay = true
      this.remoteAudioEl.setAttribute('playsinline', '')

      this.pc.ontrack = (evt) => {
        if (this.remoteAudioEl) {
          this.remoteAudioEl.srcObject = evt.streams[0]
          this.remoteAudioEl.play().catch((err) => console.warn('Audio play prompt:', err))
        }
        this.setupAudioAnalyser(evt.streams[0])
      }

      // Connection State Monitoring & Fallback
      this.pc.onconnectionstatechange = () => {
        const connState = this.pc?.connectionState
        if (connState === 'failed' || connState === 'disconnected') {
          this.handleError(`WebRTC 连接异常 (${connState})`)
        } else if (connState === 'closed') {
          this.setState('ended')
        }
      }

      this.pc.oniceconnectionstatechange = () => {
        if (this.pc?.iceConnectionState === 'failed') {
          this.handleError('ICE 网络穿透打洞失败，请检查网络或防火墙')
        }
      }

      // Step 4: DataChannel creation
      this.dc = this.pc.createDataChannel('oai-events')

      this.dc.onopen = () => {
        if (this.connectionTimeoutTimer) clearTimeout(this.connectionTimeoutTimer)
        // v3.0 Gating: un-mute microphone once connection & DataChannel are open
        if (this.audioTrack) {
          this.audioTrack.enabled = true
        }
        this.setState('listening')
        this.startTelemetryPolling()
      }

      this.dc.onclose = () => {
        if (this.currentState !== 'ended' && this.currentState !== 'error') {
          this.setState('ended')
        }
      }

      this.dc.onmessage = (msg) => this.handleDataChannelMessage(msg)

      // Step 5: WebRTC SDP Handshake with Azure /calls (Probe-A: application/sdp)
      const offer = await this.pc.createOffer()
      await this.pc.setLocalDescription(offer)

      const callsUrl = `${endpoint}/openai/v1/realtime/calls`
      const callRes = await fetch(callsUrl, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${ephemeralKey}`,
          'Content-Type': 'application/sdp',
        },
        body: offer.sdp,
      })

      if (callRes.status !== 201) {
        const errText = await callRes.text().catch(() => callRes.statusText)
        throw new Error(`/calls 握手失败 (HTTP ${callRes.status}): ${errText}`)
      }

      const answerSdp = await callRes.text()
      await this.pc.setRemoteDescription({ type: 'answer', sdp: answerSdp })
    } catch (err: any) {
      if (this.connectionTimeoutTimer) clearTimeout(this.connectionTimeoutTimer)
      this.handleError(err.message || '启动会话失败')
      this.stop()
    }
  }

  /**
   * Sets up Web Audio AnalyserNode for audio amplitude monitoring & estimated playback completion.
   */
  private setupAudioAnalyser(stream: MediaStream) {
    try {
      this.audioContext = new (window.AudioContext || (window as any).webkitAudioContext)()
      const source = this.audioContext.createMediaStreamSource(stream)
      this.analyser = this.audioContext.createAnalyser()
      this.analyser.fftSize = 256
      source.connect(this.analyser)

      const dataArray = new Uint8Array(this.analyser.frequencyBinCount)
      let silenceCount = 0

      if (this.silenceCheckTimer) clearInterval(this.silenceCheckTimer)
      this.silenceCheckTimer = setInterval(() => {
        if (!this.analyser) return
        this.analyser.getByteFrequencyData(dataArray)
        let sum = 0
        for (let i = 0; i < dataArray.length; i++) sum += dataArray[i]

        // If server output buffer is drained and amplitude is silent for 300ms, estimate playback complete
        if (this.serverOutputDrained && this.currentState === 'speaking') {
          if (sum < 50) {
            silenceCount++
            if (silenceCount >= 3) {
              // ~300ms of low amplitude
              this.serverOutputDrained = false
              silenceCount = 0
              this.setState('listening')
            }
          } else {
            silenceCount = 0
          }
        }
      }, 100)
    } catch (e) {
      console.warn('Web Audio Analyser setup failed:', e)
    }
  }

  /**
   * Handles incoming DataChannel events strictly adhering to the 11 whitelisted events.
   */
  private handleDataChannelMessage(msg: MessageEvent) {
    try {
      const evt = JSON.parse(msg.data)
      const type = evt.type as string

      switch (type) {
        case 'input_audio_buffer.speech_started':
          // User started speaking -> transition to USER_SPEAKING
          if (this.responseTimeoutTimer) clearTimeout(this.responseTimeoutTimer)
          this.serverOutputDrained = false
          this.setState('user_speaking')
          break

        case 'input_audio_buffer.speech_stopped':
          // User stopped speaking -> transition to WAITING_FOR_RESPONSE (client inferred state)
          this.setState('waiting_for_response')
          // 5s response timeout fallback
          this.responseTimeoutTimer = setTimeout(() => {
            if (this.currentState === 'waiting_for_response') {
              console.warn('Response timeout fallback triggered')
              this.setState('listening')
            }
          }, 5000)
          break

        case 'output_audio_buffer.started':
          // AI started streaming audio -> transition to SPEAKING
          if (this.responseTimeoutTimer) clearTimeout(this.responseTimeoutTimer)
          this.serverOutputDrained = false
          this.currentSubtitle = ''
          this.setState('speaking')
          break

        case 'output_audio_buffer.stopped':
          // Server audio buffer drained -> mark drained, silence analyser will transition to LISTENING
          this.serverOutputDrained = true
          break

        case 'response.output_audio_transcript.delta':
          if (evt.delta) {
            this.currentSubtitle += evt.delta
            this.callbacks.onSubtitleDelta(this.currentSubtitle, false)
          }
          break

        case 'response.output_audio_transcript.done':
          this.callbacks.onSubtitleDelta(this.currentSubtitle, true)
          break

        case 'conversation.item.input_audio_transcription.completed':
          if (evt.transcript) {
            this.callbacks.onUserTranscript(evt.transcript)
          }
          break

        case 'error':
          console.error('[Realtime DataChannel Error]', evt.error)
          break
      }
    } catch (e) {
      // Non-JSON or binary frame
    }
  }

  /**
   * User explicit Barge-in / Interruption.
   * Sends response.cancel and temporarily ducks playback volume.
   */
  public interrupt() {
    if (this.currentState === 'speaking' || this.currentState === 'waiting_for_response') {
      if (this.dc && this.dc.readyState === 'open') {
        try {
          this.dc.send(JSON.stringify({ type: 'response.cancel' }))
        } catch (e) {
          console.warn('Failed to send response.cancel:', e)
        }
      }
      if (this.remoteAudioEl) {
        this.remoteAudioEl.pause()
        this.remoteAudioEl.currentTime = 0
      }
      this.serverOutputDrained = false
      this.setState('listening')
    }
  }

  /**
   * Toggles microphone mute state.
   */
  public toggleMute(): boolean {
    if (this.audioTrack) {
      this.audioTrack.enabled = !this.audioTrack.enabled
      return this.audioTrack.enabled
    }
    return false
  }

  public isMuted(): boolean {
    return this.audioTrack ? !this.audioTrack.enabled : true
  }

  /**
   * Periodic WebRTC getStats telemetry polling.
   */
  private startTelemetryPolling() {
    if (this.statsTimer) clearInterval(this.statsTimer)

    this.statsTimer = setInterval(async () => {
      if (!this.pc) return
      try {
        const stats = await this.pc.getStats()
        let rttMs = 0
        let packetsSent = 0
        let packetsReceived = 0
        let bytesSent = 0
        let bytesReceived = 0
        let packetsLost = 0
        let jitterMs = 0

        stats.forEach((report) => {
          if (report.type === 'candidate-pair' && report.state === 'succeeded') {
            rttMs = Math.round((report.currentRoundTripTime || 0) * 1000)
          }
          if (report.type === 'outbound-rtp' && report.kind === 'audio') {
            packetsSent = report.packetsSent || 0
            bytesSent = report.bytesSent || 0
          }
          if (report.type === 'inbound-rtp' && report.kind === 'audio') {
            packetsReceived = report.packetsReceived || 0
            bytesReceived = report.bytesReceived || 0
            packetsLost = report.packetsLost || 0
            jitterMs = Math.round((report.jitter || 0) * 1000)
          }
        })

        this.callbacks.onTelemetry({
          rttMs,
          packetsSent,
          packetsReceived,
          bytesSent,
          bytesReceived,
          packetsLost,
          jitterMs,
          state: this.currentState,
        })
      } catch (e) {
        // Stats poll error
      }
    }, 1000)
  }

  private handleError(error: string) {
    console.error('[RealtimeClient Error]', error)
    this.setState('error')
    this.callbacks.onError(error)
  }

  /**
   * Stops and tears down the session.
   */
  public stop() {
    if (this.connectionTimeoutTimer) clearTimeout(this.connectionTimeoutTimer)
    if (this.responseTimeoutTimer) clearTimeout(this.responseTimeoutTimer)
    if (this.statsTimer) clearInterval(this.statsTimer)
    if (this.silenceCheckTimer) clearInterval(this.silenceCheckTimer)

    if (this.dc) {
      try {
        this.dc.close()
      } catch (e) {}
      this.dc = null
    }

    if (this.audioTrack) {
      this.audioTrack.stop()
      this.audioTrack = null
    }

    if (this.localStream) {
      this.localStream.getTracks().forEach((t) => t.stop())
      this.localStream = null
    }

    if (this.pc) {
      try {
        this.pc.close()
      } catch (e) {}
      this.pc = null
    }

    if (this.audioContext) {
      try {
        this.audioContext.close()
      } catch (e) {}
      this.audioContext = null
    }

    if (this.remoteAudioEl) {
      this.remoteAudioEl.srcObject = null
      this.remoteAudioEl = null
    }

    this.serverOutputDrained = false
    this.setState('ended')
  }
}
