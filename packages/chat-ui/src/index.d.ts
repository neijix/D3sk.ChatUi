// TypeScript declarations for @d3sk/chat-ui-web
// Shared spec and host API with D3sk.ChatUi and D3sk.ChatUi.Avalonia.

export type PaletteId = "teal" | "slate" | "midnight" | "ocean" | "forest" | "dusk" | (string & {});

export type AgentBrand =
  | "claude"
  | "gemini"
  | "openai"
  | "grok"
  | "copilot"
  | "cursor"
  | "mlx"
  | "colibri"
  | "d3skhand";

export type WorkingActivity =
  | "starting"
  | "thinking"
  | "shell"
  | "searching"
  | "editing"
  | "writing"
  | "assembling"
  | "recovering"
  | "wrapping"
  | "handling";

export type ToolKind = "shell" | "read" | "edit" | "write" | "other";

export type ToolStatus = "running" | "ok" | "error" | "unknown";

export interface ChatModel {
  id: string;
  name?: string;
}

export interface ChatAgent {
  id: string;
  name?: string;
  command?: string;
  flavor?: string;
  models?: Array<ChatModel | string>;
  [key: string]: unknown;
}

export interface ComposerAgent {
  id?: string;
  command: string;
  flavor: string;
  name: string;
  models: string[];
  [key: string]: unknown;
}

export interface ChatSendMessage {
  text: string;
  files?: Array<File | { name: string; [key: string]: unknown }>;
  command: string;
  model: string;
  permission?: string;
  effort?: string;
  [key: string]: unknown;
}

export interface ChatApprovalAnswer {
  approvalId: string;
  allow: boolean;
  message?: string;
  answers?: Record<string, string | string[]>;
}

export interface MountChatOptions {
  agents?: ChatAgent[];
  projectRoot?: string;
  palette?: PaletteId;
  clock?: () => number;
  onSend?: (message: ChatSendMessage) => void | Promise<unknown>;
  onStop?: () => void;
  onApproval?: (answer: ChatApprovalAnswer) => void | Promise<unknown>;
  onToolFix?: (callId: string) => void | Promise<unknown>;
  onLink?: (href: string, event: MouseEvent) => void;
  onError?: (message: string) => void;
  composer?: Record<string, unknown>;
}

export interface DiffLine {
  kind: "add" | "del" | "gap";
  text: string;
}

export interface ChatPartTextView {
  type: "text";
  text: string;
}

export interface ChatPartThinkingView {
  type: "thinking";
  text: string;
  label: string;
  open: boolean;
}

export interface ChatToolApprovalView {
  state: string;
  label: string;
  detailsUrl: string | null;
}

export interface ChatToolInjectionView {
  label: string;
  title: string;
  detailsUrl: string | null;
}

export interface ChatPartToolView {
  type: "tool";
  callId: string;
  name: string;
  kind: ToolKind;
  compact: boolean;
  summary: string;
  status: ToolStatus;
  statusLabel: string;
  input: string | null;
  diff: DiffLine[] | null;
  output: string;
  outputShown: boolean;
  approval: ChatToolApprovalView | null;
  injection: ChatToolInjectionView | null;
  fixable: boolean;
}

export type ChatPartView = ChatPartTextView | ChatPartThinkingView | ChatPartToolView;

export interface ChatWorkingView {
  text: string;
  queued: boolean;
}

export interface ChatUserRowView {
  key: number;
  rev: number;
  kind: "user";
  id: string | null;
  text: string;
  attachments: string[];
}

export interface ChatNoticeRowView {
  key: number;
  rev: number;
  kind: "notice";
  text: string;
  transient: boolean;
}

export interface ChatErrorRowView {
  key: number;
  rev: number;
  kind: "error";
  label: string;
  text: string;
  parts: ChatPartView[];
}

export interface ChatAssistantRowView {
  key: number;
  rev: number;
  kind: "assistant";
  id: string | null;
  label: string;
  showLabel: boolean;
  brand: AgentBrand;
  live: boolean;
  working: ChatWorkingView | null;
  parts: ChatPartView[];
}

export type ChatRowView =
  | ChatUserRowView
  | ChatNoticeRowView
  | ChatErrorRowView
  | ChatAssistantRowView;

export type ChatSnapshotRow =
  | Omit<ChatUserRowView, "key" | "rev">
  | Omit<ChatNoticeRowView, "key" | "rev">
  | Omit<ChatErrorRowView, "key" | "rev">
  | Omit<ChatAssistantRowView, "key" | "rev">;

export interface ChatQuestionOptionView {
  label: string;
  description: string;
}

export interface ChatQuestionView {
  question: string;
  header: string;
  multiSelect: boolean;
  options: ChatQuestionOptionView[];
}

export interface ChatApprovalView {
  approvalId: string;
  callId: string;
  isQuestion: boolean;
  title: string;
  summary: string;
  tool: string;
  kind: ToolKind;
  input: string | null;
  diff: DiffLine[] | null;
  questions: ChatQuestionView[];
}

export interface ChatPendingView {
  text: string;
  attachments: string[];
  brand: AgentBrand;
  working: ChatWorkingView;
}

export interface ChatViewState {
  busy: boolean;
  queued: boolean;
  liveInput: boolean;
  permissionMode: string | null;
  sessionId: string | null;
  pending: ChatPendingView | null;
  rows: ChatRowView[];
  approvals: ChatApprovalView[];
}

export interface ChatSnapshot extends Omit<ChatViewState, "rows"> {
  rows: ChatSnapshotRow[];
}

export interface SavedChatPart {
  type?: "text" | "thinking" | "tool" | (string & {});
  text?: string;
  seconds?: number | null;
  call_id?: string;
  name?: string;
  arguments?: unknown;
  output?: string | null;
  is_error?: boolean;
  approval?: string;
  approval_details_url?: string;
  injection?: {
    rules?: string[];
    excerpt?: string;
    details_url?: string;
  };
}

export interface SavedChatMessage {
  id?: string | null;
  role: "user" | "assistant" | "error" | (string & {});
  content?: string;
  text?: string;
  parts?: SavedChatPart[];
  agent?: string;
  model?: string;
  attachments?: Array<string | { name?: string }>;
}

export interface SavedChatApproval {
  approval_id: string;
  call_id?: string;
  name?: string;
  arguments?: Record<string, unknown> | null;
  description?: string;
  [key: string]: unknown;
}

export interface SavedChatState {
  messages?: SavedChatMessage[];
  approvals?: SavedChatApproval[];
  running?: boolean;
  queued?: boolean;
  agent?: string;
  model?: string;
  permissionMode?: string | null;
  sessionId?: string | null;
}

export interface CreateSessionOptions {
  agents?: ChatAgent[];
  projectRoot?: string;
  clock?: () => number;
}

export interface ChatSession {
  setAgents(list: ChatAgent[]): void;
  feed(name: string, data?: Record<string, unknown>, at?: number): boolean;
  feedEvent(event: Record<string, unknown>, at?: number): boolean;
  resume(state?: SavedChatState, at?: number): void;
  markSending(pending?: {
    text?: string;
    attachments?: Array<string | { name?: string }>;
    agent?: string;
    model?: string;
  }): void;
  cancelSending(): void;
  dismissApproval(approvalId: string): void;
  readonly busy: boolean;
  readonly agent: string;
  readonly model: string;
  view(at?: number): ChatViewState;
  snapshot(at?: number): ChatSnapshot;
  subscribe(listener: () => void): () => boolean;
}

export interface MountedComposer {
  elements: {
    slot: HTMLElement;
    stop: HTMLButtonElement;
    [key: string]: HTMLElement;
  };
  getText(): string;
  setText(text: string): void;
  send(): Promise<void>;
  update(options: Record<string, unknown>): void;
  destroy(): void;
  [key: string]: unknown;
}

export interface MountedChat {
  session: ChatSession;
  composer: MountedComposer;
  elements: {
    root: HTMLDivElement;
    messages: HTMLElement;
    composerRoot: HTMLDivElement;
    jump: HTMLButtonElement;
    dialog: HTMLDialogElement;
    banner: HTMLElement;
    title: HTMLElement;
    summary: HTMLElement;
    detail: HTMLElement;
    message: HTMLTextAreaElement;
    allow: HTMLButtonElement;
    deny: HTMLButtonElement;
    later: HTMLButtonElement;
    send: HTMLButtonElement;
    skip: HTMLButtonElement;
    [key: string]: HTMLElement;
  };
  feed(name: string, data?: Record<string, unknown>): boolean;
  feedEvent(event: Record<string, unknown>): boolean;
  readSse(response: { body: ReadableStream<Uint8Array> | null } | Response): Promise<void>;
  resume(state?: SavedChatState): void;
  setAgents(list: ChatAgent[]): void;
  setPalette(id: PaletteId): string;
  render(): ChatViewState;
  destroy(): void;
}

export function mountChat(container: HTMLElement, options?: MountChatOptions): MountedChat;
export function composerAgents(agents?: ChatAgent[]): ComposerAgent[];
export function createSession(options?: CreateSessionOptions): ChatSession;
export function eventToFrame(event: Record<string, unknown>): [string, Record<string, unknown>] | null;
export function parseSse(buffer: string, onFrame: (name: string, data: Record<string, unknown>) => void): string;
export function readSse(
  response: { body: ReadableStream<Uint8Array> | null } | Response,
  onFrame: (name: string, data: Record<string, unknown>) => void,
): Promise<void>;

export const PALETTES: readonly string[];
export function themeProperties(paletteId?: PaletteId): Record<string, string>;
export function applyTheme(element: HTMLElement, paletteId?: PaletteId): string;
export function brandColor(brand?: string): string;

export function renderMarkdown(source: string): string;
export function escapeHtml(text: string): string;
export function hideReplyTitle(text: string, isStreaming?: boolean): string;

export const QUEUED_LINE: string;
export const SENDING_LINE: string;
export const TERMINAL_WORKING_LINE: string;

export function agentBrand(agent?: string, model?: string): AgentBrand;
export function chatStatusLabel(
  conversation: {
    queued?: boolean;
    running?: boolean;
    terminal_working?: boolean;
    resume_at?: number;
    has_error?: boolean;
    unread?: boolean;
  } | null | undefined,
  now?: number,
): { kind: "queued" | "running" | "resuming" | "error" | "done"; text: string } | null;
export function formatTokenCount(tokens: number): string;
export function permissionLabel(mode?: string): string;
export function questionAnswers(
  questions: Array<{ question?: string; multiSelect?: boolean }>,
  picked?: Record<string, string | string[]>,
  own?: Record<string, { isActive?: boolean; text?: string }>,
): Record<string, string | string[]>;
export function resumeTimeText(resumeAt: number, now?: number): string;
export function shouldShowAgentLabel(previousLabel: string | null | undefined, label: string | null | undefined): boolean;
export function thinkingLabel(options?: {
  isStreaming?: boolean;
  characters?: number;
  seconds?: number | null;
}): string;
export function workingActivity(toolName?: string): WorkingActivity;
export function workingPhrase(options?: { activity?: WorkingActivity | string; elapsedSeconds?: number }): string;
export function workingWord(seed?: string): string;

export function toolLabel(name?: string): string;
export function toolKind(label?: string): ToolKind;
export function toolSummary(kind: ToolKind | string, args: unknown, projectRoot?: string): string;
export function toolInput(args: unknown): string;
export function diffLines(kind: ToolKind | string, args: unknown): DiffLine[] | null;
export function isPermissionRefusal(output?: string): boolean;

export const spec: Readonly<Record<string, unknown>>;
