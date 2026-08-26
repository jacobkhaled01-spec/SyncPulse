/**
 * SyncPulse Live Presentation Hub - Controller Script
 * Manages multi-screen layouts, WebRTC live screen casting, interactive simulation, and packet inspector.
 */

// Global State
const state = {
    currentView: 'grid', // 'grid', 'split', 'server', 'client1', 'client2'
    streams: {
        server: null,
        client1: null,
        client2: null
    },
    isInspectorOpen: false,
    packetCounter: 101
};

// DOM Elements
const viewport = document.getElementById('screens-viewport');
const inspector = document.getElementById('packet-inspector');
const protocolLogRows = document.getElementById('protocol-log-rows');

/**
 * Switch layout mode smoothly
 * @param {string} mode - 'grid', 'split', 'server', 'client1', 'client2'
 */
function switchView(mode) {
    state.currentView = mode;

    // Reset Viewport Classes
    viewport.className = 'screens-viewport';
    if (mode === 'grid') {
        viewport.classList.add('mode-grid');
    } else if (mode === 'split') {
        viewport.classList.add('mode-split');
    } else {
        viewport.classList.add(`mode-focus-${mode}`);
    }

    // Update Nav Button Active States
    document.querySelectorAll('.view-btn').forEach(btn => btn.classList.remove('active'));
    
    if (mode === 'grid') document.getElementById('btn-grid-view')?.classList.add('active');
    else if (mode === 'split') document.getElementById('btn-split-view')?.classList.add('active');
    else if (mode === 'server') document.getElementById('btn-focus-server')?.classList.add('active');
    else if (mode === 'client1') document.getElementById('btn-focus-client1')?.classList.add('active');
    else if (mode === 'client2') document.getElementById('btn-focus-client2')?.classList.add('active');
}

/**
 * Focus single device shortcut
 * @param {string} deviceId 
 */
function focusDevice(deviceId) {
    switchView(deviceId);
}

/**
 * Capture Real Laptop Screen via WebRTC Screen Capture API
 * @param {string} deviceId - 'server', 'client1', 'client2'
 */
async function startScreenCapture(deviceId) {
    const videoElement = document.getElementById(`video-${deviceId}`);
    const placeholder = document.getElementById(`placeholder-${deviceId}`);
    const captureBtn = document.getElementById(`btn-capture-${deviceId}`);

    if (state.streams[deviceId]) {
        // Stop current stream if already running
        state.streams[deviceId].getTracks().forEach(track => track.stop());
        state.streams[deviceId] = null;
        videoElement.srcObject = null;
        videoElement.classList.remove('active');
        placeholder.style.display = 'flex';
        captureBtn.classList.remove('active');
        captureBtn.innerHTML = '<span class="icon">🎥</span> بث الشاشة الحية';
        return;
    }

    try {
        // Request Display Media
        const stream = await navigator.mediaDevices.getDisplayMedia({
            video: {
                cursor: "always",
                frameRate: { ideal: 30, max: 60 }
            },
            audio: false
        });

        state.streams[deviceId] = stream;
        videoElement.srcObject = stream;
        videoElement.classList.add('active');
        placeholder.style.display = 'none';
        captureBtn.classList.add('active');
        captureBtn.innerHTML = '<span class="icon">⏹️</span> إيقاف البث';

        // When user clicks "Stop Sharing" from Chrome/Edge browser bar
        stream.getVideoTracks()[0].onended = () => {
            state.streams[deviceId] = null;
            videoElement.classList.remove('active');
            placeholder.style.display = 'flex';
            captureBtn.classList.remove('active');
            captureBtn.innerHTML = '<span class="icon">🎥</span> بث الشاشة الحية';
        };

        // Add log entry
        logPacket('Local System', deviceId.toUpperCase(), 'ScreenStreamReady', '53 01 00 20 00 00 00 00', 'Live HD Stream', 'Active');

    } catch (err) {
        console.warn('Screen capture cancelled or unavailable:', err);
    }
}

/**
 * Toggle Fullscreen for seamless defense presentation
 */
function toggleFullScreen() {
    if (!document.fullscreenElement) {
        document.documentElement.requestFullscreen().catch(err => {
            alert(`خطأ في ملء الشاشة: ${err.message}`);
        });
    } else {
        if (document.exitFullscreen) {
            document.exitFullscreen();
        }
    }
}

/**
 * Toggle Protocol and Packet Flow Inspector Drawer
 */
function toggleInspector() {
    state.isInspectorOpen = !state.isInspectorOpen;
    if (state.isInspectorOpen) {
        inspector.classList.add('active');
    } else {
        inspector.classList.remove('active');
    }
}

/**
 * Log a packet into the Inspector Table and animate particle
 */
function logPacket(from, to, packetType, headerHex, payloadLen, status) {
    const timeStr = new Date().toLocaleTimeString('ar-EG', { hour12: false });
    const row = document.createElement('tr');
    row.innerHTML = `
        <td>${timeStr}</td>
        <td><strong>${from}</strong> ➔ <strong>${to}</strong></td>
        <td><span class="tag-packet">${packetType}</span></td>
        <td><code class="tag-hex">${headerHex}</code></td>
        <td>${payloadLen}</td>
        <td><span class="tag-ok">${status}</span></td>
    `;
    protocolLogRows.prepend(row);

    // Keep log table to maximum 15 rows
    while (protocolLogRows.children.length > 15) {
        protocolLogRows.removeChild(protocolLogRows.lastChild);
    }
}

/**
 * Interactive Simulation: Send Chat Message
 */
function simulateSendMsg(fromClient) {
    const isClient1 = fromClient === 'client1';
    const sender = isClient1 ? 'Client #1 (سليمان)' : 'Client #2 (يعقوب)';
    const receiver = isClient1 ? 'Client #2 (يعقوب)' : 'Client #1 (سليمان)';
    const particle = document.getElementById('particle-c1-s');
    const particle2 = document.getElementById('particle-s-c2');

    // 1. Client to Server
    particle.classList.add('animating');
    logPacket(sender, 'Server (8888)', 'DirectChatMessage', '53 01 00 0A 00 00 00 84', '132 Bytes', 'Sent (✓)');

    // 2. Server forwards to Client 2
    setTimeout(() => {
        particle.classList.remove('animating');
        particle2.classList.add('animating');
        logPacket('Server (8888)', receiver, 'DirectChatMessage', '53 01 00 0A 00 00 00 84', '132 Bytes', 'Delivered (✓✓)');
        
        // 3. Ack returned
        setTimeout(() => {
            particle2.classList.remove('animating');
            logPacket(receiver, sender, 'MessageDeliveredAck', '53 01 00 0B 00 00 00 18', '24 Bytes', 'ACK OK (✓✓ Blue)');
        }, 600);
    }, 600);
}

/**
 * Interactive Simulation: Call Signaling & UDP Media
 */
function simulateCall(fromClient) {
    const isClient1 = fromClient === 'client1';
    const caller = isClient1 ? 'Client #1' : 'Client #2';
    const callee = isClient1 ? 'Client #2' : 'Client #1';

    logPacket(caller, 'Server (TCP 8888)', 'CallOffer', '53 01 00 14 00 00 00 40', '64 Bytes', 'Ringing 🔔');
    
    setTimeout(() => {
        logPacket(callee, 'Server (TCP 8888)', 'CallAccept', '53 01 00 16 00 00 00 40', '64 Bytes', 'Accepted 🟢');
        
        setTimeout(() => {
            logPacket('UdpMediaRelay (8889)', 'Both Clients', 'MediaFrame (16kHz Audio+Video)', '53 01 00 1E 00 00 05 00', '1280 Bytes (40ms)', 'Streaming ⚡');
        }, 500);
    }, 800);
}

/**
 * Interactive Simulation: Server Broadcast
 */
function triggerServerEvent(type) {
    if (type === 'broadcast') {
        logPacket('Server (Port 8888)', 'All Active Sockets', 'SystemBroadcast', '53 01 00 03 00 00 00 64', '100 Bytes', 'Broadcasted 📢');
        alert('تم بث إشعار النظام الإداري إلى كافة مقابس العملاء المتصلة بنجاح!');
    }
}

/**
 * Keyboard Shortcuts for Defense Presentation:
 * 1: Focus Server
 * 2: Focus Client 1
 * 3: Focus Client 2
 * G: Grid View (All 3 screens)
 * S: Split View (Client 1 & 2)
 * F: Toggle Fullscreen
 * P / I: Toggle Packet Inspector
 */
document.addEventListener('keydown', (e) => {
    // Avoid triggering when user is in input
    if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

    switch (e.key.toLowerCase()) {
        case '1':
            switchView('server');
            break;
        case '2':
            switchView('client1');
            break;
        case '3':
            switchView('client2');
            break;
        case 'g':
            switchView('grid');
            break;
        case 's':
            switchView('split');
            break;
        case 'f':
            toggleFullScreen();
            break;
        case 'p':
        case 'i':
            toggleInspector();
            break;
    }
});

// Initialize default logs
window.addEventListener('DOMContentLoaded', () => {
    logPacket('ServerDiscovery', 'Broadcast (8887)', 'DiscoveryAnnounce', '53 01 00 01 00 00 00 1E', '30 Bytes', 'Broadcasting');
    logPacket('Client #1', 'Server (8888)', 'AuthLoginRequest', '53 01 00 04 00 00 00 50', '80 Bytes', 'JWT Issued');
    logPacket('Client #2', 'Server (8888)', 'AuthLoginRequest', '53 01 00 04 00 00 00 50', '80 Bytes', 'JWT Issued');
});
