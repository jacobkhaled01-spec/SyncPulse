/**
 * ==============================================================================
 * SyncPulse Interactive Tech Cockpit Engine
 * Presentation Controller & Simulator System
 * ==============================================================================
 */

// Module & Slide Metadata
const cockpitModules = [
    {
        num: "01",
        tag: "MODULE 01 // TOPOLOGY",
        title: "المخطط الطوبولوجي العام لنظام SyncPulse (SecureTalk)",
        desc: "نظام اتصالات ومراسلة محلي فائق السرعة مبني بالكامل على المقابس الخام (Raw Sockets)",
        speaker: "المتحدث 1: مهندس المعمارية والشبكات",
        badge: "TOPOLOGY"
    },
    {
        num: "02",
        tag: "MODULE 02 // DIAGNOSTIC",
        title: "المشكلة والدوافع الهندسية لبناء نظام SyncPulse",
        desc: "معالجة مخاطر الاعتماد على السحابة الخارجية في الشبكات المعزولة",
        speaker: "المتحدث 1: مهندس المعمارية والشبكات",
        badge: "DIAGNOSTIC"
    },
    {
        num: "03",
        tag: "MODULE 03 // CLEAN ARCH",
        title: "معمارية النظام النظيفة (3-Tier Layered Stack)",
        desc: "فصل فيزيائي تام للمسؤوليات لمنع التكرار وتسهيل التوسع",
        speaker: "المتحدث 1: مهندس المعمارية والشبكات",
        badge: "CLEAN ARCH"
    },
    {
        num: "04",
        tag: "MODULE 04 // FRAMING",
        title: "مقابس TCP وبروتوكول التأطير الثنائي (12-Byte FrameHeader)",
        desc: "حل معضلة دمج وتجزئة دفق TCP بترتيب Big-Endian الشبكي الموحد",
        speaker: "المتحدث 1: مهندس المعمارية والشبكات",
        badge: "FRAMING"
    },
    {
        num: "05",
        tag: "MODULE 05 // DISCOVERY",
        title: "الاكتشاف التلقائي لشبكات الواي فاي (UDP Port 8887)",
        desc: "ربط العملاء بالخادم ذاتياً وفورياً بنمط Zero-Configuration دون ضبط IP يدوي",
        speaker: "المتحدث 1: مهندس المعمارية والشبكات",
        badge: "DISCOVERY"
    },
    {
        num: "06",
        tag: "MODULE 06 // SECURITY",
        title: "الأمن السيبراني والمصادقة المشفرة (PBKDF2 & JWT RFC 7519)",
        desc: "تجزئة كلمات المرور بـ 128-bit Salt وتوكنات الجلسات المشفرة مع سجلات ISO 27001",
        speaker: "المتحدث 2: مهندس الأمن والمصادقة",
        badge: "SECURITY"
    },
    {
        num: "07",
        tag: "MODULE 07 // CONCURRENCY",
        title: "معمارية تزامن الخادم IOCP ووضع SQLite WAL Mode",
        desc: "خدمة مئات المتصلين دون اختناق الخيوط مع مناعة تامة ضد الانهيار (Bulkhead)",
        speaker: "المتحدث 3: مهندس الخادم وقواعد البيانات",
        badge: "IOCP / WAL"
    },
    {
        num: "08",
        tag: "MODULE 08 // SYNC",
        title: "دورة حياة الرسائل وطابور الرسائل المعلقة (Offline Queue)",
        desc: "مزامنة تلغرامية متكاملة لضمان وصول الرسائل بمؤشرات التسليم (✓ / ✓✓ / ✓✓)",
        speaker: "المتحدث 3: مهندس الخادم وقواعد البيانات",
        badge: "SYNC ✓✓"
    },
    {
        num: "09",
        tag: "MODULE 09 // AUDIO",
        title: "محرك الصوت منخفض المستوى 16kHz والمخازن الدائرية الثابتة",
        desc: "القضاء التام على خطأ الذاكرة 0xc0000005 بحجز 8 مخازن مثبتة بـ GCHandle Pinned",
        speaker: "المتحدث 4: مهندس الميديا والوسائط",
        badge: "16kHz RING"
    },
    {
        num: "10",
        tag: "MODULE 10 // VIDEO",
        title: "محرك الفيديو المباشر AForge وإطفاء ضوء الكاميرا الحقيقي (LED)",
        desc: "تحكم فيزيائي بالكاميرا في طبقة النواة مع بث هجين متوازي وإلغاء تكرار الإطارات",
        speaker: "المتحدث 4: مهندس الميديا والوسائط",
        badge: "AFORGE VIDEO"
    },
    {
        num: "11",
        tag: "MODULE 11 // SPEAKERS",
        title: "مصفوفة توزيع المهام على المتحدثين الأربعة (4 Speakers)",
        desc: "تكامل هندسي وتوزيع دقيق يغطي كافة جوانب مقرر برمجة خادم وعميل",
        speaker: "فريق العمل الهندسي (4 متحدثين)",
        badge: "4 SPEAKERS"
    },
    {
        num: "12",
        tag: "MODULE 12 // QA",
        title: "نتائج حزمة الاختبارات والتحقق الآلي الشاملة (62/62 Tests)",
        desc: "اجتياز 62 اختباراً آلياً بنسبة نجاح 100% (0 Failures) في مشروع SyncPulse.Tests",
        speaker: "فريق العمل الهندسي (4 متحدثين)",
        badge: "QA PASSED"
    },
    {
        num: "13",
        tag: "MODULE 13 // DEMO",
        title: "منصة إطلاق العرض العملي الحي للأجهزة الثلاثة (Live Demo)",
        desc: "تشغيل الخادم على الجهاز 1 والعملاء على الجهازين 2 و 3 والمراقبة عبر Web Hub",
        speaker: "فريق العمل الهندسي (4 متحدثين)",
        badge: "LIVE DEMO"
    }
];

// Comprehensive Speaker Notes
const speakerNotes = [
    {
        speaker: "المتحدث 1: مهندس المعمارية والشبكات",
        notes: "<p><strong>الهدف من الشريحة:</strong> استعراض الهوية الهندسية للنظام وتوضيح أن المشروع مبني بالكامل على مقابس خام C# Sockets أصلية دون أي مكتبات سحابية وسيطة مثل SignalR أو Firebase.</p>"
    },
    {
        speaker: "المتحدث 1: مهندس المعمارية والشبكات",
        notes: "<p><strong>الهدف من الشريحة:</strong> توضيح المقارنة بين الاعتمادية السحابية والسيادة المحلية (100% On-Premise LAN) مع إبراز سرعة الاستجابة بأقل من 10ms في الشبكة المحلية.</p>"
    },
    {
        speaker: "المتحدث 1: مهندس المعمارية والشبكات",
        notes: "<p><strong>الهدف من الشريحة:</strong> شرح فصل المشاريع الثلاثة في حل .NET وفق مبادئ Clean Architecture لمنع تداخل الأكواد وتسهيل التوسع والصيانة.</p>"
    },
    {
        speaker: "المتحدث 1: مهندس المعمارية والشبكات",
        notes: "<p><strong>الهدف من الشريحة:</strong> شرح كيفية حل مشكلة TCP Stream Fragmentation ببروتوكول التأطير 12-Byte Header واستخدام Big-Endian لتوحيد الترتيب بين المعالجات.</p>"
    },
    {
        speaker: "المتحدث 1: مهندس المعمارية والشبكات",
        notes: "<p><strong>الهدف من الشريحة:</strong> شرح الاكتشاف التلقائي عبر UDP Broadcast على المنفذ 8887 وحل مشكلة العناوين المتغيرة في شبكات الواي فاي.</p>"
    },
    {
        speaker: "المتحدث 2: مهندس الأمن والمصادقة",
        notes: "<p><strong>الهدف من الشريحة:</strong> شرح محرك التشفير PBKDF2/SHA-256 مع الـ Salt العشوائي وتوكنات JWT المطابقة لمعيار RFC 7519 وسجلات التدقيق ISO 27001.</p>"
    },
    {
        speaker: "المتحدث 3: مهندس الخادم وقواعد البيانات",
        notes: "<p><strong>الهدف من الشريحة:</strong> توضيح معمارية التزامن بخادم IOCP Async ونمط Bulkhead لعزل الجلسات، وتشغيل SQLite بوضع WAL Mode لمنع الاختناق أثناء الكتابة.</p>"
    },
    {
        speaker: "المتحدث 3: مهندس الخادم وقواعد البيانات",
        notes: "<p><strong>الهدف من الشريحة:</strong> شرح طابور الرسائل المعلقة Offline Queue ومؤشرات التسليم الثلاثية (✓ Sent ➔ ✓✓ Delivered ➔ ✓✓ Read).</p>"
    },
    {
        speaker: "المتحدث 4: مهندس الميديا والوسائط",
        notes: "<p><strong>الهدف من الشريحة:</strong> شرح حل معضلة انهيار الذاكرة 0xc0000005 عبر نظام المخازن الدائرية الثابتة 8 Pre-Pinned Buffers وضمان Zero-Allocation.</p>"
    },
    {
        speaker: "المتحدث 4: مهندس الميديا والوسائط",
        notes: "<p><strong>الهدف من الشريحة:</strong> توضيح إغلاق مقبض الكاميرا الفيزيائي SignalToStop() لإطفاء ضوء LED الحقيقي، والبث الهجين UDP+TCP مع إلغاء تكرار الإطارات.</p>"
    },
    {
        speaker: "فريق العمل الهندسي (4 متحدثين)",
        notes: "<p><strong>الهدف من الشريحة:</strong> استعراض تكامل أدوار المتحدثين الأربعة وتغطيتهم الشاملة لجميع محاور مقرر برمجة خادم وعميل.</p>"
    },
    {
        speaker: "فريق العمل الهندسي (4 متحدثين)",
        notes: "<p><strong>الهدف من الشريحة:</strong> إثبات استقرار وموثوقية النظام باجتياز 62 اختباراً آلياً بنسبة نجاح 100% في مشروع SyncPulse.Tests.</p>"
    },
    {
        speaker: "فريق العمل الهندسي (4 متحدثين)",
        notes: "<p><strong>الهدف من الشريحة:</strong> إعلان الجاهزية لبدء العرض العملي الحي وربط الأجهزة الثلاثة عبر مركز المراقبة المباشر SyncPulse.Web.</p>"
    }
];

// Cockpit State
let currentModuleIndex = 0;
let isNotesOpen = false;
const themes = ["dark", "light", "matrix"];
let currentThemeIndex = 0;

/**
 * Initialize Cockpit
 */
document.addEventListener('DOMContentLoaded', () => {
    selectModule(0);
    setupKeyboardNavigation();
});

/**
 * Select and Activate a Module
 */
function selectModule(index) {
    if (index < 0 || index >= cockpitModules.length) return;
    currentModuleIndex = index;

    const moduleData = cockpitModules[index];

    // 1. Update Left Sidebar active item
    document.querySelectorAll('.nav-module-item').forEach((item, i) => {
        if (i === index) {
            item.classList.add('active');
            item.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        } else {
            item.classList.remove('active');
        }
    });

    // 2. Update Header & Stage Titles
    document.getElementById('module-counter-badge').innerText = `${(index + 1).toString().padStart(2, '0')} / 13`;
    document.getElementById('stage-module-tag').innerText = moduleData.tag;
    document.getElementById('stage-module-title').innerText = moduleData.title;
    document.getElementById('stage-module-desc').innerText = moduleData.desc;
    document.getElementById('stage-speaker-tag').innerText = moduleData.speaker;
    document.getElementById('active-speaker-name').innerText = moduleData.speaker;

    // 3. Switch Stage Content Panel
    document.querySelectorAll('.module-content-panel').forEach((panel, i) => {
        if (i === index) {
            panel.classList.add('active');
        } else {
            panel.classList.remove('active');
        }
    });

    // 4. Update Teleprompter Notes
    updateNotesContent(index);
}

function nextModule() {
    if (currentModuleIndex < cockpitModules.length - 1) {
        selectModule(currentModuleIndex + 1);
    }
}

function prevModule() {
    if (currentModuleIndex > 0) {
        selectModule(currentModuleIndex - 1);
    }
}

/**
 * Teleprompter Notes Drawer
 */
function updateNotesContent(index) {
    const notesBody = document.getElementById('cockpit-notes-body');
    if (!notesBody) return;

    const note = speakerNotes[index];
    if (note) {
        notesBody.innerHTML = `
            <div style="margin-bottom: 8px; color: var(--brand-primary); font-weight: 800; font-size: 0.86rem;">
                🎙️ ${note.speaker}
            </div>
            ${note.notes}
        `;
    }
}

function toggleNotes() {
    const drawer = document.getElementById('cockpit-notes-drawer');
    if (!drawer) return;
    isNotesOpen = !isNotesOpen;
    if (isNotesOpen) {
        drawer.classList.add('active');
    } else {
        drawer.classList.remove('active');
    }
}

/**
 * Theme Engine Cycle
 */
function cycleTheme() {
    currentThemeIndex = (currentThemeIndex + 1) % themes.length;
    const selectedTheme = themes[currentThemeIndex];
    document.documentElement.setAttribute('data-theme', selectedTheme);
}

/**
 * Toggle Fullscreen
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
function setupKeyboardNavigation() {
    document.addEventListener('keydown', (e) => {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

        switch (e.key) {
            case 'ArrowDown':
            case 'ArrowLeft':
            case ' ':
            case 'PageDown':
                e.preventDefault();
                nextModule();
                break;

            case 'ArrowUp':
            case 'ArrowRight':
            case 'PageUp':
                e.preventDefault();
                prevModule();
                break;

            case 'Home':
                e.preventDefault();
                selectModule(0);
                break;

            case 'End':
                e.preventDefault();
                selectModule(cockpitModules.length - 1);
                break;

            case 'n':
            case 'N':
            case 'ى':
                toggleNotes();
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
        }
    });
}
