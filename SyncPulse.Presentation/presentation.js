/**
 * ==============================================================================
 * SyncPulse Minimalist Executive Keynote Deck - JavaScript Controller
 * Slide Carousel Navigation, Speaker Cues & Notes Teleprompter
 * ==============================================================================
 */

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

let currentSlideIndex = 0;
let isNotesOpen = false;

document.addEventListener('DOMContentLoaded', () => {
    initDeck();
    setupKeyBindings();
});

function initDeck() {
    const slides = document.querySelectorAll('.keynote-slide');
    document.getElementById('tot-slides-num').innerText = slides.length.toString().padStart(2, '0');
    goToSlide(0);
}

function goToSlide(index) {
    const slides = document.querySelectorAll('.keynote-slide');
    if (index < 0 || index >= slides.length) return;

    slides.forEach((slide, i) => {
        if (i === index) {
            slide.classList.add('active');
        } else {
            slide.classList.remove('active');
        }
    });

    currentSlideIndex = index;

    // Update Counter
    document.getElementById('cur-slide-num').innerText = (index + 1).toString().padStart(2, '0');

    // Update Active Speaker Cue
    const activeSlide = slides[index];
    const speakerName = activeSlide.getAttribute('data-speaker') || 'فريق العمل الهندسي';
    document.getElementById('speaker-cue-text').innerHTML = `المتحدث الحالي: <strong>${speakerName}</strong>`;

    // Update Notes
    updateNotes(index);
}

function nextSlide() {
    const slides = document.querySelectorAll('.keynote-slide');
    if (currentSlideIndex < slides.length - 1) {
        goToSlide(currentSlideIndex + 1);
    }
}

function prevSlide() {
    if (currentSlideIndex > 0) {
        goToSlide(currentSlideIndex - 1);
    }
}

function updateNotes(index) {
    const notesContent = document.getElementById('notes-content');
    if (!notesContent) return;

    const note = speakerNotes[index];
    if (note) {
        notesContent.innerHTML = `
            <div style="margin-bottom: 8px; color: var(--primary); font-weight: 800; font-size: 0.88rem;">
                🎙️ ${note.speaker}
            </div>
            ${note.notes}
        `;
    }
}

function toggleNotes() {
    const drawer = document.getElementById('notes-drawer');
    if (!drawer) return;
    isNotesOpen = !isNotesOpen;
    if (isNotesOpen) {
        drawer.classList.add('active');
    } else {
        drawer.classList.remove('active');
    }
}

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

function setupKeyBindings() {
    document.addEventListener('keydown', (e) => {
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
                const slides = document.querySelectorAll('.keynote-slide');
                goToSlide(slides.length - 1);
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
        }
    });
}
