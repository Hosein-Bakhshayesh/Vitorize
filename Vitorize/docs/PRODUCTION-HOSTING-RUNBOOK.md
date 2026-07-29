# Production hosting and restart rehearsal

Vitorize requires a TLS-terminating reverse proxy, persistent Data Protection keys shared by API and Web instances, and durable storage roots that survive a package replacement. Production startup rejects missing API storage roots, public origin, or trusted-proxy configuration; the Web host also rejects missing shared key configuration.

## Required deployment configuration

Supply through the platform configuration/secret provider, never committed appsettings:

```text
Hosting__PublicOrigin=https://api.example.com
Hosting__DataProtectionKeysPath=<shared persistent path>
Hosting__DataProtectionApplicationName=Vitorize
Hosting__PublicMediaRoot=<shared persistent public media path>
Hosting__PrivateDocumentsRoot=<shared persistent private KYC path>
Hosting__TrustedProxies__0=<reverse-proxy IP>
# or Hosting__TrustedProxyNetworks__0=<CIDR>
```

The reverse proxy must only forward `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host` from the configured proxy/network. Do not trust client-supplied forwarding headers. Keep the public-media root served at `/uploads`; private documents are never a static-file root and remain authorized API downloads.

## IIS / reverse proxy and container notes

- IIS app-pool identities (or the container service user) need read/write access to the key, public-media, and private-document roots. The API probes write access during startup.
- Mount these paths outside the published package and mount the same paths into every API/Web replica. Preserve normal least-privilege ACLs; private KYC storage must not be mounted into a public static host.
- Route HTTPS at the proxy and forward the original scheme/host. Confirm callbacks use `Hosting__PublicOrigin`.
- Blazor Server circuits are stateful. Use sticky sessions for the current deployment model. Multi-instance scale-out also needs a SignalR backplane before arbitrary cross-node circuit routing can be supported; do not claim no-affinity scale-out until that component is deployed and rehearsed.

## Restart and rolling deployment acceptance checklist

1. Log in as a customer and an admin, record only the test account IDs and timestamps.
2. Upload one public test image and one private KYC test document; verify the public file works and the private URL is denied without authorization.
3. Restart one API/Web instance. Confirm both cookies remain valid, the public media remains, and authorized KYC viewing still works.
4. With sticky routing enabled, repeat against every replica. For a rolling update, drain a node, wait for active requests/circuits according to the platform policy, update it, then restore traffic before continuing.
5. Confirm `/api/health` and protected diagnostics, checkout callback origin, logs, public media write/read, and private-document authorization. Preserve screenshots/log timestamps as evidence.

Actual restart, proxy, and multi-node rehearsal require the production hosting owner and remain the external acceptance gate for RB-007.
