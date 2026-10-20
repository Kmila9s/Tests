import { test, expect } from '@playwright/test';
import * as fs from 'fs';

// F-G-06: SourceElaborator now recurses into descendant component instances
// (three-level testbenches). This spec pins the code-level pattern so a
// revert to one-level inlining is caught.
test('SourceElaborator exposes InlineComponentsToFragments recursion helper', () => {
  const src = fs.readFileSync(
    '/Users/miku/Documents/Kmila-9s/Interpreter/Services/SourceElaborator.cs', 'utf-8');
  expect(src).toContain('InlineComponentsToFragments');
  // Recursion cap is present.
  expect(src).toContain('MaxElaborationDepth');
  // Recursive call within the child branch.
  expect(src).toContain('InlineComponentsToFragments(child, all, depth + 1)');
});
