import { describe, expect, it } from 'vitest'
import type { BuddyActivity, KnowledgeBlock, QuizBlock } from '../buddy-canvas/types'

describe('BuddyCanvas Schema Invariants', () => {
  it('validates a standard knowledge card activity structure', () => {
    const activity: BuddyActivity = {
      schemaVersion: '1.0',
      activityId: 'act-123',
      sessionId: 'sess-456',
      kind: 'knowledge',
      title: '海洋巨人',
      blocks: [
        {
          type: 'knowledge',
          title: '蓝鲸',
          content: '蓝鲸是现存最大的动物。',
          factPoint: '海水浮力支撑其庞大身躯。',
          source: '海洋百科',
        } satisfies KnowledgeBlock,
      ],
    }

    expect(activity.schemaVersion).toBe('1.0')
    expect(activity.kind).toBe('knowledge')
    expect(activity.blocks.length).toBe(1)
    expect((activity.blocks[0] as KnowledgeBlock).factPoint).toBe('海水浮力支撑其庞大身躯。')
  })

  it('validates a multiple choice quiz activity with options and answer', () => {
    const activity: BuddyActivity = {
      schemaVersion: '1.0',
      activityId: 'quiz-1',
      sessionId: 'sess-1',
      kind: 'quiz',
      title: '恐龙达人问答',
      blocks: [
        {
          type: 'quiz',
          question: '下面哪种恐龙是食肉动物？',
          options: [
            { id: 'a', label: '霸王龙' },
            { id: 'b', label: '三角龙' },
          ],
          correctOptionId: 'a',
          explanation: '霸王龙有强壮的下颌和锋利的牙齿。',
        } satisfies QuizBlock,
      ],
    }

    const quiz = activity.blocks[0] as QuizBlock
    expect(quiz.correctOptionId).toBe('a')
    expect(quiz.options).toHaveLength(2)
  })
})
