import { api } from './api.js';
import { addToCart } from './cart.js';
import { el, clear, formatPrice, handleError } from './ui.js';

const PAGE_SIZE = 12;
let currentPage = 1;
let currentSearch = '';

export async function loadProducts() {
  const list = document.getElementById('product-list');
  const params = new URLSearchParams({ page: String(currentPage), pageSize: String(PAGE_SIZE) });
  if (currentSearch) params.set('search', currentSearch);

  try {
    const result = await api(`/api/products?${params}`);
    renderProducts(list, result.items);

    const totalPages = Math.max(1, Math.ceil(result.totalCount / result.pageSize));
    document.getElementById('page-info').textContent = `Sida ${result.page} av ${totalPages}`;
    document.getElementById('prev-page').disabled = result.page <= 1;
    document.getElementById('next-page').disabled = result.page >= totalPages;
  } catch (error) {
    clear(list);
    handleError(error);
  }
}

function renderProducts(list, products) {
  clear(list);

  if (products.length === 0) {
    list.append(el('p', { class: 'muted' }, 'Inga produkter hittades.'));
    return;
  }

  for (const product of products) {
    const qty = el('input', { type: 'number', min: 1, max: 100, value: 1, class: 'qty' });
    const soldOut = product.stock <= 0;

    list.append(el('article', { class: 'card product' },
      el('h3', {}, product.name),
      el('p', {}, product.description),
      el('div', { class: 'price' }, formatPrice(product.price)),
      el('div', { class: 'stock' }, soldOut ? 'Slut i lager' : `${product.stock} i lager`),
      el('div', { class: 'row' },
        qty,
        el('button', {
          type: 'button', class: 'btn', disabled: soldOut,
          onclick: () => addToCart(product, Math.max(1, Math.min(100, parseInt(qty.value, 10) || 1)))
        }, 'Lägg i kundvagn')
      )
    ));
  }
}

export function initProducts() {
  document.getElementById('search-form').addEventListener('submit', (event) => {
    event.preventDefault();
    currentSearch = document.getElementById('search-input').value.trim().slice(0, 100);
    currentPage = 1;
    loadProducts();
  });
  document.getElementById('prev-page').addEventListener('click', () => {
    if (currentPage > 1) { currentPage--; loadProducts(); }
  });
  document.getElementById('next-page').addEventListener('click', () => {
    currentPage++;
    loadProducts();
  });
}