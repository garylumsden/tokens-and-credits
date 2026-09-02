# Azure Deployment Preflight Report

**Generated:** 2026-09-02T14:10:00+01:00
**Status:** Pass with warnings

## Summary

| Property | Value |
|----------|-------|
| **Template file** | `infra/main.bicep` |
| **Parameter file** | `infra/main.parameters.json` |
| **Project type** | azd project |
| **Deployment scope** | subscription |
| **Environment** | `tokens-demo` |
| **Validation level** | Provider |

### Validation results

| Check | Status | Details |
|-------|--------|---------|
| Bicep syntax | Pass | The template builds and lints without diagnostics. |
| What-if analysis | Pass | The preview can create the three GPT-5 deployments. |
| Permission check | Pass | The provider completed deployment validation. |

## Tools executed

| Tool | Version |
|------|---------|
| Azure CLI | 2.85.0 |
| Azure Developer CLI | 1.32.0 |
| Bicep CLI | 0.46.1 |

| Step | Command | Exit code |
|------|---------|-----------|
| 1 | `az bicep build --file .\infra\main.bicep --stdout --no-restore` | 0 |
| 2 | `az bicep lint --file .\infra\main.bicep` | 0 |
| 3 | `azd provision --preview --environment tokens-demo --no-prompt` | 0 |

## Issues

### Warnings

#### Existing resource normalization

The preview reports modifications to the Foundry account, project, and retained deployments.
These changes include provider-managed properties and capacity metadata.

Review the final deployment output for unexpected changes.

#### GPT-image-2 quota unavailable

The first preview could not add `gpt-image-2`. The environment has no available GPT Image 2 quota.

The template therefore retains the working `gpt-image-1.5` deployment.

## What-if results

| Change type | Count |
|-------------|-------|
| Create | 3 |
| Modify | 4 |
| Delete | 0 |
| Skip | 1 |

### Resources to create

| Resource type | Resource name |
|---------------|---------------|
| Azure AI Services model deployment | `gpt-5.6-sol` |
| Azure AI Services model deployment | `gpt-5.4` |
| Azure AI Services model deployment | `gpt-5.4-mini` |

### Resources to modify

| Resource type | Resource name |
|---------------|---------------|
| Azure AI Services | Existing Foundry account |
| Azure AI Services model deployment | `gpt-image-1.5` |
| Azure AI Services model deployment | `text-embedding-3-small` |
| Foundry project | Existing project |

### Resources to delete

No resources will be deleted.

## Recommendation

The GPT-5 deployments were provisioned after this preview. Run another preview before future
infrastructure changes.
