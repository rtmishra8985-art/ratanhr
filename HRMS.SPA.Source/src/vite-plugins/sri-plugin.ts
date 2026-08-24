import { createHash } from 'crypto';
import { readFileSync, writeFileSync, existsSync } from 'fs';
import * as path from 'path';
import type { Plugin } from 'vite';

/**
 * Adds Subresource Integrity (integrity + crossorigin) attributes to the
 * <script src> and <link rel="stylesheet"> tags that Vite emits.
 *
 * Replaces `vite-plugin-subresource-integrity`, whose published versions
 * crash on absolute (CDN / Google Fonts) URLs by trying to read them from
 * disk. Only bundle-relative assets are hashed here; remote URLs are left
 * untouched.
 *
 * FIX (SRI-001): hashes are now computed in `writeBundle` from the bytes
 * actually written to disk. The previous implementation hashed
 * `chunk.code` inside `generateBundle`, before Vite's own post-processing
 * (asset URL rewriting, banner/footer, trailing newline) ran, so every
 * emitted integrity hash was stale. Browsers rejected the main bundle with
 * "Failed to find a valid digest in the 'integrity' attribute" and the SPA
 * rendered a blank page in any production build.
 */
export function subresourceIntegrity(
  { algorithm = 'sha384' }: { algorithm?: 'sha256' | 'sha384' | 'sha512' } = {},
): Plugin {
  return {
    name: 'local-subresource-integrity',
    apply: 'build',
    enforce: 'post',
    writeBundle(options, bundle) {
      const outDir = options.dir ?? path.dirname(options.file ?? '');
      if (!outDir) return;

      const digestOf = (fileName: string): string | undefined => {
        const abs = path.resolve(outDir, fileName);
        if (!existsSync(abs)) return undefined;
        return `${algorithm}-${createHash(algorithm)
          .update(readFileSync(abs))
          .digest('base64')}`;
      };

      const emitted = Object.keys(bundle);
      const tagPattern = /<(script|link)\b[^>]*>/gi;

      for (const fileName of emitted) {
        if (!fileName.endsWith('.html')) continue;
        const htmlPath = path.resolve(outDir, fileName);
        if (!existsSync(htmlPath)) continue;

        const html = readFileSync(htmlPath, 'utf8');

        const patched = html.replace(tagPattern, (tag) => {
          if (/\bintegrity=/.test(tag)) return tag;
          const urlMatch = tag.match(/\b(?:src|href)="([^"]+)"/i);
          if (!urlMatch) return tag;
          const url = urlMatch[1];
          // Skip remote/protocol-relative/data URLs — they cannot be hashed here.
          if (/^(?:[a-z]+:)?\/\//i.test(url) || url.startsWith('data:')) return tag;
          if (
            tag.toLowerCase().startsWith('<link') &&
            !/rel="(?:stylesheet|modulepreload)"/i.test(tag)
          ) {
            return tag;
          }

          // Resolve the URL back to an emitted bundle file name.
          const normalised = url.replace(/^\/+/, '');
          const target =
            emitted.find((name) => name === normalised) ??
            emitted.find((name) => normalised.endsWith(name)) ??
            emitted.find((name) => name.endsWith(normalised));
          if (!target) return tag;

          const hash = digestOf(target);
          if (!hash) return tag;

          // Insert before the closing bracket, preserving self-closing tags.
          return tag.replace(
            /(\s*\/?)>$/,
            (_m, tail: string) => ` integrity="${hash}" crossorigin="anonymous"${tail}>`,
          );
        });

        if (patched !== html) writeFileSync(htmlPath, patched);
      }
    },
  };
}

export default subresourceIntegrity;
