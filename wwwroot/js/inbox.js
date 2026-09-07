(function () {
    const config = window.inboxConfig;
    if (!config || !config.currentUserId) {
        return;
    }

    const tbody = document.querySelector('.chat-inbox-table tbody');
    if (!tbody) {
        return;
    }

    let connection = null;

    function formatTime(isoString) {
        const date = new Date(isoString);
        return date.toLocaleString(undefined, {
            month: 'short',
            day: 'numeric',
            hour: 'numeric',
            minute: '2-digit'
        });
    }

    function truncatePreview(text, maxLen) {
        if (!text) {
            return '';
        }

        return text.length > maxLen ? text.slice(0, maxLen) + '...' : text;
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function getTotalUnread() {
        let total = 0;
        tbody.querySelectorAll('.chat-unread-badge').forEach(function (badge) {
            total += parseInt(badge.textContent, 10) || 0;
        });
        return total;
    }

    function updateSidebarBadge(totalUnread) {
        document.querySelectorAll('[data-sidebar-unread-badge]').forEach(function (el) {
            if (totalUnread > 0) {
                el.textContent = totalUnread;
                el.hidden = false;
            } else {
                el.hidden = true;
            }
        });
    }

    function dispatchUnreadChanged() {
        const totalUnread = getTotalUnread();
        updateSidebarBadge(totalUnread);
        document.dispatchEvent(new CustomEvent('chat-unread-changed', {
            detail: { totalUnread: totalUnread }
        }));
    }

    function updateUnreadBadge(row, conversationId, unreadCount) {
        const actionsCell = row.querySelector('td:last-child');
        if (!actionsCell) {
            return;
        }

        let badge = actionsCell.querySelector('.chat-unread-badge[data-conversation-id="' + conversationId + '"]');
        const openBtn = actionsCell.querySelector('.btn');

        if (unreadCount > 0) {
            if (!badge) {
                badge = document.createElement('span');
                badge.className = 'chat-unread-badge';
                badge.dataset.conversationId = conversationId;
                actionsCell.insertBefore(badge, openBtn);
            }
            badge.textContent = unreadCount;
        } else if (badge) {
            badge.remove();
        }
    }

    function updateRow(item) {
        const conversationId = item.conversationId;
        const row = tbody.querySelector('tr[data-conversation-id="' + conversationId + '"]');

        if (!row) {
            window.location.reload();
            return;
        }

        const previewCell = row.querySelector('.chat-inbox-preview[data-conversation-id="' + conversationId + '"]');
        if (previewCell) {
            if (item.lastMessagePreview) {
                previewCell.innerHTML =
                    '<span>' + escapeHtml(truncatePreview(item.lastMessagePreview, 60)) + '</span>' +
                    (item.lastMessageAt
                        ? '<div class="small text-muted">' + escapeHtml(formatTime(item.lastMessageAt)) + '</div>'
                        : '');
            } else {
                previewCell.innerHTML = '<span class="text-muted">No messages yet</span>';
            }
        }

        updateUnreadBadge(row, conversationId, item.unreadCount || 0);
        tbody.insertBefore(row, tbody.firstChild);
        dispatchUnreadChanged();
    }

    async function startConnection() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/chat')
            .withAutomaticReconnect()
            .build();

        connection.on('InboxUpdated', updateRow);

        try {
            await connection.start();
            await connection.invoke('JoinInbox');
        } catch (err) {
            console.error('Inbox connection failed:', err);
        }
    }

    startConnection();
})();
