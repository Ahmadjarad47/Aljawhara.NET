# الأرقام الدولية وموافقة واتساب — 21 أكتوبر 2026

نُفذ التكامل مع الواجهة المحلية في `D:\FreeLance\Ecom.taani\front\Aljawhara`. تقرير التنفيذ والعقود ونتائج الفحص الكامل: `WHATSAPP_CONSENT_IMPLEMENTATION_AR.md` في جذر الواجهة.

## التغييرات الخلفية

- `Ecom.Infrastructure/WhatsApp/GupshupOptions.cs`: استخدام libphonenumber-csharp 9.0.41، الكويت افتراضياً، دعم + و00 والدولة المختارة في دالة التطبيع، رفض الأرقام غير الصالحة، E.164 داخلياً، وتطبيع قائمة Sandbox دون توسيعها. نمط الأرقام الكويتي القديم لم يعد قيداً عاماً.
- `Ecom.Infrastructure/WhatsApp/GupshupWhatsAppService.cs`: وجهة داخليّة E.164؛ إرسال الأرقام الرقمية فقط إلى Gupshup للفواتير ورموز التحقق، والتحقق من قائمة Sandbox في المسارين.
- `Ecom.Application/Services/OrderService.cs`: تطبيع رقم الطلب قبل الحفظ ومقارنة الموافقة بنفس الدالة. حفظ الوقت والرقم والمصدر والتدقيق مشروط بالموافقة الصريحة وفق العقد الموجود.
- `Ecom.Application/Services/AddressService.cs`: تطبيع أرقام العناوين الجديدة والمعدلة، بما فيها المسارات القديمة.
- `Ecom.Application/Services/UserManagerService.cs`: إزالة رسالة الخطأ المحصورة بالكويت؛ مسار تحديث الهاتف يستخدم المطبع الدولي نفسه ويلغي الموافقة والتحقق السابقين.
- `Ecom.Application/WhatsApp/WhatsAppConsentService.cs`: التحقق الدولي وإلغاء الموافقة القديمة عند بدء تحقق رقم مختلف. تأكيد الرمز لا يمنح موافقة جديدة.
- `Ecom.Application/WhatsApp/WhatsAppInvoiceProcessor.cs`: تطبيع الموافقة والمستلم ومطابقة Sandbox؛ قواعد الدفع ومنع التكرار محفوظة.
- `Ecom.API/Controllers/WhatsAppController.cs`: تحويل وجهة أحداث Gupshup إلى E.164 قبل تخزينها ومطابقتها.
- `Ecom.API/appsettings.WhatsApp.example.json`: إزالة نمط الكويت القديم من المثال؛ CountryCode يبقى قيمة افتراضية فقط.
- `Ecom.Infrastructure/Ecom.Infrastructure.csproj`: المكتبة الدولية الجديدة.
- `Ecom.Infrastructure/Data/EcomDbContext.cs` و`PostgreSqlMigrations/EcomDbContextModelSnapshot.cs` وملفا الترحيل `20261021121752_InternationalWhatsAppPhones`: 16 محرفاً للحقول المحدودة، وترحيل الأرقام الدولية الرقمية الموجودة إلى E.164 مع إبقاء الموافقات المفقودة كما هي.
- اختبارات `WhatsAppInvoiceTests.cs`, `WhatsAppConsentIntegrationTests.cs` والملف الجديد `WhatsAppPhoneMigrationTests.cs` تغطي ألمانيا والكويت والتطبيع والموافقة والتحقق والإلغاء والمطابقة وSandbox ومنع التكرار والترحيل. استُبدلت أرقام وهمية غير صالحة بأرقام صالحة في `ShippingTests.cs` و`HttpSecurityTests.cs` لتوافق التحقق الجديد دون تغيير توقعات قواعد الدفع.

## العقود والتشغيل

لا تغيير في أسماء مسارات API أو حقول DTO. الواجهة تحوّل الرقم المحلي بحسب الدولة المختارة إلى E.164 قبل الإرسال. أصبح `phoneNumber` في تفضيلات واتساب والأرقام المخزنة يبدأ بـ`+`، بينما تبقى `destination` المرسلة إلى Gupshup رقمية فقط.

ترحيل الإنتاج مطلوب قبل تشغيل العامل بالإصدار الجديد. أوقف العامل أثناء النشر والترحيل ثم أعد تشغيله. `CountryCode=965` لا يمنع ألمانيا أو أي وجهة دولية صالحة. `SandboxAllowedDestinations` يظل قيداً إلزامياً، سواء كتب الرقم بـ`+` أو بصيغته الدولية الرقمية القديمة.

لم تُضف موافقة للطلب 47، ولم تُعد إرساله، ولم يُجر اتصال بقاعدة إنتاج لفحصه. السبب المثبت في الواجهة السابقة هو غياب حقلي الموافقة من مسار إنشاء الطلب. المسار الموجود `POST /api/orders/{orderId}/whatsapp-invoice/retry` متصل الآن بواجهة تتطلب موافقة جديدة من العميل. يحفظ المسار المالك/توكن الضيف والمطابقة ومنع التكرار وحالة الفاتورة المؤهلة، ثم يعيد المعالج فحص الدفع قبل الإرسال. استجابة 202 تعني الإدراج فقط.

## التحقق

- Release build: ناجح، 0 أخطاء و12 تحذيراً سابقاً.
- 52 اختبار واتساب ناجحاً، ضمن الفحص الكامل: 217 ناجحة / 218.
- الفشل الوحيد: `ProductionSeedingTests.Production_does_not_resolve_database_or_create_demo_data`؛ مشكلة سابقة في DataSeeder موثقة سابقاً في `docs/WHATSAPP_INVOICE_PDF_AR.md`.
- EF لا يرى اختلافاً بين النموذج وآخر ترحيل؛ توليد SQL idempotent ناجح في `artifacts/whatsapp-international-migration.sql`.
- كل إرسال في الاختبارات Mock؛ PostgreSQL مستقل مؤقت. لم تتغير قواعد تحقق Sadad.

تشغيل اختبارات الخادم على PostgreSQL اختباري مستقل يملك صلاحية CREATEDB:

```powershell
$env:ECOM_TEST_POSTGRES = 'Host=127.0.0.1;Port=55439;Database=postgres;Username=test_admin'
$env:ECOM_TEST_PG_BIN = 'C:\Program Files\PostgreSQL\18\bin'
$env:ECOM_WHATSAPP_BROWSER_CHANNEL = 'msedge'
dotnet test tests/Ecom.CheckoutAudit.Tests
```

لتشغيل حالات واتساب فقط، أضف `--filter 'FullyQualifiedName~WhatsApp'`. السجلات الكاملة في `artifacts/whatsapp-full-tests.log` و`artifacts/whatsapp-release-build.log` ونتائج TRX في مجلد اختبارات المشروع.
