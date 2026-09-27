import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { redactingSignalRLogger } from "@/lib/signalr-logger";
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

  const live = useReliableBranchHub(
    "/hubs/orders",
    branchId,
    (connection) => {
      connection.on("qrOrderReceived", (payload: QrOrderReceivedEvent) => {
        if (payload.branchId === branchId) receivedRef.current?.(payload);
      });
      connection.on("qrOrderReviewed", (payload: QrOrderReviewedEvent) => {
        if (payload.branchId === branchId) reviewedRef.current?.(payload);
      });
    },
    () => synchronizedRef.current?.(),
  );

  useEffect(() => {
    if (!branchId) return;
    const interval = window.setInterval(
      () => synchronizedRef.current?.(),
      live ? 60_000 : 10_000,
    );
    return () => window.clearInterval(interval);
  }, [branchId, live]);

  return live;
}

export function useCustomerOrderLive(
  contextCode: string,
  clientRequestId: string | null,
  onChanged: () => void,
): boolean {
  const [live, setLive] = useState(false);
  const changedRef = useRef(onChanged);
  changedRef.current = onChanged;

  useEffect(() => {
    if (!contextCode || !clientRequestId) {
      setLive(false);
      return;
    }
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/customer-orders")
      .withAutomaticReconnect()
      .configureLogging(redactingSignalRLogger)
      .build();
    let disposed = false;
    connection.on("orderChanged", () => changedRef.current());
    const join = async () => {
      await connection.invoke("JoinOrder", contextCode, clientRequestId);
      if (!disposed) {
        setLive(true);
        changedRef.current();
      }
    };
    connection.onreconnecting(() => setLive(false));
    connection.onreconnected(() => {
      void join();
    });
    connection.onclose(() => setLive(false));
    void connection
      .start()
      .then(join)
      .catch(() => setLive(false));
    return () => {
      disposed = true;
      setLive(false);
      void connection.stop();
    };
  }, [contextCode, clientRequestId]);

  return live;
}
