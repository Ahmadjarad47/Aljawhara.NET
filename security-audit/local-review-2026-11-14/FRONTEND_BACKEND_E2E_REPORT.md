# Angular and API browser verification

Playwright drove Edge against a locally served Angular 20 browser build with requests proxied to the ASP.NET Core API. The build came from a temporary copy of the frontend; the API base was changed to a relative path, SSR and remote font inlining were disabled for this run, and the browser blocked other origins. PostgreSQL, synthetic email delivery, and payment doubles ran locally.

The final [browser run](browser-evidence.json) passed **14 scripted checks**, including browsing and detail, guest cart, email registration and verification, customer login, guest and customer rejection from the admin dashboard, cart and coupon, customer pay-on-delivery checkout, customer order history, review surface, and admin sign-in/dashboard and product list. The review check verified the visible route into order history; a separate [HTTP check](order-and-delete-evidence.json) submitted the customer's review after a normal delivered-order transition and received 201.

Password recovery passed only through the harness-assisted route: it obtained the synthetic mailed code and loaded the reset page with the parameter the frontend expects. The ordinary email-to-form workflow failed because the form is read-only and the mailed code provides no usable navigation. See R1 in the [assessment report](DEEP_PENETRATION_TEST_REPORT.md).

The [public browser trace](public-browser-trace.zip) contains the guest phase only. Sanitized signed-in evidence retains method, route and status and omits bodies, query strings, credentials and cookies. The final browser run blocked six requests to other origins. No real online payment, provider callback, SSR, browser administration writes, or browser review submission is claimed.
