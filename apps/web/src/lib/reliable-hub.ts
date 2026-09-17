import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { store } from "@/lib/local-store";

type ConfigureHub = (connection: signalR.HubConnection) => void;

const retryPolicy: signalR.IRetryPolicy = {
  nextRetryDelayInMilliseconds: ({ previousRetryCount }) => {
    const backoff = Math.min(30_000, 1_000 * 2 ** Math.min(previousRetryCount, 5));
    return backoff + Math.floor(Math.random() * 1_000);
  },
};

export function useReliableBranchHub(
  url: string,
  branchId: string | null,
  configure: ConfigureHub,
  onSynchronized?: () => void,
): boolean {
  const [live, setLive] = useState(false);
  const configureRef = useRef(configure);
  configureRef.current = configure;
  const synchronizedRef = useRef(onSynchronized);
  synchronizedRef.current = onSynchronized;

  useEffect(() => {
    if (!branchId) { setLive(false); return; }

    let disposed = false;
    let startTimer: number | null = null;
    let starting = false;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(url, { accessTokenFactory: () => store.get<string>("session-token") ?? "" })
      .withAutomaticReconnect(retryPolicy)
      .build();

    configureRef.current(connection);

    const joinAndSynchronize = async () => {
      await connection.invoke("JoinBranch", branchId);
      if (disposed) return;
      setLive(true);
      synchronizedRef.current?.();
    };

    const scheduleStart = (attempt: number) => {
      if (disposed || startTimer !== null) return;
      const delay = Math.min(30_000, 1_000 * 2 ** Math.min(attempt, 5)) + Math.floor(Math.random() * 1_000);
      startTimer = window.setTimeout(() => { startTimer = null; void start(attempt + 1); }, delay);
    };

    const start = async (attempt = 0) => {
      if (disposed || starting || connection.state !== signalR.HubConnectionState.Disconnected) return;
      starting = true;
      try {
        await connection.start();
        await joinAndSynchronize();
      } catch {
        setLive(false);
        if (connection.state !== signalR.HubConnectionState.Disconnected) await connection.stop().catch(() => undefined);
        scheduleStart(attempt);
      } finally {
        starting = false;
      }
    };

    connection.onreconnecting(() => setLive(false));
    connection.onreconnected(() => { void joinAndSynchronize().catch(() => { setLive(false); void connection.stop(); }); });
    connection.onclose(() => { setLive(false); scheduleStart(0); });

    const wake = () => {
      if (document.visibilityState === "visible" && navigator.onLine) {
        synchronizedRef.current?.();
        void start();
      }
    };
    window.addEventListener("online", wake);
    window.addEventListener("focus", wake);
    document.addEventListener("visibilitychange", wake);
    void start();

    return () => {
      disposed = true;
      setLive(false);
      if (startTimer !== null) window.clearTimeout(startTimer);
      window.removeEventListener("online", wake);
      window.removeEventListener("focus", wake);
      document.removeEventListener("visibilitychange", wake);
      void connection.stop();
    };
  }, [url, branchId]);

  return live;
}
