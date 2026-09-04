# Oman Fried Chicken Platform

The OFC platform is a bilingual, offline-capable restaurant management and POS system.

## Repository Layout

- `apps/web/` - React frontend application.
- `backend/src/` - ASP.NET Core modular monolith source.
- `backend/tests/` - Backend automated tests.
- `docs/` - Product requirements, architecture guardrails, sprint records, and delivery handoff.
- `infra/` - Local development and deployment infrastructure.

## Delivery Rules

- Implement one approved story at a time.
- The Master SRS in `docs/00-MASTER-SRS.md` is the product source of truth.
- PostgreSQL is the final source of truth; IndexedDB is an operational local store only.
- Do not begin a later sprint before its dependency stories are approved.

## Current Status

Sprint 00 is in progress. See `docs/PROJECT-STATE.md` for the current handoff.
