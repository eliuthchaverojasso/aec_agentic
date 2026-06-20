from pathlib import Path

from pydantic import model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

# The dev default ships in the repo, so it is public knowledge. It is only
# tolerated in local/test environments; any other app_env must override it.
_INSECURE_JWT_SECRET = "ema_ai_local_dev_secret_change_me"
_LOCAL_ENVS = {"local", "test"}


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    # Deployment environment. Only "local"/"test" may run with the bundled dev
    # secret; staging/production must provide a strong AUTH_JWT_SECRET (validator).
    app_env: str = "local"

    database_url: str = "postgresql+psycopg://ema:ema_dev_pw@localhost:5432/ema_ai"
    landing_dir: Path = Path("/app/landing")
    log_level: str = "INFO"
    cors_origins: str = (
        "http://localhost:5173,http://127.0.0.1:5173,"
        "http://192.168.1.66:5173,http://192.168.1.69:5173,"
        "http://ema-ai-demo.shokworks.io:5173"
    )

    default_organization: str = "EMA Engineering"

    api_title: str = "EMA AI Pipeline API"
    api_version: str = "0.1.0"
    api_description: str = (
        "Ingestion + serving layer for the EMA AI MVP. "
        "Accepts Revit JSON exports, runs QA/QC rules, and serves results to the dashboard."
    )
    ema_ai_product_version: str = "1.0.0-dev.1"
    ema_ai_git_sha: str = "unknown"
    ema_ai_install_root: str = "/app"
    enable_document_download: bool = False

    auth_jwt_secret: str = _INSECURE_JWT_SECRET
    auth_jwt_algorithm: str = "HS256"
    auth_access_token_expire_minutes: int = 60

    # Self-service registration is OFF by default. Enable explicitly for local
    # demos; production provisions users via invitation/admin flow (PR 2b).
    allow_public_registration: bool = False

    # Optional bucket mirroring for uploaded Data Intake files.
    # Keeps local landing writes as source of truth and adds bucket upload when enabled.
    upload_to_bucket: bool = False
    bucket_name: str | None = None
    bucket_region: str | None = None
    bucket_endpoint_url: str | None = None
    bucket_prefix: str = ""
    bucket_acl: str | None = None

    @model_validator(mode="after")
    def _reject_insecure_defaults(self) -> "Settings":
        """Fail fast rather than silently boot a non-local env with a forgeable token secret."""
        if self.app_env.strip().lower() not in _LOCAL_ENVS:
            secret = self.auth_jwt_secret or ""
            if secret == _INSECURE_JWT_SECRET or len(secret) < 32:
                raise ValueError(
                    "auth_jwt_secret must be a strong secret (>= 32 chars) and not the "
                    f"bundled dev default when app_env={self.app_env!r}. "
                    "Set AUTH_JWT_SECRET in the environment before starting."
                )
        return self


settings = Settings()
