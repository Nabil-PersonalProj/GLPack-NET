// wwwroot/js/api.js
(function (global) {
    const API_BASE = ""; // same origin as the MVC app

    async function request(path, { method = "GET", body, headers } = {}) {
        const opts = {
            method,
            headers: Object.assign(
                {
                    "Accept": "application/json",
                    "Content-Type": "application/json"
                },
                headers || {}
            ),
            body: body ? JSON.stringify(body) : undefined,
            credentials: "same-origin"
        };

        const res = await fetch(API_BASE + path, opts);
        const contentType = res.headers.get("content-type") || "";

        if (!res.ok) {
            let payload = null;
            try {
                payload = contentType.includes("application/json")
                    ? await res.json()
                    : await res.text();
            } catch { /* ignore parse error */ }

            const message =
                (payload && (
                    payload.message ||
                    payload.error ||
                    payload.detail ||
                    extractModelStateErrors(payload.errors) ||
                    payload.title
                )) ||
                (typeof payload === "string" ? payload : res.statusText);

            const err = new Error(message || `HTTP ${res.status}`);
            err.status = res.status;
            err.payload = payload;
            throw err;
        }

        if (contentType.includes("application/json")) {
            return res.json();
        }
        return res.text();
    }

    function extractModelStateErrors(errors) {
        if (!errors) return null;

        const messages = [];

        for (const key of Object.keys(errors)) {
            const value = errors[key];

            if (Array.isArray(value)) {
                messages.push(...value);
            } else if (typeof value === "string") {
                messages.push(value);
            }
        }

        return messages.length ? messages.join(" ") : null;
    }

    // ----- Companies API -----
    function getCompanies() {
        // hits GET /api/companies
        return request("/api/companies");
    }

    function createCompany(payload) {
        // hits POST /api/companies
        // payload: { name, code, ... } according to your DTO
        return request("/api/companies", {
            method: "POST",
            body: payload
        });
    }

    function companyDashboardUrl(companyId) {
        // adjust route if needed
        return `/Company/${companyId}/Dashboard`;
    }

    function deleteCompany(id) {
        return request(`/api/companies/${id}`, {
            method: "DELETE"
        });
    }

    // ----- Accounts API -----
    function getAccounts(companyId, opts = {}) {
        const { q = "", page = 1, pageSize = 10 } = opts;
        const params = new URLSearchParams();
        if (q) params.set("q", q);
        if (page) params.set("page", page);
        if (pageSize) params.set("pageSize", pageSize);

        return request(`/api/companies/${companyId}/accounts?` + params.toString());
    }

    function createAccountFromPrefix(companyId, dto) {
        return request(`/api/companies/${companyId}/accounts/from-prefix`, {
            method: "POST",
            body: Object.assign({ companyId }, dto)
        });
    }

    function updateAccount(companyId, accountCode, dto) {
        return request(`/api/companies/${companyId}/accounts/${encodeURIComponent(accountCode)}`, {
            method: "PUT",
            body: Object.assign({ companyId }, dto)
        });
    }

    function deleteAccount(companyId, accountCode) {
        return request(`/api/companies/${companyId}/accounts/${encodeURIComponent(accountCode)}`, {
            method: "DELETE"
        });
    }

    // ----- Transactions API -----
    function getTransactions(companyId, opts = {}) {
        const {
            page = 1,
            pageSize = 10,
            q = "",
            transactionNo = null,
            from = "",
            to = ""
        } = opts;

        const params = new URLSearchParams();
        params.set("page", page);
        params.set("pageSize", pageSize);
        if (q) params.set("q", q);
        if (from) params.set("from", from);
        if (to) params.set("to", to);
        if (transactionNo != null) params.set("transactionNo", String(transactionNo));

        return request(`/api/companies/${companyId}/transactions?` + params.toString());
    }

    function createTransaction(companyId, dto) {
        return request(`/api/companies/${companyId}/transactions`, {
            method: "POST",
            body: dto
        });
    }

    function updateTransaction(companyId, transactionNo, dto) {
        return request(`/api/companies/${companyId}/transactions/${encodeURIComponent(transactionNo)}`, {
            method: "PUT",
            body: dto
        });
    }

    function deleteTransaction(companyId, transactionNo) {
        return request(`/api/companies/${companyId}/transactions/${encodeURIComponent(transactionNo)}`, {
            method: "DELETE"
        });
    }

    // ----- Search API -----
    function ledgerSearch(companyId, opts = {}) {
        const {
            q = "",
            accountCode = "",
            transactionNo = null,
            from = null,
            to = null,
            page = 1,
            pageSize = 100
        } = opts;

        const params = new URLSearchParams();
        if (q) params.set("q", q);
        if (accountCode) params.set("accountCode", accountCode);
        if (transactionNo != null) params.set("transactionNo", String(transactionNo));
        if (from) params.set("from", from);
        if (to) params.set("to", to);
        params.set("page", String(page));
        params.set("pageSize", String(pageSize));

        return request(`/api/companies/${companyId}/search?` + params.toString());
    }

    // ----- Logs API -----
    function getLogs(opts = {}) {
        const {
            q = "",
            level = "",
            eventType = "",
            page = 1,
            pageSize = 50
        } = opts;

        const params = new URLSearchParams();

        if (q) params.set("q", q);
        if (level) params.set("level", level);
        if (eventType) params.set("eventType", eventType);

        params.set("page", String(page));
        params.set("pageSize", String(pageSize));

        return request("/api/admin/logs?" + params.toString());
    }

    // ----- Account Type Prefix -----
    function getAccountPrefixRules() {
        return request("/api/admin/prefix-rules");
    }

    function getAccountTypes() {
        return request("/api/admin/account-types");
    }

    function createAccountPrefixRule(dto) {
        return request("/api/admin/prefix-rules", {
            method: "POST",
            body: dto
        });
    }

    function updateAccountPrefixRule(prefix, dto) {
        return request(`/api/admin/prefix-rules/${encodeURIComponent(prefix)}`, {
            method: "PUT",
            body: dto
        });
    }

    function deleteAccountPrefixRule(prefix) {
        return request(`/api/admin/prefix-rules/${encodeURIComponent(prefix)}`, {
            method: "DELETE"
        });
    }

    function getAccountPrefixRulesForAccounts(companyId) {
        return request(`/api/companies/${companyId}/accounts/prefix-rules`);
    }

    // ----- User Account API -----
    function getAdminUsers() {
        return request("/api/admin/users");
    }

    function createAdminUser(dto) {
        return request("/api/admin/users", {
            method: "POST",
            body: dto
        });
    }

    function updateAdminUser(id, dto) {
        return request(`/api/admin/users/${encodeURIComponent(id)}`, {
            method: "PUT",
            body: dto
        });
    }

    function resetAdminUserPassword(id, dto) {
        return request(`/api/admin/users/${encodeURIComponent(id)}/reset-password`, {
            method: "POST",
            body: dto
        });
    }

    function deleteAdminUser(id) {
        return request(`/api/admin/users/${encodeURIComponent(id)}`, {
            method: "DELETE"
        });
    }

    global.API = {
        getCompanies,
        createCompany,
        deleteCompany,
        companyDashboardUrl,
        getAccounts,
        createAccountFromPrefix,
        updateAccount,
        deleteAccount,
        getTransactions,
        createTransaction,
        updateTransaction,
        deleteTransaction,
        ledgerSearch,
        getLogs,
        getAccountPrefixRules,
        createAccountPrefixRule,
        updateAccountPrefixRule,
        deleteAccountPrefixRule,
        getAdminUsers,
        createAdminUser,
        updateAdminUser,
        resetAdminUserPassword,
        deleteAdminUser,
        getAccountTypes,
        getAccountPrefixRulesForAccounts,
    };
})(window);
