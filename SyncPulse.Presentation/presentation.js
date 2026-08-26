/**
 * SyncPulse Interactive Presentation Engine
 * Controls Slide Deck Navigation, Speaker Notes, Jump Menu, and Keyboard Shortcuts.
 */

// Presentation State
const presentationState = {
    currentSlide: 0,
    totalSlides: 13,
    isNotesOpen: false,
    isMenuOpen: false
};

// Speaker Notes Data tailored specifically for the 4-Speaker Team
const speakerNotes = [
    {
        slide: 1,
        speaker: "المتحدث الأول (مهندس المعمارية والشبكات)",
        notes: `
            <strong>نقاط الشرح الافتتاحي:</strong>
            <ul>
                <li>الترحيب بأستاذ المقرر ولجنة الإشراف الكريمة.</li>
                <li>تقديم اسم المشروع: SyncPulse (SecureTalk) كنظام اتصالات محلي آمن مصمم خصيصاً لمقرر برمجة خادم وعميل.</li>
                <li>التأكيد على أن المشروع مبني من الصفر على المقابس الخام (Raw TCP/UDP Sockets) دون الاعتماد على SignalR أو WebRTC.</li>
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
                <li>توضيح حل SyncPulse: نظام On-Premise 100% يعمل باستقلالية تامة داخل الشبكة المحلية LAN/WLAN بأقل زمن تأخير.</li>
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
            <strong>حزمة الاختبارات الآلية (SyncPulse.Tests):</strong>
            <ul>
                <li>استعراض نجاح 62 اختباراً آلياً شاملاً بنسبة 100% (0 Failures).</li>
                <li>تغطية كافة الوحدات: التأطير، التشفير، تجزئة الدفق، SQLite، الجلسات، المكالمات، وUDP Relay.</li>
            </ul>
        `
    },
    {
        slide: 13,
        speaker: "جميع المتحدثين",
        notes: `
            <strong>الخاتمة والانتقال للعرض الحي:</strong>
            <ul>
                <li>شكر الأستاذ واللجنة.</li>
                <li>النقر على زر "فتح مركز العرض المباشر للأجهزة الثلاثة" للبدء بالبث العملي الحي.</li>
            </ul>
        `
    }
];

// DOM Elements
const slides = document.querySelectorAll('.slide');
const currentSlideNumEl = document.getElementById('current-slide-num');
const progressBarEl = document.getElementById('slide-progress-bar');
const speakerCueLabel = document.getElementById('speaker-cue-label');
const notesDrawer = document.getElementById('speaker-notes-drawer');
const notesContent = document.getElementById('speaker-notes-content');
const menuModal = document.getElementById('slide-menu-modal');
const menuList = document.getElementById('slide-menu-list');

/**
 * Navigate to a specific slide index (0 to totalSlides - 1)
 */
function goToSlide(index) {
    if (index < 0 || index >= presentationState.totalSlides) return;

    // Update Classes
    slides.forEach((slide, idx) => {
        slide.classList.remove('active', 'prev');
        if (idx === index) {
            slide.classList.add('active');
        } else if (idx < index) {
            slide.classList.add('prev');
        }
    });

    presentationState.currentSlide = index;

    // Update UI Indicators
    const slideNumber = index + 1;
    const formattedNum = slideNumber < 10 ? `0${slideNumber}` : `${slideNumber}`;
    currentSlideNumEl.textContent = formattedNum;

    // Update Progress Bar
    const progressPercent = (slideNumber / presentationState.totalSlides) * 100;
    progressBarEl.style.width = `${progressPercent}%`;

    // Update Current Speaker Cue
    const currentSlideEl = slides[index];
    const speakerName = currentSlideEl.getAttribute('data-speaker') || 'المتحدث الحالي';
    speakerCueLabel.innerHTML = `المتحدث الحالي: <strong>${speakerName}</strong>`;

    // Update Notes Content
    updateNotesContent(slideNumber);

    // Update Menu Active Item
    updateMenuActiveItem(index);
}

/**
 * Next Slide
 */
function nextSlide() {
    if (presentationState.currentSlide < presentationState.totalSlides - 1) {
        goToSlide(presentationState.currentSlide + 1);
    }
}

/**
 * Previous Slide
 */
function prevSlide() {
    if (presentationState.currentSlide > 0) {
        goToSlide(presentationState.currentSlide - 1);
    }
}

/**
 * Toggle Speaker Notes Drawer
 */
function toggleNotes() {
    presentationState.isNotesOpen = !presentationState.isNotesOpen;
    if (presentationState.isNotesOpen) {
        notesDrawer.classList.add('active');
    } else {
        notesDrawer.classList.remove('active');
    }
}

/**
 * Update Notes Content based on current slide
 */
function updateNotesContent(slideNum) {
    const noteObj = speakerNotes.find(n => n.slide === slideNum);
    if (noteObj) {
        notesContent.innerHTML = `
            <div style="background:#EFF6FF; border:1px solid #BFDBFE; padding:6px 12px; border-radius:6px; margin-bottom:10px; font-weight:800; color:#1D4ED8;">
                👤 ${noteObj.speaker}
            </div>
            ${noteObj.notes}
        `;
    } else {
        notesContent.innerHTML = `<p style="color:#94A3B8;">لا توجد ملاحظات مخصصة لهذه الشريحة.</p>`;
    }
}

/**
 * Toggle Slide Jump Menu Modal
 */
function toggleMenu() {
    presentationState.isMenuOpen = !presentationState.isMenuOpen;
    if (presentationState.isMenuOpen) {
        menuModal.classList.add('active');
    } else {
        menuModal.classList.remove('active');
    }
}

/**
 * Build Jump Menu List
 */
function buildJumpMenu() {
    menuList.innerHTML = '';
    slides.forEach((slide, idx) => {
        const slideTitle = slide.getAttribute('data-title') || `الشريحة ${idx + 1}`;
        const speaker = slide.getAttribute('data-speaker') || '';
        const btn = document.createElement('button');
        btn.className = `menu-item-btn ${idx === presentationState.currentSlide ? 'active' : ''}`;
        btn.innerHTML = `
            <span><strong>${idx + 1}.</strong> ${slideTitle}</span>
            <span class="speaker-tag">${speaker}</span>
        `;
        btn.onclick = () => {
            goToSlide(idx);
            toggleMenu();
        };
        menuList.appendChild(btn);
    });
}

function updateMenuActiveItem(activeIndex) {
    const items = menuList.querySelectorAll('.menu-item-btn');
    items.forEach((item, idx) => {
        if (idx === activeIndex) item.classList.add('active');
        else item.classList.remove('active');
    });
}

/**
 * Toggle Fullscreen
 */
function toggleFullscreen() {
    if (!document.fullscreenElement) {
        document.documentElement.requestFullscreen().catch(err => {
            console.warn(`Fullscreen error: ${err.message}`);
        });
    } else {
        if (document.exitFullscreen) document.exitFullscreen();
    }
}

/**
 * Keyboard Navigation Shortcuts
 */
document.addEventListener('keydown', (e) => {
    // Escape closes modals
    if (e.key === 'Escape') {
        if (presentationState.isMenuOpen) toggleMenu();
        if (presentationState.isNotesOpen) toggleNotes();
        return;
    }

    switch (e.key) {
        case 'ArrowLeft':
        case ' ':
        case 'PageDown':
            nextSlide();
            break;
        case 'ArrowRight':
        case 'PageUp':
            prevSlide();
            break;
        case 'Home':
            goToSlide(0);
            break;
        case 'End':
            goToSlide(presentationState.totalSlides - 1);
            break;
        case 'f':
        case 'F':
            toggleFullscreen();
            break;
        case 'n':
        case 'N':
            toggleNotes();
            break;
        case 'm':
        case 'M':
            toggleMenu();
            break;
    }
});

// Initialize
window.addEventListener('DOMContentLoaded', () => {
    buildJumpMenu();
    goToSlide(0);
});
