# خطة المعالجة ذات الأولوية

لم يتم تعديل Production business logic في هذه المرحلة.

## P0 — قبل أي نشر

1. **أوقف legacy payment spoof path**
   - امنع `POST /api/orders/{id}/payment` من كتابة `Paid`.
   - ألغِ direct admin `MarkTransactionAsCompleted` bypass أو افصله كحالة manual settlement ذات dual approval وaudit.
   - احصر التحويل إلى `Paid` في verified payment state transition.
   - أضف negative tests للanonymous/client amount/status.

2. **أغلق Order/Transaction IDOR مع الإبقاء على Guest Checkout**
   - owner/admin policy للمستخدمين.
   - Guest order capability token عالي entropy، مخزن hash، expiry، scope per order.
   - استبدل public order-number/ID reads بtoken-protected views قليلة البيانات.

3. **طبّق full Sadad invariant**
   - طابق invoice ID، merchant/order reference، amount، currency، merchant account، final status.
   - أي mismatch → Pending/ManualReview، ليس Paid.
   - ثبت official response/status contract مع Sadad قبل التنفيذ النهائي.

4. **صحح input/domain invariants**
   - Quantity >= 1، nonempty items، valid product IDs.
   - Service/domain guards وDB check constraints، لا تكتفِ بـ ModelState.
   - اجمع duplicate products قبل stock check.

5. **امنع oversell**
   - atomic conditional stock decrement أو rowversion/serializable reservation.
   - اختبار 20-way concurrency يجب أن يسمح بنجاح واحد عند stock=1.

6. **Rotate secrets**
   - rotate JWT signing key الموجود في tracked config إن كان استُخدم.
   - انقل secrets إلى managed secret store، وافحص repository history.

## P1 — قبل production readiness sign-off

1. **Payment/checkout idempotency**
   - idempotency key + unique DB index + PaymentAttempt state machine.
   - stable Sadad `ref_Number` وربط reconciliation.

2. **Outbox وحدود المعاملة**
   - local transaction تحفظ Order + reservation + outbox.
   - worker durable ينشئ invoice ويحدث attempt.
   - عالج remote-success/local-failure وunknown commit.

3. **KWD precision**
   - قرار موحد `decimal(18,3)` أو minor units؛ migration لكل money columns ذات الصلة.
   - `InvariantCulture`/numeric JSON وفق official contract.

4. **Inventory reservation lifecycle**
   - expiry، payment failure release، cancellation idempotency، late Paid reconciliation.

5. **Error handling/logging**
   - ProblemDetails عام، correlation ID، logging redaction.
   - لا raw Sadad bodies للعميل؛ راجع احتواء logs على customer/token data.

6. **Durable email**
   - Outbox + BackgroundService/queue؛ retries وdeduplication وshutdown drain.
   - notification failure لا يغير Order state.

7. **Webhook hardening**
   - signature/secret أو mTLS/IP policy فقط إذا نص العقد الرسمي.
   - inbox dedup/replay controls، rate limit مخصص، constant-time secret check إن وجد.

## P2 — تحسينات لاحقة

- توحيد Identity EF package إلى 10.x بعد regression build.
- pagination/input bounds لـ transaction filters.
- checkout-specific rate limiting وربط abuse signals بالguest/session وليس IP فقط.
- منع Swagger في production فعلياً؛ الكود الحالي يفتحه دائماً رغم التعليق.
- رفع coverage على services/controllers/repositories وإضافة hosted API factory آمن لا يعمل migrate/seed على config خارجي.
- frontend Playwright/a11y عند إضافة مشروع الواجهة أو staging URL.

## الاختبارات الواجب نجاحها قبل النشر

- جميع اختبارات audit الحالية بعد تحويل tests التي تصف defects إلى secure expected behavior.
- Anonymous/User A/User B/Guest token authorization matrix بالكامل.
- `Paid` mismatch tests لكل amount/currency/reference/status.
- duplicate callback/webhook/out-of-order events.
- timeout قبل وبعد remote success + retry بنفس idempotency key.
- stock=1 concurrency، duplicate product lines، negative/zero quantity.
- KWD 3-decimal/culture tests.
- rollback/unknown commit/outbox recovery.
- abandoned/failed payment reservation release.
- no-secret/no-PII logging tests.
- full HTTP E2E على isolated SQL Server + mocked Sadad.

## ترتيب التنفيذ المقترح

1. أغلق SF-01 وSF-02 فوراً مع Guest token design.
2. طبق payment verification invariant وKWD precision.
3. أدخل PaymentAttempt/idempotency/reservation/outbox كوحدة تصميم واحدة.
4. أصلح background email/error handling/secrets.
5. شغل regression + concurrency + hosted E2E + bounded staging load.

## قرار الإطلاق

**NO-GO حالياً.** شروط الانتقال إلى GO: لا public Paid mutation، لا IDOR، full Sadad invariant، idempotent retry، atomic inventory، safe failure recovery، ونجاح deployment gates أعلاه على بيئة isolated تماثل production.
