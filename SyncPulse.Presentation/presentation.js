/**
 * ==============================================================================
 * SyncPulse Zoomable Interactive Network Canvas - Controller Engine
 * Infinite Pan & Zoom, Dynamic Node Focusing, Inspector Drawer & Multi-Theme
 * ==============================================================================
 */

// Node Technical Database & Inspector Metadata
const nodeDatabase = {
    server: {
        title: "الخادم المركزي (SyncPulse.Server)",
        speaker: "المتحدث 3: مهندس الخادم وقواعد البيانات",
        desc: "النواة المركزية لإدارة المقابس وتوجيه الرسائل وتدفقات الوسائط المباشرة وتخزين السجلات.",
        points: [
            "خادم TCP غير حاجب مبني على نموذج IOCP Async على المنفذ 8888.",
            "مكرر تدفقات الوسائط اللحظي UDP Media Relay على المنفذ 8889.",
            "عزل الجلسات والأعطال بنمط Bulkhead لمنع انهيار الخادم عند خطأ أي عميل.",
            "قاعدة بيانات SQLite 3NF بوضع WAL Mode لمنع أي تعارض أثناء الكتابة والقراءة."
        ],
        code: `// Server Listening Loop
TcpListener listener = new TcpListener(IPAddress.Any, 8888);
listener.Start();
while (_isRunning) {
    TcpClient client = await listener.AcceptTcpClientAsync();
    _ = Task.Run(() => HandleClientSessionAsync(client));
}`,
        pos: { x: 1600, y: 1100 }
    },
    client_a: {
        title: "العميل الأول (Client Device 1 - Laptop 1)",
        speaker: "المتحدث 1: مهندس المعمارية والشبكات",
        desc: "تطبيق سطح المكتب للمستخدم الأول مبني بـ WPF بنمط MVVM ويتصل بالخادم عبر مقابس غير حاجبة.",
        points: [
            "خدمة الاتصال بالمقابس ClientNetworkService وإعادة الاتصال التلقائي.",
            "محرك الصوت 16kHz HD Voice ونظام المخازن الدائرية الثابتة 8 Pre-Pinned Buffers.",
            "محرك الفيديو المباشر AForge DirectShow والتحكم بالمعاينة PiP."
        ],
        code: `await _networkService.ConnectAsync(serverIp, 8888);
await _networkService.SendPacketAsync(loginPacket);`,
        pos: { x: 900, y: 800 }
    },
    client_b: {
        title: "العميل الثاني (Client Device 2 - Laptop 2)",
        speaker: "المتحدث 4: مهندس الميديا والوسائط",
        desc: "تطبيق سطح المكتب للمستخدم الثاني مع استلام فوري للرسائل والمكالمات وإطفاء ضوء الكاميرا الحقيقي.",
        points: [
            "مزامنة تلغرامية للرسائل مع إرسال إشعارات التسليم Delivered ✓✓ والقراءة Read ✓✓.",
            "إغلاق فيزيائي حقيقي لضوء الكاميرا LED عبر SignalToStop().",
            "بث هجين متوازي UDP+TCP مع إلغاء تكرار الإطارات لحظياً."
        ],
        code: `_videoSource.SignalToStop();
_videoSource.WaitForStop(); // LED Turns OFF`,
        pos: { x: 2300, y: 800 }
    },
    framing: {
        title: "بروتوكول التأطير الثنائي (12-Byte FrameHeader)",
        speaker: "المتحدث 1: مهندس المعمارية والشبكات",
        desc: "البروتوكول المخصص لحل مشكلة تجزئة ودمج حزم TCP وضمان وصول الرسائل كاملة وسليمة.",
        points: [
            "Magic Byte (0x53): التحقق الفوري من هوية البروتوكول.",
            "Version (0x01): رقم إصدار بروتوكول التأطير.",
            "PacketType (Int16): نوع الحزمة (محادثة، مصادقة، إشارة مكالمة).",
            "PayloadLength (Int32): طول حمولة الحزمة بدقة.",
            "SequenceNumber (Int32): الرقم التسلسلي لكشف الحزم المفقودة.",
            "ترتيب Big-Endian الشبكي الموحد لجميع المعالجات."
        ],
        code: `byte[] buf = new byte[12];
buf[0] = 0x53; buf[1] = 0x01;
BinaryPrimitives.WriteInt16BigEndian(buf.AsSpan(2, 2), (short)PacketType);
BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(4, 4), PayloadLength);
BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(8, 4), SequenceNumber);`,
        pos: { x: 1600, y: 520 }
    },
    discovery: {
        title: "الاكتشاف التلقائي لشبكات الواي فاي (UDP Port 8887)",
        speaker: "المتحدث 1: مهندس المعمارية والشبكات",
        desc: "خدمة الاكتشاف الفوري Zero-Configuration لربط الأجهزة بالخادم دون الحاجة لكتابة عنوان IP يدوياً.",
        points: [
            "إعلان الخادم الدوري: بث برودكاست كل 3 ثوانٍ على 255.255.255.255:8887.",
            "استماع العميل: التقاط الإعلان واستخراج عنوان IP الخادم فورياً.",
            "بدء اتصال TCP تلقائي على المنفذ 8888."
        ],
        code: `UdpClient udp = new UdpClient();
udp.EnableBroadcast = true;
byte[] beacon = Encoding.UTF8.GetBytes("SYNCPULSE_SERVER_ANNOUNCE:8888");
await udp.SendAsync(beacon, beacon.Length, new IPEndPoint(IPAddress.Broadcast, 8887));`,
        pos: { x: 1600, y: 820 }
    },
    security: {
        title: "الأمن السيبراني والمصادقة المشفرة (PBKDF2 & JWT)",
        speaker: "المتحدث 2: مهندس الأمن والمصادقة",
        desc: "تطبيق أعلى المعايير القياسية العالمية (NIST SP 800-63B & RFC 7519 & ISO 27001).",
        points: [
            "تجزئة كلمات المرور بـ 128-bit Salt فريد و 100,000 تكرار PBKDF2/SHA-256.",
            "مقارنة FixedTimeEquals لمنع هجمات التوقيت Timing Attacks.",
            "توكنات JWT موقعة بـ HMAC-SHA256 لإدارة الجلسات دون استعلام DB في كل حزمة.",
            "حماية من حقن SQL عبر Parameterized Queries وسقف 10MB للحزم لمنع DoS."
        ],
        code: `byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100000, HashAlgorithmName.SHA256, 32);
bool isValid = CryptographicOperations.FixedTimeEquals(hash, storedHash);`,
        pos: { x: 1200, y: 520 }
    },
    clean_arch: {
        title: "المعمارية النظيفة (Clean 3-Tier Architecture)",
        speaker: "المتحدث 1: مهندس المعمارية والشبكات",
        desc: "فصل فيزيائي تام للمسؤوليات في حل .NET متعدد المشاريع.",
        points: [
            "SyncPulse.Core: النواة المشتركة، بروتوكول التأطير، ومحرك التشفير.",
            "SyncPulse.Server: خادم TCP/UDP، إدارة الجلسات، وقاعدة بيانات SQLite.",
            "SyncPulse.Client: تطبيق سطح المكتب، محرك الصوت، وكاميرا AForge."
        ],
        code: `// Solution Structure
SyncPulse.Core/       -> Shared DTOs, Framing, Crypto
SyncPulse.Server/     -> IOCP Sockets, WAL Database, UDP Relay
SyncPulse.Client/     -> WPF Views, AudioEngine, VideoEngine`,
        pos: { x: 2000, y: 520 }
    },
    sqlite_wal: {
        title: "قواعد البيانات والتزامن (SQLite 3NF WAL Mode)",
        speaker: "المتحدث 3: مهندس الخادم وقواعد البيانات",
        desc: "بنية قاعدة بيانات علائقية متطابقة مع الشكل المعياري الثالث 3NF ووضع Write-Ahead Logging.",
        points: [
            "وضع WAL Mode: قراءات متزامنة لا نهائية أثناء عمليات الكتابة دون أي تعليق.",
            "7 جداول مترابطة بمفاتيح أجنبية لضمان النزاهة المرجعية ACID.",
            "طابور الرسائل المعلقة Offline Queue ومزامنة مؤشرات التسليم."
        ],
        code: `PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA foreign_keys=ON;`,
        pos: { x: 1200, y: 1420 }
    },
    audio_ring: {
        title: "محرك الصوت والمخازن الدائرية (16kHz HD Pre-Pinned)",
        speaker: "المتحدث 4: مهندس الميديا والوسائط",
        desc: "معالجة صوتية منخفضة المستوى لحل خطأ الذاكرة الشهير 0xc0000005 Status Access Violation.",
        points: [
            "تثبيت 8 مخازن بايتات مسبقاً بـ GCHandleType.Pinned عند بدء المكالمة.",
            "نسخ البيانات عبر Buffer.BlockCopy بنمط Zero-Allocation دورياً.",
            "معدل أخذ عينات 16kHz بتشفير PCM 16-bit عالي النقاء."
        ],
        code: `for (int i = 0; i < 8; i++) {
    _buffers[i] = new byte[1280];
    _handles[i] = GCHandle.Alloc(_buffers[i], GCHandleType.Pinned);
}`,
        pos: { x: 880, y: 1420 }
    },
    video_led: {
        title: "محرك الفيديو وإطفاء الكاميرا (AForge DirectShow & LED)",
        speaker: "المتحدث 4: مهندس الميديا والوسائط",
        desc: "التعامل المباشر مع مقبض الكاميرا في نظام التشغيل لحماية الخصوصية الفيزيائية.",
        points: [
            "إغلاق مقبض DirectShow عبر SignalToStop() فينطفئ ضوء LED الحقيقي للكاميرا.",
            "بث هجين متوازي عبر UDP (للسرعة) و TCP (للموثوقية).",
            "إلغاء تكرار الإطارات بمفتاح ثنائي 64-bit Timestamp."
        ],
        code: `if (_videoSource != null && _videoSource.IsRunning) {
    _videoSource.SignalToStop();
    _videoSource.WaitForStop(); // Hardware LED OFF
}`,
        pos: { x: 2280, y: 1420 }
    },
    speakers_matrix: {
        title: "مصفوفة المتحدثين الأربعة (4 Speakers Defense Team)",
        speaker: "فريق العمل الهندسي (4 متحدثين)",
        desc: "توزيع هندسي متكامل يغطي كافة محاور مقرر برمجة خادم وعميل بدقة واحترافية.",
        points: [
            "المتحدث 1: مهندس المعمارية والشبكات (المقابس، التأطير، واكتشاف الواي فاي).",
            "المتحدث 2: مهندس الأمن والمصادقة (تجزئة PBKDF2، توكنات JWT، ومنع حقن SQL).",
            "المتحدث 3: مهندس الخادم وقواعد البيانات (خادم IOCP، نمط Bulkhead، و SQLite WAL).",
            "المتحدث 4: مهندس الميديا والذاكرة (مكرر UDP Relay، صوت 16kHz، وكاميرا AForge)."
        ],
        code: `// 4-Speaker Responsibility Allocation
Speaker 1: Network Sockets & Framing (TCP 8888, UDP 8887)
Speaker 2: Cryptography & Authentication (PBKDF2, JWT)
Speaker 3: Server Concurrency & SQLite WAL Mode
Speaker 4: Real-time Audio/Video Media & Memory Safety`,
        pos: { x: 1600, y: 1420 }
    },
    tests_qa: {
        title: "حزمة الاختبارات والتحقق الآلي (SyncPulse.Tests 62/62)",
        speaker: "فريق العمل الهندسي (4 متحدثين)",
        desc: "اجتياز 62 اختباراً آلياً شاملاً بنسبة نجاح 100% (0 Failures) تغطي كافة طبقات النظام.",
        points: [
            "اختبارات التأطير 12-Byte وترتيب Big-Endian.",
            "اختبارات محرك التشفير PBKDF2 والـ Salt والـ JWT.",
            "اختبارات محلل تجزئة دفق TCP (FrameStreamParser).",
            "اختبارات قاعدة بيانات SQLite والمستودعات السبعة.",
            "اختبارات الاكتشاف التلقائي UDP 8887 ومكرر الوسائط UDP 8889.",
            "اختبارات اتصال End-to-End كاملة عبر مقابس حقيقية."
        ],
        code: `// Test Suite Results
Total Tests: 62
Passed: 62 (100%)
Failed: 0
Execution Time: 2.14s`,
        pos: { x: 2000, y: 1420 }
    }
};

// Canvas Transform State
let scale = 0.85;
let panX = 0;
let panY = 0;
let isPanning = false;
let startX = 0;
let startY = 0;

const themes = ["dark", "light", "matrix"];
let currentThemeIndex = 0;

document.addEventListener('DOMContentLoaded', () => {
    initCanvas();
    setupKeyboardControls();
    focusNode('server'); // Initial focus on server
});

/**
 * Initialize Canvas & Event Listeners
 */
function initCanvas() {
    const viewport = document.getElementById('canvas-viewport');
    const world = document.getElementById('canvas-world');

    // Pan Event Listeners
    viewport.addEventListener('mousedown', (e) => {
        if (e.target.closest('.hud-top-bar') || e.target.closest('.hud-zoom-controls') || 
            e.target.closest('.hud-quick-nodes-deck') || e.target.closest('.inspector-flyout-drawer')) {
            return;
        }
        isPanning = true;
        startX = e.clientX - panX;
        startY = e.clientY - panY;
    });

    window.addEventListener('mousemove', (e) => {
        if (!isPanning) return;
        panX = e.clientX - startX;
        panY = e.clientY - startY;
        applyTransform();
    });

    window.addEventListener('mouseup', () => {
        isPanning = false;
    });

    // Zoom on Wheel
    viewport.addEventListener('wheel', (e) => {
        e.preventDefault();
        const zoomFactor = e.deltaY < 0 ? 1.08 : 0.92;
        scale = Math.min(Math.max(0.35, scale * zoomFactor), 2.2);
        applyTransform();
    }, { passive: false });
}

function applyTransform() {
    const world = document.getElementById('canvas-world');
    world.style.transform = `translate(calc(-50% + ${panX}px), calc(-50% + ${panY}px)) scale(${scale})`;
}

/**
 * Zoom Controls
 */
function zoomIn() {
    scale = Math.min(scale * 1.2, 2.2);
    applyTransform();
}

function zoomOut() {
    scale = Math.max(scale * 0.8, 0.35);
    applyTransform();
}

function resetZoom() {
    scale = 0.85;
    panX = 0;
    panY = 0;
    applyTransform();
}

/**
 * Focus and Smoothly Fly to a Node on the Canvas
 */
function focusNode(nodeKey) {
    const node = nodeDatabase[nodeKey];
    if (!node) return;

    // Update chip buttons active state
    document.querySelectorAll('.node-chip-btn').forEach(btn => {
        if (btn.innerText.includes(node.title.substring(0, 4))) {
            btn.classList.add('active');
        } else {
            btn.classList.remove('active');
        }
    });

    // Update active speaker label in top HUD
    document.getElementById('active-speaker-label').innerText = node.speaker;

    // Smoothly pan canvas to center the target node
    // Node coordinates are relative to the 3200x2200 plane
    const targetX = 1600 - node.pos.x;
    const targetY = 1100 - node.pos.y;

    panX = targetX * scale;
    panY = targetY * scale;
    applyTransform();

    // Open Inspector with details
    inspectNode(nodeKey);
}

/**
 * Inspect Node & Open Deep-Dive Flyout Panel
 */
function inspectNode(nodeKey) {
    const node = nodeDatabase[nodeKey];
    if (!node) return;

    const drawer = document.getElementById('inspector-flyout');
    const titleElem = document.getElementById('inspector-title');
    const contentElem = document.getElementById('inspector-content');

    titleElem.innerText = node.title;

    let pointsHtml = node.points.map(p => `<li>${p}</li>`).join('');

    contentElem.innerHTML = `
        <div class="inspector-speaker-badge">
            🎙️ ${node.speaker}
        </div>

        <div class="inspector-card">
            <h4>💡 النظرة الهندسية العامة</h4>
            <p>${node.desc}</p>
        </div>

        <div class="inspector-card">
            <h4>📌 النقاط الهندسية المحورية</h4>
            <ul>${pointsHtml}</ul>
        </div>

        <div class="inspector-card">
            <h4>💻 الكود والتحقق العملي</h4>
            <div style="background:#090D18; border:1px solid rgba(255,255,255,0.1); border-radius:8px; overflow:hidden; margin-top:6px;">
                <pre style="padding:10px 12px; font-family:var(--font-mono); font-size:0.75rem; color:#F8FAFC; direction:ltr; text-align:left; overflow-x:auto;"><code>${node.code}</code></pre>
            </div>
        </div>
    `;

    drawer.classList.add('active');
}

function closeInspector() {
    document.getElementById('inspector-flyout').classList.remove('active');
}

/**
 * Multi-Theme Engine
 */
function cycleTheme() {
    currentThemeIndex = (currentThemeIndex + 1) % themes.length;
    const selectedTheme = themes[currentThemeIndex];
    document.documentElement.setAttribute('data-theme', selectedTheme);
}

/**
 * Fullscreen Toggle
 */
function toggleFullscreen() {
    if (!document.fullscreenElement) {
        document.documentElement.requestFullscreen().catch(err => {
            console.error(`Fullscreen error: ${err.message}`);
        });
    } else {
        if (document.exitFullscreen) {
            document.exitFullscreen();
        }
    }
}

/**
 * Keyboard Shortcuts
 */
function setupKeyboardControls() {
    document.addEventListener('keydown', (e) => {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

        switch (e.key) {
            case '+':
            case '=':
                zoomIn();
                break;
            case '-':
            case '_':
                zoomOut();
                break;
            case '0':
                resetZoom();
                break;
            case 'Escape':
                closeInspector();
                break;
            case 'f':
            case 'F':
            case 'ب':
                toggleFullscreen();
                break;
            case 't':
            case 'T':
            case 'ف':
                cycleTheme();
                break;
            case 'ArrowRight':
                panX += 60;
                applyTransform();
                break;
            case 'ArrowLeft':
                panX -= 60;
                applyTransform();
                break;
            case 'ArrowUp':
                panY += 60;
                applyTransform();
                break;
            case 'ArrowDown':
                panY -= 60;
                applyTransform();
                break;
        }
    });
}
