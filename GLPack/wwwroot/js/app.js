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
        } else if (page === "ledgerSearch") {
            initLedgerSearch();
        } else if (page === "adminIndex") {
            initAdminIndex();
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

            const alertClass = type === "error"
                ? "rounded-lg border border-red-300 bg-red-50 px-3 py-2 text-xs font-medium text-red-900 dark:border-red-700/60 dark:bg-red-950/40 dark:text-red-100"
                : "rounded-lg border border-emerald-300 bg-emerald-50 px-3 py-2 text-xs font-medium text-emerald-900 dark:border-emerald-700/60 dark:bg-emerald-950/40 dark:text-emerald-100";

            modalAlertHost.innerHTML = `
                <div class="${alertClass}">
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
            accountsPreview.textContent = `Dashboard loaded for ${companyName} (ID ${companyId}).`;
        }
        if (txPreview) {
            txPreview.textContent = `Dashboard loaded for ${companyName} (ID ${companyId}).`;
        }
    }

    // ---------- Accounts page ----------
    function renderPager(host, page, pageSize, totalCount, onPageChange) {
        if (!host) return;

        const totalPages = Math.max(1, Math.ceil((totalCount || 0) / pageSize));
        const safePage = Math.min(Math.max(1, page), totalPages);

        host.innerHTML = `
            <div class="text-xs text-gray-500 dark:text-neutral-400">
                Page ${safePage} of ${totalPages} · ${totalCount} item(s)
            </div>
            <div class="flex items-center gap-2">
                <button type="button"
                        data-page="prev"
                        class="rounded border border-gray-300 dark:border-neutral-700 px-3 py-1 text-xs ${safePage <= 1 ? 'opacity-40 cursor-not-allowed' : ''}"
                        ${safePage <= 1 ? 'disabled' : ''}>
                    Previous
                </button>
                <button type="button"
                        data-page="next"
                        class="rounded border border-gray-300 dark:border-neutral-700 px-3 py-1 text-xs ${safePage >= totalPages ? 'opacity-40 cursor-not-allowed' : ''}"
                        ${safePage >= totalPages ? 'disabled' : ''}>
                    Next
                </button>
            </div>
        `;

        host.querySelector('[data-page="prev"]')?.addEventListener('click', () => {
            if (safePage > 1) onPageChange(safePage - 1);
        });

        host.querySelector('[data-page="next"]')?.addEventListener('click', () => {
            if (safePage < totalPages) onPageChange(safePage + 1);
        });
    }

    function initAccountsIndex() {
        const info = window.__page__ || {};
        const companyId = info.companyId;
        const companyName = info.companyName;

        const tbody = document.querySelector('#accountsTableBody');
        const searchInput = document.querySelector('#accountsSearch');
        const typeFilter = document.querySelector('#accountsTypeFilter');
        const alertHost = document.querySelector('#accountsAlertHost');
        const btnAdd = document.querySelector('#btnAddAccountRow');
        const btnDelete = document.querySelector('#btnDeleteAccounts');
        const btnImport = document.querySelector('#btnImportAccounts');
        const chkSelectAll = document.querySelector('#chkAccountsSelectAll');
        const pagerHost = document.querySelector('#accountsPager');
        const importModal = document.querySelector('#accountImportModal');
        const importForm = document.querySelector('#accountImportForm');
        const importFile = document.querySelector('#accountImportFile');
        const importResult = document.querySelector('#accountImportResult');
        const importSubmit = document.querySelector('#submitAccountImport');
        const closeImportModal = document.querySelector('#closeAccountImportModal');
        const cancelImport = document.querySelector('#cancelAccountImport');

        if (!companyId || !tbody) return;

        const accountTypes = [
            "Asset",
            "Liabilities",
            "Expense",
            "Equity",
            "Profit & Loss",
            "Sales",
            "Cost of Sale",
            "Debtors",
            "Creditors"
        ];

        let rows = [];
        let prefixRules = [];
        let currentPage = 1;
        let pageSize = 10;
        let totalCount = 0;

        // ---------- load ----------
        async function load() {
            try {
                const result = await API.getAccounts(companyId, {
                    q: (searchInput?.value || "").trim(),
                    accountType: (typeFilter?.value || "").trim(),
                    page: currentPage,
                    pageSize
                });

                rows = (result.items || result.Items || []).map(normalizeAccount);
                totalCount = result.totalCount ?? result.TotalCount ?? 0;

                render();

                renderPager(pagerHost, currentPage, pageSize, totalCount, (nextPage) => {
                    currentPage = nextPage;
                    load();
                });
            } catch (err) {
                showAlert(alertHost, "danger", "Failed to load accounts: " + err.message);
                tbody.innerHTML = `
                    <tr>
                      <td colspan="6" class="px-4 py-4 text-center text-red-500">
                        Error loading accounts.
                      </td>
                    </tr>
                  `;
                if (pagerHost) pagerHost.innerHTML = "";
            }
        }

        async function loadPrefixRulesForAccounts() {
            if (prefixRules.length) return prefixRules;

            prefixRules = await API.getAccountPrefixRulesForAccounts(companyId);

            prefixRules = (prefixRules || []).map(r => ({
                prefix: r.prefix ?? r.Prefix ?? "",
                accountType: r.accountType ?? r.AccountType ?? ""
            }));

            return prefixRules;
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

        function openImportModal() {
            if (!importModal) return;
            importModal.classList.remove("hidden");
            importModal.classList.add("flex");
            if (importResult) {
                importResult.classList.add("hidden");
                importResult.innerHTML = "";
            }
            if (importFile) importFile.value = "";
        }

        function hideImportModal() {
            if (!importModal) return;
            importModal.classList.add("hidden");
            importModal.classList.remove("flex");
        }

        function renderImportResult(result) {
            if (!importResult) return;

            const imported = result.importedAccounts ?? result.ImportedAccounts ?? 0;
            const skipped = result.skippedAccounts ?? result.SkippedAccounts ?? 0;
            const skippedLines = result.skippedLines ?? result.SkippedLines ?? [];

            const details = skippedLines.slice(0, 12).map(line => {
                const lineNumber = line.lineNumber ?? line.LineNumber ?? "";
                const code = line.accountCode ?? line.AccountCode ?? "";
                const reason = line.reason ?? line.Reason ?? "";

                return `
                    <li class="flex gap-2">
                        <span class="shrink-0 font-mono text-gray-500 dark:text-neutral-500">#${escapeHtml(String(lineNumber))}</span>
                        <span class="shrink-0 font-mono">${escapeHtml(code || "-")}</span>
                        <span>${escapeHtml(reason)}</span>
                    </li>
                `;
            }).join("");

            importResult.innerHTML = `
                <div class="font-medium text-gray-900 dark:text-neutral-100">
                    Imported ${escapeHtml(String(imported))} account(s). Skipped ${escapeHtml(String(skipped))}.
                </div>
                ${details
                    ? `<ul class="mt-2 max-h-48 space-y-1 overflow-auto">${details}</ul>`
                    : ""}
                ${skippedLines.length > 12
                    ? `<div class="mt-2 text-gray-500 dark:text-neutral-500">Showing first 12 skipped rows.</div>`
                    : ""}
            `;
            importResult.classList.remove("hidden");
        }

        function getFilteredRowsWithIndex() {
            return rows.map((row, index) => ({ row, index }));
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

        function accountTypeOptions(selectedType) {
            const cleanSelectedType = (selectedType || "").trim();

            return `
                <option value="">Select account type</option>
                ${accountTypes.map(type => `
                    <option value="${escapeHtml(type)}" ${type === cleanSelectedType ? "selected" : ""}>
                        ${escapeHtml(type)}
                    </option>
                `).join("")}
            `;
        }

        function rowHtml(row, index) {
            if (row._mode === "edit" || row._mode === "new") {
                const isNew = row._mode === "new";

                const prefixOptions = prefixRules.map(rule => `
                        <option value="${escapeHtml(rule.prefix)}" ${row.prefix === rule.prefix ? "selected" : ""}>
                            ${escapeHtml(rule.prefix)} - ${escapeHtml(rule.accountType)}
                        </option>
                    `).join("");

                return `
                    <tr data-index="${index}" class="bg-neutral-900/10 dark:bg-neutral-800/60">
                      <td class="px-3 py-2">
                        <input type="checkbox"
                               class="h-4 w-4 rounded border-gray-300 dark:border-neutral-600 acc-select"
                               ${row._selected ? "checked" : ""} />
                      </td>

                      <td class="px-3 py-2">
                        ${isNew
                                    ? `
                                <select data-field="prefix"
                                        class="w-full rounded border border-gray-300 dark:border-neutral-700 bg-white dark:bg-neutral-900
                                               px-2 py-1 text-xs font-mono">
                                    <option value="">Select prefix...</option>
                                    ${prefixOptions}
                                </select>
                              `
                                    : `
                                <input data-field="accountCode"
                                       class="w-full rounded border border-gray-300 dark:border-neutral-700 bg-white dark:bg-neutral-900
                                              px-2 py-1 text-xs font-mono"
                                       value="${escapeHtml(row.accountCode || "")}" />
                              `
                                }
                      </td>

                      <td class="px-3 py-2">
                        <input data-field="name"
                               class="w-full rounded border border-gray-300 dark:border-neutral-700 bg-white dark:bg-neutral-900
                                      px-2 py-1 text-xs"
                               value="${escapeHtml(row.name || "")}" />
                      </td>

                      <td class="px-3 py-2">
                        ${isNew
                                    ? `
                                <span class="text-xs text-gray-500 dark:text-neutral-400">
                                    ${escapeHtml(
                                        prefixRules.find(r => r.prefix === row.prefix)?.accountType || "Type comes from prefix"
                                    )}
                                </span>
                              `
                                    : `
                                <select data-field="type"
                                        class="w-full rounded border border-gray-300 dark:border-neutral-700 bg-white dark:bg-neutral-900
                                               px-2 py-1 text-xs">
                                    ${accountTypeOptions(row.type)}
                                </select>
                              `
                                }
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
                <td class="px-3 py-2">
                  <a class="text-indigo-600 hover:underline"
                     href="/company/${companyId}/search?accountCode=${encodeURIComponent(row.accountCode)}">
                    ${escapeHtml(row.name)}
                  </a>
                </td>
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
            const prefix = (row.prefix || "").trim().toUpperCase();
            const name = (row.name || "").trim();
            const type = (row.type || "").trim();
            const isActive = row.isActive !== false;

            if (row._mode === "new" && !prefix) {
                showAlert(alertHost, "danger", "Prefix is required.");
                return;
            }

            if (row._mode !== "new" && !code) {
                showAlert(alertHost, "danger", "Account code is required.");
                return;
            }

            if (!name) {
                showAlert(alertHost, "danger", "Account name is required.");
                return;
            }

            if (row._mode !== "new" && !type) {
                showAlert(alertHost, "danger", "Account type is required.");
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
                    const created = await API.createAccountFromPrefix(companyId, {
                        prefix,
                        name,
                        isActive
                    });
                    const normalized = normalizeAccount(created || dto);
                    rows[index] = Object.assign(normalized, { _mode: "view", _selected: true });
                    showAlert(alertHost, "success", "Account created.");
                    const linkedPdCreated =
                        created?.linkedDepreciationAccountCreated ??
                        created?.LinkedDepreciationAccountCreated ??
                        false;
                    const linkedPdCode =
                        created?.linkedDepreciationAccountCode ??
                        created?.LinkedDepreciationAccountCode ??
                        "";

                    if (linkedPdCreated) {
                        showAlert(alertHost, "success", `Matching depreciation account ${linkedPdCode || "PD"} created.`);
                    }
                    await load();
                    return;
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
            btnAdd.addEventListener("click", async () => {
                try {
                    await loadPrefixRulesForAccounts();

                    if (!prefixRules.length) {
                        showAlert(alertHost, "danger", "No prefix rules found. Create prefix rules in Admin Console first.");
                        return;
                    }

                    rows.unshift({
                        accountCode: "",
                        prefix: "",
                        name: "",
                        type: "",
                        isActive: true,
                        createdAt: null,
                        _mode: "new",
                        _selected: false,
                        _orig: null
                    });

                    render();
                } catch (err) {
                    showAlert(alertHost, "danger", "Failed to load prefix rules: " + err.message);
                }
            });
        }

        if (btnDelete) {
            btnDelete.addEventListener("click", () => {
                deleteSelected();
            });
        }

        if (btnImport) {
            btnImport.addEventListener("click", openImportModal);
        }

        closeImportModal?.addEventListener("click", hideImportModal);
        cancelImport?.addEventListener("click", hideImportModal);

        importModal?.addEventListener("click", (e) => {
            if (e.target === importModal) hideImportModal();
        });

        importForm?.addEventListener("submit", async (e) => {
            e.preventDefault();

            const file = importFile?.files?.[0];
            if (!file) {
                showAlert(alertHost, "danger", "Choose a CSV or DBF file to import.");
                return;
            }

            try {
                if (importSubmit) {
                    importSubmit.disabled = true;
                    importSubmit.textContent = "Importing...";
                }

                const result = await API.importAccounts(companyId, file);
                renderImportResult(result || {});
                showAlert(alertHost, "success", "Account import finished.");
                currentPage = 1;
                await load();
            } catch (err) {
                showAlert(alertHost, "danger", "Failed to import accounts: " + err.message);
            } finally {
                if (importSubmit) {
                    importSubmit.disabled = false;
                    importSubmit.textContent = "Import";
                }
            }
        });

        if (searchInput) {
            searchInput?.addEventListener('input', debounce(() => {
                currentPage = 1;
                load();
            }, 200));
        }

        if (typeFilter) {
            typeFilter.addEventListener("change", () => {
                currentPage = 1;
                load();
            });
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

        tbody.addEventListener("change", (e) => {
            const tr = e.target.closest("tr[data-index]");
            if (!tr) return;

            const index = Number(tr.getAttribute("data-index"));
            if (Number.isNaN(index)) return;

            const field = e.target.getAttribute("data-field");
            if (!field) return;

            if (field === "prefix") {
                rows[index].prefix = e.target.value;

                const selectedRule = prefixRules.find(r => r.prefix === e.target.value);
                rows[index].type = selectedRule?.accountType || "";

                render();
                return;
            }

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

        const exactFilterHost = document.querySelector('#txExactFilterHost');
        const pageUrl = new URL(window.location.href);
        const focusTx0 = pageUrl.searchParams.get("focusTx");
        let focusTransactionNo = focusTx0 != null && focusTx0 !== ""
            ? Number(focusTx0)
            : null;
        if (focusTransactionNo != null && Number.isNaN(focusTransactionNo)) {
            focusTransactionNo = null;
        }

        const btnNew = document.querySelector('#btnTxNew');
        const btnDeleteSelected = document.querySelector('#btnTxDeleteSelected');
        const chkSelectAll = document.querySelector('#chkTxSelectAll');

        const linesBody = document.querySelector('#txLinesBody');
        const currentSummary = document.querySelector('#txCurrentSummary');
        const detailNo = document.querySelector('#txDetailNo');
        const detailDate = document.querySelector('#txDetailDate');
        const detailDesc = document.querySelector('#txDetailDescription');
        const btnEdit = document.querySelector('#btnTxEdit');
        const btnAddLine = document.querySelector('#btnTxAddLine');
        const btnSave = document.querySelector('#btnTxSave');
        const btnCancel = document.querySelector('#btnTxCancel');
        const totalDrEl = document.querySelector('#txTotalDebit');
        const totalCrEl = document.querySelector('#txTotalCredit');

        const txMasterPager = document.querySelector('#txMasterPager');
        const txLinesPager = document.querySelector('#txLinesPager');

        const quickAccountModal = document.querySelector('#txQuickAccountModal');
        const btnCloseQuickAccountModal = document.querySelector('#btnCloseTxQuickAccountModal');
        const btnCancelQuickAccount = document.querySelector('#btnCancelTxQuickAccount');
        const btnSaveQuickAccount = document.querySelector('#btnSaveTxQuickAccount');

        const quickAccountPrefix = document.querySelector("#quickAccountPrefix");
        const quickAccountName = document.querySelector("#quickAccountName");
        const quickAccountTypePreview = document.querySelector("#quickAccountTypePreview");

        let transactions = [];
        let accounts = [];
        let selectedIndex = null;
        let editingTxIndex = null;
        let txPage = 1;
        let txPageSize = 10;
        let txTotalCount = 0;
        let detailPage = 1;
        let detailPageSize = 10;
        let pendingAccountLineIndex = null;
        let quickAccountPrefixRules = [];

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

                const result = await API.getTransactions(companyId, {
                    page: txPage,
                    pageSize: txPageSize,
                    q: (searchInput?.value || "").trim(),
                    from: (fromInput?.value || "").trim(),
                    to: (toInput?.value || "").trim()
                });

                transactions = (result.items || result.Items || []).map(normalizeTx);
                txTotalCount = result.totalCount ?? result.TotalCount ?? 0;
                detailPage = 1;

                const visible = filteredIndexes();

                if (!visible.length) {
                    selectedIndex = null;
                } else {
                    const focused = applyFocusTransaction();

                    if (!focused && (selectedIndex == null || !transactions[selectedIndex])) {
                        selectedIndex = visible[0].index;
                    }
                }

                renderMaster();
                renderDetail(selectedIndex);

                renderPager(txMasterPager, txPage, txPageSize, txTotalCount, (nextPage) => {
                    txPage = nextPage;
                    selectedIndex = null;
                    loadAll();
                });
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
                if (txMasterPager) txMasterPager.innerHTML = "";
            }
        }

        function renderExactTransactionFilter() {
            if (!exactFilterHost) return;

            if (focusTransactionNo == null) {
                exactFilterHost.innerHTML = "";
                return;
            }

            exactFilterHost.innerHTML = `
              <span class="inline-flex items-center gap-2 rounded-full border border-indigo-300/40 bg-indigo-50
                           dark:bg-indigo-950/40 px-3 py-1 text-xs text-indigo-700 dark:text-indigo-200">
                Focused Transaction No.: ${escapeHtml(String(focusTransactionNo))}
                <button id="btnClearExactTransactionFilter"
                        type="button"
                        class="font-semibold hover:opacity-80"
                        aria-label="Clear focused transaction">
                  ×
                </button>
              </span>
            `;

            const btn = document.querySelector('#btnClearExactTransactionFilter');
            btn?.addEventListener('click', () => {
                focusTransactionNo = null;

                const next = new URL(window.location.href);
                next.searchParams.delete("focusTx");
                window.history.replaceState({}, "", next.toString());

                renderExactTransactionFilter();
                loadAll();
            });
        }

        function normalizeTx(tx) {
            const entries = Array.isArray(tx.Entries ?? tx.entries)
                ? (tx.Entries ?? tx.entries).map(normalizeEntry)
                : [];

            return {
                transactionNo: tx.TransactionNo ?? tx.transactionNo ?? null,
                txnDate: tx.TxnDate ?? tx.txnDate ?? null,
                description: tx.Description ?? tx.description ?? "",
                entries,
                hasError: Boolean(tx.HasError ?? tx.hasError ?? entries.some(e => e.hasError)),
                _mode: "view",
                _selected: false,
                _orig: null
            };
        }

        function normalizeEntry(e) {
            const debit = Number(e.Debit ?? e.debit ?? 0) || 0;
            const credit = Number(e.Credit ?? e.credit ?? 0) || 0;

            return {
                accountCode: (e.AccountCode ?? e.accountCode ?? "").toString(),
                debit,
                credit,
                memo: (e.Memo ?? e.memo ?? "").toString(),
                hasError: Boolean(e.HasError ?? e.hasError ?? false)
            };
        }

        function applyFocusTransaction() {
            if (focusTransactionNo == null) return false;

            const matchIndex = transactions.findIndex(t =>
                Number(t.transactionNo) === Number(focusTransactionNo)
            );

            if (matchIndex === -1) return false;

            selectedIndex = matchIndex;
            detailPage = 1;

            requestAnimationFrame(() => {
                const row = tbody?.querySelector(`tr[data-index="${matchIndex}"]`);
                row?.scrollIntoView({ behavior: "smooth", block: "center" });
            });

            return true;
        }

        function filteredIndexes() {
            return transactions.map((row, index) => ({ row, index }));
        }

        async function loadAccounts() {
            try {
                const result = await API.getAccounts(companyId, { page: 1, pageSize: 500 });
                const data = result.items || result.Items || [];

                accounts = data.map(acc => ({
                    code: acc.accountCode ?? acc.AccountCode ?? "",
                    name: acc.name ?? acc.Name ?? ""
                }))
                .sort((a, b) =>
                        String(a.code).localeCompare(String(b.code), undefined, { numeric: true, sensitivity: "base" })
                );
            } catch (err) {
                console.warn("Failed to load accounts for dropdown", err);
            }
        }

        async function loadQuickAccountPrefixRules() {
            if (quickAccountPrefixRules.length) return quickAccountPrefixRules;

            quickAccountPrefixRules = await API.getAccountPrefixRulesForAccounts(companyId);

            quickAccountPrefixRules = (quickAccountPrefixRules || []).map(r => ({
                prefix: r.prefix ?? r.Prefix ?? "",
                accountType: r.accountType ?? r.AccountType ?? ""
            }));

            if (quickAccountPrefix) {
                quickAccountPrefix.innerHTML = `
            <option value="">Select prefix...</option>
            ${quickAccountPrefixRules.map(rule => `
                <option value="${escapeHtml(rule.prefix)}">
                    ${escapeHtml(rule.prefix)} - ${escapeHtml(rule.accountType)}
                </option>
            `).join("")}
        `;
            }

            return quickAccountPrefixRules;
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
                if (txLinesPager) txLinesPager.innerHTML = "";
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
            const hasError = Boolean(row.hasError || row.entries.some(line => line.hasError));

            const baseClasses =
                "hover:bg-gray-50 dark:hover:bg-neutral-800/60 cursor-pointer";
            const selectedClass = isSelected ? " bg-indigo-50/60 dark:bg-indigo-900/40" : "";
            const editingClass = isEditing ? " border-l-2 border-indigo-500" : "";
            const errorClass = hasError ? " border-l-2 border-red-500" : "";
            const statusHtml = hasError
                ? `<span class="inline-flex rounded-full bg-red-100 px-2 py-0.5 text-[11px] font-medium text-red-700 dark:bg-red-950/50 dark:text-red-200">Error</span>`
                : `<span class="text-xs text-gray-400 dark:text-neutral-500"></span>`;

            return `
              <tr data-index="${index}" class="${baseClasses}${selectedClass}${editingClass}${errorClass}">
                <td class="px-3 py-2">
                  <input type="checkbox"
                         class="h-4 w-4 rounded border-gray-300 dark:border-neutral-600 tx-select"
                         ${row._selected ? "checked" : ""} />
                </td>
                <td class="px-3 py-2 font-mono text-xs">
                  ${row.transactionNo != null
                                    ? `<a class="text-indigo-600 hover:underline"
                          href="/company/${companyId}/search?transactionNo=${encodeURIComponent(String(row.transactionNo))}">
                         ${escapeHtml(String(row.transactionNo))}
                       </a>`
                                    : "<span class='text-gray-400'>New</span>"
                  }
                </td>
                <td class="px-3 py-2 text-xs text-gray-700 dark:text-neutral-200">
                  ${escapeHtml(formatDate(row.txnDate))}
                </td>
                <td class="px-3 py-2">
                  ${escapeHtml(row.description || "")}
                </td>
                <td class="px-3 py-2">
                  ${statusHtml}
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
                if (btnEdit) btnEdit.disabled = true;
                if (btnAddLine) btnAddLine.disabled = true;
                if (btnSave) btnSave.disabled = true;
                if (btnCancel) btnCancel.disabled = true;
                if (linesBody) {
                    linesBody.innerHTML = `
              <tr>
                <td colspan="7" class="px-4 py-4 text-center text-gray-500 dark:text-neutral-400">
                  No transaction selected.
                </td>
              </tr>
            `;
                }
                if (txLinesPager) txLinesPager.innerHTML = "";
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
            if (btnEdit) btnEdit.disabled = isEditing;
            if (btnAddLine) btnAddLine.disabled = !isEditing;
            if (btnSave) btnSave.disabled = !isEditing;
            if (btnCancel) btnCancel.disabled = !isEditing;

            if (!linesBody) return;

            if (!tx.entries.length && !isEditing) {
                linesBody.innerHTML = `
            <tr>
              <td colspan="7" class="px-4 py-4 text-center text-gray-500 dark:text-neutral-400">
                This transaction has no lines.
              </td>
            </tr>
          `;
                if (txLinesPager) txLinesPager.innerHTML = "";
            } else {
                const totalLineCount = tx.entries.length;
                const totalDetailPages = Math.max(1, Math.ceil(totalLineCount / detailPageSize));
                detailPage = Math.min(Math.max(1, detailPage), totalDetailPages);

                const start = (detailPage - 1) * detailPageSize;
                const visibleLines = tx.entries.slice(start, start + detailPageSize);

                linesBody.innerHTML = visibleLines
                    .map((line, idx) => lineRowHtml(line, start + idx, isEditing))
                    .join("");

                renderPager(txLinesPager, detailPage, detailPageSize, totalLineCount, (nextPage) => {
                    detailPage = nextPage;
                    renderDetail(selectedIndex);
                });
            }

            const totals = calcTotals(tx.entries);
            if (totalDrEl) totalDrEl.textContent = totals.dr.toFixed(2);
            if (totalCrEl) totalCrEl.textContent = totals.cr.toFixed(2);

            renderMaster();
        }

        function lineRowHtml(line, idx, editable) {
            const debit = Number(line.debit) || 0;
            const credit = Number(line.credit) || 0;
            const hasError = Boolean(line.hasError);
            const rowClass = hasError ? "bg-red-50/60 dark:bg-red-950/20" : "";
            const statusHtml = hasError
                ? `<span class="inline-flex rounded-full bg-red-100 px-2 py-0.5 text-[11px] font-medium text-red-700 dark:bg-red-950/50 dark:text-red-200">Error</span>`
                : `<span class="text-xs text-gray-400 dark:text-neutral-500"></span>`;

            const options = accounts.map(a => `
              <option value="${escapeHtml(a.code)}"
                      ${a.code === (line.accountCode || "") ? "selected" : ""}>
                ${escapeHtml(a.code)} — ${escapeHtml(a.name || "")}
              </option>
            `).join("");

                    if (!editable) {
                        return `
                    <tr class="${rowClass}">
                      <td class="px-4 py-2 text-xs text-gray-500 dark:text-neutral-400">${idx + 1}</td>
                      <td class="px-4 py-2">
                        <div class="font-mono text-xs">
                        <a href="/company/${companyId}/search?q=${encodeURIComponent(line.accountCode || "")}" class="text-indigo-600 hover:underline">
                            ${escapeHtml(line.accountCode || "")}
                        </a></div>
                      </td>
                      <td class="px-4 py-2 text-right font-mono text-xs">${escapeHtml(debit.toFixed(2))}</td>
                      <td class="px-4 py-2 text-left font-mono text-xs">${escapeHtml(credit.toFixed(2))}</td>
                      <td class="px-4 py-2 text-xs text-gray-600 dark:text-neutral-300">${escapeHtml(line.memo || "")}</td>
                      <td class="px-4 py-2">${statusHtml}</td>
                      <td class="px-4 py-2 text-right"></td>
                    </tr>
                  `;
                    }

                    return `
                  <tr data-line-index="${idx}" class="hover:bg-gray-50 dark:hover:bg-neutral-800/60 ${rowClass}">
                    <td class="px-4 py-2 text-xs text-gray-500 dark:text-neutral-400">${idx + 1}</td>
                    <td class="px-4 py-2">
                      <select data-field="accountCode"
                                class="w-full rounded border border-gray-300 dark:border-neutral-700
                                       bg-white dark:bg-neutral-900 px-2 py-1 text-xs">
                          <option value="">Select account…</option>
                          <option value="__add_new_account__">+ Add new account...</option>
                          ${options}
                        </select>
                    </td>
                    <td class="px-4 py-2 text-right">
                      <input data-field="debit"
                             type="number"
                             step="0.01"
                             class="w-24 rounded border border-gray-300 dark:border-neutral-700 bg-white dark:bg-neutral-900
                                    px-2 py-1 text-xs text-right font-mono"
                             value="${escapeHtml(debit.toString())}" />
                    </td>
                    <td class="px-4 py-2 text-right">
                      <input data-field="credit"
                             type="number"
                             step="0.01"
                             class="w-24 rounded border border-gray-300 dark:border-neutral-700 bg-white dark:bg-neutral-900
                                    px-2 py-1 text-xs text-left font-mono"
                             value="${escapeHtml(credit.toString())}" />
                    </td>
                    <td class="px-4 py-2">
                      <input data-field="memo"
                             class="w-full rounded border border-gray-300 dark:border-neutral-700 bg-white dark:bg-neutral-900
                                    px-2 py-1 text-xs"
                             value="${escapeHtml(line.memo || "")}" />
                    </td>
                    <td class="px-4 py-2">${statusHtml}</td>
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
            return entries.reduce((acc, line) => {
                acc.dr += Number(line.debit) || 0;
                acc.cr += Number(line.credit) || 0;
                return acc;
            }, { dr: 0, cr: 0 });
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
            detailPage = 1;
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
                    debit: Number(line.debit) || 0,
                    credit: Number(line.credit) || 0,
                    memo: line.memo || null
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
            return max > 0 ? max + 1 : 1;
        }

        async function openQuickAccountModal(lineIndex) {
            pendingAccountLineIndex = lineIndex;

            try {
                await loadQuickAccountPrefixRules();

                if (!quickAccountPrefixRules.length) {
                    alert("No prefix rules found. Please ask an admin to create prefix rules first.");
                    pendingAccountLineIndex = null;
                    return;
                }

                if (quickAccountPrefix) quickAccountPrefix.value = "";
                if (quickAccountName) quickAccountName.value = "";
                if (quickAccountTypePreview) {
                    quickAccountTypePreview.textContent = "Account type comes from selected prefix.";
                }

                quickAccountModal?.classList.remove("hidden");
                quickAccountModal?.classList.add("flex");

                setTimeout(() => quickAccountPrefix?.focus(), 0);
            } catch (err) {
                pendingAccountLineIndex = null;
                alert(err.message || "Failed to load prefix rules.");
            }
        }

        function closeQuickAccountModal() {
            quickAccountModal?.classList.add("hidden");
            quickAccountModal?.classList.remove("flex");
            pendingAccountLineIndex = null;
        }

        async function saveQuickAccountFromModal() {
            const prefix = (quickAccountPrefix?.value || "").trim().toUpperCase();
            const name = (quickAccountName?.value || "").trim();

            if (!prefix) {
                alert("Prefix is required.");
                quickAccountPrefix?.focus();
                return;
            }

            if (!name) {
                alert("Account name is required.");
                quickAccountName?.focus();
                return;
            }

            try {
                const created = await API.createAccountFromPrefix(companyId, {
                    prefix,
                    name,
                    isActive: true
                });

                await loadAccounts();

                if (
                    selectedIndex != null &&
                    pendingAccountLineIndex != null &&
                    created?.accountCode
                ) {
                    const tx = transactions[selectedIndex];
                    const line = tx?.entries[pendingAccountLineIndex];

                    if (line) {
                        line.accountCode = created.accountCode;
                    }
                }

                closeQuickAccountModal();
                renderDetail(selectedIndex);

                showAlert(alertHost, "success", "Account created.");
                const linkedPdCreated =
                    created?.linkedDepreciationAccountCreated ??
                    created?.LinkedDepreciationAccountCreated ??
                    false;
                const linkedPdCode =
                    created?.linkedDepreciationAccountCode ??
                    created?.LinkedDepreciationAccountCode ??
                    "";

                if (linkedPdCreated) {
                    showAlert(alertHost, "success", `Matching depreciation account ${linkedPdCode || "PD"} created.`);
                }
            } catch (err) {
                alert(err.message || "Failed to create account.");
            }
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
                detailPage = 1;
                startEdit(0, true);
            });
        }

        if (btnDeleteSelected) {
            btnDeleteSelected.addEventListener("click", () => {
                deleteSelected();
            });
        }

        searchInput?.addEventListener('input', debounce(() => {
            txPage = 1;
            selectedIndex = null;
            loadAll();
        }, 200));

        fromInput?.addEventListener('change', () => {
            txPage = 1;
            selectedIndex = null;
            loadAll();
        });

        toInput?.addEventListener('change', () => {
            txPage = 1;
            selectedIndex = null;
            loadAll();
        });

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
            detailPage = 1;
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
        if (btnEdit) {
            btnEdit.addEventListener("click", () => {
                if (selectedIndex == null) return;
                startEdit(selectedIndex, false);
            });
        }
        if (btnAddLine) {
            btnAddLine.addEventListener("click", () => {
                if (selectedIndex == null) return;
                const tx = transactions[selectedIndex];
                if (!tx) return;
                tx.entries.push(normalizeEntry({}));
                const totalDetailPages = Math.max(1, Math.ceil(tx.entries.length / detailPageSize));
                detailPage = totalDetailPages;
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
                    const newValue = e.target.value;

                    if (newValue === "__add_new_account__") {
                        e.target.value = line.accountCode || "";
                        openQuickAccountModal(idx);
                        return;
                    }

                    line.accountCode = newValue;
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
                        line.debit = amount;
                    } else {
                        line.credit = amount;
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
                    const totalDetailPages = Math.max(1, Math.ceil(tx.entries.length / detailPageSize));
                    detailPage = Math.min(detailPage, totalDetailPages);
                    renderDetail(selectedIndex);
                }
            });
        }

        btnCloseQuickAccountModal?.addEventListener("click", closeQuickAccountModal);
        btnCancelQuickAccount?.addEventListener("click", closeQuickAccountModal);
        btnSaveQuickAccount?.addEventListener("click", saveQuickAccountFromModal);

        quickAccountModal?.addEventListener("click", (e) => {
            if (e.target === quickAccountModal) {
                closeQuickAccountModal();
            }
        });

        quickAccountPrefix?.addEventListener("change", () => {
            const selectedPrefix = quickAccountPrefix.value;

            const selectedRule = quickAccountPrefixRules.find(r => r.prefix === selectedPrefix);

            if (quickAccountTypePreview) {
                quickAccountTypePreview.textContent = selectedRule
                    ? `Account type: ${selectedRule.accountType}`
                    : "Account type comes from selected prefix.";
            }
        });

        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape" && quickAccountModal && !quickAccountModal.classList.contains("hidden")) {
                closeQuickAccountModal();
            }
        });

        (async () => {
            renderExactTransactionFilter();
            await loadAccounts();
            await loadAll();
        })();
    }  

    // ---------- LedgerSearch ----------
    function initLedgerSearch() {
        const info = window.__page__ || {};
        const companyId = info.companyId;
        if (!companyId) return;

        const input = document.querySelector('#ledgerSearchInput');
        const btnSearch = document.querySelector('#btnLedgerSearch');
        const btnClear = document.querySelector('#btnLedgerClear');
        const tbody = document.querySelector('#ledgerResultsBody');
        const alertHost = document.querySelector('#ledgerAlertHost');

        const url = new URL(window.location.href);
        const q0 = url.searchParams.get("q") || "";
        const accountCode0 = url.searchParams.get("accountCode") || "";
        const transactionNo0 = url.searchParams.get("transactionNo");
        const txNoParsed = transactionNo0 != null && transactionNo0 !== ""
            ? Number(transactionNo0)
            : null;
        const tfoot = document.querySelector('#ledgerTotalsBody');

        if (input) input.value = q0;

        async function runSearch({ q, accountCode, transactionNo } = {}) {
            try {
                if (alertHost) alertHost.innerHTML = "";

                const results = await API.ledgerSearch(companyId, {
                    q: q || "",
                    accountCode: accountCode || "",
                    transactionNo: transactionNo ?? null,
                    page: 1,
                    pageSize: 200
                });

                renderResults(results || []);
            } catch (err) {
                showAlert(alertHost, "danger", "Search failed: " + (err.message || err));
                renderResults([]);
            }
        }

        function renderResults(results) {
            if (!tbody) return;

            if (!results.length) {
                tbody.innerHTML = `
                  <tr>
                    <td class="px-4 py-4 text-sm text-gray-500 dark:text-neutral-400" colspan="7">No results.</td>
                  </tr>`;
                if (tfoot) tfoot.innerHTML = "";
                return;
            }

            const money = (n) => {
                const v = Number(n || 0);
                return v ? v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : "";
            };

            const moneyZero = (n) => {
                const v = Number(n || 0);
                return v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            };

            const totalDebit = results.reduce((sum, r) => sum + Number(r.debit || 0), 0);
            const totalCredit = results.reduce((sum, r) => sum + Number(r.credit || 0), 0);

            const net = totalDebit - totalCredit;
            const netDebit = net > 0 ? net : 0;
            const netCredit = net < 0 ? Math.abs(net) : 0;

            tbody.innerHTML = results.map(r => {
                const txNo = r.transactionNo;
                const accCodeRaw = r.accountCode ?? "";
                const accCode = escapeHtml(accCodeRaw);
                const lineRaw = r.lineDescription ?? "";
                const txDescRaw = r.transactionDescription ?? "";
                const description = escapeHtml(txDescRaw || "-");
                const memo = escapeHtml(lineRaw || "-");
                const debit = money(r.debit);
                const credit = money(r.credit);
                const date = new Date(r.date).toLocaleDateString("en-CA");

                const accHref = `/company/${companyId}/search?accountCode=${encodeURIComponent(accCodeRaw)}`;
                const txHref = `/company/${companyId}/transactions?focusTx=${encodeURIComponent(String(txNo))}`;

                return `
                    <tr class="border-b border-gray-100 text-gray-900 hover:bg-gray-50 dark:border-neutral-800 dark:text-neutral-100 dark:hover:bg-neutral-800/60">
                        <td class="px-4 py-3 text-xs">
                            <a class="text-indigo-600 hover:text-indigo-500 dark:text-indigo-300 dark:hover:text-indigo-200 underline underline-offset-4"
                               href="${txHref}">${escapeHtml(String(txNo))}</a>
                        </td>
                        <td class="px-4 py-3 text-xs">${date}</td>
                        <td class="px-4 py-3 text-xs">
                            <a class="text-indigo-600 hover:text-indigo-500 dark:text-indigo-300 dark:hover:text-indigo-200 underline underline-offset-4"
                               href="${accHref}">${accCode}</a>
                        </td>
                        <td class="px-4 py-3 text-xs text-gray-700 dark:text-neutral-300">${description}</td>
                        <td class="px-4 py-3 text-xs text-gray-700 dark:text-neutral-300">${memo}</td>
                        <td class="px-4 py-3 text-xs text-right font-mono">${escapeHtml(debit)}</td>
                        <td class="px-4 py-3 text-xs text-left font-mono">${escapeHtml(credit)}</td>
                    </tr>`;
                }).join("");

            tfoot.innerHTML = `
                <tr class="font-semibold">
                    <td class="px-4 py-3 text-right" colspan="5">Total</td>
                    <td class="px-4 py-3 text-right font-mono">${escapeHtml(moneyZero(totalDebit))}</td>
                    <td class="px-4 py-3 text-left font-mono">${escapeHtml(moneyZero(totalCredit))}</td>
                </tr>
                <tr class="font-semibold border-t border-gray-200 dark:border-neutral-800">
                    <td class="px-4 py-3 text-right" colspan="5">Final Balance</td>
                    <td class="px-4 py-3 text-right font-mono">${escapeHtml(moneyZero(netDebit))}</td>
                    <td class="px-4 py-3 text-left font-mono">${escapeHtml(moneyZero(netCredit))}</td>
                </tr>`;
        }

        // Button actions
        if (btnSearch) {
            btnSearch.addEventListener("click", () => {
                const q = (input?.value || "").trim();
                // free text search takes over; clear drilldown params
                const next = new URL(window.location.href);
                next.searchParams.delete("accountCode");
                next.searchParams.delete("transactionNo");
                if (q) next.searchParams.set("q", q);
                else next.searchParams.delete("q");
                window.history.replaceState({}, "", next.toString());
                runSearch({ q });
            });
        }
        if (input) {
            input.addEventListener("keydown", (e) => {
                if (e.key === "Enter") {
                    e.preventDefault();
                    btnSearch?.click();
                }
            });
        }
        if (btnClear) {
            btnClear.addEventListener("click", () => {
                if (input) input.value = "";
                const next = new URL(window.location.href);
                next.search = "";
                window.history.replaceState({}, "", next.toString());
                renderResults([]);
            });
        }

        // Auto-run on load (drilldown works immediately)
        if (txNoParsed != null && !Number.isNaN(txNoParsed)) {
            runSearch({ transactionNo: txNoParsed });
        } else if (accountCode0) {
            runSearch({ accountCode: accountCode0 });
        } else if (q0) {
            runSearch({ q: q0 });
        }
    }

    // ---------- Admin Page ----------
    function initAdminIndex() {
        initAdminLogs();
        initAdminPrefixRules();
        initAdminUsers();
    }

    function initAdminLogs() {
        const tbody = document.querySelector("#adminLogsTableBody");
        const pagerHost = document.querySelector("#adminLogsPager");
        const searchInput = document.querySelector("#adminLogsSearch");
        const levelSelect = document.querySelector("#adminLogsLevel");
        const eventTypeInput = document.querySelector("#adminLogsEventType");
        const refreshBtn = document.querySelector("#btnRefreshLogs");

        if (!tbody) return;

        let currentPage = 1;
        const pageSize = 50;
        let searchTimer = null;

        async function loadLogs() {
            try {
                tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="px-4 py-6 text-center text-gray-500 dark:text-neutral-400">
                        Loading logs...
                    </td>
                </tr>
            `;

                const result = await API.getLogs({
                    q: (searchInput?.value || "").trim(),
                    level: (levelSelect?.value || "").trim(),
                    eventType: (eventTypeInput?.value || "").trim(),
                    page: currentPage,
                    pageSize
                });

                const items = result.items || result.Items || [];
                const totalCount = result.totalCount ?? result.TotalCount ?? 0;

                renderLogs(items);
                renderPager(pagerHost, currentPage, pageSize, totalCount, (nextPage) => {
                    currentPage = nextPage;
                    loadLogs();
                });
            } catch (err) {
                console.error("Failed to load admin logs", err);

                tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="px-4 py-6 text-center text-red-500">
                        Failed to load logs: ${escapeHtml(err.message || "Unknown error")}
                    </td>
                </tr>
            `;

                if (pagerHost) pagerHost.innerHTML = "";
            }
        }

        function renderLogs(items) {
            if (!items.length) {
                tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="px-4 py-6 text-center text-gray-500 dark:text-neutral-400">
                        No logs found.
                    </td>
                </tr>
            `;
                return;
            }

            tbody.innerHTML = items.map(log => {
                const tsUtc = log.tsUtc || log.TsUtc || "";
                const level = log.level || log.Level || "";
                const eventType = log.eventType || log.EventType || "";
                const logCode = log.logCode || log.LogCode || "";
                const logMessage = log.logMessage || log.LogMessage || "";
                const sourceFile = log.sourceFile || log.SourceFile || "";
                const sourceFunction = log.sourceFunction || log.SourceFunction || "";

                const source = [sourceFile, sourceFunction]
                    .filter(Boolean)
                    .join(" / ");

                return `
                <tr class="hover:bg-gray-50 dark:hover:bg-neutral-800/60">
                    <td class="px-3 py-3 whitespace-nowrap text-gray-500 dark:text-neutral-400">
                        ${escapeHtml(formatDateTime(tsUtc))}
                    </td>
                    <td class="px-3 py-3 whitespace-nowrap">
                        ${renderLogLevelBadge(level)}
                    </td>
                    <td class="px-3 py-3 whitespace-nowrap">
                        ${escapeHtml(eventType)}
                    </td>
                    <td class="px-3 py-3 whitespace-nowrap font-mono text-[11px]">
                        ${escapeHtml(logCode)}
                    </td>
                    <td class="px-3 py-3">
                        ${escapeHtml(logMessage)}
                    </td>
                    <td class="px-3 py-3 whitespace-nowrap text-gray-500 dark:text-neutral-400">
                        ${escapeHtml(source)}
                    </td>
                </tr>
            `;
            }).join("");
        }

        function reloadFromFirstPage() {
            currentPage = 1;
            loadLogs();
        }

        refreshBtn?.addEventListener("click", reloadFromFirstPage);

        levelSelect?.addEventListener("change", reloadFromFirstPage);

        searchInput?.addEventListener("input", () => {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(reloadFromFirstPage, 300);
        });

        eventTypeInput?.addEventListener("input", () => {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(reloadFromFirstPage, 300);
        });

        loadLogs();
    }

    function renderLogLevelBadge(level) {
        const normalized = (level || "").toUpperCase();

        let cls = "bg-gray-100 text-gray-700 dark:bg-neutral-800 dark:text-neutral-200";

        if (normalized === "ERROR") {
            cls = "bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300";
        } else if (normalized === "WARN" || normalized === "WARNING") {
            cls = "bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-300";
        } else if (normalized === "INFO") {
            cls = "bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300";
        }

        return `
        <span class="inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium ${cls}">
            ${escapeHtml(level || "-")}
        </span>
    `;
    }

    function formatDateTime(value) {
        if (!value) return "";

        const d = new Date(value);

        if (Number.isNaN(d.getTime())) {
            return value;
        }

        return d.toLocaleString();
    }

    function initAdminPrefixRules() {
        const tbody = document.querySelector("#prefixRulesTableBody");
        const addBtn = document.querySelector("#btnAddPrefixRule");

        const modal = document.querySelector("#prefixRuleModal");
        const modalTitle = document.querySelector("#prefixRuleModalTitle");
        const form = document.querySelector("#prefixRuleForm");
        const modeInput = document.querySelector("#prefixRuleMode");
        const prefixInput = document.querySelector("#prefixRulePrefix");
        const accountTypeSelect = document.querySelector("#prefixRuleAccountType");
        const errorBox = document.querySelector("#prefixRuleError");
        const saveBtn = document.querySelector("#btnSavePrefixRule");
        const closeBtn = document.querySelector("#btnClosePrefixRuleModal");
        const cancelBtn = document.querySelector("#btnCancelPrefixRuleModal");

        let accountTypes = [];
        let editingPrefix = "";

        if (!tbody) return;

        async function loadAccountTypes() {
            if (accountTypes.length) return accountTypes;

            accountTypes = await API.getAccountTypes();

            accountTypeSelect.innerHTML = `
            <option value="">Select account type</option>
            ${(accountTypes || []).map(type => `
                <option value="${escapeHtml(type)}">${escapeHtml(type)}</option>
            `).join("")}
        `;

            return accountTypes;
        }

        function showPrefixRuleError(message) {
            if (!errorBox) return;

            errorBox.textContent = message || "";
            errorBox.classList.toggle("hidden", !message);
        }

        function openPrefixRuleModal(mode, rule = null) {
            showPrefixRuleError("");

            const isEdit = mode === "edit";
            editingPrefix = isEdit ? (rule?.prefix || "") : "";

            modeInput.value = mode;
            modalTitle.textContent = isEdit ? "Edit Prefix Rule" : "New Prefix Rule";

            prefixInput.value = isEdit ? (rule?.prefix || "") : "";
            prefixInput.disabled = isEdit;

            accountTypeSelect.value = isEdit ? (rule?.accountType || "") : "";

            saveBtn.textContent = isEdit ? "Save Changes" : "Create Rule";

            modal.classList.remove("hidden");
            modal.classList.add("flex");

            setTimeout(() => {
                if (isEdit) {
                    accountTypeSelect.focus();
                } else {
                    prefixInput.focus();
                }
            }, 0);
        }

        function closePrefixRuleModal() {
            modal.classList.add("hidden");
            modal.classList.remove("flex");

            form.reset();
            prefixInput.disabled = false;
            editingPrefix = "";
            showPrefixRuleError("");
        }

        async function loadPrefixRules() {
            try {
                tbody.innerHTML = `
                <tr>
                    <td colspan="3" class="px-4 py-6 text-center text-gray-500 dark:text-neutral-400">
                        Loading prefix rules...
                    </td>
                </tr>
            `;

                const rules = await API.getAccountPrefixRules();
                renderPrefixRules(rules || []);
            } catch (err) {
                console.error("Failed to load prefix rules", err);

                tbody.innerHTML = `
                <tr>
                    <td colspan="3" class="px-4 py-6 text-center text-red-500">
                        Failed to load prefix rules: ${escapeHtml(err.message || "Unknown error")}
                    </td>
                </tr>
            `;
            }
        }

        function renderPrefixRules(rules) {
            if (!rules.length) {
                tbody.innerHTML = `
                <tr>
                    <td colspan="3" class="px-4 py-6 text-center text-gray-500 dark:text-neutral-400">
                        No prefix rules found.
                    </td>
                </tr>
            `;
                return;
            }

            tbody.innerHTML = rules.map(rule => {
                const prefix = rule.prefix || rule.Prefix || "";
                const accountType = rule.accountType || rule.AccountType || "";

                return `
                <tr class="hover:bg-gray-50 dark:hover:bg-neutral-800/60">
                    <td class="px-4 py-3 font-mono font-semibold">
                        ${escapeHtml(prefix)}
                    </td>
                    <td class="px-4 py-3">
                        ${escapeHtml(accountType)}
                    </td>
                    <td class="px-4 py-3 text-right whitespace-nowrap">
                        <button type="button"
                                class="btn-edit-prefix-rule inline-flex items-center rounded-lg border border-gray-200 dark:border-neutral-700
                                       px-3 py-1.5 text-xs text-gray-700 dark:text-neutral-100
                                       hover:bg-gray-100 dark:hover:bg-neutral-800"
                                data-prefix="${escapeHtml(prefix)}"
                                data-account-type="${escapeHtml(accountType)}">
                            <i class="fa-solid fa-pen mr-1"></i>
                            Edit
                        </button>

                        <button type="button"
                                class="btn-delete-prefix-rule ml-2 inline-flex items-center rounded-lg border border-red-200 dark:border-red-900/60
                                       px-3 py-1.5 text-xs text-red-600 dark:text-red-300
                                       hover:bg-red-50 dark:hover:bg-red-900/20"
                                data-prefix="${escapeHtml(prefix)}">
                            <i class="fa-solid fa-trash mr-1"></i>
                            Delete
                        </button>
                    </td>
                </tr>
            `;
            }).join("");
        }

        addBtn?.addEventListener("click", async () => {
            try {
                await loadAccountTypes();
                openPrefixRuleModal("create");
            } catch (err) {
                alert(err.message || "Failed to load account types.");
            }
        });

        closeBtn?.addEventListener("click", closePrefixRuleModal);
        cancelBtn?.addEventListener("click", closePrefixRuleModal);

        modal?.addEventListener("click", (e) => {
            if (e.target === modal) {
                closePrefixRuleModal();
            }
        });

        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape" && modal && !modal.classList.contains("hidden")) {
                closePrefixRuleModal();
            }
        });

        form?.addEventListener("submit", async (e) => {
            e.preventDefault();

            const mode = modeInput.value;
            const cleanPrefix = prefixInput.value.trim().toUpperCase();
            const cleanAccountType = accountTypeSelect.value.trim();

            if (!cleanPrefix) {
                showPrefixRuleError("Prefix is required.");
                prefixInput.focus();
                return;
            }

            if (!cleanAccountType) {
                showPrefixRuleError("Account type is required.");
                accountTypeSelect.focus();
                return;
            }

            saveBtn.disabled = true;
            showPrefixRuleError("");

            try {
                if (mode === "edit") {
                    await API.updateAccountPrefixRule(editingPrefix, {
                        prefix: editingPrefix,
                        accountType: cleanAccountType
                    });
                } else {
                    await API.createAccountPrefixRule({
                        prefix: cleanPrefix,
                        accountType: cleanAccountType
                    });
                }

                closePrefixRuleModal();
                await loadPrefixRules();
            } catch (err) {
                showPrefixRuleError(err.message || "Failed to save prefix rule.");
            } finally {
                saveBtn.disabled = false;
            }
        });

        tbody.addEventListener("click", async (e) => {
            const editBtn = e.target.closest(".btn-edit-prefix-rule");
            const deleteBtn = e.target.closest(".btn-delete-prefix-rule");

            if (editBtn) {
                const prefix = editBtn.dataset.prefix || "";
                const accountType = editBtn.dataset.accountType || "";

                try {
                    await loadAccountTypes();
                    openPrefixRuleModal("edit", {
                        prefix,
                        accountType
                    });
                } catch (err) {
                    alert(err.message || "Failed to load account types.");
                }

                return;
            }

            if (deleteBtn) {
                const prefix = deleteBtn.dataset.prefix || "";

                const ok = confirm(
                    `Delete prefix rule '${prefix}'?\n\nThis will not delete accounts, but new account type auto-detection may no longer recognise this prefix.`
                );

                if (!ok) return;

                try {
                    await API.deleteAccountPrefixRule(prefix);
                    await loadPrefixRules();
                } catch (err) {
                    alert(err.message || "Failed to delete prefix rule.");
                }
            }
        });

        loadPrefixRules();
    }

    function initAdminUsers() {
        const tbody = document.querySelector("#adminUsersTableBody");
        const addBtn = document.querySelector("#btnAddUser");

        if (!tbody) return;

        async function loadUsers() {
            try {
                tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="px-4 py-6 text-center text-gray-500 dark:text-neutral-400">
                        Loading users...
                    </td>
                </tr>
            `;

                const users = await API.getAdminUsers();
                renderUsers(users || []);
            } catch (err) {
                console.error("Failed to load users", err);

                tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="px-4 py-6 text-center text-red-500">
                        Failed to load users: ${escapeHtml(err.message || "Unknown error")}
                    </td>
                </tr>
            `;
            }
        }

        function renderUsers(users) {
            if (!users.length) {
                tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="px-4 py-6 text-center text-gray-500 dark:text-neutral-400">
                        No users found.
                    </td>
                </tr>
            `;
                return;
            }

            tbody.innerHTML = users.map(user => {
                const id = user.id ?? user.Id;
                const email = user.email || user.Email || "";
                const isAdmin = user.isAdmin ?? user.IsAdmin ?? false;
                const isActive = user.isActive ?? user.IsActive ?? false;
                const lastLoginAtUtc = user.lastLoginAtUtc || user.LastLoginAtUtc || "";

                return `
                <tr class="hover:bg-gray-50 dark:hover:bg-neutral-800/60">
                    <td class="px-4 py-3 font-mono text-xs">
                        ${escapeHtml(String(id))}
                    </td>
                    <td class="px-4 py-3">
                        ${escapeHtml(email)}
                    </td>
                    <td class="px-4 py-3">
                        ${renderBoolBadge(isAdmin, "Admin", "User")}
                    </td>
                    <td class="px-4 py-3">
                        ${renderBoolBadge(isActive, "Active", "Inactive")}
                    </td>
                    <td class="px-4 py-3 whitespace-nowrap text-gray-500 dark:text-neutral-400">
                        ${escapeHtml(formatDateTime(lastLoginAtUtc))}
                    </td>
                    <td class="px-4 py-3 text-right whitespace-nowrap">
                        <button type="button"
                                class="btn-edit-admin-user inline-flex items-center rounded-lg border border-gray-200 dark:border-neutral-700
                                       px-3 py-1.5 text-xs text-gray-700 dark:text-neutral-100
                                       hover:bg-gray-100 dark:hover:bg-neutral-800"
                                data-id="${escapeHtml(String(id))}"
                                data-email="${escapeHtml(email)}"
                                data-is-admin="${isAdmin ? "true" : "false"}"
                                data-is-active="${isActive ? "true" : "false"}">
                            <i class="fa-solid fa-pen mr-1"></i>
                            Edit
                        </button>

                        <button type="button"
                                class="btn-reset-admin-user-password ml-2 inline-flex items-center rounded-lg border border-gray-200 dark:border-neutral-700
                                       px-3 py-1.5 text-xs text-gray-700 dark:text-neutral-100
                                       hover:bg-gray-100 dark:hover:bg-neutral-800"
                                data-id="${escapeHtml(String(id))}"
                                data-email="${escapeHtml(email)}">
                            <i class="fa-solid fa-key mr-1"></i>
                            Password
                        </button>

                        <button type="button"
                                class="btn-delete-admin-user ml-2 inline-flex items-center rounded-lg border border-red-200 dark:border-red-900/60
                                       px-3 py-1.5 text-xs text-red-600 dark:text-red-300
                                       hover:bg-red-50 dark:hover:bg-red-900/20"
                                data-id="${escapeHtml(String(id))}"
                                data-email="${escapeHtml(email)}">
                            <i class="fa-solid fa-trash mr-1"></i>
                            Delete
                        </button>
                    </td>
                </tr>
            `;
            }).join("");
        }

        addBtn?.addEventListener("click", async () => {
            const email = prompt("Enter user email:");

            if (email === null) return;

            const cleanEmail = email.trim().toLowerCase();

            if (!cleanEmail) {
                alert("Email is required.");
                return;
            }

            const password = prompt("Enter initial password:");

            if (password === null) return;

            if (!password.trim()) {
                alert("Password is required.");
                return;
            }

            const isAdmin = confirm("Should this user be an admin?");

            try {
                await API.createAdminUser({
                    email: cleanEmail,
                    password,
                    isAdmin,
                    isActive: true
                });

                await loadUsers();
            } catch (err) {
                alert(err.message || "Failed to create user.");
            }
        });

        tbody.addEventListener("click", async (e) => {
            const editBtn = e.target.closest(".btn-edit-admin-user");
            const resetBtn = e.target.closest(".btn-reset-admin-user-password");
            const deleteBtn = e.target.closest(".btn-delete-admin-user");

            if (editBtn) {
                const id = editBtn.dataset.id;
                const currentEmail = editBtn.dataset.email || "";
                const currentIsAdmin = editBtn.dataset.isAdmin === "true";
                const currentIsActive = editBtn.dataset.isActive === "true";

                const email = prompt("Edit email:", currentEmail);

                if (email === null) return;

                const cleanEmail = email.trim().toLowerCase();

                if (!cleanEmail) {
                    alert("Email is required.");
                    return;
                }

                const isAdmin = confirm(
                    currentIsAdmin
                        ? "Keep this user as admin?\n\nOK = Admin, Cancel = Normal user"
                        : "Make this user admin?\n\nOK = Admin, Cancel = Normal user"
                );

                const isActive = confirm(
                    currentIsActive
                        ? "Keep this user active?\n\nOK = Active, Cancel = Inactive"
                        : "Activate this user?\n\nOK = Active, Cancel = Inactive"
                );

                try {
                    await API.updateAdminUser(id, {
                        email: cleanEmail,
                        isAdmin,
                        isActive
                    });

                    await loadUsers();
                } catch (err) {
                    alert(err.message || "Failed to update user.");
                }

                return;
            }

            if (resetBtn) {
                const id = resetBtn.dataset.id;
                const email = resetBtn.dataset.email || "";

                const password = prompt(`Enter new password for ${email}:`);

                if (password === null) return;

                if (!password.trim()) {
                    alert("Password is required.");
                    return;
                }

                try {
                    await API.resetAdminUserPassword(id, {
                        password
                    });

                    alert("Password updated.");
                } catch (err) {
                    alert(err.message || "Failed to reset password.");
                }

                return;
            }

            if (deleteBtn) {
                const id = deleteBtn.dataset.id;
                const email = deleteBtn.dataset.email || "";

                const ok = confirm(
                    `Delete user '${email}'?\n\nThis cannot be undone.`
                );

                if (!ok) return;

                try {
                    await API.deleteAdminUser(id);
                    await loadUsers();
                } catch (err) {
                    alert(err.message || "Failed to delete user.");
                }
            }
        });

        loadUsers();
    }

    function renderBoolBadge(value, trueText, falseText) {
        if (value) {
            return `
            <span class="inline-flex items-center rounded-full bg-green-100 px-2 py-0.5 text-[11px] font-medium text-green-700
                         dark:bg-green-900/30 dark:text-green-300">
                ${escapeHtml(trueText)}
            </span>
        `;
        }

        return `
        <span class="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-[11px] font-medium text-gray-700
                     dark:bg-neutral-800 dark:text-neutral-200">
            ${escapeHtml(falseText)}
        </span>
    `;
    }

    // ---------- helpers ----------
    function showAlert(host, type, msg) {
        if (!host) return;

        if (host._alertTimer) {
            clearTimeout(host._alertTimer);
        }

        const existingMessages = new Set(
            Array.from(host.querySelectorAll("[data-alert-message]"))
                .map(x => x.dataset.alertMessage)
        );

        if (!existingMessages.has(msg)) {
            const div = document.createElement("div");

            const successClass =
                "pointer-events-auto rounded-xl border border-emerald-300 bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-900 shadow-lg " +
                "dark:border-emerald-700/60 dark:bg-emerald-950/80 dark:text-emerald-100";

            const dangerClass =
                "pointer-events-auto rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-sm font-medium text-red-900 shadow-lg " +
                "dark:border-red-700/60 dark:bg-red-950/80 dark:text-red-100";

            div.className = type === "success" ? successClass : dangerClass;

            div.dataset.alertMessage = msg;
            div.textContent = msg;

            host.appendChild(div);
        }

        host._alertTimer = setTimeout(() => {
            host.innerHTML = "";
            host._alertTimer = null;
        }, 4000);
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
})();
