from app.services.operation_log_service import redact_payload


def test_redact_sensitive_fields():
    payload = {
        "token": "abc",
        "nested": {"password": "secret", "safe": "ok"},
        "api_key_value": "xyz",
    }
    redacted = redact_payload(payload)
    assert redacted["token"] == "[REDACTED]"
    assert redacted["nested"]["password"] == "[REDACTED]"
    assert redacted["nested"]["safe"] == "ok"
    assert redacted["api_key_value"] == "[REDACTED]"
