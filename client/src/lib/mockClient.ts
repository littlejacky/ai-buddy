import { toUserError } from './errors'
import type { SessionState, StartOptions, VoiceClient, VoiceClientEvents } from './types'

/**
 * 演示用客户端：不连任何后端，用脚本化对话驱动同一套 UI。
 * 打开 `?demo=1` 即可体验；`?demo=1&fail=mic` 可预览错误态。
 */
const SCRIPTS = {
  adult: [
    ['user', '今天加班到好晚，有点累。'],
    ['assistant', '哎，辛苦了。是那种忙到停不下来的累，还是干完了但心里空落落的？'],
    ['user', '就是事情太多，感觉怎么做都做不完。'],
    ['assistant', '嗯，我懂这种感觉。要不先别想清单了，挑一件最小的事，今晚只干它，其他的明天再说？'],
  ],
  child: [
    ['user', '为什么天空是蓝色的呀？'],
    ['assistant', '哇，这个问题问得好！阳光里藏着很多颜色，蓝色的小光点最爱到处乱跑，跑得满天都是，所以我们抬头就看到蓝蓝的天啦。'],
    ['user', '那晚上为什么是黑的？'],
    ['assistant', '因为太阳去地球的另一边玩啦，没有阳光，天空就黑下来，星星就出来上班咯！'],
  ],
} as const

export class MockClient implements VoiceClient {
  private ev: VoiceClientEvents
  private state: SessionState = 'idle'
  private timers: ReturnType<typeof setTimeout>[] = []
  private levelTimer: ReturnType<typeof setInterval> | null = null
  private muted = false
  private mode: StartOptions['mode'] = 'adult'
  private idx = 0
  private speaking = false
  private userTalking = false
  private seq = 0
  private fail: string | null

  constructor(events: VoiceClientEvents, fail: string | null = null) {
    this.ev = events
    this.fail = fail
  }

  async start(o: StartOptions): Promise<void> {
    this.mode = o.mode
    this.idx = 0
    this.set('connecting')
    this.ev.onSessionInfo({ maxSeconds: o.mode === 'child' ? 900 : 0 })
    await this.wait(900)
    if (this.fail === 'mic') {
      this.ev.onError(toUserError(new DOMException('denied', 'NotAllowedError')))
      this.set('error')
      return
    }
    this.set('listening')
    this.levelTimer = setInterval(() => this.emitLevel(), 60)
    this.wait(1200).then(() => this.nextLine())
  }

  stop(): void {
    this.clear()
    this.set('ended')
  }

  interrupt(): void {
    if (this.state !== 'speaking') return
    this.clear(false)
    this.speaking = false
    this.set('listening')
    this.wait(2500).then(() => this.nextLine())
  }

  setMuted(m: boolean): void {
    this.muted = m
  }

  wrapUp(): void {
    const id = `m${++this.seq}`
    this.say(id, '时间差不多啦，今天聊得很开心，下次再聊，拜拜！')
  }

  resumeAudio(): void {}

  // ── 内部 ──

  private script() {
    return SCRIPTS[this.mode]
  }

  private nextLine() {
    if (this.state === 'ended' || this.state === 'error' || this.state === 'idle') return
    const line = this.script()[this.idx % this.script().length]
    this.idx++
    const id = `m${++this.seq}`
    if (line[0] === 'user') {
      this.set('user_speaking')
      this.userTalking = true
      this.ev.onTurn({ id, role: 'user', text: '', pending: true, at: Date.now() })
      this.wait(1800).then(() => {
        this.userTalking = false
        this.set('thinking')
        this.ev.onTurn({ id, role: 'user', text: line[1], pending: false })
        this.wait(900).then(() => this.nextLine())
      })
    } else {
      this.say(id, line[1])
    }
  }

  private say(id: string, text: string) {
    this.set('speaking')
    this.speaking = true
    let i = 0
    const tick = () => {
      if (!this.speaking) return
      i = Math.min(text.length, i + 2)
      this.ev.onTurn({ id, role: 'assistant', text: text.slice(0, i), at: Date.now() })
      if (i < text.length) this.wait(130).then(tick)
      else
        this.wait(600).then(() => {
          this.speaking = false
          this.set('listening')
          this.wait(2200).then(() => this.nextLine())
        })
    }
    tick()
  }

  private emitLevel() {
    const t = Date.now() / 1000
    const wave = (Math.sin(t * 9) + Math.sin(t * 5.3) + 2) / 4
    this.ev.onLevel({
      mic: this.userTalking && !this.muted ? 0.15 + wave * 0.7 : 0,
      remote: this.speaking ? 0.2 + wave * 0.7 : 0,
    })
  }

  private set(s: SessionState) {
    if (this.state === s) return
    this.state = s
    this.ev.onState(s)
  }

  private wait(ms: number) {
    return new Promise<void>((res) => {
      this.timers.push(setTimeout(res, ms))
    })
  }

  private clear(all = true) {
    this.timers.forEach(clearTimeout)
    this.timers = []
    if (all && this.levelTimer) clearInterval(this.levelTimer)
    this.speaking = false
    this.userTalking = false
    this.ev.onLevel({ mic: 0, remote: 0 })
  }
}
