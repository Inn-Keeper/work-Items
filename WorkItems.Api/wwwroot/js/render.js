import { formatDate, isOverdue } from './date.js';

export const statuses = ['Todo', 'In progress', 'Done'];

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

export function renderItems(list, items, { selectedId, filtered, onSelect, onNew }) {
  list.replaceChildren();

  if (!items.length) {
    const empty = el('li', 'empty');
    empty.append(el('p', '', filtered ? 'No items match your filters.' : 'No work items yet.'));
    if (!filtered) {
      const create = el('button', 'secondary small', 'Create your first item');
      create.type = 'button';
      create.addEventListener('click', onNew);
      empty.append(create);
    }
    list.append(empty);
    return;
  }

  for (const item of items) {
    const button = el('button', 'item');
    button.type = 'button';
    button.dataset.id = item.id;
    if (item.id === selectedId) button.setAttribute('aria-current', 'true');
    button.addEventListener('click', () => onSelect(item));

    button.append(el('span', item.status === 2 ? 'item-title done' : 'item-title', item.title));
    if (item.description) button.append(el('span', 'item-desc', item.description));

    const meta = el('span', 'item-meta');
    meta.append(el('span', `badge badge-${item.status}`, statuses[item.status] ?? 'Unknown'));
    if (item.dueDate) {
      const overdue = isOverdue(item);
      meta.append(el('span', overdue ? 'overdue' : '', `${overdue ? 'Overdue' : 'Due'} ${formatDate(item.dueDate)}`));
    }
    button.append(meta);

    const row = el('li');
    row.append(button);
    list.append(row);
  }
}
