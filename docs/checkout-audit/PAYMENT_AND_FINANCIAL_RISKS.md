# مخاطر الدفع والحسابات المالية

## الحكم على الـ invariant

> An order must never become Paid unless the trusted Sadad payment result is verified and the amount, currency, and order reference match the expected server-side values.

**الحكم: FAIL.** يوجد سببان مستقلان:

1. Legacy `POST /api/orders/{id}/payment` يستطيع تسجيل `Paid` بلا Sadad.
2. Sadad handler نفسه يقارن status فقط ولا يقارن amount/currency/reference.
3. Admin `MarkTransactionAsCompletedAsync` يضبط `Paid` مباشرة بلا gateway verification.

## PF-01 — Client-controlled legacy amount/status

`TransactionCreateDto` بلا validation على amount، والخدمة تحفظ amount الذي أرسله العميل وتفرض `Paid`. الاختبار حفظ `-250` لOrder غير موجود.

## PF-02 — Local Transaction amount قد يختلف عن Sadad amount

في `CreateTransactionAsync`، `transaction.Amount` يأتي من `TransactionCreateAdvancedDto.Amount`، بينما invoice amount يحسب من Order items/shipping/tax. الاختبار أثبت local `0.01` مقابل invoice `10.000`. المسار الطبيعي من `CreateOrderAsync` يمرر `order.Total` بشكل صحيح، لكن الخدمة العامة نفسها لا تفرض invariant.

## PF-03 — KWD precision غير صحيحة في قاعدة البيانات

جميع money columns الأساسية معرفة `decimal(18,2)` (`EcomDbContext.cs:72-73,126-129,142,152,186,219-221`). اختبار SQL Server LocalDB حفظ `12.345` كـ `12.35`. KWD يحتاج ثلاث خانات للتمثيل المطلوب في هذا التكامل.

هذا قد يجعل:

- invoice `F3` لا يساوي القيمة المحلية بعد round-trip.
- discount/shipping totals تختلف فلساً واحداً أو أكثر.
- reconciliation يفشل أو يقبل مبلغاً مختلفاً إن لم توجد مقارنة صارمة.

## PF-04 — Culture-sensitive formatting

`invoiceAmount.ToString("F3")` في `TransactionService.cs:216` بلا `InvariantCulture`. تحت `fr-FR` أرسل الاختبار string بقيمة `12,345`. Swagger الرسمي يعرّف `amount` كرقم `double`، بينما الكود يرسله string؛ يلزم تأكيد عقد Sadad الفعلي واستخدام JSON numeric + invariant representation عند الحاجة.

## PF-05 — Missing invoice ID لا يفشل العملية

إذا أعاد Sadad `isValid=true` مع response بلا `invoiceId`، يحفظ الكود Pending transaction بلا invoice ID وبلا URL. الاختبار أكد save واحداً. Expected هو رفض atomic أو وضع attempt واضح للمصالحة، لا Transaction مبهم.

## PF-06 — Coupon entity غير محمّلة قد ترفع المبلغ الخارجي

`hasCoupon` يعتمد `CouponId` و`CouponDiscountAmount`، لكن إرسال discount مشروط أيضاً بـ `order.Coupon != null`. الاختبار المركب أثبت local total `8` وinvoice amount `10` بلا discount. Repository الحالي يحمل Coupon، لذلك السيناريو ليس المسار الطبيعي اليوم، لكنه invariant هش وغير محمي داخل الخدمة.

## نتائج الحساب الإيجابية

- CreateOrder يتجاهل client `Price` ويستخدم `product.newPrice` (`OrderService.cs:106-123`).
- Fixed/percentage discount يحد ب`MaximumDiscountAmount` ولا يتجاوز subtotal (`OrderService.cs:457-487`).
- Shipping rule موجودة ومحددة؛ Tax حالياً صفر صراحة.
- COD يبقى Pending ولا يتصل بـ Sadad في working tree الحالي.

## سيناريوهات Sadad المنفذة

| السيناريو | النتيجة |
|---|---|
| token + invoice + URL happy path | PASS mocked |
| Missing invoice ID | Confirmed unsafe persistence |
| Missing URL | returns null؛ order already committed in caller |
| 401 / 403 / 429 / 500 / 503 on insert | لا Transaction محلي، exception propagates |
| connection interruption | لا Transaction محلي، لكن caller Order/Stock سبق commit |
| invalid/unpaid verification | لا يحول إلى Paid |
| duplicate Paid webhook | no second save، لكنه يعيد token/get request |
| paid amount/currency/reference mismatch | **يحوله إلى Paid — Critical** |
| duplicate concurrent payment link | ينشئ محاولتين — High |

## Contract gaps — BLOCKED

Swagger الرسمي العام لا يحدد response schema لـ invoice insert/getbyid، ولا webhook signing أو status enum. لذلك الحالات `Cancelled`, `Expired`, out-of-order، وofficial callback signature لا يمكن ربطها بدقة بعقد رسمي من المستودع/Swagger المنشور. يلزم specification من Sadad merchant onboarding قبل كتابة production assertions النهائية.
