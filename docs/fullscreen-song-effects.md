# Fullscreen song effects

Fullscreen song effects are bundled, offline-only decorations rendered between the ambient background and the lyric content. The host owns matching, settings, lifecycle, safe areas, and cleanup; renderers only own their visuals and frame work.

## Add a song using an existing effect

1. Add flattened SVG assets to `LyricHover.App/Assets/SongEffects/` when needed.
2. Add one structured entry to `catalog.json`. Use exact artist aliases and a base title; do not add regular expressions.
3. Run the fullscreen feature fixture. It validates duplicate identifiers, parameters, renderer registration, and every referenced SVG.
4. Use a local debug presentation with the target title and artist, then verify readability at 1080p, 1440p, and 4K.

The SVG subset accepts a positive `viewBox` and one or more flattened `<path d="...">` elements. Scripts, external references, text, images, filters, event attributes, and transforms are rejected. Flatten transforms in the authoring tool before export.

The bundled maple-leaf silhouette is adapted by runtime recoloring from Twemoji's U+1F341 graphic. See `THIRD_PARTY_NOTICES.md` for attribution and license details.

## Add a new motion type

Implement `IFullscreenSongEffectRenderer`, register its stable type name in `FullscreenSongEffectHost`, and extend catalog validation for its parameters. The renderer must fail closed, avoid hit testing, honor static/reduced-motion mode, stop frame subscriptions on deactivation, and remove all visuals in `Dispose`.
