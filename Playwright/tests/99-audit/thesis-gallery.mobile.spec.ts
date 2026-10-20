/**
 * thesis-gallery.mobile.spec.ts — Galería multiplataforma, viewport MÓVIL (Pixel 7).
 * Como la app es Blazor híbrido (WebView), esta vista responsiva móvil equivale
 * a la app nativa. Cubre "bloques en móvil".
 * Ejecutar: npx playwright test tests/99-audit/thesis-gallery.mobile.spec.ts --project=chromium-mobile
 */
import { galleryTests } from './_galleryFlow';

galleryTests('webmobile');
