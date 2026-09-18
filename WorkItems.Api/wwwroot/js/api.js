const baseUrl = '/workitems/';

// Empty values are left out so the API applies its defaults.
export async function getItems(query = {}) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query))
    if (value !== null && value !== undefined && value !== '') params.set(key, value);
  const response = await fetch(`${baseUrl}?${params}`);
  await check(response, 'Could not load work items.');
  return response.json();
}

// Thrown on HTTP 412: the item changed on the server since this client loaded it.
export class VersionConflictError extends Error {
  constructor() { super('This item was changed elsewhere.'); }
}

async function check(response, fallback) {
  if (response.ok) return;
  if (response.status === 412) throw new VersionConflictError();
  if (response.status === 404) throw new Error('This item no longer exists. Refresh the list.');
  // ProblemDetails: validation errors carry per-field messages; other errors only title/detail.
  const problem = await response.json().catch(() => ({}));
  throw new Error(Object.values(problem.errors ?? {})[0]?.[0] ?? problem.detail ?? problem.title ?? fallback);
}

const ifMatch = (item) => ({ 'If-Match': `"${item.version}"` });

export async function getItem(id) {
  const response = await fetch(`${baseUrl}${id}`);
  await check(response, 'Could not load this item.');
  return response.json();
}

// Creates when existing is null, otherwise updates it if its version is still current.
export async function saveItem(existing, body) {
  const response = await fetch(existing ? `${baseUrl}${existing.id}` : baseUrl, {
    method: existing ? 'PUT' : 'POST',
    headers: { 'Content-Type': 'application/json', ...(existing ? ifMatch(existing) : {}) },
    body: JSON.stringify(body)
  });
  await check(response, 'Could not save this item.');
  return response.json();
}

export async function removeItem(item) {
  const response = await fetch(`${baseUrl}${item.id}`, { method: 'DELETE', headers: ifMatch(item) });
  await check(response, 'Could not delete this item.');
}
