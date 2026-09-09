BEGIN;
SET search_path = ofc;

-- Clean transaction/ledger tables (dependents first) ------
DELETE FROM ofc.kitchen_ticket_items;
DELETE FROM ofc.kitchen_tickets;
DELETE FROM ofc.print_jobs;
DELETE FROM ofc.qr_order_approvals;
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
DELETE FROM ofc.waste_records;
DELETE FROM ofc.inventory_movements;
DELETE FROM ofc.stock_transfer_lines;
DELETE FROM ofc.stock_transfers;
DELETE FROM ofc.inventory_count_lines;
DELETE FROM ofc.inventory_counts;
DELETE FROM ofc.goods_receipt_lines;
DELETE FROM ofc.goods_receipts;
DELETE FROM ofc.purchase_order_lines;
DELETE FROM ofc.purchase_orders;
DELETE FROM ofc.sync_operations;
DELETE FROM ofc.sync_states;
DELETE FROM ofc.report_exports;
DELETE FROM ofc.external_outbox;
DELETE FROM ofc.audit_entries;
DELETE FROM ofc.catalog_versions;
DELETE FROM ofc.recipe_lines;
DELETE FROM ofc.recipe_versions;
DELETE FROM ofc.inventory_items;
DELETE FROM ofc.unit_conversions;
DELETE FROM ofc.units_of_measure;

-- Clean slate (idempotent) --------------------------------
DELETE FROM ofc.user_branches;
DELETE FROM ofc.user_roles;
DELETE FROM ofc.role_permissions;
DELETE FROM ofc.user_permissions;
DELETE FROM ofc.sessions;
DELETE FROM ofc.product_selection_groups;
DELETE FROM ofc.selection_group_branch_availability;
DELETE FROM ofc.selection_options;
DELETE FROM ofc.selection_groups;
DELETE FROM ofc.product_branch_availability;
DELETE FROM ofc.price_rules;
DELETE FROM ofc.promotions;
DELETE FROM ofc.tax_rules;
DELETE FROM ofc.catalog_versions;
DELETE FROM ofc.payment_methods;
DELETE FROM ofc.cancellation_reasons;
DELETE FROM ofc.pos_devices;
DELETE FROM ofc.branch_settings;
DELETE FROM ofc.branches;
DELETE FROM ofc.organizations;
DELETE FROM ofc.users;
DELETE FROM ofc.roles;
DELETE FROM ofc.permissions;
DELETE FROM ofc.product_images;
DELETE FROM ofc.products;
DELETE FROM ofc.categories;
DELETE FROM ofc.preparation_stations;
DELETE FROM ofc.sales_channels;
DELETE FROM ofc.tax_categories;

-- Organization -------------------------------------------
INSERT INTO ofc.organizations ("Id","NameAr","NameEn","CreatedAt")
VALUES ('11111111-1111-1111-1111-111111111111','مؤسسة OFC','OFC Establishment', now());

-- Branches: Suwaiq & Khaboora ----------------------------
INSERT INTO ofc.branches ("Id","OrganizationId","Code","NameAr","NameEn","TimeZone","IsActive") VALUES
('22222222-2222-2222-2222-222222222221','11111111-1111-1111-1111-111111111111','SUWAIQ','فرع السويق','Suwaiq Branch','Asia/Muscat',true),
('22222222-2222-2222-2222-222222222222','11111111-1111-1111-1111-111111111111','KHABOORA','فرع الخابورة','Khaboora Branch','Asia/Muscat',true);

-- Admin user (login: admin / Admin@123456) ---------------
INSERT INTO ofc.users ("Id","Username","Email","DisplayName","PasswordHash","PasswordSalt","IsActive","CreatedAt")
VALUES ('33333333-3333-3333-3333-333333333333','admin','admin@newofc.om','OFC Administrator',
  '\x9f45b99ba41d7c62b6c0f8be0624152c5cf8726de16e0973dba933097f5ea8c2'::bytea,
  '\x0abd7fd928ef5ee3dedb41097f246db9'::bytea, true, now());

-- Default roles: Admin, Branch Manager, Cashier ------------
INSERT INTO ofc.roles ("Id","Name") VALUES
('44444444-4444-4444-4444-444444444444','Admin'),
('44444444-4444-4444-4444-444444444445','Branch Manager'),
('44444444-4444-4444-4444-444444444446','Cashier');

-- All permissions ----------------------------------------
INSERT INTO ofc.permissions ("Id","Code","Name")
SELECT gen_random_uuid(), code, code FROM unnest(ARRAY[
'users.manage','roles.manage','branches.manage','devices.manage','settings.manage',
'catalog.categories.manage','catalog.products.manage','catalog.selection-groups.manage',
'pricing.manage','pricing.override','orders.manage','payments.manage','payment-methods.manage',
'cancellations.manage','cancellations.cancel','cancellations.void','cancellations.refund','cancellations.approve','cancellations.report',
'shifts.open','shifts.manage','shifts.close','shifts.approve','shifts.view-variance','shifts.report',
'printing.configs.manage','printing.templates.manage','printing.routes.manage','printing.jobs.manage','printing.view',
'kitchen.view','kitchen.manage','kitchen.acknowledge','kitchen.cancel',
'inventory.view','inventory.uoms.manage','inventory.items.manage','inventory.recipes.manage','inventory.movements.manage',
'inventory.counts.manage','inventory.transfers.manage','inventory.waste.manage','inventory.costing.view',
'reports.view','reports.export','audit.view','audit.manage',
'procurement.view','procurement.suppliers.manage','procurement.purchase-orders.manage','procurement.goods-receipt.manage','procurement.approve',
'qr.manage','qr.approve','integrations.view','integrations.manage','integrations.ai']) AS code;

INSERT INTO ofc.role_permissions ("RoleId","PermissionId")
SELECT '44444444-4444-4444-4444-444444444444', "Id" FROM ofc.permissions;

-- Branch Manager inherits full operational permissions (no org/global admin).
INSERT INTO ofc.role_permissions ("RoleId","PermissionId")
SELECT '44444444-4444-4444-4444-444444444445', p."Id" FROM ofc.permissions p
WHERE p."Code" IN ('catalog.categories.manage','catalog.products.manage','catalog.selection-groups.manage','pricing.manage','pricing.override','orders.manage','payments.manage','payment-methods.manage','cancellations.manage','cancellations.cancel','cancellations.void','cancellations.refund','cancellations.approve','cancellations.report','shifts.open','shifts.manage','shifts.close','shifts.approve','shifts.view-variance','shifts.report','printing.configs.manage','printing.templates.manage','printing.routes.manage','printing.jobs.manage','printing.view','kitchen.view','kitchen.manage','kitchen.acknowledge','kitchen.cancel','inventory.view','inventory.uoms.manage','inventory.items.manage','inventory.recipes.manage','inventory.movements.manage','inventory.counts.manage','inventory.transfers.manage','inventory.waste.manage','inventory.costing.view','reports.view','reports.export','procurement.view','procurement.suppliers.manage','procurement.purchase-orders.manage','procurement.goods-receipt.manage','procurement.approve','qr.manage','qr.approve');

-- Cashier inherits only point-of-sale permissions.
INSERT INTO ofc.role_permissions ("RoleId","PermissionId")
SELECT '44444444-4444-4444-4444-444444444446', p."Id" FROM ofc.permissions p
WHERE p."Code" IN ('orders.manage','payments.manage','cancellations.cancel','cancellations.void','shifts.open','shifts.close');

-- Grant admin to the two branches -------------------------
INSERT INTO ofc.user_roles ("UserId","RoleId") VALUES ('33333333-3333-3333-3333-333333333333','44444444-4444-4444-4444-444444444444');
INSERT INTO ofc.user_branches ("UserId","BranchId") VALUES
('33333333-3333-3333-3333-333333333333','22222222-2222-2222-2222-222222222221'),
('33333333-3333-3333-3333-333333333333','22222222-2222-2222-2222-222222222222');

-- Branch details ------------------------------------------
INSERT INTO ofc.branch_settings ("Id","BranchId","Key","Value") VALUES
(gen_random_uuid(),'22222222-2222-2222-2222-222222222221','currency','OMR'),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222221','tax_inclusive','false'),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222222','currency','OMR'),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222222','tax_inclusive','false');

INSERT INTO ofc.pos_devices ("Id","BranchId","Name","RegistrationCode","IsActive","LastSeenAt") VALUES
(gen_random_uuid(),'22222222-2222-2222-2222-222222222221','Suwaiq POS-01','REG-SUWAIQ-001',true,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222222','Khaboora POS-01','REG-KHABOORA-001',true,now());

-- Sales channels ------------------------------------------
INSERT INTO ofc.sales_channels ("Id","Code","NameAr","NameEn","IsActive","CreatedAt") VALUES
('aaaaaaaa-0000-0000-0000-000000000001','POS','نقطة بيع','POS',true,now()),
('aaaaaaaa-0000-0000-0000-000000000002','WEBQR','ويب / QR','Web / QR',true,now()),
('aaaaaaaa-0000-0000-0000-000000000003','DINEIN','محلي','Dine In',true,now()),
('aaaaaaaa-0000-0000-0000-000000000004','TAKEAWAY','سفري','Takeaway',true,now());

-- Tax ------------------------------------------------------
INSERT INTO ofc.tax_categories ("Id","Code","NameAr","NameEn","Rate","IsActive") VALUES
('bbbbbbbb-0000-0000-0000-000000000001','VAT','ضريبة القيمة المضافة','VAT',5.0000,true);

INSERT INTO ofc.tax_rules ("Id","TaxCategoryId","BranchId","Rate","CalculationMode","EffectiveFrom","EffectiveTo","IsActive","CreatedAt") VALUES
('bbbbbbbb-0000-0000-0000-000000000011','bbbbbbbb-0000-0000-0000-000000000001',NULL,5.0000,'Exclusive', now(), NULL, true, now());

-- Preparation stations ------------------------------------
INSERT INTO ofc.preparation_stations ("Id","Code","NameAr","NameEn","IsActive") VALUES
('cccccccc-0000-0000-0000-000000000001','KITCHEN','المطبخ','Kitchen',true),
('cccccccc-0000-0000-0000-000000000002','GRILL','الشواية','Grill',true),
('cccccccc-0000-0000-0000-000000000003','BAR','المشروبات','Bar',true);

-- Categories ----------------------------------------------
INSERT INTO ofc.categories ("Id","ParentId","NameAr","NameEn","SortOrder","ImageUrl","IsActive","CreatedAt") VALUES
('10000000-0000-0000-0000-000000000001',NULL,'وجبات عائلية','Family Meals',1,'/menu/01_Family_Meals_Page2/Big_Bucket_Crispy_21pcs.jpg',true,now()),
('10000000-0000-0000-0000-000000000002',NULL,'وجبات زنجر','Zinger Meals',2,'/menu/03_Zinger_Meals/Combo_3.jpg',true,now()),
('10000000-0000-0000-0000-000000000003',NULL,'ساندويتشات ولفائف','Sandwiches & Wraps',3,'/menu/04_Sandwich_Wraps/Royal_Zinger.jpg',true,now()),
('10000000-0000-0000-0000-000000000004',NULL,'وجبات فردية','Individual Deals',4,'/menu/05_Individual_Deals/OFC_1.jpg',true,now()),
('10000000-0000-0000-0000-000000000005',NULL,'ركن الأطفال','Kids Corner',5,'/menu/06_Kids_Corner/Kids_Burger_Meal.jpg',true,now()),
('10000000-0000-0000-0000-000000000006',NULL,'أطباق جانبية وإضافات','Sides & Extras',6,'/menu/07_Side_Menu/Fries.jpg',true,now()),
('10000000-0000-0000-0000-000000000007',NULL,'سلال الدجاج','Chicken Buckets',7,'/menu/08_Chicken_and_Pizza/Chicken_Buckets_9_12_15_Strips.jpg',true,now()),
('10000000-0000-0000-0000-000000000008',NULL,'بيتزا','Pizza',8,'/menu/08_Chicken_and_Pizza/Pepperoni_Pizza.jpg',true,now());

-- Products (real OFC menu, imported from the printed menu photos) ----------
INSERT INTO ofc.products ("Id","Sku","Barcode","NameAr","NameEn","DescriptionAr","DescriptionEn","CategoryId","Type","TaxCategoryId","PreparationStationId","BasePrice","IsActive","CreatedAt") VALUES
-- Family Meals
('20000000-0000-0000-0000-000000000001','FAM-BKT-CR21','6300000000001','الوجبة العائلية الكبيرة كرسبي','Big Bucket Crispy (21 pcs)','21 قطعة دجاج كرسبي، 5 مشروب، 8 خبز، 4 سلط و 2 شبس','21 Pcs Crispy Chicken, 5 Drinks, 8 Bun, 2 Fries, 4 Coleslaw','10000000-0000-0000-0000-000000000001','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',9.300,true,now()),
('20000000-0000-0000-0000-000000000002','FAM-BKT-GR21','6300000000002','الوجبة العائلية الكبيرة مشوي','Big Bucket Grilled (21 pcs)','21 قطعة دجاج مشوي، 5 مشروب، 8 خبز، 4 سلط و 2 شبس','21 Pcs Grilled Chicken, 5 Drinks, 8 Bun, 2 Fries, 4 Coleslaw','10000000-0000-0000-0000-000000000001','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',9.300,true,now()),
('20000000-0000-0000-0000-000000000003','FAM-BOX-CR15','6300000000003','وجبة باكيت العائلية كرسبي (15 قطعة)','Crispy Family Box (15 pcs)','15 قطعة دجاج كرسبي، 4 مشروب، 5 خبز، 2 سلط و شبس كبير','15 pcs Chicken Crispy, 4 Drinks, 5 Bun, 2 Coleslaw & Fries (L)','10000000-0000-0000-0000-000000000001','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',7.200,true,now()),
('20000000-0000-0000-0000-000000000004','FAM-BOX-GR15','6300000000004','وجبة باكيت العائلية مشوي (15 قطعة)','Grilled Family Box (15 pcs)','15 قطعة دجاج مشوي، 4 مشروب، 5 خبز، 2 سلط و شبس','15 Pcs Grilled Chicken, 4 Drinks, 5 Bun, 2 Coleslaw & Fries','10000000-0000-0000-0000-000000000001','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',7.200,true,now()),
('20000000-0000-0000-0000-000000000005','FAM-MIX-6-6','6300000000005','مكس باكيت 6+6','Mix Bucket 6+6','6 قطع دجاج، 6 قطع ستريبس، 3 مشروب، 3 خبز، 2 سلط، صوص وشبس','6 pcs Chicken, 6 pcs Strips, 3 Drinks, 3 Bun, 2 Coleslaw, Sauce and Fries','10000000-0000-0000-0000-000000000001','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',5.200,true,now()),
('20000000-0000-0000-0000-000000000006','FAM-MIX-9-9','6300000000006','مكس باكيت 9+9','Mix Bucket 9+9','9 قطع دجاج، 9 قطع ستريبس، 4 مشروب، 4 خبز، 2 سلط، صوص وشبس','9 pcs Chicken, 9 pcs Strips, 4 Drinks, 4 Bun, 2 Coleslaw, Sauce and Fries','10000000-0000-0000-0000-000000000001','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',6.700,true,now()),
('20000000-0000-0000-0000-000000000007','FAM-DRM-CR15','6300000000007','افخاذ دجاج كرسبي (15 قطعة)','Chicken Drumstick Crispy (15 pcs)','15 افخاذ دجاج كرسبي، 2 مشروب وصوص','15 pcs Chicken Drumstick Crispy, 2 Drinks and Sauce','10000000-0000-0000-0000-000000000001','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',3.500,true,now()),
('20000000-0000-0000-0000-000000000008','FAM-DRM-GR15','6300000000008','افخاذ دجاج مشوي (15 قطعة)','Chicken Drumstick Grilled (15 pcs)','15 افخاذ دجاج مشوي، 2 مشروب وصوص','15 pcs Chicken Drumstick Grill, 2 Drinks and Sauce','10000000-0000-0000-0000-000000000001','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',3.500,true,now()),
('20000000-0000-0000-0000-000000000009','FAM-BOX-CR9','6300000000009','وجبة باكيت العائلية كرسبي (9 قطع)','Crispy Family Box (9 pcs)','9 قطع دجاج كرسبي، 3 مشروب، 3 خبز، 2 سلط وشبس','9 pcs Chicken Crispy, 3 Drinks, 3 Bun, 2 Coleslaw & Fries','10000000-0000-0000-0000-000000000001','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',5.200,true,now()),
('20000000-0000-0000-0000-000000000010','FAM-STRIP-15','6300000000010','وجبة ستريبس عائلية (15 قطعة)','Family Strips Meal (15 pcs)','15 ستريبس دجاج، 3 مشروب، 3 خبز، 2 سلط، شبس وصوص','15 pcs Chicken Strips, 3 Drinks, 3 Bun, 2 Coleslaw, Fries & Sauce','10000000-0000-0000-0000-000000000001','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',5.400,true,now()),
('20000000-0000-0000-0000-000000000011','FAM-STRIP-21','6300000000011','وجبة ستريبس عائلية (21 قطعة)','Family Strips Meal (21 pcs)','21 ستريبس، 4 مشروب، 6 خبز، 3 سلط، 2 شبس كبير و 2 صوص','21 pcs Chicken Strips, 4 Drinks, 6 Bun, 3 Coleslaw, 2 Fries (L) & 2 Sauce','10000000-0000-0000-0000-000000000001','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',7.000,true,now()),
('20000000-0000-0000-0000-000000000012','FAM-BOX-GR9','6300000000012','وجبة باكيت العائلية مشوي (9 قطع)','Grilled Family Box (9 pcs)','9 قطع دجاج مشوي، 3 مشروب، 3 خبز، 2 سلط وشبس','9 Pcs Grilled Chicken, 3 Drinks, 3 Bun, 2 Coleslaw & Fries','10000000-0000-0000-0000-000000000001','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',5.200,true,now()),
-- Zinger Meals
('20000000-0000-0000-0000-000000000013','ZIN-COMBO1','6300000000013','كومبو 1','Combo 1','تندوري برجر، قطعة دجاج، بطاطا و مشروب','1 Tandoori Burger, 1 pc Chicken, Fries and Drink','10000000-0000-0000-0000-000000000002','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',2.000,true,now()),
('20000000-0000-0000-0000-000000000014','ZIN-COMBO2','6300000000014','كومبو 2','Combo 2','تويستر راب، تندوري برجر، بطاطا، مشروب','Twister Wrap, Tandoori Burger, Fries (R), Drink','10000000-0000-0000-0000-000000000002','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',2.400,true,now()),
('20000000-0000-0000-0000-000000000015','ZIN-COMBO3','6300000000015','كومبو 3','Combo 3','ميكسي زنجر، زنجر سوبريم، بطاطا، مشروب','Mexi Zinger, Zinger Supreme, Fries (R), Drink','10000000-0000-0000-0000-000000000002','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',2.700,true,now()),
('20000000-0000-0000-0000-000000000016','ZIN-COMBO4','6300000000016','كومبو 4','Combo 4','2 قطعة دجاج، تندوري برجر، ميكسي زنجر، بطاطا','2 Pcs Chicken, Tandoori Burger, Mexi Zinger, Fries (R)','10000000-0000-0000-0000-000000000002','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',2.700,true,now()),
('20000000-0000-0000-0000-000000000017','ZIN-COMBO5','6300000000017','كومبو 5','Combo 5','2 تويستر راب، وجبة برجر اطفال، تشيك بوب، بطاطا','2 Twister Wrap, Kids Burger Meal, Chicken Pop, Fries','10000000-0000-0000-0000-000000000002','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',3.100,true,now()),
('20000000-0000-0000-0000-000000000018','ZIN-CRSPY-SW','6300000000018','برجر دجاج كرسبي (ساندويتش)','Crispy Chicken Burger (Sandwich)',NULL,'Crispy chicken burger','10000000-0000-0000-0000-000000000002','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.800,true,now()),
('20000000-0000-0000-0000-000000000019','ZIN-CRSPY-ML','6300000000019','وجبة برجر دجاج كرسبي','Crispy Chicken Burger (Meal)',NULL,'Crispy chicken burger with fries and a drink','10000000-0000-0000-0000-000000000002','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.200,true,now()),
('20000000-0000-0000-0000-000000000020','ZIN-DBLBEEF-SW','6300000000020','برجر لحم دبل (ساندويتش)','Double Beef Burger (Sandwich)',NULL,'Double beef burger','10000000-0000-0000-0000-000000000002','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',1.900,true,now()),
('20000000-0000-0000-0000-000000000021','ZIN-DBLBEEF-ML','6300000000021','وجبة برجر لحم دبل','Double Beef Burger (Meal)',NULL,'Double beef burger with fries and a drink','10000000-0000-0000-0000-000000000002','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',2.400,true,now()),
('20000000-0000-0000-0000-000000000022','ZIN-DBLROY-SW','6300000000022','رويال زنجر دبل (ساندويتش)','Double Royal Zinger (Sandwich)',NULL,'Double royal zinger burger','10000000-0000-0000-0000-000000000002','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.500,true,now()),
('20000000-0000-0000-0000-000000000023','ZIN-DBLROY-ML','6300000000023','وجبة رويال زنجر دبل','Double Royal Zinger (Meal)',NULL,'Double royal zinger burger with fries and a drink','10000000-0000-0000-0000-000000000002','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',2.000,true,now()),
('20000000-0000-0000-0000-000000000024','ZIN-VEG-SW','6300000000024','برجر خضار (ساندويتش)','Vegetable Burger (Sandwich)',NULL,'Vegetable burger','10000000-0000-0000-0000-000000000002','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.700,true,now()),
('20000000-0000-0000-0000-000000000025','ZIN-VEG-ML','6300000000025','وجبة برجر خضار','Vegetable Burger (Meal)',NULL,'Vegetable burger with fries and a drink','10000000-0000-0000-0000-000000000002','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.200,true,now()),
-- Sandwiches & Wraps
('20000000-0000-0000-0000-000000000026','SW-BEEF-SW','6300000000026','برجر لحم (ساندويتش)','Beef Burger (Sandwich)',NULL,'Beef burger','10000000-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',1.200,true,now()),
('20000000-0000-0000-0000-000000000027','SW-BEEF-ML','6300000000027','وجبة برجر لحم','Beef Burger (Meal)',NULL,'Beef burger with fries and a drink','10000000-0000-0000-0000-000000000003','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',1.800,true,now()),
('20000000-0000-0000-0000-000000000028','SW-FISH-SW','6300000000028','برجر سمك (ساندويتش)','Fish Burger (Sandwich)',NULL,'Fish burger','10000000-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.200,true,now()),
('20000000-0000-0000-0000-000000000029','SW-FISH-ML','6300000000029','وجبة برجر سمك','Fish Burger (Meal)',NULL,'Fish burger with fries and a drink','10000000-0000-0000-0000-000000000003','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.600,true,now()),
('20000000-0000-0000-0000-000000000030','SW-GRILLSUP-SW','6300000000030','جريل سوبريم (ساندويتش)','Grill Supreme (Sandwich)',NULL,'Grill supreme sandwich','10000000-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',1.200,true,now()),
('20000000-0000-0000-0000-000000000031','SW-GRILLSUP-ML','6300000000031','وجبة جريل سوبريم','Grill Supreme (Meal)',NULL,'Grill supreme sandwich with fries and a drink','10000000-0000-0000-0000-000000000003','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',1.800,true,now()),
('20000000-0000-0000-0000-000000000032','SW-MEXI-SW','6300000000032','ميكسي زنجر (ساندويتش)','Mexi Zinger (Sandwich)',NULL,'Mexi zinger sandwich','10000000-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.200,true,now()),
('20000000-0000-0000-0000-000000000033','SW-MEXI-ML','6300000000033','وجبة ميكسي زنجر','Mexi Zinger (Meal)',NULL,'Mexi zinger sandwich with fries and a drink','10000000-0000-0000-0000-000000000003','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.800,true,now()),
('20000000-0000-0000-0000-000000000034','SW-ROYAL-SW','6300000000034','رويال زنجر (ساندويتش)','Royal Zinger (Sandwich)',NULL,'Royal zinger sandwich','10000000-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.100,true,now()),
('20000000-0000-0000-0000-000000000035','SW-ROYAL-ML','6300000000035','وجبة رويال زنجر','Royal Zinger (Meal)',NULL,'Royal zinger sandwich with fries and a drink','10000000-0000-0000-0000-000000000003','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.600,true,now()),
('20000000-0000-0000-0000-000000000036','SW-SHRIMP8-SNG','6300000000036','روبيان (8 قطع)','Shrimp (8 pcs)',NULL,'Fried shrimp, 8 pcs','10000000-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.100,true,now()),
('20000000-0000-0000-0000-000000000037','SW-SHRIMP8-ML','6300000000037','وجبة روبيان (8 قطع)','Shrimp Meal (8 pcs)',NULL,'Fried shrimp with fries and a drink','10000000-0000-0000-0000-000000000003','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.400,true,now()),
('20000000-0000-0000-0000-000000000038','SW-SHRTWIST-SW','6300000000038','تويستر روبيان (ساندويتش)','Shrimp Twister (Sandwich)',NULL,'Shrimp twister wrap','10000000-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.000,true,now()),
('20000000-0000-0000-0000-000000000039','SW-SHRTWIST-ML','6300000000039','وجبة تويستر روبيان','Shrimp Twister (Meal)',NULL,'Shrimp twister wrap with fries and a drink','10000000-0000-0000-0000-000000000003','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.500,true,now()),
('20000000-0000-0000-0000-000000000040','SW-TACOS-SW','6300000000040','تاكو (ساندويتش)','Tacos (Sandwich)',NULL,'Chicken tacos','10000000-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.000,true,now()),
('20000000-0000-0000-0000-000000000041','SW-TACOS-ML','6300000000041','وجبة تاكو','Tacos (Meal)',NULL,'Chicken tacos with fries and a drink','10000000-0000-0000-0000-000000000003','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.500,true,now()),
('20000000-0000-0000-0000-000000000042','SW-TANDOORI-SW','6300000000042','تندوري برجر (ساندويتش)','Tandoori Burger (Sandwich)',NULL,'Tandoori burger','10000000-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.200,true,now()),
('20000000-0000-0000-0000-000000000043','SW-TANDOORI-ML','6300000000043','وجبة تندوري برجر','Tandoori Burger (Meal)',NULL,'Tandoori burger with fries and a drink','10000000-0000-0000-0000-000000000003','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.800,true,now()),
('20000000-0000-0000-0000-000000000044','SW-FISHWRAP-SW','6300000000044','تويستر سمك (ساندويتش)','Twister Fish Wrap (Sandwich)',NULL,'Twister fish wrap','10000000-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.000,true,now()),
('20000000-0000-0000-0000-000000000045','SW-FISHWRAP-ML','6300000000045','وجبة تويستر سمك','Twister Fish Wrap (Meal)',NULL,'Twister fish wrap with fries and a drink','10000000-0000-0000-0000-000000000003','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.500,true,now()),
('20000000-0000-0000-0000-000000000046','SW-GRILLWRAP-SW','6300000000046','تويستر جريل (ساندويتش)','Twister Grill Wrap (Sandwich)',NULL,'Twister grill wrap','10000000-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',0.800,true,now()),
('20000000-0000-0000-0000-000000000047','SW-GRILLWRAP-ML','6300000000047','وجبة تويستر جريل','Twister Grill Wrap (Meal)',NULL,'Twister grill wrap with fries and a drink','10000000-0000-0000-0000-000000000003','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',1.200,true,now()),
('20000000-0000-0000-0000-000000000048','SW-TWISTER-SW','6300000000048','تويستر راب (ساندويتش)','Twister Wrap (Sandwich)',NULL,'Twister wrap','10000000-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.800,true,now()),
('20000000-0000-0000-0000-000000000049','SW-TWISTER-ML','6300000000049','وجبة تويستر راب','Twister Wrap (Meal)',NULL,'Twister wrap with fries and a drink','10000000-0000-0000-0000-000000000003','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.200,true,now()),
('20000000-0000-0000-0000-000000000050','SW-2FISH-ML','6300000000050','وجبة برجرين سمك','2 Fish Burger Meal','2 برجر سمك، 2 بطاطا و 2 مشروب','2 Fish Burger, 2 Fries & 2 Drinks','10000000-0000-0000-0000-000000000003','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',2.200,true,now()),
('20000000-0000-0000-0000-000000000051','SW-ZNGSUP-SW','6300000000051','زنجر سوبريم (ساندويتش)','Zinger Supreme (Sandwich)',NULL,'Zinger supreme sandwich','10000000-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.200,true,now()),
('20000000-0000-0000-0000-000000000052','SW-ZNGSUP-ML','6300000000052','وجبة زنجر سوبريم','Zinger Supreme (Meal)',NULL,'Zinger supreme sandwich with fries and a drink','10000000-0000-0000-0000-000000000003','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.800,true,now()),
-- Individual Deals
('20000000-0000-0000-0000-000000000053','IND-CHKNRICE','6300000000053','قطع الدجاج الكرسبي مع أرز','Crispy Chicken Rice',NULL,'Crispy chicken pieces served over rice','10000000-0000-0000-0000-000000000004','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.700,true,now()),
('20000000-0000-0000-0000-000000000054','IND-OFC1','6300000000054','او اف سي 1','OFC 1','2 قطعة دجاج، أرز و مشروب','2 Pcs Chicken, Rice & Drink','10000000-0000-0000-0000-000000000004','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.500,true,now()),
('20000000-0000-0000-0000-000000000055','IND-OFC2','6300000000055','او اف سي 2','OFC 2','تويستر راب، قطعة دجاج، بطاطا و مشروب','Twister Wrap, 1 Pc Chicken, Fries & Drink','10000000-0000-0000-0000-000000000004','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.500,true,now()),
('20000000-0000-0000-0000-000000000056','IND-OFC3','6300000000056','او اف سي 3','OFC 3','3 قطع دجاج، سلط، بطاطا، خبز و مشروب','3 Pcs Chicken, Coleslaw, Fries, Bun & Drink','10000000-0000-0000-0000-000000000004','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.900,true,now()),
('20000000-0000-0000-0000-000000000057','IND-OFC4','6300000000057','او اف سي 4','OFC 4','4 أفخاذ دجاج، بطاطا و مشروب','4 Pcs Chicken Drumstick, Fries & Drink','10000000-0000-0000-0000-000000000004','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.700,true,now()),
('20000000-0000-0000-0000-000000000058','IND-ROYSNACK','6300000000058','رويال سناك','Royal Snacks','2 قطعة دجاج، خبز، بطاطا و مشروب','2 Pc Chicken, Bun, Fries & Drink','10000000-0000-0000-0000-000000000004','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.300,true,now()),
('20000000-0000-0000-0000-000000000059','IND-SHRRICE','6300000000059','روبيان مع الأرز','Shrimp Rice',NULL,'Crispy shrimp served over rice','10000000-0000-0000-0000-000000000004','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.000,true,now()),
('20000000-0000-0000-0000-000000000060','IND-STRIPMEAL','6300000000060','وجبة ستريبس','Strips Meal','5 قطع ستريبس، سلط، بطاطا، خبز و مشروب','5 Pcs Strips, Coleslaw, Fries, Bun & Drink','10000000-0000-0000-0000-000000000004','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.900,true,now()),
('20000000-0000-0000-0000-000000000061','IND-VALUEMEAL','6300000000061','وجبة توفير','Value Meal','قطعة دجاج، 2 قطعة ستريبس، أرز و مشروب','1 Pc Chicken, 2 Pcs Strips, Rice & Drink','10000000-0000-0000-0000-000000000004','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.500,true,now()),
-- Kids Corner
('20000000-0000-0000-0000-000000000062','KID-NUGGET5','6300000000062','وجبة ناجتس (5 قطع)','Chicken Nuggets Meal (5 pcs)','5 قطع ناجتس، بطاطا و مشروب','5 Pcs Chicken Nuggets, Fries & Drink','10000000-0000-0000-0000-000000000005','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.200,true,now()),
('20000000-0000-0000-0000-000000000063','KID-POPMEAL','6300000000063','وجبة تشيك بوب','Chicken Pop Meal',NULL,'Chicken pop, fries & drink','10000000-0000-0000-0000-000000000005','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.200,true,now()),
('20000000-0000-0000-0000-000000000064','KID-BURGERMEAL','6300000000064','وجبة برجر أطفال','Kids Burger Meal',NULL,'Kids burger, fries & drink','10000000-0000-0000-0000-000000000005','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.200,true,now()),
('20000000-0000-0000-0000-000000000065','KID-TWISTGRILL','6300000000065','كيدز تويستر جريل','Kids Twister Grill',NULL,'Kids twister grill wrap, fries & drink','10000000-0000-0000-0000-000000000005','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',1.200,true,now()),
-- Sides & Extras
('20000000-0000-0000-0000-000000000066','SIDE-ADDON-TND','6300000000066','إضافة تندوري','Tandoori Add-on',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.100,true,now()),
('20000000-0000-0000-0000-000000000067','SIDE-ADDON-MJT','6300000000067','إضافة موهيتو','Mojito Add-on',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000003',0.300,true,now()),
('20000000-0000-0000-0000-000000000068','SIDE-BUN','6300000000068','خبز','Bun',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.100,true,now()),
('20000000-0000-0000-0000-000000000069','SIDE-NUGGET5','6300000000069','ناجتس (5 قطع)','Chicken Nuggets (5 pcs)',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.600,true,now()),
('20000000-0000-0000-0000-000000000070','SIDE-POP-SM','6300000000070','تشيك بوب (صغير)','Chicken Pop (Small)',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.600,true,now()),
('20000000-0000-0000-0000-000000000071','SIDE-POP-LG','6300000000071','تشيك بوب (كبير)','Chicken Pop (Large)',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.100,true,now()),
('20000000-0000-0000-0000-000000000072','SIDE-COLESLAW','6300000000072','سلط كولسلو','Coleslaw',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.500,true,now()),
('20000000-0000-0000-0000-000000000073','SIDE-SPICY-SM','6300000000073','صوص حار (صغير)','Dipping Sauce Spicy (Small)',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.150,true,now()),
('20000000-0000-0000-0000-000000000074','SIDE-SPICY-LG','6300000000074','صوص حار (كبير)','Dipping Sauce Spicy (Large)',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.300,true,now()),
('20000000-0000-0000-0000-000000000075','SIDE-DRINK','6300000000075','مشروب غازي','Drink (Can)',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000003',0.300,true,now()),
('20000000-0000-0000-0000-000000000076','SIDE-XCHEESE','6300000000076','إضافة جبن','Extra Cheese',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.100,true,now()),
('20000000-0000-0000-0000-000000000077','SIDE-FRIES-R','6300000000077','بطاطا (صغير)','Fries (Regular)',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.500,true,now()),
('20000000-0000-0000-0000-000000000078','SIDE-FRIES-L','6300000000078','بطاطا (كبير)','Fries (Large)',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.900,true,now()),
('20000000-0000-0000-0000-000000000079','SIDE-FRIESCHKN','6300000000079','بطاطا الدجاج المقرمش بالصوص','Fries Chicken with Sauce',NULL,NULL,'10000000-0000-0000-0000-000000000006','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.400,true,now()),
('20000000-0000-0000-0000-000000000080','SIDE-GARLIC-SM','6300000000080','صلصة ثوم (صغير)','Garlic Sauce (Small)',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.150,true,now()),
('20000000-0000-0000-0000-000000000081','SIDE-GARLIC-LG','6300000000081','صلصة ثوم (كبير)','Garlic Sauce (Large)',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.300,true,now()),
('20000000-0000-0000-0000-000000000082','SIDE-MOJITO','6300000000082','موهيتو','Mojito',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000003',0.600,true,now()),
('20000000-0000-0000-0000-000000000083','SIDE-TNDFRIES','6300000000083','بطاطا حارة','Tandoori Fries',NULL,NULL,'10000000-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.000,true,now()),
-- Chicken Buckets
('20000000-0000-0000-0000-000000000084','BUCKET-CHK9','6300000000084','باكيت دجاج (9 قطع)','Chicken Bucket (9 pcs)',NULL,NULL,'10000000-0000-0000-0000-000000000007','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',3.200,true,now()),
('20000000-0000-0000-0000-000000000085','BUCKET-CHK12','6300000000085','باكيت دجاج (12 قطعة)','Chicken Bucket (12 pcs)',NULL,NULL,'10000000-0000-0000-0000-000000000007','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',3.900,true,now()),
('20000000-0000-0000-0000-000000000086','BUCKET-CHK15','6300000000086','باكيت دجاج (15 قطعة)','Chicken Bucket (15 pcs)',NULL,NULL,'10000000-0000-0000-0000-000000000007','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',4.800,true,now()),
('20000000-0000-0000-0000-000000000087','BUCKET-STRIP15','6300000000087','باكيت ستريبس (15 قطعة)','Chicken Strips Bucket (15 pcs)',NULL,NULL,'10000000-0000-0000-0000-000000000007','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',2.600,true,now()),
-- Pizza
('20000000-0000-0000-0000-000000000088','PIZZA-BEEF-M','6300000000088','بيتزا لحم (وسط)','Beef Pizza (Medium)',NULL,NULL,'10000000-0000-0000-0000-000000000008','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',2.600,true,now()),
('20000000-0000-0000-0000-000000000089','PIZZA-CHKN-M','6300000000089','بيتزا دجاج (وسط)','Chicken Pizza (Medium)',NULL,NULL,'10000000-0000-0000-0000-000000000008','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',2.600,true,now()),
('20000000-0000-0000-0000-000000000090','PIZZA-MARG-M','6300000000090','بيتزا مارجريتا (وسط)','Margarita Pizza (Medium)',NULL,NULL,'10000000-0000-0000-0000-000000000008','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',2.300,true,now()),
('20000000-0000-0000-0000-000000000091','PIZZA-PEP-M','6300000000091','بيتزا بيروني (وسط)','Pepperoni Pizza (Medium)',NULL,NULL,'10000000-0000-0000-0000-000000000008','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',2.600,true,now()),
('20000000-0000-0000-0000-000000000092','PIZZA-SHRIMP-M','6300000000092','بيتزا روبيان (وسط)','Shrimp Pizza (Medium)',NULL,NULL,'10000000-0000-0000-0000-000000000008','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',2.600,true,now()),
('20000000-0000-0000-0000-000000000093','PIZZA-VEG-M','6300000000093','بيتزا خضروات (وسط)','Vegetables Pizza (Medium)',NULL,NULL,'10000000-0000-0000-0000-000000000008','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',2.300,true,now());

-- Product photos (served from the web app's /menu static assets) ----------
INSERT INTO ofc.product_images ("Id","ProductId","Url","IsPrimary","SortOrder")
SELECT gen_random_uuid(), "Id"::uuid, "ImageUrl", true, 1 FROM (VALUES
('20000000-0000-0000-0000-000000000001','/menu/01_Family_Meals_Page2/Big_Bucket_Crispy_21pcs.jpg'),
('20000000-0000-0000-0000-000000000002','/menu/01_Family_Meals_Page2/Big_Bucket_Grilled_21pcs.jpg'),
('20000000-0000-0000-0000-000000000003','/menu/01_Family_Meals_Page2/Crispy_Family_Box_15pcs.jpg'),
('20000000-0000-0000-0000-000000000004','/menu/01_Family_Meals_Page2/Grilled_Family_Box_15pcs.jpg'),
('20000000-0000-0000-0000-000000000005','/menu/01_Family_Meals_Page2/Mix_Bucket_6plus6.jpg'),
('20000000-0000-0000-0000-000000000006','/menu/01_Family_Meals_Page2/Mix_Bucket_9plus9.jpg'),
('20000000-0000-0000-0000-000000000007','/menu/02_Family_Meals_Page3/Chicken_Drumstick_Crispy_15pcs.jpg'),
('20000000-0000-0000-0000-000000000008','/menu/02_Family_Meals_Page3/Chicken_Drumstick_Grilled_15pcs.jpg'),
('20000000-0000-0000-0000-000000000009','/menu/02_Family_Meals_Page3/Crispy_Family_Box_9pcs.jpg'),
('20000000-0000-0000-0000-000000000010','/menu/02_Family_Meals_Page3/Family_Strips_Meal_15pcs.jpg'),
('20000000-0000-0000-0000-000000000011','/menu/02_Family_Meals_Page3/Family_Strips_Meal_21pcs.jpg'),
('20000000-0000-0000-0000-000000000012','/menu/02_Family_Meals_Page3/Grilled_Family_Box_9pcs.jpg'),
('20000000-0000-0000-0000-000000000013','/menu/03_Zinger_Meals/Combo_1.jpg'),
('20000000-0000-0000-0000-000000000014','/menu/03_Zinger_Meals/Combo_2.jpg'),
('20000000-0000-0000-0000-000000000015','/menu/03_Zinger_Meals/Combo_3.jpg'),
('20000000-0000-0000-0000-000000000016','/menu/03_Zinger_Meals/Combo_4.jpg'),
('20000000-0000-0000-0000-000000000017','/menu/03_Zinger_Meals/Combo_5.jpg'),
('20000000-0000-0000-0000-000000000018','/menu/03_Zinger_Meals/Crispy_Chicken_Burger.jpg'),
('20000000-0000-0000-0000-000000000019','/menu/03_Zinger_Meals/Crispy_Chicken_Burger.jpg'),
('20000000-0000-0000-0000-000000000020','/menu/03_Zinger_Meals/Double_Beef_Burger.jpg'),
('20000000-0000-0000-0000-000000000021','/menu/03_Zinger_Meals/Double_Beef_Burger.jpg'),
('20000000-0000-0000-0000-000000000022','/menu/03_Zinger_Meals/Double_Royal_Zinger.jpg'),
('20000000-0000-0000-0000-000000000023','/menu/03_Zinger_Meals/Double_Royal_Zinger.jpg'),
('20000000-0000-0000-0000-000000000024','/menu/03_Zinger_Meals/Vegetable_Burger.jpg'),
('20000000-0000-0000-0000-000000000025','/menu/03_Zinger_Meals/Vegetable_Burger.jpg'),
('20000000-0000-0000-0000-000000000026','/menu/04_Sandwich_Wraps/Beef_Burger.jpg'),
('20000000-0000-0000-0000-000000000027','/menu/04_Sandwich_Wraps/Beef_Burger.jpg'),
('20000000-0000-0000-0000-000000000028','/menu/04_Sandwich_Wraps/Fish_Burger.jpg'),
('20000000-0000-0000-0000-000000000029','/menu/04_Sandwich_Wraps/Fish_Burger.jpg'),
('20000000-0000-0000-0000-000000000030','/menu/04_Sandwich_Wraps/Grill_Supreme.jpg'),
('20000000-0000-0000-0000-000000000031','/menu/04_Sandwich_Wraps/Grill_Supreme.jpg'),
('20000000-0000-0000-0000-000000000032','/menu/04_Sandwich_Wraps/Mexi_Zinger.jpg'),
('20000000-0000-0000-0000-000000000033','/menu/04_Sandwich_Wraps/Mexi_Zinger.jpg'),
('20000000-0000-0000-0000-000000000034','/menu/04_Sandwich_Wraps/Royal_Zinger.jpg'),
('20000000-0000-0000-0000-000000000035','/menu/04_Sandwich_Wraps/Royal_Zinger.jpg'),
('20000000-0000-0000-0000-000000000036','/menu/04_Sandwich_Wraps/Shrimp_8pcs.jpg'),
('20000000-0000-0000-0000-000000000037','/menu/04_Sandwich_Wraps/Shrimp_8pcs.jpg'),
('20000000-0000-0000-0000-000000000038','/menu/04_Sandwich_Wraps/Shrimp_Twister.jpg'),
('20000000-0000-0000-0000-000000000039','/menu/04_Sandwich_Wraps/Shrimp_Twister.jpg'),
('20000000-0000-0000-0000-000000000040','/menu/04_Sandwich_Wraps/Tacos.jpg'),
('20000000-0000-0000-0000-000000000041','/menu/04_Sandwich_Wraps/Tacos.jpg'),
('20000000-0000-0000-0000-000000000042','/menu/04_Sandwich_Wraps/Tandoori_Burger.jpg'),
('20000000-0000-0000-0000-000000000043','/menu/04_Sandwich_Wraps/Tandoori_Burger.jpg'),
('20000000-0000-0000-0000-000000000044','/menu/04_Sandwich_Wraps/Twister_Fish_Wrap.jpg'),
('20000000-0000-0000-0000-000000000045','/menu/04_Sandwich_Wraps/Twister_Fish_Wrap.jpg'),
('20000000-0000-0000-0000-000000000046','/menu/04_Sandwich_Wraps/Twister_Grill_Wrap.jpg'),
('20000000-0000-0000-0000-000000000047','/menu/04_Sandwich_Wraps/Twister_Grill_Wrap.jpg'),
('20000000-0000-0000-0000-000000000048','/menu/04_Sandwich_Wraps/Twister_Wrap.jpg'),
('20000000-0000-0000-0000-000000000049','/menu/04_Sandwich_Wraps/Twister_Wrap.jpg'),
('20000000-0000-0000-0000-000000000050','/menu/04_Sandwich_Wraps/Two_Fish_Burger_Meal.jpg'),
('20000000-0000-0000-0000-000000000051','/menu/04_Sandwich_Wraps/Zinger_Supreme.jpg'),
('20000000-0000-0000-0000-000000000052','/menu/04_Sandwich_Wraps/Zinger_Supreme.jpg'),
('20000000-0000-0000-0000-000000000053','/menu/05_Individual_Deals/Crispy_Chicken_Rice.jpg'),
('20000000-0000-0000-0000-000000000054','/menu/05_Individual_Deals/OFC_1.jpg'),
('20000000-0000-0000-0000-000000000055','/menu/05_Individual_Deals/OFC_2.jpg'),
('20000000-0000-0000-0000-000000000056','/menu/05_Individual_Deals/OFC_3.jpg'),
('20000000-0000-0000-0000-000000000057','/menu/05_Individual_Deals/OFC_4.jpg'),
('20000000-0000-0000-0000-000000000058','/menu/05_Individual_Deals/Royal_Snacks.jpg'),
('20000000-0000-0000-0000-000000000059','/menu/05_Individual_Deals/Shrimp_Rice.jpg'),
('20000000-0000-0000-0000-000000000060','/menu/05_Individual_Deals/Strips_Meal.jpg'),
('20000000-0000-0000-0000-000000000061','/menu/05_Individual_Deals/Value_Meal.jpg'),
('20000000-0000-0000-0000-000000000062','/menu/06_Kids_Corner/Chicken_Nuggets_Meal_5pcs.jpg'),
('20000000-0000-0000-0000-000000000063','/menu/06_Kids_Corner/Chicken_Pop_Meal.jpg'),
('20000000-0000-0000-0000-000000000064','/menu/06_Kids_Corner/Kids_Burger_Meal.jpg'),
('20000000-0000-0000-0000-000000000065','/menu/06_Kids_Corner/Kids_Twister_Grill.jpg'),
('20000000-0000-0000-0000-000000000066','/menu/07_Side_Menu/Add_Ons_Tandoori_Mojito.jpg'),
('20000000-0000-0000-0000-000000000067','/menu/07_Side_Menu/Add_Ons_Tandoori_Mojito.jpg'),
('20000000-0000-0000-0000-000000000068','/menu/07_Side_Menu/Bun.jpg'),
('20000000-0000-0000-0000-000000000069','/menu/07_Side_Menu/Chicken_Nuggets_5pcs.jpg'),
('20000000-0000-0000-0000-000000000070','/menu/07_Side_Menu/Chicken_Pop.jpg'),
('20000000-0000-0000-0000-000000000071','/menu/07_Side_Menu/Chicken_Pop.jpg'),
('20000000-0000-0000-0000-000000000072','/menu/07_Side_Menu/Coleslaw.jpg'),
('20000000-0000-0000-0000-000000000073','/menu/07_Side_Menu/Dipping_Sauce_Spicy.jpg'),
('20000000-0000-0000-0000-000000000074','/menu/07_Side_Menu/Dipping_Sauce_Spicy.jpg'),
('20000000-0000-0000-0000-000000000075','/menu/07_Side_Menu/Drink.jpg'),
('20000000-0000-0000-0000-000000000076','/menu/07_Side_Menu/Extra_Cheese.jpg'),
('20000000-0000-0000-0000-000000000077','/menu/07_Side_Menu/Fries.jpg'),
('20000000-0000-0000-0000-000000000078','/menu/07_Side_Menu/Fries.jpg'),
('20000000-0000-0000-0000-000000000079','/menu/07_Side_Menu/Fries_Chicken_with_Sauce.jpg'),
('20000000-0000-0000-0000-000000000080','/menu/07_Side_Menu/Garlic_Sauce.jpg'),
('20000000-0000-0000-0000-000000000081','/menu/07_Side_Menu/Garlic_Sauce.jpg'),
('20000000-0000-0000-0000-000000000082','/menu/07_Side_Menu/Mojito.jpg'),
('20000000-0000-0000-0000-000000000083','/menu/07_Side_Menu/Tandoori_Fries.jpg'),
('20000000-0000-0000-0000-000000000084','/menu/08_Chicken_and_Pizza/Chicken_Buckets_9_12_15_Strips.jpg'),
('20000000-0000-0000-0000-000000000085','/menu/08_Chicken_and_Pizza/Chicken_Buckets_9_12_15_Strips.jpg'),
('20000000-0000-0000-0000-000000000086','/menu/08_Chicken_and_Pizza/Chicken_Buckets_9_12_15_Strips.jpg'),
('20000000-0000-0000-0000-000000000087','/menu/08_Chicken_and_Pizza/Chicken_Buckets_9_12_15_Strips.jpg'),
('20000000-0000-0000-0000-000000000088','/menu/08_Chicken_and_Pizza/Beef_Pizza.jpg'),
('20000000-0000-0000-0000-000000000089','/menu/08_Chicken_and_Pizza/Chicken_Pizza.jpg'),
('20000000-0000-0000-0000-000000000090','/menu/08_Chicken_and_Pizza/Margarita_Pizza.jpg'),
('20000000-0000-0000-0000-000000000091','/menu/08_Chicken_and_Pizza/Pepperoni_Pizza.jpg'),
('20000000-0000-0000-0000-000000000092','/menu/08_Chicken_and_Pizza/Shrimp_Pizza.jpg'),
('20000000-0000-0000-0000-000000000093','/menu/08_Chicken_and_Pizza/Vegetables_Pizza.jpg')
) AS photo("Id","ImageUrl");

-- Available at both branches ------------------------------
INSERT INTO ofc.product_branch_availability ("ProductId","BranchId","IsAvailable")
SELECT p."Id", b."Id", true FROM ofc.products p CROSS JOIN ofc.branches b;

-- Catalog version -----------------------------------------
INSERT INTO ofc.catalog_versions ("Id","Number","Note","PublishedAt","PublishedByUserId") VALUES
(gen_random_uuid(),1,'OFC menu import',now(),'33333333-3333-3333-3333-333333333333');

-- Payment methods (per branch) ----------------------------
INSERT INTO ofc.payment_methods ("Id","BranchId","Code","NameAr","NameEn","Kind","IsActive","SortOrder","CreatedAt") VALUES
(gen_random_uuid(),'22222222-2222-2222-2222-222222222221','CASH','نقدي','Cash','Cash',true,1,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222221','OMANNET','عمان نت','OmanNet','OmanNet',true,2,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222221','CARD','بطاقة','Card','Visa',true,3,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222221','APPLEPAY','أبل باي','Apple Pay','ApplePay',true,4,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222222','CASH','نقدي','Cash','Cash',true,1,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222222','OMANNET','عمان نت','OmanNet','OmanNet',true,2,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222222','CARD','بطاقة','Card','Visa',true,3,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222222','APPLEPAY','أبل باي','Apple Pay','ApplePay',true,4,now());

-- Cancellation reasons (per branch) -----------------------
INSERT INTO ofc.cancellation_reasons ("Id","BranchId","Code","NameAr","NameEn","RequiresNote","IsActive","SortOrder","CreatedAt") VALUES
(gen_random_uuid(),'22222222-2222-2222-2222-222222222221','CHANGED_MIND','غير رأيه','Customer changed mind',false,true,1,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222221','WRONG_ITEM','صنف خاطئ','Wrong item',false,true,2,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222221','LONG_WAIT','انتظار طويل','Long wait',false,true,3,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222221','OTHER','أخرى','Other',true,true,4,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222222','CHANGED_MIND','غير رأيه','Customer changed mind',false,true,1,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222222','WRONG_ITEM','صنف خاطئ','Wrong item',false,true,2,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222222','LONG_WAIT','انتظار طويل','Long wait',false,true,3,now()),
(gen_random_uuid(),'22222222-2222-2222-2222-222222222222','OTHER','أخرى','Other',true,true,4,now());

-- Demo inventory (units, items, one recipe) ---------------
DELETE FROM ofc.recipe_lines;
DELETE FROM ofc.recipe_versions;
DELETE FROM ofc.inventory_movements;
DELETE FROM ofc.inventory_items;
DELETE FROM ofc.unit_conversions;
DELETE FROM ofc.units_of_measure;

INSERT INTO ofc.units_of_measure ("Id","Code","NameAr","NameEn","Symbol","IsActive","SortOrder","CreatedAt") VALUES
('99999999-0000-0000-0000-000000000001','KG','كيلوغرام','Kilogram','kg',true,1,now()),
('99999999-0000-0000-0000-000000000002','PCS','قطعة','Piece','pcs',true,2,now()),
('99999999-0000-0000-0000-000000000003','L','لتر','Litre','L',true,3,now());

INSERT INTO ofc.inventory_items ("Id","Sku","Barcode","NameAr","NameEn","DescriptionAr","DescriptionEn","Type","UnitCost","StockOnHand","BaseUnitId","IsActive","CreatedAt") VALUES
('88888888-0000-0000-0000-000000000001','RAW-CHKN','6200000000018','دجاج (كغ)','Chicken (kg)',NULL,NULL,'RawMaterial',2.0000,50,'99999999-0000-0000-0000-000000000001',true,now()),
('88888888-0000-0000-0000-000000000002','RAW-BEEF','6200000000025','لحم (كغ)','Beef (kg)',NULL,NULL,'RawMaterial',3.5000,40,'99999999-0000-0000-0000-000000000001',true,now()),
('88888888-0000-0000-0000-000000000003','RAW-POTATO','6200000000032','بطاطس (كغ)','Potato (kg)',NULL,NULL,'RawMaterial',0.8000,120,'99999999-0000-0000-0000-000000000001',true,now());

INSERT INTO ofc.recipe_versions ("Id","ProductId","VersionNumber","NameAr","NameEn","Status","EffectiveFrom","CreatedByUserId","CreatedAt") VALUES
('77777777-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000018',1,'برجر دجاج كرسبي','Crispy Chicken Burger','Active',now(),'33333333-3333-3333-3333-333333333333',now());

INSERT INTO ofc.recipe_lines ("Id","RecipeVersionId","InventoryItemId","UnitId","Quantity") VALUES
(gen_random_uuid(),'77777777-0000-0000-0000-000000000001','88888888-0000-0000-0000-000000000001','99999999-0000-0000-0000-000000000001',0.200);

COMMIT;
