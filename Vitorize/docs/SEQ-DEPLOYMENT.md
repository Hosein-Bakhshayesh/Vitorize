# Seq deployment

`docker-compose.monitoring.yml` pins Seq 2025.2, persists `/data`, restarts automatically, includes a health probe, uses an isolated network, and binds both host ports to loopback.

1. Generate the first-run password hash using Seq's documented hash utility or isolated first-run procedure. Never place the clear password in Compose.
2. Export `SEQ_FIRSTRUN_ADMINPASSWORDHASH`; optionally set `SEQ_UI_PORT` and `SEQ_INGEST_PORT`.
3. Run `docker compose -f docker-compose.monitoring.yml up -d`.
4. Sign in at `http://127.0.0.1:8081`, create a least-privilege ingestion key per environment/service, and put it in the application's secret provider.
5. Configure retention and disk alerts before production traffic.

Do not publish Seq directly to the internet. Use a VPN/internal segment or authenticated TLS reverse proxy with access restrictions and firewall rules. Containers on the monitoring network use the ingestion-only endpoint `http://seq:5341`; host applications use `http://127.0.0.1:5341`. The UI remains on internal port 80 and loopback host port 8081.

Production variables per host:

```text
Seq__Enabled=true
Seq__ServerUrl=https://seq-ingest.internal.example
Seq__ApiKey=<secret from vault>
Seq__ApplicationName=Vitorize.Api
Monitoring__ShowSeqLink=true
Monitoring__SeqUiUrl=https://seq.internal.example
```

Use `Vitorize.Web` for Web. The UI URL is not a key and must not contain credentials. Rotate least-privilege ingestion keys independently.

Verify Compose health, `/health`, disk capacity, ingestion from both services, and authenticated UI. A Seq outage does not fail startup or requests; the bounded queue may shed events while rolling files continue. Set explicit retention, maintain 30% free space, back up consistently, and test restoration separately. Never place backups in public storage.

Read release notes and test in staging before changing the pinned image. Do not use `latest`. Disabling `Seq__Enabled` is the immediate application-side rollback and requires no database rollback.
