(function () {
  "use strict";

  window.LyricHoverPlan = Object.freeze({
    width: 1920,
    height: 1080,
    fps: 60,
    duration: 20,
    worldWidth: 13440,
    scenes: [
      {
        id: "scene-01",
        start: 0,
        end: 2,
        copy: "这一句，值得被看见。",
        responsibility: "建立连续世界、品牌和产品，最迟 1 秒内看见歌词岛",
        techniques: ["mask", "svg-path-draw", "transform"]
      },
      {
        id: "scene-02",
        start: 2,
        end: 5,
        copy: "靠近时，让开一点。",
        responsibility: "展示鼠标靠近时产品降低透明度并恢复",
        techniques: ["transform", "opacity", "subtle-parallax"]
      },
      {
        id: "scene-03",
        start: 5,
        end: 9,
        copy: "想怎么展开，就怎么呈现。",
        responsibility: "同一产品对象完成紧凑、展开、重排、折叠和再次展开",
        techniques: ["layout-morph", "mask", "transform"]
      },
      {
        id: "scene-04",
        start: 9,
        end: 12,
        copy: "换个播放器，歌词照常在场。",
        responsibility: "在稳定产品外壳内切换封面、曲名与播放状态",
        techniques: ["mask", "opacity", "transform"]
      },
      {
        id: "scene-05",
        start: 12,
        end: 16,
        copy: "唱到哪里，亮到哪里。",
        responsibility: "推进歌词区域并逐词高亮，形成全片视觉高潮",
        techniques: ["mask", "color", "subtle-scale", "beat-sync"]
      },
      {
        id: "scene-06",
        start: 16,
        end: 18.5,
        copy: "少占一点，刚好看见。",
        responsibility: "同一歌词岛对象连续缩小、压扁、下移并吸附任务栏",
        techniques: ["layout-morph", "transform", "mask"]
      },
      {
        id: "scene-07",
        start: 18.5,
        end: 20,
        copy: "让歌词，始终在场。",
        responsibility: "品牌、官网与路径收束，保留至少 0.8 秒",
        techniques: ["mask", "opacity", "svg-path-draw"]
      }
    ]
  });
})();
