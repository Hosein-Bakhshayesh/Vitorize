// A rendered Blazor Server circuit cannot write HTTP response cookies itself.
// These same-origin endpoint calls let it update or clear the browser's HttpOnly cookies.
window.vzAuthSession = window.vzAuthSession || {
    persistTokens: async function (scheme, accessToken, refreshToken) {
        const response = await fetch("/auth/session/tokens", {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ scheme, accessToken, refreshToken })
        });
        return response.ok;
    },
    // Ends one scheme's session in the browser's real cookie jar. Without this a session that ended
    // inside a circuit left a cookie behind holding an already-revoked refresh token.
    endSession: async function (scheme) {
        const response = await fetch("/auth/session/end", {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ scheme })
        });
        return response.ok;
    }
};
