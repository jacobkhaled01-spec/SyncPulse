# 🚀 SyncPulse (SecureTalk)
### نظام المراسلة والاتصالات المتقدم للشبكات المحلية واللاسلكية (LAN & Wi-Fi)
### Enterprise Local Instant Messaging & 1-to-1 Voice/Video Calling System
### وفق المعايير القياسية الدولية (ISO/IEC/IEEE 12207, ISO 25010, & APA 7th Edition)

---

## 🌟 نظرة عامة (Overview)
**SyncPulse** هو نظام اتصالات ومراسلة محلي شامل فائق السرعة والأمان، مبني بمعمارية **Client-Server** مركزية مستوحاة من منصة **Telegram**. يدعم النظام كلاً من الشبكات السلكية (Ethernet LAN) والشبكات اللاسلكية المحلية (Local Wi-Fi / WLAN & Hotspots) مع ميزة **الاكتشاف التلقائي للخادم (Auto-Discovery)**، ونقل الملفات والوسائط بنمط Telegram المباشر، ومكالمات الصوت والفيديو الفورية بجودة HD عبر محرك صوت 16kHz محمي من أخطاء الذاكرة وبث فيديو مزدوج (PiP).

---

## 🏗️ مكونات النظام البرمجية (System Architecture)

1. **`SyncPulse.Core` (المكتبة المشتركة):**
   - بروتوكول تأطير الحزم المعياري (12-Byte Header / IETF TLV).
   - نماذج البيانات وحزم البروتوكول (DTOs & Enums).
   - محركات التشفير، تجزئة كلمات المرور مع Salt (PBKDF2/SHA-256)، ورموز JWT (RFC 7519).
   - خدمة الاكتشاف التلقائي لشبكات الواي فاي عبر كافة البطاقات والموجهات الفرعية (UDP Port 8887).

2. **`SyncPulse.Server` (تطبيق الخادم الإداري):**
   - واجهة إدارية حديثة (WPF Admin Dashboard) باللونين الأزرق والأبيض النقي لمراقبة المتصلين وحركة الحزم وموارد النظام.
   - محرك مقابس TCP متعدد الخيوط ومكرر وسائط المكالمات UDP Relay على المنفذ `8889`.
   - قاعدة بيانات مركزية مدمجة `SQLite` بتصميم 3NF مع فهارس استعلام ذكية وسجلات تدقيق كاملة.
   - محرك المزامنة التلغرامي وطوابير الرسائل المعلقة (Offline Queue).

3. **`SyncPulse.Client` (تطبيق العميل التفاعلي):**
   - واجهة مستخدم عصرية وسلسة بنمط WPF / MVVM باللونين الأزرق والأبيض النقي.
   - **عرض الوسائط المتعددة بنمط تليجرام:** عرض الصور المباشرة (Inline Images)، بطاقات الصوت التفاعلية، وبطاقات المستندات الذكية.
   - **مكالمات صوت وفيديو HD عالية النقاء:** محرك صوت 16 kHz بدون تشويش، محمي من أخطاء الذاكرة (Zero-Crash Ring Buffers)، مع ميزة المعاينة الذاتية في الزاوية (Picture-in-Picture PIP).
   - **التحكم الفيزيائي بالكاميرا:** إطفاء ضوء الكاميرا الحقيقي (LED) فورياً عند كتم الكاميرا وإعادة تشغيله عند التفعيل.
   - البحث الشامل باسم المستخدم (`@username`) وإدارة جهات الاتصال.
   - المراسلة الفردية المباشرة مع حالات التسليم والقراءة (✓ أُرسلت / ✓✓ استُلمت / ✓✓ زرقاء قُرئت).
   - مؤشر "جاري الكتابة..." اللحظي (`✍️ يكتب الآن...`).
   - درج سجل المكالمات التفاعلي (`Call History Drawer`).

4. **`SyncPulse.Tests` (مشروع الاختبارات الآلية المستقل):**
   - **62 اختبار وحدات وتكامل شامل** يغطي 10 وحدات هندسية بنسبة نجاح 100%.

---

## 🛠️ متطلبات التشغيل والبناء (Getting Started)

### المتطلبات الأساسية:
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download) أو أحدث.
- نظام تشغيل Windows 10 / 11 (لواجهات WPF).

### أوامر البناء والتشغيل:
```bash
# استنساخ المستودع
git clone https://github.com/jacobkhaled01-spec/SyncPulse.git
cd SyncPulse

# بناء الحل بالكامل
dotnet build SyncPulse.sln

# تشغيل حزمة الاختبارات الآلية (62 اختباراً)
dotnet run --project SyncPulse.Tests/SyncPulse.Tests.csproj
```

---

## 📚 المراجع الهندسية والأكاديمية (References - APA 7th Edition)

1. Bass, L., Clements, P., & Kazman, R. (2021). *Software Architecture in Practice* (4th ed.). Addison-Wesley Professional.
2. Forouzan, B. A. (2012). *Data Communications and Networking* (5th ed.). McGraw-Hill Education.
3. International Organization for Standardization. (2011). *Systems and software engineering — Systems and software Quality Requirements and Evaluation (SQuaRE) — System and software quality models* (ISO/IEC 25010:2011). ISO.
4. International Organization for Standardization. (2017). *Systems and software engineering — Software life cycle processes* (ISO/IEC/IEEE 12207:2017). ISO.
5. Jones, M., Bradley, J., & Sakimura, N. (2015). *JSON Web Token (JWT)* (RFC 7519). Internet Engineering Task Force. https://doi.org/10.17487/RFC7519
6. Martin, R. C. (2017). *Clean Architecture: A Craftsman's Guide to Software Structure and Design*. Prentice Hall.
7. Postel, J. (1981). *Transmission Control Protocol* (RFC 793). Internet Engineering Task Force. https://doi.org/10.17487/RFC0793
8. Schulzrinne, H., Casner, S., Frederick, R., & Jacobson, V. (2003). *RTP: A Transport Protocol for Real-Time Applications* (RFC 3550). Internet Engineering Task Force. https://doi.org/10.17487/RFC3550