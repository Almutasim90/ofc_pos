# Frontend Foundation

## Scope

STORY-00-03 establishes the Vite React TypeScript application, Tailwind 4 integration, and shadcn/ui configuration prerequisites. It intentionally does not add a design system, localization, themes, routes, application shell, business UI, forms, or data fetching.

## Commands

Run from `apps/web`:

```text
npm run dev
npm run check
npm run build
```

## Conventions

- Use `@/` for imports from `src/`.
- Use `cn` from `@/lib/utils` for shadcn-compatible class composition.
- Add shadcn components only when a story requires them.
- Add semantic design tokens in STORY-00-04, not in individual components.
