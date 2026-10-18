# التزامن، المعاملات، وIdempotency

## النتائج المؤكدة

### CI-01 — High — Lost update يسمح ببيع آخر قطعة مرتين

اختبار SQL Server LocalDB فتح Contextين، وكلاهما قرأ `TotalInStock=1`، ثم حفظ كل منهما `0`. نجح الحفظان بلا `DbUpdateConcurrencyException`. القيمة النهائية `0` تخفي أن عمليتي شراء استهلكتا قطعة واحدة.

السبب: Product بلا `rowversion`/concurrency token، والإنقاص read-modify-write (`ProductService.cs:548-561`) وليس conditional atomic update.

### CI-02 — High — Stock يحجز/يخصم قبل الدفع ولا توجد expiry recovery

`ReduceProductStockAsync` ثم DB commit يحدثان قبل Sadad invoice. لا يوجد Reservation entity أو expiry/background release. Failed/abandoned payment لا يعيد stock تلقائياً. `CancelOrderAsync` يعيد stock فقط عند إلغاء صريح.

### CI-03 — Critical — Local commit يسبق external invoice

ترتيب `OrderService.cs:164-188`:

1. stock reduction؛
2. coupon usage؛
3. local commit؛
4. Sadad transaction/invoice.

فشل Sadad يترك Order + خصم stock بلا payable invoice. `catch` بعد ذلك ينادي rollback، لكن UnitOfWork سبق أن تخلص من transaction عند commit، فيصبح rollback بلا أثر.

### CI-04 — High — Unknown remote result قابل للتكرار

Sadad invoice ينشأ قبل حفظ Transaction (`TransactionService.cs:237-268`). إذا نجح remote operation ثم انقطع الاتصال أو فشل local save، لا يوجد idempotency key/unique merchant reference reconciliation. retry قد ينشئ invoice ثانياً.

### CI-05 — High — Concurrent payment link check-then-act

`GetOrderPaymentLinkAsync` يفحص successful/latest ثم ينشئ transaction بلا lock. الاختبار المتزامن أثبت استدعاءين منفصلين وإنشاء رابطين.

### CI-06 — Medium — EF ExecutionStrategy بلا `verifySucceeded`

`CreateOrderAsync` و`CancelOrderAsync` يمران `verifySucceeded: null` (`OrderService.cs:197,262`). عند unknown commit لا توجد طريقة للتأكد هل العملية نفذت قبل retry. كما أن external payment يبدأ داخل delegate بعد local commit، ما يوسع أثر retry.

### CI-07 — Medium — Coupon usage failure مخفي

`CouponService.IncrementCouponUsageAsync` يلتقط exception ويعيد `false`، و`OrderService` يهمل النتيجة. يمكن commit order رغم فشل counter. لا يوجد optimistic concurrency ظاهر على usage limits.

### CI-08 — Medium — Order number race

`GenerateOrderNumberAsync` يستخدم `CountAsync(today)+1`. يوجد unique index على `OrderNumber`، لذلك التزامن لن ينشئ duplicate silently، لكنه قد يفشل checkout الثاني بدلاً من retry آمن. لا sequence/identity مخصص.

## ما تم إثباته حول DB transaction

اختبار LocalDB أكد أن explicit rollback في SQL Server يزيل التغييرات. المشكلة ليست أن SQL transaction لا يعمل؛ المشكلة في حدود transaction وترتيب external side effects.

## التصميم الأدنى المقترح

- Order/PaymentAttempt state machine واضحة: `Created → PaymentPending → Paid/Failed/Expired`.
- InventoryReservation بكمية وexpiry، أو conditional atomic stock decrement داخل transaction.
- Outbox event بعد commit، وworker durable ينشئ Sadad invoice.
- Idempotency key فريد لكل checkout/payment attempt + unique DB constraints.
- ثابت `ref_Number` يحدد merchant/order/payment-attempt، ويعاد استخدامه في retry والمصالحة.
- Inbox/dedup table للwebhook event IDs إذا وفرها Sadad.
- `rowversion` على Product/Coupon/Transaction state أو conditional updates مع affected-row check.
- reconciliation job يعالج: remote-success/local-failure وlocal-success/remote-unknown.

## Regression gates

- 20 طلباً متزامناً على stock=1: نجاح واحد فقط.
- requestان متزامنان بنفس idempotency key: Order/PaymentAttempt/Invoice واحد.
- timeout بعد remote success: retry يعيد نفس invoice ولا ينشئ آخر.
- transient failure قبل/بعد commit: لا duplicate order ولا stock deduction.
- coupon usage limit تحت التزامن لا يتجاوز الحد.
- abandoned/expired payment يعيد reservation مرة واحدة.
