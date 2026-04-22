<script lang="ts">
  import { onMount, tick } from 'svelte';
  import { api } from '$api/client';
  import { toast } from '$stores/toast';
  import { LoadingSpinner } from '$components';
  import type { ChatDto, ChatMessageDto } from '$api/types';

  let chats: ChatDto[] = [];
  let activeChatId: string | null = null;
  let messages: ChatMessageDto[] = [];
  let userMessage = '';
  let sending = false;
  let loading = true;
  let messagesEl: HTMLElement;

  async function loadChats() {
    try {
      chats = await api.get<ChatDto[]>('/chat');
    } catch (err: any) {
      toast.error(err.message);
    }
  }

  async function newChat() {
    try {
      const chat = await api.post<ChatDto>('/chat');
      chats = [chat, ...chats];
      await selectChat(chat.id);
    } catch (err: any) {
      toast.error(err.message);
    }
  }

  async function selectChat(chatId: string) {
    activeChatId = chatId;
    try {
      messages = await api.get<ChatMessageDto[]>(`/chat/${chatId}/messages`);
      await tick();
      scrollToBottom();
    } catch (err: any) {
      toast.error(err.message);
    }
  }

  async function send() {
    if (!userMessage.trim() || !activeChatId) return;
    const msg = userMessage.trim();
    userMessage = '';
    sending = true;

    // Optimistic add
    messages = [...messages, { id: 'temp', chatId: activeChatId, role: 'user', content: msg }];
    await tick();
    scrollToBottom();

    try {
      const reply = await api.post<ChatMessageDto>(`/chat/${activeChatId}/messages`, { message: msg });
      messages = [...messages.filter(m => m.id !== 'temp'),
        { id: 'u-' + Date.now(), chatId: activeChatId!, role: 'user', content: msg },
        reply];
      await tick();
      scrollToBottom();
    } catch (err: any) {
      toast.error(err.message);
      messages = messages.filter(m => m.id !== 'temp');
    } finally {
      sending = false;
    }
  }

  function scrollToBottom() {
    if (messagesEl) messagesEl.scrollTop = messagesEl.scrollHeight;
  }

  onMount(async () => {
    await loadChats();
    if (chats.length > 0) {
      await selectChat(chats[0].id);
    }
    loading = false;
  });
</script>

<div class="flex flex-col h-[calc(100vh-120px)] lg:h-[calc(100vh-48px)]">
  <div class="page-header mb-0 pb-3 border-b border-gray-200">
    <h1 class="page-title flex items-center gap-2">👷‍♂️ Ask Bob</h1>
    <button class="btn-secondary text-sm" on:click={newChat}>+ New Chat</button>
  </div>

  <div class="flex flex-1 min-h-0">
    <!-- Chat list (desktop) -->
    <div class="hidden md:flex flex-col w-56 border-r border-gray-200 overflow-y-auto py-2">
      {#each chats as chat}
        <button
          class="text-left px-3 py-2 text-sm truncate rounded-lg mx-1 transition-colors
            {chat.id === activeChatId ? 'bg-brand-50 text-brand-700 font-medium' : 'text-gray-600 hover:bg-gray-50'}"
          on:click={() => selectChat(chat.id)}>
          {chat.title}
        </button>
      {/each}
    </div>

    <!-- Messages -->
    <div class="flex-1 flex flex-col min-w-0">
      <div bind:this={messagesEl} class="flex-1 overflow-y-auto p-4 space-y-4">
        {#if !activeChatId}
          <div class="flex items-center justify-center h-full text-gray-400 text-sm">
            Start a chat with Bob — your AI contractor assistant
          </div>
        {:else if messages.length === 0}
          <div class="flex flex-col items-center justify-center h-full text-center">
            <span class="text-5xl mb-3">👷‍♂️</span>
            <p class="text-gray-500 text-sm">Hey! I'm Bob. Ask me anything about estimating, scheduling, or running your business.</p>
          </div>
        {:else}
          {#each messages as msg}
            <div class="flex {msg.role === 'user' ? 'justify-end' : 'justify-start'}">
              <div class="max-w-[80%] px-4 py-2.5 rounded-2xl text-sm
                {msg.role === 'user'
                  ? 'bg-brand-600 text-white rounded-br-sm'
                  : 'bg-gray-100 text-gray-800 rounded-bl-sm'}">
                {msg.content}
              </div>
            </div>
          {/each}
          {#if sending}
            <div class="flex justify-start">
              <div class="bg-gray-100 text-gray-500 px-4 py-2.5 rounded-2xl rounded-bl-sm text-sm">
                Bob is thinking...
              </div>
            </div>
          {/if}
        {/if}
      </div>

      <!-- Input -->
      <form on:submit|preventDefault={send}
        class="flex items-center gap-2 p-3 border-t border-gray-200 bg-white">
        <input
          class="input flex-1"
          placeholder="Ask Bob anything..."
          bind:value={userMessage}
          disabled={!activeChatId || sending}
        />
        <button type="submit" class="btn-primary px-4" disabled={!userMessage.trim() || sending}>
          Send
        </button>
      </form>
    </div>
  </div>
</div>
