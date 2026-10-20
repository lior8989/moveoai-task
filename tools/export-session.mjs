#!/usr/bin/env node
// Exports the Claude Code session that built this project into docs/session/:
//   session-transcript.md  — readable: user messages, assistant replies, tool calls (+ truncated results)
//   session-raw.jsonl      — the complete original log (every record, for full fidelity)
// Secrets are masked defensively in both outputs (API keys were never printed, but be safe).
//
// Usage: node tools/export-session.mjs [path/to/session.jsonl]
//   Without an argument, the most recent session of this project in ~/.claude/projects is used.
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

const RESULT_LIMIT = 1500;  // characters of each tool result shown in the Markdown
const INPUT_LIMIT = 600;    // characters of each tool input shown in the Markdown
const repoRoot = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1")), "..");
const outDir = path.join(repoRoot, "docs", "session");

const SECRET_PATTERNS = [
  /gsk_[A-Za-z0-9]{20,}/g,                 // Groq
  /AIza[0-9A-Za-z_-]{30,}/g,               // Google
  /sk-[A-Za-z0-9_-]{20,}/g,                // OpenAI-style
  /(Bearer\s+)[A-Za-z0-9._-]{20,}/g,       // Authorization headers
];

function mask(text) {
  let out = text;
  for (const pattern of SECRET_PATTERNS) {
    out = out.replace(pattern, (match, prefix) => (typeof prefix === "string" && prefix.startsWith("Bearer") ? `${prefix}[REDACTED]` : "[REDACTED]"));
  }
  return out;
}

function findLatestSession() {
  // Claude Code stores sessions under ~/.claude/projects/<cwd with separators replaced by "-">.
  const projectKey = repoRoot.replace(/[:\\/]/g, "-");
  const dir = path.join(os.homedir(), ".claude", "projects", projectKey);
  const files = fs.readdirSync(dir).filter((f) => f.endsWith(".jsonl"))
    .map((f) => ({ f, t: fs.statSync(path.join(dir, f)).mtimeMs }))
    .sort((a, b) => b.t - a.t);
  if (!files.length) throw new Error(`No session files in ${dir}`);
  return path.join(dir, files[0].f);
}

const truncate = (text, limit) => (text.length <= limit ? text : `${text.slice(0, limit)}\n… [${text.length - limit} more characters]`);
const stripReminders = (text) => text.replace(/<system-reminder>[\s\S]*?<\/system-reminder>/g, "").trim();

function resultText(content) {
  if (typeof content === "string") return content;
  if (Array.isArray(content)) {
    return content.map((c) => (c.type === "text" ? c.text : c.type === "image" ? "[image]" : `[${c.type}]`)).join("\n");
  }
  return JSON.stringify(content);
}

function toolInput(input) {
  // Show the essence of common tools compactly; fall back to JSON.
  if (input == null) return "";
  if (input.command) return input.command;
  if (input.file_path && input.content !== undefined) return `${input.file_path}  (${input.content.length} chars written)`;
  if (input.file_path && input.old_string !== undefined) return `${input.file_path}  (edit)`;
  if (input.file_path) return input.file_path;
  return JSON.stringify(input);
}

const source = process.argv[2] ?? findLatestSession();
const lines = fs.readFileSync(source, "utf8").split("\n").filter(Boolean);
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, "session-raw.jsonl"), mask(lines.join("\n")) + "\n");

const md = [];
const counts = { user: 0, assistant: 0, tools: 0 };
let first = null;
let last = null;

for (const line of lines) {
  let record;
  try { record = JSON.parse(line); } catch { continue; }
  if (record.type !== "user" && record.type !== "assistant") continue;
  first ??= record.timestamp;
  last = record.timestamp ?? last;
  const content = record.message?.content;
  const blocks = typeof content === "string" ? [{ type: "text", text: content }] : Array.isArray(content) ? content : [];

  for (const block of blocks) {
    if (record.type === "user" && block.type === "text") {
      const text = stripReminders(block.text ?? "");
      if (!text || text.startsWith("[Request interrupted")) {
        if (text) md.push(`> _${text}_\n`);
        continue;
      }
      counts.user++;
      md.push(`---\n\n## 🧑 User — ${record.timestamp ?? ""}\n\n${text}\n`);
    } else if (record.type === "assistant" && block.type === "text" && block.text?.trim()) {
      counts.assistant++;
      md.push(`### 🤖 Assistant\n\n${block.text.trim()}\n`);
    } else if (record.type === "assistant" && block.type === "tool_use") {
      counts.tools++;
      const description = block.input?.description ? ` — ${block.input.description}` : "";
      md.push(`<details><summary>🔧 <code>${block.name}</code>${description}</summary>\n\n\`\`\`\n${truncate(toolInput(block.input), INPUT_LIMIT)}\n\`\`\`\n`);
    } else if (record.type === "user" && block.type === "tool_result") {
      md.push(`Result${block.is_error ? " (error)" : ""}:\n\n\`\`\`\n${truncate(stripReminders(resultText(block.content)), RESULT_LIMIT)}\n\`\`\`\n</details>\n`);
    }
    // Thinking blocks are omitted from the readable transcript (they remain in session-raw.jsonl).
  }
}

const header = [
  "# AI session transcript",
  "",
  "The complete working session with the AI agent (Claude Code) that planned and built this project —",
  "requirements discussion, architecture decisions, the task plan, every review/approval round, live",
  "testing findings and fixes.",
  "",
  `- Source: \`${path.basename(source)}\` · ${first ?? "?"} → ${last ?? "?"}`,
  `- ${counts.user} user messages · ${counts.assistant} assistant replies · ${counts.tools} tool calls`,
  "- Tool calls are collapsed; long outputs are truncated. The full, unmodified log is `session-raw.jsonl`.",
  "- Secrets are masked (none were ever printed: the LLM key lives in .NET user-secrets).",
  "",
].join("\n");

fs.writeFileSync(path.join(outDir, "session-transcript.md"), mask(header + "\n" + md.join("\n")));
console.log(`Exported ${counts.user} user messages, ${counts.assistant} replies, ${counts.tools} tool calls → ${path.relative(repoRoot, outDir)}`);
