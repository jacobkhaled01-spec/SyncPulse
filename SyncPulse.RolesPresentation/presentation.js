/**
 * ==========================================================================
 * SyncPulse Roles Presentation Engine
 * Keynote Controller, Slide Navigation, Speaker Jumps, and Notes Drawer
 * Designed for Public Defense Display & Executive Screen Projection
 * ==========================================================================
 */

document.addEventListener('DOMContentLoaded', () => {
    const slides = document.querySelectorAll('.slide');
    const currentSlideEl = document.getElementById('currentSlide');
    const totalSlidesEl = document.getElementById('totalSlides');
    const prevBtn = document.getElementById('prevBtn');
    const nextBtn = document.getElementById('nextBtn');
    const notesDrawer = document.getElementById('notesDrawer');
    const notesContent = document.getElementById('notesContent');
    const toggleNotesBtn = document.getElementById('toggleNotesBtn');
    const closeNotesBtn = document.getElementById('closeNotesBtn');
    const jumpBtns = document.querySelectorAll('.jump-btn');

    let currentSlide = 0;
    const totalSlides = slides.length;
    totalSlidesEl.textContent = totalSlides.toString().padStart(2, '0');

    // Formal Speaker Notes Content (Indexed by Slide 0 to 9)
    const slideNotes = [
        // Slide 1: Formal Opening
        "<strong>المقدمة الرسمية المشتركة (المتحدث الأول):</strong><br>• الترحيب برئيس وأعضاء لجنة التحكيم وأستاذ المقرر والحضور.<br>• إعلان عنوان المشروع: 'SyncPulse - نظام المراسلة والاتصالات المتقدم للشبكات المحلية'.<br>• توضيح الركيزة الأساسية: المشروع مبني بالكامل من الصفر فوق المقابس الخام (Raw TCP/UDP Sockets) في بيئة C# .NET 9 دون الاعتماد على أي وسائط أو سحابة خارجية.<br>• الإشارة إلى توزيع محاور العرض على المهندسين الأربعة بالتكامل والتسلسل المنطقي.",

        // Slide 2: Architecture & Responsibilities
        "<strong>استعراض الهيكل المعماري (الفريق):</strong><br>• توضيح تطبيق معايير Clean Architecture والعزل المادي التام للمسؤوليات (Workspace Isolation).<br>• استعراض طبقات المشروع الأربعة: النواة المشتركة (Core)، خادم المقابس (Server)، عميل سطح المكتب (Client)، وحزمة التحقق (Tests).<br>• التأكيد على أن هذا التقسيم ألغى التعارضات البرمجية بنسبة 100% ومكّن كل مهندس من قيادة محوره بتخصص كامل.",

        // Slide 3: Speaker 1 Deep Dive
        "<strong>نص إلقاء المتحدث الأول (هندسة المقابس والتأطير):</strong><br>• <em>الوقت المخصص: 3 إلى 4 دقائق.</em><br>• <strong>البداية:</strong> 'بسم الله، أتناول في المحور الأول البنية التحتية للشبكة: كيف صممنا اتصالاً محلياً 100% On-Premise LAN.'<br>• <strong>النقاط الجوهرية:</strong> دورة حياة المقابس (Socket, Bind, Listen, AcceptAsync)، معضلة دفق TCP وحلها عبر ترويسة 12-Byte FrameHeader مع Magic Byte (0x53) وترتيب Big-Endian لمنع اختلاف المعالجات، وخدمة الاكتشاف التلقائي UDP Broadcast 8887.<br>• <strong>التسليم:</strong> 'وبعد ضمان نقل البايتات مؤطرة وسليمة، أنقل الكلمة لزميلي المهندس [المتحدث 2] لشرح منظومة الأمان والتشفير.'",

        // Slide 4: Speaker 2 Deep Dive
        "<strong>نص إلقاء المتحدث الثاني (الأمن السيبراني والمصادقة والجلسات):</strong><br>• <em>الوقت المخصص: 3 إلى 4 دقائق.</em><br>• <strong>البداية:</strong> 'شكراً زميلي. في المحور الثاني، قمنا بتحصين النظام وفق أعلى المعايير القياسية العالمية (NIST & RFC 7519).'<br>• <strong>النقاط الجوهرية:</strong> تجزئة كلمات المرور بـ PBKDF2/SHA-256 مع 100k تكرار و 128-bit Salt، حماية هجمات التوقيت بمقارنة البايتات في زمن ثابت عبر FixedTimeEquals، توكنات JWT الموقعة بـ HMAC-SHA256 والتحقق منها في الذاكرة دون لمس قاعدة البيانات، وحظر حقن SQL بـ Parameterized Queries وتدقيق ISO 27001.<br>• <strong>التسليم:</strong> 'والآن مع زميلي المهندس [المتحدث 3] لشرح الخادم وقاعدة البيانات.'",

        // Slide 5: Speaker 3 Deep Dive
        "<strong>نص إلقاء المتحدث الثالث (خادم TCP وإدارة التزامن وقواعد البيانات):</strong><br>• <em>الوقت المخصص: 3 إلى 4 دقائق.</em><br>• <strong>البداية:</strong> 'شكراً زميلي. يمثل خادم المقابس المركزي TcpSocketServer عصب النظام الذي يستقبل مئات الاتصالات المتزامنة.'<br>• <strong>النقاط الجوهرية:</strong> نموذج IOCP Async غير الحاجب على المنفذ 8888، نمط الحواجز Bulkhead Pattern لعزل استثناءات العملاء، قاعدة بيانات SQLite 3NF بتفعيل وضع WAL Mode للسماح بالقراءات المتزامنة أثناء الكتابة، ومحرك المزامنة التلغرامي Offline Queue مع حالات التسليم الثلاث (✓ / ✓✓ / ✓✓ زرقاء).<br>• <strong>التسليم:</strong> 'والآن مع زميلي المهندس [المتحدث 4] لشرح وسائط ومكالمات UDP.'",

        // Slide 6: Speaker 4 Deep Dive
        "<strong>نص إلقاء المتحدث الرابع (وسائط UDP وإدارة الذاكرة والبث المباشر):</strong><br>• <em>الوقت المخصص: 3 إلى 4 دقائق.</em><br>• <strong>البداية:</strong> 'شكراً زميلي. أختتم المحاور الهندسية بالتحدي الأكثر تعقيداً: الاتصال الصوتي والمرئي بالزمن الحقيقي والتعامل المباشر مع عتاد الحاسوب.'<br>• <strong>النقاط الجوهرية:</strong> مكرر وسائط UdpMediaRelay على المنفذ 8889 بنمط Stateless Forwarding بزمن تأخير أقل من 10ms، محرك الصوت 16kHz HD وحل خطأ الذاكرة 0xc0000005 بنظام 8 مخازن دائرية مثبتة بـ GCHandle Pinned، محرك الفيديو المباشر AForge DirectShow وإطفاء ضوء الكاميرا الحقيقي LED عتادياً عند الكتم عبر SignalToStop، والبث الهجين بمفتاح 64-bit Timestamp.<br>• <strong>التسليم:</strong> 'والآن ننتقل للعرض العملي الحي للأجهزة الثلاثة.'",

        // Slide 7: Live 3-Device Demo Playbook
        "<strong>إدارة سيناريو العرض العملي الحي (Playbook):</strong><br>• <strong>المتحدث 1:</strong> يقف عند اللابتوب 1 ويشغل السيرفر، ثم يظهر التقاط اللابتوب 2 لعنوان IP الخادم تلقائياً عبر UDP Broadcast 8887.<br>• <strong>المتحدث 2:</strong> يسجل دخول المستخدمين ويشير لشاشة السيرفر لإظهار عداد المتصلين Active Clients: 2 وتوكنات JWT.<br>• <strong>المتحدث 3:</strong> يرسل رسائل شات ويستعرض علامات الصح الثلاث، ثم يغلق لابتوب العميل B ويرسل رسائل معلقة، ثم يعيد فتحه لإثبات استلامها فورياً من الـ Offline Queue.<br>• <strong>المتحدث 4:</strong> يبدأ مكالمة فيديو مباشرة ويظهر نقاء الصوت 16kHz، ثم يضغط زر كتم الكاميرا ويوجه أنظار اللجنة لانطفاء ضوء الـ LED الحقيقي على اللابتوب.",

        // Slide 8: Cross-Domain Integration Contracts
        "<strong>التكامل وتدفق البيانات بين الطبقات:</strong><br>• شرح مسار الحزمة: النواة (تأطير 12-Byte) ➔ الأمان (فحص JWT) ➔ السيرفر (حفظ SQLite WAL وتوجيه الجلسة) ➔ الوسائط (تحويل تدفقات UDP 8889).<br>• التأكيد على العقد البرمجي الموحد وسياسة عدم الانهيار Zero-Crash عبر async/await والتحرير الحتمي للموارد.",

        // Slide 9: Automated Verification Suite
        "<strong>حزمة التحقق والاختبارات الآلية (SyncPulse.Tests):</strong><br>• الإشارة إلى التقرير الرقمي: 62 اختباراً آلياً شاملاً لكافة الطبقات العشرة للنظام بنسبة نجاح 100%.<br>• التأكيد على اجتياز اختبارات التكامل الحقيقية End-to-End بمقابس فعلية في زمن قياسي قدره 2.14 ثانية.",

        // Slide 10: Formal Conclusion & Q&A
        "<strong>الخاتمة وبدء جلسة المناقشة:</strong><br>• توجيه أسمى عبارات الشكر والامتنان لأستاذ المقرر ولجنة التحكيم الكريمة.<br>• إعلان الجاهزية التامة للإجابة على كافة الأسئلة والاستفسارات البرمجية والهندسية."
    ];

    function updateSlide(index) {
        if (index < 0 || index >= totalSlides) return;
        
        slides[currentSlide].classList.remove('active');
        currentSlide = index;
        slides[currentSlide].classList.add('active');
        
        currentSlideEl.textContent = (currentSlide + 1).toString().padStart(2, '0');
        
        // Update Notes Content
        if (slideNotes[currentSlide]) {
            notesContent.innerHTML = slideNotes[currentSlide];
        }

        // Update Jump Buttons Active State
        jumpBtns.forEach(btn => {
            const targetSlide = parseInt(btn.getAttribute('data-slide'));
            btn.classList.remove('active-s1', 'active-s2', 'active-s3', 'active-s4', 'active-all');
            if (targetSlide === currentSlide) {
                const spClass = btn.getAttribute('data-speaker');
                btn.classList.add(`active-${spClass}`);
            }
        });
    }

    prevBtn.addEventListener('click', () => updateSlide(currentSlide - 1));
    nextBtn.addEventListener('click', () => updateSlide(currentSlide + 1));

    // Jump Buttons
    jumpBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            const slideIdx = parseInt(btn.getAttribute('data-slide'));
            updateSlide(slideIdx);
        });
    });

    // Notes Drawer
    function toggleNotes() {
        notesDrawer.classList.toggle('open');
    }
    toggleNotesBtn.addEventListener('click', toggleNotes);
    closeNotesBtn.addEventListener('click', () => notesDrawer.classList.remove('open'));

    // Keyboard Navigation for Presenter
    document.addEventListener('keydown', (e) => {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

        switch (e.key) {
            case 'ArrowRight':
            case 'PageDown':
            case ' ':
                e.preventDefault();
                updateSlide(currentSlide + 1);
                break;
            case 'ArrowLeft':
            case 'PageUp':
                e.preventDefault();
                updateSlide(currentSlide - 1);
                break;
            case 'Home':
                e.preventDefault();
                updateSlide(0);
                break;
            case 'End':
                e.preventDefault();
                updateSlide(totalSlides - 1);
                break;
            case 'n':
            case 'N':
            case 'ى':
                e.preventDefault();
                toggleNotes();
                break;
            case 'f':
            case 'F':
            case 'ب':
                e.preventDefault();
                if (!document.fullscreenElement) {
                    document.documentElement.requestFullscreen().catch(() => {});
                } else {
                    document.exitFullscreen().catch(() => {});
                }
                break;
            // Quick Numbers for Slides: 1 -> Domain 1, 2 -> Domain 2, etc.
            case '1':
                updateSlide(2); // Slide 3: Speaker 1
                break;
            case '2':
                updateSlide(3); // Slide 4: Speaker 2
                break;
            case '3':
                updateSlide(4); // Slide 5: Speaker 3
                break;
            case '4':
                updateSlide(5); // Slide 6: Speaker 4
                break;
        }
    });

    // Initialize first view
    updateSlide(0);
});
