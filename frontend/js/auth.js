import { api } from './api.js';
import { setUser } from './state.js';
import { flash, handleError, navigate } from './ui.js';

// Hämtar vem som är inloggad. Frontend kan inte läsa token (HttpOnly-cookie),
// så servern får svara på frågan.
export async function loadCurrentUser() {
  try {
    const user = await api('/api/auth/me');
    setUser(user);
  } catch {
    setUser(null);
  }
}

export function initAuth() {
  const loginForm = document.getElementById('login-form');
  const registerForm = document.getElementById('register-form');

  loginForm.addEventListener('submit', async (event) => {
    event.preventDefault();
    const button = loginForm.querySelector('button');
    button.disabled = true;
    try {
      const user = await api('/api/auth/login', {
        method: 'POST',
        body: { email: loginForm.email.value.trim(), password: loginForm.password.value }
      });
      loginForm.reset();
      setUser(user);
      flash(`Välkommen, ${user.email}!`, 'success', [], { keep: true });
      navigate('#products');
    } catch (error) {
      loginForm.password.value = '';
      handleError(error);
    } finally {
      button.disabled = false;
    }
  });

  registerForm.addEventListener('submit', async (event) => {
    event.preventDefault();
    const button = registerForm.querySelector('button');
    button.disabled = true;
    try {
      // Bara e-post och lösenord skickas. Rollen bestäms alltid av servern (T9).
      await api('/api/auth/register', {
        method: 'POST',
        body: { email: registerForm.email.value.trim(), password: registerForm.password.value }
      });
      registerForm.reset();
      flash('Kontot har skapats. Du kan nu logga in.', 'success');
    } catch (error) {
      registerForm.password.value = '';
      handleError(error);
    } finally {
      button.disabled = false;
    }
  });

  document.getElementById('logout-button').addEventListener('click', async () => {
    try {
      // Servern tar bort cookien, eftersom JavaScript inte kan radera en HttpOnly-cookie
      await api('/api/auth/logout', { method: 'POST' });
    } catch {
      // Användaren loggas ut i gränssnittet även om anropet misslyckas
    }
    setUser(null);
    flash('Du är utloggad.', 'info', [], { keep: true });
    navigate('#products');
  });
}