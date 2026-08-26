# سجل التغييرات والتوثيق الهندسي للمشروع (CHANGELOG.md)
### نظام المراسلة والاتصالات المتقدم (SyncPulse / SecureTalk)
### وفق المعايير القياسية الدولية (ISO/IEC/IEEE 12207, ISO 25010, & APA 7th Edition)

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.3.0] - 2026-08-26

### 🎨 عرض الوسائط المتعددة بنمط تليجرام (Telegram-Style Rich Media Rendering)
- **Added:** دعم عرض الصور المباشرة (PNG, JPG, JPEG, BMP, GIF, WEBP) على طبيعتها مباشرة داخل فقاعة الرسائل (Inline Image Display) بزوايا دائرية أنيقة وإمكانية النقر للمعاينة بالحجم الكامل.
- **Added:** بطاقات المقاطع والرسائل الصوتية المدمجة (Telegram Audio Player Cards) مع زر تشغيل دائري أزرق `▶`، اسم المقطع، حجمه، وزر الحفظ `💾`.
- **Added:** بطاقات المستندات والملفات الذكية مع أيقونات ملونة مخصصة بحسب نوع الملف (📕 PDF، 📦 ZIP/RAR، 📄 DOCX/TXT، 🎬 MP4) مع أزرار الفتح والحفظ المباشر.
- **Added:** دعم التعليقات التوضيحية (Photo / Media Captions) بحيث تظهر الصورة أو الملف مدمجاً مع النص في نفس الفقاعة.

### 🎙️ محرك الصوت عالي النقاء والحماية من أخطاء الذاكرة (16 kHz Zero-Crash HD Voice Engine)
- **Changed:** ترقية محرك الصوت `AudioEngine.cs` إلى تردد **16000 Hz** (16-bit Mono, 1280B per frame) وفق معيار HD Voice الدولي، مما أزال أي تشويش أو روبوتية في الصوت.
- **Fixed:** القضاء التام على خطأ الذاكرة `0xc0000005` (Access Violation) من خلال ابتكار **هندسة المخازن الدائرية الثابتة (Zero-Allocation Pre-Pinned Ring Buffers)**، حيث يتم حجز وتثبيت ترويسات ومخازن `WAVEHDR` مرة واحدة فقط عند بدء المكالمة بدون أي حجز ديناميكي أثناء البث الحي.

### 📹 بث الفيديو المزدوج والمعاينة الذاتية (Picture-in-Picture PIP Camera Preview)
- **Added:** نافذة معاينة ذاتية حية لكاميرا المستخدم (Picture-in-Picture PIP) في زاوية الشاشة (`أنت 📷`) تعرض بث الكاميرا الحية محلياً مع إمكانية تصغيرها وتكبيرها.
- **Changed:** التحكم الفيزيائي المباشر في أجهزة الكاميرا عبر مكتبة `AForge.Video.DirectShow` مع إطفاء ضوء الكاميرا الحقيقي (LED) فورياً عند كتم الكاميرا وإعادة تشغيله عند التفعيل.
- **Fixed:** إغلاق التطبيق عند إنهاء المكالمة عبر ضبط `ShutdownMode="OnMainWindowClose"` صراحة في `App.xaml`.

### 🌐 بروتوكول البث الهجين (Hybrid Dual-Channel UDP + TCP Media Relay)
- **Added:** بث تدفقات الصوت والفيديو عبر قناتي UDP و TCP بالتوازي، مع إزالة التكرار اللحظي عبر مفتاح الإطار الثنائي `frameKey = ((long)mediaFrame.FrameType << 32) | mediaFrame.SequenceNumber` لضمان وصول الوسائط فورياً حتى في شبكات الواي فاي التي تحظر حزم UDP.

---

## [1.2.0] - 2026-08-25

### 1. النواة المشتركة والعقد الموحد (SyncPulse.Core)
- **Added:** بروتوكول التأطير الثنائي المعياري `12-Byte FrameHeader` بدعم Big-Endian Network Byte Order و Magic Byte `0x53` ('S') والتحقق الصارم من سقف الحمولة ($10\text{ MB}$).
- **Added:** نماذج الحزم والبيانات الموحدة (DTOs): `RegisterRequest/Response`, `LoginRequest/Response`, `ChatMessagePacket`, `MessageAckPacket`, `SyncHistoryRequest/Response`, `ContactSearchRequest/Response`, `AddContactRequest/Response`, `CallSignalPacket`, `MediaFramePacket`.
- **Added:** محرك التشفير الآمن `CryptoEngine` باستخدام خوارزمية PBKDF2/SHA-256 مع ملح عشوائي مشفر `128-bit Salt` ومقارنة زمنية ثابتة `FixedTimeEquals`.
- **Added:** محرك الرموز المميزة للجلسات `JwtTokenEngine` بتوقيع HMAC-SHA256 وفق معيار RFC 7519.
- **Added:** خدمة الاكتشاف التلقائي لشبكات الواي فاي `ServerDiscovery` عبر منفذ UDP Broadcast `8887`.

### 2. منظومة الخادم المركزية (SyncPulse.Server)
- **Added:** قاعدة بيانات SQLite 3NF متكاملة ومحسنة مع تفعيل وضع `WAL Mode`، مفاتيح الربط `Foreign Keys`، والفهارس الذكية على الجداول السبعة (`USERS`, `USER_CONTACTS`, `USER_SESSIONS`, `DIRECT_CONVERSATIONS`, `MESSAGES`, `CALL_RECORDS`, `SERVER_AUDIT_LOGS`).
- **Added:** مستودعات البيانات المعزولة: `UserRepository`, `ContactRepository`, `MessageRepository`, `CallRepository`, `AuditLogRepository`.
- **Added:** خادم مقابس TCP متقدم `TcpSocketServer` على المنفذ `8888` بنمط عزل الاستثناءات `Bulkhead Pattern`.
- **Added:** مكرر تدفقات الوسائط المباشرة للصوت والفيديو `UdpMediaRelay` على المنفذ `8889`.
- **Added:** منسق إشارات المكالمات وإدارتها وحساب مدتها `CallCoordinator`.
- **Added:** مدير الجلسات والبث الجماعي للتنبيهات `SessionManager`.
- **Added:** لوحة تحكم إدارية حديثة وعصرية بنظام اللونين الأزرق والأبيض النقي (Blue & Pure White SaaS Theme).

### 3. حزمة الاختبارات الآلية الشاملة (SyncPulse.Tests)
- **Added:** 62 اختبار وحدات وتكامل آلي تغطي كافة المكونات العشرة للنظام بنسبة نجاح 100% (0 أخطاء).

---

## المراجع الأكاديمية والهندسية (References - APA 7th Edition)

1. Bass, L., Clements, P., & Kazman, R. (2021). *Software Architecture in Practice* (4th ed.). Addison-Wesley Professional.
2. Fielding, R. T., & Taylor, R. N. (2002). Principled design of the modern Web architecture. *ACM Transactions on Internet Technology (TOIT)*, 2(2), 115–150.
3. Forouzan, B. A. (2012). *Data Communications and Networking* (5th ed.). McGraw-Hill Education.
4. International Organization for Standardization. (2011). *Systems and software engineering — Systems and software Quality Requirements and Evaluation (SQuaRE) — System and software quality models* (ISO/IEC 25010:2011). ISO.
5. International Organization for Standardization. (2017). *Systems and software engineering — Software life cycle processes* (ISO/IEC/IEEE 12207:2017). ISO.
6. Jones, M., Bradley, J., & Sakimura, N. (2015). *JSON Web Token (JWT)* (RFC 7519). Internet Engineering Task Force. https://doi.org/10.17487/RFC7519
7. Martin, R. C. (2017). *Clean Architecture: A Craftsman's Guide to Software Structure and Design*. Prentice Hall.
8. Microsoft Corporation. (2024). *Windows Multimedia Audio Functions and WAVEHDR Structure*. Microsoft Learn.
9. Postel, J. (1981). *Transmission Control Protocol* (RFC 793). Internet Engineering Task Force. https://doi.org/10.17487/RFC0793
10. Schulzrinne, H., Casner, S., Frederick, R., & Jacobson, V. (2003). *RTP: A Transport Protocol for Real-Time Applications* (RFC 3550). Internet Engineering Task Force. https://doi.org/10.17487/RFC3550
