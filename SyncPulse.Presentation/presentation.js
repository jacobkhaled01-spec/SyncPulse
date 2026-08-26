/**
 * SyncPulse Interactive Presentation Engine - Executive Edition
 * Controls Slide Navigation, Speaker Notes, Stopwatch Timer, Virtual Laser Pointer, and Interactive Simulators.
 */

// Global Presentation State
const presentationState = {
    currentSlide: 0,
    totalSlides: 13,
    isNotesOpen: false,
    isMenuOpen: false,
    isGridOpen: false,
    isLaserActive: false,
    
    // Stopwatch Timer
    timerSeconds: 0,
    timerInterval: null,
    isTimerRunning: false,
    
    // Ring buffer animation
    ringBufferIndex: 0,
    ringBufferInterval: null
};

// Speaker Notes for the 4-Speaker Team
const speakerNotes = [
    {
        slide: 1,
        speaker: "المتحدث الأول (مهندس المعمارية والشبكات)",
        notes: `
            <strong>نقاط الشرح الافتتاحي:</strong>
            <ul>
                <li>الترحيب بأستاذ المقرر ولجنة الإشراف الكريمة.</li>
                <li>تقديم اسم المشروع: SyncPulse (SecureTalk) كنظام اتصالات محلي آمن مصمم خصيصاً لمقرر برمجة خادم وعميل.</li>
                <li>التأكيد على أن المشروع مبني من الصفر على المقابس الخام (Raw TCP/UDP Sockets) دون الاعتماد على مكتبات جاهزة مثل SignalR أو WebRTC.</li>
                <li>توضيح هيكل الفريق المكون من 4 مهندسين متكاملين في مجالات المعمارية، الأمان، السيرفر، والميديا.</li>
            </ul>
        `
    },
    {
        slide: 2,
        speaker: "المتحدث الأول (مهندس المعمارية والشبكات)",
        notes: `
            <strong>المشكلة والأهداف:</strong>
            <ul>
                <li>شرح مشكلة انقطاع الإنترنت السحابي في المؤسسات والمنشآت المعزولة.</li>
                <li>توضيح خطر تسريب البيانات الحساسة للمزودين الخارجيين وفقدان السيادة الرقمية.</li>
                <li>توضيح حل SyncPulse: نظام On-Premise 100% يعمل باستقلالية تامة داخل الشبكة المحلية LAN/WLAN بأقل زمن تأخير (&lt;10ms).</li>
            </ul>
        `
    },
    {
        slide: 3,
        speaker: "المتحدث الأول (مهندس المعمارية والشبكات)",
        notes: `
            <strong>المعمارية وتقسيم الطبقات:</strong>
            <ul>
                <li>شرح تقسيم المشاريع وفق Clean Architecture: النواة المشتركة SyncPulse.Core، الخادم المركزي SyncPulse.Server، والعميل SyncPulse.Client.</li>
                <li>توضيح أن Core هو Class Library يحتوي على عقد البروتوكول الموحد وDTOs لمنع التكرار.</li>
                <li>ذكر المنافذ المعتمدة: TCP 8888 للتحكم والرسائل، UDP 8889 لمكرر الوسائط، و UDP 8887 لاكتشاف الواي فاي.</li>
            </ul>
        `
    },
    {
        slide: 4,
        speaker: "المتحدث الأول (مهندس المعمارية والشبكات)",
        notes: `
            <strong>بروتوكول التأطير الثنائي 12-Byte FrameHeader:</strong>
            <ul>
                <li>شرح معضلة دمج الحزم (Merging) وتجزئة الحزم (Fragmentation) في دفق بايتات TCP.</li>
                <li>استعراض حقول الترويسة: Magic (0x53), Version (0x01), PacketType (16-bit), PayloadLength (32-bit), SeqNumber (32-bit).</li>
                <li>شرح أهمية ترتيب الشبكة القياسي Big-Endian في توحيد قراءة الأرقام بين معالجات x86 و ARM.</li>
                <li>شرح آلة الحالة FrameStreamParser في اقتطاع الحزم بدقة دون تداخل.</li>
            </ul>
        `
    },
    {
        slide: 5,
        speaker: "المتحدث الأول (مهندس المعمارية والشبكات)",
        notes: `
            <strong>الاكتشاف التلقائي على الواي فاي (UDP Port 8887):</strong>
            <ul>
                <li>شرح استخدام UDP Broadcast على عنوان 255.255.255.255.</li>
                <li>الخادم يرسل إعلاناً دورياً، والعميل يستمع ويلتقط IP الخادم ذاتياً.</li>
                <li>فائدة الميزة: عدم إلزام المستخدم بكتابة عنوان IP السيرفر يدوياً، والتوافق مع DHCP الديناميكي.</li>
            </ul>
        `
    },
    {
        slide: 6,
        speaker: "المتحدث الثاني (مهندس الأمن والمصادقة)",
        notes: `
            <strong>الأمن السيبراني والمصادقة وجلسات JWT:</strong>
            <ul>
                <li>شرح خوارزمية PBKDF2/SHA-256 مع 100,000 دورة تكرار ومولّد ملح عشوائي 128-bit Salt لكل مستخدم.</li>
                <li>توضيح الحماية من هجمات التوقيت عبر دالة FixedTimeEquals.</li>
                <li>شرح توكنات JWT الموقعة بـ HMAC-SHA256 للتحقق الرياضي الفوري دون استعلام قاعدة البيانات في كل حزمة.</li>
                <li>شرح حماية قاعدة البيانات عبر Parameterized Queries ومنع هجمات DoS بسقف 10MB للحزمة.</li>
            </ul>
        `
    },
    {
        slide: 7,
        speaker: "المتحدث الثالث (مهندس الخادم وقواعد البيانات)",
        notes: `
            <strong>الخادم والتزامن و SQLite WAL Mode:</strong>
            <ul>
                <li>شرح برمجة المقابس غير الحاجبة عبر I/O Completion Ports (IOCP) واستدعاءات async/await.</li>
                <li>مقارنة نموذج IOCP مع نموذج خيط لكل عميل (Thread-per-Client) وتجنب اختناق الخيوط.</li>
                <li>شرح نمط الحواجز المقسمة Bulkhead Pattern لعزل استثناءات العملاء ومنع انهيار الخادم.</li>
                <li>شرح وضع WAL Mode في SQLite الذي يسمح بعدة قراءات متزامنة أثناء الكتابة بدون أقفال.</li>
            </ul>
        `
    },
    {
        slide: 8,
        speaker: "المتحدث الثالث (مهندس الخادم وقواعد البيانات)",
        notes: `
            <strong>طابور الرسائل المعلقة والمزامنة التلغرامية:</strong>
            <ul>
                <li>شرح دورة حياة حالات الرسائل: Sent (✓) ➔ Delivered (✓✓) ➔ Read (✓✓ زرقاء).</li>
                <li>شرح آلية طابور الانتظار (Offline Queue): حفظ الرسائل للمستخدمين غير المتصلين ودفعها فورياً عند دخولهم.</li>
                <li>شرح كشف المقابس المعلقة (Half-Open Sockets) بفحص القراءة الصفرية ونبضات القلب Heartbeat.</li>
            </ul>
        `
    },
    {
        slide: 9,
        speaker: "المتحدث الرابع (مهندس الميديا والوسائط)",
        notes: `
            <strong>محرك الصوت 16kHz HD والمخازن الدائرية:</strong>
            <ul>
                <li>شرح معيار الصوت 16000 Hz, 16-bit Mono (1280 بايت لكل إطار 40ms).</li>
                <li>شرح السبب الجذري لخطأ الذاكرة 0xc0000005 الناتج عن تحرير الـ WAVEHDR أثناء قراءة بطاقة الصوت.</li>
                <li>شرح الحل المبتكر: نظام المخازن الدائرية الثابتة Zero-Allocation Pre-Pinned Ring Buffers المثبتة بـ Pinned GCHandle.</li>
            </ul>
        `
    },
    {
        slide: 10,
        speaker: "المتحدث الرابع (مهندس الميديا والوسائط)",
        notes: `
            <strong>الفيديو والبث الهجين وإطفاء ضوء الكاميرا:</strong>
            <ul>
                <li>شرح استخدام مكتبة AForge DirectShow والتحكم بمقبض الكاميرا في نظام التشغيل.</li>
                <li>توضيح انطفاء ضوء الكاميرا الحقيقي (LED) على اللابتوب عند الكتم لحماية خصوصية المستخدم.</li>
                <li>شرح خوارزمية البث الهجين (UDP+TCP) مع إلغاء التكرار اللحظي عبر مفتاح ثنائي 64-بت.</li>
                <li>عرض وسائط تليجرام المباشرة (Inline Images & Audio Players).</li>
            </ul>
        `
    },
    {
        slide: 11,
        speaker: "جميع المتحدثين",
        notes: `
            <strong>مصفوفة المتحدثين الـ 4:</strong>
            <ul>
                <li>تأكيد جاهزية كل متحدث لمحوره الأكاديمي والعملي.</li>
                <li>المتحدث 1 للشبكات والمقابس والتأطير، المتحدث 2 للأمن والمصادقة، المتحدث 3 للخادم وقواعد البيانات، والمتحدث 4 للميديا والذاكرة.</li>
            </ul>
        `
    },
    {
        slide: 12,
        speaker: "جميع المتحدثين",
        notes: `
            <strong>التحقق الآلي والاختبارات الشاملة (62/62):</strong>
            <ul>
                <li>استعراض نتائج اختبارات مشروع SyncPulse.Tests الآلية الـ 62 بدون أي فشل (Zero Failures).</li>
                <li>التأكيد على اختبار كل وحدة بمفردها واختبار التدفق الكامل End-to-End.</li>
            </ul>
        `
    },
    {
        slide: 13,
        speaker: "جميع المتحدثين",
        notes: `
            <strong>الخاتمة والانتقال للعرض العملي الحي:</strong>
            <ul>
                <li>شكر أستاذ المقرر ولجنة الإشراف على اهتمامهم ودعمهم.</li>
                <li>الإعلان عن بدء العرض العملي الحي للأجهزة الثلاثة عبر مركز العرض المباشر (SyncPulse Live Web Hub).</li>
            </ul>
        `
    }
];

// Document Ready Initialization
document.addEventListener('DOMContentLoaded', () => {
    initPresentation();
    setupKeyboardShortcuts();
    buildSlideMenu();
    buildGridOverview();
    initLaserPointer();
    initSimulators();
});

/**
 * Initialize Slides, Indicators, and Progress Bar
 */
function initPresentation() {
    const slides = document.querySelectorAll('.slide');
    presentationState.totalSlides = slides.length;
    document.getElementById('total-slides-num').innerText = presentationState.totalSlides.toString().padStart(2, '0');
    goToSlide(0);
}

/**
 * Navigate to a specific slide index
 */
function goToSlide(index) {
    const slides = document.querySelectorAll('.slide');
    if (index < 0 || index >= slides.length) return;

    slides.forEach((slide, i) => {
        if (i === index) {
            slide.classList.add('active');
        } else {
            slide.classList.remove('active');
        }
    });

    presentationState.currentSlide = index;

    // Update Header Indicators
    const currentNumStr = (index + 1).toString().padStart(2, '0');
    document.getElementById('current-slide-num').innerText = currentNumStr;

    // Update Progress Bar
    const progressPercent = ((index + 1) / presentationState.totalSlides) * 100;
    document.getElementById('slide-progress-bar').style.width = `${progressPercent}%`;

    // Update Speaker Cue in Footer
    const activeSlide = slides[index];
    const speakerName = activeSlide.getAttribute('data-speaker') || 'فريق العمل الهندسي';
    const cueElem = document.getElementById('speaker-cue-label');
    if (cueElem) {
        cueElem.innerHTML = `المتحدث الحالي: <strong>${speakerName}</strong>`;
    }

    // Update Speaker Notes Content
    updateSpeakerNotes(index);

    // Update Active states in Menu & Grid
    updateActiveMenuAndGrid(index);

    // Slide-specific triggers
    handleSlideSpecialTriggers(index);
}

function nextSlide() {
    if (presentationState.currentSlide < presentationState.totalSlides - 1) {
        goToSlide(presentationState.currentSlide + 1);
    }
}

function prevSlide() {
    if (presentationState.currentSlide > 0) {
        goToSlide(presentationState.currentSlide - 1);
    }
}

/**
 * Update Speaker Notes Drawer Content
 */
function updateSpeakerNotes(slideIndex) {
    const notesContainer = document.getElementById('notes-content') || document.getElementById('speaker-notes-content');
    if (!notesContainer) return;

    const currentNote = speakerNotes[slideIndex];
    if (currentNote) {
        notesContainer.innerHTML = `
            <div style="margin-bottom: 8px; color: #1E3A8A; font-weight: 800; font-size: 0.86rem;">
                🎙️ ${currentNote.speaker}
            </div>
            ${currentNote.notes}
        `;
    } else {
        notesContainer.innerHTML = `<p style="color: #64748B;">لا توجد ملاحظات إضافية لهذه الشريحة.</p>`;
    }
}

/**
 * Toggle Speaker Notes Drawer
 */
function toggleNotes() {
    const drawer = document.getElementById('notes-drawer') || document.getElementById('speaker-notes-drawer');
    const btn = document.getElementById('btn-notes');
    if (!drawer) return;

    presentationState.isNotesOpen = !presentationState.isNotesOpen;

    if (presentationState.isNotesOpen) {
        drawer.classList.add('active');
        if (btn) btn.classList.add('active');
    } else {
        drawer.classList.remove('active');
        if (btn) btn.classList.remove('active');
    }
}

/**
 * Build Visual Grid Light-Table (Overview Mode)
 */
function buildGridOverview() {
    const container = document.getElementById('grid-thumbs-container') || document.getElementById('grid-thumbnails-container');
    if (!container) return;

    container.innerHTML = '';
    const slides = document.querySelectorAll('.slide');

    slides.forEach((slide, index) => {
        const title = slide.getAttribute('data-title') || `الشريحة ${index + 1}`;
        const speaker = slide.getAttribute('data-speaker') || '';

        const card = document.createElement('div');
        card.className = 'thumb-box';
        card.id = `thumb-card-${index}`;
        card.innerHTML = `
            <div>
                <span class="num">SLIDE ${(index + 1).toString().padStart(2, '0')}</span>
                <h4 class="title">${title}</h4>
            </div>
            <span class="spk">${speaker}</span>
        `;
        card.onclick = () => {
            goToSlide(index);
            toggleGrid();
        };
        container.appendChild(card);
    });
}

function toggleGrid() {
    const modal = document.getElementById('grid-modal') || document.getElementById('slide-grid-modal');
    if (!modal) return;
    presentationState.isGridOpen = !presentationState.isGridOpen;
    if (presentationState.isGridOpen) {
        modal.classList.add('active');
    } else {
        modal.classList.remove('active');
    }
}

function updateActiveMenuAndGrid(activeIndex) {
    document.querySelectorAll('.menu-item-btn').forEach((btn, i) => {
        if (i === activeIndex) btn.classList.add('active');
        else btn.classList.remove('active');
    });

    document.querySelectorAll('.thumb-card').forEach((card, i) => {
        if (i === activeIndex) card.classList.add('active');
        else card.classList.remove('active');
    });
}

/**
 * Defense Presentation Stopwatch Timer
 */
function toggleTimer() {
    const btn = document.getElementById('timer-toggle-btn');
    if (presentationState.isTimerRunning) {
        clearInterval(presentationState.timerInterval);
        presentationState.isTimerRunning = false;
        if (btn) btn.innerText = '▶';
    } else {
        presentationState.timerInterval = setInterval(() => {
            presentationState.timerSeconds++;
            const mins = Math.floor(presentationState.timerSeconds / 60).toString().padStart(2, '0');
            const secs = (presentationState.timerSeconds % 60).toString().padStart(2, '0');
            const disp = document.getElementById('timer-display');
            if (disp) disp.innerText = `${mins}:${secs}`;
        }, 1000);
        presentationState.isTimerRunning = true;
        if (btn) btn.innerText = '⏸';
    }
}

function resetTimer() {
    clearInterval(presentationState.timerInterval);
    presentationState.isTimerRunning = false;
    presentationState.timerSeconds = 0;
    const disp = document.getElementById('timer-display');
    const btn = document.getElementById('timer-toggle-btn');
    if (disp) disp.innerText = '00:00';
    if (btn) btn.innerText = '▶';
}

/**
 * Virtual Laser Pointer Tool
 */
function initLaserPointer() {
    const laserDot = document.getElementById('laser-dot');
    if (!laserDot) return;

    window.addEventListener('mousemove', (e) => {
        if (presentationState.isLaserActive) {
            laserDot.style.left = `${e.clientX}px`;
            laserDot.style.top = `${e.clientY}px`;
        }
    });
}

function toggleLaser() {
    const laserDot = document.getElementById('laser-dot');
    const btn = document.getElementById('btn-laser');
    presentationState.isLaserActive = !presentationState.isLaserActive;

    if (presentationState.isLaserActive) {
        if (laserDot) laserDot.classList.add('active');
        if (btn) btn.classList.add('active');
    } else {
        if (laserDot) laserDot.classList.remove('active');
        if (btn) btn.classList.remove('active');
    }
}

/**
 * Interactive Slide Simulators
 */
function initSimulators() {
    // 1. Binary Inspector on Slide 4
    document.querySelectorAll('.binary-cell').forEach(cell => {
        cell.addEventListener('click', () => {
            document.querySelectorAll('.binary-cell').forEach(c => c.classList.remove('selected'));
            cell.classList.add('selected');
        });
    });

    // 2. Interactive Ring Buffer on Slide 9
    startRingBufferSimulation();
}

function startRingBufferSimulation() {
    if (presentationState.ringBufferInterval) clearInterval(presentationState.ringBufferInterval);
    presentationState.ringBufferInterval = setInterval(() => {
        const slots = document.querySelectorAll('.ring-slot');
        if (!slots || slots.length === 0) return;

        slots.forEach((s, idx) => {
            if (idx === presentationState.ringBufferIndex) {
                s.classList.add('active-slot');
                s.querySelector('.slot-state').innerText = 'IN USE';
            } else {
                s.classList.remove('active-slot');
                s.querySelector('.slot-state').innerText = 'READY';
            }
        });

        presentationState.ringBufferIndex = (presentationState.ringBufferIndex + 1) % slots.length;
    }, 600);
}

function handleSlideSpecialTriggers(slideIndex) {
    if (slideIndex === 8) {
        // Slide 9: Audio Ring buffer
        startRingBufferSimulation();
    }
}

/**
 * Multi-Theme Engine Switcher
 */
function setTheme(themeName) {
    document.documentElement.setAttribute('data-theme', themeName);
    localStorage.setItem('syncpulse_theme', themeName);

    // Update active button state
    document.querySelectorAll('.theme-pill-btn').forEach(btn => {
        const title = btn.getAttribute('title') || '';
        if (themeName === 'dark' && title.includes('دارك')) {
            btn.classList.add('active');
        } else if (themeName === 'light' && title.includes('فاتح')) {
            btn.classList.add('active');
        } else if (themeName === 'matrix' && title.includes('ماتركس')) {
            btn.classList.add('active');
        } else {
            btn.classList.remove('active');
        }
    });
}

/**
 * Toggle Fullscreen
 */
function toggleFullscreen() {
    if (!document.fullscreenElement) {
        document.documentElement.requestFullscreen().catch(err => {
            console.error(`Error entering fullscreen: ${err.message}`);
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
function setupKeyboardShortcuts() {
    document.addEventListener('keydown', (e) => {
        // Don't trigger if user is typing
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

        switch (e.key) {
            case 'ArrowLeft':
            case ' ':
            case 'PageDown':
            case 'Enter':
                e.preventDefault();
                nextSlide();
                break;

            case 'ArrowRight':
            case 'PageUp':
            case 'Backspace':
                e.preventDefault();
                prevSlide();
                break;

            case 'Home':
                e.preventDefault();
                goToSlide(0);
                break;

            case 'End':
                e.preventDefault();
                goToSlide(presentationState.totalSlides - 1);
                break;

            case 'n':
            case 'N':
            case 'ى':
                toggleNotes();
                break;

            case 'm':
            case 'M':
            case 'ة':
                toggleMenu();
                break;

            case 'g':
            case 'G':
            case 'ل':
                toggleGrid();
                break;

            case 'l':
            case 'L':
            case 'م':
                toggleLaser();
                break;

            case 'f':
            case 'F':
            case 'ب':
                toggleFullscreen();
                break;

            case 'Escape':
                if (presentationState.isNotesOpen) toggleNotes();
                if (presentationState.isMenuOpen) toggleMenu();
                if (presentationState.isGridOpen) toggleGrid();
                if (presentationState.isLaserActive) toggleLaser();
                break;
        }
    });
}
