export type ActivityKind = 'knowledge' | 'quiz' | 'flashcard' | 'story'

export interface KnowledgeBlock {
  type: 'knowledge'
  title: string
  imageUrl?: string
  content: string
  factPoint: string
  source?: string
}

export interface QuizOption {
  id: string
  label: string
}

export interface QuizBlock {
  type: 'quiz'
  question: string
  options: QuizOption[]
  correctOptionId: string
  explanation: string
}

export interface FlashCardBlock {
  type: 'flashcard'
  question: string
  answer: string
  hint?: string
}

export interface StoryChoice {
  id: string
  label: string
  outcome: string
}

export interface StoryBlock {
  type: 'story'
  paragraph: string
  choices: StoryChoice[]
}

export type ActivityBlock = KnowledgeBlock | QuizBlock | FlashCardBlock | StoryBlock

export interface BuddyActivity {
  schemaVersion: '1.0'
  activityId: string
  sessionId: string
  kind: ActivityKind
  title: string
  ageBand?: string
  blocks: ActivityBlock[]
}
