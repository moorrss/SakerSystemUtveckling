import { initAdmin, loadAdmin } from './admin.js';
import { initAuth, loadCurrentUser } from './auth.js';
import { initCart, renderCart } from './cart.js';
import { loadOrders } from './orders.js';
import { initProducts, loadProducts } from './products.js';
import { isAdmin, isLoggedIn, onUserChange } from './state.js';
import { clearFlash, flash, navigate } from './ui.js';

// Enkel router baserad på #-delen av URL:en
const routes = {
  products: { load: loadProducts },
  cart: { load: renderCart },
  orders: { load: loadOrders, requires: 'user' },
  login: {},
  admin: { load: loadAdmin, requires: 'admin' }
};

function route() {
  const name = location.hash.replace('#', '') || 'products';
  const current = Object.hasOwn(routes, name) ? name : 'products';
  const config = routes[current];

  // Dessa kontroller visar bara rätt sida. Om någon kringgår dem
  // i DevTools nekar backend ändå anropen med 401/403.
  if (config.requires === 'user' && !isLoggedIn()) {
    flash('Logga in för att se dina ordrar.', 'info', [], { keep: true });
    navigate('#login');
    return;
  }
  if (config.requires === 'admin' && !isAdmin()) {
    flash('Sidan kräver administratörsbehörighet.', 'error', [], { keep: true });
    navigate('#products');
    return;
  }

  clearFlash();
  document.querySelectorAll('.view').forEach(v => { v.hidden = v.id !== `view-${current}`; });
  document.querySelectorAll('.nav a').forEach(a => a.classList.toggle('active', a.getAttribute('href') === `#${current}`));

  if (config.load) config.load();
}

function updateNav(user) {
  document.querySelectorAll('[data-requires="user"]').forEach(e => { e.hidden = !user; });
  document.querySelectorAll('[data-requires="admin"]').forEach(e => { e.hidden = !isAdmin(); });
  document.getElementById('login-link').hidden = !!user;
  document.getElementById('logout-button').hidden = !user;
  document.getElementById('user-email').textContent = user ? user.email : '';
}

async function start() {
  initAuth();
  initProducts();
  initCart();
  initAdmin();

  onUserChange(updateNav);
  await loadCurrentUser();

  window.addEventListener('hashchange', route);
  route();
}

start();