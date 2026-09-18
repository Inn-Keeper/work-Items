import { VersionConflictError, getItem, getItems, removeItem, saveItem } from './api.js';
import { fromInputDate, toInputDate } from './date.js';
import { renderItems } from './render.js';

const $ = (selector) => document.querySelector(selector);
const form = $('#item-form');
const list = $('#item-list');
const search = $('#search');
const sortField = $('#sort');
const loadMoreButton = $('#load-more');
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
const tagsField = $('#tags');
const tagFilterChip = $('#tag-filter');
const banner = $('#banner');
const toast = $('#toast');
const confirmDialog = $('#confirm-dialog');
const conflict = $('#conflict');
const conflictOverwrite = $('#conflict-overwrite');

let items = [];      // pages loaded so far
let total = 0;
let page = 1;
let selected = null; // item in the editor; may be outside the loaded pages
let filter = 'all';
let tagFilter = null;
let requestId = 0;   // ignore responses that arrive after a newer query was sent
let toastTimer;
let searchTimer;
let conflictOnDelete = false;

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
  $('#count').textContent = `${total} item${total === 1 ? '' : 's'}`;
  loadMoreButton.hidden = items.length >= total;
  const filtered = filter !== 'all' || tagFilter !== null || search.value.trim() !== '';
  renderItems(list, items, { selectedId: selected?.id, filtered, onSelect: select, onNew: newItem, onTag: filterByTag });
}

function filterByTag(tag) {
  tagFilter = tag;
  tagFilterChip.textContent = `#${tag} ✕`;
  tagFilterChip.hidden = tag === null;
  loadItems();
}

function fillForm(item) {
  selected = item;
  titleField.value = item?.title ?? '';
  descriptionField.value = item?.description ?? '';
  statusField.value = String(item?.status ?? 0);
  dueDateField.value = toInputDate(item?.dueDate);
  tagsField.value = (item?.tags ?? []).join(', ');
  heading.textContent = item ? 'Edit item' : 'New item';
  saveButton.textContent = item ? 'Save changes' : 'Add item';
  cancelButton.hidden = !item;
  deleteButton.hidden = !item;
  message.textContent = '';
  conflict.hidden = true;
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

async function loadItems(pageToLoad = 1) {
  const id = ++requestId;
  const option = sortField.selectedOptions[0];
  try {
    const result = await getItems({
      status: filter === 'all' ? null : filter,
      search: search.value.trim(),
      tag: tagFilter,
      sort: option.value,
      desc: option.dataset.desc === 'true',
      page: pageToLoad
    });
    if (id !== requestId) return;
    items = pageToLoad === 1 ? result.items : [...items, ...result.items];
    ({ total, page } = result);
    banner.hidden = true;
    render();
  } catch {
    if (id !== requestId) return;
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
    const saved = await saveItem(selected, {
      title,
      description: descriptionField.value.trim() || null,
      status: Number(statusField.value),
      dueDate: fromInputDate(dueDateField.value),
      tags: tagsField.value.split(',').map((tag) => tag.trim()).filter(Boolean)
    });
    showToast(selected ? 'Changes saved' : 'Item added');
    // Keep editing the saved item even if the current filter or page hides it.
    fillForm(saved);
    await loadItems();
  } catch (error) { handleError(error, false); }
  finally { saveButton.disabled = false; }
}

function handleError(error, onDelete) {
  if (!(error instanceof VersionConflictError)) {
    message.textContent = error.message;
    return;
  }
  conflictOnDelete = onDelete;
  conflictOverwrite.textContent = onDelete ? 'Delete anyway' : 'Overwrite';
  conflict.hidden = false;
}

// Both options start from the server's latest version. Overwrite keeps the form as typed and
// retries against that version; Reload discards the form and shows the latest.
async function resolveConflict(overwrite) {
  let latest;
  try { latest = await getItem(selected.id); }
  catch (error) {
    conflict.hidden = true;
    message.textContent = error.message;
    return;
  }
  if (!overwrite) {
    fillForm(latest);
    await loadItems();
    return;
  }
  selected = latest;
  conflict.hidden = true;
  await (conflictOnDelete ? removeSelected() : save());
}

async function deleteSelected() {
  if (!selected) return;
  $('#confirm-text').textContent = `“${selected.title}” will be permanently removed.`;
  confirmDialog.showModal();
  const choice = await new Promise((resolve) =>
    confirmDialog.addEventListener('close', () => resolve(confirmDialog.returnValue), { once: true }));
  if (choice !== 'delete') return;
  await removeSelected();
}

async function removeSelected() {
  try {
    await removeItem(selected);
    fillForm(null);
    showToast('Item deleted');
    await loadItems();
  } catch (error) { handleError(error, true); }
}

form.addEventListener('submit', (event) => { event.preventDefault(); save(); });
titleField.addEventListener('input', () => { if (titleField.value.trim()) setTitleError(false); });
cancelButton.addEventListener('click', () => fillForm(null));
deleteButton.addEventListener('click', deleteSelected);
$('#conflict-reload').addEventListener('click', () => resolveConflict(false));
conflictOverwrite.addEventListener('click', () => resolveConflict(true));
$('#new-button').addEventListener('click', newItem);
tagFilterChip.addEventListener('click', () => filterByTag(null));
$('#retry-button').addEventListener('click', () => loadItems());
loadMoreButton.addEventListener('click', () => loadItems(page + 1));
sortField.addEventListener('change', () => loadItems());
search.addEventListener('input', () => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => loadItems(), 250);
});

for (const chip of document.querySelectorAll('.chip')) {
  chip.addEventListener('click', () => {
    filter = chip.dataset.filter;
    for (const other of document.querySelectorAll('.chip'))
      other.setAttribute('aria-pressed', String(other === chip));
    loadItems();
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
