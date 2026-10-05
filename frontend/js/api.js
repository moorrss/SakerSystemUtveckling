// Gemensam funktion för alla anrop till API:t.
// Token hanteras ALDRIG här: den ligger i en HttpOnly-cookie som JavaScript inte kan läsa,
// och webbläsaren skickar den automatiskt till den egna sajten.

export class ApiError extends Error {
  constructor(status, message, details = []) {
    super(message);
    this.status = status;
    this.details = details;
  }
}

// Standardtitlar från ASP.NET Core ersätts med svenska meddelanden
const GENERIC_TITLES = new Set([
  'Unauthorized', 'Forbidden', 'Not Found', 'Bad Request', 'Conflict',
  'Unsupported Media Type', 'Too Many Requests',
  'One or more validation errors occurred.',
  'An error occurred while processing your request.'
]);

function defaultMessage(status) {
  switch (status) {
    case 400: return 'Kontrollera uppgifterna och försök igen.';
    case 401: return 'Du måste logga in.';
    case 403: return 'Du har inte behörighet till detta.';
    case 404: return 'Det du letade efter hittades inte.';
    case 409: return 'Åtgärden kunde inte genomföras.';
    case 429: return 'För många anrop. Vänta en stund och försök igen.';
    default: return 'Något gick fel. Försök igen senare.';
  }
}

export async function api(path, { method = 'GET', body } = {}) {
  const options = {
    method,
    credentials: 'same-origin',   // cookien skickas bara till den egna sajten
    headers: { 'Accept': 'application/json' }
  };

  if (body !== undefined) {
    options.headers['Content-Type'] = 'application/json';
    options.body = JSON.stringify(body);
  }

  let response;
  try {
    response = await fetch(path, options);
  } catch {
    throw new ApiError(0, 'Kunde inte nå servern.');
  }

  if (response.status === 204) return null;

  const contentType = response.headers.get('Content-Type') || '';
  let data = null;
  if (contentType.includes('json')) {
    try { data = await response.json(); } catch { data = null; }
  }

  if (!response.ok) {
    const title = data && typeof data.title === 'string' ? data.title : '';
    const message = title && !GENERIC_TITLES.has(title) ? title : defaultMessage(response.status);
    const details = data && data.errors ? Object.values(data.errors).flat().map(String) : [];
    if (data && data.errorId) details.push(`Felkod: ${data.errorId}`);
    throw new ApiError(response.status, message, details);
  }

  return data;
}