// From D3SKHAND hub/static/markdown.js, unchanged rules: minimal, safe Markdown → HTML. All text
// is escaped first; only a whitelist becomes markup again, and links must be http(s). D3sk.ChatUi's
// MarkdownParser + HtmlWriter pass the same corpus (tests/fixtures/markdown/cases.json).
export function escapeHtml(text) {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

function renderInline(text) {
  // Inline code is split out first so its content never gets bold/italic/link formatting.
  return text
    .split(/(`[^`\n]+`)/g)
    .map((part, index) => {
      if (index % 2 === 1) return `<code>${escapeHtml(part.slice(1, -1))}</code>`;
      let html = escapeHtml(part);
      // The url may hold one level of balanced parentheses (e.g. Wikipedia links).
      html = html.replace(/\[([^\]\n]+)\]\(((?:[^()\s]|\([^()\s]*\))+)\)/g, (_match, label, url) => {
        if (/^https?:\/\//i.test(url)) return `<a href="${url}" target="_blank" rel="noopener noreferrer">${label}</a>`;
        if (/^#\/new\?/.test(url)) return `<a href="${url}">${label}</a>`;
        return label;
      });
      html = html.replace(/\*\*([^*\n]+)\*\*/g, "<strong>$1</strong>");
      html = html.replace(/(^|[^*])\*([^*\s][^*\n]*)\*(?!\*)/g, "$1<em>$2</em>");
      return html;
    })
    .join("");
}

const TABLE_DIVIDER = /^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?\s*$/;

function tableCells(line) {
  return line.trim().replace(/^\|/, "").replace(/\|$/, "").split("|").map((cell) => cell.trim());
}

export function renderMarkdown(source) {
  const lines = String(source ?? "").replace(/\r\n?/g, "\n").split("\n");
  const out = [];
  let paragraph = [];
  let list = null; // { tag: "ul" | "ol", items: string[] }

  const flushParagraph = () => {
    if (paragraph.length) {
      out.push(`<p>${paragraph.map(renderInline).join("<br>")}</p>`);
      paragraph = [];
    }
  };
  const flushList = () => {
    if (list) {
      const items = list.items.map((item) => `<li>${renderInline(item)}</li>`).join("");
      out.push(`<${list.tag}>${items}</${list.tag}>`);
      list = null;
    }
  };

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    const fence = line.match(/^\s*```\s*([\w+#.-]*)\s*$/);
    if (fence) {
      flushParagraph();
      flushList();
      const code = [];
      i++;
      // An unclosed fence runs to the end — the normal state while a reply is streaming.
      while (i < lines.length && !/^\s*```\s*$/.test(lines[i])) {
        code.push(lines[i]);
        i++;
      }
      const language = fence[1] ? ` class="language-${escapeHtml(fence[1])}"` : "";
      out.push(`<pre><code${language}>${escapeHtml(code.join("\n"))}</code></pre>`);
      continue;
    }

    // A table needs a header row followed by a |---| divider; every cell is inline text.
    if (line.includes("|") && i + 1 < lines.length && lines[i + 1].includes("|") && TABLE_DIVIDER.test(lines[i + 1])) {
      flushParagraph();
      flushList();
      const head = tableCells(line);
      const rows = [];
      i += 2;
      while (i < lines.length && lines[i].includes("|") && lines[i].trim()) {
        rows.push(tableCells(lines[i]));
        i++;
      }
      i--;
      const cells = (row, tag) => head.map((_, k) => `<${tag}>${renderInline(row[k] ?? "")}</${tag}>`).join("");
      out.push(
        `<div class="table-wrap"><table><thead><tr>${cells(head, "th")}</tr></thead>` +
          `<tbody>${rows.map((row) => `<tr>${cells(row, "td")}</tr>`).join("")}</tbody></table></div>`,
      );
      continue;
    }

    if (/^\s*(?:-{3,}|\*{3,}|_{3,})\s*$/.test(line)) {
      flushParagraph();
      flushList();
      out.push("<hr>");
      continue;
    }

    const quote = line.match(/^\s*>\s?(.*)$/);
    if (quote) {
      flushParagraph();
      flushList();
      const quoted = [quote[1]];
      while (i + 1 < lines.length && /^\s*>/.test(lines[i + 1])) {
        quoted.push(lines[++i].replace(/^\s*>\s?/, ""));
      }
      out.push(`<blockquote>${quoted.map(renderInline).join("<br>")}</blockquote>`);
      continue;
    }

    const heading = line.match(/^(#{1,6})\s+(.*)$/);
    if (heading) {
      flushParagraph();
      flushList();
      const level = Math.min(heading[1].length, 3);
      out.push(`<h${level}>${renderInline(heading[2])}</h${level}>`);
      continue;
    }

    const bullet = line.match(/^\s*[-*]\s+(.*)$/);
    const numbered = line.match(/^\s*\d+[.)]\s+(.*)$/);
    if (bullet || numbered) {
      flushParagraph();
      const tag = bullet ? "ul" : "ol";
      if (list && list.tag !== tag) flushList();
      if (!list) list = { tag, items: [] };
      list.items.push((bullet || numbered)[1]);
      continue;
    }

    if (!line.trim()) {
      flushParagraph();
      flushList();
      continue;
    }
    flushList();
    paragraph.push(line);
  }
  flushParagraph();
  flushList();
  return out.join("");
}
