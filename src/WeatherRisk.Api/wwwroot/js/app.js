// Chat UI controller: sends questions to POST /api/chat and renders the structured, validated answers.
import { renderMarkdown, escapeHtml } from "./markdown.js";
import { formatNumber, formatDuration, healthClass, humanize, describeProblem } from "./format.js";
import { canListen, canSpeak, listen, speak, stopSpeaking } from "./voice.js";

const EXAMPLES = [
  "Which hubs in the Midwest are most exposed to winter disruption?",
  "Compare Miami and Houston in terms of hurricane and flood exposure.",
  "What percentage of days in Denver last year had snowfall?",
  "Why is the Dallas hub's weather disruption risk high?",
];

const el = {
  log: document.getElementById("log"),
  form: document.getElementById("composer"),
  input: document.getElementById("message"),
  send: document.getElementById("send"),
  mic: document.getElementById("mic"),
  speak: document.getElementById("speak"),
  reset: document.getElementById("new-chat"),
  examples: document.getElementById("examples"),
  starter: document.querySelector(".starter"),
  health: document.getElementById("health"),
};

// The conversation id lives in memory only: after a reload the visible log is empty, so a fresh
// conversation matches what the analyst sees.
const state = {
  conversationId: null,
  busy: false,
  readAloud: false,
  stopListening: null,
};

// ---------------------------------------------------------------- rendering

function addMessage(role, html) {
  const item = document.createElement("article");
  item.className = `message ${role}`;
  item.innerHTML = html; // Callers pass escaped/sanitized HTML only.
  el.log.append(item);
  item.scrollIntoView({ behavior: "smooth", block: "end" });
  return item;
}

function chips(items, className, clickable) {
  if (!items?.length) return "";
  return `<div class="chips">${items
    .map((text) => clickable
      ? `<button type="button" class="chip ${className}" data-question="${escapeHtml(text)}">${escapeHtml(text)}</button>`
      : `<span class="chip ${className}">${escapeHtml(text)}</span>`)
    .join("")}</div>`;
}

function findingsTable(findings) {
  if (!findings?.length) return "";
  const rows = findings.map((f) => `
    <tr>
      <td>${escapeHtml(f.hubId)}</td>
      <td>${escapeHtml(f.metric)}</td>
      <td class="num">${escapeHtml(formatNumber(f.value))}</td>
      <td>${escapeHtml(f.unit)}</td>
      <td class="${f.verified ? "verified" : "unverified"}" title="${f.verified ? "Matches a tool result from this turn" : "Not found in tool results"}">
        ${f.verified ? "✓ verified" : "⚠ unverified"}</td>
    </tr>`).join("");
  return `
    <table class="findings">
      <caption>Numbers cited</caption>
      <thead><tr><th>Hub</th><th>Metric</th><th>Value</th><th>Unit</th><th>Check</th></tr></thead>
      <tbody>${rows}</tbody>
    </table>`;
}

function auditDetails(chat) {
  const tools = chat.toolCalls.map((t) => `
    <li><code>${escapeHtml(t.tool)}</code> ${t.succeeded ? "" : "<span class=\"unverified\">(failed)</span>"}
      <span class="muted">${escapeHtml(formatDuration(t.durationMs))}</span>
      <pre>${escapeHtml(JSON.stringify(t.arguments, null, 2))}</pre></li>`).join("");
  const notes = chat.verification.notes.length
    ? `<p class="unverified">${chat.verification.notes.map(escapeHtml).join("<br>")}</p>` : "";
  return `
    <details class="audit">
      <summary>How I got this</summary>
      ${notes}
      <p class="muted">Model <strong>${escapeHtml(chat.llm.provider)} / ${escapeHtml(chat.llm.model)}</strong> ·
        ${chat.llm.steps} steps · ${chat.llm.promptTokens + chat.llm.completionTokens} tokens ·
        ${escapeHtml(formatDuration(chat.llm.durationMs))}</p>
      <ol class="tools">${tools || "<li class=\"muted\">No tools needed.</li>"}</ol>
    </details>`;
}

function renderAnswer(chat) {
  const assumptions = chat.assumptions?.length
    ? `<details><summary>Assumptions</summary><ul>${chat.assumptions.map((a) => `<li>${escapeHtml(a)}</li>`).join("")}</ul></details>`
    : "";
  return `
    <div class="answer">${renderMarkdown(chat.answer)}</div>
    ${findingsTable(chat.findings)}
    ${chat.keyFactors?.length ? `<h4>Key factors</h4>${chips(chat.keyFactors, "factor", false)}` : ""}
    <p class="uncertainty"><strong>Confidence &amp; caveats:</strong> ${escapeHtml(chat.uncertainty)}</p>
    ${assumptions}
    ${chat.followUpQuestions?.length ? `<h4>Ask next</h4>${chips(chat.followUpQuestions, "follow-up", true)}` : ""}
    ${auditDetails(chat)}
    <span class="badge intent">${escapeHtml(humanize(chat.intent))}</span>`;
}

function renderError({ title, detail, hint }) {
  return `<p><strong>${escapeHtml(title)}</strong></p><p>${escapeHtml(detail)}</p>${hint ? `<p class="muted">${escapeHtml(hint)}</p>` : ""}`;
}

// ---------------------------------------------------------------- behaviour

function setBusy(busy) {
  state.busy = busy;
  el.send.disabled = busy;
  el.input.disabled = busy;
  el.form.setAttribute("aria-busy", String(busy));
}

async function ask(question) {
  const message = question.trim();
  if (!message || state.busy) return;

  el.starter.hidden = true; // Examples only on an empty conversation; follow-up chips take over.
  addMessage("user", `<p>${escapeHtml(message)}</p>`);
  el.input.value = "";
  setBusy(true);
  const pending = addMessage("assistant pending", `<p class="typing">Analyzing hub data<span>.</span><span>.</span><span>.</span></p>`);

  try {
    const response = await fetch("/api/chat", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({ message, conversationId: state.conversationId }),
    });
    const body = await response.json().catch(() => null);

    if (!response.ok) {
      pending.className = "message assistant error";
      pending.innerHTML = renderError(describeProblem(response.status, body));
      return;
    }

    state.conversationId = body.conversationId;
    pending.className = "message assistant";
    pending.innerHTML = renderAnswer(body);
    if (state.readAloud) speak(body.answer);
  } catch {
    pending.className = "message assistant error";
    pending.innerHTML = renderError(describeProblem(0, null));
  } finally {
    setBusy(false);
    el.input.focus();
  }
}

function newConversation() {
  state.conversationId = null;
  stopSpeaking();
  el.starter.hidden = false;
  el.log.innerHTML = "";
  addMessage("assistant", `<p>Hi! I rank, compare and explain how exposed our distribution hubs are to weather disruption.
    Ask a question, tap an example, or use the 🎤 microphone.</p>`);
}

function toggleListening() {
  if (state.stopListening) {
    state.stopListening();
    return;
  }
  el.mic.classList.add("listening");
  el.mic.setAttribute("aria-pressed", "true");
  state.stopListening = listen({
    onInterim: (text) => { el.input.value = text; },
    onFinal: (text) => { el.input.value = text; ask(text); },
    onError: (error) => addMessage("assistant error", renderError({ title: "Voice input", detail: `Speech recognition error: ${error}` })),
    onEnd: () => {
      state.stopListening = null;
      el.mic.classList.remove("listening");
      el.mic.setAttribute("aria-pressed", "false");
    },
  });
}

async function refreshHealth() {
  try {
    const health = await (await fetch("/api/system/health")).json();
    el.health.className = `health ${healthClass(health.status)}`;
    el.health.textContent = health.status;
    el.health.title = health.checks.map((c) => `${c.name}: ${c.status}${c.description ? ` — ${c.description}` : ""}`).join("\n");
  } catch {
    el.health.className = "health bad";
    el.health.textContent = "Offline";
  }
}

// ---------------------------------------------------------------- wiring

el.examples.innerHTML = chips(EXAMPLES, "example", true);
el.form.addEventListener("submit", (e) => { e.preventDefault(); ask(el.input.value); });
el.input.addEventListener("keydown", (e) => {
  if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); ask(el.input.value); }
});
document.addEventListener("click", (e) => {
  const chip = e.target.closest("[data-question]");
  if (chip) ask(chip.dataset.question);
});
el.reset.addEventListener("click", newConversation);

if (canListen) el.mic.addEventListener("click", toggleListening);
else el.mic.hidden = true;

if (canSpeak) {
  el.speak.addEventListener("click", () => {
    state.readAloud = !state.readAloud;
    el.speak.setAttribute("aria-pressed", String(state.readAloud));
    if (!state.readAloud) stopSpeaking();
  });
} else {
  el.speak.hidden = true;
}

newConversation();
refreshHealth();

// Deep link: /?q=<question> asks it on load (shareable/bookmarkable demo questions).
const initialQuestion = new URLSearchParams(location.search).get("q");
if (initialQuestion) ask(initialQuestion);
setInterval(refreshHealth, 30_000);
