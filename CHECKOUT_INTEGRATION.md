# Checkout shipping and payment

Use the existing `POST /api/Orders` endpoint. JSON fields use camelCase.

```json
{
  "items": [{ "productId": 123, "quantity": 1 }],
  "shippingAddress": {
    "fullName": "بدر",
    "phone": "213123213",
    "street": "محافظة الفروانية - عبدالله المبارك",
    "city": "عبدالله المبارك",
    "state": "الفروانية",
    "alQataa": "8",
    "alSharee": "809",
    "alJada": "16",
    "alManzil": "16",
    "alDor": "2",
    "alShakka": "5"
  },
  "paymentMethod": 2
}
```

- `paymentMethod: 2` (`Cod`): cash on delivery. The transaction is saved as `Pending` (`status: 0`), with `paymentUrl: null` and no gateway invoice. Show order confirmation; do not request a payment link or redirect to the gateway.
- `paymentMethod: 4` (`Bank`): the existing electronic payment flow through Sadad. Use the returned `paymentUrl` to redirect to payment when present. Creating the invoice does not mark the transaction paid.
- Omitting `paymentMethod` retains the existing electronic payment behavior (`4`) for older clients. Undefined numeric enum values are rejected.
- The existing response includes `orderId`, `orderNumber`, `paymentMethod`, `status`, `gatewayInvoiceId`, and `paymentUrl`.
- `POST /api/Orders/{id}/payment-link` rejects cash-on-delivery orders instead of creating an electronic transaction for them.

Only `fullName`, `phone`, and `street` are required for checkout shipping. `street` contains the combined governorate/area label; `alSharee` remains the separate street field. `city` is the area and `state` is the governorate; both are optional, as are postal code, country, and the detailed address fields.

For account address endpoints, use `addressLine1` and `phoneNumber` instead of checkout's `street` and `phone`. No new address endpoint is needed.

Apply `20261017161057_MakeShippingLocationFieldsOptional` to each deployed database. The existing API startup applies pending migrations. Payment selection uses the existing transaction column and requires no additional migration.

Run payment regression checks with `dotnet run --project tests/Checkout.PaymentChecks/PaymentChecks.csproj --configuration Release`. These checks use repository doubles and a mocked HTTP gateway; they do not create real orders or contact Sadad.
