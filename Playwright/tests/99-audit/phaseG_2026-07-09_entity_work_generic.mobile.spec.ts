import { test, expect } from '@playwright/test';
import * as fs from 'fs';

// F-G-05: The parser used to reject `label : entity work.X generic map (…) port map (…);`.
// This spec verifies that the code-level fix is present — we don't reach into
// the C# unit test framework from here, so it's a soft check for the source
// pattern that resolves the issue.
test('ArchitectureParser has value-based generic/port lookahead (source pattern)', () => {
  const src = fs.readFileSync(
    '/Users/miku/Documents/Kmila-9s/Parser/Repositories/ArchitectureParser.cs', 'utf-8');
  // The fix wraps the generic/map + port/map checks in a value-based IsWord
  // helper. If somebody reverts to strict-KEYWORDS matching, this assertion
  // catches the regression.
  expect(src).toContain('IsWord(_streamer.Current, "generic")');
  expect(src).toContain('IsWord(_streamer.Current, "port")');
});
