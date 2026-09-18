import { getItems, removeItem, saveItem } from './api.js';
import { fromInputDate, toInputDate } from './date.js';
import { renderItems } from './render.js';

const $ = (selector) => document.querySelector(selector);
const form = $('#item-form');
const list = $('#item-list');
const search = $('#search');
const message = $('#message');
const heading = $('#form-heading');
const saveButton = $('#save-button');
const cancelButton = $('#cancel-button');
const deleteButton = $('#delete-button');
const titleField = $('#title');
const titleError = $('#title-error');
const descriptionField = $('#description');
const statusField = $('#status');
const dueDateField = $('#due-date');
const banner = $('#banner');
const toast = $('#toast');
const confirmDialog = $('#confirm-dialog');

let items = [];
let selected = null;
let filter = 'all';
let toastTimer;

function showToast(text) {
  toast.textContent = text;
  toast.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { toast.hidden = true; }, 2200);
}

function setTitleError(show) {
  titleError.hidden = !show;
  titleField.setAttribute('aria-invalid', String(show));
}

function render() {
  const query = search.value.trim().toLowerCase();
  const visible = items.filter((item) =>
    (filter === 'all' || item.status === Number(filter)) &&
    (!query || `${item.title} ${item.description ?? ''}`.toLowerCase().includes(query)));

  $('#count').textContent = `${items.length} item${items.length === 1 ? '' : 's'}`;
  renderItems(list, visible, { selectedId: selected?.id, hasAny: items.length > 0, onSelect: select, onNew: newItem });
}

function fillForm(item) {
  selected = item;
  titleField.value = item?.title ?? '';
  descriptionField.value = item?.description ?? '';
  statusField.value = String(item?.status ?? 0);
  dueDateField.value = toInputDate(item?.dueDate);
  heading.textContent = item ? 'Edit item' : 'New item';
  saveButton.textContent = item ? 'Save changes' : 'Add item';
  cancelButton.hidden = !item;
  deleteButton.hidden = !item;
  message.textContent = '';
  setTitleError(false);
  render();
}

function select(item) {
  fillForm(item);
  titleField.focus();
}

function newItem() {
  fillForm(null);
  titleField.focus();
}

async function loadItems() {
  try {
    items = await getItems();
    banner.hidden = true;
    // Keep the editor on the same item if it still exists.
    if (selected) selected = items.find((item) => item.id === selected.id) ?? null;
    render();
  } catch {
    $('#banner-text').textContent = 'Cannot reach the Work Items API.';
    banner.hidden = false;
  }
}

async function save() {
  const title = titleField.value.trim();
  if (!title) {
    setTitleError(true);
    titleField.focus();
    return;
  }
  setTitleError(false);

  saveButton.disabled = true;
  message.textContent = '';
  try {
    const saved = await saveItem(selected?.id ?? null, {
      title,
      description: descriptionField.value.trim() || null,
      status: Number(statusField.value),
      dueDate: fromInputDate(dueDateField.value)
    });
    showToast(selected ? 'Changes saved' : 'Item added');
    await loadItems();
    fillForm(items.find((item) => item.id === saved.id) ?? null);
  } catch (error) { message.textContent = error.message; }
  finally { saveButton.disabled = false; }
}

async function deleteSelected() {
  if (!selected) return;
  $('#confirm-text').textContent = `“${selected.title}” will be permanently removed.`;
  confirmDialog.showModal();
  const choice = await new Promise((resolve) =>
    confirmDialog.addEventListener('close', () => resolve(confirmDialog.returnValue), { once: true }));
  if (choice !== 'delete') return;

  try {
    await removeItem(selected.id);
    fillForm(null);
    showToast('Item deleted');
    await loadItems();
  } catch (error) { message.textContent = error.message; }
}

form.addEventListener('submit', (event) => { event.preventDefault(); save(); });
titleField.addEventListener('input', () => { if (titleField.value.trim()) setTitleError(false); });
cancelButton.addEventListener('click', () => fillForm(null));
deleteButton.addEventListener('click', deleteSelected);
$('#new-button').addEventListener('click', newItem);
$('#retry-button').addEventListener('click', loadItems);
search.addEventListener('input', render);

for (const chip of document.querySelectorAll('.chip')) {
  chip.addEventListener('click', () => {
    filter = chip.dataset.filter;
    for (const other of document.querySelectorAll('.chip'))
      other.setAttribute('aria-pressed', String(other === chip));
    render();
  });
}

document.addEventListener('keydown', (event) => {
  if (confirmDialog.open) return;
  const mod = event.metaKey || event.ctrlKey;
  const typing = event.target.matches('input, textarea, select');

  if (mod && (event.key === 's' || event.key === 'Enter')) {
    event.preventDefault();
    save();
  } else if (event.key === 'Escape') {
    // Cancels an edit; never wipes an unsaved new item.
    if (selected) fillForm(null);
    document.activeElement.blur();
  } else if (!mod && !typing && event.key === 'n') {
    event.preventDefault();
    newItem();
  } else if (!mod && !typing && event.key === '/') {
    event.preventDefault();
    search.focus();
  }
});

loadItems();
