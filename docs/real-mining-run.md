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

## Fixed afterwards (engine follow-up)

- **Issue 2:** a GPU miner with no hashrate for 3 minutes, or one that exits, now counts as failed. Otium tries the next
  algorithm and says so plainly ("Graphics card isn't mining kawpow: its dataset doesn't fit in the graphics card's free
  memory… Trying … instead"). The failed algorithm is set aside for 1 hour, doubling after each failure up to 24 hours, so
  it isn't retried in a loop. When nothing is left to try, the app shows "Graphics card isn't mining: <reason>" and when
  it tries again. Hashes from SRBMiner's first dev-fee round (60 s after its "doing the first devfee mining round" line)
  count neither as earnings nor as benchmark samples. A benchmark also only passes if the card is still hashing in its
  last 30 s.
- **Issue 3:** on Windows, miners join a Job Object with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`, so they and SRBMiner's
  watchdog child die with Otium, however it exits. On Linux and macOS, Otium kills them with their child processes on
  normal exit, SIGTERM, SIGHUP or an unhandled exception. At startup, every platform stops miners left running from
  this install's engines folder. That cleanup is skipped while another Otium holds the same data folder's `otium.lock`.
- **Issue 4:** onboarding, Settings and the antivirus cards now tell users to choose **Cancel** at the Windows Firewall
  prompt. The miners only make outgoing connections, and Otium reads both stats APIs at `127.0.0.1`, which the
  firewall doesn't filter.
- **Issue 1 (partly):** before launching an algorithm, Otium compares the free VRAM (NVML, NVIDIA only) with its
  `MinMemoryGb`. If it doesn't fit, Otium skips that algorithm for 10 minutes and explains that other apps are using the
  graphics memory. A skipped algorithm isn't recorded as a failed benchmark. This doesn't replace the retest with free
  VRAM: at 7.5 GB free, etchash still failed in the direct run.

## Fixed in this change

- `KryptexClient.ParseWorkers` reads `hashrate` (number or string) and falls back to `avg_hashrate_30m`, the live
  shape. Only worker names drive crediting, so balances were never affected, but `KryptexWorker.Hashrate` was always 0.
  DevPool now serves the live shape.
- Tests pin the real SRBMiner 3.7.6 API document, its `"algorithms": null` startup state, its console `Total:` line,
  and the live Kryptex worker list.

## Retest with VRAM free (16 October 2026, 15:36–15:52)

A retest of the engine fixes above (commit 6d0a462), with Unreal Editor closed: `pwsh build/dev.ps1 -RealMiners`, mining
into the developer's own address (not recorded here). The miners ran from 15:37:20 until Otium was force-killed at 15:51:39,
about 14 minutes. Electricity was set to "free", so all four MoneroOcean GPU algorithms were tested.

- **Before the run:** 10,952 MiB of 12,288 MiB VRAM free (1,142 MiB used by the desktop), GPU idle at ~47 W. Ports 5080/5090
  were free and no other Otium or miner process was running.
- **Antivirus:** Windows Defender didn't block either engine this time, so no "Allow automatically" was needed.

| Check | Result |
|---|---|
| XMRig on MoneroOcean | ✅ **33 accepted, 0 rejected** in 14 min. The first shares came ~6 min in. API `hashrate.total` read 1.0–2.0 kH/s (the PC was in use). Otium showed 1.66 / 1.64 / 1.23 kH/s at moments when the API read 1.70 / 1.66 / 1.36 kH/s. |
| SRBMiner accepted shares | ❌ **0 on every algorithm, with VRAM free.** So the first run's failures weren't caused by Unreal holding memory (details below). |
| Benchmarks fail plainly | ✅ Each failed algorithm was recorded with SRBMiner's reason and shown in Settings ("didn't work: …"). Once all four failed, the GPU row and Advanced showed *"Graphics card isn't mining: none of the coins it can mine worked in testing. Use "Test GPU again" in Settings to retry."*, not "Starting…". |
| Stall failover while mining | ⚪ **Not exercised.** No algorithm passed testing, so the provider never reached mining mode. Unit tests cover it. |
| Switching between tests | ✅ Each new test started a fresh SRBMiner. The previous process was gone each time (one `SRBMiner-MULTI` at a time, parent Otium). |
| Killing Otium stops the miners | ✅ `Stop-Process -Force` on Otium (like Task Manager's End task; no shutdown code runs) during a kawpow test, with SRBMiner at 349 W and 3.3 GB VRAM. **3 s later neither XMRig nor SRBMiner was running**, and VRAM was back to 990 MiB. Both miners were in a job object before the kill. No separate SRBMiner watchdog process appeared in this run, so killing a watchdog wasn't observed directly; the job covers any child either way. |
| Free-VRAM check | ✅ No false positive: 10.9 GB free is above every algorithm's `MinMemoryGb`, so nothing was skipped. The "too little free memory" path wasn't exercised on hardware. |
| Firewall prompt | No Windows Firewall prompt was seen during the run. |

### GPU tests (SRBMiner 3.7.6, driver 616.92)

| Algorithm | Window | API `gpu.total` | Power | Failure Otium recorded |
|---|---|---|---|---|
| kawpow | 15:37:20–15:39:40 | 0 throughout (t = 15, 49, 89, 114 s) | 345–350 W for ~50 s (the dev-fee round), then ~100–140 W | `Couldn't set epoch …` (VRAM used peaked at 3.3 GB) |
| autolykos2 | 15:39:40–15:42:15 | 0 | ~97 W | `GPU0[t0] OCL Error code -52 [clEnqueueNDRangeKernel [2]]` |
| etchash | 15:42:15–15:44:50 | 0 (pool jobs arriving, difficulty 23.4) | ~99 W | `Couldn't set epoch …` |
| cn/gpu | 15:44:50–15:47:20 | 0 | ~98 W | A "GPU0 couldn't setup …" error (cut off in the UI) |

The API's hashrate stayed 0 even while the card drew ~345 W in kawpow's first minute. That fits the dev-fee round mining
for the developer, which 3.7.6 apparently doesn't count under the user's algorithm. Error -52 is OpenCL's
`CL_INVALID_KERNEL_ARGS`, so SRBMiner ran its OpenCL path on this NVIDIA card. Together with "Couldn't set epoch" on 10.9 GB
free, this points at SRBMiner 3.7.6 with this driver, not at Otium or VRAM.

### Fixed after the retest

- Otium said kawpow's and etchash's "dataset doesn't fit in the graphics card's free memory", which was wrong here.
  "Couldn't set epoch" is now reported as *"the miner couldn't load this coin's dataset onto the graphics card ("Couldn't set
  epoch 611 on GPU0")"*. Only explicit out-of-memory lines mention memory. Low free memory is reported separately, from
  NVML, before launch.

### Still open

- **GPU mining doesn't work on this RTX 3080 with SRBMiner 3.7.6.** Next steps:
  - Run SRBMiner directly with `--log-file` to get its full output.
  - Try another SRBMiner release or NVIDIA driver.
  - Consider a CUDA-based miner for NVIDIA cards.
- **Leftover from the first run:** an `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Otium` entry that starts the other
  worktree's dev build (`optimistic-vaughan-de3538`) at sign-in. This retest pre-seeded its settings instead of clicking through
  onboarding, so it didn't add another. The entry was left in place for the developer to remove.

## Making the GPU earn (16 October 2026, 15:58–16:43)

### Diagnosis: SRBMiner's OpenCL path fails on this card, its CUDA path works

All runs were short direct SRBMiner runs (`--log-file`, API on loopback), each killed afterwards.

- **Backends:** `--list-devices` finds the RTX 3080 as a CUDA device (`[CUDA][0] … [CC: 8.6] [12287 MB]`). For mining, SRBMiner
  starts kawpow, etchash and autolykos2 on its **OpenCL** backend (`GPU0 init... [OPENCL]`) and quantus on **CUDA**
  (`GPU0 init... [CUDA]`). It reports 10.5 GB free and an 11.6 GB maximum buffer.
- **kawpow (MoneroOcean):** the dev-fee round ran at ~350 W with a small dataset (+2.1 GB). The switch to the user's job then
  failed with `Couldn't set epoch 611 on GPU0` (a ~5.9 GB dataset). `--gpu-table-slow-build` didn't help.
- **etchash:** `Epoch : 850 [etchash]`, then `Couldn't set epoch on GPU0` 3 s later, before any memory was allocated.
  This happened on MoneroOcean and on unMineable's standard-stratum pool (`etchash.unmineable.com:3333`).
- **autolykos2:** `OCL Error code -52 [clEnqueueNDRangeKernel [2]]` (OpenCL's CL_INVALID_KERNEL_ARGS), before the pool
  connected, on both MoneroOcean and unMineable.
- **SRBMiner 3.5.8** (official release, MD5 `dd4b411b40af51346e42a248400abd86` as published, SHA-256
  `1407087e595c5fe393478f258d76fde6ff1d39ec41eba46f1645efdea88df143`) fails identically on kawpow and etchash. So it's not
  a 3.7.6 regression.
- **Otium's arguments aren't the cause.** SRBMiner's own example scripts use `--algorithm-gpu`, but `--help` says
  `--algorithm` with `--disable-cpu` is equivalent, and the failures occur in OpenCL setup, before any pool-specific step.
  Changing the pool didn't change them either.

**Conclusion:** on this RTX 3080 with driver 616.92, SRBMiner (3.5.8 and 3.7.6) can't run its OpenCL algorithms at
today's dataset sizes. Its CUDA algorithms work. This is one machine, so whether every NVIDIA card is affected is still open.

### Kryptex coins (CUDA) on the real Kryptex Pool (the developer's account, not recorded here)

| Run | Coin | SRBMiner `gpu.total` | Power | Shares | Kryptex worker list |
|---|---|---|---|---|---|
| Direct, 130 s | pearlhash | ~1.14e14 | ~334 W | 2 accepted, 0 rejected | worker `diag`: 2 valid, 30-min avg 1.0e13 |
| Direct, 130 s | quantus | ~5.15e8 | ~349 W | 7 accepted, 0 rejected | worker `diag`: 7 valid, 30-min avg 7.1e7 |
| Otium, 14 min | pearlhash + quantus | 8.57e13 / 3.61e8 (Otium's benchmarks) | +247.5 W / +250.3 W measured | | worker `ot9kbke9riaubr`: Pearl 4 valid, Quantus 10 valid + 2 stale, 0 invalid |

- **Units (open item 6):** Kryptex's 30-minute averages are on the same scale as SRBMiner's `gpu.total`, given about
  2 minutes of mining in a 30-minute window and few shares. So `gpu.total` is H/s for both coins, with no ×1000 factor.
  `/api/v3/miner/workers` again returned the live shape (string `avg_hashrate_*`). The per-worker chart endpoint returned
  **403 Forbidden** to a plain request, which the server's crediting may need to handle.
- **Payout:** the server priced Pearl at 3.51e-14 and Quantus at 7.17e-9 USD per H/s per day. That's ~$3.0 and ~$2.6 a day
  gross at Otium's measured speeds (the app showed "Mining Pearl · $2.90/day · 86.78 TH/s"), against about $0.9 a day of
  electricity at $0.15/kWh.
- **Speed:** inside Otium, both coins ran about 25–30% slower than in the direct runs (85.7 vs 114 T, 361 vs 515 M). In the
  direct runs XMRig wasn't running and SRBMiner had normal priority (Otium uses below-normal). The cause isn't isolated yet.

### Otium's engine with real mining

- **Benchmarks:** pearlhash and quantus both passed (2.5 min each; MoneroOcean's algorithms were excluded because
  electricity was set as paid).
- 🐛 **Fixed: the switcher kept the last algorithm tested.** Right after testing, "current" was quantus (tested last), and the
  15-minute minimum hold kept it, although Pearl paid about $0.40 a day more. Otium now picks the best one outright after
  testing.
- ✅ **Stall failover:** I froze the running quantus miner (NtSuspendProcess) at 16:36:44. Its API stopped answering. About
  3 minutes later Otium killed it and started pearlhash (16:39:50), which hashed at 85–88 T. The frozen process didn't
  survive.
- ✅ **Killing Otium:** a second `Stop-Process -Force` during Pearl mining removed XMRig and SRBMiner within 3 s.
- **Fixed: a plain message before the GPU policy arrives.** The first start attempt came before the app had fetched the
  server's GPU policy. That showed "Hit a problem, retrying…" for a minute, then the retry started Pearl. That case now
  reads "Graphics card isn't mining: no coin it can mine pays more than its electricity right now."
- **Hardened:** if SRBMiner's API stops answering, its console's last "Total" line now counts only if it's under 90 s old,
  so a frozen miner can't look busy (the console is the fallback source).

### Still open

- **MoneroOcean's GPU algorithms on NVIDIA.** They aren't excluded for NVIDIA in `GpuAlgorithm.Vendors` yet: SRBMiner
  documents NVIDIA support for them, and this is one machine. They're "free power only" anyway. If more NVIDIA machines fail
  the same way, the server's kill switch (`GpuPolicy:Disabled`) can turn them off without an app release, or they can be
  made AMD-only.
- **Production setting:** set `Kryptex:Enabled=true` and `Kryptex:Account` to the operator account on the deployed server.
- **Speed gap:** find the cause of the 25–30% lower speed inside Otium (CPU contention with XMRig, or priority).
- **Startup leftover:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Otium` still starts the other worktree's dev
  build at sign-in, for the developer to remove.

## Kryptex account test (16 October 2026, 17:11–17:58)

The operator's own Kryptex account (a "krx…" username with auto-exchange to BTC, not recorded here), checked for
what Otium needs from it: per-worker statistics for crediting, CPU Monero on Kryptex, and payouts.

| Check | Result |
|---|---|
| Per-worker chart for an account (`/{coin}/api/v1/miner/chart/{account}/{worker}`) | ❌ **403** "You do not have permission to perform this action", for Pearl, Quantus and Monero workers alike. Charts are public only for wallet-address mining, which can't use auto-exchange. Crediting from charts would have paid users nothing. |
| Worker list for an account (`/{coin}/api/v3/miner/workers/{account}`) | ✅ Public. Each worker has `avg_hashrate_30m`, `_3h` and `_24h` (strings, H/s), valid/stale/invalid shares, status, miner agent and country. Offline workers stay listed while their averages drain. |
| XMRig 6.26.0 on Kryptex Monero | ✅ `xmr.kryptex.network:8029` with TLS, login `account.otiumtest`, password `x`: 15 minutes at about 2.4 kH/s (50% of an i7-8700K), 7 of 7 shares accepted. |
| Crediting from the worker list | ✅ Sampling `avg_hashrate_30m` every 10 minutes and summing average × 10 minutes gave **2,582,660 hashes** against XMRig's own **2,582,772** (0.004% apart). Samples: 898, 1,435, 1,435, 537, 0 H/s. With only 7 shares this closeness is partly luck, since the pool's figure comes from share difficulty, but the method reproduces exactly what Kryptex counts and pays for. Otium's crediting now works this way (commit "Kryptex crediting: sample the worker list's 30-minute averages"). |
| BTC withdrawal to the treasury | ⏳ Not tested. The account held 0.00000049 BTC, far below Kryptex's 0.00025 BTC withdrawal minimum, and moving funds is for the operator to do in the Kryptex dashboard (Payouts). |

Still open:

- **Withdrawal:** once the account passes 0.00025 BTC, withdraw to the bitcoind treasury once and note the fee and
  the time it takes.
- **Credited vs. paid:** after a few days of real mining, compare what Otium credited from Kryptex workers with the BTC
  the account received, to measure the auto-exchange spread.

## Non-custodial run through the app (17 October 2026, 19:21–20:00)

The app itself (dev mode with `-RealMiners -FeeWallet 'USDT:TB2x…'`, a local non-custodial server, real unMineable and
WhatToMine) on the i7-8700K and RTX 3080. The user's payout address was `BNB:0x166a…` (the operator's own, on BNB Smart
Chain) and Otium's fee target `USDT:TB2x…`. Both engines' mining time was set to 23:28 so the 24-hour fee cycle reached
its fee slice after about 3 minutes; electricity was set as free.

| Check | Result |
|---|---|
| Registration | ✅ The app registered `BNB:0x166a…` with the non-custodial server (worker `ot35v9qwinyxrb`, no pool wallet); the GPU policy priced Pearl, Xelis and FishHash from live WhatToMine. |
| GPU login | ✅ SRBMiner 3.7.6: `--algorithm pearlhash --pool pearlpow.unmineable.com:443 --tls true --wallet BNB:0x166a….ot6148da9bg#ztjq-jr7s --password x`, about 105 TH/s, 3 of 3 shares accepted after switching back. |
| CPU login | ✅ XMRig 6.26.0 config: `rx.unmineable.com:443`, TLS, `user` = the slice's target + `.ot6148da9b#ztjq-jr7s`, 50% threads. |
| Fee slice | ✅ At mining time 23:31:12 both miners restarted with `USDT:TB2x…` (same worker, same referral code); at 24:00:00 (19:54:20) the GPU went back to `BNB:0x166a…`. Otium's address collected **0.014903 USDT** in about 30 minutes of fee slice (CPU and GPU). |
| Referral code | ✅ unMineable lists every worker on both addresses with `"referral":"ztjq-jr7s"`, and the user's address dropped from a 1% to a **0.75%** fee once a worker with the code was active. |
| App | ✅ Home showed "0.00000001 BNB at unMineable · paid out at 0.01 BNB" (no auto-pay warning: this address has auto-pay on) and "Mining Pearl (short fee slice)" with 104.7 TH/s and $3.15/day. |
| New GPU algorithms | ✅ On first run the miner benchmarked FishHash and Xelis (`xelishashv3`) before settling on Pearl; unMineable recorded the Xelis worker too. |
| Antivirus | ⚠️ Windows Defender deleted the XMRig copied into the new data folder three times; mining resumed after "Allow automatically" in the app. |

Still open: Otium's own USDT address still has unMineable's auto-pay off (`"auto":false`), so it won't be paid until the
operator turns it on; and a funded Mysterium registration end to end.

## Monero payout: GPU on unMineable, paid in XMR (17 October 2026, 23:13–23:22)

The app from `main` after PR #6 (dev mode `-RealMiners`, non-custodial server, real pools, Norway, electricity set as
free), with the operator's own Monero address as the payout target (`XMR:42zZ…`) and no fee address.

| Check | Result |
|---|---|
| CPU | ✅ XMRig on `gulf.moneroocean.stream:20128` with TLS, the bare Monero address as user; MoneroOcean showed the address at about 1.25 kH/s. |
| GPU routing | ✅ The GPU policy offered MoneroOcean's algorithms and unMineable's Pearl route together. SRBMiner first benchmarked CryptoNight-GPU (new on this PC; it failed with "couldn't setup ctx->algorithm", as MoneroOcean's GPU algorithms do on this card), then settled on Pearl. |
| GPU login | ✅ `--algorithm pearlhash --pool pearlpow.unmineable.com:443 --tls true --wallet XMR:42zZ….ot6148da9bg#ztjq-jr7s`, about 101 TH/s, 2 of 2 shares accepted in 5 minutes. |
| unMineable | ✅ The XMR address has a `pearlpow` worker with `"referral":"ztjq-jr7s"`, the 0.75% fee, a balance building up (0.00000081 XMR after 5 minutes), threshold 0.03 XMR. Auto-pay is off, as for every new address. |
| App | ✅ "0.00000302 XMR at MoneroOcean · paid out at 0.003 XMR · plus 0 XMR at unMineable from the graphics card, paid out at 0.03 XMR" (the unMineable figure refreshes every 5 minutes), the warning that auto-pay is off at unMineable for the graphics card's earnings, and "Mining Pearl" at 100.35 TH/s, $3.13/day. |
| Fixed after the run | The warning's "Open my address page" opened MoneroOcean's page; it now opens the page of the pool the warning is about. |
