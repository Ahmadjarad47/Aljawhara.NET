# مصفوفة الاختبارات والمخاطر

`PASS` هنا يعني أن الاختبار نفذ بنجاح وأثبت السلوك المكتوب في اسمه؛ بعض اختبارات characterization تثبت عيباً أمنياً متعمداً ولا تعني أن النظام آمن.

| ID | الفئة / الخطر | الاختبار أو الدليل | النتيجة |
|---|---|---|---|
| TM-01 | Guest create بلا login | `Guest_checkout_passes_null_identity_to_order_service` | PASS: guest creation يصل للخدمة |
| TM-02 | Guest ownership token | `Guest_records_have_no_access_token_or_ownership_secret` | PASS: العيب مؤكد، لا token |
| TM-03 | Anonymous order-number IDOR | `Anonymous_order_number_lookup_returns_guest_personal_data` | PASS: PII مكشوفة |
| TM-04 | Anonymous sensitive routes | 5 حالات `Sensitive_order_action_has_no_authorization_requirement` | PASS: routes عامة |
| TM-05 | User A على Guest cancel | `Authenticated_user_can_cancel_an_unowned_guest_order` | PASS: الإلغاء مسموح |
| TM-06 | User A على Guest payment link | `Authenticated_user_can_retrieve_an_unowned_guest_payment_link` | PASS: الرابط مسموح |
| TM-07 | Global fallback auth | `Application_has_no_global_fallback_authorization_policy` | PASS: لا fallback |
| TM-08 | Stack/internal detail leak | `Controller_returns_internal_exception_text_to_anonymous_client` | PASS: النص يعود للعميل |
| TM-09 | Client-spoofed Paid | `Legacy_payment_service_marks_client_amount_paid_without_gateway_verification` | PASS: Critical defect reproduced |
| TM-09b | Admin manual Paid bypass | `Admin_completion_method_marks_transaction_paid_without_gateway_verification` | PASS: gateway لم يُستدعَ |
| TM-10 | Negative/zero amount | حالتا `Legacy_transaction_accepts_non_positive_amount` | PASS: accepted |
| TM-11 | Negative/zero quantity | حالتا `Order_item_accepts_non_positive_quantity` | PASS: accepted |
| TM-12 | Empty order | `Empty_order_is_model_valid` | PASS: accepted |
| TM-13 | Negative stock delta | `Negative_stock_reduction_increases_inventory` | PASS: stock ارتفع من 5 إلى 8 |
| TM-14 | Client amount vs invoice | `Transaction_amount_can_differ_from_server_generated_Sadad_invoice_amount` | PASS: 0.01 local مقابل 10.000 invoice |
| TM-15 | Missing invoice ID | `Invoice_success_without_invoice_id_is_persisted_as_pending` | PASS: persisted بلا ID/link |
| TM-16 | Missing Coupon entity | `Missing_loaded_coupon_causes_invoice_to_ignore_local_discount` | PASS: local 8 مقابل invoice 10 |
| TM-17 | Culture formatting | `Invoice_amount_format_depends_on_server_culture` | PASS: `fr-FR` أرسل `12,345` |
| TM-18 | 401/403/429/500/503 | 5 حالات `Sadad_invoice_http_errors_do_not_persist_a_transaction` | PASS: transaction غير محفوظ؛ exception propagates |
| TM-19 | Connection interruption | `Network_interruption_during_invoice_creation_propagates_with_no_local_transaction` | PASS؛ لكن Order سبق أن commit في caller flow |
| TM-20 | Forged callback only | source + handler tests | PASS جزئياً: يعاد الاستعلام من Sadad |
| TM-21 | Paid mismatch | `Paid_gateway_status_marks_transaction_paid_even_when_amount_currency_and_reference_mismatch` | PASS: Critical invariant fails |
| TM-22 | Duplicate webhook | `Duplicate_paid_webhook_is_idempotent_only_after_an_additional_gateway_call` | PASS: لا save ثانٍ، لكن Sadad call يتكرر |
| TM-23 | Duplicate concurrent invoice | `Concurrent_payment_link_requests_can_create_duplicate_external_invoices` | PASS: عمليتان مستقلتان |
| TM-24 | DB idempotency constraints | `Production_transaction_model_has_no_idempotency_or_concurrency_key` | PASS: لا unique/concurrency key |
| TM-25 | SQL transaction rollback | `Database_transaction_rollback_removes_all_test_changes` | PASS: rollback يعمل |
| TM-26 | KWD 3 decimals | `Kuwaiti_three_decimal_amount_is_rounded_by_two_decimal_database_schema` | PASS: 12.345 أصبحت 12.35 |
| TM-27 | Final stock race | `Two_buyers_can_both_commit_the_same_final_stock_unit` | PASS: كلا الـ commits نجحا بلا conflict |
| TM-28 | Local/external ordering | `Order_flow_commits_stock_and_coupon_before_calling_Sadad` | PASS: العيب مؤكد |
| TM-29 | Coupon failure | `Order_flow_ignores_coupon_usage_failure_result` | PASS: failure result مهمل |
| TM-30 | Fire-and-forget email | `Order_email_is_fire_and_forget_and_swallowing_exceptions` | PASS: النمط مؤكد |
| TM-31 | COD regression | existing `Checkout.PaymentChecks` | PASS: Pending، no gateway، no URL |
| TM-32 | Electronic happy path | existing `Checkout.PaymentChecks` | PASS: mocked tokens/invoice/URL |
| TM-33 | Dependency CVEs | `dotnet list ... --vulnerable --include-transitive` | PASS: لا advisory معروف من المصادر الحالية |
| TM-34 | Full HTTP E2E | WebApplicationFactory/host | BLOCKED: `Program.cs` يعمل migrate+seed دائماً على configured DB |
| TM-35 | Browser/a11y | Playwright | NOT APPLICABLE/BLOCKED: لا Frontend في المستودع |
| TM-36 | Live Sadad sandbox | Authorized integration | BLOCKED: لا credentials/authorization؛ لم تُنشأ invoices حقيقية |
| TM-37 | Load/connection pools | k6/staging | BLOCKED: لا isolated deployed environment؛ لم ينفذ stress على production |
| TM-38 | Real email lifecycle | shutdown/provider fault | BLOCKED runtime؛ source defect مؤكد، لم يُرسل بريد حقيقي |

## الفئات المغطاة

- Unit / functional / negative / boundary / simple property-style theories.
- Controller/API metadata and direct controller execution.
- Authorization، anonymous access، IDOR، sensitive exposure.
- Payment contract mocks، fault injection، retry/idempotency characterization.
- SQL Server LocalDB transaction، decimal precision، lost update concurrency.
- Static dependency scan and focused source-security assertions.
- Regression for COD and electronic happy path.

## الفئات غير المكتملة

Full hosted API E2E، real Sadad contract validation، browser accessibility، durable email shutdown، وcontrolled staging load بقيت BLOCKED للأسباب المذكورة، ولم تُستبدل بادعاءات نظرية على أنها نتائج تنفيذ.
