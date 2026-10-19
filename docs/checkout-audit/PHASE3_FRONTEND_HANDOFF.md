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

## إصلاح متابعة الدفع بعد الرجوع من سداد

- `GET /api/orders/{id}/payment-status` يقرأ الحالة المخزّنة فقط بشكل افتراضي لتجهيز رابط الدفع دون بدء تحقق مبكر. بعد الرجوع من سداد، تستخدم الواجهة `GET /api/orders/{id}/payment-status?verify=true` لتشغيل GetById بعد إثبات صلاحية الوصول للطلب. يستخدم التحقق lease مخزّناً وفاصل 10 ثوانٍ بين الفحوصات لاسترداد webhook متأخر دون مضاعفة الاستدعاءات من عدة تبويبات.
- مسار `prepare=1` يعرض تجهيز الرابط ثم يفتح فاتورة سداد الموجودة. لا يستدعي callback ولا يرسل `verify=true`. أخطاء إعداد التحقق السابقة التي حدثت قبل الاتصال بسداد لا تحجب رابط فاتورة معروفاً. لا يُستأنف رابط عند وجود نتيجة دفع غير مطابقة أو تأكيد دفع يحتاج معالجة.
- يبقى webhook وcallback قادرين على بدء تحقق فوري حتى إذا أعاد فحص الحالة السابق `Unpaid` قبل وصول الدفع.
- الاستجابة تضيف `lastVerificationAtUtc` و`verificationReasonCode`؛ وcallback يضيف `reasonCode`. تسجّل المراقبة النتيجة وسبب الرفض دون مفاتيح أو استجابة سداد الخام.
- الواجهة تحدّد مهلة 20 ثانية لطلبات callback والحالة. بعد مهلة callback أو خطأ مؤقت تستكمل التحقق عبر حالة الطلب المحمية. بعد 30 تحديثاً تلقائياً تعرض تأخر التأكيد وزر تحديث يدوي بدلاً من مؤشر تحميل دائم. أخطاء إعداد التحقق أو عدم تطابق بيانات الدفع تعرض حالة متابعة مباشرة ولا تسمح بإعادة الدفع.
- عنوان webhook الذي يُضبط في حساب سداد لهذا النشر: `https://api.aljawharaplus.com/api/orders/webhook/sadad-paid`. عنوان رجوع المتصفح `/checkout/success` له وظيفة مختلفة ولا يستقبل webhook.

الإعدادات المحلية الحالية تترك `Sadad:MerchantVendorId` فارغاً وتعطّل `ContractProfileCertified` و`EnablePaidTransitions`. لذلك لن تتحول أي محاولة إلى Paid بهذا الإعداد حتى إن عاد المتصفح بـ`payment=success`. يجب ضبط Vendor ID الحقيقي وملف التحقق المعتمد في بيئة خدمة API؛ لا تُستبدل هوية التاجر برقم تجريبي مأخوذ من توثيق سداد. يمكن رؤية المانع الفعلي في المراقبة أو `verificationReasonCode` بعد نشر التعديل.

## ما لم يتغير

- checkout الأساسي يبقى `POST /api/orders`.
- COD يبقى بلا `paymentUrl` ولا تحويل إلى Sadad.
- `merchantReference` للعرض/الدعم فقط؛ لا يُستخدم كسر أو إثبات دفع.
- لا يحتاج frontend إلى توقيع webhook، قراءة `amount/total/subtotal` أو تطبيق منطق refund.

