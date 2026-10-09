import { api } from './api.js';
import { isLoggedIn } from './state.js';
import { el, clear, flash, formatPrice, handleError, navigate } from './ui.js';

const STORAGE_KEY = 'jensenonline-cart';

function readCart() {
  try {
    const data = JSON.parse(sessionStorage.getItem(STORAGE_KEY) || '[]');
    return Array.isArray(data) ? data : [];
  } catch {
    return [];
  }
}

function writeCart(items) {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(items));
  } catch {
    
  }
  updateCartCount();
}

export function updateCartCount() {
  const count = readCart().reduce((sum, item) => sum + item.quantity, 0);
  document.getElementById('cart-count').textContent = String(count);
}

export function addToCart(product, quantity) {
  const items = readCart();
  const existing = items.find(i => i.productId === product.id);
  if (existing) {
    existing.quantity = Math.min(existing.quantity + quantity, 100);
  } else {
    items.push({ productId: product.id, name: product.name, price: product.price, quantity });
  }
  writeCart(items);
  flash(`${product.name} har lagts i kundvagnen.`, 'success');
}

function setQuantity(productId, quantity) {
  let items = readCart();
  if (quantity <= 0) {
    items = items.filter(i => i.productId !== productId);
  } else {
    const item = items.find(i => i.productId === productId);
    if (item) item.quantity = Math.min(quantity, 100);
  }
  writeCart(items);
  renderCart();
}

export function renderCart() {
  const container = document.getElementById('cart-items');
  const checkoutForm = document.getElementById('checkout-form');
  const items = readCart();
  clear(container);

  if (items.length === 0) {
    container.append(el('p', { class: 'muted' }, 'Kundvagnen är tom.'));
    checkoutForm.hidden = true;
    return;
  }

  for (const item of items) {
    const qtyInput = el('input', { type: 'number', min: 1, max: 100, value: item.quantity, class: 'qty' });
    qtyInput.addEventListener('change', () => setQuantity(item.productId, parseInt(qtyInput.value, 10) || 0));

    container.append(el('div', { class: 'card cart-row' },
      el('div', {},
        el('strong', {}, item.name),
        el('div', { class: 'muted' }, `${formatPrice(item.price)} / st`)
      ),
      el('div', { class: 'row' },
        qtyInput,
        el('button', { type: 'button', class: 'btn btn-danger btn-small', onclick: () => setQuantity(item.productId, 0) }, 'Ta bort')
      )
    ));
  }

  const total = items.reduce((sum, i) => sum + i.price * i.quantity, 0);
  container.append(el('p', { class: 'cart-total' }, `Preliminär summa: ${formatPrice(total)}`));
  checkoutForm.hidden = false;
}

export function initCart() {
  updateCartCount();
  const form = document.getElementById('checkout-form');

  form.addEventListener('submit', async (event) => {
    event.preventDefault();

    if (!isLoggedIn()) {
      flash('Logga in för att lägga en order.', 'info', [], { keep: true });
      navigate('#login');
      return;
    }

    const button = form.querySelector('button');
    button.disabled = true;
    try {
      const order = await api('/api/orders', {
        method: 'POST',
        body: {
        
          items: readCart().map(i => ({ productId: i.productId, quantity: i.quantity })),
          shippingAddress: form.shippingAddress.value.trim()
        }
      });
      writeCart([]);
      form.reset();
      flash(`Tack! Order ${order.id} är lagd. Totalt ${formatPrice(order.totalAmount)}.`, 'success', [], { keep: true });
      navigate('#orders');
    } catch (error) {
      handleError(error);
    } finally {
      button.disabled = false;
    }
  });
}