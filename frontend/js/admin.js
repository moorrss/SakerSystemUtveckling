import { api } from './api.js';
import { state } from './state.js';
import { el, clear, flash, formatDate, formatPrice, handleError } from './ui.js';

// Adminsidan syns bara för admins i menyn, men det är bara bekvämlighet.
// VARJE anrop härifrån kontrolleras av [Authorize(Roles = "Admin")] i backend (T9).

let activeTab = 'admin-products';

export function initAdmin() {
  document.querySelectorAll('.tab').forEach(tab => {
    tab.addEventListener('click', () => showTab(tab.dataset.tab));
  });
  document.getElementById('product-form').addEventListener('submit', saveProduct);
  document.getElementById('product-form-cancel').addEventListener('click', resetProductForm);
}

export function loadAdmin() {
  showTab(activeTab);
}

function showTab(tabId) {
  activeTab = tabId;
  document.querySelectorAll('.tab').forEach(t => t.classList.toggle('active', t.dataset.tab === tabId));
  document.querySelectorAll('.tab-panel').forEach(p => { p.hidden = p.id !== tabId; });

  if (tabId === 'admin-products') loadAdminProducts();
  if (tabId === 'admin-orders') loadAdminOrders();
  if (tabId === 'admin-users') loadAdminUsers();
}

// ---------- Produkter ----------

async function loadAdminProducts() {
  const rows = document.getElementById('admin-product-rows');
  try {
    // API:t returnerar max 50 per sida (paginering), så sidorna hämtas en i taget
    const products = [];
    for (let page = 1; page <= 20; page++) {
      const result = await api(`/api/products?page=${page}&pageSize=50`);
      products.push(...result.items);
      if (products.length >= result.totalCount || result.items.length === 0) break;
    }

    clear(rows);
    for (const p of products) {
      rows.append(el('tr', {},
        el('td', {}, p.id),
        el('td', {}, p.name),
        el('td', {}, formatPrice(p.price)),
        el('td', {}, p.stock),
        el('td', { class: 'actions' },
          el('button', { type: 'button', class: 'btn btn-secondary btn-small', onclick: () => editProduct(p) }, 'Redigera'),
          el('button', { type: 'button', class: 'btn btn-danger btn-small', onclick: () => deleteProduct(p) }, 'Ta bort')
        )
      ));
    }
  } catch (error) {
    handleError(error);
  }
}

function editProduct(product) {
  document.getElementById('product-form-title').textContent = `Redigera produkt ${product.id}`;
  document.getElementById('product-id').value = String(product.id);
  document.getElementById('product-name').value = product.name;
  document.getElementById('product-description').value = product.description;
  document.getElementById('product-price').value = String(product.price);
  document.getElementById('product-stock').value = String(product.stock);
  document.getElementById('product-form-cancel').hidden = false;
}

function resetProductForm() {
  document.getElementById('product-form').reset();
  document.getElementById('product-id').value = '';
  document.getElementById('product-form-title').textContent = 'Ny produkt';
  document.getElementById('product-form-cancel').hidden = true;
}

async function saveProduct(event) {
  event.preventDefault();
  const id = document.getElementById('product-id').value;
  const body = {
    name: document.getElementById('product-name').value.trim(),
    description: document.getElementById('product-description').value.trim(),
    price: Number(document.getElementById('product-price').value),
    stock: parseInt(document.getElementById('product-stock').value, 10)
  };

  try {
    if (id) {
      await api(`/api/products/${encodeURIComponent(id)}`, { method: 'PUT', body });
      flash('Produkten har uppdaterats.', 'success');
    } else {
      await api('/api/products', { method: 'POST', body });
      flash('Produkten har skapats.', 'success');
    }
    resetProductForm();
    loadAdminProducts();
  } catch (error) {
    handleError(error);
  }
}

async function deleteProduct(product) {
  if (!confirm(`Ta bort "${product.name}"?`)) return;
  try {
    await api(`/api/products/${encodeURIComponent(product.id)}`, { method: 'DELETE' });
    flash('Produkten har tagits bort.', 'success');
    loadAdminProducts();
  } catch (error) {
    handleError(error);
  }
}

// ---------- Ordrar ----------

async function loadAdminOrders() {
  const rows = document.getElementById('admin-order-rows');
  try {
    const orders = await api('/api/admin/orders');
    clear(rows);
    if (orders.length === 0) {
      rows.append(el('tr', {}, el('td', { colspan: 5, class: 'muted' }, 'Inga ordrar än.')));
    }
    for (const o of orders) {
      rows.append(el('tr', {},
        el('td', {}, o.id),
        el('td', {}, o.customerEmail),
        el('td', {}, formatDate(o.createdAt)),
        el('td', {}, o.itemCount),
        el('td', {}, formatPrice(o.totalAmount))
      ));
    }
  } catch (error) {
    handleError(error);
  }
}

// ---------- Användare ----------

async function loadAdminUsers() {
  const rows = document.getElementById('admin-user-rows');
  try {
    const users = await api('/api/admin/users');
    clear(rows);
    for (const u of users) {
      const isSelf = state.user && state.user.id === u.id;
      rows.append(el('tr', {},
        el('td', {}, u.email),
        el('td', {}, u.roles.join(', ')),
        el('td', { class: u.isLockedOut ? 'status-fail' : 'status-ok' }, u.isLockedOut ? 'Låst' : 'Aktiv'),
        el('td', { class: 'actions' },
          isSelf ? el('span', { class: 'muted' }, 'Du') :
            el('button', {
              type: 'button',
              class: u.isLockedOut ? 'btn btn-secondary btn-small' : 'btn btn-danger btn-small',
              onclick: () => setLock(u, !u.isLockedOut)
            }, u.isLockedOut ? 'Lås upp' : 'Lås konto')
        )
      ));
    }
  } catch (error) {
    handleError(error);
  }
}

async function setLock(user, locked) {
  try {
    await api(`/api/admin/users/${encodeURIComponent(user.id)}/lock`, { method: 'PUT', body: { locked } });
    flash(locked ? `${user.email} har låsts.` : `${user.email} har låsts upp.`, 'success');
    loadAdminUsers();
  } catch (error) {
    handleError(error);
  }
}