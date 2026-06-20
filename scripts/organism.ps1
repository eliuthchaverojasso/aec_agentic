$ErrorActionPreference = "Stop"

$command = if ($args.Count -gt 0) { $args[0] } else { "help" }

switch ($command) {
    "up" {
        docker compose -f .\infra\compose\organism\docker-compose.organism.yml up -d
    }
    "down" {
        docker compose -f .\infra\compose\organism\docker-compose.organism.yml down
    }
    "routes" {
        Get-Content .\.organism\runtime\model-routes.yaml
    }
    default {
        Write-Host "Usage: pwsh ./scripts/organism.ps1 [up|down|routes]"
    }
}

