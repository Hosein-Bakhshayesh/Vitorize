export async function copyLink(url) {
    try {
        await navigator.clipboard.writeText(url);
        return true;
    } catch {
        return false;
    }
}
