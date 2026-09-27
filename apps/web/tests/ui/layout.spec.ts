import { test, expect, type Page } from '@playwright/test';

const permissions = ['users.manage','branches.manage','devices.manage','orders.manage','kitchen.view','inventory.view','procurement.view','shifts.open','printing.view','cancellations.manage','catalog.categories.manage','catalog.products.manage','catalog.selection-groups.manage','pricing.manage','reports.view','qr.manage','integrations.view'];
const branches = [{ id: 'b1', code: 'SUW', nameAr: 'فرع السويق', nameEn: 'Suwaiq branch', isActive: true, settings: [], timeZone: 'Asia/Muscat' }];
const roles = [{ id: 'r1', name: 'Restaurant manager', permissions }];
async function setup(page: Page, language: string, theme: string) {
  await page.addInitScript(({ language, theme }) => {
    localStorage.setItem('ofc:session-token', JSON.stringify('local-ui-test'));
    localStorage.setItem('ofc:language', JSON.stringify(language));
    localStorage.setItem('ofc:theme', JSON.stringify(theme));
  }, { language, theme });
  // Never forward test traffic to the online restaurant API.
  await page.route('**/hubs/**', route => route.abort());
  await page.route('**/api/**', async route => {
    const p = new URL(route.request().url()).pathname;
    let data: unknown = [];
    if (p.endsWith('/auth/me')) data = { permissions };
    else if (p.endsWith('/pos/context')) data = { branches, channels: [{id:'c1',code:'DINEIN',nameAr:'محلي',nameEn:'Dine in'},{id:'c2',code:'TAKEAWAY',nameAr:'سفري',nameEn:'Takeaway'},{id:'c3',code:'TALABAT',nameAr:'طلبات',nameEn:'Talabat'}] };
    else if (p.endsWith('/pos/catalog')) data = Array.from({length:24}, (_,i)=>({id:`p${i}`,sku:`SKU${i}`,categoryId:'cat1', categoryNameAr:'الوجبات',categoryNameEn:'Meals',nameAr:`وجبة الدجاج المقرمش ${i+1}`,nameEn:`Crispy chicken family meal ${i+1}`,imageUrl:null,basePrice:2.5,pricing:{listPrice:2.5,discountRate:0,taxRate:0,taxCalculationMode:'Inclusive'},selectionGroups:[]}));
    else if (p.endsWith('/branches')) data = branches;
    else if (p.endsWith('/roles')) data = roles;
    else if (/\/users\/[^/]+\/permissions$/.test(p)) data = {permissions:[]};
    else if (p.endsWith('/permissions')) data = permissions.map((code,i)=>({id:`perm${i}`,code}));
    else if (p.endsWith('/users')) data = Array.from({length:12},(_,i)=>({id:`u${i}`,username:`suwaiq${i}`,displayName:i===0?'مدير مطعم السويق Restaurant Manager':`User ${i}`, email:'manager.long.email@example.com',roles:['Restaurant manager'],branchIds:['b1'],isActive:true}));
    else if (p.endsWith('/orders/history')) data = {total:0,items:[]};
    else if (p.endsWith('/payment-methods')) data = [{id:'cash',code:'CASH',nameAr:'نقد',nameEn:'Cash',kind:'Cash'},{id:'card',code:'CARD',nameAr:'بطاقة',nameEn:'Card',kind:'Card'},{id:'external',code:'EXTERNAL',nameAr:'دفع خارجي',nameEn:'External payment',kind:'External'}];
    else if (p.endsWith('/orders') && route.request().method()==='GET') data = Array.from({length:8},(_,i)=>({id:`order-${i+1}`,status:i%3===0?'Paid':'Pending',grossAmount:2.5+i,note:i===2?'Pickup customer Ahmed':'',createdAt:new Date(Date.now()-i*60000).toISOString(),table:i%2===0?{code:`T${i+1}`,nameAr:`طاولة ${i+1}`,nameEn:`Table ${i+1}`}:null}));
    else if (p.endsWith('/orders') && route.request().method()==='POST') data = {id:'o1',grossAmount:2.5};
    else if (p.endsWith('/orders/o1/status')) data = {id:'o1',grossAmount:2.5};
    else if (p.endsWith('/orders/o1/payments')) data = {payments:[]};
    else if (p.endsWith('/kitchen/tickets')) data = [{id:'k1'}];
    await route.fulfill({json:data});
  });
}

test('cash payment dispatches once only after payment', async ({page}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  const kitchen: string[] = []; page.on('request', request => { if(request.method()==='POST' && new URL(request.url()).pathname.endsWith('/kitchen/tickets')) kitchen.push(request.url()); });
  await page.goto('/#/pos');
  await page.getByRole('button',{name:/Crispy chicken family meal 1 OMR/}).first().click();
  expect(kitchen).toHaveLength(0);
  // Payment methods stay hidden until the cashier presses Pay.
  await expect(page.getByRole('button',{name:'Cash',exact:true})).toHaveCount(0);
  await page.getByRole('button',{name:'Pay',exact:true}).click();
  expect(kitchen).toHaveLength(0);
  await page.getByRole('button',{name:'Cash',exact:true}).click();
  await page.route('**/orders/o1/payments', async route => {
    expect(kitchen).toHaveLength(0);
    expect(route.request().postDataJSON().payments[0].paymentMethodId).toBe('cash');
    await route.fulfill({json:{payments:[]}});
  });
  await page.getByRole('button',{name:'Confirm payment',exact:true}).click();
  await expect.poll(()=>kitchen.length).toBe(1);
});

test('electronic checkout remains visible on a short Arabic desktop', async ({page}) => {
  await page.setViewportSize({width:1920,height:850});
  await setup(page,'ar','light');
  await page.goto('/#/pos');
  await page.locator('html').evaluate(element => { element.style.zoom = '1.4'; });
  await page.getByRole('radio',{name:'إلكتروني',exact:true}).click();
  await page.getByRole('button',{name:/وجبة الدجاج المقرمش 1 OMR/}).first().click();
  const checkout = page.getByRole('button',{name:'تأكيد وإرسال للمطبخ',exact:true});
  await expect(checkout).toBeEnabled();
  await expect(checkout).toBeInViewport({ratio:1});
  await checkout.click();
  await expect(page.getByText('السلة 0', {exact:false})).toBeVisible();
});

test('electronic company order skips payment and dispatches once', async ({page}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  const posted: string[] = []; page.on('request', request => { if(request.method()==='POST') posted.push(new URL(request.url()).pathname); });
  await page.goto('/#/pos');
  await page.getByRole('radio',{name:'Electronic',exact:true}).click();
  await page.getByRole('radio',{name:'Talabat',exact:true}).click();
  await page.getByRole('button',{name:/Crispy chicken family meal 1 OMR/}).first().click();
  await page.getByRole('button',{name:'Confirm & send to kitchen',exact:true}).click();
  await expect.poll(()=>posted.filter(path=>path.endsWith('/kitchen/tickets')).length).toBe(1);
  expect(posted.filter(path=>path.endsWith('/payments'))).toHaveLength(1);
  expect(posted.filter(path=>path.endsWith('/orders/o1/status'))).toHaveLength(1);
});
async function noOverflow(page: Page) {
  expect(await page.evaluate(()=>document.documentElement.scrollWidth <= innerWidth + 1)).toBeTruthy();
}

test('register bounds a 500-line receipt and keeps totals pinned at 1080p', async ({page}, info) => {
  await page.setViewportSize({width:1920,height:1080});
  await setup(page,'en','light');
  await page.addInitScript(() => {
    const product = {id:'p0',sku:'SKU0',barcode:null,categoryId:'cat1',nameAr:'وجبة',nameEn:'Crispy chicken family meal 1',imageUrl:null,pricing:{listPrice:2.5,discountRate:0,taxRate:0,taxCalculationMode:'Inclusive'},selectionGroups:[]};
    localStorage.setItem('ofc:pos-cart',JSON.stringify(Array.from({length:500},(_,i)=>({key:`line-${i}`,product,quantity:1,note:'',selections:{}}))));
  });
  await page.goto('/#/pos');
  const ticket = page.locator('.pos-ticket-desktop');
  await expect(ticket.getByTestId('ticket-total')).toContainText('1250.000');
  expect(await ticket.getByRole('listitem').count()).toBeLessThan(20);
  const footer = ticket.locator('.pos-ticket-footer');
  await expect(footer).toBeInViewport({ratio:1});
  const before = await footer.boundingBox();
  await ticket.locator('.pos-ticket-scroll').evaluate(element => { element.scrollTop = element.scrollHeight; });
  await expect(ticket.getByRole('listitem').last()).toHaveAttribute('aria-posinset','500');
  expect(await footer.boundingBox()).toEqual(before);
  await ticket.getByRole('button',{name:'Increase',exact:true}).last().click();
  await expect(ticket.getByTestId('ticket-total')).toContainText('1252.500');
  await expect(ticket.getByTestId('ticket-tax')).toBeInViewport({ratio:1});
  await expect(ticket.getByTestId('ticket-discount')).toBeInViewport({ratio:1});
  const smallTargets = await page.locator('.pos-shell button:visible').evaluateAll(buttons => buttons.filter(button => {
    const rect = button.getBoundingClientRect();
    return rect.width < 40 || rect.height < 40;
  }).map(button => button.getAttribute('aria-label') || button.textContent));
  expect(smallTargets).toEqual([]);
  await noOverflow(page);
  await page.screenshot({path:info.outputPath('register-1080p-large-ticket.png')});
});

test('register scanner bursts append once without stealing notes or keyboard events', async ({page}) => {
  await page.setViewportSize({width:1366,height:900});
  await setup(page,'en','dark');
  await page.goto('/#/pos');
  const product = page.getByRole('button',{name:/Crispy chicken family meal 1 OMR/}).first();
  await product.click();
  await product.focus();
  await page.keyboard.type('SKU0',{delay:5});
  await page.keyboard.press('Enter');
  await expect(page.locator('.pos-ticket-desktop').getByTestId('ticket-total')).toContainText('5.000');
  await page.evaluate(() => {
    (window as any).__scanEvents = 0;
    window.addEventListener('keydown',() => (window as any).__scanEvents++);
    for (let i=0;i<10;i++) for (const key of ['S','K','U','0','Enter']) {
      document.activeElement!.dispatchEvent(new KeyboardEvent('keydown',{key,bubbles:true,cancelable:true}));
    }
  });
  await expect(page.locator('.pos-ticket-desktop').getByTestId('ticket-total')).toContainText('30.000');
  expect(await page.evaluate(() => (window as any).__scanEvents)).toBe(50);
  const note = page.locator('.pos-ticket-desktop input').first();
  await note.fill('SKU0');
  await note.press('Enter');
  await expect(page.locator('.pos-ticket-desktop').getByTestId('ticket-total')).toContainText('30.000');
  await page.keyboard.press('F2');
  await expect(page.getByRole('textbox',{name:'Search products',exact:true})).toBeFocused();
  await page.getByRole('textbox',{name:'Search products',exact:true}).fill('SKU0');
  await page.keyboard.press('Enter');
  await expect(page.locator('.pos-ticket-desktop').getByTestId('ticket-total')).toContainText('32.500');
});

test('register keeps discounted tax and checkout visible while editing a discount', async ({page}) => {
  await page.setViewportSize({width:1366,height:900});
  await setup(page,'en','dark');
  await page.route('**/auth/me',route => route.fulfill({json:{permissions:[...permissions,'orders.discount']}}));
  await page.route('**/pos/catalog?**',route => route.fulfill({json:[{id:'taxed',sku:'TAX',categoryId:'cat1',categoryNameEn:'Meals',categoryNameAr:'وجبات',nameEn:'Taxed meal',nameAr:'وجبة',imageUrl:null,pricing:{listPrice:10,discountRate:0,taxRate:5,taxCalculationMode:'Exclusive'},selectionGroups:[]}]}));
  await page.goto('/#/pos');
  await page.getByRole('button',{name:'Taxed meal OMR 10.500',exact:true}).click();
  // Discount lives in the payment popup next to the payment method.
  await page.getByRole('button',{name:'Pay',exact:true}).click();
  await page.getByRole('button',{name:'Add discount',exact:true}).click();
  await page.getByRole('textbox',{name:'Add discount',exact:true}).fill('10');
  await expect(page.getByTestId('ticket-total')).toContainText('9.450');
  await expect(page.getByTestId('ticket-tax')).toContainText('0.450');
  await expect(page.getByTestId('ticket-discount')).toContainText('1.050');
  await expect(page.getByRole('button',{name:'Confirm payment',exact:true})).toBeInViewport({ratio:1});
});

for (const theme of ['light','dark']) for (const language of ['ar','en']) test(`register full-screen ${language} ${theme} with real menu photos`, async ({page}, info) => {
  await page.setViewportSize({width:1920,height:1080});
  await setup(page,language,theme);
  await page.route('**/pos/catalog?**',route => route.fulfill({json:Array.from({length:24},(_,index) => {
    const meals = [
      ['Big Bucket · 21 pieces','دلو الدجاج الكبير · ٢١ قطعة','Big_Bucket_Crispy_21pcs.jpg',9.5],
      ['Grilled Bucket · 21 pieces','دلو الدجاج المشوي · ٢١ قطعة','Big_Bucket_Grilled_21pcs.jpg',9.5],
      ['Crispy Family Box','وجبة العائلة المقرمشة','Crispy_Family_Box_15pcs.jpg',7.25],
      ['Grilled Family Box','وجبة العائلة المشوية','Grilled_Family_Box_15pcs.jpg',7.25],
    ] as const;
    const meal = meals[index % meals.length];
    return {id:`p${index}`,sku:`SKU${index}`,categoryId:'cat1',categoryNameEn:'Family meals',categoryNameAr:'الوجبات العائلية',nameEn:meal[0],nameAr:meal[1],imageUrl:`/menu/01_Family_Meals_Page2/${meal[2]}`,pricing:{listPrice:meal[3],discountRate:0,taxRate:5,taxCalculationMode:'Inclusive'},selectionGroups:[]};
  })}));
  await page.goto('/#/pos');
  await page.locator('.pos-product').first().click();
  await page.locator('.pos-product').nth(1).click();
  await page.locator('.pos-product').nth(2).click();
  await expect(page.locator('.pos-ticket-footer')).toBeInViewport({ratio:1});
  await expect(page.locator('.pos-product img').first()).toBeVisible();
  await page.locator('.pos-product img').first().evaluate((image: HTMLImageElement) => image.decode());
  await page.screenshot({path:info.outputPath(`register-${language}-${theme}-1080p.png`)});
});

test('register shares the application theme with header, receipt and portaled controls', async ({page}) => {
  await page.setViewportSize({width:1920,height:1080});
  await setup(page,'en','light');
  await page.goto('/#/pos');
  await expect(page.locator('.pos-product').first()).toBeVisible();
  const surface = () => page.locator('.pos-ticket').evaluate(element => getComputedStyle(element).backgroundColor);
  const light = await surface();
  expect(await page.locator('.app-header').evaluate(element => getComputedStyle(element).backgroundColor)).toBe(light);
  await expect(page.locator('.pos-register')).not.toHaveAttribute('data-theme','dark');
  await page.getByRole('button',{name:'Change appearance',exact:true}).click();
  await page.getByRole('button',{name:'Dark',exact:true}).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme','dark');
  await expect.poll(surface).not.toBe(light);
  const dark = await surface();
  expect(await page.locator('.app-header').evaluate(element => getComputedStyle(element).backgroundColor)).toBe(dark);
  await page.getByRole('button',{name:'Close',exact:true}).click();
  await page.locator('.pos-toolbar').getByRole('combobox').click();
  const portal = page.locator('[data-slot="popover-content"]');
  await expect(portal).toBeVisible();
  expect(await portal.evaluate(element => getComputedStyle(element).getPropertyValue('--app-primary').trim())).toBe(await page.locator('.pos-register').evaluate(element => getComputedStyle(element).getPropertyValue('--app-primary').trim()));
  await page.keyboard.press('Escape');
  await page.getByRole('button',{name:'Change appearance',exact:true}).click();
  await page.getByRole('button',{name:'Light',exact:true}).click();
  await expect.poll(surface).toBe(light);
});

test('system theme follows the operating-system preference without reloading', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.setViewportSize({ width: 1366, height: 900 });
  await setup(page, 'en', 'system');
  await page.goto('/#/users');

  await expect(page.locator('html')).toHaveAttribute('data-theme-mode', 'system');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

  await page.emulateMedia({ colorScheme: 'light' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
});

for (const width of [375, 768, 1366, 1920]) for (const theme of ['light', 'dark']) {
  test(`current orders responsive ${width} ${theme}`, async ({ page }, info) => {
    await page.setViewportSize({ width, height: 700 });
    await setup(page, 'en', theme);
    await page.goto('/#/pos');
    await page.getByRole('button', { name: /Current orders/ }).click();
    const dialog = page.getByRole('dialog', { name: 'Current orders' });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole('searchbox')).toBeVisible();
    await page.screenshot({ path: info.outputPath('current-orders-full.png') });
    await dialog.getByRole('searchbox').fill('Ahmed');
    await expect(dialog.getByText('Pickup customer Ahmed').filter({ visible: true })).toBeVisible();
    await expect(dialog.getByText('order-1', { exact: false })).toHaveCount(0);
    await expect(dialog.locator('div.sticky').first()).toHaveCSS('position', 'sticky');
    await noOverflow(page);
    await page.screenshot({ path: info.outputPath('current-orders.png') });
  });
}

for (const width of [375,768,1366,1920]) for (const language of ['ar','en']) for (const theme of ['light','dark']) {
  test(`users ${width} ${language} ${theme}`, async ({page}, info) => {
    await page.setViewportSize({width,height:900}); await setup(page,language,theme);
    await page.goto('/#/users');
    await expect(page.getByRole('heading',{name:language==='ar'?'المستخدمون':'Users',exact:true})).toBeVisible();
    await noOverflow(page);
    await page.getByRole('button',{name:language==='ar'?'إضافة مستخدم':'Add user',exact:true}).click();
    const name = language==='ar'?'مستخدم جديد':'New user';
    await expect(page.getByRole('heading',{name,exact:true}).last()).toBeVisible();
    await noOverflow(page);
    const input = page.locator('input').filter({visible:true}).first();
    await input.fill('ui-test');
    await page.screenshot({path:info.outputPath('user-dialog.png')});
    await page.getByRole('combobox').first().click();
    const option = page.getByRole('option').filter({hasText: language==='ar'?'منح':'Grant'}).first();
    await expect(option).toBeVisible(); await option.click();
    await page.keyboard.press('Escape');
  });
  test(`pos ${width} ${language} ${theme}`, async ({page}, info) => {
    await page.setViewportSize({width,height:900}); await setup(page,language,theme);
    await page.goto('/#/pos');
    const product=page.getByRole('button',{name:language==='ar'?/وجبة الدجاج المقرمش 1 OMR/:/Crispy chicken family meal 1 OMR/}).first();
    await expect(product).toBeVisible(); await noOverflow(page); await product.click();
    await page.screenshot({path:info.outputPath('pos.png')});
    if(width>=1024){
      await page.getByRole('button',{name:language==='ar'?'وضع الأكشاك':'Kiosk mode',exact:true}).click();
      await expect(page.getByRole('button',{name:language==='ar'?'خروج من وضع الأكشاك':'Exit kiosk mode',exact:true})).toBeVisible();
      await noOverflow(page); await page.screenshot({path:info.outputPath('kiosk.png')});
    }
  });
}

test('clear order empties the cart only after a confirming second tap', async ({page}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  await page.goto('/#/pos');
  await page.getByRole('button',{name:/Crispy chicken family meal 1 OMR/}).first().click();
  await page.getByRole('button',{name:/Crispy chicken family meal 2 OMR/}).first().click();
  const ticket = page.locator('.pos-ticket-desktop');
  await expect(ticket.getByRole('listitem')).toHaveCount(2);
  await ticket.getByRole('button',{name:'Clear',exact:true}).click();
  await expect(ticket.getByRole('listitem')).toHaveCount(2);
  await ticket.getByRole('button',{name:'Confirm clear',exact:true}).click();
  await expect(ticket.getByRole('listitem')).toHaveCount(0);
  await expect(ticket.getByRole('button',{name:'Clear',exact:true})).toHaveCount(0);
});

test('current orders ask for the open shift and show table names and order numbers', async ({page}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  let query = '';
  await page.route('**/api/v1/orders?**', async route => {
    const url = new URL(route.request().url());
    if (route.request().method() !== 'GET' || url.pathname !== '/api/v1/orders') return route.fallback();
    query = url.search;
    await route.fulfill({json:[{id:'order-1',number:1042,status:'Pending',grossAmount:4.5,note:'',createdAt:new Date().toISOString(),table:{code:'QR-T7-X9',nameAr:'طاولة 7',nameEn:'Table 7'}}]});
  });
  await page.goto('/#/pos');
  await page.getByRole('button',{name:/Current orders/}).first().click();
  const dialog = page.getByRole('dialog',{name:'Current orders'});
  await expect(dialog.getByRole('button',{name:'#1042'}).last()).toBeVisible();
  await expect(dialog.getByText('Table 7').last()).toBeVisible();
  await expect(dialog.getByText('QR-T7-X9')).toHaveCount(0);
  expect(query).toContain('scope=shift');
});

test('F11 toggles kiosk mode', async ({page}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  await page.goto('/#/users');
  await expect(page.locator('.app-header')).not.toHaveClass(/kiosk-header/);
  await page.keyboard.press('F11');
  await expect(page.locator('.app-header')).toHaveClass(/kiosk-header/);
  await page.keyboard.press('F11');
  await expect(page.locator('.app-header')).not.toHaveClass(/kiosk-header/);
});

const kdsTicket = (id: string) => ({id,branchId:'b1',orderId:`o-${id}`,dispatchId:`d-${id}`,orderNumber:'21',stationId:null,stationCode:null,stationNameAr:null,stationNameEn:null,dispatchStatus:'SentToKds',channel:'Kds',status:'New',targetMinutes:15,kdsAttempts:1,fallbackPrinted:false,lastError:null,note:null,wasPrepStartedBeforeCancellation:false,cancellationNotified:false,createdAt:new Date().toISOString(),startedAt:null,readyAt:null,completedAt:null,cancelledAt:null,acknowledgedAt:null,fallbackPrintedAt:null,updatedAt:new Date().toISOString(),overdue:false,items:[{id:`i-${id}`,orderLineId:null,productId:'p0',productNameAr:'وجبة',productNameEn:'Chicken meal',quantity:2,note:null,selections:'[]',status:'New',startedAt:null,readyAt:null,completedAt:null}]});

test('kitchen tablet shows new tickets ready to acknowledge without a send step', async ({page}) => {
  await page.setViewportSize({width:1280,height:800}); await setup(page,'en','light');
  await page.route('**/api/v1/kitchen/tickets?**', route => route.fulfill({json:[kdsTicket('t1')]}));
  await page.goto('/#/kitchen');
  await expect(page.getByRole('button',{name:'Received',exact:true})).toBeVisible();
  await expect(page.getByText('New',{exact:true}).first()).toBeVisible();
  await expect(page.getByRole('button',{name:'Send to KDS'})).toHaveCount(0);
});

test('cashier is warned when the kitchen does not acknowledge and can print the slip', async ({page}) => {
  await page.clock.install();
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  await page.addInitScript(() => { (window as unknown as {__printed:number}).__printed = 0; HTMLIFrameElement.prototype.focus = () => {}; });
  // Every GET of the order list reports the dispatched order still waiting in the kitchen.
  await page.route('**/api/v1/orders?**', async route => {
    if (route.request().method() !== 'GET' || new URL(route.request().url()).pathname !== '/api/v1/orders') return route.fallback();
    await route.fulfill({json:[{id:'o1',number:77,status:'SentToKitchen',grossAmount:2.5,note:'',createdAt:new Date().toISOString(),table:null}]});
  });
  await page.route('**/api/v1/orders/o1', route => route.fulfill({json:{id:'o1',number:77,status:'SentToKitchen',note:null,netAmount:2.5,taxAmount:0,grossAmount:2.5,createdAt:new Date().toISOString(),lines:[{id:'l1',productId:'p0',productNameAr:'وجبة',productNameEn:'Crispy chicken family meal 1',quantity:1,note:'No onion',unitGrossAmount:2.5,selectionsSnapshot:'[{"NameAr":"مشروبات","choices":[{"NameAr":"كولا","NameEn":"Cola","Quantity":1}]}]'}]}}));
  await page.goto('/#/pos');
  await page.getByRole('radio',{name:'Electronic',exact:true}).click();
  await page.getByRole('radio',{name:'Talabat',exact:true}).click();
  await page.getByRole('button',{name:/Crispy chicken family meal 1 OMR/}).first().click();
  await page.getByRole('button',{name:'Confirm & send to kitchen',exact:true}).click();
  await expect(page.getByRole('alert').filter({hasText:'Not received in the kitchen'})).toHaveCount(0);
  await page.clock.fastForward(26_000);
  const alert = page.locator('.pos-kitchen-alert');
  await expect(alert).toContainText('#77');
  await expect(alert).toContainText('Not received in the kitchen');
  const printed = page.waitForEvent('frameattached');
  await alert.getByRole('button',{name:'Print for kitchen'}).click();
  const frame = await printed;
  await expect.poll(async () => (await frame.content()).includes('Cola') && (await frame.content()).includes('No onion')).toBe(true);
  await expect(alert).toHaveCount(0);
});

async function openOrdersRoute(page: Page, orders: unknown[]) {
  await page.route('**/api/v1/orders?**', async route => {
    if (route.request().method() !== 'GET' || new URL(route.request().url()).pathname !== '/api/v1/orders') return route.fallback();
    await route.fulfill({json:orders});
  });
}

test('a held order can be reopened, extended and paid as the same order', async ({page}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  await openOrdersRoute(page, [{id:'o5',number:5,status:'Pending',salesChannelId:'c1',grossAmount:2.5,note:'',createdAt:new Date().toISOString(),table:null}]);
  await page.route('**/api/v1/orders/o5', route => route.fulfill({json:{id:'o5',number:5,salesChannelId:'c1',status:'Pending',note:null,netAmount:2.5,taxAmount:0,grossAmount:2.5,manualDiscountAmount:0,createdAt:new Date().toISOString(),lines:[{id:'l1',productId:'p0',productNameAr:'وجبة',productNameEn:'Crispy chicken family meal 1',quantity:1,note:null,unitGrossAmount:2.5,selectionsSnapshot:'[]'}]}}));
  const calls: string[] = [];
  page.on('request', request => { const u = new URL(request.url()); if (request.method() !== 'GET' && u.pathname.startsWith('/api/')) calls.push(`${request.method()} ${u.pathname}`); });
  await page.route('**/api/v1/orders/o5/lines', route => route.fulfill({json:{id:'o5',grossAmount:5}}));
  await page.route('**/api/v1/orders/o5/payments', route => route.fulfill({json:{payments:[]}}));
  await page.goto('/#/pos');
  await page.getByRole('button',{name:/Current orders/}).first().click();
  await page.getByRole('dialog',{name:'Current orders'}).getByRole('button',{name:'Edit',exact:true}).last().click();
  const ticket = page.locator('.pos-ticket-desktop');
  await expect(ticket.getByText('Editing order #5')).toBeVisible();
  await expect(ticket.getByRole('listitem')).toHaveCount(1);
  await page.getByRole('button',{name:/Crispy chicken family meal 2 OMR/}).first().click();
  await expect(ticket.getByRole('listitem')).toHaveCount(2);
  await ticket.getByRole('button',{name:'Pay',exact:true}).click();
  await page.getByRole('button',{name:'Confirm payment',exact:true}).click();
  await expect.poll(() => calls.includes('POST /api/v1/orders/o5/payments')).toBe(true);
  expect(calls).toContain('PUT /api/v1/orders/o5/lines');
  expect(calls).not.toContain('POST /api/v1/orders');
  expect(calls).not.toContain('POST /api/v1/orders/o5/status');
  await expect(ticket.getByText('Editing order #5')).toHaveCount(0);
});

test('an add-on for a paid order is a new invoice that references it', async ({page}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  await openOrdersRoute(page, [{id:'o9',number:9,status:'Paid',salesChannelId:'c1',grossAmount:2.5,note:'',createdAt:new Date().toISOString(),table:null}]);
  let created: {note: string | null} | null = null;
  page.on('request', request => { if (request.method() === 'POST' && new URL(request.url()).pathname === '/api/v1/orders') created = request.postDataJSON(); });
  await page.goto('/#/pos');
  await page.getByRole('button',{name:/Current orders/}).first().click();
  await page.getByRole('dialog',{name:'Current orders'}).getByRole('button',{name:'Add-on order',exact:true}).last().click();
  const ticket = page.locator('.pos-ticket-desktop');
  await expect(ticket.getByText('Add-on for order #9')).toBeVisible();
  await page.getByRole('button',{name:/Crispy chicken family meal 1 OMR/}).first().click();
  await ticket.getByRole('button',{name:'Hold',exact:true}).click();
  await expect.poll(() => created?.note ?? null).toBe('Add-on for order #9');
});

test('reports dashboard charts the period with a table view for every chart', async ({page}) => {
  await page.setViewportSize({width:1366,height:1200}); await setup(page,'en','light');
  const hour = new Date(); hour.setMinutes(0,0,0);
  await page.route('**/api/v1/reports/dashboard?**', route => route.fulfill({json:{todaySales:30,orderCount:4,averageOrderValue:7.5,openShifts:1,cashVariance:0,refunds:{count:0,amount:0},cancellations:{count:1,amount:5,rate:0.2},lowStockCount:0,waste:{quantity:0},kitchen:{avgPrepMinutes:9,overdue:0}}}));
  await page.route('**/api/v1/reports/sales?**', route => route.fulfill({json:{summary:{},daily:[],hourly:[{hour:hour.toISOString(),orderCount:4,grossSales:30}],byChannel:[{channelId:'c1',channelNameAr:'محلي',channelNameEn:'Dine in',orderCount:4,grossSales:30}],byProduct:[{productId:'p0',nameAr:'وجبة',nameEn:'Chicken meal',quantity:6}],byCategory:[{categoryId:'k1',categoryNameAr:'وجبات',categoryNameEn:'Meals',quantity:4,grossSales:22},{categoryId:'k2',categoryNameAr:'مشروبات',categoryNameEn:'Drinks',quantity:3,grossSales:8}],byPayment:[{paymentMethodId:'m1',nameAr:'نقد',nameEn:'Cash',count:3,amount:20},{paymentMethodId:'m2',nameAr:'بطاقة',nameEn:'Card',count:1,amount:10}]}}));
  await page.goto('/#/reports');
  await expect(page.locator('.viz-columns:not(.viz-columns-categories) .viz-column')).toHaveCount(1);
  await expect(page.locator('.viz-bar-label',{hasText:'Chicken meal'})).toBeVisible();
  await expect(page.locator('.viz-columns-categories .viz-column')).toHaveCount(2);
  await expect(page.locator('.viz-cap').first()).toBeVisible();
  await expect(page.locator('.viz-share-segment')).toHaveCount(2);
  await expect(page.locator('.viz-meter-danger')).toBeVisible();
  await expect(page.getByText('Very high')).toBeVisible();
  await expect(page.locator('details.viz-table')).toHaveCount(7);
  await expect(page.locator('.viz-line-path')).toHaveCount(1);
  await expect(page.locator('.viz-donut-segment')).toHaveCount(1);
  await expect(page.locator('.viz-gauge.viz-meter-good')).toContainText('On target');
  await page.locator('.viz-line-plot').focus();
  await expect(page.locator('.viz-line-plot .viz-tooltip')).toContainText('4 orders');
  await page.locator('.viz-share-segment').first().focus();
  await expect(page.locator('.viz-tooltip')).toContainText('67%');
  await noOverflow(page);
});

test('register header shows the cashier, the open shift and the time', async ({page}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  await page.route('**/api/v1/auth/me', route => route.fulfill({json:{permissions, displayName:'Sara Al-Balushi'}}));
  const opened = new Date(); opened.setHours(8,30,0,0);
  await page.route('**/api/v1/shifts/current?**', route => route.fulfill({json:{shift:{openedAt:opened.toISOString()}}}));
  await page.goto('/#/pos');
  const session = page.locator('.pos-session');
  await expect(session).toContainText('Sara Al-Balushi');
  await expect(session).toContainText('Shift open since');
  await expect(session.locator('time')).toBeVisible();
});

test('every configured non-cash method can take the payment', async ({page}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  await page.route('**/api/v1/payment-methods**', route => route.fulfill({json:[{id:'cash',code:'CASH',nameAr:'نقد',nameEn:'Cash',kind:'Cash'},{id:'card',code:'CARD',nameAr:'بطاقة',nameEn:'Card',kind:'Card'},{id:'apple',code:'APPLE',nameAr:'آبل باي',nameEn:'Apple Pay',kind:'ApplePay'}]}));
  let paidWith = '';
  await page.route('**/orders/o1/payments', async route => { paidWith = route.request().postDataJSON().payments[0].paymentMethodId; await route.fulfill({json:{payments:[]}}); });
  await page.goto('/#/pos');
  await page.getByRole('button',{name:/Crispy chicken family meal 1 OMR/}).first().click();
  await page.getByRole('button',{name:'Pay',exact:true}).click();
  await page.getByRole('button',{name:'Apple Pay',exact:true}).click();
  await page.getByRole('button',{name:'Confirm payment',exact:true}).click();
  await expect.poll(() => paidWith).toBe('apple');
});

test('customer display mirrors the cart and thanks the customer after payment', async ({page, context}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  await page.goto('/#/pos');
  const display = await context.newPage();
  await display.goto('/#/customer-display');
  await expect(display.getByRole('heading',{name:'Welcome'})).toBeVisible();
  // A delivery-company order is paid externally, so it completes in one step.
  await page.getByRole('radio',{name:'Electronic',exact:true}).click();
  await page.getByRole('radio',{name:'Talabat',exact:true}).click();
  await page.getByRole('button',{name:/Crispy chicken family meal 1 OMR/}).first().click();
  await page.getByRole('button',{name:/Crispy chicken family meal 1 OMR/}).first().click();
  await expect(display.locator('.cd-lines li')).toHaveCount(1);
  await expect(display.locator('.cd-qty')).toHaveText('2×');
  await expect(display.locator('.cd-total strong')).toContainText('5.000');
  await page.getByRole('button',{name:'Confirm & send to kitchen',exact:true}).click();
  await expect(display.getByRole('heading',{name:'Thank you'})).toBeVisible();
});

const offlineOp = (key: string) => ({idempotencyKey:key,operationType:'order.create',baseVersion:null,baseCatalogVersion:null,occurredAt:new Date().toISOString(),payload:{status:'Paid',lines:[]}});
const idbOutbox = (page: Page) => page.evaluate(() => new Promise<string[]>((resolve, reject) => {
  const open = indexedDB.open('ofc-offline');
  open.onerror = () => reject(open.error);
  open.onsuccess = () => {
    const request = open.result.transaction('outbox').objectStore('outbox').getAll();
    request.onsuccess = () => { resolve((request.result as Array<{idempotencyKey:string}>).map(x => x.idempotencyKey)); open.result.close(); };
  };
}));

test('offline queue left in localStorage moves into IndexedDB without losing a sale', async ({page}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  await page.addInitScript((ops) => {
    if (!localStorage.getItem('seeded')) {
      localStorage.setItem('ofc:sync:outbox', JSON.stringify(ops));
      localStorage.setItem('seeded', '1');
    }
  }, [offlineOp('legacy-1'), offlineOp('legacy-2')]);
  await page.route('**/api/v1/sync**', route => route.abort());
  await page.goto('/#/sync');
  await expect(page.getByText('(2)').first()).toBeVisible();
  await expect.poll(() => idbOutbox(page)).toEqual(['legacy-1','legacy-2']);
  expect(await page.evaluate(() => localStorage.getItem('ofc:sync:outbox'))).toBeNull();
});

test('a sale queued while a sync is in flight is not dropped when the sync settles', async ({page}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  let release: () => void = () => {};
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/api/v1/sync', async route => {
    await gate;
    await route.fulfill({json:{serverVersion:5,currentCatalogVersion:1,pendingConflicts:0,results:[{idempotencyKey:'first',operationType:'order.create',status:'applied',flags:[],conflictReason:null,error:null,serverVersion:5}]}});
  });
  await page.goto('/#/users');
  const remaining = page.evaluate(async (op) => {
    const outbox = await import('/src/lib/sync-outbox.ts');
    await outbox.enqueue(op);
    const inFlight = outbox.flush('t');
    await new Promise(r => setTimeout(r, 200));
    await outbox.enqueue({...op, idempotencyKey:'second'});
    (window as unknown as {releaseSync?: boolean}).releaseSync = true;
    await inFlight;
    return (await outbox.pending()).map(x => x.idempotencyKey);
  }, offlineOp('first'));
  await page.waitForFunction(() => (window as unknown as {releaseSync?: boolean}).releaseSync === true);
  release();
  expect(await remaining).toEqual(['second']);
});

test('an offline hold is stored in IndexedDB before the cart is cleared', async ({page, context}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  await page.goto('/#/pos');
  await page.getByRole('button',{name:/Crispy chicken family meal 1 OMR/}).first().click();
  await context.setOffline(true);
  await page.evaluate(() => window.dispatchEvent(new Event('offline')));
  await page.locator('.pos-ticket-desktop').getByRole('button',{name:'Hold',exact:true}).click();
  await expect(page.locator('.pos-ticket-desktop').getByRole('listitem')).toHaveCount(0);
  await expect.poll(async () => (await idbOutbox(page)).length).toBe(1);
  await context.setOffline(false);
});

test('register messages dismiss themselves and can be closed', async ({page}) => {
  await page.clock.install();
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  await page.goto('/#/pos');
  await page.getByRole('radio',{name:'Electronic',exact:true}).click();
  await page.getByRole('radio',{name:'Talabat',exact:true}).click();
  await page.getByRole('button',{name:/Crispy chicken family meal 1 OMR/}).first().click();
  await page.getByRole('button',{name:'Confirm & send to kitchen',exact:true}).click();
  const toast = page.locator('.pos-message');
  await expect(toast).toContainText('Order sent to kitchen.');
  await expect(toast).toHaveClass(/is-success/);
  await page.clock.fastForward(4500);
  await expect(toast).toHaveCount(0);

  await page.getByRole('button',{name:/Crispy chicken family meal 1 OMR/}).first().click();
  await page.getByRole('button',{name:'Confirm & send to kitchen',exact:true}).click();
  await expect(toast).toBeVisible();
  await page.getByRole('button',{name:'Dismiss message'}).click();
  await expect(toast).toHaveCount(0);
});


test('adding the same item again raises its quantity instead of opening a new line', async ({page}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  await page.goto('/#/pos');
  const item = page.getByRole('button',{name:/Crispy chicken family meal 1 OMR/}).first();
  const ticket = page.locator('.pos-ticket-desktop');
  await item.click();
  await item.click();
  await item.click();
  await expect(ticket.getByRole('listitem')).toHaveCount(1);
  await expect(ticket.locator('.pos-quantity > span')).toHaveText('3');
  await expect(ticket.getByTestId('ticket-total')).toContainText('7.500');
  // A line with its own instruction stays separate from new plain units.
  await ticket.getByRole('textbox',{name:/Crispy chicken family meal 1/}).fill('No onion');
  await item.click();
  await expect(ticket.getByRole('listitem')).toHaveCount(2);
  await expect(ticket.getByTestId('ticket-total')).toContainText('10.000');
});

test('several unacknowledged orders collapse into one alert that prints them in one job', async ({page}) => {
  await page.clock.install();
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  let next = 0;
  await page.route('**/api/v1/orders', async route => {
    if (route.request().method() !== 'POST') return route.fallback();
    next += 1;
    await route.fulfill({json:{id:`k${next}`,grossAmount:2.5}});
  });
  await page.route('**/api/v1/orders?**', async route => {
    if (route.request().method() !== 'GET' || new URL(route.request().url()).pathname !== '/api/v1/orders') return route.fallback();
    await route.fulfill({json:[1,2].map(n=>({id:`k${n}`,number:500+n,status:'SentToKitchen',grossAmount:2.5,note:'',createdAt:new Date().toISOString(),table:null}))});
  });
  await page.route(/\/api\/v1\/orders\/k\d$/, route => {
    const id = new URL(route.request().url()).pathname.split('/').pop()!;
    return route.fulfill({json:{id,number:500+Number(id.slice(1)),status:'SentToKitchen',note:null,netAmount:2.5,taxAmount:0,grossAmount:2.5,createdAt:new Date().toISOString(),lines:[{id:'l',productId:'p0',productNameAr:'وجبة',productNameEn:`Meal for ${id}`,quantity:1,note:null,unitGrossAmount:2.5,selectionsSnapshot:'[]'}]}});
  });
  await page.goto('/#/pos');
  await page.getByRole('radio',{name:'Electronic',exact:true}).click();
  await page.getByRole('radio',{name:'Talabat',exact:true}).click();
  for (let i = 0; i < 2; i++) {
    await page.getByRole('button',{name:/Crispy chicken family meal 1 OMR/}).first().click();
    await page.getByRole('button',{name:'Confirm & send to kitchen',exact:true}).click();
    await expect(page.locator('.pos-ticket-desktop').getByRole('listitem')).toHaveCount(0);
  }
  await page.clock.fastForward(26_000);
  const alerts = page.locator('.pos-kitchen-alert');
  await expect(alerts).toHaveCount(1);
  await expect(alerts).toContainText('2 orders not received in the kitchen');
  await expect(alerts).toContainText('#501 · #502');
  const frames: string[] = [];
  page.on('frameattached', frame => frames.push(frame.url()));
  const printed = page.waitForEvent('frameattached');
  await alerts.getByRole('button',{name:'Print all for kitchen'}).click();
  const frame = await printed;
  await expect.poll(async () => { const html = await frame.content(); return html.includes('Meal for k1') && html.includes('Meal for k2'); }).toBe(true);
  expect(frames).toHaveLength(1);
  await expect(alerts).toHaveCount(0);
});

test('the register warns once when the kitchen screen drops, and only if one was connected', async ({page}) => {
  await page.setViewportSize({width:1366,height:900}); await setup(page,'en','light');
  let presence: {screens:number; lastSeenAt:string|null} = {screens:0,lastSeenAt:null};
  await page.route('**/api/v1/kitchen/presence?**', route => route.fulfill({json:presence}));
  await page.goto('/#/pos');
  await expect(page.locator('.pos-ticket-desktop')).toBeVisible();
  await expect(page.locator('.pos-kitchen-alert.is-offline')).toHaveCount(0);

  presence = {screens:0,lastSeenAt:new Date(Date.now() - 120_000).toISOString()};
  await page.reload();
  const offline = page.locator('.pos-kitchen-alert.is-offline');
  await expect(offline).toContainText('Kitchen screen disconnected since');
  await offline.getByRole('button',{name:'Hide',exact:true}).click();
  await expect(offline).toHaveCount(0);

  presence = {screens:1,lastSeenAt:new Date().toISOString()};
  await page.reload();
  await expect(page.locator('.pos-ticket-desktop')).toBeVisible();
  await expect(page.locator('.pos-kitchen-alert.is-offline')).toHaveCount(0);
});
