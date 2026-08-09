// A rendered Blazor Server circuit cannot write HTTP response cookies itself.
// This same-origin endpoint call updates the browser's HttpOnly cookies after rotation.
window.vzAuthSession = window.vzAuthSession || {
    persistTokens: async function (scheme, accessToken, refreshToken) {
        const response = await fetch("/auth/session/tokens", {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ scheme, accessToken, refreshToken })
        });
        return response.ok;
    }
};
