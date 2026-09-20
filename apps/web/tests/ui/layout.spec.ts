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
  await page.getByRole('button',{name:'Pay',exact:true}).click();
  await expect(page.getByRole('dialog',{name:'Payment'})).toBeVisible();
  expect(kitchen).toHaveLength(0);
  await page.getByRole('button',{name:'Cash',exact:true}).click();
  await page.getByRole('button',{name:'Complete payment',exact:true}).click();
  await expect.poll(()=>kitchen.length).toBe(1);
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
