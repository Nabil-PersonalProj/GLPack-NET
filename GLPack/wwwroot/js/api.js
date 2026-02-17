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
                (payload && (payload.message || payload.error)) ||
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
        const { q = "", page = 1, pageSize = 50 } = opts;
        const params = new URLSearchParams();
        if (q) params.set("q", q);
        if (page) params.set("page", page);
        if (pageSize) params.set("pageSize", pageSize);

        return request(`/api/companies/${companyId}/accounts?` + params.toString());
    }

    function createAccount(companyId, dto) {
        return request(`/api/companies/${companyId}/accounts`, {
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
            pageSize = 20,
            from = null,
            to = null
        } = opts;

        const params = new URLSearchParams();
        params.set("page", page);
        params.set("pageSize", pageSize);
        if (from) params.set("from", from);
        if (to) params.set("to", to);

        return request(`/api/companies/${companyId}/transactions?` + params.toString())
            .then(data => ({
                items: data,
            }));
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

    // ----- Accounts API -----

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

    global.API = {
        getCompanies,
        createCompany,
        deleteCompany,
        companyDashboardUrl,
        getAccounts,
        createAccount,
        updateAccount,
        deleteAccount,
        getTransactions,
        createTransaction,
        updateTransaction,
        deleteTransaction,
        ledgerSearch,
    };
})(window);
