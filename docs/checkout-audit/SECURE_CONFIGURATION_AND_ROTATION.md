# Secure configuration and rotation

## Phase 2 additions

- `CheckoutSecurity__GuestTokenDerivationKey`: stable random secret of at least 32 bytes, identical on every API instance. Rotation invalidates deterministic recovery of existing keyed guest capabilities unless the old key is retained in a versioned compatibility window.
- `Notifications__AdminEmail`: destination for durable order notifications (configuration, not source code).
- `Sadad__MerchantIdentity`: the provider-issued merchant identity expected in authoritative verification evidence; do not invent or enable this value before Sadad documents the field.
- Payment and notification workers must receive the same database connection and application secrets as the API; leases are stored in SQL, not memory.

No deployable secret belongs in tracked JSON configuration. Supply these values through the deployment secret store or environment variables:

- `ConnectionStrings__DefaultConnection`
- `JwtSettings__SecretKey` (at least 32 bytes of cryptographically random material)
- `Sadad__ClientKey` and `Sadad__ClientSecret` for Sadad. Existing flat `ClientKey` / `ClientSecret` settings remain supported. Configure both keys together; a partial `Sadad` credential section is rejected rather than mixed with legacy credentials.
- `SmtpSettings__Host`, `SmtpSettings__Username`, `SmtpSettings__Password`, and `SmtpSettings__FromEmail`
- `BootstrapAdmin__Email` and `BootstrapAdmin__Password` only during an explicitly authorized bootstrap

The values previously committed must be treated as compromised. An authorized deployment operator must rotate the JWT signing key, SMTP password, database credentials if the old connection string contained credentials, Sadad credentials if ever committed elsewhere, and any bootstrap account password. Revoke old material after the rollout window, invalidate existing JWTs, inspect repository history and build logs, and record the rotation in the deployment change log. This repository change intentionally does not rotate live credentials.

## Diagnosing a missing checkout payment URL

`attemptStatus=0` and `initializationStatus=1` mean invoice dispatch is pending. Authentication failure returns the attempt to this state and retries after 30 seconds. In `/admin/monitoring`, search for `Sadad authentication failed` under `Ecom.Application.Services.TransactionService` and inspect `ReasonCode`:

- `SADAD_CREDENTIALS_MISSING`: the API service has no complete credential pair. Configure its systemd environment file and restart the service; exporting keys in an SSH shell does not configure an already running service.
- `SADAD_REFRESH_HTTP_401` / `SADAD_REFRESH_HTTP_403` (or the corresponding `ACCESS` codes): the gateway refused authentication. Check the keys and whether they belong to the configured `Sadad__BaseUrl` environment.
- `SADAD_REFRESH_REJECTED` / `SADAD_ACCESS_REJECTED`: the gateway returned HTTP success but did not accept authentication.
- Codes ending in `TOKEN_EMPTY`, `INVALID_RESPONSE`, `NETWORK_ERROR` or `TIMEOUT` identify the failing authentication stage without exposing response bodies or tokens.

Do not recreate orders to troubleshoot authentication: pending outbox messages retry automatically while their reservations remain active. `OrderCreatedEmail` failures are separate from invoice dispatch.
