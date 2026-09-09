import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { store } from "@/lib/local-store";

export type QrOrderReceivedEvent = {
  branchId: string;
  orderId: string;
  clientRequestId: string;
  status: string;
  approvalStatus: string;
  grossAmount: number;
  contextCode: string | null;
};

export type QrOrderReviewedEvent = {
  branchId: string;
  orderId: string;
  clientRequestId: string;
  status: string;
  approvalStatus: string;
};

// Realtime QR-order events for staff screens. SignalR is transport only, never the source of truth
// (docs/01-ARCHITECTURE-GUARDRAILS.md): a screen uses these events to refresh its REST-backed lists and
// to surface a notification — it never trusts order state carried over the socket.
export function useQrOrdersLive(
  branchId: string | null,
  onReceived?: (payload: QrOrderReceivedEvent) => void,
  onReviewed?: (payload: QrOrderReviewedEvent) => void,
): boolean {
  const [live, setLive] = useState(false);
  const receivedRef = useRef(onReceived);
  receivedRef.current = onReceived;
  const reviewedRef = useRef(onReviewed);
  reviewedRef.current = onReviewed;

  useEffect(() => {
    if (!branchId) return;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/orders", { accessTokenFactory: () => store.get<string>("session-token") ?? "" })
      .withAutomaticReconnect()
      .build();
    connection.on("qrOrderReceived", (payload: QrOrderReceivedEvent) => { if (payload.branchId === branchId) receivedRef.current?.(payload); });
    connection.on("qrOrderReviewed", (payload: QrOrderReviewedEvent) => { if (payload.branchId === branchId) reviewedRef.current?.(payload); });
    connection.onreconnected(() => { setLive(true); void connection.invoke("JoinBranch", branchId); });
    connection.onreconnecting(() => setLive(false));
    connection.onclose(() => setLive(false));
    connection.start().then(() => { setLive(true); return connection.invoke("JoinBranch", branchId); }).catch(() => setLive(false));
    return () => { setLive(false); void connection.stop(); };
  }, [branchId]);

  return live;
}
