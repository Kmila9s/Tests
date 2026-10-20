/**
 * thesis-gallery.spec.ts — Galería multiplataforma, viewport ESCRITORIO (Web).
 * Ejecutar: npx playwright test tests/99-audit/thesis-gallery.spec.ts --project=chromium-desktop
 */
import { galleryTests } from './_galleryFlow';

galleryTests('web');
