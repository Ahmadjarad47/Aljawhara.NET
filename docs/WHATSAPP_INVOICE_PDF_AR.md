# تكامل فواتير PDF مع واتساب

أصبح المرفق **PDF عربي قابل للبحث ومتعدد الصفحات**، وفق تعديل الطلب الأخير. يحتوي على رقم الطلب والفاتورة، اسم الزبون، وقت تأكيد الدفع بتوقيت الكويت، المنتجات والكميات والأسعار، الخصم والتوصيل والضريبة والإجمالي بدقة ثلاث منازل بالدينار الكويتي، وحالة «مدفوع بنجاح».

## نقطة الربط مع الدفع

في `PaymentOrchestrationService.FinalizeVerifiedCoreAsync`، بعد التحقق الصارم الحالي من Sadad وتحديث `PaymentAttempt` و`Transaction` إلى `Paid`، يُضاف سجل `WhatsAppInvoice` في **معاملة قاعدة البيانات نفسها**، قبل `SaveChangesAsync` و`CommitAsync`. لم تتغير شروط اعتماد الدفع أو منطق Sadad.

`WhatsAppInvoices` هو Outbox متخصص، وله قيد فريد على `PaymentAttemptId`. لا يظهر السجل للعامل الخلفي قبل نجاح الحفظ. تكرار تأكيد الدفع لا يضيف مهمة ثانية. العامل مستقل عن عامل الدفع ويعيد قراءة حالة الدفع ودليل التحقق والمبلغ والعملة وحالة الطلب؛ يستبعد الدفع عند الاستلام، الاسترداد، الطلبات غير المؤكدة، والأرقام غير الصالحة أو غير المصرح بها.

## الإعدادات

انسخ قسمَي `Gupshup` و`Gupshup_sandbox` من `Ecom.API/appsettings.WhatsApp.example.json` إلى `appsettings.json`. تم إعداد النسخة المحلية بالفعل، مع إبقاء الميزة معطلة إلى حين إكمال إعدادات الاتصال الخارجي.

```json
"Gupshup": { "Environment": "Sandbox" },
"Gupshup_sandbox": {
  "Enabled": false,
  "APIKey": "",
  "AppName": "whatsappaljawhara",
  "Source": "917834811114",
  "CountryCode": "965",
  "NationalNumberPattern": "[569][0-9]{7}",
  "PublicBaseUrl": "https://your-api.example.com",
  "WebhookSecret": "",
  "TemplateId": "",
  "SandboxSessionMessages": true,
  "SandboxAllowedDestinations": [],
  "MaxAttempts": 5,
  "MediaLifetimeMinutes": 30,
  "PdfRetentionDays": 7
}
```

- **كود الدولة**: `Gupshup_sandbox:CountryCode`، أرقام فقط دون `+` أو `00`. تتحول الأرقام المحلية و`+965…` و`00965…` إلى الصيغة الدولية دون علامة `+` التي تستخدمها Gupshup. يُرفض الرقم الدولي لدولة أخرى بدلاً من إعادة توجيهه بالخطأ.
- **قواعد الرقم المحلي**: `NationalNumberPattern`، وهي للكويت افتراضياً ثمانية أرقام تبدأ بـ5 أو6 أو9. عند تغيير الدولة، غيّر قواعد الرقم أيضاً. مثال ألمانيا: `CountryCode: "49"` و`NationalNumberPattern: "1[5-7][0-9]{8,9}"` لأرقام الجوال.
- **Source**: رقم المرسل المسجل بالتطبيق. رقم Sandbox في لقطة الشاشة هو `917834811114`؛ يُمنع استخدام هذا الرقم في وضع Production.
- **PublicBaseUrl**: عنوان HTTPS خارجي يصل إلى الـAPI، ويجب أن تستطيع Gupshup جلب المرفق منه.
- **WebhookSecret**: سر عشوائي قوي لا يقل عن32 حرفاً؛ يفضّل64 خانة سداسية ناتجة عن32 بايت عشوائية.
- **SandboxAllowedDestinations**: أرقام الاختبار المسموحة كاملة، مثل `965…` أو `49…`، دون `+`. مصدر رقم الإرسال الفعلي يبقى الطلب؛ هذه قائمة تقييد للاختبار.
- **APIKey**: لا تضع القيمة الحقيقية في Git. المفتاح الموجود محلياً نُقل إلى .NET User Secrets تحت `Gupshup_sandbox:APIKey`. تُحمّل User Secrets تلقائياً في Development؛ للخادم استخدم `Gupshup_sandbox__APIKey` أو `Gupshup__ApiKey` حسب البيئة.
- **PdfBrowserChannel**: اختياري؛ القيمة `msedge` لاستخدام Edge المثبت أو `chrome` لاستخدام Chrome. عند غيابه يستخدم Playwright نسخته من Chromium. النسخة المحلية مضبوطة على `msedge` وقد اختُبرت بها الفواتير.

لبيئة Production ضع `Environment: "Production"` وحقول الإعدادات في قسم `Gupshup` نفسه. عندئذ لا يُقرأ قسم Sandbox، ويصبح `TemplateId` إلزامياً وتُستخدم رسالة قالب من نوع Document.

### موافقة العميل

أُضيف حقل اختياري إلى طلب إنشاء الطلب:

```json
"whatsAppInvoiceOptIn": true
```

يجب أن يأتي من اختيار واضح للعميل في Checkout. قيمته الافتراضية `false`؛ الطلبات القديمة أو التي لا ترسل هذا الحقل لا تعتبر موافقة. يُحفظ وقت الموافقة ورقم الهاتف الفعلي المرتبط بعنوان الطلب. إذا تغير الرقم لاحقاً، لا تُرسل الفاتورة بناءً على موافقة الرقم السابق. بيانات ومبالغ الفاتورة تُقرأ من قاعدة البيانات حصراً.

## توليد PDF وتشغيله

المولد `InvoicePdfRenderer` يستخدم Chromium/Edge لطباعة HTML عربي RTL إلى PDF، مع تضمين خط Noto Sans Arabic المرخص وفق SIL OFL. التنسيق يدعم التفاف أسماء المنتجات، تكرار رؤوس الجدول، وترقيم الصفحات. أُخذ شعار المتجر من `front/Aljawhara/public/logo.png` الموجود محلياً، وأُضيفت نسخة مستقلة منه إلى `Ecom.Infrastructure/InvoiceAssets/logo.png` لتضمينه في PDF دون اعتماد على ملفات الواجهة أثناء التشغيل.

على خادم يستخدم Chromium المرفق، بعد Build أو Publish، شغّل سكربت Playwright الموجود بجوار ملفات التطبيق باستخدام حساب تشغيل الخدمة:

```powershell
$env:PLAYWRIGHT_SKIP_BROWSER_GC = '1'
pwsh Ecom.API/bin/Release/net10.0/playwright.ps1 install --with-deps chromium
```

اضبط المسار وفق مجلد النشر. على Windows يمكن استخدام Edge المثبت عبر `PdfBrowserChannel: "msedge"` بدلاً من تثبيت Chromium منفصل. لا تُنشر هذه التغييرات تلقائياً.

## حماية المرفق والإرسال الموثوق

- يُحفظ PDF في PostgreSQL بصيغة `bytea`، خارج `wwwroot`.
- يحتوي رابط التحميل على رمز عشوائي256 بت. قاعدة البيانات تحتفظ ببصمته فقط. تنتهي صلاحيته بعد30 دقيقة افتراضياً، والاستجابة `application/pdf` مع منع التخزين المؤقت. لا يوجد رابط عام دائم لبيانات العملاء.
- استُخدم رابط تنزيل مؤقت لأن توثيق Self Serve المتاح يوضح إرسال المستند بالرابط أو Media ID، دون عقد رفع موثق لهذا الحساب. لم يُخترع مسار رفع أو طلب حالة غير موثق. احجب معاملات `token` من سجلات أي Proxy عند تجهيز بيئة التشغيل؛ مراقبة التطبيق نفسها تسجل قالب المسار دون معاملات الرابط.
- الحد المحلي للمرفق10 ميغابايت. تُزال بايتات PDF بعد7 أيام افتراضياً عند انتهاء صلاحية الرابط، وتبقى حالة الإرسال ومعرّف الرسالة. تُزال أحداث التسليم بعد30 يوماً.
- الحجز الذري ومدة الحجز يمنعان عاملين من إرسال المهمة نفسها. توجد نقطة حفظ `Dispatching` قبل طلب HTTP، مع تحقق من ملكية الحجز قبل الإرسال.
- أخطاء تجهيز الملف ورفض429 يعادان بعد تأخير تصاعدي محدود بخمس محاولات. المهلة، انقطاع النقل،5xx، والرد غير المعروف تدخل `RequiresReconciliation` ولا تعاد عمياء؛ ربما قُبلت الرسالة فعلاً.
- حالات القبول والإرسال والتسليم والقراءة والفشل تُحفظ دون تغيير الدفع. فشل التسليم لا يسبب إعادة إرسال تلقائية.
- إذا كانت الميزة معطلة، تتوقف المعالجة وتبقى المهام محفوظة. عند التفعيل سيعيد العامل فحص أهلية كل طلب وموافقته قبل إرسال المهام المتراكمة.

## إعداد Webhook والتسوية

أضف في لوحة Gupshup رابط HTTPS:

```text
https://your-api.example.com/api/whatsapp/webhook?token=YOUR_WEBHOOK_SECRET
```

فعّل أحداث `enqueued`, `sent`, `delivered`, `read`, `failed`. تُفحص سرية الرابط واسم التطبيق، ويطابق التحديث رقم المستلم ومعرّف الرسالة. الأحداث متينة ومزالة التكرار؛ وصول حدث قبل رد الإرسال محفوظ ويعاد تطبيقه بعد حفظ `messageId`. لا تعيد الأحداث المتأخرة حالة Delivered أو Read إلى حالة أقدم. الأحداث التي تفتقد `gsId` للتسليم/القراءة لا تُربط بتخمين؛ يحتفظ النظام بالحالة السابقة.

واجهات الإدارة تتطلب JWT بصلاحية Admin:

- `GET /api/whatsapp/admin/invoices`: أحدث100 مهمة دون المرفقات أو أرقام العملاء.
- `POST /api/whatsapp/admin/invoices/{id}/reconcile`: تسوية نتيجة مجهولة بعد فحص سجل Gupshup. أرسل `evidence` مع `providerMessageId` لإثبات قبول الرسالة، **أو** `confirmedNotAccepted: true` لإثبات عدم قبولها والسماح بمحاولة ضمن الحد المتبقي. لا تستخدم الخيار الثاني لمجرد غياب إشعار التسليم. يبقى سبب التسوية وهوية المسؤول في السجل.

لا يفترض هذا الـWebhook توقيع HMAC غير موثق من Gupshup؛ يعتمد سر رابط قوي عبر HTTPS. تفاصيل الربط موجودة في [توثيق Webhook](https://docs.gupshup.io/docs/set-webhookcallback-url) و[أحداث الرسائل](https://docs.gupshup.io/docs/message-events).

## قالب الإنتاج

في Gupshup أنشئ `payment_invoice` باللغة العربية، فئة **Utility**، وبرأس **Document/PDF** وفق الطلب المعدل، ثم أرفق PDF تجريبياً خالياً من بيانات العملاء كعينة للاعتماد. نص مقترح:

> مرحباً، تم استلام دفعتك بنجاح للطلب رقم {{1}} من الجوهرة بلس. مرفق فاتورتك بصيغة PDF. شكراً لتسوقك معنا.

بعد الاعتماد، انسخ معرّف القالب الفعلي إلى `Gupshup:TemplateId`، واستخدم رقم WhatsApp Business الإنتاجي. الكود يرسل `template.params` برقم الطلب و`message.type: document` مع `document.link` و`filename`؛ راجع [إرسال قالب مستند](https://docs.gupshup.io/reference/sending-document-template).

## تجربة Sandbox

1. استخدم طلباً تجريبياً ورقمك المصرح به، واضبط كود الدولة وقواعد الرقم وقائمة `SandboxAllowedDestinations`.
2. اربط رقم الاختبار بالتطبيق من لوحة Sandbox، وافتح محادثة واتساب مع Sandbox وفق تعليمات التطبيق. رسائل الجلسة تحتاج نافذة محادثة فعالة.
3. اضبط عنوان HTTPS وسر Webhook، ثم `Enabled: true`. المفتاح يُحمّل من User Secrets في Development أو متغيرات البيئة.
4. أنشئ الطلب مع `whatsAppInvoiceOptIn: true`. أبقِ إعدادات تحقق Sadad الحالية وشروط اعتماد Paid كما هي؛ لا تفعل أعلام الاعتماد لمجرد تجربة واتساب.
5. عند تأكيد Sadad الموثوق، يظهر سجل Outbox ثم `ProviderMessageId`، ويصل PDF إلى هاتف الطلب. في Sandbox الطلب هو `type: file` مع `url` و`filename` كما في لقطة المستخدم؛ راجع [إرسال مستند ضمن جلسة](https://docs.gupshup.io/reference/post_wa-api-v1-msg-5).
6. تابع Delivered/Read من واجهة الإدارة. لا تختبر على أرقام عملاء الإنتاج.

لم تُرسل رسالة فعلية خلال التنفيذ: عنوان HTTPS القابل للوصول وقائمة أرقام الاختبار وسر Webhook غير مهيأة. اختبارات HTTP استخدمت مزوداً وهمياً، واختبارات تأكيد Sadad استخدمت الـMock الموجود، دون الاتصالات بالعملاء.

## قاعدة البيانات والملفات

استُعيدت ترحيلات PostgreSQL الثلاثة الأصلية من الإصدار `061f48f` لأنها محذوفة من HEAD الحالي، مع الحفاظ على أسمائها ومعرفاتها الأصلية. الترحيل الجديد `WhatsAppInvoicePdfOutbox` يضيف جدولَي الفواتير وأحداث التسليم وحقلي الموافقة فقط؛ لا يعيد إنشاء جداول الدفع. اختُبر مسار الترحيل من سجل الشحن القديم إلى النموذج الحالي. لا تطبق أرشيف SQL Server الموجود في `Migrations` على PostgreSQL.

الملفات الرئيسية:

| الطبقة | الملفات |
| --- | --- |
| Domain | `Entity/WhatsAppInvoice.cs`, `Interfaces/IWhatsAppInvoiceServices.cs`, وتحديث `Entity/Order.cs` |
| Infrastructure | `WhatsApp/GupshupOptions.cs`, `WhatsApp/GupshupWhatsAppService.cs`, `WhatsApp/InvoicePdfRenderer.cs`, `InvoiceAssets/`، وتحديث `EcomDbContext` وملف المشروع، وترحيلات `PostgreSqlMigrations/` |
| Application | `WhatsApp/WhatsAppInvoiceProcessor.cs`، وربط الدفع في `PaymentOrchestrationService`، وحفظ الموافقة في `OrderService`, `OrderDto`, `MappingProfile` |
| API | `Controllers/WhatsAppController.cs`, `HostedServices/WhatsAppInvoiceWorker.cs`, `Program.cs`, `appsettings.WhatsApp.example.json` وتحديث الإعدادات المحلية |
| Tests | `WhatsAppInvoiceTests.cs`، وتوسيع `Phase2OrchestrationTests.cs` و`HttpSecurityTests.cs` |

## التحقق

تفاصيل النتائج النهائية محفوظة في `artifacts/whatsapp/tests/`. اختُبر PDF حقيقياً على Edge، ورُوجعت صفحات النماذج بصرياً بعد تحويلها إلى PNG. توجد نماذج ببيانات اصطناعية في `artifacts/whatsapp/samples/`.

- Build ناجح. توجد تحذيرات Nullable سابقة في المستودعات والبريد والمصادقة.
- اختبارات واتساب وحماية واجهاته: **33 ناجحة، دون فشل**.
- مجموعة الدفع/Checkout الكاملة قبل إضافة آخر3 اختبارات: **195 ناجحة من196**؛ الفشل الوحيد في `ProductionSeedingTests.Production_does_not_resolve_database_or_create_demo_data` لأن `DataSeeder.SeedAsync` يحاول حل `EcomDbContext` في Production قبل الحماية المتوقعة. هذا الكود سابق ولم يتغير ضمن التكامل.
- اختبارات ترحيلات الشحن والنسخ الاحتياطي ومرحلة الدفع الثانية: ناجحة بعد استعادة الترحيلات الأصلية وضبط `ECOM_TEST_PG_BIN`.
- الفحص المستقل `tests/Checkout.PaymentChecks`: ناجح للدفع عند الاستلام دون اتصال بالبوابة، وللدفع الإلكتروني عبر Sadad وهمي.
- تحقق EF من عدم وجود اختلاف بين النموذج وآخر Migration: ناجح، وتوليد SQL idempotent: ناجح.
- المولد ينتج نموذجاً من صفحة واحدة وآخر من5 صفحات لـ60 منتجاً، مع إجمالي `1482.400 KWD` في النموذج الطويل.

تشغيل الاختبارات يتطلب PostgreSQL اختبارياً يملك الدور فيه `CREATEDB`:

```powershell
$env:ECOM_TEST_POSTGRES = 'Host=127.0.0.1;Port=55438;Database=postgres;Username=test_admin'
$env:ECOM_TEST_PG_BIN = 'C:\Program Files\PostgreSQL\18\bin'
$env:ECOM_WHATSAPP_BROWSER_CHANNEL = 'msedge'
dotnet test Ecom.taani.slnx
```

تُنشئ الاختبارات قواعد مؤقتة بأسماء فريدة وتحذفها؛ لا تستخدم قاعدة الإنتاج. `ECOM_WHATSAPP_SAMPLE_DIR` اختياري لتصدير نماذج PDF من اختبار المولد.
