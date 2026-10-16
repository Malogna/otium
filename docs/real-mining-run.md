# Real mining run: i7-8700K + RTX 3080, 16 October 2026

A short run of the real engines (`pwsh build/dev.ps1 -RealMiners`) on the developer's PC, to check the "Real-machine
run" item under **Before launch** in the README. XMRig 6.26.0 and SRBMiner-Multi 3.7.6 mined on the real MoneroOcean
pool into the developer's own Monero address (not recorded here).

- **Hardware:** Intel i7-8700K (6 cores / 12 threads), NVIDIA RTX 3080 12 GB, driver 616.92, Windows 11 22621.
- **Otium run:** 14:44–14:57 (13 minutes).
- **Direct SRBMiner diagnostics:** 14:57–15:05, about 5 minutes of mining.
- **Settings:** for this test, the onboarding answer was "electricity is included or free". At the estimated $0.15/kWh
  (Norway, NO1) the app correctly said mining wouldn't pay and would have waited. Free power also makes the GPU
  benchmark try cn/gpu.
- **Machine load:** the PC was in normal use. Other Claude sessions, Python jobs and the Monero GUI wallet were running,
  and Unreal Editor 5.8 was opened at 14:50, taking about 4.2 GB of VRAM.

## Summary

| Check | Result |
|---|---|
| SRBMiner API format | ✅ Matches the parser: `algorithms[].hashrate.gpu.total` in **H/s** (also `1min`/`15min`/`1hr`/`6hr`). Before the first pool job, 3.7.6 serves `"algorithms": null`, and the parser returns null for that. |
| SRBMiner GPU speed units | ✅ kawpow: the API reported `33040828.85` (H/s), the benchmark stored `33539467.94`, and the UI showed **33.54 MH/s**. |
| SRBMiner console fallback | ✅ The real line `Total: 0.00 H/s [P:91.1W …]` matches the regex. Only 0 H/s console lines were captured, so no non-zero kH/MH unit was seen on the console. |
| XMRig `/2/summary` | ✅ `hashrate.total[1]` (60 s), falling back to `[0]` (10 s), in H/s. The UI showed 456 H/s → 1.58 kH/s, matching the API. Accepted shares: 0 → 18 during the run (`results.shares_good`). |
| GPU power through NVML | ✅ kawpow benchmark +239 W measured. nvidia-smi showed 309–349 W under load and ~70–96 W idle. SRBMiner's own `asic_power` (266 W) is lower because it is chip-only. |
| SRBMiner dev fees | ✅ `--list-algorithms` matches `GpuAlgorithms`: pearlhash 2%, quantus 2%, kawpow 0.85%, autolykos2 1%, etchash 0.65%, cryptonight_gpu 0.85%. |
| GPU actually earning on MoneroOcean | ❌ No accepted GPU share on any algorithm (details below). Otium read what SRBMiner reported correctly (0 H/s); the failures are in SRBMiner and VRAM. |
| MoneroOcean shows the worker | ✅ The account's worker id appears in `/miner/{address}/identifiers` and `chart/hashrate/allWorkers` (rx/0, `hs` = `hs2` = 274.5 H/s pool-side average; the address's `validShares` went 9 → 36). It was missing at 14:49 because MoneroOcean lists a worker only after its first accepted share, which XMRig sent at ~14:51. |
| `pearlhash` / `quantus` stats units | ❌ **Not verified.** No Kryptex account was available. MoneroOcean lists a pearlhash port, but its stratum dropped SRBMiner's pearlhash login every 5 s, so no stats were produced. SRBMiner has no offline benchmark mode. |
| Kryptex per-worker chart units | ✅ Checked on public data (no account needed). `/prl/api/v1/miner/chart/{address}/{worker}` returns 10-minute buckets: `timestamp` in ms and a numeric `hashrate` on the same scale as the pool's `net_hashrate` and WhatToMine's `nethash`. That scale is what the policy's USD-per-H/s rate is based on. |
| Kryptex worker list | 🐛 **Fixed.** The live `/api/v3/miner/workers/{address}` has **no `hashrate` field**, though Kryptex's OpenAPI file says it does. It sends `avg_hashrate_30m`/`3h`/`24h` as **strings**. It isn't paginated (3,381 workers came back in one response). |

## Evidence

### GPU benchmark inside Otium (`gpu-benchmarks.json`)

| Algorithm | Result | Miner/pool evidence |
|---|---|---|
| kawpow | 33,539,467.94 H/s, +239.3 W | API `gpu.total` 33,040,828.85 at 68 s; nvidia-smi 309 W; 0 shares in 150 s at pool difficulty 0.987 (expected ~1 share per ~126 s at this speed) |
| autolykos2 | failed, "no stable hashrate" | API 0 H/s for the whole 150 s, GPU ~95 W. Direct run: `OCL Error code -52 [clEnqueueNDRangeKernel [2]]` right after start |
| etchash | failed, "no stable hashrate" | API 0 H/s, difficulty 23.17 (jobs arriving). Direct run: `Couldn't set epoch on GPU0`, with 7,550 MB of 12,287 MB VRAM free |
| cn/gpu | failed, "no stable hashrate" | GPU at ~300 W but API `gpu.total` stayed 0 for 150 s (`gpu_autotune_results` "0") |

After the benchmark, Otium picked kawpow as expected. That second kawpow launch also stalled at 0 H/s. The direct run
shows why. SRBMiner first does `Hold on, doing the first devfee mining round` for about 45 s at ~330 W, then switches to
the user's pool and fails with `Couldn't set epoch 611 on GPU0`. The DAG for MoneroOcean's job doesn't fit next to
what other apps hold (Unreal Editor plus the desktop used ~4.2 GB). One consequence: a benchmark sample can be taken
during SRBMiner's dev-fee round, so a "successful" benchmark doesn't prove the card hashes for the user's pool.

### CPU

XMRig ran 6 threads (`max-threads-hint: 50`) at idle priority. It got only about 2 cores' worth of time because the PC
was busy, so its speed ranged from 120 to 1,670 H/s. That reflects this machine's load, not a parsing problem. The
Advanced view's "12 threads" label is `Environment.ProcessorCount`, not the number XMRig runs.

### Antivirus

Windows Defender acted seconds after the downloads (hash-verified official builds):

- `xmrig.exe` was quarantined as **Trojan:Win64/HashvaultMiner.A**.
- `SRBMiner-MULTI.exe` was flagged as **Trojan:Win32/Ravartar!rfn**.

Otium detected this and showed its "Your antivirus blocked the earning engines" card. The user approved "Allow
automatically" (one UAC prompt adds a Defender exclusion for the engines folder), and both engines then ran.

## Issues found (not fixed here)

1. **No accepted GPU shares on MoneroOcean with this card while other apps hold VRAM.** Retest kawpow/etchash with
   VRAM free (Unreal etc. closed) to separate SRBMiner from environment. Otium's per-algorithm `MinMemoryGb` checks
   total VRAM, not free VRAM.
2. **A GPU stuck at 0 H/s after the benchmark stays "Starting…" forever.** `SrbMinerGpuProvider.GetStatusAsync` only
   handles a process that exited. A live miner reporting 0 H/s for minutes is never failed over or reported.
3. **Killing or crashing the Otium process leaves XMRig and SRBMiner mining.** SRBMiner also runs a watchdog child
   that survives killing the parent. On Windows, a Job Object with kill-on-close would end both with the app.
4. **SRBMiner's stats API listens on 0.0.0.0** (XMRig binds 127.0.0.1), and 3.7.6 has no bind-address option. This
   triggers a Windows Firewall prompt, and onboarding tells users to click Allow, which exposes the read-only stats
   (pool, wallet, hashrate) to the LAN. Users should be told the miner only needs outgoing connections.
5. **Closing the window keeps mining in the tray**, presumably by design. dev.ps1 waits for the window's process, so
   dev mode only stops when the process exits.
6. **pearlhash / quantus units still need a Kryptex run:** about 5 minutes of `SRBMiner-MULTI --algorithm pearlhash
   --pool prl.kryptex.network:7048 --wallet <account>.test --api-enable`, comparing `gpu.total` with the Kryptex
   worker chart.

## Fixed in this change

- `KryptexClient.ParseWorkers` reads `hashrate` (number or string) and falls back to `avg_hashrate_30m`, the live
  shape. Only worker names drive crediting, so balances were never affected, but `KryptexWorker.Hashrate` was always 0.
  DevPool now serves the live shape.
- Tests pin the real SRBMiner 3.7.6 API document, its `"algorithms": null` startup state, its console `Total:` line,
  and the live Kryptex worker list.
