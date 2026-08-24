# دليل المساهمة والتعاون لفريق العمل (CONTRIBUTING.md)

أهلاً بك في فريق تطوير مشروع **SecureTalk / SyncPulse**.

---

## 1. خطوات الإعداد الأولي للمطور (Developer Onboarding)

```bash
# 1. استنساخ المستودع
git clone https://github.com/jacobkhaled01-spec/SyncPulse.git
cd SyncPulse

# 2. جلب وتحديث الفروع
git fetch origin
git checkout develop

# 3. التحقق من بناء الحل واختبارات الوحدات
dotnet build SyncPulse.sln
dotnet run --project SyncPulse.Tests/SyncPulse.Tests.csproj
```

---

## 2. توزيع المهام والفروع

* **المطور (أ) - مهندس الخادم:**
  ```bash
  git checkout feature/dev-A-server
  ```
  *يعمل فقط في مشروع `SyncPulse.Server`.*

* **المطور (ب) - مهندس العميل:**
  ```bash
  git checkout feature/dev-B-client
  ```
  *يعمل فقط في مشروع `SyncPulse.Client`.*

---

## 3. إرشادات تقديم الـ Pull Requests

1. تأكد من أن كودك يُبنى بدون أي تحذيرات أو أخطاء (`0 Errors, 0 Warnings`).
2. تأكد من نجاح كافة الاختبارات في `SyncPulse.Tests`.
3. التزم بتسمية الالتزامات وفق معيار Conventional Commits (`feat:`, `fix:`, `refactor:`).
4. ارفع فرعك وافتح Pull Request لدمجه في `develop`.
