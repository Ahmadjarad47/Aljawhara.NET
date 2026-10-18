# Phase 3 frontend handoff

هذا الملف هو الملخص المطلوب لوكيل الـFrontend. لا يجب على الواجهة التحقق من Sadad أو اعتبار callback دليلاً مالياً؛ الـBackend يقوم بذلك كاملاً.

## التغييرات المطلوبة في الواجهة

1. استمر بإرسال `Idempotency-Key` عالي العشوائية مع `POST /api/orders`، واستخدم المفتاح نفسه فقط لإعادة الطلب نفسه.
2. الدفع الإلكتروني غير متزامن. إذا كان `paymentUrl` فارغاً، اعرض حالة تجهيز واطلب `GET /api/orders/{id}/payment-status` بصلاحية JWT أو `X-Guest-Order-Token`.
3. تعامل مع `paymentInitializationStatus` الرقمية كالتالي:
   - `0`: NotApplicable، عادة COD.
   - `1`: Pending، ما زال إنشاء/تحديد الفاتورة جارياً.
   - `2`: Ready، الرابط جاهز ويمكن التحويل إلى Sadad.
   - `3`: RequiresReconciliation، اعرض “الدفع قيد التحقق” ولا تعرض نجاحاً أو تعيد إنشاء الطلب.
   - `4`: Failed، يمكن إظهار retry حسب صلاحية المسار.
   - `5`: Paid، اعرض نجاح الدفع النهائي.
4. `attemptStatus` الرقمية: `0 PendingDispatch`, `1 Dispatching`, `2 AwaitingPayment`, `3 RequiresReconciliation`, `4 Paid`, `5 Failed`, `6 Cancelled`, `7 Expired`.
5. بعد عودة المستخدم من Sadad، لا تثق بقيمة `payment=success`. يمكن استدعاء `GET /api/orders/payment-callback?invoice_id=...&payment=...` كtrigger، ثم يجب polling لحالة الطلب.
6. استجابات callback:
   - `200`: تم التحقق من Paid أو كان Paid مسبقاً.
   - `202`: لم يثبت الدفع بعد/تحتاج مصالحة/التحقق مشغول؛ استمر polling بتأخير مناسب.
   - `400`: trigger أو استجابة Sadad رُفضت؛ لا تعرض Paid.
   - `404`: invoice غير معروف؛ لا تعرض تفاصيل الطلب.
7. لا تستدعِ webhook من المتصفح؛ `POST /api/orders/webhook/sadad-paid` مخصص لـSadad.
8. لا تنشئ محاولة جديدة أثناء Pending/Dispatching/AwaitingPayment/RequiresReconciliation. retry فقط بعد حالة نهائية مسموحة وبـ`Idempotency-Key` جديد عبر `POST /api/orders/{id}/payment-attempts`.
9. احترم `reservationExpiresAtUtc` ولا تعد المستخدم بتوفر المخزون بعد الانتهاء.

## ما لم يتغير

- checkout الأساسي يبقى `POST /api/orders`.
- COD يبقى بلا `paymentUrl` ولا تحويل إلى Sadad.
- `merchantReference` للعرض/الدعم فقط؛ لا يُستخدم كسر أو إثبات دفع.
- لا يحتاج frontend إلى توقيع webhook، قراءة `amount/total/subtotal` أو تطبيق منطق refund.

