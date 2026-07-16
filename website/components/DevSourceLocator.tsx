"use client";

import { useEffect, useState } from "react";

type Highlight = {
  label: string;
  rect: DOMRect;
};

function getSourceElement(target: EventTarget | null) {
  return target instanceof Element
    ? target.closest<HTMLElement>("[data-locatorjs]")
    : null;
}

function parseSource(source: string) {
  const match = source.match(/^(.*):(\d+):(\d+)$/);
  if (!match) return null;

  return {
    file: match[1].replaceAll("\\", "/"),
    line: match[2],
    column: match[3]
  };
}

export default function DevSourceLocator() {
  const [highlight, setHighlight] = useState<Highlight | null>(null);
  const [message, setMessage] = useState("Ctrl + Shift + 单击，在 VS Code 中打开源码");

  useEffect(() => {
    let currentElement: HTMLElement | null = null;

    const clear = () => {
      currentElement = null;
      setHighlight(null);
    };

    const updateHighlight = (event: MouseEvent) => {
      if (!event.ctrlKey || !event.shiftKey) {
        clear();
        return;
      }

      const element = getSourceElement(event.target);
      const source = element?.dataset.locatorjs;
      if (!element || !source) {
        clear();
        return;
      }

      currentElement = element;
      const parsed = parseSource(source);
      setHighlight({
        label: parsed
          ? `${parsed.file.split("/").at(-1)}:${parsed.line}`
          : source,
        rect: element.getBoundingClientRect()
      });
      setMessage("松开按键可取消；单击直接打开源码");
    };

    const openSource = async (event: MouseEvent) => {
      if (!event.ctrlKey || !event.shiftKey) return;

      const element = getSourceElement(event.target) ?? currentElement;
      const source = element?.dataset.locatorjs;
      if (!source) return;

      const parsed = parseSource(source);
      if (!parsed) {
        setMessage("没有读取到有效的源码位置");
        return;
      }

      event.preventDefault();
      event.stopPropagation();
      event.stopImmediatePropagation();

      const sourceText = `${parsed.file}:${parsed.line}:${parsed.column}`;
      try {
        await navigator.clipboard.writeText(sourceText);
      } catch {
        // The VS Code link still works when clipboard permission is unavailable.
      }

      setMessage(`正在打开 ${parsed.file.split("/").at(-1)}:${parsed.line}`);
      window.location.href = `vscode://file/${encodeURI(sourceText)}`;
    };

    const onKeyUp = (event: KeyboardEvent) => {
      if (event.key === "Control" || event.key === "Shift") clear();
    };

    const onScroll = () => clear();

    document.addEventListener("mousemove", updateHighlight, true);
    document.addEventListener("click", openSource, true);
    window.addEventListener("keyup", onKeyUp, true);
    window.addEventListener("blur", clear);
    window.addEventListener("scroll", onScroll, true);

    return () => {
      document.removeEventListener("mousemove", updateHighlight, true);
      document.removeEventListener("click", openSource, true);
      window.removeEventListener("keyup", onKeyUp, true);
      window.removeEventListener("blur", clear);
      window.removeEventListener("scroll", onScroll, true);
    };
  }, []);

  if (!highlight) return null;

  return (
    <div aria-hidden="true" data-source-locator-ui>
      <div
        style={{
          position: "fixed",
          zIndex: 2147483646,
          pointerEvents: "none",
          left: highlight.rect.left,
          top: highlight.rect.top,
          width: highlight.rect.width,
          height: highlight.rect.height,
          border: "2px solid #3860BE",
          borderRadius: 8,
          background: "rgba(56, 96, 190, 0.08)",
          boxSizing: "border-box"
        }}
      />
      <div
        style={{
          position: "fixed",
          zIndex: 2147483647,
          pointerEvents: "none",
          left: Math.max(12, Math.min(highlight.rect.left, window.innerWidth - 260)),
          top: Math.max(12, highlight.rect.top - 38),
          maxWidth: 248,
          padding: "7px 11px",
          borderRadius: 999,
          background: "#141413",
          color: "#FCFBFA",
          font: "600 13px/1.2 'Sofia Sans Variable', sans-serif",
          whiteSpace: "nowrap",
          overflow: "hidden",
          textOverflow: "ellipsis",
          boxShadow: "0 6px 20px rgba(20, 20, 19, 0.18)"
        }}
      >
        {highlight.label} · {message}
      </div>
    </div>
  );
}
