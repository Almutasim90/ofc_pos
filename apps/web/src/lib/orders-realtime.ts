import { useEffect, useRef } from "react";
import { useReliableBranchHub } from "@/lib/reliable-hub";

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
  onSynchronized?: () => void,
): boolean {
  const receivedRef = useRef(onReceived);
  receivedRef.current = onReceived;
  const reviewedRef = useRef(onReviewed);
  reviewedRef.current = onReviewed;
  const synchronizedRef = useRef(onSynchronized);
  synchronizedRef.current = onSynchronized;

  const live = useReliableBranchHub("/hubs/orders", branchId, (connection) => {
    connection.on("qrOrderReceived", (payload: QrOrderReceivedEvent) => { if (payload.branchId === branchId) receivedRef.current?.(payload); });
    connection.on("qrOrderReviewed", (payload: QrOrderReviewedEvent) => { if (payload.branchId === branchId) reviewedRef.current?.(payload); });
  }, () => synchronizedRef.current?.());

  useEffect(() => {
    if (!branchId) return;
    const interval = window.setInterval(() => synchronizedRef.current?.(), live ? 60_000 : 10_000);
    return () => window.clearInterval(interval);
  }, [branchId, live]);

  return live;
}
