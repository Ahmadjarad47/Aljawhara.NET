# البنية ومسار الطلب والدفع

## الملخص التنفيذي

النظام هو ASP.NET Core API على `net10.0` ببنية طبقية (`API` / `Application` / `Domain` / `Infrastructure`) ويستخدم EF Core 10 مع SQL Server، وASP.NET Core Identity + JWT، وAutoMapper، و`IUnitOfWork`. لا يوجد Frontend داخل المستودع، ولا توجد اختبارات قياسية سابقة ضمن الحل؛ الموجود مسبقاً هو Console regression harness غير مضاف إلى الحل.

النتيجة الأساسية: إنشاء الطلب كضيف يعمل مبدئياً دون تسجيل، ويعيد `PaymentUrl` عند نجاح Sadad، لكن نموذج ملكية الضيف غير موجود. لذلك لا يمكن اعتبار Guest Checkout آمناً أو قابلاً للاستعادة بعد انقطاع المتصفح. توجد كذلك مسارات عامة تكشف بيانات الطلب وتسمح بتسجيل `Paid` دون تحقق موثوق.

## Technology stack الفعلي

| المكوّن | الدليل |
|---|---|
| Runtime | `net10.0` في المشاريع الأربعة |
| API | ASP.NET Core Controllers، JWT Bearer، Identity |
| ORM / DB | EF Core 10 + `Microsoft.EntityFrameworkCore.SqlServer` |
| Production provider | SQL Server؛ `UseSqlServer` في `Ecom.API/Program.cs:59-71` |
| Mapping | AutoMapper 16.2 |
| Validation | DataAnnotations + FluentValidation packages، لكن لا يوجد Validator للطلب/العناصر محل التدقيق |
| Payment | named `HttpClient("Sadad")`، sandbox base URL في working tree |
| Background work | `SadadPaymentStatusCheckerService` كل 5 دقائق، و`Task.Run` للبريد |
| Tests | أضيف `tests/Ecom.CheckoutAudit.Tests`؛ SQL Server LocalDB + mocked Sadad |
| Frontend | غير موجود في هذا المستودع؛ E2E browser/a11y غير قابل للتنفيذ هنا |

## المسار الفعلي: CreateOrder إلى Sadad

1. `POST /api/orders` يصل إلى `OrdersController.CreateOrder` بلا `[Authorize]`، ويستخرج `userId` إن وجد (`OrdersController.cs:74-85`).
2. `OrderService.CreateOrderAsync` يبدأ EF execution strategy وDB transaction (`OrderService.cs:59-73`).
3. للمستخدم المسجّل يعيد استخدام عنوان محفوظ؛ للضيف ينشئ `ShippingAddress` و`AppUserId = null` (`75-91`).
4. يحمّل المنتجات من الخادم ويستعمل `product.newPrice`، أي أن `OrderItemCreateDto.Price` لا يحدد السعر الفعلي (`103-125`).
5. يطبق Coupon ويحسب Shipping/Tax/Total (`129-149`).
6. يحفظ Order وOrderItems، ثم يخفض Stock ويزيد Coupon usage (`151-175`).
7. يعمل `CommitTransactionAsync` محلياً (`178`).
8. بعد الـ commit ينشئ `TransactionCreateAdvancedDto` وينادي `TransactionService.CreateTransactionAsync` (`180-188`).
9. `TransactionService` يجلب Order، يولد Sadad tokens، يبني invoice، وينادي `/api/Invoice/insert` (`TransactionService.cs:148-268`).
10. يحفظ Transaction بعد نجاح الاستدعاء الخارجي، ثم يطلب `/api/Invoice/getbyid?id=...` لاسترجاع الرابط (`269-279`).
11. Controller يعيد Transaction ويفتح `Task.Run` لإرسال البريد (`OrdersController.cs:87-101`).

الحد الخطير هو الخطوتان 7 و8: المخزون والطلب يصبحان committed قبل إنشاء Sadad invoice. فشل الشبكة بعد ذلك لا يعيد الحالة المحلية.

## COD

في working tree الحالي، `PaymentMethod.Cod` ينشئ Transaction بحالة `Pending`، بلا `GatewayInvoiceId` أو `PaymentUrl`، ولا يستدعي Sadad (`TransactionService.cs:162-174`). الاختبارات السابقة والجديدة أثبتت هذا المسار.

## تأكيد الدفع

- Webhook: `POST /api/orders/webhook/sadad-paid` هو `[AllowAnonymous]`، يقرأ body ثم ينادي `HandleSadadPaidWebhookAsync`.
- Browser callback: `GET /api/orders/payment-callback` عام، يحول `payment=success` إلى نفس handler.
- Background checker: يفحص Pending transactions كل 5 دقائق.
- جميع المسارات تستعلم Sadad مرة أخرى قبل التحويل إلى `Paid`، وهذه نقطة إيجابية.
- التحقق الحالي يقارن `response.status == "Paid"` فقط (`TransactionService.cs:318-345`) ولا يقارن amount/currency/order reference.

## Endpoints وحدود الصلاحية

| Endpoint | Auth الحالي | الملاحظة |
|---|---|---|
| `POST /api/orders` | Anonymous | مطلوب للضيف؛ يعمل مبدئياً |
| `GET /api/orders/{id}` | Authenticated | يقارن `AppUserId` للمستخدم المسجّل |
| `GET /api/orders/number/{orderNumber}` | Anonymous | يعيد Order + ShippingAddress + `AppUserId` |
| `PUT /api/orders/{id}/cancel` | Authenticated | أي مستخدم مسجّل يجتاز الفحص إذا كان الطلب Guest |
| `POST /api/orders/{id}/payment` | Anonymous | Legacy path يسجل `Paid` محلياً |
| `GET /api/orders/{id}/transactions` | Anonymous | IDOR / enumeration |
| `GET /api/orders/{id}/invoice-payment` | Anonymous | يكشف customer/order/financial data |
| `POST /api/orders/{id}/payment-link` | Authenticated | أي مستخدم مسجّل يمكنه طلب رابط Guest order؛ الضيف نفسه لا يستطيع recovery |
| `POST /api/orders/webhook/sadad-paid` | Anonymous | يعيد الاستعلام من Sadad، لكن بلا signature/replay token |
| `GET /api/orders/payment-callback` | Anonymous | يعيد الاستعلام من Sadad؛ لا يكفي query وحده لجعل Pending مدفوعاً |

## Trust boundaries

- Client → API: IDs، quantity، coupon، shipping، legacy amount/status.
- API → SQL Server: Order/Stock/Coupon commit قبل Sadad.
- API → Sadad: credentials من environment، invoice data، access/refresh tokens.
- Sadad/browser → callback/webhook: بيانات غير موثوقة؛ يجب ربطها بنتيجة server-to-server كاملة.
- HTTP request scope → `Task.Run`: scoped services تستخدم بعد انتهاء الطلب.
- Background checker → DB/Sadad: تحديثات دورية بلا distributed lock ظاهر.

## Sadad contract

تم فحص Swagger الرسمي المنشور على `https://apisandbox.sadadpay.net/swagger/index.html`، والذي يشير إلى `https://apisandbox.sadadpay.net/swagger/SadadAPI/swagger.json`.

- `POST /api/Invoice/insert` موثق بأنه يستقبل `Composite.invoices[]` من نوع `DTO`.
- `DTO` يوثق `amount` كـ `double`، و`currency_Code`، و`ref_Number`، و`items[]`.
- `GET /api/Invoice/getbyid` يوثق `id` كـ `int64`.
- Responses موثقة كـ HTTP 200 فقط بلا response schema؛ كما لا توجد في الـ Swagger المنشور مواصفة موثوقة لتوقيع webhook أو قيم status النهائية.

لذلك أمكن التحقق من أسماء request fields/endpoints، لكن تعذر إثبات wrappers الحالية (`isValid`, `response.invoiceId`) أو أن القيمة الرسمية النهائية هي حرفياً `Paid`. هذا جزء **BLOCKED** حتى توفير Merchant/Sandbox integration specification المعتمدة، دون استخدام credentials حقيقية في الاختبارات.

## الحكم على متطلبات القبول

| المتطلب | الحكم |
|---|---|
| Guest Checkout يعمل دون login | **جزئياً نعم**: الإنشاء والرابط الأولي يعملان؛ recovery/cancel/link لاحقاً لا يعمل للضيف بأمان |
| Guest orders تبقى خاصة | **لا** |
| لا يمكن للعميل تغيير القيم المالية | **لا** بسبب legacy payment؛ سعر CreateOrder الأساسي server-side |
| لا يمكن spoof لحالة الدفع | **لا** عبر legacy endpoint؛ Sadad webhook نفسه يعيد التحقق لكن تحققُه ناقص |
| Payment retries idempotent | **لا** |
| Inventory consistent under concurrency | **لا** |
| DB failures لا تسبب duplicate operations | **غير مضمون** |
| Sadad errors لا تفسد Order state | **لا**؛ local commit يسبق invoice |
| Checkout failures recover safely | **لا** |
| جاهز للإنتاج | **لا** |
