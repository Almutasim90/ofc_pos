import * as signalR from "@microsoft/signalr";

// SignalR's default logger prints connection URLs, and the WebSocket URL carries the session token as
// `access_token`. This logger keeps only warnings and errors and redacts the token from anything it prints,
// so a session token never ends up in the browser console (or in a pasted console log).
export const redactingSignalRLogger: signalR.ILogger = {
  log(level, message) {
    if (level < signalR.LogLevel.Warning) return;
    const safe = message.replace(
      /access_token=[^&\s'"]+/gi,
      "access_token=[redacted]",
    );
    if (level >= signalR.LogLevel.Error) console.error(`[realtime] ${safe}`);
    else console.warn(`[realtime] ${safe}`);
  },
};
