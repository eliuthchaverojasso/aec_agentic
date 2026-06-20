from uuid import uuid4

import pytest
from fastapi.testclient import TestClient

from app.config import settings
from app.main import app

# These tests exercise the real authentication flow (login, register, the 401 on
# /profile), so they must NOT receive the test-identity injection from conftest.
pytestmark = pytest.mark.noauth


@pytest.fixture(autouse=True)
def _enable_public_registration():
    """Registration defaults to OFF; turn it on for the register/login tests."""
    original = settings.allow_public_registration
    settings.allow_public_registration = True
    try:
        yield
    finally:
        settings.allow_public_registration = original


def test_register_disabled_returns_403():
    settings.allow_public_registration = False
    response = TestClient(app).post(
        "/api/v1/auth/register",
        json={"name": "Nope", "email": f"disabled-{uuid4().hex[:8]}@example.com", "password": "ChangeMe123!"},
    )
    assert response.status_code == 403
    assert response.json()["detail"] == "Public registration is disabled"


def test_register_user_success_and_case_insensitive_conflict():
    client = TestClient(app)
    email = f"auth-{uuid4().hex[:12]}@example.com"

    response = client.post(
        "/api/v1/auth/register",
        json={
            "name": "Auth User",
            "email": email,
            "password": "ChangeMe123!",
        },
    )

    assert response.status_code == 201
    payload = response.json()
    assert payload["message"] == "User registered successfully"
    assert payload["user"]["name"] == "Auth User"
    assert payload["user"]["email"] == email
    assert payload["user"]["auth_provider"] == "local"
    assert payload["user"]["is_active"] is True
    assert payload["user"]["is_locked"] is False
    assert payload["user"]["failed_login_attempts"] == 0
    assert payload["user"]["must_change_password"] is False
    assert "password_hash" not in payload["user"]

    conflict = client.post(
        "/api/v1/auth/register",
        json={
            "name": "Auth User 2",
            "email": email.upper(),
            "password": "ChangeMe123!",
        },
    )

    assert conflict.status_code == 409
    assert conflict.json()["detail"] == "Email is already registered"


def test_login_user_success_and_invalid_password():
    client = TestClient(app)
    email = f"login-{uuid4().hex[:12]}@example.com"

    register = client.post(
        "/api/v1/auth/register",
        json={
            "name": "Login User",
            "email": email,
            "password": "ChangeMe123!",
        },
    )
    assert register.status_code == 201

    login = client.post(
        "/api/v1/auth/login",
        json={
            "email": email,
            "password": "ChangeMe123!",
        },
    )
    assert login.status_code == 200
    payload = login.json()
    assert payload["message"] == "Login successful"
    assert payload["token_type"] == "bearer"
    assert isinstance(payload["access_token"], str)
    assert payload["access_token"].count(".") == 2
    assert payload["expires_in"] > 0
    assert payload["user"]["email"] == email
    assert payload["user"]["failed_login_attempts"] == 0
    assert payload["user"]["last_login_at"] is not None

    invalid = client.post(
        "/api/v1/auth/login",
        json={
            "email": email,
            "password": "WrongPass123!",
        },
    )
    assert invalid.status_code == 401
    assert invalid.json()["detail"] == "Invalid credentials"


def test_profile_requires_bearer_and_returns_user():
    client = TestClient(app)
    email = f"profile-{uuid4().hex[:12]}@example.com"

    register = client.post(
        "/api/v1/auth/register",
        json={
            "name": "Profile User",
            "email": email,
            "password": "ChangeMe123!",
        },
    )
    assert register.status_code == 201

    login = client.post(
        "/api/v1/auth/login",
        json={
            "email": email,
            "password": "ChangeMe123!",
        },
    )
    assert login.status_code == 200
    token = login.json()["access_token"]

    unauthorized = client.get("/api/v1/auth/profile")
    assert unauthorized.status_code == 401
    assert unauthorized.json()["detail"] == "Not authenticated"

    profile = client.get("/api/v1/auth/profile", headers={"Authorization": f"Bearer {token}"})
    assert profile.status_code == 200
    payload = profile.json()
    assert payload["user"]["email"] == email
    assert payload["user"]["name"] == "Profile User"
