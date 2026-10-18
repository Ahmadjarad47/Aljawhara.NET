# تقرير تنفيذ المرحلة الثالثة — التحقق من Sadad وشهادة التكامل

التاريخ: 2026-10-18  
القرار: **NO-GO للدفع الإلكتروني في الإنتاج**، مع اكتمال طبقة التحقق محلياً واستعدادها لاختبار sandbox المنضبط.

## ما أثبتته الوثائق الرسمية الجديدة

تطلب Sadad صراحةً استدعاء `GetById` للتحقق قبل تنفيذ أي إجراء بعد browser callback أو webhook. وثقت صفحة Payment Status حقول `id`, `vendor_Id`, `status`, `amount`, `total`, `subtotal`, `refunded_Amount`, `ref_Number`, `currency_Code`, `transactions`, و`transactionID`. كما وثقت Paid Webhook حمولة الحدث وإعادة الإرسال كل ثلاث دقائق بحد أقصى خمس مرات عند غياب HTTP 200.

الوثائق لا تنشر توقيع webhook، ولا مخطط استجابة تعاقدياً في OpenAPI، ولا معنى حاسماً للفروق بين `amount/total/subtotal`، ولا معادلات الخصم، ولا بنية عنصر transaction الناجح، ولا ضمان فريدة/اكتمال `GetAll Ref_Number`. لذلك عولجت هذه النقاط كقيود وليست افتراضات.

## التنفيذ الفعلي

- أضيف adapter صارم لـGetById يستخدم `decimal` فقط، ويتحقق من envelope والأنواع والحقول المطلوبة.
- يتحقق من invoice ID و`vendor_Id` والمرجع والعملة KWD والمبلغ المحلي وكل من `amount/total/subtotal` ودقة ثلاث خانات.
- يرفض الحالة المجهولة، الحقول الناقصة، transaction evidence الناقص، refund غير الصفري، والتعارضات المالية.
- callback وwebhook أصبحا منبّهين فقط؛ لا قيمة من المتصفح أو payload تستطيع تعليم المعاملة Paid.
- أضيف lease على `PaymentAttempt` لمنع تحقق عاملين من الفاتورة نفسها، مع finalization مركزي وشرطي.
- يُحفظ hash دليل GetById، معرف transaction، والحقول المالية المتحقق منها عبر migration جديدة.
- الدفع المتأخر بعد تحرير الحجز يبقى reconciliation ولا يبدأ تجهيز الطلب.
- أضيف مسار GetAll اختياري لاستعادة نتيجة إنشاء مجهولة: لا يعيد إنشاء الفاتورة، ولا يقبل إلا مرشحاً وحيداً مطابقاً ثم يعيد التحقق الكامل بـGetById.
- أضيفت `PaymentInitializationStatus.Paid = 5` حتى يستطيع frontend عرض النهاية الصحيحة.

## الحماية من التفعيل غير المقصود

الإعدادات `ContractProfileCertified`, `EnablePaidTransitions`, `DiscountSemanticsCertified`, و`GetAllContractCertified` افتراضياً false، و`MerchantVendorId` فارغ. حتى استجابة Paid سليمة شكلياً لا تنتقل إلى Paid ما لم تُعتمد الشهادة ويُفعّل المسار صراحةً. الخصومات وGetAll لهما بوابتان منفصلتان.

## نتائج الاختبار

- اختبارات Phase 3 الجديدة: **18 ناجح، 0 فاشل، 0 متخطى**.
- كامل الاختبارات: **86 ناجح، 0 فاشل، 0 متخطى**؛ تم الحفاظ على الـ68 السابقة.
- build: ناجح، 0 أخطاء.
- EF: لا تغييرات model غير مرحّلة.
- فحص الحزم: لا توجد ثغرات معلنة من المصادر المهيأة.
- فحصا checkout التنفيذيان: COD وتهيئة Sadad mock ناجحان.

شملت الأدلة: Unpaid، Paid موثق، اختلاف invoice/vendor/reference/amount/currency، الخصم، KWD `12.345`، webhook مكرر/replay، callback متأخر، timeout ومصالحة، انتهاء الحجز، عمال متزامنون، حقول ناقصة، refund وpartial amount، وGetAll غامض.

## الاختبارات المحجوبة والتبعيات

لم تتوفر `ClientKey`, `ClientSecret` أو sandbox vendor ID؛ لذلك لم تُنفذ شهادة Sadad sandbox ولم تُستخدم بيانات إنتاج أو بطاقات حقيقية. يلزم قبل التفعيل إثبات دلالات المال والخصم والدقة، هوية vendor، transaction الناجح، refund، GetAll، وسلوك webhook، والحصول على جواب رسمي حول توقيعه.

## هل يمكن الانتقال بأمان إلى Paid؟

برمجياً: نعم فقط داخل adapter الصارم، بعد نجاح جميع المقارنات والحجوزات، وبعد فتح بوابتي الشهادة والتفعيل.  
في الإعداد الحالي والإنتاج الآن: **لا**؛ المسار fail-closed ويبقى `RequiresReconciliation`.

## قرار GO / NO-GO المحدّث

- **NO-GO للدفع الإلكتروني production** حتى إتمام شهادة sandbox، إغلاق أسئلة المزود، ضبط secrets/vendor ID، تطبيق migration ومراقبة reconciliation، ثم مراجعة تفعيل flags.
- يمكن متابعة COD كقرار نشر منفصل وفق قائمة جاهزية المرحلة الثانية.
- لا يجوز ضبط `EnablePaidTransitions=true` قبل اعتماد دليل sandbox كتابياً.

