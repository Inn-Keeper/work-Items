const baseUrl = '/workitems/';

export async function getItems() {
  const response = await fetch(baseUrl);
  if (!response.ok) throw new Error('Could not load work items.');
  return response.json();
}

export async function saveItem(id, item) {
  const response = await fetch(id === null ? baseUrl : `${baseUrl}${id}`, {
    method: id === null ? 'POST' : 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(item)
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(Object.values(error.errors ?? {})[0]?.[0] ?? 'Could not save this item.');
  }
  return response.json();
}

export async function removeItem(id) {
  const response = await fetch(`${baseUrl}${id}`, { method: 'DELETE' });
  if (!response.ok) throw new Error('Could not delete this item.');
}
