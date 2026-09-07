(function () {
    const config = window.chatConfig;
    if (!config || !config.conversationId) {
        return;
    }

    const messagesEl = document.getElementById('chat-messages');
    const formEl = document.getElementById('chat-form');
    const inputEl = document.getElementById('chat-input');
    const typingEl = document.getElementById('chat-typing');
    const sendBtn = document.getElementById('chat-send-btn');

    if (!messagesEl || !formEl || !inputEl) {
        return;
    }

    let connection = null;
    let typingTimeout = null;

    async function markAsRead() {
        if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
            return;
        }

        try {
            await connection.invoke('MarkAsRead', config.conversationId);
        } catch (err) {
            console.error('MarkAsRead failed:', err);
        }
    }

    function scrollToBottom() {
        messagesEl.scrollTop = messagesEl.scrollHeight;
    }

    function formatTime(isoString) {
        const date = new Date(isoString);
        return date.toLocaleString(undefined, {
            month: 'short',
            day: 'numeric',
            hour: 'numeric',
            minute: '2-digit'
        });
    }

    function appendMessage(message) {
        if (message.messageId && messagesEl.querySelector('[data-message-id="' + message.messageId + '"]')) {
            return;
        }
        const isMine = message.isMine === true || message.senderUserId === config.currentUserId;
        const wrapper = document.createElement('div');
        wrapper.className = 'chat-message' + (isMine ? ' chat-message--mine' : ' chat-message--theirs');
        wrapper.dataset.messageId = message.messageId;

        const bubble = document.createElement('div');
        bubble.className = 'chat-message__bubble';

        const meta = document.createElement('div');
        meta.className = 'chat-message__meta';
        meta.textContent = (message.senderName || 'User') + ' · ' + formatTime(message.sentAt);

        const body = document.createElement('div');
        body.className = 'chat-message__body';
        body.textContent = message.body;

        bubble.appendChild(meta);
        bubble.appendChild(body);
        wrapper.appendChild(bubble);
        messagesEl.appendChild(wrapper);
        scrollToBottom();
    }

    async function startConnection() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/chat')
            .withAutomaticReconnect()
            .build();

        connection.on('ReceiveMessage', function (message) {
            appendMessage({
                messageId: message.messageId,
                senderName: message.senderName,
                body: message.body,
                sentAt: message.sentAt,
                isMine: message.isMine
            });
            markAsRead();
        });

        connection.on('UserTyping', function () {
            if (!typingEl) {
                return;
            }
            typingEl.textContent = 'Someone is typing...';
            typingEl.style.display = 'block';
            clearTimeout(typingTimeout);
            typingTimeout = setTimeout(function () {
                typingEl.style.display = 'none';
            }, 2000);
        });

        try {
            await connection.start();
            await connection.invoke('JoinConversation', config.conversationId);
            await markAsRead();
        } catch (err) {
            console.error('Chat connection failed:', err);
        }
    }

    window.addEventListener('pagehide', function () {
        markAsRead();
    });

    formEl.addEventListener('submit', async function (event) {
        event.preventDefault();
        const body = inputEl.value.trim();
        if (!body) {
            return;
        }

        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            try {
                if (sendBtn) {
                    sendBtn.disabled = true;
                }
                await connection.invoke('SendMessage', config.conversationId, body);
                inputEl.value = '';
            } catch (err) {
                console.error('Send failed:', err);
                alert('Could not send message. Please try again.');
            } finally {
                if (sendBtn) {
                    sendBtn.disabled = false;
                }
            }
        } else {
            formEl.submit();
        }
    });

    inputEl.addEventListener('input', function () {
        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke('UserTyping', config.conversationId).catch(function () { });
        }
    });

    scrollToBottom();
    startConnection();
})();
