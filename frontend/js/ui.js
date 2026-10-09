import { state, setUser } from './state.js';

export function el(tag, props = {}, ...children) {
  const node = document.createElement(tag);

  for (const [key, value] of Object.entries(props)) {
    if (value === undefined || value === null || value === false) continue;

    if (key.startsWith('on') && typeof value === 'function') {
      node.addEventListener(key.slice(2).toLowerCase(), value);
    } else if (key === 'class') {
      node.className = value;
    } else {
      node.setAttribute(key, value === true ? '' : String(value));
    }
  }

  for (const child of children.flat()) {
    if (child === null || child === undefined || child === false) continue;
    // Text blir en textnod: "<script>" visas som tecknen < s c r i p t >, körs aldrig
    node.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return node;
}

export function clear(node) {
  node.replaceChildren();
}

const priceFormat = new Intl.NumberFormat('sv-SE', { style: 'currency', currency: 'SEK' });
export function formatPrice(value) {
  return priceFormat.format(Number(value));
}

export function formatDate(value) {
  const text = String(value);
  const date = new Date(/[zZ]|[+-]\d\d:\d\d$/.test(text) ? text : text + 'Z');
  return date.toLocaleString('sv-SE', { dateStyle: 'short', timeStyle: 'short' });
}

// ---------- Meddelanden ----------

const flashBox = () => document.getElementById('flash');

export function flash(message, type = 'info', details = [], { keep = false } = {}) {
  const box = flashBox();
  clear(box);
  box.className = `flash flash-${type}`;
  box.append(el('div', {}, message));
  if (details.length > 0) {
    box.append(el('ul', {}, details.map(d => el('li', {}, d))));
  }
  box.dataset.keep = keep ? '1' : '';
  box.hidden = false;
}

export function clearFlash() {
  const box = flashBox();
  if (box.dataset.keep === '1') {
    box.dataset.keep = '';
    return;
  }
  box.hidden = true;
  clear(box);
}

export function navigate(hash) {
  if (location.hash === hash) {
    window.dispatchEvent(new HashChangeEvent('hashchange'));
  } else {
    location.hash = hash;
  }
}

export function handleError(error) {
  if (error.status === 401 && state.user !== null) {
    setUser(null);
    flash('Din inloggning har gått ut. Logga in igen.', 'error', [], { keep: true });
    navigate('#login');
    return;
  }
  flash(error.message || 'Något gick fel.', 'error', error.details || []);
}