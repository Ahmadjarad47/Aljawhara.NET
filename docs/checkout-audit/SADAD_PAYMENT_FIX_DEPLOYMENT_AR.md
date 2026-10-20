# نشر إصلاح تحقق Sadad على Ubuntu

## قبل النشر

1. خذ نسخة PostgreSQL قابلة للاستعادة وأوقف خدمة التطبيق حتى لا يعمل أكثر من worker أثناء الاستبدال.
2. ابنِ واختبر نفس commit المراد نشره:
   `dotnet restore Ecom.taani.slnx`
   ثم `dotnet build Ecom.taani.slnx -c Release --no-restore`
   ثم `dotnet test Ecom.taani.slnx -c Release --no-build` مع ضبط `ECOM_TEST_POSTGRES` لخادم اختبارات منفصل يملك صلاحية `CREATEDB`.
3. انشر API: `dotnet publish Ecom.API/Ecom.API.csproj -c Release -o artifacts/publish/aljawhara`.

## إعداد بوابات الأمان

في `/etc/aljawhara/aljawhara.env` اضبط `Sadad__BaseUrl` وبيانات الدخول و`Sadad__MerchantVendorId` الحقيقي لنفس بيئة Sadad. ابدأ بالقيمتين:

```text
Sadad__Verification__ContractProfileCertified=false
Sadad__Verification__EnablePaidTransitions=false
Sadad__Verification__TransactionEvidenceRetryCount=2
Sadad__Verification__TransactionEvidenceRetryDelayMilliseconds=750
```

تحقق في نفس البيئة من أن GetById لفاتورة اختبار مدفوعة يعيد `Paid` مع InvoiceId وVendorId وMerchantReference وAmount وTotal وCurrency صحيحة وبدون استرداد. افحص `transactions` كما تعيدها Sadad فعلياً؛ لا تفترض أن `response.transactionID` يساوي `transactions[].id`، ولا تجعل اسم `Capture` شرطاً غير موثق. بعدها فقط غيّر بوابتي الاعتماد إلى `true`. فعّل `DiscountSemanticsCertified` فقط بعد اختبار فاتورة خصم، و`GetAllContractCertified` فقط إذا كان استرداد invoice ID عبر GetAll مطلوباً ومختبراً.

## تشغيل ومطابقة الفواتير السابقة

1. شغّل الخدمة وراقب health والـlogs مع بقاء تسجيل جسم Sadad والـtokens معطلاً.
2. بحساب Admin استدعِ `POST /api/admin/transaction/sadad/reconcile` بالجسم `{ "invoiceIds": [70934, 70935] }`.
3. راجع نتيجة كل فاتورة ثم `PaymentAttempt`, `Transaction`, `Order`, حجوزات المخزون والكوبون، و`PaymentTransitionAudit`. لا تعِد إنشاء invoice ولا تستدعِ الدفع مرة أخرى.
4. الفاتورة الموثقة مع حجز صالح يجب أن تصبح `Paid`/`Processing`. الفاتورة المدفوعة مع حجز منتهي أو تعارض طلب يجب أن تبقى `RequiresReconciliation` حتى المعالجة التشغيلية اليدوية.

للتراجع، أعد `EnablePaidTransitions=false` أولاً ثم أعد تشغيل الخدمة. هذا يوقف الانتقالات الجديدة إلى Paid من دون حذف أدلة التحقق أو تعديل السجل التاريخي.
