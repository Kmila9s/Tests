import { Page } from '@playwright/test';

/**
 * Monaco interop helpers. The Editor uses BlazorMonaco wrapping the global
 * `window.monaco` API, so we operate on `monaco.editor.getModels()` directly.
 *
 * Setting via `model.setValue(...)` fires `onDidChangeModelContent`, which
 * BlazorMonaco bridges back to the C# side via JS-interop, so the Blazor-
 * bound content stays in sync without us re-triggering anything.
 */
export class Monaco {
  constructor(readonly page: Page) {}

  async waitForReady(timeoutMs = 15_000) {
    await this.page.waitForFunction(
      () => !!(window as any).monaco?.editor?.getModels?.().length,
      undefined,
      { timeout: timeoutMs },
    );
  }

  async setValue(content: string) {
    await this.waitForReady();
    await this.focusEditor();
    await this.page.evaluate((c) => {
      const monaco = (window as any).monaco;
      // Editor.razor mounts one StandaloneCodeEditor per pane via the
      // `.editor-monaco` CSS class. Find the editor whose DOM node lives
      // inside a visible `.editor-monaco` container — that's the one
      // currently showing the tab the test just clicked open.
      const editors = monaco.editor.getEditors?.() ?? [];
      const codeEditor =
        editors.find((e: any) => {
          const dom = e.getDomNode?.();
          if (!dom) return false;
          // Walk up until we hit an `.editor-monaco` container that's still
          // attached and visible (offsetParent !== null filters out detached
          // or display:none editors).
          let cur: HTMLElement | null = dom;
          while (cur && !cur.classList?.contains('editor-monaco')) {
            cur = cur.parentElement;
          }
          return !!cur && cur.offsetParent !== null;
        }) ?? editors[0];
      const model = codeEditor?.getModel?.() ?? monaco.editor.getModels()[0];
      if (model) model.setValue(c);
    }, content);
  }

  async getValue(): Promise<string> {
    await this.waitForReady();
    return this.page.evaluate(() => {
      const monaco = (window as any).monaco;
      const editors = monaco.editor.getEditors?.() ?? [];
      const codeEditor =
        editors.find((e: any) => {
          const dom = e.getDomNode?.();
          if (!dom) return false;
          let cur: HTMLElement | null = dom;
          while (cur && !cur.classList?.contains('editor-monaco')) {
            cur = cur.parentElement;
          }
          return !!cur && cur.offsetParent !== null;
        }) ?? editors[0];
      const model = codeEditor?.getModel?.() ?? monaco.editor.getModels()[0];
      return model?.getValue() ?? '';
    });
  }

  /** Returns markers owned by the `kmila-vhdl` source (pushed via PublishDiagnosticsAsync). */
  async getMarkers(): Promise<Array<{ severity: number; message: string; startLineNumber: number }>> {
    await this.waitForReady();
    return this.page.evaluate(() => {
      const monaco = (window as any).monaco;
      return monaco.editor.getModelMarkers({ owner: 'kmila-vhdl' });
    });
  }

  async focusEditor() {
    await this.page.locator('.editor-monaco').first().click();
  }

  async saveWithShortcut() {
    await this.focusEditor();
    // The app handles both CtrlKey and MetaKey at the same level
    // (Editor.razor:1563 + Monaco AddCommand). Control+s works on Chromium.
    await this.page.keyboard.press('Control+s');
  }
}
