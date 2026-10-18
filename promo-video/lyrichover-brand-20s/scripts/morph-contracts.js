(function () {
  "use strict";

  window.LyricHoverMorphContracts = Object.freeze({
    selector: "#lyric-island",
    invariant: "The same DOM node persists from scene-01 through scene-06.",
    contentSlot: "#lyric-island-content-slot",
    states: {
      opening: { x: 340, y: 38, width: 1240, height: 82, radius: 28, opacity: 1 },
      hoverAvoidance: { x: 340, y: 38, width: 1240, height: 82, radius: 28, opacity: 0.34 },
      compact: { x: 630, y: 48, width: 660, height: 66, radius: 24, opacity: 1 },
      expanded: { x: 300, y: 40, width: 1320, height: 84, radius: 30, opacity: 1 },
      playerStable: { x: 300, y: 40, width: 1320, height: 84, radius: 30, opacity: 1 },
      lyricFocus: { x: 240, y: 382, width: 1440, height: 116, radius: 34, opacity: 1 },
      docked: { x: 86, y: 948, width: 1040, height: 62, radius: 16, opacity: 1 }
    },
    rules: [
      "Never replace the wrapper at a scene boundary.",
      "Animate transforms, masks, border radius and content opacity only.",
      "Do not animate width or height directly when scaleX or scaleY can express the same change.",
      "Scene 06 reveals the taskbar behind the existing wrapper; it does not swap to a second dock object.",
      "Only scene 07 may animate the persistent product out."
    ]
  });
})();
