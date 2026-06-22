from pathlib import Path
from urllib.parse import urlparse

from pydantic import model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

# The dev default ships in the repo, so it is public knowledge. It is only
# tolerated in local/test environments; any other app_env must override it.
_INSECURE_JWT_SECRET = "ema_ai_local_dev_secret_change_me"
_INSECURE_DB_PASSWORD = "ema_dev_pw"
_LOCAL_ENVS = {"local", "test"}

# Development-only CORS origins that must not reach staging/production.
_DEV_CORS_ORIGINS = {
    "http://localhost:5173",
    "http://127.0.0.1:5173",
    "http://192.168.1.66:5173",
    "http://192.168.1.69:5173",
}


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
        """Fail fast rather than silently boot a non-local env with insecure defaults."""
        env = self.app_env.strip().lower()
        if env in _LOCAL_ENVS:
            return self

        # --- JWT secret ---
        secret = self.auth_jwt_secret or ""
        if secret == _INSECURE_JWT_SECRET or len(secret) < 32:
            raise ValueError(
                "auth_jwt_secret must be a strong secret (>= 32 chars) and not the "
                f"bundled dev default when app_env={self.app_env!r}. "
                "Set AUTH_JWT_SECRET in the environment before starting."
            )

        # --- Database password ---
        parsed_db = urlparse(self.database_url)
        if parsed_db.password and parsed_db.password == _INSECURE_DB_PASSWORD:
            raise ValueError(
                f"database_url uses the development default password "
                f"({_INSECURE_DB_PASSWORD!r}) which is not allowed when "
                f"app_env={self.app_env!r}. Change the password in DATABASE_URL."
            )

        # --- CORS origins ---
        configured_origins = {o.strip() for o in self.cors_origins.split(",") if o.strip()}
        dev_origins_found = configured_origins & _DEV_CORS_ORIGINS
        if dev_origins_found:
            raise ValueError(
                f"CORS_ORIGINS includes development-only origins {dev_origins_found} "
                f"when app_env={self.app_env!r}. Remove these and set production origins."
            )

        # --- HTTPS check (base URL convention) ---
        # We cannot know the public base URL from config alone, but we check that
        # CORS origins use HTTPS if they are public hosts (not localhost/127.x).
        for origin in configured_origins:
            parsed = urlparse(origin)
            host = parsed.hostname or ""
            if not host.startswith("localhost") and not host.startswith("127.") and parsed.scheme != "https":
                raise ValueError(
                    f"CORS origin {origin!r} must use HTTPS when "
                    f"app_env={self.app_env!r}."
                )

        return self


settings = Settings()
