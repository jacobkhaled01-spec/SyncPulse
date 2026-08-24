# سجل التغييرات والتوثيق الهندسي للمشروع (CHANGELOG.md)
### نظام المراسلة والاتصالات المتقدم (SyncPulse / SecureTalk)
### وفق المعايير القياسية (ISO/IEC/IEEE 12207, ISO 25010, & APA 7th Edition)

All notable changes to this project will be documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased] - 2026-08-24

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
- **Added:** لوحة تحكم إدارية حديثة وعصرية بنظام اللونين الأزرق والأبيض النقي (Blue & Pure White SaaS Theme) بـ 4 تبويبات شاملة:
  1. *المراقبة اللحظية وبث إعلانات النظام العامة.*
  2. *إدارة الحسابات والمستخدمين والحظر وفك الحظر وتصفير كلمات المرور.*
  3. *سجل المكالمات وتاريخ الاتصالات.*
  4. *سجلات التدقيق والأمان المتجاوبة تلقائياً مع حجم النصوص وشارات مستويات الأحداث الملونة.*

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
8. Postel, J. (1981). *Transmission Control Protocol* (RFC 793). Internet Engineering Task Force. https://doi.org/10.17487/RFC0793
