

const listeners = [];

export const state = {
  user: null // { id, email, roles }
};

export function setUser(user) {
  state.user = user;
  listeners.forEach(fn => fn(user));
}

export function onUserChange(fn) {
  listeners.push(fn);
}

export function isLoggedIn() {
  return state.user !== null;
}

export function isAdmin() {
  return state.user !== null && state.user.roles.includes('Admin');
}