// Voice input (Web Speech API speech recognition) and read-aloud (speech synthesis).
// Both are progressive enhancements: when the browser lacks them, the UI hides the controls.

const Recognition = globalThis.SpeechRecognition ?? globalThis.webkitSpeechRecognition;

/** Whether the browser supports speech recognition (Chrome, Edge, Safari). */
export const canListen = Boolean(Recognition);

/** Whether the browser supports speech synthesis. */
export const canSpeak = "speechSynthesis" in globalThis;

/**
 * Starts listening once. Calls onInterim with partial text while speaking and onFinal with the final transcript.
 * @returns {() => void} stop function
 */
export function listen({ onInterim, onFinal, onEnd, onError, lang = "en-US" }) {
  const recognition = new Recognition();
  recognition.lang = lang;
  recognition.interimResults = true;
  recognition.maxAlternatives = 1;

  recognition.onresult = (event) => {
    let interim = "";
    let final = "";
    for (let i = event.resultIndex; i < event.results.length; i++) {
      const text = event.results[i][0].transcript;
      if (event.results[i].isFinal) final += text;
      else interim += text;
    }
    if (interim) onInterim?.(interim);
    if (final) onFinal?.(final.trim());
  };
  recognition.onerror = (event) => onError?.(event.error);
  recognition.onend = () => onEnd?.();
  recognition.start();
  return () => recognition.stop();
}

/** Reads text aloud, replacing anything currently being spoken. Markdown symbols are stripped first. */
export function speak(text, lang = "en-US") {
  if (!canSpeak) return;
  speechSynthesis.cancel();
  const plain = String(text).replace(/[*_`#>]/g, "").replace(/\s+/g, " ").trim();
  const utterance = new SpeechSynthesisUtterance(plain);
  utterance.lang = lang;
  speechSynthesis.speak(utterance);
}

/** Stops any speech in progress. */
export function stopSpeaking() {
  if (canSpeak) speechSynthesis.cancel();
}
