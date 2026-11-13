# Authentication API Documentation

## Overview
This document describes the comprehensive authentication system implemented for the Aljawhara platform, including user registration, login, verification, password management, and address management features.

## Features Implemented

### 🔐 Authentication Features
- **User Registration** with email verification via OTP
- **User Login** with JWT token generation
- **Email Verification** using OTP (6-digit code, 1-hour expiration)
- **Resend Verification** OTP functionality
- **Change Password** for authenticated users
- **Forgot Password** with OTP-based reset
- **Reset Password** using OTP verification
- **Logout** functionality
- **Refresh Token** (placeholder for future implementation)

### 📍 Address Management Features
- **Create Address** for authenticated users
- **Get User Addresses** list
- **Get Address by ID** with user validation
- **Update Address** with user ownership validation
- **Delete Address** (soft delete if used in orders)
- **Set Default Address** functionality

## API Endpoints

### Authentication Endpoints

#### 1. Register User
```
POST /api/auth/register
```
**Request Body:**
```json
{
  "username": "john_doe",
  "email": "john@example.com",
  "phoneNumber": "+1234567890",
  "password": "Example!Pass123",
  "confirmPassword": "Example!Pass123"
}
```
**Response:**
```json
{
  "success": true,
  "message": "If registration is available, check your email for a verification code."
}
```

#### 2. Login
```
POST /api/auth/login
```
**Request Body:**
```json
{
  "email": "john@example.com",
  "password": "Example!Pass123",
  "rememberMe": false
}
```
**Response:**
```json
{
  "success": true,
  "message": "Login successful",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "base64encodedstring",
    "expiresAt": "2024-01-01T12:00:00Z",
    "user": {
      "id": "user-id",
      "username": "john_doe",
      "email": "john@example.com",
      "phoneNumber": "+1234567890",
      "emailConfirmed": true,
      "createdAt": "2024-01-01T10:00:00Z"
    }
  }
}
```

#### 3. Verify Account
```
POST /api/auth/verify
```
**Request Body:**
```json
{
  "email": "john@example.com",
  "otp": "123456"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Account verified successfully"
}
```

#### 4. Resend Verification
```
POST /api/auth/resend-verification
```
**Request Body:**
```json
{
  "email": "john@example.com"
}
```
**Response:**
```json
{
  "success": true,
  "message": "If the account needs verification, a code has been sent."
}
```

#### 5. Change Password (Authenticated)
```
POST /api/auth/change-password
Authorization: Bearer {token}
```
**Request Body:**
```json
{
  "currentPassword": "Example!Old123",
  "newPassword": "Example!New123",
  "confirmNewPassword": "Example!New123"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Password changed successfully"
}
```

#### 6. Forgot Password
```
POST /api/auth/forgot-password
```
**Request Body:**
```json
{
  "email": "john@example.com"
}
```
**Response:**
```json
{
  "success": true,
  "message": "If the account is eligible, a password reset code has been sent."
}
```

#### 7. Reset Password
```
POST /api/auth/reset-password
```
**Request Body:**
```json
{
  "email": "john@example.com",
  "otp": "123456",
  "newPassword": "Example!New123",
  "confirmNewPassword": "Example!New123"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Password reset successfully"
}
```

#### 8. Logout (Authenticated)
```
POST /api/auth/logout
Authorization: Bearer {token}
```
**Response:**
```json
{
  "success": true,
  "message": "Logged out successfully"
}
```

### Address Management Endpoints

#### 1. Get User Addresses
```
GET /api/auth/addresses
Authorization: Bearer {token}
```
**Response:**
```json
{
  "success": true,
  "message": "Addresses retrieved successfully",
  "data": [
    {
      "id": 1,
      "fullName": "John Doe",
      "addressLine1": "123 Main St",
      "addressLine2": "Apt 4B",
      "city": "New York",
      "state": "NY",
      "postalCode": "10001",
      "country": "USA",
      "phoneNumber": "+1234567890",
      "isDefault": true,
      "createdAt": "2024-01-01T10:00:00Z",
      "updatedAt": "2024-01-01T10:00:00Z"
    }
  ]
}
```

#### 2. Get Address by ID
```
GET /api/auth/addresses/{addressId}
Authorization: Bearer {token}
```

#### 3. Create Address
```
POST /api/auth/addresses
Authorization: Bearer {token}
```
**Request Body:**
```json
{
  "fullName": "John Doe",
  "addressLine1": "123 Main St",
  "addressLine2": "Apt 4B",
  "city": "New York",
  "state": "NY",
  "postalCode": "10001",
  "country": "USA",
  "phoneNumber": "+1234567890",
  "isDefault": false
}
```

#### 4. Update Address
```
PUT /api/auth/addresses
Authorization: Bearer {token}
```
**Request Body:**
```json
{
  "id": 1,
  "fullName": "John Doe",
  "addressLine1": "456 Oak Ave",
  "addressLine2": "Suite 200",
  "city": "Boston",
  "state": "MA",
  "postalCode": "02101",
  "country": "USA",
  "phoneNumber": "+1234567890",
  "isDefault": true
}
```

#### 5. Delete Address
```
DELETE /api/auth/addresses/{addressId}
Authorization: Bearer {token}
```

#### 6. Set Default Address
```
POST /api/auth/addresses/set-default
Authorization: Bearer {token}
```
**Request Body:**
```json
{
  "addressId": 1
}
```

## Security Features

### OTP System
- Six-digit codes expire after 10 minutes and are stored as keyed hashes in PostgreSQL.
- Each code is bound to the account and purpose. Verification and password reset codes are not interchangeable.
- Each code has five attempts. Per account and purpose, one code can be issued per minute, up to three per one-hour window.
- Successful proof is single use. If the reset rejects a replacement password, the old password remains usable and a new code is required.
- Issuance and verification endpoints apply client-IP rate limits and can return HTTP 429.

### Password Security
- Identity requires at least eight characters, including uppercase, lowercase, a digit, and a non-alphanumeric character.
- **Unique email** requirement
- **Access failed tracking** with automatic lockout

### JWT Token Security
- **HMAC SHA256** signing algorithm
- **Configurable expiration** (default 60 minutes)
- **Role-based claims** included
- **User identification** via NameIdentifier claim
- The server checks session and account state on every bearer authentication and checks current Admin membership before honoring an Admin claim.
- Use `POST /api/auth/refresh-token` with `{ "refreshToken": "…" }`. Replace the previous refresh token with the returned one after success. Only one concurrent redemption can succeed; clients should serialize refreshes. An invalid, expired or reused token returns HTTP 401.
- `expiresAt` is the access token's exact UTC expiration. Each sign-in creates a session with a fixed lifetime (seven days by default). Logout revokes the current session; password changes, successful resets and administrative blocks revoke all affected sessions.

## Error Handling

All endpoints return consistent error responses:
```json
{
  "success": false,
  "message": "Error description",
  "errors": ["Detailed error 1", "Detailed error 2"]
}
```

### Common HTTP Status Codes
- **200 OK**: Successful operation
- **201 Created**: Resource created successfully
- **400 Bad Request**: Invalid input data or business logic error
- **401 Unauthorized**: Authentication required or invalid credentials
- **403 Forbidden**: Insufficient permissions
- **404 Not Found**: Resource not found
- **429 Too Many Requests**: Authentication or API rate limit reached
- **500 Internal Server Error**: Server-side error

## Database Changes

### ShippingAddress Entity Updates
- Added `PhoneNumber` property (maps to legacy `Phone`)
- Added `AddressLine1` property (maps to legacy `Street`)
- Added `AddressLine2` property for additional address info
- Added `IsDefault` property for default address management
- Added `IsDeleted` property for soft delete functionality
- Maintained backward compatibility with legacy properties

## Configuration

### JWT Settings (appsettings.json)
```json
{
  "JwtSettings": {
    "SecretKey": "your-secret-key-here",
    "Issuer": "Aljawhara-API",
    "Audience": "Aljawhara-Users",
    "ExpiryMinutes": "60"
  }
}
```

The signing key must come from external configuration and match across API replicas. Set `JwtSettings:RefreshTokenDays` to choose the fixed session lifetime (1–90 days); access JWTs are capped at 120 minutes and cannot outlive the session.

P1 requires the PostgreSQL migration `20261112093003_P1AccountAndPaymentSecurity`. Pre-P1 bearer tokens and outstanding OTPs do not carry over. After cutover, sign in again or request a new code.

## Usage Examples

### Complete Registration Flow
1. **Register**: `POST /api/auth/register`
2. **Check Email**: User receives 6-digit OTP
3. **Verify**: `POST /api/auth/verify` with OTP
4. **Login**: `POST /api/auth/login`

### Password Reset Flow
1. **Request Reset**: `POST /api/auth/forgot-password`
2. **Check Email**: User receives 6-digit OTP
3. **Reset Password**: `POST /api/auth/reset-password` with OTP and new password

### Address Management Flow
1. **Login**: Get JWT token
2. **Create Address**: `POST /api/auth/addresses`
3. **Set Default**: `POST /api/auth/addresses/set-default`
4. **Update Address**: `PUT /api/auth/addresses`

## Notes

- The client submits the emailed code. Identity password reset tokens are issued and consumed internally.
- **Email confirmation is required** before login
- **Addresses are user-scoped** - users can only access their own addresses
- **Soft delete** is used for addresses that are referenced in orders
- **Default address** management ensures only one default per user
- **Comprehensive logging** for all authentication operations
- **Backward compatibility** maintained for existing address properties

This authentication system provides a robust, secure, and user-friendly experience for the Aljawhara platform, supporting both Arabic and English users with proper validation and error handling.
