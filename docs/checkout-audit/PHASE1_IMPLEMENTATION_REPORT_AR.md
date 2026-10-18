# تقرير تنفيذ المرحلة الأولى — إصلاحات Checkout والدفع الحرجة

التاريخ: 2026-10-18

## القرار النهائي

**P0 PARTIAL — وقرار الإطلاق ما زال NO-GO.**

أُغلقت مسارات الاحتيال وIDOR وفساد الكمية/المخزون المشمولة أدناه، لكن لا يمكن إعلان P0 COMPLETE لأن عقد Sadad الرسمي المتاح لا يعرّف حقول استجابة موثوقة تسمح بإثبات الحالة النهائية والمبلغ والعملة والمرجع وهوية التاجر. لذلك بقي تأكيد الدفع الإلكتروني **fail-closed** ولا يستطيع تحويل أي عملية إلى `Paid` حالياً. كذلك لم تُنفّذ عملية تدوير الأسرار الحية لأنها تحتاج تفويض نشر.

## الثغرات التي عولجت

| SF | قبل | بعد |
|---|---|---|
| SF-01 | endpoint عام ينشئ `Paid` من مبلغ وحالة يرسلهما العميل | `POST /api/orders/{id}/payment` يعيد 410 دائماً، والخدمة القديمة ترفض التنفيذ بلا أي حفظ |
| SF-02 / SF-03 / SF-09 | رقم/معرف الطلب يكفي لكشف الطلب والمعاملات والفاتورة، وأي مستخدم يستطيع لمس طلب ضيف | capability عشوائي بطول 256-bit ومحدد بطلب واحد؛ لا يُخزن إلا SHA-256؛ انتهاء 30 يوماً؛ تدوير عند استعادة رابط الدفع؛ إلغاء الطلب يلغي capability؛ المالك/admin أو capability الصحيح فقط |
| SF-04 | نتيجة `Paid` النصية وحدها تحول الطلب إلى Processing | المسار fail-closed بالكامل إلى حين توفير عقد Sadad المعتمد؛ الاستجابات الناقصة أو غير الموثقة أو غير المتطابقة لا تغير Transaction أو Order |
| SF-05 | طلبا payment-link متزامنان داخل التطبيق ينشئان محاولتين | قفل لكل طلب يمنع التكرار داخل instance واحد ويعيد الرابط القائم؛ idempotency الموزع بقي P1 |
| SF-06 | صفر/سالب/طلب فارغ مقبول، والتخفيض السالب يزيد المخزون، والـ duplicates لا تُجمع | DataAnnotations + حراس خدمة + check constraints؛ جمع الكميات المتكررة؛ decrement ذري مشروط في SQL؛ increment ذري؛ إلغاء ذري يمنع إعادة المخزون مرتين |
| SF-07 | JWT وSMTP secrets موجودة في JSON tracked وbootstrap password ثابت | أزيلت القيم؛ أصبحت من secret store/environment؛ أضيفت تعليمات التدوير؛ لم تُمس الأسرار الحية |
| SF-08 | أخطاء Sadad الخام و`ex.Message` قد تصل للعميل | checkout يعيد ProblemDetails عامة، وسجلات Sadad structured بلا raw body أو invoice identifiers في رسائل الخطأ |
| SF-10 | Admin يستطيع تحويل electronic transaction إلى `Paid` مباشرة | كل الانتقالات الإدارية إلى `Paid` مرفوضة؛ COD فقط ينتقل إلى `Collected` المنفصلة، ولا يسمى `Paid` |

كما أصبح مبلغ Transaction الإلكتروني مشتقاً حصراً من `Order.Total`، وأصبح إنشاء invoice بلا `invoiceId` يفشل قبل حفظ Transaction، ويُرفض بدء الدفع إذا كانت بيانات الخصم المحملة لا تكفي لإثبات المبلغ.

## الملفات المعدلة

### API

- `Ecom.API/Controllers/OrdersController.cs`
- `Ecom.API/Program.cs`
- `Ecom.API/appsettings.json`
- `Ecom.API/appsettings.Development.json`

### Application

- `Ecom.Application/DTOs/Order/OrderDto.cs`
- `Ecom.Application/DTOs/Order/OrderItemDto.cs`
- `Ecom.Application/DTOs/Order/TransactionDto.cs`
- `Ecom.Application/DTOs/Order/TransactionAdvancedDto.cs`
- `Ecom.Application/Mappings/MappingProfile.cs`
- `Ecom.Application/Services/Interfaces/IOrderService.cs`
- `Ecom.Application/Services/OrderService.cs`
- `Ecom.Application/Services/ProductService.cs`
- `Ecom.Application/Services/TransactionService.cs`

### Domain / Infrastructure

- `Ecom.Domain/Entity/Order.cs`
- `Ecom.Domain/Entity/Transaction.cs`
- `Ecom.Domain/constant/TransactionStatus.cs`
- `Ecom.Infrastructure/Data/DataSeeder.cs`
- `Ecom.Infrastructure/Data/EcomDbContext.cs`
- `Ecom.Infrastructure/Repositories/Interfaces/IOrderRepository.cs`
- `Ecom.Infrastructure/Repositories/Interfaces/IProductRepository.cs`
- `Ecom.Infrastructure/Repositories/OrderRepository.cs`
- `Ecom.Infrastructure/Repositories/ProductRepository.cs`
- `Ecom.Infrastructure/Migrations/EcomDbContextModelSnapshot.cs`
- `Ecom.Infrastructure/Migrations/20261018054031_Phase1CheckoutSecurity.cs`
- `Ecom.Infrastructure/Migrations/20261018054031_Phase1CheckoutSecurity.Designer.cs`

### Tests / Documentation

- `tests/Ecom.CheckoutAudit.Tests/Ecom.CheckoutAudit.Tests.csproj`
- `tests/Ecom.CheckoutAudit.Tests/AuthorizationAndControllerTests.cs`
- `tests/Ecom.CheckoutAudit.Tests/BusinessAndPaymentTests.cs`
- `tests/Ecom.CheckoutAudit.Tests/GuestAccessTests.cs`
- `tests/Ecom.CheckoutAudit.Tests/HttpSecurityTests.cs`
- `tests/Ecom.CheckoutAudit.Tests/MigrationTests.cs`
- `tests/Ecom.CheckoutAudit.Tests/SqlServerIntegrationTests.cs`
- `tests/Ecom.CheckoutAudit.Tests/WorkflowAndResilienceTests.cs`
- `tests/Checkout.PaymentChecks/Program.cs`
- `docs/checkout-audit/SECURE_CONFIGURATION_AND_ROTATION.md`
- `docs/checkout-audit/PHASE1_IMPLEMENTATION_REPORT_AR.md`

## Migration قاعدة البيانات

`20261018054031_Phase1CheckoutSecurity` تضيف:

- `Orders.GuestAccessTokenHash` بطول 64.
- `Orders.GuestAccessExpiresAtUtc`.
- `Orders.GuestAccessRevokedAtUtc`.
- `Transactions.GatewayVerifiedAtUtc` كدليل محلي على انتقال مركزي موثّق.
- `CK_OrderItems_Quantity_Positive` (`Quantity > 0`).
- `CK_Products_TotalInStock_NonNegative` (`TotalInStock >= 0`).

تم اختبار تطبيق سلسلة migrations كاملة على قاعدة LocalDB معزولة ثم حذفها.

## الاختبارات المنفذة

- `dotnet build Ecom.taani.slnx --configuration Release --nologo`: نجح، 0 errors و0 warnings في التحقق النهائي.
- `dotnet test Ecom.taani.slnx --configuration Release --no-build --nologo`: نجح؛ 51 passed، 0 failed، 4 skipped بعد إضافة اختبار migration النهائي.
- HTTP integration على SQL Server LocalDB + Sadad mock: Guest COD، capability الصحيح/الخاطئ، User A/User B، cancel/revocation، legacy 410، callback fail-closed.
- concurrency على LocalDB: 20 تخفيضاً متزامناً لمخزون = 1؛ نجح واحد فقط وبقي المخزون 0.
- DB constraints: رفض quantity=0 وstock سالب.
- `Checkout.PaymentChecks`: مجموعتا COD وelectronic invoice initiation نجحتا.
- `dotnet list ... --vulnerable --include-transitive`: لا حزم ذات advisories معروفة من المصادر المستخدمة.

الاختبارات الأربعة skipped تمثل P1 معلنة: دقة KWD ثلاثية، outbox/payment boundary، coupon concurrency، وdurable email.

## القضايا BLOCKED

1. **Sadad authoritative verification (SF-04):** Swagger العام لا يحدد schema موثوقة للاستجابة أو status enum أو merchant identity/signature. يلزم Merchant Integration Specification معتمدة وعينات sandbox مصرح بها. حتى ذلك الوقت لا يوجد انتقال إلكتروني إلى `Paid`.
2. **تدوير الأسرار الحية (SF-07):** يجب أن ينفذه مسؤول النشر؛ القيم السابقة تعامل كـ compromised، مع إبطال JWTs وفحص history/logs. لم تُعرض أو تُدوّر أي قيمة حية هنا.
3. **Focused commits:** working tree كان يحتوي تعديلات غير committed سابقة ومتداخلة في الملفات نفسها؛ لم تُنشأ commits كي لا تُنسب تغييرات المستخدم الحالية إلى هذه المعالجة.

## مخاطر P1 غير المحلولة

- idempotency موزع وunique payment-attempt key؛ القفل الحالي داخل process واحد فقط.
- outbox/reconciliation لحالات remote-success/local-failure وunknown result.
- حجز مخزون مع expiry وإرجاع تلقائي للطلبات المهجورة.
- KWD `decimal(18,3)` بدلاً من schema الحالية ذات خانتين.
- coupon usage concurrency وorder-number race.
- durable email بدل `Task.Run`.
- webhook signature/replay/inbox بعد توفير عقد Sadad.
- مسار commit المحلي قبل إنشاء invoice الخارجي ما زال يحتاج إعادة تصميم P1.

نجاح اختبارات P0 لا يعني أن التطبيق production-ready؛ بوابات Sadad الرسمية، idempotency، recovery، KWD precision، واختبارات staging المصرح بها ما زالت مطلوبة.
