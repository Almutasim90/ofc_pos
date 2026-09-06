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

-- Admin role ----------------------------------------------
INSERT INTO ofc.roles ("Id","Name") VALUES ('44444444-4444-4444-4444-444444444444','Admin');

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
('dddddddd-0000-0000-0000-000000000001',NULL,'وجبات ساخنة','Hot Meals',1,'https://placehold.co/800x600',true,now()),
('dddddddd-0000-0000-0000-000000000002',NULL,'برجر','Burgers',2,'https://placehold.co/800x600',true,now()),
('dddddddd-0000-0000-0000-000000000003',NULL,'دجاج مقلي','Fried Chicken',3,'https://placehold.co/800x600',true,now()),
('dddddddd-0000-0000-0000-000000000004',NULL,'مشروبات وعصائر','Drinks & Juices',4,'https://placehold.co/800x600',true,now()),
('dddddddd-0000-0000-0000-000000000005',NULL,'حلويات','Desserts',5,'https://placehold.co/800x600',true,now()),
('dddddddd-0000-0000-0000-000000000006',NULL,'أطباق جانبية','Sides',6,'https://placehold.co/800x600',true,now());

-- Products -------------------------------------------------
INSERT INTO ofc.products ("Id","Sku","Barcode","NameAr","NameEn","DescriptionAr","DescriptionEn","CategoryId","Type","TaxCategoryId","PreparationStationId","BasePrice","IsActive","CreatedAt") VALUES
('eeeeeeee-0000-0000-0000-000000000001','CHK-SHAWARMA','6100000000011','شاورما دجاج','Chicken Shawarma','شاورما دجاج طازجة','Fresh chicken shawarma','dddddddd-0000-0000-0000-000000000001','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',2.500,true,now()),
('eeeeeeee-0000-0000-0000-000000000002','MIXED-GRILL','6100000000028','مشاوي مشكل','Mixed Grill Platter','تشكيلة لحم ودجاج مشوي','Grilled meat and chicken platter','dddddddd-0000-0000-0000-000000000001','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',6.000,true,now()),
('eeeeeeee-0000-0000-0000-000000000003','BEEF-BURGER','6100000000035','برجر لحم','Beef Burger','برجر لحم طازج مع خضار','Fresh beef burger','dddddddd-0000-0000-0000-000000000002','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',3.000,true,now()),
('eeeeeeee-0000-0000-0000-000000000004','CHK-BURGER','6100000000042','برجر دجاج','Chicken Burger','برجر دجاج مقرمش','Crispy chicken burger','dddddddd-0000-0000-0000-000000000002','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',2.700,true,now()),
('eeeeeeee-0000-0000-0000-000000000005','CRISPY-CHKN','6100000000059','دجاج كرسبي','Crispy Chicken','دجاج كرسبي مقلي','Crispy fried chicken','dddddddd-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',3.500,true,now()),
('eeeeeeee-0000-0000-0000-000000000006','NUGGETS','6100000000066','ناغتس دجاج','Chicken Nuggets','قطع ناغتس دجاج','Chicken nuggets','dddddddd-0000-0000-0000-000000000003','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',2.200,true,now()),
('eeeeeeee-0000-0000-0000-000000000007','COLA','6100000000073','كولا','Cola','مشروب غازي بارد','Cold soft drink','dddddddd-0000-0000-0000-000000000004','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000003',0.500,true,now()),
('eeeeeeee-0000-0000-0000-000000000008','OJ','6100000000080','عصير برتقال طازج','Fresh Orange Juice','عصير برتقال طبيعي','Fresh orange juice','dddddddd-0000-0000-0000-000000000004','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000003',1.200,true,now()),
('eeeeeeee-0000-0000-0000-000000000009','WATER','6100000000097','ماء','Water','مياه معدنية','Mineral water','dddddddd-0000-0000-0000-000000000004','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000003',0.200,true,now()),
('eeeeeeee-0000-0000-0000-000000000010','KUNAFA','6100000000103','كنافة','Kunafa','كنافة بالجبن','Cheese kunafa','dddddddd-0000-0000-0000-000000000005','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.500,true,now()),
('eeeeeeee-0000-0000-0000-000000000011','CHOC-CAKE','6100000000110','كيك شوكولاتة','Chocolate Cake','كيك شوكولاتة','Chocolate cake','dddddddd-0000-0000-0000-000000000005','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.800,true,now()),
('eeeeeeee-0000-0000-0000-000000000012','FRIES','6100000000127','بطاطس مقلية','Fries','بطاطس مقلية مقرمشة','Crispy fries','dddddddd-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',0.800,true,now()),
('eeeeeeee-0000-0000-0000-000000000013','CHEESE-FRIES','6100000000134','بطاطس بالجبن','Cheese Fries','بطاطس مع صلصة الجبن','Fries with cheese sauce','dddddddd-0000-0000-0000-000000000006','Simple','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001',1.200,true,now()),
('eeeeeeee-0000-0000-0000-000000000014','COMBO-SHAWARMA','6100000000141','وجبة شاورما كومبو','Combo Shawarma Meal','شاورما مع بطاطس ومشروب','Shawarma with fries and drink','dddddddd-0000-0000-0000-000000000001','Combo','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000002',3.500,true,now());

INSERT INTO ofc.product_images ("Id","ProductId","Url","IsPrimary","SortOrder") VALUES
(gen_random_uuid(),'eeeeeeee-0000-0000-0000-000000000001','https://placehold.co/800x600',true,1),
(gen_random_uuid(),'eeeeeeee-0000-0000-0000-000000000003','https://placehold.co/800x600',true,1),
(gen_random_uuid(),'eeeeeeee-0000-0000-0000-000000000007','https://placehold.co/800x600',true,1);

-- Available at both branches ------------------------------
INSERT INTO ofc.product_branch_availability ("ProductId","BranchId","IsAvailable")
SELECT p."Id", b."Id", true FROM ofc.products p CROSS JOIN ofc.branches b;

-- Price rules (branch / channel specific) -----------------
INSERT INTO ofc.price_rules ("Id","ProductId","BranchId","SalesChannelId","Price","EffectiveFrom","EffectiveTo","IsActive","CreatedAt") VALUES
(gen_random_uuid(),'eeeeeeee-0000-0000-0000-000000000003','22222222-2222-2222-2222-222222222221',NULL,2.800,now(),NULL,true,now()),
(gen_random_uuid(),'eeeeeeee-0000-0000-0000-000000000007','22222222-2222-2222-2222-222222222222','aaaaaaaa-0000-0000-0000-000000000001',0.450,now(),NULL,true,now());

-- Promotion (10% off Cola) --------------------------------
INSERT INTO ofc.promotions ("Id","Code","NameAr","NameEn","ProductId","BranchId","SalesChannelId","DiscountType","DiscountValue","Priority","EffectiveFrom","EffectiveTo","IsActive","CreatedAt") VALUES
(gen_random_uuid(),'PROMO-REFRESH','عرض الانتعاش','Refresher Offer','eeeeeeee-0000-0000-0000-000000000007',NULL,NULL,'Percentage',10.00,1,now(),NULL,true,now());

-- Catalog version -----------------------------------------
INSERT INTO ofc.catalog_versions ("Id","Number","Note","PublishedAt","PublishedByUserId") VALUES
(gen_random_uuid(),1,'Initial catalog',now(),'33333333-3333-3333-3333-333333333333');

-- Selection groups: drink size, add-ons, combo ------------
INSERT INTO ofc.selection_groups ("Id","Kind","NameAr","NameEn","IsRequired","MinSelections","MaxSelections","IsActive","CreatedAt") VALUES
('ffffffff-0000-0000-0000-000000000001','Modifier','حجم المشروب','Drink Size',false,0,1,true,now()),
('ffffffff-0000-0000-0000-000000000002','Modifier','إضافات','Add-ons',false,0,2,true,now()),
('ffffffff-0000-0000-0000-000000000003','Combo','الوجبة','Meal',true,1,1,true,now());

INSERT INTO ofc.selection_options ("Id","SelectionGroupId","ProductId","PriceAdjustment","IsDefault","MaxQuantity","SortOrder") VALUES
(gen_random_uuid(),'ffffffff-0000-0000-0000-000000000001','eeeeeeee-0000-0000-0000-000000000007',0.000,true,1,1),
(gen_random_uuid(),'ffffffff-0000-0000-0000-000000000001','eeeeeeee-0000-0000-0000-000000000009',0.200,false,1,2),
(gen_random_uuid(),'ffffffff-0000-0000-0000-000000000001','eeeeeeee-0000-0000-0000-000000000008',0.400,false,1,3),
(gen_random_uuid(),'ffffffff-0000-0000-0000-000000000002','eeeeeeee-0000-0000-0000-000000000013',0.300,false,1,1),
(gen_random_uuid(),'ffffffff-0000-0000-0000-000000000002','eeeeeeee-0000-0000-0000-000000000012',0.600,false,1,2),
(gen_random_uuid(),'ffffffff-0000-0000-0000-000000000003','eeeeeeee-0000-0000-0000-000000000014',1.500,true,1,1);

-- Attach selection groups to products ---------------------
INSERT INTO ofc.product_selection_groups ("ProductId","SelectionGroupId","SortOrder") VALUES
('eeeeeeee-0000-0000-0000-000000000007','ffffffff-0000-0000-0000-000000000001',1),
('eeeeeeee-0000-0000-0000-000000000008','ffffffff-0000-0000-0000-000000000001',1),
('eeeeeeee-0000-0000-0000-000000000003','ffffffff-0000-0000-0000-000000000002',1),
('eeeeeeee-0000-0000-0000-000000000004','ffffffff-0000-0000-0000-000000000002',1),
('eeeeeeee-0000-0000-0000-000000000014','ffffffff-0000-0000-0000-000000000003',1);

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
('77777777-0000-0000-0000-000000000001','eeeeeeee-0000-0000-0000-000000000004',1,'برجر دجاج','Chicken Burger','Active',now(),'33333333-3333-3333-3333-333333333333',now());

INSERT INTO ofc.recipe_lines ("Id","RecipeVersionId","InventoryItemId","UnitId","Quantity") VALUES
(gen_random_uuid(),'77777777-0000-0000-0000-000000000001','88888888-0000-0000-0000-000000000001','99999999-0000-0000-0000-000000000001',0.200);

COMMIT;
