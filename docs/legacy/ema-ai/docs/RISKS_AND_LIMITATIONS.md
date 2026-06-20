# EMA AI — Risks and Limitations

**Last updated:** 2026-06-08

---

## Current Risks

### R1: Live Revit Smoke Pending
| Field | Value |
|-------|-------|
| **Risk** | Revit add-in build passes, but runtime behavior in host Revit has not been fully validated |
| **Impact** | Unexpected behavior during demo or pilot |
| **Mitigation** | Run manual Revit smoke checklist before any live demo |
| **Owner** | Dev / QA |
| **Status** | Pending |

### R2: Visual Report QA Pending
| Risk | Visual report rendering quality may have inconsistencies |
| Impact | Poor user perception, readability issues |
| Mitigation | Run visual QA checklist before pilot |
| Owner | Design / QA |
| Status | Pending |

### R3: Large Traceability Overflow
| Risk | Requirements with 50+ Element IDs may overflow the UI |
| Impact | Unreadable requirement cards |
| Mitigation | Collapsed by default with expand/collapse toggle (implemented) |
| Owner | Dev |
| Status | Mitigated |

### R4: Semantic False Positives
| Risk | Weak/mismatched model evidence may incorrectly produce Met status |
| Impact | Overconfidence in readiness |
| **Example** | Row 100: Electrical "IDENTIFICATION OF EQUIPMENT" with manufacturer names — should not be Met based on Mechanical Equipment + Level |
| Mitigation | `ApplySemanticGuardrail()` downgrades Weak/MismatchRisk to NeedsHumanReview |
| Owner | Dev / QA |
| Status | Implemented but needs validation with real data |

### R5: Ask EMA AI Provider Availability
| Risk | Local Ollama may not be running, or model may not be loaded |
| Impact | Ask EMA AI unavailable or slow |
| Mitigation | Deterministic fallback provides rule-based responses |
| Owner | Dev |
| Status | Mitigated |

### R6: Ollama Model Load Time
| Risk | Large models (qwen3.6:35b at 23 GB) take time to load |
| Impact | First response may be slow (30-60s) |
| Mitigation | Smaller fallback model (granite4.1:30b) configured. Warm-up prompt can pre-load. |
| Owner | Dev |
| Status | Acceptable |

### R7: Cloud API Keys Not Guaranteed
| Risk | OpenRouter / OpenCode API keys configured via env vars only |
| Impact | Cloud AI not available in all environments |
| Mitigation | Local Ollama is the default and always preferred |
| Owner | Dev |
| Status | Mitigated |

### R8: Real Client Data Handling
| Risk | Real XLSX/RVT files accidentally committed to repo |
| Impact | Client data exposure |
| Mitigation | Strict `.gitignore`, never `git add .`, explicit path staging only |
| Owner | All |
| Status | Ongoing vigilance required |

### R9: Drawing/Spec/Manual Review Limitations
| Risk | The engine currently prioritizes Model evidence. Drawing, Specification, and Manual validation types have limited implementation. |
| Impact | Requirements that need spec or drawing review may not get full evidence matching |
| Mitigation | ValidationType classifier routes to appropriate handler. Needs Human Review status for non-model requirements. |
| Owner | Dev |
| Status | Partial |

### R10: No-Overclaim Boundary
| Risk | Demo or documentation may inadvertently overclaim compliance |
| Impact | Legal / reputational risk |
| Mitigation | Explicit no-overclaim language in all docs. Banned word check in tests. |
| Owner | PM / Docs |
| Status | Ongoing |

### R11: Backend/Cloud Not Blocking Demo
| Risk | Current demo does not require backend or cloud services, but confusion about architecture may persist. |
| Impact | Stakeholders may expect cloud features that are not implemented |
| Mitigation | Clear docs stating that backend is optional and cloud is planned |
| Owner | PM / Docs |
| Status | Documented |

---

## Known Limitations

| Limitation | Detail |
|------------|--------|
| Revit runtime | Build-only validation. Full runtime smoke requires host Revit session. |
| PDF export | Via browser print only. Not a custom PDF generator. |
| Drawing evidence | Limited to metadata. Full sheet/spec parsing not implemented. |
| Specification evidence | Manual review primarily. Spec section parsing not implemented. |
| ACC integration | Not available. Local landing is the primary path. |
| APS model viewer | Architecture seam only. No credentials/upload/URN flow. |
| Production auth | Local demo users only. No Azure AD / RBAC. |
| Azure deployment | Documentation only. No resources deployed. |
| Multi-user | Single-user Revit add-in. No concurrent access. |
| Compliance certification | Not a compliance tool. AI-assisted first-pass review only. |
