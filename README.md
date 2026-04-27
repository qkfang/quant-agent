# quant-agent

A framework for orchestrating AI agents that simulate quantitative analyst roles to analyze market conditions and provide investment recommendations.

## Overview

This project uses Azure AI Foundry to create specialized quant agents — Pricing, Risk, and Alpha — that collaborate through structured orchestration patterns (debate, compare, turn-based) to deliver data-driven market analysis. Each agent brings a distinct perspective inspired by real quantitative analyst roles found on trading desks.

## Architecture

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│  quantweb   │────▶│  quantapi   │────▶│  quantlib   │
│  (Blazor)   │     │  (ASP.NET)  │     │  (Library)  │
└─────────────┘     └─────────────┘     └─────────────┘
                                              │
                                    ┌─────────┼─────────┐
                                    ▼         ▼         ▼
                               Pricing    Risk     Alpha
                                Quant     Quant     Quant
```

- **quantlib** — Core library containing agent definitions and orchestration logic
- **quantapi** — API layer exposing agent capabilities as endpoints
- **quantweb** — Blazor web frontend for interactive analysis

## Agent Roles

| Agent | Focus |
|---|---|
| **Pricing Quant** | Valuation models, fair value estimation, derivatives pricing |
| **Risk Quant** | Risk assessment, VaR, stress testing, downside scenarios |
| **Alpha Quant** | Signal generation, market patterns, trading opportunities |

## Orchestration Patterns

- **Debate** — Agents argue their perspectives across multiple rounds, with an orchestrator synthesizing a final recommendation
- **Compare** — Agents provide independent analyses that are compared side-by-side
- **Turn** — Agents contribute sequentially, each building on the previous analysis

## Tech Stack

- .NET 10 / C#
- Azure AI Foundry (Azure.AI.Projects SDK)
- Bing Grounding Search for real-time market data
- Azure AI Search for domain knowledge retrieval
- Azure Bicep for infrastructure deployment

## Getting Started

### Prerequisites

- .NET 10 SDK
- Azure subscription with AI Foundry resources deployed

### Infrastructure

Deploy Azure resources using the Bicep templates in the `bicep/` directory.

### Run

```bash
# Run the API
cd src/quantapi
dotnet run

# Run the web frontend
cd src/quantweb
dotnet run

# Run the library directly (console mode)
cd src/quantlib
dotnet run
```

## Documentation

- [What Quantitative Analysts Do](docs/what-quants-do.md) — Background on quant roles and techniques that inform the agent design