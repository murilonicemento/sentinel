
---
paths:
  - "src/frontend/**/*.{html,ts,tsx,js,jsx,vue}"
  - "src/client/**/*.{html,ts,tsx,js,jsx,vue}"
---

# Accessibility Rules

## Purpose
Ensure interfaces are usable by people with different abilities.

## Standards
- Target accessibility standard: TODO
- Applicable WCAG version and conformance level: TODO

## Rules
- Use semantic HTML elements.
- Ensure interactive elements are keyboard accessible.
- Provide accessible names for controls.
- Associate form fields with labels.
- Maintain sufficient color contrast.
- Do not communicate important information through color alone.
- Preserve visible focus indicators.
- Provide appropriate alternative text for meaningful images.

## Dynamic Interfaces
- Announce relevant status changes where appropriate.
- Manage focus for dialogs and other interactive transitions.
- Respect reduced-motion preferences where applicable.

## Validation
- Automated accessibility tools: TODO
- Manual keyboard testing: TODO
- Screen reader testing: TODO