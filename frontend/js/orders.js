import { api } from './api.js';
import { el, clear, formatDate, formatPrice, handleError } from './ui.js';

// Hämtar bara den inloggade användarens egna ordrar. VILKA ordrar avgörs
// av servern utifrån token, inte av något ID som frontend skickar (T6).
export async function loadOrders() {
  const list = document.getElementById('order-list');
  try {
    const orders = await api('/api/orders/mine');
    clear(list);

    if (orders.length === 0) {
      list.append(el('p', { class: 'muted' }, 'Du har inga ordrar än.'));
      return;
    }

    for (const order of orders) {
      list.append(el('article', { class: 'card' },
        el('div', { class: 'order-head' },
          el('strong', {}, `Order ${order.id}`),
          el('span', { class: 'muted' }, formatDate(order.createdAt))
        ),
        el('div', { class: 'muted' }, `Leveransadress: ${order.shippingAddress}`),
        el('ul', { class: 'order-items' },
          order.items.map(item => el('li', {}, `${item.quantity} st ${item.productName} à ${formatPrice(item.unitPrice)}`))
        ),
        el('p', { class: 'price' }, `Totalt: ${formatPrice(order.totalAmount)}`)
      ));
    }
  } catch (error) {
    clear(list);
    handleError(error);
  }
}