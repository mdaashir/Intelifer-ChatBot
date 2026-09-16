import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
} from '@microsoft/signalr';
import { BehaviorSubject, Observable, Subject, tap } from 'rxjs';
import { ChatMessage } from '../models/chat-message';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private static readonly SessionKey = 'intellifer.sessionId';

  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  private readonly messages = new BehaviorSubject<ChatMessage[]>([]);
  private readonly errors = new Subject<string>();
  private readonly sessionId = signal(ChatService.resolveSessionId());
  private readonly state = signal<HubConnectionState>(HubConnectionState.Disconnected);

  private connection: HubConnection | null = null;

  readonly messages$ = this.messages.asObservable();
  readonly errors$ = this.errors.asObservable();
  readonly session = this.sessionId.asReadonly();
  readonly connectionState = this.state.asReadonly();

  async connect(): Promise<void> {
    if (this.connection) {
      return;
    }

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/chat', { accessTokenFactory: () => this.auth.token ?? '' })
      .withAutomaticReconnect()
      .build();

    connection.on('ReceiveMessage', (message: ChatMessage) => {
      this.messages.next([...this.messages.value, message]);
    });

    connection.on('ReceiveError', (error: string) => this.errors.next(error));

    connection.onreconnecting(() => this.state.set(HubConnectionState.Reconnecting));
    connection.onreconnected(() => this.state.set(HubConnectionState.Connected));
    connection.onclose(() => this.state.set(HubConnectionState.Disconnected));

    try {
      await connection.start();

      this.connection = connection;
      this.state.set(HubConnectionState.Connected);
    } catch (error) {
      this.state.set(HubConnectionState.Disconnected);

      throw error;
    }
  }

  async disconnect(): Promise<void> {
    const connection = this.connection;

    if (!connection) {
      return;
    }

    this.connection = null;

    await connection.stop();
    this.state.set(HubConnectionState.Disconnected);
  }

  async sendMessage(content: string): Promise<void> {
    if (!this.connection) {
      throw new Error('The chat connection is not open.');
    }

    await this.connection.invoke('SendMessage', this.sessionId(), content);
  }

  /**
   * Reloads this session's history. SignalR only carries live updates, so the
   * initial load and any refresh come from the REST endpoint.
   */
  loadHistory(): Observable<ChatMessage[]> {
    return this.http
      .get<ChatMessage[]>('/api/messages', { params: { sessionId: this.sessionId() } })
      .pipe(tap((messages) => this.messages.next(messages)));
  }

  startNewConversation(): void {
    const sessionId = crypto.randomUUID();

    localStorage.setItem(ChatService.SessionKey, sessionId);
    this.sessionId.set(sessionId);
    this.messages.next([]);
  }

  private static resolveSessionId(): string {
    const existing = localStorage.getItem(ChatService.SessionKey);

    if (existing) {
      return existing;
    }

    const created = crypto.randomUUID();

    localStorage.setItem(ChatService.SessionKey, created);

    return created;
  }
}
