"""Authentication endpoints for local MVP auth flows."""

from __future__ import annotations

from datetime import datetime, timezone
import base64
import hashlib
import hmac
import json
import re
import secrets

from fastapi import APIRouter, Depends, HTTPException, Request, Security, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.config import settings
from app.database import get_db
from app.models import AppUser
from app.schemas import (
    AuthLoginRequest,
    AuthLoginResponse,
    AuthProfileResponse,
    AuthRegisterRequest,
    AuthRegisterResponse,
    AuthUserOut,
)

router = APIRouter(prefix="/api/v1/auth", tags=["auth"])

_PASSWORD_HASH_ALGO = "pbkdf2_sha256"
_PASSWORD_HASH_ITERATIONS = 390000
_MAX_FAILED_LOGIN_ATTEMPTS = 5
_EMAIL_REGEX = re.compile(r"^[^@\s]+@[^@\s]+\.[^@\s]+$")
_bearer_scheme = HTTPBearer(auto_error=False)


def _normalize_email(email: str) -> str:
    normalized = email.strip().lower()
    if not _EMAIL_REGEX.match(normalized):
        raise HTTPException(status_code=400, detail="A valid email is required")
    return normalized


def _hash_password(password: str) -> str:
    salt = secrets.token_bytes(16)
    digest = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, _PASSWORD_HASH_ITERATIONS)
    return f"{_PASSWORD_HASH_ALGO}${_PASSWORD_HASH_ITERATIONS}${salt.hex()}${digest.hex()}"


def _verify_password(password: str, encoded_hash: str | None) -> bool:
    if not encoded_hash:
        return False
    try:
        algo, iterations_raw, salt_hex, digest_hex = encoded_hash.split("$", 3)
        if algo != _PASSWORD_HASH_ALGO:
            return False
        iterations = int(iterations_raw)
        salt = bytes.fromhex(salt_hex)
        expected_digest = bytes.fromhex(digest_hex)
    except (TypeError, ValueError):
        return False

    candidate = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, iterations)
    return secrets.compare_digest(candidate, expected_digest)


def _b64url_encode(raw: bytes) -> str:
    return base64.urlsafe_b64encode(raw).rstrip(b"=").decode("ascii")


def _b64url_decode(raw: str) -> bytes:
    padding = "=" * (-len(raw) % 4)
    return base64.urlsafe_b64decode(raw + padding)


def _create_access_token(user: AppUser) -> tuple[str, int]:
    if settings.auth_jwt_algorithm != "HS256":
        raise HTTPException(status_code=500, detail="Unsupported JWT algorithm")

    now = int(datetime.now(timezone.utc).timestamp())
    expires_in = max(60, settings.auth_access_token_expire_minutes * 60)
    exp = now + expires_in

    header = {"alg": "HS256", "typ": "JWT"}
    payload = {
        "sub": str(user.id),
        "email": user.email,
        "role": user.role,
        "provider": user.auth_provider,
        "iat": now,
        "exp": exp,
    }

    header_b64 = _b64url_encode(json.dumps(header, separators=(",", ":"), sort_keys=True).encode("utf-8"))
    payload_b64 = _b64url_encode(json.dumps(payload, separators=(",", ":"), sort_keys=True).encode("utf-8"))
    signing_input = f"{header_b64}.{payload_b64}".encode("ascii")
    signature = hmac.new(settings.auth_jwt_secret.encode("utf-8"), signing_input, hashlib.sha256).digest()
    token = f"{header_b64}.{payload_b64}.{_b64url_encode(signature)}"
    return token, expires_in


def _decode_access_token(token: str) -> dict[str, str | int | None]:
    try:
        header_b64, payload_b64, signature_b64 = token.split(".", 2)
    except ValueError as exc:
        raise HTTPException(status_code=401, detail="Invalid token") from exc

    try:
        header = json.loads(_b64url_decode(header_b64).decode("utf-8"))
        payload = json.loads(_b64url_decode(payload_b64).decode("utf-8"))
        provided_signature = _b64url_decode(signature_b64)
    except (ValueError, json.JSONDecodeError) as exc:
        raise HTTPException(status_code=401, detail="Invalid token") from exc

    if header.get("alg") != settings.auth_jwt_algorithm:
        raise HTTPException(status_code=401, detail="Invalid token")

    signing_input = f"{header_b64}.{payload_b64}".encode("ascii")
    expected_signature = hmac.new(settings.auth_jwt_secret.encode("utf-8"), signing_input, hashlib.sha256).digest()
    if not secrets.compare_digest(provided_signature, expected_signature):
        raise HTTPException(status_code=401, detail="Invalid token")

    exp = payload.get("exp")
    if not isinstance(exp, int):
        raise HTTPException(status_code=401, detail="Invalid token")
    if exp <= int(datetime.now(timezone.utc).timestamp()):
        raise HTTPException(status_code=401, detail="Token expired")

    return payload


def get_current_user(
    request: Request,
    credentials: HTTPAuthorizationCredentials | None = Security(_bearer_scheme),
    db: Session = Depends(get_db),
) -> AppUser:
    token: str | None = None
    if credentials is not None and credentials.scheme.lower() == "bearer":
        token = credentials.credentials

    # Fallback parser for clients that send Authorization header in formats
    # not captured by HTTPBearer.
    if token is None and request is not None:
        auth_header = request.headers.get("authorization", "")
        if auth_header.lower().startswith("bearer "):
            token = auth_header[7:].strip()

    if not token:
        raise HTTPException(status_code=401, detail="Not authenticated")

    payload = _decode_access_token(token)
    sub = payload.get("sub")
    try:
        user_id = int(sub) if sub is not None else None
    except (TypeError, ValueError) as exc:
        raise HTTPException(status_code=401, detail="Invalid token") from exc
    if user_id is None:
        raise HTTPException(status_code=401, detail="Invalid token")

    user = db.get(AppUser, user_id)
    if user is None:
        raise HTTPException(status_code=401, detail="Invalid token")
    if not user.is_active:
        raise HTTPException(status_code=403, detail="User is inactive")
    if user.is_locked:
        raise HTTPException(status_code=423, detail="User is locked")
    return user


@router.post("/register", response_model=AuthRegisterResponse, status_code=status.HTTP_201_CREATED)
def register_user(payload: AuthRegisterRequest, db: Session = Depends(get_db)) -> AuthRegisterResponse:
    if not settings.allow_public_registration:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Public registration is disabled",
        )
    name = payload.name.strip()
    if not name:
        raise HTTPException(status_code=400, detail="Name is required")

    email = _normalize_email(payload.email)

    existing = db.execute(select(AppUser).where(func.lower(AppUser.email) == email)).scalar_one_or_none()
    if existing is not None:
        raise HTTPException(status_code=409, detail="Email is already registered")

    now = datetime.now(timezone.utc)
    user = AppUser(
        name=name,
        email=email,
        role="engineer",
        password_hash=_hash_password(payload.password),
        auth_provider="local",
        is_active=True,
        is_locked=False,
        failed_login_attempts=0,
        last_login_at=None,
        password_changed_at=now,
        must_change_password=False,
    )
    db.add(user)
    db.commit()
    db.refresh(user)

    return AuthRegisterResponse(
        message="User registered successfully",
        user=AuthUserOut.model_validate(user),
    )


@router.post("/login", response_model=AuthLoginResponse)
def login_user(payload: AuthLoginRequest, db: Session = Depends(get_db)) -> AuthLoginResponse:
    email = _normalize_email(payload.email)
    user = db.execute(select(AppUser).where(func.lower(AppUser.email) == email)).scalar_one_or_none()

    # Keep a generic error for missing user and invalid password.
    invalid_credentials = HTTPException(status_code=401, detail="Invalid credentials")

    if user is None:
        raise invalid_credentials

    if not user.is_active:
        raise HTTPException(status_code=403, detail="User is inactive")

    if user.is_locked:
        raise HTTPException(status_code=423, detail="User is locked")

    if user.auth_provider != "local":
        raise HTTPException(status_code=400, detail="User auth provider is not local")

    if not _verify_password(payload.password, user.password_hash):
        user.failed_login_attempts = (user.failed_login_attempts or 0) + 1
        if user.failed_login_attempts >= _MAX_FAILED_LOGIN_ATTEMPTS:
            user.is_locked = True
        db.commit()
        raise invalid_credentials

    now = datetime.now(timezone.utc)
    user.last_login_at = now
    user.failed_login_attempts = 0
    db.commit()
    db.refresh(user)
    access_token, expires_in = _create_access_token(user)

    return AuthLoginResponse(
        message="Login successful",
        access_token=access_token,
        token_type="bearer",
        expires_in=expires_in,
        user=AuthUserOut.model_validate(user),
    )


@router.get("/profile", response_model=AuthProfileResponse)
def get_profile(current_user: AppUser = Depends(get_current_user)) -> AuthProfileResponse:
    return AuthProfileResponse(user=AuthUserOut.model_validate(current_user))
