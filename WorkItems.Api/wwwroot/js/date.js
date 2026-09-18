export function formatDate(isoDate) {
  if (!isoDate) return '';
  const [year, month, day] = isoDate.slice(0, 10).split('-');
  return `${day}-${month}-${year}`;
}

// <input type="date"> uses YYYY-MM-DD; the API stores UTC midnight.
export const toInputDate = (isoDate) => isoDate ? isoDate.slice(0, 10) : '';
export const fromInputDate = (value) => value ? `${value}T00:00:00Z` : null;

export function isOverdue(item) {
  return item.status !== 2 && !!item.dueDate &&
    item.dueDate.slice(0, 10) < new Date().toLocaleDateString('en-CA');
}
