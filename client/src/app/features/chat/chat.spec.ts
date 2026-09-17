import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HubConnectionState } from '@microsoft/signalr';
import { BehaviorSubject, Subject, of } from 'rxjs';
import { ChatMessage } from '../../core/models/chat-message';
import { ChatService } from '../../core/services/chat.service';
import { Chat } from './chat';

/** Mirrors the real service's shape: signals for state, observables for messages. */
class FakeChatService {
  private readonly messages = new BehaviorSubject<ChatMessage[]>([]);
  private readonly state = signal(HubConnectionState.Disconnected);

  readonly messages$ = this.messages.asObservable();
  readonly errors$ = new Subject<string>();
  readonly session = signal('session-1').asReadonly();
  readonly connectionState = this.state.asReadonly();

  connectCalls = 0;
  loadHistoryCalls = 0;
  newConversationCalls = 0;
  readonly sent: string[] = [];

  loadHistory() {
    this.loadHistoryCalls++;

    return of(this.messages.value);
  }

  async connect(): Promise<void> {
    this.connectCalls++;
    this.state.set(HubConnectionState.Connected);
  }

  async sendMessage(content: string): Promise<void> {
    this.sent.push(content);
  }

  startNewConversation(): void {
    this.newConversationCalls++;
    this.messages.next([]);
  }

  emit(messages: ChatMessage[]): void {
    this.messages.next(messages);
  }

  fail(error: string): void {
    this.errors$.next(error);
  }
}

describe('Chat', () => {
  let fixture!: ComponentFixture<Chat>;
  let chat!: FakeChatService;
  let opened!: ReturnType<typeof vi.fn>;

  const message = (id: number, sender: 'User' | 'Bot', content: string): ChatMessage => ({
    id,
    sessionId: 'session-1',
    content,
    sender,
    createdAtUtc: '2026-01-01T12:00:00Z',
  });

  // jsdom has no scrollIntoView, which the component calls to follow the conversation.
  // A real browser has it, so this is an environment gap rather than a product bug.
  beforeAll(() => {
    Element.prototype.scrollIntoView = vi.fn();
  });

  beforeEach(async () => {
    chat = new FakeChatService();

    await TestBed.configureTestingModule({
      imports: [Chat],
      providers: [{ provide: ChatService, useValue: chat }],
    }).compileComponents();

    opened = vi.spyOn(TestBed.inject(MatSnackBar), 'open').mockReturnValue({} as never);

    fixture = TestBed.createComponent(Chat);

    await settle();
  });

  it('loads the history and opens the connection when it appears', () => {
    expect(chat.loadHistoryCalls).toBe(1);
    expect(chat.connectCalls).toBe(1);
  });

  it('shows a placeholder before anything has been said', () => {
    expect(element().textContent).toContain('No messages yet');
  });

  it('renders a bubble per message, with each side on its own side', async () => {
    chat.emit([message(1, 'User', 'hello'), message(2, 'Bot', 'Hello!')]);

    await settle();

    const bubbles = element().querySelectorAll('.bubble');

    expect(bubbles.length).toBe(2);
    expect(bubbles[0].classList).toContain('bubble--user');
    expect(bubbles[1].classList).toContain('bubble--bot');
    expect(element().textContent).toContain('Hello!');
  });

  it('sends the draft and clears the box', async () => {
    setDraft('hello there');

    await send();

    expect(chat.sent).toEqual(['hello there']);

    await settle();

    expect(draft()).toBe('');
  });

  it('sends nothing when the draft is only whitespace', async () => {
    setDraft('   ');

    await send();

    expect(chat.sent).toEqual([]);
  });

  it('reports a failure the service raises', async () => {
    chat.fail('The message could not be sent.');

    await settle();

    expect(opened).toHaveBeenCalledWith(
      'The message could not be sent.',
      'Dismiss',
      expect.anything(),
    );
  });

  it('starts a new conversation when asked', async () => {
    findButton('New conversation').click();

    await settle();

    expect(chat.newConversationCalls).toBe(1);
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  // See the note in login.spec: let queued microtasks finish, then render.
  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function findButton(label: string): HTMLButtonElement {
    const button = [...element().querySelectorAll('button')].find((candidate) =>
      candidate.textContent?.includes(label),
    );

    if (!button) {
      throw new Error(`No button labelled "${label}" was rendered.`);
    }

    return button;
  }

  function draft(): string {
    return (fixture.componentInstance as unknown as { draft: () => string }).draft();
  }

  function setDraft(value: string): void {
    (fixture.componentInstance as unknown as { draft: { set(v: string): void } }).draft.set(value);
  }

  async function send(): Promise<void> {
    element().querySelector('form')!.dispatchEvent(new Event('submit'));

    await settle();
  }
});
