# النتائج الأمنية

## SF-01 — Critical — Anonymous client يستطيع إنشاء Transaction بحالة Paid

- **الموقع:** `Ecom.API/Controllers/OrdersController.cs:151-171` و`Ecom.Application/Services/OrderService.cs:285-295`.
- **الجذر:** endpoint قديم بلا `[Authorize]` يمرر `TransactionCreateDto` إلى خدمة تضبط `Status = Paid` مباشرة، بلا تحميل Order وبلا Sadad verification.
- **إثبات منفذ:** `Legacy_payment_service_marks_client_amount_paid_without_gateway_verification` استخدم `OrderId=999999` و`Amount=-250` ونجح في حفظ `Paid`.
- **Expected:** لا يصبح أي Order/Transaction مدفوعاً إلا بعد trusted server-to-server verification وربط amount/currency/reference.
- **Actual:** client input يصبح سجل Paid محلياً.
- **الأثر:** fraud، تلاعب التقارير، fulfillment غير مستحق.
- **الإصلاح الأدنى:** تعطيل/حذف public legacy route أو تحويله إلى payment initialization فقط؛ منع أي كتابة `Paid` خارجة عن verified payment state machine.
- **Regression gate:** طلب anonymous/authenticated بقيم `Paid` أو amount مخصص يجب أن يرجع 401/403/4xx ولا يغير DB.

## SF-02 — Critical — Guest/Customer order data مكشوفة عبر IDOR

- **الموقع:** `OrdersController.cs:48-57`, `175-196`؛ DTO يعيد `AppUserId` وShippingAddress وبيانات مالية.
- **الجذر:** `GetOrderByNumber`, `GetOrderTransactions`, و`GetInvoicePaymentData` بلا authorization أو guest capability token. `GenerateOrderNumberAsync` يعتمد التاريخ والعدد (`OrderService.cs:401-405`) فيسهل التخمين نسبياً.
- **إثبات منفذ:** 5 metadata cases + `Anonymous_order_number_lookup_returns_guest_personal_data`.
- **الأثر:** كشف الاسم والهاتف والعنوان وسجل المعاملات والمبلغ؛ OWASP API1 Broken Object Level Authorization.
- **الإصلاح الأدنى:** authorization موحد: owner/admin للمستخدم، وhigh-entropy single-order capability token للضيف (hash مخزن، expiry، rotation/revocation، عدم وضعه في logs).
- **Regression gate:** anonymous بلا token، token خاطئ/منتهي/replayed، User A على User B، وأرقام متسلسلة كلها 404/403 بلا فرق كاشف.

## SF-03 — High — أي مستخدم مسجّل يستطيع إلغاء Guest order أو جلب رابط دفعه

- **الموقع:** `OrdersController.cs:128-133` و`210-216`.
- **الجذر:** فحص الملكية ينفذ فقط عندما `order.AppUserId` غير فارغ؛ Guest order (`null`) يجتاز الفحص لأي principal مسجل.
- **إثبات منفذ:** `Authenticated_user_can_cancel_an_unowned_guest_order` و`Authenticated_user_can_retrieve_an_unowned_guest_payment_link`.
- **الأثر:** sabotage، disclosure للرابط، duplicate payment attempts.
- **الإصلاح الأدنى:** Guest capability model مستقل؛ لا تعتبر `null` ملكية عامة. المستخدم المسجّل لا يصل Guest order إلا بguest token صالح أو admin policy صريحة.

## SF-04 — Critical — Sadad verification لا يتحقق من amount/currency/reference

- **الموقع:** `TransactionService.cs:318-345` ثم `353-388`.
- **الجذر:** response model يحتفظ `status` و`url` فقط؛ القرار يعتمد على `status == "Paid"`.
- **إثبات منفذ:** gateway response تجريبي `Paid` مع amount `0.001` وcurrency `USD` وreference مختلف، ومع ذلك تغيرت Transaction إلى `Paid` وOrder إلى `Processing`.
- **الأثر:** خرق invariant المالي الإلزامي وربط دفعة بطلب خاطئ.
- **الإصلاح الأدنى:** parse documented authoritative response؛ قارن invoice ID، merchant/order reference، amount بوحدة minor/decimal الصحيحة، currency، merchant account، والحالة النهائية. أي mismatch يبقى Pending/Review مع audit event.

## SF-05 — High — لا يوجد idempotency boundary للدفع

- **الموقع:** `OrderService.cs:304-339`; `EcomDbContext.cs:182-197,231-253`.
- **الجذر:** check-then-act بلا lock/unique key؛ لا unique index على `GatewayInvoiceId` أو idempotency key؛ لا client idempotency header.
- **إثبات منفذ:** طلبا payment-link متزامنان أنشآ عمليتي `CreateTransactionAsync` ورابطين؛ metadata test أكد غياب القيود.
- **الأثر:** duplicate invoices/charges، retries غير آمنة، unknown-result duplication.
- **الإصلاح الأدنى:** server-generated idempotency key per order/attempt، unique DB constraint، state machine + outbox، وSadad merchant reference ثابت قابل للمصالحة.

## SF-06 — High — Quantity validation غائبة وnegative reduction يزيد Stock

- **الموقع:** `OrderItemCreateDto` بلا DataAnnotations؛ `OrderService.cs:103-125`; `ProductService.cs:548-561`.
- **إثبات منفذ:** quantity `-1` و`0` model-valid؛ `ReduceProductStockAsync(product, -3)` رفع stock من 5 إلى 8.
- **الأثر:** totals سالبة/صفرية، inventory corruption، abuse للكوبون/الشحن.
- **الإصلاح الأدنى:** `[Range(1,...)]` + service/domain guard + DB check constraint؛ aggregate duplicate product quantities قبل التحقق.

## SF-07 — High — JWT signing key/config حساسة مخزنة في tracked appsettings

- **الموقع:** `Ecom.API/appsettings.json:3,6` (القيم محجوبة من التقرير).
- **الدليل:** الملف tracked؛ `SecretKey` non-empty بطول 96 ولا يبدو placeholder. لم تُعرض القيمة ولم تُستخدم.
- **الأثر:** إذا استُخدمت في أي بيئة، يمكن تزوير JWT؛ connection information أيضاً لا يجب أن يعتمد على repository config.
- **الإصلاح الأدنى:** rotate signing key فوراً إن كان مستخدماً، انقله إلى secret manager/environment، نظف history وفق سياسة الفريق، وافحص logs/artifacts.

## SF-08 — Medium — Internal/Sadad response details تعاد للعميل

- **الموقع:** `TransactionService.cs:252-257,454-460,490-496` يبني exceptions من raw body؛ `OrdersController.cs:105-112,169-172,231-234` يعيد `ex.Message`.
- **إثبات منفذ:** `Controller_returns_internal_exception_text_to_anonymous_client`.
- **الأثر:** endpoint/internal contract enumeration وقد يتسرب provider detail أو identifiers.
- **الإصلاح الأدنى:** ProblemDetails عام + correlation ID؛ التفاصيل في structured logs بعد redaction فقط.

## SF-09 — Medium — Guest recovery غير مصمم

- **الموقع:** لا property/token في Order/Transaction/DTO؛ payment-link وcancel يتطلبان login.
- **إثبات منفذ:** `Guest_records_have_no_access_token_or_ownership_secret`.
- **الأثر:** الضيف يستطيع الإنشاء والدفع فقط إذا لم يفقد response الأول؛ لا استعادة آمنة ولا متابعة خاصة.
- **الإصلاح الأدنى:** opaque guest token، مخزن hash فقط، scoped إلى order، expiry، secure delivery، one-time rotation عند العمليات الحساسة.

## SF-10 — High — Admin API يستطيع تجاوز payment verification وتحويل أي Transaction إلى Paid

- **الموقع:** `Ecom.API/Controllers/AdminTransactionController.cs:294-312`، `Ecom.Application/Services/TransactionService.cs:655-670,795-798`.
- **الجذر:** `MarkTransactionAsCompletedAsync` ينادي `UpdateTransactionStatusAsync(...Paid...)` مباشرة بلا Sadad query أو reconciliation evidence.
- **إثبات منفذ:** `Admin_completion_method_marks_transaction_paid_without_gateway_verification` حول Pending إلى Paid، حفظ مرة، وSadad calls = 0.
- **الأثر:** account compromise أو operator mistake يتجاوز invariant المالي؛ audit trail غير كافٍ.
- **الإصلاح الأدنى:** اجعل manual override حالة مستقلة مثل `ManualReview/ManuallySettled` مع dual approval وreason/audit، ولا تسمها Sadad `Paid` من دون provider evidence.

## ضوابط موجودة بالفعل

- JWT validation يتحقق issuer/audience/lifetime/signature و`ClockSkew=0` (`Program.cs:198-221`).
- Global rate limiter موجود: 100 request/minute حسب identity/IP، وAPI policy 1000/minute (`Program.cs:262-303`). هذا ليس checkout abuse/idempotency control متخصصاً.
- Sadad webhook/callback لا يثق في query/body وحده بل يعيد الاستعلام من Sadad.
- أسعار CreateOrder تؤخذ من Product server-side، وAutoMapper يتجاهل Order totals و`AppUserId` في `OrderCreateDto` mapping (`MappingProfile.cs:126-138`).
- فحص NuGet لم يجد advisories معروفة في المصادر الحالية.
