// Minimal, safe Markdown → HTML for assistant answers.
// Safety model: the whole input is HTML-escaped FIRST; only then are a few Markdown constructs turned into
// tags we generate ourselves. Model output can therefore never inject markup or script.
// Supported: headings (#..###), **bold**, *italic* / _italic_, `code`, "-"/"*" bullet lists,
// "1." numbered lists, paragraphs and line breaks — what the assistant actually produces.

/** Escapes text for safe insertion into HTML. */
export function escapeHtml(text) {
  return String(text ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

/** Inline formatting on already-escaped text. Code spans are protected from further formatting. */
function inline(escaped) {
  const codes = [];
  let out = escaped.replace(/`([^`]+)`/g, (_, code) => {
    codes.push(code);
    return `\u0000${codes.length - 1}\u0000`;
  });
  out = out
    .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
    .replace(/(^|[^*])\*([^*\s][^*]*)\*/g, "$1<em>$2</em>")
    .replace(/(^|[\s(])_([^_\s][^_]*)_/g, "$1<em>$2</em>");
  return out.replace(/\u0000(\d+)\u0000/g, (_, i) => `<code>${codes[Number(i)]}</code>`);
}

/** Converts Markdown to safe HTML. */
export function renderMarkdown(markdown) {
  const lines = escapeHtml(markdown).replace(/\r\n?/g, "\n").split("\n");
  const html = [];
  let list = null; // "ul" | "ol" | null
  let paragraph = [];

  const flushParagraph = () => {
    if (paragraph.length) {
      html.push(`<p>${paragraph.map(inline).join("<br>")}</p>`);
      paragraph = [];
    }
  };
  const closeList = () => {
    if (list) {
      html.push(`</${list}>`);
      list = null;
    }
  };

  for (const raw of lines) {
    const line = raw.trimEnd();
    const heading = /^(#{1,3})\s+(.*)$/.exec(line);
    const bullet = /^\s*[-*•]\s+(.*)$/.exec(line);
    const numbered = /^\s*\d+[.)]\s+(.*)$/.exec(line);

    if (heading) {
      flushParagraph();
      closeList();
      const level = heading[1].length + 2; // # → h3: keeps answer headings below the page title.
      html.push(`<h${level}>${inline(heading[2])}</h${level}>`);
    } else if (bullet || numbered) {
      flushParagraph();
      const kind = bullet ? "ul" : "ol";
      if (list !== kind) {
        closeList();
        html.push(`<${kind}>`);
        list = kind;
      }
      html.push(`<li>${inline((bullet ?? numbered)[1])}</li>`);
    } else if (line.trim() === "") {
      flushParagraph();
      closeList();
    } else {
      closeList();
      paragraph.push(line.trim());
    }
  }

  flushParagraph();
  closeList();
  return html.join("");
}
