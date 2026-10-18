# Secure configuration and rotation

## Phase 2 additions

- `CheckoutSecurity__GuestTokenDerivationKey`: stable random secret of at least 32 bytes, identical on every API instance. Rotation invalidates deterministic recovery of existing keyed guest capabilities unless the old key is retained in a versioned compatibility window.
- `Notifications__AdminEmail`: destination for durable order notifications (configuration, not source code).
- `Sadad__MerchantIdentity`: the provider-issued merchant identity expected in authoritative verification evidence; do not invent or enable this value before Sadad documents the field.
- Payment and notification workers must receive the same database connection and application secrets as the API; leases are stored in SQL, not memory.

No deployable secret belongs in tracked JSON configuration. Supply these values through the deployment secret store or environment variables:

- `ConnectionStrings__DefaultConnection`
- `JwtSettings__SecretKey` (at least 32 bytes of cryptographically random material)
- `ClientKey` and `ClientSecret` for Sadad
- `SmtpSettings__Host`, `SmtpSettings__Username`, `SmtpSettings__Password`, and `SmtpSettings__FromEmail`
- `BootstrapAdmin__Email` and `BootstrapAdmin__Password` only during an explicitly authorized bootstrap

The values previously committed must be treated as compromised. An authorized deployment operator must rotate the JWT signing key, SMTP password, database credentials if the old connection string contained credentials, Sadad credentials if ever committed elsewhere, and any bootstrap account password. Revoke old material after the rollout window, invalidate existing JWTs, inspect repository history and build logs, and record the rotation in the deployment change log. This repository change intentionally does not rotate live credentials.
