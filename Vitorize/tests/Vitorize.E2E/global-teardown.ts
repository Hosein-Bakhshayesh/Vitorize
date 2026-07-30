import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';

/**
 * Playwright can terminate a webServer wrapper before its PowerShell finally
 * block runs. The PID file is validated by Stop-E2EStack.ps1 before any process
 * is stopped, so teardown cannot target an unrelated reused PID.
 */
export default async function globalTeardown(): Promise<void> {
  if (process.env.E2E_MANAGE_STACK !== 'true') return;

  const script = path.resolve(__dirname, 'scripts', 'Stop-E2EStack.ps1');
  const pidFile = path.resolve(__dirname, 'artifacts', 'stack', 'managed-stack-pids.json');
  const logFile = path.resolve(__dirname, 'artifacts', 'diagnostics', 'teardown.log');
  await fs.mkdir(path.dirname(logFile), { recursive: true });
  await fs.appendFile(logFile, `[${new Date().toISOString()}] Global teardown started.\n`);
  try {
    execFileSync('powershell.exe', [
      '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', script, '-PidFile', pidFile
    ], { stdio: 'inherit', windowsHide: true });
    await fs.appendFile(logFile, `[${new Date().toISOString()}] Managed stack teardown completed.\n`);
  } catch (error) {
    await fs.appendFile(logFile, `[${new Date().toISOString()}] Managed stack teardown failed: ${String(error)}\n`);
    throw error;
  }
}
