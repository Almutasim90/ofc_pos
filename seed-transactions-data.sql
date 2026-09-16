-- Realistic historical sales test data ------------------------------------
-- Generates ~75 days of orders/payments/shifts ending 2026-09-15 (the day
-- before "today" in this environment), for both seeded branches, so the
-- reports/pagination/dashboards have real-looking data to exercise.
--
-- Prerequisite: seed-dev-data.sql has already been run (branches, products,
-- sales channels, payment methods, tax rule, cancellation reasons must
-- exist). This script only ADDS transactional data on top of that; it does
-- not touch catalog/master data. Safe to re-run: it clears its own output
-- (orders/payments/shifts/etc. and the "cashier.*" demo staff it creates)
-- before regenerating.
--
-- What this seeds, per branch, per day in the range:
--   - One shift (opened 09:00, closed 23:30 Asia/Muscat) assigned to a
--     random demo cashier for that branch, with cash/card totals and a
--     small random cash variance computed from the day's actual payments.
--   - A realistic number of orders (more on Fri/Sat), spread across the
--     POS/Dine-in/Takeaway/QR channels, each with 1-4 real product lines
--     at real menu prices and 5% VAT (matching the seeded tax rule).
--   - ~92% Completed (captured payment + financial transaction), ~5%
--     Cancelled before payment, ~3% Refunded (captured then fully refunded
--     the same day, with the matching negative reversal ledger entry).
--
-- Simplifications made deliberately to keep this readable and fast:
--   - No PartiallyRefunded orders, no kitchen tickets/QR approvals, no
--     inventory deductions (only one demo product has a recipe today).
--   - Refunds happen the same day/shift as the sale (not days later).
--   - Every order has exactly one full payment (no split tenders).
BEGIN;
SET search_path = ofc;

-- Clean previously generated output so this script is safe to re-run -----
DELETE FROM ofc.qr_order_approvals;
DELETE FROM ofc.kitchen_ticket_items;
DELETE FROM ofc.kitchen_tickets;
DELETE FROM ofc.print_jobs WHERE "OrderId" IS NOT NULL;
DELETE FROM ofc.financial_transactions;
DELETE FROM ofc.payment_status_history;
DELETE FROM ofc.payments;
DELETE FROM ofc.refunds;
DELETE FROM ofc.order_line_voids;
DELETE FROM ofc.order_cancellations;
DELETE FROM ofc.order_status_history;
DELETE FROM ofc.order_lines;
DELETE FROM ofc.orders;
DELETE FROM ofc.shift_denominations;
DELETE FROM ofc.shift_movements;
DELETE FROM ofc.shifts;
DELETE FROM ofc.user_roles WHERE "UserId" IN (SELECT "Id" FROM ofc.users WHERE "Username" LIKE 'cashier.%');
DELETE FROM ofc.user_branches WHERE "UserId" IN (SELECT "Id" FROM ofc.users WHERE "Username" LIKE 'cashier.%');
DELETE FROM ofc.users WHERE "Username" LIKE 'cashier.%';

-- Demo cashier staff (2 per branch) so historical shifts/orders are -------
-- attributed to realistic-looking people rather than only the admin. They
-- reuse the seeded admin's password hash/salt: nobody needs to log in as
-- them, they exist only so the generated history looks like real staff.
INSERT INTO ofc.users ("Id","Username","Email","DisplayName","PasswordHash","PasswordSalt","IsActive","CreatedAt")
SELECT gen_random_uuid(), u.username, NULL, u.display_name, a."PasswordHash", a."PasswordSalt", true, now() - interval '150 days'
FROM (VALUES
  ('cashier.suwaiq1', 'فاطمة السعدية'),
  ('cashier.suwaiq2', 'خالد الرواحي'),
  ('cashier.khaboora1', 'مريم البلوشية'),
  ('cashier.khaboora2', 'يوسف الحارثي')
) AS u(username, display_name)
CROSS JOIN (SELECT "PasswordHash", "PasswordSalt" FROM ofc.users WHERE "Username" = 'admin') a;

INSERT INTO ofc.user_roles ("UserId", "RoleId")
SELECT u."Id", r."Id" FROM ofc.users u CROSS JOIN ofc.roles r
WHERE u."Username" LIKE 'cashier.%' AND r."Name" = 'Cashier';

INSERT INTO ofc.user_branches ("UserId", "BranchId")
SELECT u."Id", b."Id" FROM ofc.users u JOIN ofc.branches b
  ON (u."Username" LIKE 'cashier.suwaiq%' AND b."Code" = 'SUWAIQ')
  OR (u."Username" LIKE 'cashier.khaboora%' AND b."Code" = 'KHABOORA')
WHERE u."Username" LIKE 'cashier.%';

-- Main generator -----------------------------------------------------------
DO $seed$
DECLARE
  v_start_date date := DATE '2026-09-15' - INTERVAL '74 days';
  v_end_date date := DATE '2026-09-15';
  v_day date;
  v_branch RECORD;
  v_cashiers uuid[];
  v_product_ids uuid[];
  v_channel_pos uuid;
  v_channel_dinein uuid;
  v_channel_takeaway uuid;
  v_channel_webqr uuid;
  v_tax_rate numeric := 5.0000;
  v_tax_rule_id uuid;
  v_catalog_version_id uuid;
  v_catalog_version_number int;
  v_reason_ids uuid[];
  v_shift_id uuid;
  v_shift_cashier uuid;
  v_shift_open timestamptz;
  v_shift_close timestamptz;
  v_orders_count int;
  v_i int;
  v_j int;
  v_line_count int;
  v_order_id uuid;
  v_order_time timestamptz;
  v_last_change timestamptz;
  v_channel_id uuid;
  v_source text;
  v_status text;
  v_net numeric;
  v_tax numeric;
  v_gross numeric;
  v_unit_net numeric;
  v_unit_tax numeric;
  v_unit_gross numeric;
  v_qty int;
  v_product_id uuid;
  v_product_name_ar text;
  v_product_name_en text;
  v_product_price numeric;
  v_created_by uuid;
  v_payment_id uuid;
  v_pm_id uuid;
  v_pm_kind text;
  v_tendered numeric;
  v_change numeric;
  v_sale_txn_id uuid;
  v_reason_id uuid;
  v_r numeric;
  v_shift_cash_sales numeric;
  v_shift_card_sales numeric;
  v_shift_cash_refunds numeric;
  v_shift_card_refunds numeric;
  v_actual_cash numeric;
  v_expected_cash numeric;
BEGIN
  IF v_start_date >= v_end_date THEN RAISE EXCEPTION 'bad date range'; END IF;

  SELECT "Id" INTO v_channel_pos FROM ofc.sales_channels WHERE "Code" = 'POS';
  SELECT "Id" INTO v_channel_dinein FROM ofc.sales_channels WHERE "Code" = 'DINEIN';
  SELECT "Id" INTO v_channel_takeaway FROM ofc.sales_channels WHERE "Code" = 'TAKEAWAY';
  SELECT "Id" INTO v_channel_webqr FROM ofc.sales_channels WHERE "Code" = 'WEBQR';
  SELECT "Id" INTO v_tax_rule_id FROM ofc.tax_rules WHERE "TaxCategoryId" = (SELECT "Id" FROM ofc.tax_categories WHERE "Code" = 'VAT') LIMIT 1;
  SELECT "Id", "Number" INTO v_catalog_version_id, v_catalog_version_number FROM ofc.catalog_versions ORDER BY "Number" DESC LIMIT 1;
  SELECT array_agg("Id") INTO v_product_ids FROM ofc.products WHERE "IsActive" = true;

  IF v_channel_pos IS NULL OR v_tax_rule_id IS NULL OR v_product_ids IS NULL THEN
    RAISE EXCEPTION 'Master data missing — run seed-dev-data.sql first.';
  END IF;

  FOR v_branch IN SELECT "Id", "Code" FROM ofc.branches LOOP
    SELECT array_agg(u."Id") INTO v_cashiers
      FROM ofc.users u JOIN ofc.user_branches ub ON ub."UserId" = u."Id"
      WHERE ub."BranchId" = v_branch."Id" AND u."Username" LIKE 'cashier.%';
    SELECT array_agg("Id") INTO v_reason_ids FROM ofc.cancellation_reasons WHERE "BranchId" = v_branch."Id";

    v_day := v_start_date;
    WHILE v_day <= v_end_date LOOP
      v_shift_cashier := v_cashiers[1 + floor(random() * array_length(v_cashiers, 1))::int];
      v_shift_open := (v_day::timestamp + TIME '09:00') AT TIME ZONE 'Asia/Muscat';
      v_shift_close := (v_day::timestamp + TIME '23:30') AT TIME ZONE 'Asia/Muscat';
      v_shift_id := gen_random_uuid();

      INSERT INTO ofc.shifts ("Id", "BranchId", "OpenedByUserId", "DeviceId", "OpenedAt", "OpeningCash", "Status", "ClosedByUserId", "ClosedAt", "ReviewStatus")
      VALUES (v_shift_id, v_branch."Id", v_shift_cashier, NULL, v_shift_open, round((50 + random() * 50)::numeric, 4), 'Closed', v_shift_cashier, v_shift_close, 'NotReviewed');

      v_orders_count := CASE WHEN extract(isodow FROM v_day) IN (5, 6) THEN 35 + floor(random() * 20)::int ELSE 20 + floor(random() * 15)::int END;
      v_shift_cash_sales := 0; v_shift_card_sales := 0; v_shift_cash_refunds := 0; v_shift_card_refunds := 0;

      FOR v_i IN 1..v_orders_count LOOP
        v_order_time := v_shift_open + (random() * extract(epoch FROM (v_shift_close - v_shift_open))) * INTERVAL '1 second';
        v_r := random();
        v_channel_id := CASE WHEN v_r < 0.55 THEN v_channel_pos WHEN v_r < 0.75 THEN v_channel_dinein WHEN v_r < 0.90 THEN v_channel_takeaway ELSE v_channel_webqr END;
        v_source := CASE WHEN v_channel_id = v_channel_webqr THEN 'Qr' ELSE 'Pos' END;
        v_created_by := CASE WHEN v_source = 'Qr' THEN NULL ELSE v_shift_cashier END;

        v_order_id := gen_random_uuid();
        INSERT INTO ofc.orders ("Id", "BranchId", "SalesChannelId", "DeviceId", "CreatedByUserId", "CustomerId", "ClientRequestId", "Source", "Status", "Note", "NetAmount", "TaxAmount", "GrossAmount", "CreatedAt", "UpdatedAt")
        VALUES (v_order_id, v_branch."Id", v_channel_id, NULL, v_created_by, NULL, gen_random_uuid(), v_source, 'Draft', NULL, 0, 0, 0, v_order_time, v_order_time);

        v_line_count := 1 + floor(random() * 4)::int;
        FOR v_j IN 1..v_line_count LOOP
          v_product_id := v_product_ids[1 + floor(random() * array_length(v_product_ids, 1))::int];
          SELECT "NameAr", "NameEn", "BasePrice" INTO v_product_name_ar, v_product_name_en, v_product_price FROM ofc.products WHERE "Id" = v_product_id;
          v_qty := CASE WHEN random() < 0.8 THEN 1 ELSE 2 END;
          v_unit_net := coalesce(v_product_price, 1.000);
          v_unit_tax := round(v_unit_net * v_tax_rate / 100, 4);
          v_unit_gross := v_unit_net + v_unit_tax;

          INSERT INTO ofc.order_lines ("Id", "OrderId", "ProductId", "ProductNameAr", "ProductNameEn", "Quantity", "VoidedQuantity", "Note", "SelectionsSnapshot", "UnitListAmount", "UnitDiscountAmount", "UnitNetAmount", "UnitTaxAmount", "UnitGrossAmount", "TaxRate", "TaxCalculationMode", "PriceSource", "PriceRuleId", "PromotionId", "TaxRuleId", "CatalogVersionId", "CatalogVersionNumber")
          VALUES (gen_random_uuid(), v_order_id, v_product_id, v_product_name_ar, v_product_name_en, v_qty, 0, NULL, '[]', v_unit_net, 0, v_unit_net, v_unit_tax, v_unit_gross, v_tax_rate, 'Exclusive', 'Default', NULL, NULL, v_tax_rule_id, v_catalog_version_id, v_catalog_version_number);
        END LOOP;

        SELECT round(sum("UnitNetAmount" * "Quantity"), 4), round(sum("UnitTaxAmount" * "Quantity"), 4), round(sum("UnitGrossAmount" * "Quantity"), 4)
          INTO v_net, v_tax, v_gross FROM ofc.order_lines WHERE "OrderId" = v_order_id;

        v_r := random();
        v_last_change := v_order_time;

        INSERT INTO ofc.order_status_history ("Id", "OrderId", "FromStatus", "ToStatus", "ChangedByUserId", "ChangedAt", "Note")
        VALUES (gen_random_uuid(), v_order_id, 'Draft', 'Pending', v_created_by, v_order_time, NULL);

        IF v_r < 0.05 THEN
          -- Cancelled before payment.
          v_status := 'Cancelled';
          v_last_change := v_order_time + (INTERVAL '1 minute' * (1 + random() * 4));
          v_reason_id := v_reason_ids[1 + floor(random() * array_length(v_reason_ids, 1))::int];

          INSERT INTO ofc.order_status_history ("Id", "OrderId", "FromStatus", "ToStatus", "ChangedByUserId", "ChangedAt", "Note")
          VALUES (gen_random_uuid(), v_order_id, 'Pending', 'Cancelled', v_created_by, v_last_change, NULL);

          INSERT INTO ofc.order_cancellations ("Id", "OrderId", "CancellationReasonId", "BranchId", "CancelledByUserId", "DeviceId", "ShiftId", "Note", "OrderTotal", "OrderStatusAtCancellation", "WasSentToKitchen", "ReturnInventory", "ApprovedByUserId", "CancelledAt")
          VALUES (gen_random_uuid(), v_order_id, v_reason_id, v_branch."Id", coalesce(v_created_by, v_shift_cashier), NULL, v_shift_id, NULL, v_gross, 'Pending', false, false, NULL, v_last_change);
        ELSE
          -- Paid (captured payment), then either stays Completed or gets fully Refunded.
          v_last_change := v_order_time + (INTERVAL '1 minute' * (5 + random() * 25));
          INSERT INTO ofc.order_status_history ("Id", "OrderId", "FromStatus", "ToStatus", "ChangedByUserId", "ChangedAt", "Note")
          VALUES (gen_random_uuid(), v_order_id, 'Pending', 'Paid', v_created_by, v_last_change, NULL);

          v_r := random();
          v_pm_id := NULL;
          IF v_r < 0.45 THEN SELECT "Id" INTO v_pm_id FROM ofc.payment_methods WHERE "BranchId" = v_branch."Id" AND "Code" = 'CASH';
          ELSIF v_r < 0.65 THEN SELECT "Id" INTO v_pm_id FROM ofc.payment_methods WHERE "BranchId" = v_branch."Id" AND "Code" = 'OMANNET';
          ELSIF v_r < 0.90 THEN SELECT "Id" INTO v_pm_id FROM ofc.payment_methods WHERE "BranchId" = v_branch."Id" AND "Code" = 'CARD';
          ELSE SELECT "Id" INTO v_pm_id FROM ofc.payment_methods WHERE "BranchId" = v_branch."Id" AND "Code" = 'APPLEPAY';
          END IF;
          SELECT "Kind" INTO v_pm_kind FROM ofc.payment_methods WHERE "Id" = v_pm_id;

          IF v_pm_kind = 'Cash' THEN v_tendered := ceil(v_gross); v_change := round(v_tendered - v_gross, 4);
          ELSE v_tendered := v_gross; v_change := 0;
          END IF;

          v_payment_id := gen_random_uuid();
          INSERT INTO ofc.payments ("Id", "OrderId", "BranchId", "PaymentMethodId", "ClientRequestId", "Amount", "TenderedAmount", "ChangeAmount", "Status", "ProviderReference", "CreatedByUserId", "DeviceId", "CreatedAt")
          VALUES (v_payment_id, v_order_id, v_branch."Id", v_pm_id, gen_random_uuid(), v_gross, v_tendered, v_change, 'Captured', NULL, coalesce(v_created_by, v_shift_cashier), NULL, v_order_time + INTERVAL '2 minutes');

          INSERT INTO ofc.payment_status_history ("Id", "PaymentId", "FromStatus", "ToStatus", "ChangedByUserId", "ChangedAt", "Note")
          VALUES (gen_random_uuid(), v_payment_id, 'Pending', 'Captured', coalesce(v_created_by, v_shift_cashier), v_order_time + INTERVAL '2 minutes', NULL);

          v_sale_txn_id := gen_random_uuid();
          INSERT INTO ofc.financial_transactions ("Id", "OrderId", "PaymentId", "BranchId", "PaymentMethodId", "DeviceId", "ShiftId", "Type", "Amount", "Reference", "CreatedByUserId", "CreatedAt", "ReversalReferenceId")
          VALUES (v_sale_txn_id, v_order_id, v_payment_id, v_branch."Id", v_pm_id, NULL, v_shift_id, 'Sale', v_gross, 'TXN-' || left(v_order_id::text, 8), coalesce(v_created_by, v_shift_cashier), v_order_time + INTERVAL '2 minutes', NULL);

          IF v_pm_kind = 'Cash' THEN v_shift_cash_sales := v_shift_cash_sales + v_gross; ELSE v_shift_card_sales := v_shift_card_sales + v_gross; END IF;

          IF v_r < 0.03 THEN
            -- ~3% of paid orders are fully refunded the same day.
            v_status := 'Refunded';
            v_reason_id := v_reason_ids[1 + floor(random() * array_length(v_reason_ids, 1))::int];
            v_last_change := v_last_change + (INTERVAL '1 minute' * (10 + random() * 90));

            INSERT INTO ofc.order_status_history ("Id", "OrderId", "FromStatus", "ToStatus", "ChangedByUserId", "ChangedAt", "Note")
            VALUES (gen_random_uuid(), v_order_id, 'Paid', 'Refunded', coalesce(v_created_by, v_shift_cashier), v_last_change, NULL);

            INSERT INTO ofc.refunds ("Id", "OrderId", "PaymentId", "ClientRequestId", "CancellationReasonId", "BranchId", "RefundedByUserId", "DeviceId", "Amount", "Note", "ReturnInventory", "ApprovedByUserId", "RefundedAt")
            VALUES (gen_random_uuid(), v_order_id, v_payment_id, gen_random_uuid(), v_reason_id, v_branch."Id", v_shift_cashier, NULL, v_gross, NULL, false, v_shift_cashier, v_last_change);

            INSERT INTO ofc.financial_transactions ("Id", "OrderId", "PaymentId", "BranchId", "PaymentMethodId", "DeviceId", "ShiftId", "Type", "Amount", "Reference", "CreatedByUserId", "CreatedAt", "ReversalReferenceId")
            VALUES (gen_random_uuid(), v_order_id, v_payment_id, v_branch."Id", v_pm_id, NULL, v_shift_id, 'Refund', -v_gross, 'REFUND-' || left(v_order_id::text, 8), v_shift_cashier, v_last_change, v_sale_txn_id);

            IF v_pm_kind = 'Cash' THEN v_shift_cash_refunds := v_shift_cash_refunds + v_gross; ELSE v_shift_card_refunds := v_shift_card_refunds + v_gross; END IF;
          ELSE
            v_status := 'Completed';
            v_last_change := v_last_change + (INTERVAL '1 minute' * (10 + random() * 20));
            INSERT INTO ofc.order_status_history ("Id", "OrderId", "FromStatus", "ToStatus", "ChangedByUserId", "ChangedAt", "Note")
            VALUES (gen_random_uuid(), v_order_id, 'Paid', 'Completed', coalesce(v_created_by, v_shift_cashier), v_last_change, NULL);
          END IF;
        END IF;

        UPDATE ofc.orders SET "NetAmount" = v_net, "TaxAmount" = v_tax, "GrossAmount" = v_gross, "Status" = v_status, "UpdatedAt" = v_last_change WHERE "Id" = v_order_id;
      END LOOP;

      -- Blind-close reconciliation: a small random cash variance keeps the
      -- shift-variance report meaningful instead of always showing zero.
      v_expected_cash := round((SELECT "OpeningCash" FROM ofc.shifts WHERE "Id" = v_shift_id) + v_shift_cash_sales - v_shift_cash_refunds, 4);
      v_actual_cash := round((v_expected_cash + (random() * 2 - 1))::numeric, 3);
      UPDATE ofc.shifts SET
        "CashSales" = v_shift_cash_sales, "CardSales" = v_shift_card_sales,
        "CashRefunds" = v_shift_cash_refunds, "CardRefunds" = v_shift_card_refunds,
        "CashInTotal" = 0, "CashOutTotal" = 0, "PettyCashTotal" = 0, "CashDropsTotal" = 0,
        "ExpectedCash" = v_expected_cash, "CardExpectedTotal" = v_shift_card_sales - v_shift_card_refunds,
        "ActualCash" = v_actual_cash, "ActualCardTotal" = v_shift_card_sales - v_shift_card_refunds,
        "CashVariance" = round(v_actual_cash - v_expected_cash, 4), "CardVariance" = 0,
        "ReviewStatus" = 'Approved', "ReviewedByUserId" = v_shift_cashier, "ReviewedAt" = v_shift_close
      WHERE "Id" = v_shift_id;

      v_day := v_day + 1;
    END LOOP;
  END LOOP;
END $seed$;

COMMIT;
