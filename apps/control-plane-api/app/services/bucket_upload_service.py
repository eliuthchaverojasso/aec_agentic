from __future__ import annotations

from functools import lru_cache
from pathlib import Path

from app.config import settings

try:
    import boto3
    from botocore.exceptions import BotoCoreError, ClientError
except Exception:  # pragma: no cover - optional dependency
    boto3 = None
    BotoCoreError = Exception
    ClientError = Exception


@lru_cache(maxsize=1)
def _bucket_client():
    if boto3 is None:
        raise RuntimeError("boto3 is not installed. Install boto3 to enable bucket uploads.")

    kwargs: dict[str, str] = {}
    if settings.bucket_region:
        kwargs["region_name"] = settings.bucket_region
    if settings.bucket_endpoint_url:
        kwargs["endpoint_url"] = settings.bucket_endpoint_url

    return boto3.client("s3", **kwargs)


def build_bucket_key(relative_path: str) -> str:
    base = relative_path.replace("\\", "/").lstrip("/")
    prefix = settings.bucket_prefix.strip("/")
    if not prefix:
        return base
    return f"{prefix}/{base}"


def upload_file_to_bucket(local_path: Path, relative_path: str) -> dict[str, str]:
    if not settings.bucket_name:
        raise RuntimeError("BUCKET_NAME is required when UPLOAD_TO_BUCKET=true")

    key = build_bucket_key(relative_path)
    extra_args: dict[str, str] = {}
    if settings.bucket_acl:
        extra_args["ACL"] = settings.bucket_acl

    client = _bucket_client()
    try:
        if extra_args:
            client.upload_file(str(local_path), settings.bucket_name, key, ExtraArgs=extra_args)
        else:
            client.upload_file(str(local_path), settings.bucket_name, key)
    except (BotoCoreError, ClientError) as exc:
        raise RuntimeError(f"Bucket upload failed for {relative_path}: {exc}") from exc

    return {"bucket": settings.bucket_name, "key": key}
