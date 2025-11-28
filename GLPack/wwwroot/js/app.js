// wwwroot/js/app.js
(function () {
    document.addEventListener("DOMContentLoaded", () => {
        const page = window.__page__?.name;

        if (page === "companiesHub") {
            initCompaniesHub();
        } else if (page === "companyDashboard") {
            initCompanyDashboard();
        } else if (page === "accountsIndex") {
            initAccountsIndex();
        } else if (page === "transactionsIndex") {
            initTransactionsIndex();
        }
    });

    // ---------- Home / Companies page ----------
    function initCompaniesHub() {
        const alertHost = document.querySelector('#alertHost');
        const grid = document.querySelector('#companiesGrid');

        // store selected company when clicking cards (kept from before)
        if (grid) {
            grid.addEventListener('click', async (e) => {
                // 1) Deleting a company
                const delBtn = e.target.closest('[data-action="delete-company"]');
                if (delBtn) {
                    e.preventDefault();
                    e.stopPropagation();

                    const id = Number(delBtn.dataset.companyId);
                    const name = delBtn.dataset.companyName || "this company";
                    if (!id) return;

                    if (!confirm(`Delete company "${name}"? This cannot be undone.`)) {
                        return;
                    }

                    try {
                        await API.deleteCompany(id);

                        const card = delBtn.closest('[data-company-card]');
                        if (card) card.remove();

                        showAlert(alertHost, "success", `Company "${name}" deleted.`);
                    } catch (err) {
                        showAlert(alertHost, "danger", `Failed to delete company: ${err.message}`);
                    }

                    return;
                }

                // 2) Normal navigation: click company card
                const a = e.target.closest('a[data-company-id]');
                if (!a) return;

                try {
                    const id = Number(a.dataset.companyId);
                    const name = a.dataset.companyName || a.textContent.trim();
                    localStorage.setItem('currentCompany', JSON.stringify({
                        id,
                        name,
                        ts: Date.now()
                    }));
                } catch (err) {
                    console.warn("Failed to store currentCompany", err);
                }
            });
        }

        // -------- Modal wiring --------
        const modal = document.querySelector('#newCompanyModal');
        const btnOpen = document.querySelector('#btnOpenNewCompanyModal');
        const btnClose = document.querySelector('#btnCloseNewCompanyModal');
        const btnCancel = document.querySelector('#btnCancelNewCompany');
        const btnSave = document.querySelector('#btnSaveNewCompany');
        const backdrop = modal?.querySelector('[data-role="modal-backdrop"]');
        const nameInput = document.querySelector('#newCompanyName');
        const codeInput = document.querySelector('#newCompanyCode');
        const modalAlertHost = document.querySelector('#newCompanyModalAlert');

        function openModal() {
            if (!modal) return;
            modal.classList.remove('hidden');
            modal.classList.add('flex');
            if (modalAlertHost) modalAlertHost.innerHTML = "";
            if (nameInput) {
                nameInput.value = "";
                nameInput.focus();
            }
            if (codeInput) codeInput.value = "";
        }

        function closeModal() {
            if (!modal) return;
            modal.classList.add('hidden');
            modal.classList.remove('flex');
        }

        function showModalAlert(type, msg) {
            if (!modalAlertHost) return;
            modalAlertHost.innerHTML = `
      <div class="${type === 'error'
                    ? 'text-red-600 dark:text-red-400'
                    : 'text-emerald-600 dark:text-emerald-400'}">
        ${escapeHtml(msg)}
      </div>
    `;
        }

        if (btnOpen) btnOpen.addEventListener('click', openModal);
        if (btnClose) btnClose.addEventListener('click', closeModal);
        if (btnCancel) btnCancel.addEventListener('click', closeModal);
        if (backdrop) backdrop.addEventListener('click', closeModal);

        if (btnSave) {
            btnSave.addEventListener('click', async () => {
                const name = (nameInput?.value || "").trim();
                const code = (codeInput?.value || "").trim();

                if (!name) {
                    showModalAlert("error", "Name is required.");
                    if (nameInput) nameInput.focus();
                    return;
                }

                btnSave.disabled = true;
                btnSave.textContent = "Saving...";

                try {
                    // Call your existing POST /api/companies
                    const payload = { name, code: code || null };
                    const created = await API.createCompany(payload);

                    // Try to get id from various shapes
                    const id = created.id ?? created.companyId ?? created.Id;
                    if (!id) {
                        showModalAlert("error", "Company created, but response had no id.");
                        btnSave.disabled = false;
                        btnSave.textContent = "Save";
                        return;
                    }

                    // Redirect straight to the new company's dashboard
                    window.location.href = API.companyDashboardUrl(id);
                } catch (err) {
                    console.error("Create company failed", err);
                    showModalAlert("error", err.message || "Failed to create company.");
                    btnSave.disabled = false;
                    btnSave.textContent = "Save";
                }
            });
        }
    }

    // ---------- Company dashboard page ----------
    function initCompanyDashboard() {
        const info = window.__page__ || {};
        const companyId = info.companyId;
        const companyName = info.companyName;

        const accountsPreview = document.querySelector('#accountsPreview');
        const txPreview = document.querySelector('#transactionsPreview');

        // For now, just show a simple message using server-provided info.
        if (accountsPreview) {
            accountsPreview.textContent = `Dashboard loaded for ${companyName} (ID ${companyId}). Accounts preview coming soon.`;
        }
        if (txPreview) {
            txPreview.textContent = `Dashboard loaded for ${companyName} (ID ${companyId}). Transactions preview coming soon.`;
        }

        // Later: here is where we will call
        //   API.listAccounts(companyId, { page: 1, pageSize: 5 })
        //   API.listTransactions(companyId, { page: 1, pageSize: 5 })
        // and render into #accountsPreview / #transactionsPreview.
    }

    // ---------- Accounts page ----------
    function initAccountsIndex() {
        const info = window.__page__ || {};
        const companyId = info.companyId;
        const companyName = info.companyName;

        const tbody = document.querySelector('#accountsTableBody');
        const searchInput = document.querySelector('#accountsSearch');
        const alertHost = document.querySelector('#accountsAlertHost');
        const btnAdd = document.querySelector('#btnAddAccountRow');
        const btnDelete = document.querySelector('#btnDeleteAccounts');
        const chkSelectAll = document.querySelector('#chkAccountsSelectAll');

        if (!companyId || !tbody) return;

        let rows = []; // state: array of {accountCode, name, type, isActive, createdAt, _mode, _selected, _orig}

        // ---------- load ----------
        async function load() {
            try {
                const data = await API.getAccounts(companyId, { page: 1, pageSize: 500 });
                rows = (data || []).map(normalizeAccount);
                render();
            } catch (err) {
                showAlert(alertHost, "danger", "Failed to load accounts: " + err.message);
                tbody.innerHTML = `
                    <tr>
                      <td colspan="6" class="px-4 py-4 text-center text-red-500">
                        Error loading accounts.
                      </td>
                    </tr>
                  `;
            }
        }

        function normalizeAccount(acc) {
            return {
                accountCode: acc.accountCode ?? acc.AccountCode ?? "",
                name: acc.name ?? acc.Name ?? "",
                type: acc.type ?? acc.Type ?? "",
                _mode: "view",
                _selected: false,
                _orig: null
            };
        }

        function getFilteredRowsWithIndex() {
            const q = (searchInput?.value || "").trim().toLowerCase();
            const result = rows.map((row, index) => ({ row, index }));
            if (!q) return result;
            return result.filter(({ row }) => {
                const code = (row.accountCode || "").toLowerCase();
                const name = (row.name || "").toLowerCase();
                return code.includes(q) || name.includes(q);
            });
        }

        // ---------- render ----------
        function render() {
            const viewRows = getFilteredRowsWithIndex();

            if (!viewRows.length) {
                tbody.innerHTML = `
                <tr>
                  <td colspan="6" class="px-4 py-4 text-center text-gray-500 dark:text-neutral-400">
                    No accounts found for ${escapeHtml(companyName || "")}.
                  </td>
                </tr>
              `;
                if (btnDelete) btnDelete.disabled = true;
                if (chkSelectAll) chkSelectAll.checked = false;
            } else {
                tbody.innerHTML = viewRows.map(({ row, index }) => rowHtml(row, index)).join("");

                const anySelected = rows.some(r => r._selected);
                if (btnDelete) btnDelete.disabled = !anySelected;

                if (chkSelectAll) {
                    const allVisibleSelected = viewRows.every(({ row }) => row._selected);
                    chkSelectAll.checked = allVisibleSelected && anySelected;
                }
            }
        }

        function rowHtml(row, index) {
            if (row._mode === "edit" || row._mode === "new") {
                const isNew = row._mode === "new";
                return `
                    <tr data-index="${index}" class="bg-neutral-900/10 dark:bg-neutral-800/60">
                      <td class="px-3 py-2">
                        <input type="checkbox"
                               class="h-4 w-4 rounded border-gray-300 dark:border-neutral-600 acc-select"
                               ${row._selected ? "checked" : ""} />
                      </td>
                      <td class="px-3 py-2">
                        <input data-field="accountCode"
                               class="w-full rounded border border-gray-300 dark:border-neutral-700 bg-white dark:bg-neutral-900
                                      px-2 py-1 text-xs font-mono"
                               value="${escapeHtml(row.accountCode || "")}" />
                      </td>
                      <td class="px-3 py-2">
                        <input data-field="name"
                               class="w-full rounded border border-gray-300 dark:border-neutral-700 bg-white dark:bg-neutral-900
                                      px-2 py-1 text-xs"
                               value="${escapeHtml(row.name || "")}" />
                      </td>
                      <td class="px-3 py-2">
                        <input data-field="type"
                               class="w-full rounded border border-gray-300 dark:border-neutral-700 bg-white dark:bg-neutral-900
                                      px-2 py-1 text-xs"
                               value="${escapeHtml(row.type || "")}" />
                      </td>
                      <td class="px-3 py-2 text-right space-x-2">
                        <button type="button"
                                data-action="save"
                                class="text-xs text-emerald-600 hover:text-emerald-500">
                          Save
                        </button>
                        <button type="button"
                                data-action="cancel"
                                class="text-xs text-gray-400 hover:text-gray-200">
                          Cancel
                        </button>
                      </td>
                    </tr>
                  `;
            }

            // view mode
            return `
              <tr data-index="${index}" class="hover:bg-gray-50 dark:hover:bg-neutral-800/60 cursor-default">
                <td class="px-3 py-2">
                  <input type="checkbox"
                         class="h-4 w-4 rounded border-gray-300 dark:border-neutral-600 acc-select"
                         ${row._selected ? "checked" : ""} />
                </td>
                <td class="px-3 py-2 font-mono text-xs">${escapeHtml(row.accountCode)}</td>
                <td class="px-3 py-2">${escapeHtml(row.name)}</td>
                <td class="px-3 py-2">${escapeHtml(row.type)}</td>
                <td class="px-3 py-2 text-right space-x-2">
                  <button type="button"
                          data-action="edit"
                          class="text-xs text-indigo-500 hover:text-indigo-400">
                    Edit
                  </button>
                  <button type="button"
                          data-action="delete"
                          class="text-xs text-red-500 hover:text-red-400">
                    Delete
                  </button>
                </td>
              </tr>
            `;
        }

        // ---------- helpers for row operations ----------
        function enterEdit(index, isNew = false) {
            const row = rows[index];
            if (!row) return;
            if (!isNew) {
                row._orig = { ...row };
            }
            row._mode = isNew ? "new" : "edit";
            render();
        }

        function cancelEdit(index) {
            const row = rows[index];
            if (!row) return;

            if (row._mode === "new") {
                // Drop the new row entirely
                rows.splice(index, 1);
            } else if (row._mode === "edit" && row._orig) {
                rows[index] = { ...row._orig, _mode: "view", _selected: row._selected, _orig: null };
            } else {
                row._mode = "view";
                row._orig = null;
            }
            render();
        }

        async function saveRow(index) {
            const row = rows[index];
            if (!row) return;

            const code = (row.accountCode || "").trim();
            const name = (row.name || "").trim();
            const type = (row.type || "").trim() || "Unknown";
            const isActive = !!row.isActive;

            if (!code) {
                showAlert(alertHost, "danger", "Account code is required.");
                return;
            }
            if (!name) {
                showAlert(alertHost, "danger", "Account name is required.");
                return;
            }

            const dto = {
                accountCode: code,
                name,
                type,
                isActive
            };

            try {
                if (row._mode === "new") {
                    const created = await API.createAccount(companyId, dto);
                    const normalized = normalizeAccount(created || dto);
                    rows[index] = Object.assign(normalized, { _mode: "view", _selected: true });
                    showAlert(alertHost, "success", "Account created.");
                } else {
                    const originalCode = row._orig?.accountCode || row.accountCode;
                    const updated = await API.updateAccount(companyId, originalCode, dto);
                    const normalized = normalizeAccount(updated || dto);
                    rows[index] = Object.assign(normalized, { _mode: "view", _selected: row._selected });
                    showAlert(alertHost, "success", "Account updated.");
                }
            } catch (err) {
                showAlert(alertHost, "danger", "Failed to save account: " + err.message);
            }

            render();
        }

        async function deleteRow(index) {
            const row = rows[index];
            if (!row) return;
            if (!row.accountCode) {
                rows.splice(index, 1);
                render();
                return;
            }

            if (!confirm(`Delete account ${row.accountCode}?`)) return;

            try {
                await API.deleteAccount(companyId, row.accountCode);
                rows.splice(index, 1);
                showAlert(alertHost, "success", "Account deleted.");
                render();
            } catch (err) {
                showAlert(alertHost, "danger", "Failed to delete account: " + err.message);
            }
        }

        async function deleteSelected() {
            const indexes = rows
                .map((r, i) => (r._selected ? i : -1))
                .filter(i => i >= 0)
                .reverse(); // delete from end to keep indexes stable

            if (!indexes.length) return;
            if (!confirm(`Delete ${indexes.length} selected account(s)?`)) return;

            try {
                for (const i of indexes) {
                    const row = rows[i];
                    if (!row || !row.accountCode) continue;
                    await API.deleteAccount(companyId, row.accountCode);
                    rows.splice(i, 1);
                }
                showAlert(alertHost, "success", "Selected accounts deleted.");
                render();
            } catch (err) {
                showAlert(alertHost, "danger", "Failed to delete some accounts: " + err.message);
                load(); // fallback to reload from server
            }
        }

        // ---------- events ----------
        if (btnAdd) {
            btnAdd.addEventListener("click", () => {
                rows.unshift({
                    accountCode: "",
                    name: "",
                    type: "",
                    isActive: true,
                    createdAt: null,
                    _mode: "new",
                    _selected: false,
                    _orig: null
                });
                render();
            });
        }

        if (btnDelete) {
            btnDelete.addEventListener("click", () => {
                deleteSelected();
            });
        }

        if (searchInput) {
            searchInput.addEventListener("input", debounce(render, 200));
        }

        if (chkSelectAll) {
            chkSelectAll.addEventListener("change", () => {
                const viewRows = getFilteredRowsWithIndex();
                const checked = chkSelectAll.checked;
                viewRows.forEach(({ index }) => {
                    if (rows[index]) rows[index]._selected = checked;
                });
                render();
            });
        }

        // row clicks / dblclicks / inputs via delegation
        tbody.addEventListener("dblclick", (e) => {
            const tr = e.target.closest("tr[data-index]");
            if (!tr) return;
            const index = Number(tr.getAttribute("data-index"));
            if (Number.isNaN(index)) return;

            // double-click anywhere on row → edit (unless it's already new)
            if (rows[index]._mode === "view") {
                enterEdit(index, false);
            }
        });

        tbody.addEventListener("click", (e) => {
            const tr = e.target.closest("tr[data-index]");
            if (!tr) return;
            const index = Number(tr.getAttribute("data-index"));
            if (Number.isNaN(index)) return;

            const action = e.target.getAttribute("data-action");
            if (action === "edit") {
                enterEdit(index, false);
            } else if (action === "delete") {
                deleteRow(index);
            } else if (action === "save") {
                saveRow(index);
            } else if (action === "cancel") {
                cancelEdit(index);
            } else if (e.target.classList.contains("acc-select")) {
                rows[index]._selected = e.target.checked;
                render();
            }
        });

        tbody.addEventListener("input", (e) => {
            const tr = e.target.closest("tr[data-index]");
            if (!tr) return;
            const index = Number(tr.getAttribute("data-index"));
            if (Number.isNaN(index)) return;

            const field = e.target.getAttribute("data-field");
            if (!field) return;

            if (field === "isActive") {
                rows[index].isActive = e.target.checked;
            } else {
                rows[index][field] = e.target.value;
            }
        });

        load();
    }

    // ---------- Transactions page ----------
    function initTransactionsIndex() {
        const info = window.__page__ || {};
        const companyId = info.companyId;
        const companyName = info.companyName;

        if (!companyId) return;

        const tbody = document.querySelector('#txTableBody');
        const searchInput = document.querySelector('#txSearch');
        const fromInput = document.querySelector('#txFrom');
        const toInput = document.querySelector('#txTo');
        const alertHost = document.querySelector('#txAlertHost');
        const pagerSummary = document.querySelector('#txPagerSummary');

        const btnNew = document.querySelector('#btnTxNew');
        const btnDeleteSelected = document.querySelector('#btnTxDeleteSelected');
        const chkSelectAll = document.querySelector('#chkTxSelectAll');

        const linesBody = document.querySelector('#txLinesBody');
        const currentSummary = document.querySelector('#txCurrentSummary');
        const detailNo = document.querySelector('#txDetailNo');
        const detailDate = document.querySelector('#txDetailDate');
        const detailDesc = document.querySelector('#txDetailDescription');
        const btnAddLine = document.querySelector('#btnTxAddLine');
        const btnSave = document.querySelector('#btnTxSave');
        const btnCancel = document.querySelector('#btnTxCancel');
        const totalDrEl = document.querySelector('#txTotalDebit');
        const totalCrEl = document.querySelector('#txTotalCredit');

        let transactions = [];      // [{ transactionNo, txnDate, description, entries[], _mode, _selected, _orig }]
        let selectedIndex = null;   // index in transactions[]
        let accounts = []; //{ code, name }

        // ---------- loading ----------
        async function loadAll() {
            try {
                tbody.innerHTML = `
                    <tr>
                      <td colspan="6" class="px-4 py-4 text-center text-gray-500 dark:text-neutral-400">
                        Loading transactions...
                      </td>
                    </tr>
                  `;
                renderDetail(null);

                const fromVal = (fromInput?.value || "").trim();
                const toVal = (toInput?.value || "").trim();
                const q = (searchInput?.value || "").trim();

                const { items } = await API.getTransactions(companyId, {
                    page: 1,
                    pageSize: 500,
                    from: fromVal || undefined,
                    to: toVal || undefined,
                    q: q || undefined
                });

                transactions = (items || []).map(normalizeTx);
                if (pagerSummary) {
                    pagerSummary.textContent =
                        transactions.length > 0
                            ? `${transactions.length} transaction${transactions.length === 1 ? "" : "s"}`
                            : "No transactions";
                }
                renderMaster();
                renderDetail(null);
            } catch (err) {
                showAlert(alertHost, "danger", "Failed to load transactions: " + err.message);
                tbody.innerHTML = `
                    <tr>
                      <td colspan="6" class="px-4 py-4 text-center text-red-500">
                        Error loading transactions.
                      </td>
                    </tr>
                  `;
                renderDetail(null);
            }
        }

        function normalizeTx(tx) {
            return {
                transactionNo: tx.TransactionNo ?? tx.transactionNo ?? null,
                txnDate: tx.TxnDate ?? tx.txnDate ?? null,
                description: tx.Description ?? tx.description ?? "",
                entries: Array.isArray(tx.Entries ?? tx.entries) ? (tx.Entries ?? tx.entries).map(normalizeEntry) : [],
                _mode: "view",
                _selected: false,
                _orig: null
            };
        }

        function normalizeEntry(e) {
            return {
                accountCode: e.AccountCode ?? e.accountCode ?? "",
                amount: Number(e.Amount ?? e.amount ?? 0) || 0,
                drCr: (e.DrCr ?? e.drCr ?? "DR").toString().toUpperCase(),
                memo: e.Memo ?? e.memo ?? ""
            };
        }

        function filteredIndexes() {
            const q = (searchInput?.value || "").trim().toLowerCase();
            const result = transactions.map((row, index) => ({ row, index }));
            if (!q) return result;
            return result.filter(({ row }) => {
                const desc = (row.description || "").toLowerCase();
                const txnNo = String(row.transactionNo || "");
                return desc.includes(q) || txnNo.includes(q);
            });
        }

        async function loadAccounts() {
            try {
                const data = await API.getAccounts(companyId, { page: 1, pageSize: 500 });
                accounts = (data || []).map(acc => ({
                    code: acc.accountCode ?? acc.AccountCode ?? "",
                    name: acc.name ?? acc.Name ?? ""
                }));
            } catch (err) {
                console.warn("Failed to load accounts for dropdown", err);
            }
        }

        // ---------- master render ----------
        function renderMaster() {
            const viewRows = filteredIndexes();

            if (!viewRows.length) {
                tbody.innerHTML = `
                    <tr>
                      <td colspan="6" class="px-4 py-4 text-center text-gray-500 dark:text-neutral-400">
                        No transactions found for ${escapeHtml(companyName || "")}.
                      </td>
                    </tr>
                  `;
                if (btnDeleteSelected) btnDeleteSelected.disabled = true;
                if (chkSelectAll) chkSelectAll.checked = false;
                return;
            }

            tbody.innerHTML = viewRows.map(({ row, index }) => txRowHtml(row, index)).join("");

            const anySel = transactions.some(t => t._selected);
            if (btnDeleteSelected) btnDeleteSelected.disabled = !anySel;

            if (chkSelectAll) {
                const allVisibleSelected = viewRows.every(({ row }) => row._selected);
                chkSelectAll.checked = allVisibleSelected && anySel;
            }
        }

        function txRowHtml(row, index) {
            const isSelected = selectedIndex === index;
            const isEditing = row._mode === "edit" || row._mode === "new";

            const baseClasses =
                "hover:bg-gray-50 dark:hover:bg-neutral-800/60 cursor-pointer";
            const selectedClass = isSelected ? " bg-indigo-50/60 dark:bg-indigo-900/40" : "";
            const editingClass = isEditing ? " border-l-2 border-indigo-500" : "";

            return `
              <tr data-index="${index}" class="${baseClasses}${selectedClass}${editingClass}">
                <td class="px-3 py-2">
                  <input type="checkbox"
                         class="h-4 w-4 rounded border-gray-300 dark:border-neutral-600 tx-select"
                         ${row._selected ? "checked" : ""} />
                </td>
                <td class="px-3 py-2 font-mono text-xs">
                  ${row.transactionNo != null
                                    ? escapeHtml(String(row.transactionNo))
                                    : "<span class='text-gray-400'>New</span>"}
                </td>
                <td class="px-3 py-2 text-xs text-gray-700 dark:text-neutral-200">
                  ${escapeHtml(formatDate(row.txnDate))}
                </td>
                <td class="px-3 py-2">
                  ${escapeHtml(row.description || "")}
                </td>
                <td class="px-3 py-2 text-right space-x-2">
                  <button type="button"
                          data-action="edit"
                          class="text-xs text-indigo-500 hover:text-indigo-400">
                    Edit
                  </button>
                  <button type="button"
                          data-action="delete"
                          class="text-xs text-red-500 hover:text-red-400">
                    Delete
                  </button>
                </td>
              </tr>
            `;
        }

        // ---------- detail render ----------
        function renderDetail(index) {
            selectedIndex = index;

            const tx = index == null ? null : transactions[index];

            if (!tx) {
                if (currentSummary) {
                    currentSummary.textContent = "Select a transaction above to view or edit its lines.";
                }
                if (detailDate) {
                    detailDate.value = "";
                    detailDate.disabled = true;
                }
                if (detailDesc) {
                    detailDesc.value = "";
                    detailDesc.disabled = true;
                }
                if (detailNo) {
                    detailNo.value = "";
                    detailNo.disabled = true;
                }
                if (btnAddLine) btnAddLine.disabled = true;
                if (btnSave) btnSave.disabled = true;
                if (btnCancel) btnCancel.disabled = true;
                if (linesBody) {
                    linesBody.innerHTML = `
          <tr>
            <td colspan="6" class="px-4 py-4 text-center text-gray-500 dark:text-neutral-400">
              No transaction selected.
            </td>
          </tr>
        `;
                }
                if (totalDrEl) totalDrEl.textContent = "0.00";
                if (totalCrEl) totalCrEl.textContent = "0.00";
                renderMaster();
                return;
            }

            const isEditing = tx._mode === "edit" || tx._mode === "new";

            if (currentSummary) {
                const no = tx.transactionNo != null ? tx.transactionNo : "(new)";
                currentSummary.textContent =
                    `${no} · ${formatDate(tx.txnDate) || "no date"} · ${tx.description || "(no description)"}`;
            }

            if (detailDate) {
                detailDate.disabled = !isEditing;
                detailDate.value = tx.txnDate
                    ? new Date(tx.txnDate).toISOString().slice(0, 10)
                    : "";
            }
            if (detailDesc) {
                detailDesc.disabled = !isEditing;
                detailDesc.value = tx.description || "";
            }
            if (detailNo) {
                detailNo.disabled = !(tx._mode === "new");
                detailNo.value = tx.transactionNo != null ? tx.transactionNo : "";
            }

            if (btnAddLine) btnAddLine.disabled = !isEditing;
            if (btnSave) btnSave.disabled = !isEditing;
            if (btnCancel) btnCancel.disabled = !isEditing;

            if (!linesBody) return;

            if (!tx.entries.length && !isEditing) {
                linesBody.innerHTML = `
                <tr>
                  <td colspan="6" class="px-4 py-4 text-center text-gray-500 dark:text-neutral-400">
                    This transaction has no lines.
                  </td>
                </tr>
              `;
            } else {
                linesBody.innerHTML = tx.entries.map((line, idx) => lineRowHtml(line, idx, isEditing)).join("");
            }

            const totals = calcTotals(tx.entries);
            if (totalDrEl) totalDrEl.textContent = totals.dr.toFixed(2);
            if (totalCrEl) totalCrEl.textContent = totals.cr.toFixed(2);

            renderMaster();
        }

        function lineRowHtml(line, idx, editable) {
            const debit = line.drCr === "DR" ? line.amount : 0;
            const credit = line.drCr === "CR" ? line.amount : 0;
            const options = accounts.map(a => `
              <option value="${escapeHtml(a.code)}"
                      ${a.code === (line.accountCode || "") ? "selected" : ""}>
                ${escapeHtml(a.code)} — ${escapeHtml(a.name || "")}
              </option>
            `).join("");

            if (!editable) {
                return `
                    <tr>
                      <td class="px-4 py-2 text-xs text-gray-500 dark:text-neutral-400">${idx + 1}</td>
                      <td class="px-4 py-2">
                        <div class="font-mono text-xs">${escapeHtml(line.accountCode)}</div>
                      </td>
                      <td class="px-4 py-2 text-right font-mono text-xs">${debit ? escapeHtml(debit.toFixed(2)) : "&nbsp;"}</td>
                      <td class="px-4 py-2 text-right font-mono text-xs">${credit ? escapeHtml(credit.toFixed(2)) : "&nbsp;"}</td>
                      <td class="px-4 py-2 text-xs text-gray-600 dark:text-neutral-300">${escapeHtml(line.memo || "")}</td>
                      <td class="px-4 py-2 text-right"></td>
                    </tr>
                  `;
            }

            // editable
            return `
                  <tr data-line-index="${idx}" class="hover:bg-gray-50 dark:hover:bg-neutral-800/60">
                    <td class="px-4 py-2 text-xs text-gray-500 dark:text-neutral-400">${idx + 1}</td>
                    <td class="px-4 py-2">
                      <select data-field="accountCode"
                              class="w-full rounded border border-gray-300 dark:border-neutral-700
                                     bg-white dark:bg-neutral-900 px-2 py-1 text-xs">
                        <option value="">Select account…</option>
                        ${options}
                      </select>
                    </td>
                    <td class="px-4 py-2 text-right">
                      <input data-field="debit"
                             type="number"
                             step="0.01"
                             class="w-24 rounded border border-gray-300 dark:border-neutral-700 bg-white dark:bg-neutral-900
                                    px-2 py-1 text-xs text-right font-mono"
                             value="${debit ? escapeHtml(debit.toString()) : ""}" />
                    </td>
                    <td class="px-4 py-2 text-right">
                      <input data-field="credit"
                             type="number"
                             step="0.01"
                             class="w-24 rounded border border-gray-300 dark:border-neutral-700 bg-white dark:bg-neutral-900
                                    px-2 py-1 text-xs text-right font-mono"
                             value="${credit ? escapeHtml(credit.toString()) : ""}" />
                    </td>
                    <td class="px-4 py-2">
                      <input data-field="memo"
                             class="w-full rounded border border-gray-300 dark:border-neutral-700 bg-white dark:bg-neutral-900
                                    px-2 py-1 text-xs"
                             value="${escapeHtml(line.memo || "")}" />
                    </td>
                    <td class="px-4 py-2 text-right">
                      <button type="button"
                              data-action="remove-line"
                              class="text-xs text-red-500 hover:text-red-400">
                        ✕
                      </button>
                    </td>
                  </tr>
                `;
        }

        function calcTotals(entries) {
            return (entries || []).reduce(
                (acc, line) => {
                    if (line.drCr === "DR") acc.dr += line.amount || 0;
                    else if (line.drCr === "CR") acc.cr += line.amount || 0;
                    return acc;
                },
                { dr: 0, cr: 0 }
            );
        }

        // ---------- edit helpers ----------
        function startEdit(index, isNew = false) {
            if (index == null) return;
            const tx = transactions[index];
            if (!tx) return;

            // cancel other edits
            transactions.forEach((t, i) => {
                if (i !== index && (t._mode === "edit" || t._mode === "new") && t._orig) {
                    transactions[i] = { ...t._orig, _mode: "view", _selected: t._selected, _orig: null };
                } else if (i !== index) {
                    t._mode = "view";
                    t._orig = null;
                }
            });

            if (!isNew) {
                tx._orig = JSON.parse(JSON.stringify(tx));
            }
            tx._mode = isNew ? "new" : "edit";
            selectedIndex = index;
            renderDetail(index);
        }

        function cancelEdit() {
            if (selectedIndex == null) return;
            const tx = transactions[selectedIndex];
            if (!tx) return;

            if (tx._mode === "new") {
                transactions.splice(selectedIndex, 1);
                selectedIndex = null;
            } else if (tx._mode === "edit" && tx._orig) {
                transactions[selectedIndex] = { ...tx._orig, _mode: "view", _selected: tx._selected, _orig: null };
            } else {
                tx._mode = "view";
                tx._orig = null;
            }

            renderDetail(selectedIndex);
        }

        async function saveCurrent() {
            if (selectedIndex == null) return;
            const tx = transactions[selectedIndex];
            if (!tx) return;

            // header updates from inputs
            const dateVal = detailDate?.value;
            const descVal = (detailDesc?.value || "").trim();

            if (!dateVal) {
                showAlert(alertHost, "danger", "Please select a date.");
                return;
            }

            tx.txnDate = dateVal;
            tx.description = descVal;

            const totals = calcTotals(tx.entries);
            if (Math.abs(totals.dr - totals.cr) > 0.005) {
                showAlert(alertHost, "danger", "Debits and credits must balance before saving.");
                return;
            }

            // build payload
            const payload = {
                companyId,
                transactionNo: tx.transactionNo,
                txnDate: tx.txnDate,
                description: tx.description,
                entries: tx.entries.map(line => ({
                    accountCode: line.accountCode,
                    amount: line.amount,
                    drCr: line.drCr,
                    memo: line.memo
                }))
            };

            try {
                let saved;
                if (tx._mode === "new" || tx.transactionNo == null) {
                    saved = await API.createTransaction(companyId, payload);
                } else {
                    saved = await API.updateTransaction(companyId, tx.transactionNo, payload);
                }
                const normalized = normalizeTx(saved || tx);
                normalized._mode = "view";
                normalized._selected = true;
                normalized._orig = null;
                transactions[selectedIndex] = normalized;
                showAlert(alertHost, "success", "Transaction saved.");
                renderDetail(selectedIndex);
            } catch (err) {
                showAlert(alertHost, "danger", "Failed to save transaction: " + err.message);
            }
        }

        async function deleteRow(index) {
            const tx = transactions[index];
            if (!tx) return;

            if (tx.transactionNo == null || tx._mode === "new") {
                transactions.splice(index, 1);
                if (selectedIndex === index) selectedIndex = null;
                renderDetail(selectedIndex);
                return;
            }

            if (!confirm(`Delete transaction ${tx.transactionNo}?`)) return;

            try {
                await API.deleteTransaction(companyId, tx.transactionNo);
                transactions.splice(index, 1);
                if (selectedIndex === index) selectedIndex = null;
                showAlert(alertHost, "success", "Transaction deleted.");
                renderDetail(selectedIndex);
            } catch (err) {
                showAlert(alertHost, "danger", "Failed to delete transaction: " + err.message);
            }
        }

        async function deleteSelected() {
            const idxs = transactions
                .map((t, i) => (t._selected ? i : -1))
                .filter(i => i >= 0)
                .reverse();

            if (!idxs.length) return;
            if (!confirm(`Delete ${idxs.length} selected transaction(s)?`)) return;

            try {
                for (const i of idxs) {
                    const tx = transactions[i];
                    if (!tx || tx.transactionNo == null) {
                        transactions.splice(i, 1);
                        continue;
                    }
                    await API.deleteTransaction(companyId, tx.transactionNo);
                    transactions.splice(i, 1);
                }
                selectedIndex = null;
                showAlert(alertHost, "success", "Selected transactions deleted.");
                renderDetail(null);
            } catch (err) {
                showAlert(alertHost, "danger", "Failed to delete some transactions: " + err.message);
                loadAll(); // fallback reload
            }
        }

        function getNextTransactionNo() {
            let max = 0;
            for (const t of transactions) {
                const n = Number(t.transactionNo);
                if (Number.isFinite(n) && n > max) max = n;
            }
            return max > 0 ? max + 1 : 2001; // or 1, or whatever starting point you like
        }

        // ---------- events ----------
        if (btnNew) {
            btnNew.addEventListener("click", () => {
                const newTx = {
                    transactionNo: getNextTransactionNo(),
                    txnDate: new Date().toISOString().slice(0, 10),
                    description: "",
                    entries: [normalizeEntry({})],
                    _mode: "new",
                    _selected: false,
                    _orig: null
                };
                transactions.unshift(newTx);
                startEdit(0, true);
            });
        }

        if (btnDeleteSelected) {
            btnDeleteSelected.addEventListener("click", () => {
                deleteSelected();
            });
        }

        if (searchInput) searchInput.addEventListener("input", debounce(() => { renderMaster(); }, 200));
        if (fromInput) fromInput.addEventListener("change", () => loadAll());
        if (toInput) toInput.addEventListener("change", () => loadAll());

        if (chkSelectAll) {
            chkSelectAll.addEventListener("change", () => {
                const checked = chkSelectAll.checked;
                filteredIndexes().forEach(({ index }) => {
                    if (transactions[index]) transactions[index]._selected = checked;
                });
                renderMaster();
            });
        }

        // master table events
        tbody.addEventListener("click", (e) => {
            const tr = e.target.closest("tr[data-index]");
            if (!tr) return;
            const index = Number(tr.getAttribute("data-index"));
            if (Number.isNaN(index)) return;

            const action = e.target.getAttribute("data-action");
            if (action === "edit") {
                startEdit(index, false);
                return;
            }
            if (action === "delete") {
                deleteRow(index);
                return;
            }

            if (e.target.classList.contains("tx-select")) {
                transactions[index]._selected = e.target.checked;
                renderMaster();
                return;
            }

            // default click = select row
            renderDetail(index);
        });

        // detail header changes
        if (detailDate) {
            detailDate.addEventListener("change", () => {
                if (selectedIndex == null) return;
                const tx = transactions[selectedIndex];
                if (!tx) return;
                tx.txnDate = detailDate.value;
                renderDetail(selectedIndex);
            });
        }
        if (detailDesc) {
            detailDesc.addEventListener("input", () => {
                if (selectedIndex == null) return;
                const tx = transactions[selectedIndex];
                if (!tx) return;
                tx.description = detailDesc.value;
            });
        }
        if (detailNo) {
            detailNo.addEventListener("input", () => {
                if (selectedIndex == null) return;
                const tx = transactions[selectedIndex];
                if (!tx) return;
                const n = parseInt(detailNo.value, 10);
                tx.transactionNo = Number.isFinite(n) ? n : null;
                renderMaster();
            });
        }

        if (btnAddLine) {
            btnAddLine.addEventListener("click", () => {
                if (selectedIndex == null) return;
                const tx = transactions[selectedIndex];
                if (!tx) return;
                tx.entries.push(normalizeEntry({}));
                renderDetail(selectedIndex);
            });
        }

        if (btnCancel) {
            btnCancel.addEventListener("click", () => cancelEdit());
        }
        if (btnSave) {
            btnSave.addEventListener("click", () => saveCurrent());
        }

        // detail lines events
        if (linesBody) {

            function handleLineChange(e) {
                const tr = e.target.closest("tr[data-line-index]");
                if (!tr || selectedIndex == null) return;

                const idx = Number(tr.getAttribute("data-line-index"));
                if (Number.isNaN(idx)) return;

                const field = e.target.getAttribute("data-field");
                if (!field) return;

                const tx = transactions[selectedIndex];
                const line = tx?.entries[idx];
                if (!line) return;

                if (field === "accountCode") {
                    // dropdown change
                    line.accountCode = e.target.value;
                    return;
                }

                if (field === "memo") {
                    line.memo = e.target.value;
                    return;
                }

                if (field === "debit" || field === "credit") {
                    const value = parseFloat(e.target.value);
                    const amount = Number.isFinite(value) ? value : 0;

                    if (field === "debit") {
                        line.drCr = amount > 0 ? "DR" : line.drCr;
                        line.amount = amount > 0 ? amount : 0;

                        // clear credit input
                        const creditInput = tr.querySelector('[data-field="credit"]');
                        if (creditInput && amount > 0) creditInput.value = "";
                    } else {
                        line.drCr = amount > 0 ? "CR" : line.drCr;
                        line.amount = amount > 0 ? amount : 0;

                        const debitInput = tr.querySelector('[data-field="debit"]');
                        if (debitInput && amount > 0) debitInput.value = "";
                    }
                }

                const totals = calcTotals(tx.entries);
                if (totalDrEl) totalDrEl.textContent = totals.dr.toFixed(2);
                if (totalCrEl) totalCrEl.textContent = totals.cr.toFixed(2);
            }

            // listen for both typing and dropdown changes
            linesBody.addEventListener("input", handleLineChange);
            linesBody.addEventListener("change", handleLineChange);

            // keep your existing click handler for remove-line
            linesBody.addEventListener("click", (e) => {
                const tr = e.target.closest("tr[data-line-index]");
                if (!tr || selectedIndex == null) return;
                const idx = Number(tr.getAttribute("data-line-index"));
                if (Number.isNaN(idx)) return;

                if (e.target.getAttribute("data-action") === "remove-line") {
                    const tx = transactions[selectedIndex];
                    if (!tx) return;
                    tx.entries.splice(idx, 1);
                    renderDetail(selectedIndex);
                }
            });
        }

        (async () => {
            await loadAccounts();
            await loadAll();
        })();
    }  

    // ---------- helpers ----------
    function showAlert(host, type, msg) {
        if (!host) return;
        const div = document.createElement("div");
        div.className = `alert ${type === "success" ? "alert-success" : "alert-danger"}`;
        div.textContent = msg;
        host.appendChild(div);
        setTimeout(() => div.remove(), 4000);
    }

    function formatDate(value) {
        try {
            if (!value) return "";
            const d = new Date(value);
            if (Number.isNaN(d.getTime())) return "";
            return d.toISOString().slice(0, 10);
        } catch {
            return "";
        }
    }

    function escapeHtml(s) {
        return String(s ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function debounce(fn, ms) {
        let t;
        return (...args) => {
            clearTimeout(t);
            t = setTimeout(() => fn(...args), ms);
        };
    }

    function qs(sel) { return document.querySelector(sel); }
})();
