window.notificationSystem = {
    connection: null,
    unreadCount: 0,

    init: async function () {
        try {
            console.log('Initializing notification system');
            await this.setupSignalRConnection();
            this.setupEventListeners();
            await this.loadInitialNotifications();

            // Remove mark all read button from dropdown if it exists
            const markAllReadBtn = document.getElementById('markAllRead');
            if (markAllReadBtn && markAllReadBtn.closest('.dropdown-menu')) {
                markAllReadBtn.parentElement.remove();
                const divider = document.querySelector('.dropdown-divider');
                if (divider) {
                    divider.remove();
                }
            }
        } catch (error) {
            console.error('Error initializing notification system:', error);
        }
    },

    setupEventListeners: function () {
        const dropdownBtn = document.getElementById('notificationDropdownBtn');
        if (dropdownBtn) {
            dropdownBtn.addEventListener('click', () => {
                this.loadInitialNotifications();
            });
        }

        document.addEventListener('click', (e) => {
            if (e.target.classList.contains('mark-read-btn')) {
                const notificationId = e.target.closest('.notification-item').dataset.id;
                this.markAsRead(notificationId);
            }
            if (e.target.classList.contains('delete-btn')) {
                const notificationId = e.target.closest('.notification-item').dataset.id;
                this.deleteNotification(notificationId);
            }
        });

        const deleteAllBtn = document.getElementById('deleteAllBtn');
        if (deleteAllBtn) {
            deleteAllBtn.addEventListener('click', () => {
                this.deleteAllNotifications();
            });
        }
    },

    async setupSignalRConnection() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/notificationHub")
            .withAutomaticReconnect()
            .build();

        this.connection.on("ReceiveNotification", this.handleNewNotification.bind(this));

        try {
            await this.connection.start();
            console.log("Connected to notification hub");
        } catch (err) {
            console.error("Error connecting to notification hub:", err);
        }
    },

    async loadInitialNotifications() {
        try {
            const response = await fetch('/Notification/GetNotifications?page=1');
            const data = await response.json();

            if (data.notifications) {
                this.updateNotificationList(data.notifications);
                this.unreadCount = data.unreadCount || 0;
                this.updateUnreadBadge();
            }
        } catch (error) {
            console.error('Error loading notifications:', error);
            this.updateNotificationList([]);
        }
    },

    updateNotificationList: function (notifications) {
        const notificationList = document.getElementById('notificationList');
        if (!notificationList) return;

        if (!notifications || notifications.length === 0) {
            notificationList.innerHTML = `
                <div class="text-center p-3 text-muted">
                    <i class="bi bi-bell"></i>
                    <p class="mb-0">No notifications</p>
                </div>`;
            return;
        }

        notificationList.innerHTML = notifications.map(notification => `
            <div class="notification-item ${notification.isRead ? '' : 'unread'}" data-id="${notification.notificationId}">
                <div class="d-flex justify-content-between">
                    <div class="notification-content">
                        <h6 class="mb-1">${this.escapeHtml(notification.title)}</h6>
                        <p class="mb-1 small">${this.escapeHtml(notification.message)}</p>
                        <small class="text-muted">${this.getTimeAgo(new Date(notification.createdAt))}</small>
                    </div>
                    <div class="ms-2">
                        ${!notification.isRead ? '<button class="btn btn-sm btn-outline-primary mark-read-btn">Mark read</button>' : ''}
                        <button class="btn btn-sm btn-outline-danger delete-btn">Delete</button>
                    </div>
                </div>
            </div>
        `).join('');
    },

    async markAsRead(notificationId) {
        try {
            const response = await fetch(`/Notification/MarkAsRead`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ notificationId })
            });
            if (response.ok) {
                console.log(`Notification ${notificationId} marked as read.`);
                this.unreadCount = Math.max(0, this.unreadCount - 1);
                this.updateUnreadBadge();
                location.reload();
            } else {
                console.error('Failed to mark notification as read.');
            }
        } catch (error) {
            console.error('Error marking notification as read:', error);
        }
    },

    async markAllAsRead() {
        try {
            const response = await fetch(`/Notification/MarkAllAsRead`, { method: 'POST' });
            if (response.ok) {
                console.log('All notifications marked as read.');
                this.unreadCount = 0;
                this.updateUnreadBadge();
                location.reload();
            } else {
                console.error('Failed to mark all notifications as read.');
            }
        } catch (error) {
            console.error('Error marking all notifications as read:', error);
        }
    },

    async deleteNotification(notificationId) {
        try {
            const response = await fetch(`/Notification/Delete`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ notificationId })
            });
            if (response.ok) {
                console.log(`Notification ${notificationId} deleted.`);
                if (!this.isRead) {
                    this.unreadCount = Math.max(0, this.unreadCount - 1);
                    this.updateUnreadBadge();
                }
                location.reload();
            } else {
                console.error('Failed to delete notification.');
            }
        } catch (error) {
            console.error('Error deleting notification:', error);
        }
    },

    async deleteAllNotifications() {
        try {
            const response = await fetch(`/Notification/DeleteAll`, { method: 'POST' });
            if (response.ok) {
                console.log('All notifications deleted.');
                location.reload();
            } else {
                console.error('Failed to delete all notifications.');
            }
        } catch (error) {
            console.error('Error deleting all notifications:', error);
        }
    },

    handleNewNotification: function (notification) {
        this.unreadCount++;
        this.updateUnreadBadge();
        this.loadInitialNotifications();
        this.showToast(notification);
    },

    updateUnreadBadge: function () {
        const badge = document.getElementById('notificationBadge');
        if (badge) {
            badge.textContent = this.unreadCount;
            badge.style.display = this.unreadCount > 0 ? 'block' : 'none';
        }
    },

    escapeHtml: function (unsafe) {
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    },

    getTimeAgo: function (date) {
        // Subtract pala dapat 8 hours for Philippines timezone
        const adjustedDate = new Date(date.getTime() + (8 * 60 * 60 * 1000));
        const seconds = Math.floor((new Date() - adjustedDate) / 1000);
        let interval = seconds / 31536000;
        if (interval > 1) return Math.floor(interval) + ' years ago';
        interval = seconds / 2592000;
        if (interval > 1) return Math.floor(interval) + ' months ago';
        interval = seconds / 86400;
        if (interval > 1) return Math.floor(interval) + ' days ago';
        interval = seconds / 3600;
        if (interval > 1) return Math.floor(interval) + ' hours ago';
        interval = seconds / 60;
        if (interval > 1) return Math.floor(interval) + ' minutes ago';
        return 'just now';
    },

    showToast: function (notification) {
        // Add toast notification if needed
        console.log('New notification received:', notification);
    }
};

// Initialize when the DOM is loaded
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        window.notificationSystem.init();
    });
} else {
    window.notificationSystem.init();
}