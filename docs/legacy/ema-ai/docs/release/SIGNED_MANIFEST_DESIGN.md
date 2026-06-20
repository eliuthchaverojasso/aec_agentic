# Signed Manifest Design

EMA AI release updates should verify metadata before applying any payload.

## Goals

- verify publisher identity
- verify file hashes before staging
- reject older versions when anti-downgrade policy forbids them
- keep update operations non-blocking
- support last-known-good recovery

## Proposed Manifest Fields

```json
{
  "product": "EMA AI",
  "channel": "pilot",
  "version": "1.0.0",
  "build": {
    "git_commit": "ae6ded2e...",
    "built_at_utc": "2026-06-15T00:00:00Z"
  },
  "signature": {
    "algorithm": "rsa-sha256",
    "thumbprint": "PUBLISHER_THUMBPRINT",
    "state": "pending"
  },
  "anti_downgrade": {
    "minimum_version": "1.0.0",
    "allow_same_version": false
  },
  "artifacts": [
    {
      "path": "installer.exe",
      "sha256": "..."
    }
  ]
}
```

## Operational Rule

The updater should refuse to activate a payload until:

1. the manifest hash matches the staged manifest
2. the publisher thumbprint matches the expected value
3. the version is not below the installed minimum
4. the last-known-good snapshot has been recorded

