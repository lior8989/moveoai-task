// Pure presentation helpers (no DOM) — unit-tested with `node --test tests/ui`.

/** Formats a number compactly: integers as-is, otherwise up to 2 decimals. */
export function formatNumber(value) {
  if (typeof value !== "number" || Number.isNaN(value)) return "–";
  return Number.isInteger(value) ? String(value) : String(Math.round(value * 100) / 100);
}

/** Formats milliseconds as "850 ms" or "4.3 s". */
export function formatDuration(ms) {
  if (typeof ms !== "number") return "–";
  return ms < 1000 ? `${Math.round(ms)} ms` : `${(ms / 1000).toFixed(1)} s`;
}

/** Maps overall health status to a CSS modifier. */
export function healthClass(status) {
  switch (status) {
    case "Healthy": return "ok";
    case "Degraded": return "warn";
    default: return "bad";
  }
}

/** Splits camelCase/PascalCase identifiers into words: "SevereStorm" → "Severe storm". */
export function humanize(identifier) {
  const words = String(identifier ?? "").replace(/([a-z0-9])([A-Z])/g, "$1 $2").toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

/**
 * Turns an RFC 7807 problem body (or a network error) into a short message for the chat.
 * @returns {{title: string, detail: string, hint?: string}}
 */
export function describeProblem(status, problem) {
  if (status === 429) {
    return { title: "Too many questions", detail: problem?.detail ?? "Please wait a moment and try again." };
  }
  return {
    title: problem?.title ?? (status ? `Request failed (${status})` : "Network error"),
    detail: problem?.detail ?? "The server could not be reached.",
    hint: problem?.hint,
  };
}
