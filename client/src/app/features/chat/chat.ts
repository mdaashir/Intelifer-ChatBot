import { AsyncPipe, DatePipe } from '@angular/common';
import {
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HubConnectionState } from '@microsoft/signalr';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MAX_MESSAGE_LENGTH } from '../../core/models/chat-message';
import { ChatService } from '../../core/services/chat.service';

@Component({
  selector: 'app-chat',
  imports: [
    AsyncPipe,
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './chat.html',
  styleUrl: './chat.css',
})
export class Chat implements OnInit {
  private readonly chat = inject(ChatService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  private readonly scrollAnchor = viewChild<ElementRef<HTMLDivElement>>('scrollAnchor');

  protected readonly messages$ = this.chat.messages$;
  protected readonly session = this.chat.session;
  protected readonly connectionState = this.chat.connectionState;
  protected readonly maxLength = MAX_MESSAGE_LENGTH;

  protected readonly draft = signal('');
  protected readonly sending = signal(false);

  protected readonly connected = computed(
    () => this.connectionState() === HubConnectionState.Connected,
  );

  protected readonly canSend = computed(
    () => this.connected() && !this.sending() && this.draft().trim().length > 0,
  );

  ngOnInit(): void {
    this.chat
      .loadHistory()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        error: () => this.notify('Could not load this conversation.'),
      });

    this.chat.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.scrollToLatest());

    this.chat.errors$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((error) => this.notify(error));

    void this.connect();
  }

  protected async send(): Promise<void> {
    const content = this.draft().trim();

    if (!content || !this.connected() || this.sending()) {
      return;
    }

    this.sending.set(true);

    try {
      await this.chat.sendMessage(content);
      this.draft.set('');
    } catch {
      this.notify('The message could not be sent.');
    } finally {
      this.sending.set(false);
    }
  }

  protected newConversation(): void {
    this.chat.startNewConversation();
    this.notify('Started a new conversation.');
  }

  private async connect(): Promise<void> {
    try {
      await this.chat.connect();
    } catch {
      this.notify('The chat service is unavailable. Try reloading the page.');
    }
  }

  private scrollToLatest(): void {
    // No signal is written here, so this is safe without change detection and
    // works under the default zoneless setup.
    requestAnimationFrame(() => {
      const element = this.scrollAnchor()?.nativeElement;

      if (element) {
        element.scrollIntoView({ block: 'end' });
      }
    });
  }

  private notify(message: string): void {
    this.snackBar.open(message, 'Dismiss', { duration: 5000 });
  }
}
