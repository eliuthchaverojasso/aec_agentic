# Auth API (Simple MVP)

## Scope

This document covers the current local auth flow implemented for EMA AI:

1. `POST /api/v1/auth/register`
2. `POST /api/v1/auth/login`
3. `GET /api/v1/auth/profile`

Current scope is intentionally minimal:

- Access token only (JWT bearer)
- No refresh token
- No global auth guard across all endpoints yet
- `/api/v1/auth/profile` is the route currently protected by bearer validation

## Base URL

`http://localhost:8010`

## 1) Register

### Endpoint

`POST /api/v1/auth/register`

### Request Body

```json
{
  "name": "Miguel Chavez",
  "email": "miguel@example.com",
  "password": "ChangeMe123!"
}
```

### Success Response (201)

```json
{
  "message": "User registered successfully",
  "user": {
    "id": 1,
    "name": "Miguel Chavez",
    "email": "miguel@example.com",
    "role": "engineer",
    "auth_provider": "local",
    "is_active": true,
    "is_locked": false,
    "failed_login_attempts": 0,
    "last_login_at": null,
    "password_changed_at": "2026-05-27T18:13:19.012350Z",
    "must_change_password": false,
    "created_at": "2026-05-27T18:13:19.005728Z",
    "updated_at": "2026-05-27T18:13:19.005728Z"
  }
}
```

## 2) Login

### Endpoint

`POST /api/v1/auth/login`

### Request Body

```json
{
  "email": "miguel@example.com",
  "password": "ChangeMe123!"
}
```

### Success Response (200)

```json
{
  "message": "Login successful",
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJlbWFpbCI6Im1pZ3VlbEBleGFtcGxlLmNvbSIsImV4cCI6MTc3OTkxMjgxOCwiaWF0IjoxNzc5OTA5MjE4LCJwcm92aWRlciI6ImxvY2FsIiwicm9sZSI6ImVuZ2luZWVyIiwic3ViIjoiMSJ9.GtWBeh-m-xlM8RwBvHtNQODf7qngX0NULhN00FAKyGY",
  "token_type": "bearer",
  "expires_in": 3600,
  "user": {
    "id": 1,
    "name": "Miguel Chavez",
    "email": "miguel@example.com",
    "role": "engineer",
    "auth_provider": "local",
    "is_active": true,
    "is_locked": false,
    "failed_login_attempts": 0,
    "last_login_at": "2026-05-27T19:13:38.039975Z",
    "password_changed_at": "2026-05-27T18:13:19.012350Z",
    "must_change_password": false,
    "created_at": "2026-05-27T18:13:19.005728Z",
    "updated_at": "2026-05-27T19:13:37.862649Z"
  }
}
```

## 3) Profile (Bearer)

### Endpoint

`GET /api/v1/auth/profile`

### Required Header

```http
Authorization: Bearer <access_token>
```

### Success Response (200)

```json
{
  "user": {
    "id": 1,
    "name": "Miguel Chavez",
    "email": "miguel@example.com",
    "role": "engineer",
    "auth_provider": "local",
    "is_active": true,
    "is_locked": false,
    "failed_login_attempts": 0,
    "last_login_at": "2026-05-27T19:13:38.039975Z",
    "password_changed_at": "2026-05-27T18:13:19.012350Z",
    "must_change_password": false,
    "created_at": "2026-05-27T18:13:19.005728Z",
    "updated_at": "2026-05-27T19:13:37.862649Z"
  }
}
```

## Error Notes

- `401 Not authenticated`: missing bearer token
- `401 Invalid token`: malformed/invalid signature token
- `401 Token expired`: token expired
- `403 User is inactive`: account disabled
- `423 User is locked`: account locked by failed attempts threshold

## Quick Flow Summary

1. Register user
2. Login to receive `access_token`
3. Call profile with `Authorization: Bearer <access_token>`
