# الدليل المعماري والتقني السريع (ARCHITECTURE.md)

## 1. المعمارية العامة (High-Level Architecture)

النظام مبني بنمط **Centralized Client-Server** محلي بالكامل يدعم الشبكات السلكية واللاسلكية:

```text
[Client WPF GUI]  <--- TCP: 8888 (Control/Messages/Signaling) --->  [Server WPF GUI & Core]
[Client WPF GUI]  <--- UDP: 8889 (Voice/Video Media Relay)    --->  [Server WPF GUI & Core]
[Client UDP Auto] <--- UDP: 8887 (Wi-Fi Auto-Discovery)       <---  [Server UDP Broadcaster]
```

---

## 2. تركيبة ترويسة الحزم (12-Byte FrameHeader Layout)

```text
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|  Magic (0x53) | Version (0x01)|      PacketType (16-bit)      |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                      PayloadLength (32-bit)                   |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                     SequenceNumber (32-bit)                   |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
```

---

## 3. قاعدة البيانات المركزية (SQLite 3NF Schema)

* `USERS`: المستخدمين، كلمات المرور المشفرة بـ PBKDF2/SHA-256، والـ Salt.
* `USER_CONTACTS`: جهات الاتصال المحفوظة والأسماء المخصصة.
* `USER_SESSIONS`: الجلسات النشطة، عناوين الـ IP، وتوكنات الـ JWT.
* `DIRECT_CONVERSATIONS`: المحادثات الثنائية المباشرة (User1_ID, User2_ID).
* `MESSAGES`: سجل الرسائل، المرفقات، وحالات التسليم (0: Sent ✓, 1: Delivered ✓✓, 2: Read).
* `CALL_RECORDS`: سجل المكالمات الصوتية والمرئية، مدة المكالمة بالثواني، وحالة الإنهاء.
* `SERVER_AUDIT_LOGS`: سجلات تدقيق وأحداث الخادم اللحظية.
