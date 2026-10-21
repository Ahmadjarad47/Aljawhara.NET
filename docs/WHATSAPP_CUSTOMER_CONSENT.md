# WhatsApp customer consent handoff

## Root cause fixed

The checkout contract previously exposed only `whatsAppInvoiceOptIn`. Existing clients that did not send this new Boolean silently used its `false` default, so the order had no `WhatsAppConsentAtUtc` or `WhatsAppConsentPhone`. The worker also compared the two stored phone strings before normalization, so equivalent Kuwaiti formats could be rejected as `NO_MATCHING_CUSTOMER_CONSENT`.

The backend now validates the explicit phone against the checkout recipient, stores the normalized `965XXXXXXXX` value and a consent audit in the same transaction, and normalizes again before dispatch.

## Checkout UI

The frontend source is not present in this repository. Add an unchecked, optional checkbox with this exact label:

> أوافق على استلام تأكيد الدفع وفاتورة الطلب عبر واتساب على رقم هاتفي المسجّل لدى الجوهرة بلس.

Show the number next to the checkbox and bind its correction to `shippingAddress.phone`. Do not block checkout when the box is unchecked. Send both consent fields when checked:

```json
{
  "items": [{ "productId": 123, "quantity": 1 }],
  "shippingAddress": {
    "fullName": "أحمد محمد",
    "phone": "+965 5000 0000",
    "street": "شارع الخليج",
    "city": "السالمية",
    "state": "محافظة حولي"
  },
  "paymentMethod": 0,
  "whatsAppInvoiceOptIn": true,
  "whatsAppConsentPhone": "+965 5000 0000"
}
```

When unchecked, send `whatsAppInvoiceOptIn: false`; omit `whatsAppConsentPhone` or send `null`. The API rejects opt-in when the two normalized numbers differ or are invalid.

## Account settings UI

Add a section titled **إشعارات واتساب** with the toggle **استلام فواتير وإشعارات طلباتي عبر واتساب**, the current number, a phone-change/verification flow, and a save button.

All account endpoints require the customer's bearer token:

- `GET /api/account/whatsapp/preferences`
- `POST /api/account/whatsapp/phone-verification`
- `POST /api/account/whatsapp/phone-verification/confirm`
- `PUT /api/account/whatsapp/preferences`

Phone verification request:

```json
{ "phoneNumber": "+965 5000 0000" }
```

Confirmation:

```json
{ "phoneNumber": "96550000000", "code": "123456" }
```

Enable after verification:

```json
{ "enabled": true, "phoneNumber": "96550000000" }
```

Revoke:

```json
{ "enabled": false }
```

Changing the legacy profile phone through `PUT /api/auth/me/phone` now invalidates phone confirmation and any old WhatsApp account consent. It never transfers consent to the new number.

## Previous skipped invoices and sandbox check

No database consent is backfilled and no old invoice is automatically retried. To test without another payment:

1. Keep Gupshup in Sandbox and put only the tester's normalized number in `SandboxAllowedDestinations`.
2. Start the sandbox conversation from that WhatsApp number as required by Gupshup.
3. As Admin, use `GET /api/whatsapp/admin/invoices` to map invoice `2` or `3` to its `orderId`. Do not change the row.
4. Have the authenticated order owner call the endpoint below. A guest uses the original `X-Guest-Order-Token`.
5. The worker will pick up only that invoice. Check the Admin status endpoint for `providerMessageId` and subsequent delivery state.

`POST /api/orders/{orderId}/whatsapp-invoice/retry`

```json
{
  "consent": true,
  "whatsAppPhone": "96550000000"
}
```

The retry is accepted only when the job was skipped specifically for `NO_MATCHING_CUSTOMER_CONSENT`, the phone matches the order recipient after normalization, and no provider/WhatsApp message ID or dispatch attempt exists.

## Gupshup consent behavior

Gupshup's official August 2024 WASS release retired the legacy single/bulk opt-in, opt-out, and list APIs and directs applications to manage consent locally. Therefore this implementation does not call those retired endpoints. It records local consent/revocation, consumes Gupshup `opted-in`/`opted-out` user events, and immediately fences pending sends after opt-out. Production phone verification uses the configured `PhoneVerificationTemplateId`; Sandbox uses the active session message flow.

Configuration example: `Ecom.API/appsettings.WhatsApp.example.json`. Secrets remain server-side.
