# OFC UI/UX Design System

## Technology
React + TypeScript + Tailwind CSS + shadcn/ui + Radix UI where needed.

## Responsive Targets
- 360 / 390 mobile
- 768 tablet
- 1024 POS tablet
- 1280 / 1366 desktop POS
- 1440 / 1920 admin desktop

## UX Principles
- Arabic-first usability with full RTL.
- English LTR parity.
- Large touch targets for POS/KDS.
- Clear states: loading, empty, error, retry, offline.
- Avoid giant forms and modal-heavy flows.
- Use drawers/sheets for contextual actions.
- Keep destructive actions behind explicit confirmation.
- Never show technical errors to operators.

## POS Layout
Desktop:
- top operational header
- category/product grid
- persistent cart panel
- sticky payment actions

Tablet:
- product-first view
- collapsible cart sheet

Mobile:
- product browser
- sticky cart summary
- bottom sheet cart
- bottom payment action

## Accessibility
- visible focus
- keyboard navigation in admin
- adequate contrast
- accessible labels/dialogs
