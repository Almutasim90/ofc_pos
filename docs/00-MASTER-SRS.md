# OFC Master SRS
## Oman Fried Chicken Restaurant Management Platform

**Status:** Source of Truth
**Delivery Model:** Agile / Vertical Slices
**Architecture:** Modular Monolith
**Frontend:** React + TypeScript + Tailwind CSS + shadcn/ui
**Backend:** ASP.NET Core Web API
**Database:** PostgreSQL
**Offline:** PWA + IndexedDB/Dexie + idempotent synchronization
**Languages:** Arabic RTL + English LTR

---

## Governance Rules

1. هذا الملف هو المرجع الأعلى للمتطلبات.
2. ملفات الـSprint لا يجوز أن تغيّر Business Rule واردة هنا دون تحديث هذا الملف أولًا.
3. التنفيذ يكون Vertical Slice وليس طبقات منفصلة.
4. كل Story يجب أن تنتهي بقاعدة بيانات + Backend + API + UI + Validation + Permission + Audit + Tests عند الحاجة.
5. Offline, Security, Localization, Audit وResponsive UX هي Cross-Cutting Requirements منذ البداية.
6. لا يتم بناء Microservices في المرحلة الحالية.
7. لا يتم تنفيذ Good-to-Have قبل استقرار Must-to-Have.
8. لا يجوز حذف أو تعديل Financial Transactions المنشورة؛ التصحيح يكون Reversal.
9. لا يجوز حذف الطلبات الملغاة؛ يجب الاحتفاظ بسبب الإلغاء وسجل التدقيق.
10. أي تعقيد داخلي يجب ألا يظهر للمستخدم النهائي إلا عند الحاجة.

---

## Original Integrated Requirements

# البرومت الاحترافي الشامل لتطوير نظام Oman Fried Chicken POS & Restaurant Management

> **الهدف من هذا البرومت:** استخدامه مع Cursor / GitHub Copilot / Claude Code / Codex أو أي وكيل برمجي لبناء نظام مطاعم ونقاط بيع حديث، متجاوب، سهل الاستخدام، قابل للتوسع، وملائم للعمل الفعلي داخل فروع **Oman Fried Chicken (OFC)**.

---

# 1. الدور المطلوب منك

أنت تعمل كفريق متكامل يضم:

- Senior Software Architect
- Senior ASP.NET Core Developer
- Senior React + TypeScript Developer
- PostgreSQL Database Architect
- UX/UI Product Designer
- POS & Restaurant Operations Specialist
- Security Engineer
- QA / Test Automation Engineer

مهمتك ليست إنشاء شاشات CRUD تقليدية، بل بناء **منصة تشغيل مطاعم حديثة** يكون فيها التعقيد داخل النظام، بينما تبقى تجربة الكاشير والمطبخ والمدير سهلة وسريعة وواضحة.

يجب أن تتعامل مع المتطلبات التالية كـ **System Requirements Contract**، وألا تغير قواعد العمل الجوهرية دون توضيح السبب.

---

# 2. رؤية النظام

بناء منصة موحدة لإدارة مطاعم Oman Fried Chicken تشمل:

- نقطة البيع POS
- إدارة الطلبات
- إدارة الوجبات المركبة Combos
- الإضافات Modifiers
- التسعير حسب الفرع وقناة البيع
- الدفع النقدي والإلكتروني والمجزأ
- الورديات والتقفيل الأعمى
- الإلغاءات والاسترجاعات
- المطبخ KDS
- الطباعة الحرارية
- إدارة المواد الخام
- الوصفات BOM
- المخزون والحركات المخزنية
- المشتريات والموردين
- الهدر
- المصروفات
- التقارير
- الإدارة المركزية للفروع
- العمل دون إنترنت
- المزامنة
- التدقيق Audit
- الصلاحيات
- دعم العربية والإنجليزية
- الاستعداد للتكاملات المستقبلية والذكاء الاصطناعي

يجب أن يعمل النظام كنظام مطاعم حديث، وليس كبرنامج Desktop قديم تم تحويله إلى Web.

---

# 3. المبادئ الأساسية غير القابلة للتفاوض

## 3.1 البساطة للمستخدم

النظام يجب أن يكون:

- سريع
- واضح
- Touch Friendly
- قليل الخطوات
- مناسب للكاشير تحت ضغط العمل
- متجاوب مع الشاشات المختلفة
- بدون ازدحام بصري
- بدون جداول أو نماذج معقدة في العمليات اليومية

**قاعدة UX الأساسية:**

> Complex inside, simple outside.

---

## 3.2 تصميم قائم على Workflow وليس الجداول

ممنوع بناء واجهات المستخدم بشكل مباشر اعتمادًا على جداول قاعدة البيانات.

مثال خاطئ:

- Orders CRUD
- Payments CRUD
- Inventory Transactions CRUD

المطلوب:

- Create Order Workflow
- Pay Order Workflow
- Cancel Order Workflow
- Close Shift Workflow
- Receive Stock Workflow
- Count Inventory Workflow

---

# 4. الحزمة التقنية المقترحة

## Frontend

استخدم:

- React
- TypeScript
- Vite
- Tailwind CSS
- shadcn/ui
- Radix UI عند الحاجة
- TanStack Query
- Zustand أو Context خفيف للحالة المحلية
- React Hook Form
- Zod
- React Router
- Lucide Icons

## PWA

يجب أن تكون الواجهة:

- Progressive Web App
- Installable
- Offline capable
- Fast startup
- Local caching
- Service Worker support

## Offline Storage

استخدم:

- IndexedDB
- Dexie.js

ولا تعتبر IndexedDB مرجعًا ماليًا نهائيًا.

## Backend

استخدم:

- ASP.NET Core Web API
- C#
- .NET LTS
- Entity Framework Core
- FluentValidation
- MediatR فقط إذا كانت فائدته واضحة، وليس لمجرد التعقيد
- SignalR للتحديثات اللحظية

## Database

استخدم:

- PostgreSQL
- Supabase PostgreSQL إذا كان مناسبًا للبنية التشغيلية

## Architecture

اعتمد:

- Modular Monolith
- Clean Architecture عند الحاجة داخل الوحدات
- Domain Driven Design بصورة عملية وليست مبالغًا فيها

البنية العامة:

```text
Backend
├── Modules
│   ├── Identity
│   ├── Organization
│   ├── Catalog
│   ├── Pricing
│   ├── Ordering
│   ├── Payments
│   ├── Kitchen
│   ├── Shifts
│   ├── Inventory
│   ├── Procurement
│   ├── Expenses
│   ├── Reporting
│   ├── Notifications
│   ├── Integrations
│   └── AI
│
├── SharedKernel
├── Infrastructure
└── API
```

لا تستخدم Microservices في المرحلة الحالية.

---

# 5. تصميم الواجهة وتجربة المستخدم

## 5.1 Responsive Design

اعتمد Mobile First.

يجب دعم:

- 360px Mobile
- 390px Mobile
- 768px Tablet
- 1024px POS Tablet
- 1280px Desktop
- 1366px POS screen
- 1440px Desktop
- 1920px Large screen

ويجب ألا تتكسر الواجهة عند أي عرض بين هذه المقاسات.

---

## 5.2 Tailwind Breakpoints

استخدم Tailwind responsive breakpoints بصورة منهجية:

```text
sm
md
lg
xl
2xl
```

ويجب أن يكون التصميم Fluid وليس ثابتًا فقط على نقاط محددة.

---

## 5.3 RTL / LTR

النظام ثنائي اللغة بالكامل:

- Arabic RTL
- English LTR

يجب أن يكون الاتجاه ديناميكيًا.

ممنوع:

- تثبيت margin-left / margin-right بشكل يفسد RTL
- كتابة نصوص داخل Components بشكل Hardcoded
- استخدام Layout يعمل فقط بالإنجليزية

استخدم:

- logical properties
- dir="rtl" / dir="ltr"
- i18n translation files

---

## 5.4 Design System

أنشئ Design Tokens موحدة تشمل:

- Primary
- Secondary
- Success
- Warning
- Danger
- Surface
- Background
- Border
- Muted
- Text Primary
- Text Secondary

وأيضًا:

- spacing scale
- border radius
- shadows
- typography
- button sizes
- input sizes
- status badges

لا تستخدم ألوانًا عشوائية داخل المكونات.

---

# 6. أسلوب الواجهة

اعتمد تصميمًا:

- Modern
- Minimal
- Clean
- Professional
- High contrast where needed
- Touch friendly
- Fast scanning
- Arabic friendly

تجنب:

- gradients المبالغ فيها
- animations الثقيلة
- glassmorphism الذي يضعف الوضوح
- كثرة الـ cards
- كثرة borders
- كثرة النصوص
- icons بدون labels في العمليات الحرجة

---

# 7. متطلبات شاشة POS الأساسية

## Must Have

يجب أن تحتوي شاشة POS الرئيسية على:

### Header

- الفرع الحالي
- اسم الكاشير
- رقم الوردية
- قناة البيع
- حالة الاتصال
- الساعة
- إشعار الطلبات الجديدة

### Product Area

- Category tabs
- Product grid
- Product images
- Product name
- Product price
- Availability
- Search
- Fast filter

### Cart

- اسم الصنف
- الكمية
- السعر
- الإضافات
- الملاحظات
- الخصم
- الضريبة
- الإجمالي

### Actions

- Increase quantity
- Decrease quantity
- Remove item
- Add note
- Hold order
- Cancel order
- Discount
- Payment

### Payment buttons

- Cash
- Card
- Split payment
- Other configured methods

---

# 8. قواعد سهولة الاستخدام في POS

- إضافة المنتج العادي: ضغطة واحدة بعد الوصول للتصنيف.
- المنتج المركب: خطوات قصيرة موجهة.
- الدفع النقدي: لا يزيد عن خطوتين بعد فتح نافذة الدفع.
- الدفع بالبطاقة: لا يزيد عن خطوتين.
- الإلغاء: اختيار السبب ثم التأكيد.
- لا تعرض أزرارًا لا يملك المستخدم صلاحيتها.
- لا تعرض الوظائف النادرة بجانب الوظائف اليومية الأساسية.
- العمليات الحرجة تحتاج Confirmation واضح.
- لا تستخدم Dialogs كثيرة بدون داعٍ.
- عند إضافة منتج للسلة أعط Feedback بصريًا فورًا.

---

# 9. المنتجات

كل Product يجب أن يدعم:

- Id
- SKU
- Barcode
- Arabic Name
- English Name
- Arabic Description
- English Description
- Image
- Category
- Product Type
- Tax Category
- Base Price
- Active status
- Branch availability
- Preparation station
- Recipe
- Modifier groups
- Combo configuration

أنواع المنتجات:

```text
SIMPLE
COMBO
SERVICE
```

---

# 10. التصنيفات

دعم:

- Main categories
- Sub categories
- Sort order
- Image/icon
- Branch availability
- Arabic / English names

---

# 11. Combo / Meal Engine

يجب دعم الوجبات المركبة بصورة احترافية.

مثال:

```text
Zinger Meal

Main:
- Zinger Burger

Side:
- Fries Regular
- Fries Large +0.300

Drink:
- Pepsi
- Diet Pepsi
- 7Up
- Water
```

يجب دعم:

- Required groups
- Optional groups
- Min selection
- Max selection
- Default selection
- Price adjustment
- Branch availability

ولا تعامل Combo كـ Addon بسيط.

---

# 12. Modifier Groups

دعم:

- Modifier Group
- Modifiers
- Min Selection
- Max Selection
- Default Option
- Free Modifier
- Paid Modifier
- Quantity limit
- Branch availability

مثال:

```text
Bread Type
- Regular
- Brioche +0.200

Sauces
- Garlic
- Spicy
- BBQ
```

---

# 13. Sales Channels

أنشئ Entity مستقلة للقنوات.

أمثلة:

- Counter
- Takeaway
- Dine In
- Drive Thru
- QR
- Delivery
- Talabat
- Other Aggregator

لا تخلط Sales Channel مع Payment Method.

---

# 14. Pricing Engine

دعم التسعير حسب:

```text
Promotion
↓
Branch + Channel
↓
Branch
↓
Channel
↓
Default Price
```

يجب حفظ Price Snapshot داخل Order Item.

أي تعديل لاحق على السعر لا يؤثر على الفواتير السابقة.

---

# 15. Taxes

يجب أن تكون الضرائب:

- Configurable
- Effective-date based
- Tax Inclusive أو Tax Exclusive
- Branch aware إذا تطلب الأمر
- Snapshot saved at sale time

ممنوع Hardcode نسبة الضريبة.

---

# 16. الطلبات

حالات الطلب:

```text
Draft
Pending
Confirmed
Paid
SentToKitchen
Preparing
Ready
Completed
Cancelled
Rejected
PartiallyRefunded
Refunded
```

كل تغيير حالة يسجل في OrderStatusHistory.

---

# 17. الإلغاء

## Must Have

أي إلغاء يجب أن يحتوي سببًا.

ممنوع حذف الطلب نهائيًا من النظام.

عند Cancel:

يجب أن يسجل:

- OrderId
- CancellationReasonId
- Optional note
- CancelledBy
- CancelledAt
- BranchId
- DeviceId
- ShiftId
- OrderTotal
- OrderStatusAtCancellation
- WasSentToKitchen

أسباب الإلغاء تكون Configurable.

أمثلة:

- Customer changed mind
- Wrong item entered
- Wrong quantity
- Item unavailable
- Payment issue
- Kitchen delay
- Duplicate order
- Pricing error
- Other

إذا تم اختيار Other تكون الملاحظة إلزامية.

---

# 18. صلاحيات الإلغاء

مثال:

- Cashier: الإلغاء قبل الدفع ضمن حدود معينة
- Supervisor: الإلغاء بعد إرسال الطلب للمطبخ
- Manager: اعتماد العمليات الكبيرة

يجب أن يكون حد القيمة Configurable.

---

# 19. تقارير الإلغاء

أنشئ:

- Cancellation Count
- Cancellation Amount
- Cancellation Rate
- Top Cancellation Reasons
- Cancellation by Branch
- Cancellation by User
- Cancellation by Channel
- Cancellation by Hour
- Cancellation before/after Kitchen

---

# 20. Void / Cancel / Refund

يجب التفريق بين:

### Void Item
قبل الدفع.

### Cancel Order
إلغاء الطلب بالكامل.

### Refund
بعد الدفع.

### Partial Refund
إرجاع جزء من الفاتورة.

كل عملية لها:

- Reason
- User
- Timestamp
- Approval if required
- Audit entry

---

# 21. Payments

Payment Methods ديناميكية:

- Cash
- OmanNet
- Visa
- Mastercard
- Apple Pay
- Voucher
- Online
- Other

كل Payment صف مستقل.

لا تستخدم CashAmount وCardAmount فقط داخل Orders.

---

# 22. Split Payment

يدعم:

```text
Cash: 1.000
OmanNet: 1.700
Total: 2.700 OMR
```

ويجب التحقق أن مجموع Payments يساوي Order Total.

---

# 23. Payment Lifecycle

للدفع الإلكتروني:

```text
Pending
Authorized
Captured
Failed
Cancelled
Reversed
```

---

# 24. Financial Ledger

بعد Posting:

- ممنوع تعديل الحركة المالية
- ممنوع حذفها
- التصحيح يكون Reversal

كل Financial Transaction يحتوي:

- TransactionId
- Branch
- Shift
- Device
- Type
- Amount
- Reference
- PaymentMethod
- CreatedBy
- CreatedAt
- ReversalReference

---

# 25. الورديات

## Open Shift

- Opening Cash
- User
- Device
- Branch
- Date/Time

## خلال الوردية

تتبع:

- Cash Sales
- Card Sales
- Refunds
- Petty Cash
- Cash In
- Cash Out
- Cash Drop

---

# 26. Blind Close

الكاشير لا يرى Expected Cash قبل إغلاق الوردية.

يدخل:

- Actual Cash
- Card total
- Cash denomination count

ثم يحسب النظام:

```text
Expected Cash =
Opening Float
+ Cash Sales
+ Cash In
- Cash Refunds
- Petty Cash
- Cash Drops
```

ثم:

```text
Cash Variance = Actual Cash - Expected Cash
```

---

# 27. Cash Denominations

دعم فئات العملة مثل:

- 50 OMR
- 20 OMR
- 10 OMR
- 5 OMR
- 1 OMR
- 500 Baisa
- 100 Baisa
- 50 Baisa
- 25 Baisa

ويتم حساب الإجمالي تلقائيًا.

---

# 28. المواد الخام والمخزون

افصل بين:

```text
Sellable Product
Inventory Item
```

مثال:

```text
Zinger Burger = Product
Chicken Breast = Inventory Item
```

---

# 29. Units of Measurement

دعم:

- KG
- G
- L
- ML
- Piece
- Pack
- Carton
- NOS إذا تطلب التوافق

مع Unit Conversion.

---

# 30. Recipes / BOM

كل وصفة تحتوي:

- Product
- Ingredient
- Quantity
- Unit
- Recipe Version
- Effective Date

مثال:

```text
Zinger Burger
- Chicken 150g
- Bread 1 piece
- Sauce 30ml
```

---

# 31. Recipe Versioning

لا تعدل وصفة قديمة مباشرة إذا كان التغيير مؤثرًا على التاريخ.

أنشئ Version جديدة.

---

# 32. Inventory Ledger

كل تغير يسجل كحركة:

```text
PURCHASE
SALE_DEDUCTION
WASTE
TRANSFER_IN
TRANSFER_OUT
ADJUSTMENT
RETURN
COUNT_ADJUSTMENT
```

الرصيد يمكن حسابه من الحركات.

يمكن الاحتفاظ Cached Balance للأداء.

---

# 33. Stock Adjustment

كل Adjustment يحتاج:

- Reason
- Quantity
- User
- Branch
- Reference
- Timestamp

---

# 34. Physical Inventory Count

دعم:

- Count Session
- System Qty
- Actual Qty
- Variance
- Adjustment after approval

---

# 35. Waste

أنواع الهدر:

- Expired
- Damaged
- Preparation Waste
- Finished Product Waste
- Cancelled Order Waste

مع:

- Reason
- Quantity
- User
- Branch
- Optional photo

---

# 36. المشتريات

Must Have الأساسي:

- Supplier
- Purchase entry
- Goods receipt
- Item
- Qty
- Unit
- Unit cost
- Total
- Warehouse
- Reference

ثم لاحقًا:

- Purchase Orders
- Approval workflow
- Supplier invoices

---

# 37. Suppliers

دعم:

- Name
- Contact
- VAT Number
- Items supplied
- Latest cost
- Purchase history

---

# 38. Costing

دعم:

```text
Recipe Cost
Selling Price
Food Cost %
Gross Margin
```

Weighted Average Cost مناسب كبداية.

---

# 39. Kitchen Display System KDS

## Must Have

الـKDS وحدة أساسية.

محطات نموذجية:

- Fryer
- Grill
- Drinks
- Packing
- Expeditor

كل Kitchen Ticket يحتوي:

- Order number
- Time
- Items
- Modifiers
- Notes
- Station
- Status

---

# 40. KDS Status

```text
New
Preparing
Ready
Completed
Cancelled
```

يجب أن يكون لكل Item حالة مستقلة عند الحاجة.

---

# 41. Kitchen Timing

تتبع:

- Ticket Created
- Start Preparation
- Ready Time
- Completed Time

ويدعم Target Preparation Time.

الطلبات المتأخرة تظهر بوضوح.

---

# 42. Kitchen Cancellation

إذا ألغي الطلب بعد دخوله المطبخ:

- أرسل Cancellation alert فورًا
- سجّل وقت الإلغاء
- سجّل هل بدأ التحضير
- اسمح للمشرف بتحديد هل نتج Waste

---

# 43. Printing

لا تعتمد على Web USB مباشرة كحل أساسي.

استخدم Local Print Agent.

البنية:

```text
React PWA
↓
Local POS Print Agent
↓
Receipt Printer
Kitchen Printer
Cash Drawer
Customer Display
```

---

# 44. Print Queue

كل Print Job:

```text
Pending
Printing
Printed
Failed
Retrying
```

مع Retry.

---

# 45. Printer Routing

لا تربط المنتج بطابعة واحدة مباشرة.

استخدم:

```text
Product
↓
Preparation Station
↓
Printer Route
```

---

# 46. Offline First

Must Have.

أثناء انقطاع الإنترنت يستطيع الكاشير:

- فتح الطلب
- إضافة الأصناف
- الدفع المسموح
- الطباعة
- إرسال المطبخ محليًا
- تسجيل النثريات
- إغلاق العمليات المحلية

---

# 47. Local Transactions

كل عملية Offline تحتوي:

- LocalTransactionId
- IdempotencyKey
- DeviceId
- ShiftId
- Sequence
- CreatedAtLocal
- Payload
- SyncStatus

الحالات:

```text
Pending
Syncing
Synced
Conflict
Failed
```

---

# 48. Idempotency

استخدم UUIDv7 أو UUID قوي يتم إنشاؤه مرة واحدة لكل Transaction.

إعادة المحاولة تستخدم المفتاح نفسه.

الـBackend لا يعيد تنفيذ المعاملة إذا استقبل نفس المفتاح.

---

# 49. Offline Inventory Conflicts

لا تلغِ بيعًا حدث فعليًا لمجرد أن الرصيد تغير.

سجل الحركة.

إذا أصبح Stock Negative:

- أنشئ Inventory Discrepancy
- أرسل تنبيهًا للمشرف

---

# 50. Audit Trail

Must Have.

يسجل:

- Login
- Logout
- Price Change
- Discount
- Refund
- Void
- Cancel
- Shift Close
- Stock Adjustment
- Tax Change
- Permission Change
- Configuration Change

مع:

- User
- Branch
- Device
- Timestamp
- Old Value
- New Value
- Reason
- CorrelationId

---

# 51. Roles & Permissions

Roles نموذجية:

- Cashier
- Supervisor
- Branch Manager
- Inventory Officer
- Finance
- Admin

Permissions دقيقة:

- CanRefund
- CanVoid
- CanCancel
- CanDiscount
- CanChangePrice
- CanCloseShift
- CanViewVariance
- CanAdjustStock
- CanViewCost
- CanManageProducts
- CanManageUsers

Backend هو المسؤول الحقيقي عن Authorization.

إخفاء الزر في UI ليس حماية كافية.

---

# 52. Admin Portal

التقسيم المقترح:

```text
Dashboard

Operations
├── Orders
├── Kitchen
└── Shifts

Inventory
├── Stock
├── Purchases
├── Transfers
├── Counts
└── Waste

Menu
├── Categories
├── Products
├── Combos
├── Modifiers
├── Pricing
└── Promotions

Finance
├── Sales
├── Payments
├── Expenses
└── Reports

Administration
├── Branches
├── Devices
├── Users
├── Roles
├── Permissions
└── Settings
```

---

# 53. Dashboard

Must Have:

- Today's Sales
- Order Count
- Average Order Value
- Open Shifts
- Cash Variance
- Refunds
- Cancelled Orders
- Cancellation Rate
- Low Stock
- Waste
- Kitchen Avg Prep Time
- Late Orders

---

# 54. تقارير Must Have

- Daily Sales
- Sales by Branch
- Sales by Product
- Sales by Category
- Sales by Channel
- Sales by Cashier
- Sales by Payment Method
- Discounts
- Refunds
- Voids
- Cancelled Orders
- Cancellation Reasons
- Shift Variance
- Inventory Balance
- Low Stock
- Waste
- Purchases
- Food Cost
- Gross Margin

---

# 55. الإدارة المركزية

الإدارة المركزية تستطيع:

- Publish menu
- Publish prices
- Configure tax
- Disable item
- Define promotions
- Compare branches
- View branch sales
- View inventory
- View cancellation reasons
- View performance

مع Branch Override عند الحاجة.

---

# 56. QR Ordering — Good to Have

دعم:

- Branch QR
- Table QR
- Parking spot QR
- Browse menu
- Modifiers
- Notes
- Submit order
- Track status

يستخدم نفس Catalog وPricing وOrder Engine.

---

# 57. Customer Display — Good to Have

شاشة ثانية تعرض:

- Items
- Qty
- Price
- Discount
- VAT
- Total

---

# 58. Loyalty — Good to Have

دعم مستقبلي:

- Customers
- Points
- Rewards
- Coupons
- Order history

لكن لا تجعل Loyalty جزءًا من Core POS.

---

# 59. AI — Good to Have

يكون Module منفصل.

استخداماته:

- Demand Forecasting
- Stock Forecasting
- Upselling
- Management Analysis

AI Failure يجب ألا يوقف:

- POS
- Payment
- Kitchen
- Printing
- Inventory

---

# 60. Security

Must Have:

- HTTPS
- Secure authentication
- Server-side authorization
- Least privilege
- Input validation
- Rate limiting
- Secure secrets
- Audit logging
- CSRF strategy where applicable
- XSS prevention
- SQL injection protection
- Secure headers
- Strong password policies if local auth exists

---

# 61. Secrets

لا تعيد:

- API keys
- Payment secrets
- Integration tokens

للـFrontend بعد حفظها.

استخدم Vault / Secret Manager في الإنتاج عند توفره.

---

# 62. Observability

استخدم:

- Structured logs
- Correlation IDs
- Health checks
- Metrics
- Error tracking

راقب:

- API
- DB
- Sync
- Printing
- KDS
- Integrations

---

# 63. Accessibility

التزم قدر الإمكان بـWCAG المناسبة لتطبيق إداري.

- contrast جيد
- keyboard navigation
- focus states
- accessible dialogs
- labels
- aria attributes عند الحاجة

---

# 64. Performance

الأهداف:

- POS startup سريع
- Product navigation شبه فوري
- Add to cart بدون انتظار الشبكة
- Local operations responsive
- No unnecessary full-page reload
- Lazy-load modules غير الضرورية
- Optimize product images
- Cache static catalog data

---

# 65. UX Performance Targets

- Add simple product: 1 click
- Common combo: 2–4 interactions
- Cash payment: ≤ 2 main actions
- Card payment: ≤ 2 main actions
- Cancel order: ≤ 3 main actions + reason selection
- Find product: < 5 seconds for trained cashier
- Open shift: < 30 seconds
- Connection state visible instantly

---

# 66. Error Handling

ممنوع عرض رسائل تقنية للمستخدم مثل:

```text
500 Internal Server Error
NullReferenceException
```

اعرض رسالة مفهومة.

مثال:

```text
تعذر الاتصال بالخادم.
تم حفظ العملية محليًا وستتم مزامنتها تلقائيًا.
```

ويتم تسجيل الخطأ الحقيقي في Logs.

---

# 67. حالات Loading / Empty / Error

كل شاشة يجب أن تحتوي:

- Loading state
- Empty state
- Error state
- Retry state

لا تترك شاشة فارغة بدون تفسير.

---

# 68. Responsive POS Behavior

## Desktop / Large POS

استخدم Split View:

- Product grid
- Cart side panel

## Tablet

- Product area أساسي
- Cart collapsible أو side sheet

## Mobile

- Product browser
- Sticky cart summary
- Bottom sheet للCart
- Bottom action bar للدفع

لا تحاول تصغير Desktop layout حرفيًا إلى Mobile.

---

# 69. Responsive Admin Behavior

Desktop:

- Sidebar + content

Tablet:

- Collapsible sidebar

Mobile:

- Drawer navigation
- Cards بدل الجداول الكبيرة عند الحاجة
- Horizontal scroll فقط إذا كانت البيانات لا يمكن إعادة تنظيمها

---

# 70. Tables

في Admin:

- sticky header
- pagination
- search
- filters
- sorting
- column visibility
- responsive strategy
- export when relevant

استخدم TanStack Table إذا كان مناسبًا.

---

# 71. Forms

- استخدم React Hook Form
- Zod validation
- validation messages واضحة
- field grouping
- avoid giant forms
- use tabs only if meaningful
- autosave فقط إذا كان آمنًا

---

# 72. Modals

استخدم Modal فقط للأعمال القصيرة.

لا تستخدم Modal لإنشاء Product كامل أو Workflow طويل.

استخدم Page / Drawer / Stepper عند الحاجة.

---

# 73. Confirmation Rules

استخدم Confirmation فقط عند:

- Refund
- Cancel
- Void
- Shift Close
- Stock Adjustment
- Delete configuration
- Security-sensitive changes

لا تضع Confirm على كل عملية عادية.

---

# 74. Oman Localization

- العملة OMR
- ثلاث منازل عشرية عند الحاجة
- العربية أولاً
- English supported
- VAT configurable
- الفواتير قابلة للتوافق مع متطلبات الفوترة الإلكترونية في عُمان عبر Integration Layer
- لا Hardcode أي معيار خاص بدولة أخرى

---

# 75. E-Invoicing Readiness

أنشئ abstraction:

```text
IEInvoiceProvider
```

لا تربط Core POS مباشرة بمزود واحد.

احفظ:

- Invoice UUID
- Seller VATIN
- Invoice timestamp
- Taxable amount
- VAT amount
- Total
- Submission status
- External reference

---

# 76. Database Guidelines

- استخدم UUID / UUIDv7 حيث يناسب
- TIMESTAMPTZ
- DECIMAL مناسب للعملة
- FK واضحة
- Indexes مدروسة
- Unique constraints
- Soft delete فقط للبيانات الإدارية المناسبة
- لا Soft Delete للحركات المالية المنشورة
- Audit للأحداث الحساسة

---

# 77. Monetary Precision

استخدم Decimal.

ممنوع Float / Double للأموال.

---

# 78. API Standards

- RESTful naming
- Versioning strategy
- ProblemDetails للأخطاء
- Validation responses standardized
- Pagination standardized
- Filtering standardized
- Idempotency support
- Correlation IDs
- Authorization policies

---

# 79. Testing

## Unit Tests

لـ:

- Pricing
- Tax
- Payment split
- Shift calculations
- Inventory deductions
- Refund rules
- Cancellation rules

## Integration Tests

لـ:

- Database
- API
- Offline sync
- Idempotency
- Payments
- Inventory ledger

## UI Tests

لـ:

- Add product
- Combo
- Payment
- Cancel
- Refund
- Close shift
- Responsive layout

استخدم Playwright أو Cypress.

---

# 80. Acceptance Test الرئيسي

يجب إثبات أن السيناريو التالي يعمل:

```text
Login
↓
Open Shift
↓
Create Zinger Meal
↓
Choose Large Fries
↓
Choose Drink
↓
Add Note
↓
Pay using Cash + Card
↓
Print Receipt
↓
Send Kitchen Ticket
↓
Start Preparation
↓
Mark Ready
↓
Deduct BOM
↓
Complete Order
↓
Cancel another order with mandatory reason
↓
Record cancellation report
↓
Close Shift using Blind Close
↓
Generate Variance
```

ويجب اختبار نفس السيناريو مع انقطاع الإنترنت.

---

# 81. Must Have Release Scope

## Phase 1 — Core Operational POS

- Authentication
- Roles / Permissions
- Branches
- POS Devices
- Categories
- Products
- Combos
- Modifiers
- Pricing
- Taxes
- POS Screen
- Orders
- Payments
- Split Payment
- Cancel / Void / Refund
- Cancellation Reasons
- Shifts
- Blind Close
- Cash Denominations
- Petty Cash
- Offline
- Idempotency
- Printing
- Basic KDS
- Inventory Items
- UOM
- BOM
- Inventory Ledger
- Audit Trail
- Core Reports
- Responsive bilingual UI

---

# 82. Good to Have

## Phase 2

- Procurement workflow
- Advanced stock counts
- Transfers
- Advanced waste
- Food costing dashboard
- Promotions
- QR ordering
- Customer display
- Advanced KDS
- E-Invoicing integration
- Manager dashboards

## Phase 3

- Online ordering
- Delivery integrations
- Loyalty
- Customer CRM
- Advanced analytics
- Email / WhatsApp notifications
- AI forecasting
- AI upselling
- AI management assistant

---

# 83. قواعد تنفيذ مهمة للوكيل البرمجي

1. لا تنفذ كل النظام دفعة واحدة.
2. اعمل Module by Module.
3. لا تنشئ Entity بلا استخدام واضح.
4. لا تنشئ CRUD لمجرد وجود جدول.
5. لا تضف Dependency بلا سبب.
6. لا تحول النظام إلى Microservices.
7. لا تكتب Business Logic داخل React Components.
8. لا تضع Business Logic داخل Controllers.
9. لا Hardcode الأسعار أو الضرائب أو الفروع.
10. لا تستخدم Float للأموال.
11. لا تسمح بتعديل حركات مالية منشورة.
12. لا تسمح بحذف Order مالي من التاريخ.
13. لا تعيد API Secrets للواجهة.
14. لا تعتمد على الإنترنت في العمليات المحلية الحرجة.
15. لا تجعل SignalR مصدر الحقيقة.
16. لا تجعل AI جزءًا من Critical Path.
17. لا تنشئ UI desktop-only.
18. لا تترك RTL كتحسين لاحق.
19. لا تستخدم CSS عشوائي خارج Design System إلا للضرورة.
20. لا تنشئ صفحة لا تملك Loading / Empty / Error states.

---

# 84. استراتيجية التنفيذ

ابدأ بهذا الترتيب:

```text
Step 1
Foundation + Authentication + Branches + Devices

Step 2
Catalog + Categories + Products

Step 3
Combos + Modifiers

Step 4
Pricing + Taxes

Step 5
POS Cart + Orders

Step 6
Payments

Step 7
Cancellation + Refund + Void

Step 8
Shifts + Blind Close

Step 9
Printing

Step 10
KDS

Step 11
Inventory + UOM + BOM

Step 12
Offline + Idempotency

Step 13
Audit + Reports

Step 14
Procurement

Step 15
Good-to-have modules
```

---

# 85. المطلوب منك قبل كتابة الكود

قبل التنفيذ:

1. حلل المتطلبات.
2. اقترح Domain Model.
3. اقترح Module boundaries.
4. أنشئ ERD مبدئي.
5. أنشئ API Contract مبدئي.
6. أنشئ Screen Map.
7. أنشئ User Flows.
8. أنشئ Design System.
9. أنشئ Roadmap.
10. حدد المخاطر.
11. لا تبدأ في Coding حتى تكون النواة واضحة.

---

# 86. المطلوب في كل Module

لكل Module قدم:

- Purpose
- Entities
- Value Objects
- Business Rules
- Use Cases
- API Endpoints
- Permissions
- Database tables
- UI Screens
- Validation rules
- Audit requirements
- Unit tests
- Integration tests
- Acceptance criteria

---

# 87. شكل التسليم المطلوب

لكل Feature نفذ بالترتيب:

```text
1. Requirement
2. Domain rule
3. Database design
4. Backend service
5. API endpoint
6. Frontend UI
7. Validation
8. Permission
9. Audit
10. Test
11. Acceptance verification
```

ولا تعتبر Feature مكتملة قبل إنهاء النقاط أعلاه.

---

# 88. معيار النجاح النهائي

النظام الناجح ليس النظام الذي يحتوي أكبر عدد من الشاشات.

النظام الناجح هو الذي يحقق:

- بيع سريع
- أخطاء أقل
- تدريب أسهل
- رقابة مالية أقوى
- مخزون أدق
- معرفة أسباب الإلغاء
- متابعة أداء الفروع
- عمل مستقر دون إنترنت
- سهولة التوسع
- واجهة حديثة
- تجربة عربية ممتازة
- صيانة وتطوير أسهل

---

# 89. أهم قاعدة تصميم

> لا تعرض للمستخدم تعقيد النظام الداخلي.

مثال:

زر واحد:

```text
PAY
```

قد ينفذ داخليًا:

```text
Payment
↓
Financial Transaction
↓
Inventory Deduction
↓
Kitchen Ticket
↓
Print Job
↓
Audit Event
↓
Offline Sync
```

لكن المستخدم يجب أن يشعر أن العملية بسيطة وسريعة.

---

# 90. النتيجة المطلوبة

ابنِ نظام Oman Fried Chicken كمنصة مطاعم حديثة قابلة للاستخدام اليومي الفعلي، وليس Demo أو CRUD System.

الأولوية:

```text
Reliability
↓
Usability
↓
Correctness
↓
Performance
↓
Maintainability
↓
Advanced Features
```

أي ميزة متقدمة لا يجوز أن تأتي على حساب سرعة البيع، استقرار المطبخ، دقة النقدية، المخزون، أو سهولة استخدام الكاشير.

---

# 91. إضافات تقنية حرجة قبل بدء التنفيذ

تُعد المتطلبات التالية **Must Have / P0** لأنها تمس سلامة المبيعات أثناء العمل دون إنترنت، استمرارية تشغيل المطبخ، وأداء قاعدة البيانات.

## 91.1 Offline Price Lock & Price Snapshot

عند عمل جهاز POS في وضع **Offline** يجب أن يعتمد احتساب الأسعار حصراً على نسخة التسعير المحلية المزامنة مسبقاً مع الجهاز.

عند إضافة أي صنف أو Combo أو Modifier إلى الطلب، يجب تنفيذ Price Resolution محلياً وتثبيت القيم الناتجة داخل السلة والطلب كـ **Price Snapshot**.

يجب أن يتضمن الـSnapshot على الأقل:

```text
ProductId
PriceListId / PriceRuleId إن وجد
SalesChannelId
BranchId
BasePriceSnapshot
ModifierPriceSnapshot
PromotionIdSnapshot إن وجد
DiscountSnapshot
TaxRateSnapshot
TaxModeSnapshot
ResolvedUnitPrice
ResolvedAtLocal
PricingVersion / CatalogVersion
```

### قواعد إلزامية

1. أي تعديل سحابي على الأسعار بعد إنشاء الطلب **لا يعيد تسعير الطلب المفتوح أو المنفذ Offline تلقائياً**.
2. عند عودة الاتصال ومزامنة الطلب، يقبل الخادم السعر المثبت وقت إنشاء العملية إذا كانت العملية أنشئت وفق نسخة تسعير محلية صالحة وقتها.
3. لا يجوز للـBackend استبدال `PriceSnapshot` بسعر أحدث أثناء Sync.
4. يجب حفظ رقم نسخة أو إصدار التسعير `PricingVersion` الذي استخدمه جهاز POS.
5. إذا كانت نسخة التسعير المحلية قديمة بشكل يتجاوز حدًا إداريًا مسموحًا، يسجل النظام تنبيه `StalePricingWarning` دون إلغاء عملية بيع حقيقية تمت بالفعل.
6. في حالة فتح طلب قديم وتعديله بعد عودة الاتصال، يجب تطبيق سياسة واضحة:
   - العناصر السابقة تحتفظ بلقطتها.
   - العناصر الجديدة تستخدم السعر الساري وقت إضافتها.
   - لا يتم إعادة تسعير العناصر السابقة إلا عبر إجراء صريح ومخول مع Audit Log.
7. أي Price Override يدوي يحتاج صلاحية مستقلة وسببًا ويُسجل في Audit Trail.

### Acceptance Criteria

```text
Given POS-01 is offline
And Zinger Meal local price is 2.700 OMR
When head office changes the cloud price to 2.900 OMR
And POS-01 sells the meal before receiving the new pricing version
Then the order remains 2.700 OMR
And synchronization must not change the financial total
And the server records the PricingVersion used by POS-01
```

---

## 91.2 KDS Offline Fallback & Kitchen Continuity

يجب ألا يؤدي انقطاع الإنترنت أو تعطل خدمة KDS السحابية إلى إيقاف إنتاج الطلبات في المطبخ.

اعتمد مبدأ:

> **Kitchen operation must survive cloud connectivity failure.**

يجب تصميم طبقة توجيه الطلبات بحيث تدعم:

```text
Primary Route:
POS → KDS

Fallback Route:
POS → Local Print Agent → Kitchen Printer
```

### حالات التفعيل

يتم تشغيل Fallback عندما:

- يتعذر الوصول إلى KDS ضمن Timeout محدد.
- يفشل إرسال Kitchen Ticket بعد عدد محاولات محدد.
- تصبح قناة SignalR / WebSocket الخاصة بـKDS غير متاحة.
- تكون محطة KDS نفسها `Offline / Unhealthy`.
- يفعّل المشرف وضع الطباعة الاحتياطي يدويًا.

### قواعد إلزامية

1. لا يطبع النظام التذكرة الاحتياطية فور أول خطأ transient.
2. يجب وجود `KitchenDispatchId` أو معرف حتمي موحد لكل Kitchen Ticket لمنع التكرار بين KDS والطباعة.
3. يسجل النظام قناة التنفيذ:

```text
KDS
PRINT_FALLBACK
MANUAL_FALLBACK
```

4. إذا عاد KDS بعد أن تمت الطباعة بنجاح، لا يعيد النظام إرسال نفس الطلب كطلب جديد دون تمييز.
5. يجب أن يظهر على التذكرة الاحتياطية بوضوح أنها:

```text
OFFLINE / FALLBACK TICKET
```

مع:
- Order Number
- Created Time
- Station
- Items
- Modifiers
- Notes
- Cancellation state إن وجد

6. إذا ألغي الطلب بعد طباعة التذكرة، يجب طباعة **Cancellation Ticket** أو إرسال تنبيه محلي للمطبخ.
7. يجب وجود Health Check محلي لـ:
   - KDS station
   - Local Print Agent
   - Kitchen Printer

### حالات Kitchen Dispatch

```text
PENDING
SENT_TO_KDS
KDS_ACKNOWLEDGED
PRINT_FALLBACK_PENDING
PRINTED_FALLBACK
FAILED
CANCELLED
```

### Acceptance Criteria

```text
Given the internet connection is unavailable
And KDS cannot acknowledge the kitchen ticket
When the cashier confirms a paid order
Then the Local Print Agent prints the ticket to the configured kitchen station
And the order continues normally
And the dispatch is recorded as PRINTED_FALLBACK
And reconnecting the KDS must not create a duplicate kitchen order
```

---

## 91.3 Database Composite Index Strategy

يجب تصميم الفهارس بناءً على **أنماط الاستعلام الحقيقية** وليس إنشاء فهارس عشوائية لكل Foreign Key.

ينبغي إضافة الفهارس المركبة بعد تحليل Queries الأكثر تكراراً، خصوصًا في:

- Orders
- Payments
- Shifts
- InventoryTransactions
- FinancialTransactions
- AuditLog
- KitchenTickets
- Local/Sync Transactions
- Pricing Rules

### فهارس أولية موصى بها

> يجب تعديلها وفق الـSchema النهائي وخطط الاستعلام الفعلية.

```sql
-- منع تكرار المعاملة أثناء المزامنة
CREATE UNIQUE INDEX ux_transactions_idempotency_key
ON sync_transactions (idempotency_key);

-- استعلامات الورديات داخل الفرع
CREATE INDEX ix_shifts_branch_status_openedat
ON shifts (branch_id, status, opened_at DESC);

-- استعلامات الطلبات التشغيلية
CREATE INDEX ix_orders_branch_status_createdat
ON orders (branch_id, status, created_at DESC);

-- في حال تفعيل Multi-Tenant فعليًا
CREATE INDEX ix_orders_tenant_branch_createdat
ON orders (tenant_id, branch_id, created_at DESC);

-- الحركات المالية حسب الفرع والوردية
CREATE INDEX ix_financial_transactions_branch_shift_createdat
ON financial_transactions (branch_id, shift_id, created_at DESC);

-- الحركات المخزنية حسب المادة والفرع
CREATE INDEX ix_inventory_transactions_branch_item_createdat
ON inventory_transactions (branch_id, inventory_item_id, created_at DESC);

-- أحداث المطبخ الحالية
CREATE INDEX ix_kitchen_tickets_branch_station_status_createdat
ON kitchen_tickets (branch_id, station_id, status, created_at);

-- سجل التدقيق
CREATE INDEX ix_audit_log_branch_entity_timestamp
ON audit_log (branch_id, entity_type, entity_id, timestamp DESC);
```

### قواعد إلزامية

1. `IdempotencyKey` يجب أن يكون عليه **Unique Index / Unique Constraint**.
2. لا تضف `TenantId` إلى كل Index إلا إذا كان Multi-Tenancy مفعلًا فعليًا وكان Tenant جزءًا من الاستعلام.
3. ترتيب أعمدة الـComposite Index يجب أن يتبع أنماط `WHERE`, `JOIN`, و`ORDER BY`.
4. راقب Query Plans باستخدام:

```sql
EXPLAIN
EXPLAIN ANALYZE
```

5. لا تعتمد على كثرة الفهارس؛ فهي تزيد تكلفة `INSERT/UPDATE` وحجم التخزين.
6. أضف Partial Indexes عند وجود فائدة حقيقية، مثل الطلبات المفتوحة فقط.
7. ضع Index strategy ضمن مراجعة الأداء قبل الإنتاج، وليس فقط أثناء إنشاء الـSchema.

### مثال Partial Index

```sql
CREATE INDEX ix_orders_open_branch_createdat
ON orders (branch_id, created_at DESC)
WHERE status IN ('PENDING', 'CONFIRMED', 'PAID', 'PREPARING', 'READY');
```

---

# 92. تحديث معايير القبول الحرجة

أضف الاختبارات التالية إلى سيناريوهات القبول قبل أي إطلاق إنتاجي:

## Offline Pricing Test

- مزامنة Price List إلى جهاز POS.
- قطع الإنترنت.
- تعديل السعر سحابيًا.
- تنفيذ طلب بالسعر المحلي السابق.
- إعادة الاتصال.
- التأكد من عدم تغير Total أو VAT أو Financial Ledger عند Sync.

## Kitchen Fallback Test

- تشغيل POS وقطع الاتصال عن KDS.
- تنفيذ طلب.
- التأكد من الطباعة الاحتياطية.
- إعادة KDS.
- التأكد من عدم تكرار Kitchen Ticket.

## Idempotency Load Test

- إرسال نفس Transaction عشرات المرات بنفس IdempotencyKey.
- يجب إنشاء Business Transaction واحدة فقط.

## Database Performance Test

اختبر على بيانات مماثلة للإنتاج المتوقع:

- Orders
- InventoryTransactions
- FinancialTransactions
- AuditLog

وقِس:
- Query latency
- Index usage
- Write throughput
- Lock contention
