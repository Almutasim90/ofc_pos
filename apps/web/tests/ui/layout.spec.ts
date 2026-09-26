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
