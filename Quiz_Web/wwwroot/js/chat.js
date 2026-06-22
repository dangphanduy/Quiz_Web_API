"use strict";

let connection = null;
let currentConversationId = null;
let currentUserId = null;
let conversations = [];

document.addEventListener("DOMContentLoaded", function () {
    currentUserId = parseInt(document.getElementById("currentUserId").value);
    const urlParams = new URLSearchParams(window.location.search);
    const initialConvId = urlParams.get('conversationId');

    // 1. Khởi tạo SignalR
    initSignalR();

    // 2. Load danh sách cuộc hội thoại
    loadConversations(initialConvId);

    // 3. Đăng ký sự kiện nút gửi tin nhắn
    document.getElementById("btnSend").addEventListener("click", sendTextMessage);
    document.getElementById("txtMessage").addEventListener("keypress", function (e) {
        if (e.key === "Enter") {
            sendTextMessage();
        }
    });

    // 4. Đăng ký sự kiện chọn file đính kèm
    document.getElementById("btnAttach").addEventListener("click", () => {
        document.getElementById("fileInput").click();
    });
    document.getElementById("fileInput").addEventListener("change", handleFileUpload);

    // 5. Ô tìm kiếm hội thoại
    document.getElementById("searchChat").addEventListener("input", function (e) {
        const keyword = e.target.value.toLowerCase();
        filterConversations(keyword);
    });
});

function initSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveMessage", function (conversationId, senderId, senderName, messageId, content, messageType, fileName, createdAt) {
        // Cập nhật tin nhắn mới nhất trong danh sách hội thoại
        const conv = conversations.find(c => c.conversationId === conversationId);
        if (conv) {
            conv.lastMessage = {
                senderId: senderId,
                content: content,
                messageType: messageType,
                createdAt: createdAt
            };
            if (conversationId !== currentConversationId) {
                conv.unreadCount++;
            }
            renderConversationsList();
        }

        // Nếu đang mở đúng cuộc hội thoại này, hiển thị tin nhắn ngay lập tức
        if (conversationId === currentConversationId) {
            appendMessage({
                messageId: messageId,
                senderId: senderId,
                content: content,
                messageType: messageType,
                fileName: fileName,
                createdAt: createdAt
            });
            scrollToBottom();
            // Đánh dấu đã đọc
            markAsRead(conversationId);
        }
    });

    connection.start().then(function () {
        console.log("SignalR Connected.");
        if (currentConversationId) {
            connection.invoke("JoinConversation", currentConversationId);
        }
    }).catch(function (err) {
        return console.error(err.toString());
    });
}

async function loadConversations(selectConvId) {
    try {
        const response = await fetch('/api/chat/conversations');
        const data = await response.json();
        if (data.success) {
            conversations = data.conversations;
            renderConversationsList();

            if (selectConvId) {
                selectConversation(parseInt(selectConvId));
            } else if (conversations.length > 0) {
                selectConversation(conversations[0].conversationId);
            } else {
                showEmptyChatState();
            }
        }
    } catch (err) {
        console.error("Lỗi khi tải danh sách hội thoại:", err);
    }
}

function renderConversationsList() {
    const listEl = document.getElementById("conversationsList");
    listEl.innerHTML = "";

    // Sắp xếp theo tin nhắn mới nhất
    conversations.sort((a, b) => {
        const timeA = a.lastMessage ? new Date(a.lastMessage.createdAt) : new Date(a.createdAt);
        const timeB = b.lastMessage ? new Date(b.lastMessage.createdAt) : new Date(b.createdAt);
        return timeB - timeA;
    });

    conversations.forEach(conv => {
        const isStudent = currentUserId === conv.studentId;
        const displayName = isStudent ? conv.instructorName : conv.studentName;
        const displayAvatar = isStudent ? conv.instructorAvatar : conv.studentAvatar;
        
        let lastMsgText = "Chưa có tin nhắn";
        if (conv.lastMessage) {
            if (conv.lastMessage.messageType === "Text") {
                lastMsgText = conv.lastMessage.content;
            } else if (conv.lastMessage.messageType === "Image") {
                lastMsgText = "[Hình ảnh]";
            } else {
                lastMsgText = "[Tệp đính kèm]";
            }
        }

        const activeClass = conv.conversationId === currentConversationId ? "active" : "";
        const unreadBadge = conv.unreadCount > 0 ? `<span class="badge bg-danger rounded-pill float-end">${conv.unreadCount}</span>` : "";

        const itemHtml = `
            <div class="list-group-item list-group-item-action border-0 d-flex align-items-center p-3 chat-user-item ${activeClass}" 
                 onclick="selectConversation(${conv.conversationId})" id="conv-${conv.conversationId}">
                <div class="position-relative me-3">
                    <img src="${displayAvatar || '/wwwroot/img/default-avatar.png' || '/img/default-avatar.png' || 'https://ui-avatars.com/api/?name=' + encodeURIComponent(displayName)}" 
                         class="rounded-circle" width="48" height="48" alt="${displayName}">
                </div>
                <div class="flex-grow-1 min-width-0">
                    <div class="d-flex justify-content-between align-items-baseline mb-1">
                        <h6 class="mb-0 text-truncate text-dark fw-semibold" style="max-width: 140px;">${displayName}</h6>
                        <small class="text-muted" style="font-size: 11px;">${formatTime(conv.lastMessage ? conv.lastMessage.createdAt : conv.createdAt)}</small>
                    </div>
                    <div class="d-flex justify-content-between align-items-center">
                        <p class="mb-0 text-truncate text-muted small" style="max-width: 160px;">${lastMsgText}</p>
                        ${unreadBadge}
                    </div>
                    <small class="text-primary text-truncate d-block" style="font-size: 10px; max-width: 180px;">${conv.courseTitle}</small>
                </div>
            </div>
        `;
        listEl.insertAdjacentHTML("beforeend", itemHtml);
    });
}

function filterConversations(keyword) {
    const items = document.querySelectorAll(".chat-user-item");
    items.forEach(item => {
        const name = item.querySelector("h6").textContent.toLowerCase();
        const course = item.querySelector("small.text-primary").textContent.toLowerCase();
        if (name.includes(keyword) || course.includes(keyword)) {
            item.style.setProperty("display", "flex", "important");
        } else {
            item.style.setProperty("display", "none", "important");
        }
    });
}

async function selectConversation(conversationId) {
    if (currentConversationId && connection && connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke("LeaveConversation", currentConversationId);
    }

    currentConversationId = conversationId;
    
    // Highlight active item
    document.querySelectorAll(".chat-user-item").forEach(item => item.classList.remove("active"));
    const activeItem = document.getElementById(`conv-${conversationId}`);
    if (activeItem) {
        activeItem.classList.add("active");
    }

    // Reset unread count local
    const conv = conversations.find(c => c.conversationId === conversationId);
    if (conv) {
        conv.unreadCount = 0;
        renderConversationsList();
    }

    // Connect to room SignalR
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke("JoinConversation", conversationId);
    }

    // Load header thông tin phòng chat
    loadChatHeader(conv);

    // Tải lịch sử tin nhắn
    await loadChatHistory(conversationId);
    
    // Đánh dấu đã đọc trên server
    markAsRead(conversationId);
}

function loadChatHeader(conv) {
    if (!conv) return;
    const isStudent = currentUserId === conv.studentId;
    const displayName = isStudent ? conv.instructorName : conv.studentName;
    const displayAvatar = isStudent ? conv.instructorAvatar : conv.studentAvatar;

    document.getElementById("chatPartnerName").textContent = displayName;
    document.getElementById("chatPartnerCourse").textContent = conv.courseTitle;
    document.getElementById("chatPartnerAvatar").src = displayAvatar || 'https://ui-avatars.com/api/?name=' + encodeURIComponent(displayName);
    document.getElementById("chatWelcomeArea").classList.add("d-none");
    document.getElementById("chatContentArea").classList.remove("d-none");
}

async function loadChatHistory(conversationId) {
    const chatContainer = document.getElementById("chatMessagesContainer");
    chatContainer.innerHTML = `<div class="d-flex justify-content-center my-4"><div class="spinner-border text-primary" role="status"></div></div>`;

    try {
        const response = await fetch(`/api/chat/history/${conversationId}`);
        const data = await response.json();
        if (data.success) {
            chatContainer.innerHTML = "";
            data.messages.forEach(msg => {
                appendMessage(msg);
            });
            scrollToBottom();
        }
    } catch (err) {
        console.error("Lỗi khi tải lịch sử tin nhắn:", err);
    }
}

function appendMessage(msg) {
    const chatContainer = document.getElementById("chatMessagesContainer");
    const isOutgoing = msg.senderId === currentUserId;
    const bubbleClass = isOutgoing ? "bg-primary text-white" : "bg-light text-dark";
    const alignClass = isOutgoing ? "justify-content-end" : "justify-content-start";
    
    let contentHtml = "";
    if (msg.messageType === "Text") {
        contentHtml = `<p class="mb-0">${escapeHtml(msg.content)}</p>`;
    } else if (msg.messageType === "Image") {
        contentHtml = `
            <a href="${msg.content}" target="_blank">
                <img src="${msg.content}" class="img-fluid rounded border max-width-250" style="max-height: 200px; object-fit: cover;" alt="Hình ảnh">
            </a>
        `;
    } else {
        contentHtml = `
            <div class="d-flex align-items-center">
                <i class="bi bi-file-earmark-arrow-down-fill me-2 fs-3"></i>
                <div class="min-width-0">
                    <a href="${msg.content}" target="_blank" class="text-truncate d-block fw-semibold ${isOutgoing ? 'text-white' : 'text-primary'}" style="max-width: 180px;">${escapeHtml(msg.fileName || 'Tệp đính kèm')}</a>
                    <small class="${isOutgoing ? 'text-white-50' : 'text-muted'}">Nhấp để tải về</small>
                </div>
            </div>
        `;
    }

    const messageHtml = `
        <div class="d-flex ${alignClass} mb-3 msg-bubble-wrapper">
            <div class="max-width-70">
                <div class="card p-3 border-0 rounded-4 shadow-sm ${bubbleClass}">
                    ${contentHtml}
                </div>
                <div class="text-muted small mt-1 px-2 text-end" style="font-size: 10px;">
                    ${formatMessageTime(msg.createdAt)}
                </div>
            </div>
        </div>
    `;
    chatContainer.insertAdjacentHTML("beforeend", messageHtml);
}

async function sendTextMessage() {
    const input = document.getElementById("txtMessage");
    const content = input.value.trim();
    if (!content || !currentConversationId) return;

    input.value = "";
    try {
        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            await connection.invoke("SendMessage", currentConversationId, content, "Text", null);
        }
    } catch (err) {
        console.error("Gửi tin nhắn thất bại:", err);
    }
}

async function handleFileUpload(e) {
    const file = e.target.files[0];
    if (!file || !currentConversationId) return;

    // Reset file input
    e.target.value = "";

    const formData = new FormData();
    formData.append("file", file);

    // Show uploading bubble placeholder
    const chatContainer = document.getElementById("chatMessagesContainer");
    const uploadPlaceholderId = "uploading-" + Date.now();
    const placeholderHtml = `
        <div class="d-flex justify-content-end mb-3" id="${uploadPlaceholderId}">
            <div class="card p-3 border-0 rounded-4 shadow-sm bg-primary text-white">
                <div class="d-flex align-items-center">
                    <div class="spinner-border spinner-border-sm me-2 text-light" role="status"></div>
                    <span class="small">Đang tải lên: ${escapeHtml(file.name)}</span>
                </div>
            </div>
        </div>
    `;
    chatContainer.insertAdjacentHTML("beforeend", placeholderHtml);
    scrollToBottom();

    try {
        const response = await fetch('/api/chat/upload', {
            method: 'POST',
            body: formData
        });
        const data = await response.json();
        
        // Remove placeholder
        document.getElementById(uploadPlaceholderId).remove();

        if (data.success) {
            const isImage = file.type.startsWith('image/');
            const messageType = isImage ? "Image" : "File";
            
            if (connection && connection.state === signalR.HubConnectionState.Connected) {
                await connection.invoke("SendMessage", currentConversationId, data.fileUrl, messageType, data.fileName);
            }
        } else {
            alert("Tải file thất bại: " + data.message);
        }
    } catch (err) {
        document.getElementById(uploadPlaceholderId).remove();
        console.error("Lỗi upload file:", err);
        alert("Có lỗi xảy ra khi tải file đính kèm.");
    }
}

async function markAsRead(conversationId) {
    try {
        await fetch(`/api/chat/read/${conversationId}`, { method: 'POST' });
    } catch (err) {
        console.error("Lỗi đánh dấu đã đọc:", err);
    }
}

function showEmptyChatState() {
    document.getElementById("chatWelcomeArea").classList.remove("d-none");
    document.getElementById("chatContentArea").classList.add("d-none");
}

function scrollToBottom() {
    const chatContainer = document.getElementById("chatMessagesContainer");
    chatContainer.scrollTop = chatContainer.scrollHeight;
}

// Helpers
function formatTime(dateStr) {
    if (!dateStr) return "";
    const date = new Date(dateStr);
    const now = new Date();
    
    // Nếu trong ngày hôm nay, hiển thị Giờ:Phút
    if (date.toDateString() === now.toDateString()) {
        return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false });
    }
    
    // Nếu trong năm nay, hiển thị ngày/tháng
    if (date.getFullYear() === now.getFullYear()) {
        return date.toLocaleDateString([], { day: 'numeric', month: 'numeric' });
    }
    
    return date.toLocaleDateString([], { day: 'numeric', month: 'numeric', year: 'numeric' });
}

function formatMessageTime(dateStr) {
    if (!dateStr) return "";
    const date = new Date(dateStr);
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false });
}

function escapeHtml(unsafe) {
    return unsafe
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}
