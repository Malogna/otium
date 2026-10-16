# Treasury automation and monitoring for Otium (as of October 2026)

Scope: an automated money flow for a solo operator (Norway/EEA), from revenue sources (MoneroOcean XMR, Kryptex Pool Pearl/Quantus with auto-exchange, bandwidth partners) to funded user payouts (PayPal Payouts API, Monero from the operator's monero-wallet-rpc hot wallet), plus reconciliation, hedging and alerting. Research budget was limited (about 16 tool calls), so several items are flagged as gaps or as inferences from general knowledge.

## Off-ramps for XMR and other coins (exchanges and swap services) for an EEA/Norwegian operator

### Takeaway
No major EU-authorised exchange takes XMR deposits from EEA customers in October 2026. Kraken stopped EEA XMR deposits and trading on 31 Oct 2024, Binance delisted XMR worldwide in Feb 2024, and Coinbase, Bitstamp, Bitpanda, Bitvavo, OKX and Gate.io have no XMR pairs. From 10 July 2027 the EU AMLR will also bar custody of anonymity-enhancing coins at licensed providers. The practical XMR exits are therefore non-custodial swaps (instant-exchange aggregators such as Trocador and ChangeNOW, and possibly THORChain, whose XMR support was still being tested) into BTC or a stablecoin, followed by a regulated EEA exchange for fiat. That path carries real business KYC/AML and source-of-funds risk. The cleanest design pays XMR cash-outs directly from incoming XMR and converts only the surplus.

### Cited Findings
- Kraken halted XMR trading and deposits for EEA clients on 31 Oct 2024. Withdrawals stayed open until 31 Dec 2024, and remaining balances were converted to BTC (distributed 6 Jan 2025). Kraken had already pulled XMR in Ireland and Belgium in June 2024. — [Kraken support: Support for Monero (XMR) in Europe](https://support.kraken.com/articles/support-for-monero-xmr-in-europe); [Cointelegraph](https://cointelegraph.com/news/kraken-ends-monero-support-european-economic-area); [Decrypt](https://decrypt.co/284276/monero-kraken-delisting-xmr-european)
- A market check dated 5 Oct 2026 (article published 6 Oct, updated 12 Oct 2026) looked at eight venues:
  - Kraken lists 5 XMR pairs but blocks them for the EEA.
  - KuCoin lists 4 XMR pairs but has no EU authorisation and does not serve Germany.
  - Gate.io, OKX, Coinbase, Bitpanda, Bitvavo and Bitstamp have no XMR pairs.
  - Binance stopped XMR trading worldwide in Feb 2024.
  — [CryptoTicker, "Buying Monero in Germany"](https://cryptoticker.io/en/buy-monero-germany-delisting/)
- AMLR (Regulation (EU) 2024/1624), Art. 79(1), bars credit institutions, financial institutions and CASPs from keeping accounts that allow anonymisation "including through anonymity-enhancing coins". It applies from 10 July 2027 (Art. 90). It goes further than MiCA Art. 76(3), which covers only admission to trading. — [CryptoTicker](https://cryptoticker.io/en/buy-monero-germany-delisting/); [Xgram, "Is Monero legal"](https://xgram.io/blog/is-monero-legal) (vendor blog)
- Vendor and secondary blogs list KuCoin, MEXC, Gate.io, TradeOgre and Kraken (non-EEA) as the remaining centralised XMR venues. Sources conflict on Gate.io: CryptoTicker found no XMR pairs there in Oct 2026. — [Leodex 2026 guide](https://leodex.io/learn/delistings/buy-monero-after-delistings); contradicted by [CryptoTicker](https://cryptoticker.io/en/buy-monero-germany-delisting/)
- THORChain announced v3.20 with native XMR and ZEC swaps against BTC, ETH and stablecoins (Chainwire press release, 25 Aug 2026). — [Decrypt / Chainwire](https://decrypt.co/376438/thorchain-3-20-unlocks-native-monero-and-zcash-swaps-with-bitcoin-ethereum-and-stablecoins)
- That launch is contradicted by a THORWallet release of 5 Oct 2026, which says Monero support on THORChain is "currently being tested and audited". — [Cointelegraph (THORWallet press release)](https://cointelegraph.com/press-releases/thorwallet-integrates-monero-bringing-native-xmr-support-to-the-app)
- An April 2026 THORChain post warned of shallow pools, chain pauses and failed swaps early on. It also said refunds need a return address in the memo. — [THORChain blog](https://blog.thorchain.org/thorchain-is-back-monero-nears-mainnet-and-pol-takes-center-stage)
- ChangeNOW calls itself non-custodial with no registration. KYC "may be initiated only in specific cases", on a risk-based approach. — [ChangeNOW AML/KYC policy (via tostracker)](https://tostracker.app/document/changenowio-tos); [ChangeNOW terms](https://changenow.io/de/terms-of-use)
- ChangeNOW risk in practice:
  - A competitor's review says AML checks often trigger KYC or holds on privacy-coin pairs, "especially above €2,000", and estimates all-in cost at about 0.5–2%. — [Baltex review](https://baltex.io/blog/ecosystem/changenow-review-btc-to-xmr-swap-experience) (competitor, low reliability)
  - One unverified Trustpilot account reports about $30k of ETH frozen for 2 months pending a source-of-funds review, with Sumsub KYC requested. — [Trustpilot](https://nz.trustpilot.com/review/changenow.io?page=2)
- Trocador is an aggregator of 20–30+ instant-exchange partners (e.g. ChangeNOW, TradeOgre, MajesticBank). It shows a per-partner KYC/AML rating, and limits and privacy depend on the partner chosen. Settlement takes about 5–30 minutes. I found no official API documentation. — [Baltex Trocador review](https://baltex.io/blog/ecosystem/trocador-review-privacy-focused-xmr-aggregator); [Trocador guide 2026](https://arnoldnakamura.codeberg.page/trocador-guide/)
- Kraken cash withdrawal fees:

  | Method | Provider | Fee | Minimum | Processing time |
  |---|---|---|---|---|
  | SEPA | Bank Frick / Banking Circle / Openpayd | €1 | €2 | 0–5 business days |
  | Instant SEPA | ClearJunction | €0.90 | €3 | near-instant |
  | SWIFT | (fallback when the IBAN is not SEPA-reachable) | €5 | €100 | not stated |

  Fees may vary by the account's registration country. — [Kraken support, cash withdrawal options](https://support.kraken.com/gb/articles/360000423043)
- Stablecoins under MiCA: Kraken delisted USDT, PYUSD, EURT, TUSD and UST for EEA clients in Q1 2025.
  - Steps: sell-only from 27 Feb, spot trading halted 24 Mar, remaining balances converted to a MiCA-compliant stablecoin on 31 Mar 2025.
  - After the deadlines, deposits of these assets are withdraw-only.
  - Binance also halted spot USDT trading in the EEA.
  — [Cointelegraph](https://cointelegraph.com/news/kraken-usdt-delisting-europe-mica-stablecoin-compliance); [DL News](https://www.dlnews.com/articles/markets/kraken-delists-tether-usdt-in-europe-as-mica-rules-take-hold); [Cointelegraph, Binance](https://cointelegraph.com/news/binance-halt-spot-usdt-trading-eea-mica-compliance)

### Inferences
- Norway is in the EEA, so EEA-scoped delistings (Kraken XMR, Kraken/Binance USDT) almost certainly apply to a Norwegian business account. No source named Norway explicitly.
- For an EEA operator, USDC (MiCA-compliant) or BTC is the practical intermediate asset to get to EUR/NOK, not USDT. Kryptex USDT payouts would still need to be swapped to USDC/BTC or sold somewhere that accepts USDT deposits. Withdraw-only status after the deadlines suggests deposits of USDT to Kraken EEA may be blocked or limited; this needs verification.
- A registered Norwegian business that routinely swaps XMR through no-KYC instant exchanges creates three problems:
  - a bank/exchange source-of-funds problem: funds arriving at the EU exchange are traceable to a swap service, and some exchanges flag those inflows;
  - freeze risk on larger swaps;
  - a weaker AML position after July 2027.
- Mitigations:
  - keep the volume of XMR that needs off-ramping small, by paying XMR cash-outs in XMR;
  - steer GPU and other revenue into BTC/USDC via Kryptex auto-exchange;
  - keep a documented trail (pool payout tx IDs, swap order IDs, tx hashes) for the bank and the accountant.
- MoneroOcean itself can pay out in other coins in some configurations, which is not verified here. If it does, that would remove XMR from the operator's off-ramp path entirely except for funding the XMR hot wallet.

### Gaps
- I found no verified 2026 per-exchange fee or withdrawal-limit tables for KuCoin or MEXC XMR, and their EEA/Norway onboarding status for business accounts is unconfirmed. Using them is likely a compliance mismatch for a Norwegian business.
- THORChain XMR mainnet status as of 16 Oct 2026 is unresolved: the sources conflict.
- I found no official ChangeNOW partner API fee schedule, and no Trocador API docs; they need to be checked directly.
- FixedFloat and Chainflip XMR support were not researched.
- I did not verify whether Kraken's `WithdrawFunds` API works for fiat to a pre-registered business IBAN. To my knowledge it does, using a saved withdrawal "key", but it is not sourced here.
- Bitstamp and Coinbase API fiat-withdrawal capability for Norwegian business accounts was not researched.

## Kryptex Pool: payouts, auto-exchange, automation

### Takeaway
Kryptex Pool can auto-exchange mined coins and pay out in BTC, USDT (BEP-20/TRC-20), ETH or LTC, with low minimums (about 1 USDT plus network fee). Its documentation is inconsistent and dated. I found no confirmation of a public API for balances or scheduled withdrawals.

### Cited Findings
- Kryptex Pool payout and auto-exchange options:
  - The Pearl pool page lists BTC, USDT, ETH and LTC as payout options, with a 1 PRL minimum payout.
  - You can mine with a Kryptex username instead of a wallet to get BTC, USDT, Volet or gift-card withdrawals.
  - USDT withdrawals go over BEP-20 or TRC-20.
  — [Kryptex Pearl pools article](https://pool.kryptex.com/articles/pearl-mining-pools-en); [Kryptex profitability/auto-exchange article](https://www.kryptex.com/en/articles/profitability-kryptex-app-pool-autoexchange-en)
- A Kryptex comparison page says pool.kryptex.com does not have automatic BTC exchange and only kryptex.com does. This contradicts other Kryptex pages, so the page is likely outdated. — [Kryptex vs Kryptex Pool](https://pool.kryptex.com/articles/kryptex-vs-kryptex-pool-en)
- Minimum USDT payout:
  - March 2024: 1 USDT plus network fee on BEP-20 and TRC-20.
  - Earlier: 12.5 USDT (2023, TRC-20 only) and 1.5 USDT on BEP-20 (Aug 2023).
  — [Kryptex news](https://www.kryptex.com/it/news/?page=6); [Kryptex Pool vs F2pool](https://pool.kryptex.com/ja/articles/kryptex-pool-vs-f2pool-en)
- Kryptex says auto-exchange does not change mining profitability, only the payout currency. Its own example shows a month where USDT income was 10.4% higher with auto-exchange. — [Kryptex article](https://www.kryptex.com/en/articles/profitability-kryptex-app-pool-autoexchange-en)

### Inferences
- Auto-exchange at the pool is effectively an automatic, immediate hedge: it converts volatile PRL/QTM to BTC/USDT at mining time. That suits a ledger that credits users in USD. USDT is best for price stability, but in the EEA it then needs swapping to USDC or selling outside EEA-restricted venues (see the first section).
- If payouts can go to an address, Kryptex can pay directly to an exchange deposit address (e.g. a BTC address at Kraken), creating a hands-off flow: pool → exchange → EUR → bank → PayPal. Whether automatic payouts to a fixed external address on a threshold or schedule are supported for username-based accounts was not confirmed.

### Gaps
- No public Kryptex API for account balances or withdrawals was found. Monitoring may need to rely on the pool's per-wallet stats endpoints or page scraping. The 2026 payout fees per network are unknown.
- I found no current (2026) Kryptex help-center page confirming payout schedules, minimums for Quantus (QTM), or auto-withdrawal to external addresses.

## Fiat rails: exchange → bank → PayPal business balance

### Takeaway
PayPal Payouts are funded only from the PayPal balance. I found no documented API or scheduled auto top-up for a business PayPal balance. Top-ups are bank transfers into PayPal ("Add money"), done manually in the PayPal UI. The only automatable part is exchange → business bank (e.g. Kraken SEPA, about €1). Keeping a large PayPal float and alerting on low balance is therefore the realistic design.

### Cited Findings
- PayPal Payouts are funded using your PayPal balance, and you must add enough to cover the payout total including fees. PayPal advises linking a bank account first. — [PayPal Payouts overview](https://developer.paypal.com/payouts/use-payouts/overview.md); [Payouts integration](https://developer.paypal.com/api/payouts/standard.md)
- Adding money from a bank account to the PayPal balance:
  - Usually up to 6 working days, done via Wallet → Transfer funds → Add money. — [PayPal help (HK)](https://www.paypal.com/hk/cshelp/article/help114)
  - In Belgium: Instant Top-Up within about 5 minutes (min €25, max €1,000 per payment), or "Add money" in up to 2 business days.
  - The bank account name must match the PayPal account name.
  - No PayPal fee for the transfer.
  — [PayPal help (BE)](https://www.paypal.com/be/selfhelp/article/faq1348)
- Kraken EUR SEPA withdrawal: €1 fee and €2 minimum (0–5 business days), or Instant SEPA via ClearJunction at €0.90 (€3 minimum). — [Kraken support](https://support.kraken.com/gb/articles/360000423043)

### Inferences
- Suggested automated pipeline:
  1. Pool/partner payouts land at the exchange.
  2. A bot sells to EUR via the exchange API on a schedule or threshold.
  3. The bot withdraws to the business IBAN by API (Kraken supports API withdrawals to pre-whitelisted addresses/IBANs).
  4. The operator does a periodic manual "Add money" to PayPal (e.g. weekly). An alternative is to fund PayPal directly with partner payouts that arrive in PayPal (e.g. Massive's monthly PayPal payout) where possible, which is the most automatic option.
- Treat PayPal as a float account with a target of about 2–4 weeks of expected PayPal cash-outs. Pause or queue PayPal cash-outs when the balance falls below pending demand, rather than letting payouts fail.
- PayPal currency conversion inside the account (e.g. EUR balance funding USD payouts) adds spread. Matching the payout currency to the funded balance currency avoids this. This is not sourced here; PayPal's conversion fee should be checked.

### Gaps
- I did not confirm whether Norwegian business PayPal accounts offer Instant Top-Up, or the NOK/EUR limits. I also could not confirm whether any PayPal API (e.g. a Transfers or Funding API) can pull from a linked bank. I found no documentation that one does.
- Wise Business API (which supports programmatic transfers) as an intermediate rail was not researched.

## Hot wallet float, natural hedge and sweeping (Monero)

### Takeaway
monero-wallet-rpc exposes everything needed for an automated float policy: `get_balance` returns balance, unlocked balance and blocks to unlock; there are `transfer`, `sweep_all` and `get_transfers`; and view-only wallets plus unsigned-tx flows allow a cold-wallet split. Pay XMR cash-outs straight from MoneroOcean income (natural hedge), keep a bounded float, and auto-sweep the excess to a cold or view-only-monitored address.

### Cited Findings
- `get_balance` returns `balance`, `unlocked_balance`, `blocks_to_unlock` and `time_to_unlock`, per account and subaddress. `get_accounts` returns `total_balance` and `total_unlocked_balance`. — [Monero wallet RPC docs](https://docs.getmonero.org/rpc-library/wallet-rpc/)
- Transfer and history methods:
  - `transfer`, `sweep_all` and `sweep_dust` return unsigned tx sets when used with view-only wallets.
  - `describe_transfer` decodes an `unsigned_txset` (fee, unlock_time, summary) before signing.
  - `get_tx_key` returns proof keys.
  - `get_transfers` can filter in, out, pending, failed and pool.
  - `incoming_transfers` can filter available or unavailable.
  — [Monero wallet RPC docs](https://docs.getmonero.org/rpc-library/wallet-rpc/)
- Wallet setup and access:
  - `generate_from_keys` without a spend key creates a view-only wallet.
  - `create_address` creates labelled subaddresses.
  - `--rpc-login` enables digest auth.
  - Since v0.18.3.4, `unlock_time` is enforced at 0 by relay rule.
  — [Monero wallet RPC docs](https://docs.getmonero.org/rpc-library/wallet-rpc/)

### Inferences
- Float policy:
  - Target unlocked float = max(about 2× trailing-7-day XMR cash-outs, minimum buffer). A cron job checks `get_balance`.
  - If `unlocked_balance` > target + margin, `sweep`/`transfer` the excess to a cold address, or to the exchange or swap path if converting.
  - If below target, alert and slow down XMR cash-out processing.
- Use a separate subaddress for MoneroOcean payouts and for change, so `get_transfers` can attribute incoming funds per source for reconciliation.
- Natural hedge: credits accrue in USD, but XMR cash-outs are converted at payout time. If the operator's XMR inflow at least matches XMR cash-outs, price moves largely cancel on that slice. Only the net XMR position (inflow minus XMR cash-outs) carries price risk and needs converting.
- Monero outputs need 10 confirmations before they are spendable; this is general protocol knowledge, not found in the fetched excerpt. Payout batching should use `unlocked_balance`, not `balance`, and expect about 20 minutes of lock after each send (change outputs are locked too). Sending many payouts in one multi-destination `transfer` avoids locking the change repeatedly.
- Security: run wallet-rpc bound to localhost with `--rpc-login`, keep the spend key only on the hot wallet holding the float, and set a hard per-day outgoing cap enforced in the Otium server code.

### Gaps
- MoneroOcean payout thresholds and fees for 2026 were not re-verified in this pass; they are covered in other notes.

## Price-risk practices: crediting at expected vs realized value, buffers, stablecoin hedging

### Takeaway
I found no authoritative "best practice" source specific to this model. The design guidance below is inference. The core options:
- credit provisionally at expected value and finalize at realized value;
- haircut credits with a risk buffer;
- convert to stable value as early as possible: Kryptex auto-exchange, frequent XMR conversion of the net position, or a USDC reserve.

### Cited Findings
- Kryptex's own comparison shows auto-exchange can change monthly USD income by about ±10% depending on price path, which illustrates the size of exposure between mining and conversion. — [Kryptex](https://www.kryptex.com/en/articles/profitability-kryptex-app-pool-autoexchange-en)
- Under Norwegian tax rules, mined crypto is income at its NOK market value when received. Later price moves are a separate gain or loss on realisation, so there is accounting exposure as well as cash exposure. — [Skatteetaten, mining](https://www.skatteetaten.no/person/skatt/hjelp-til-riktig-skatt/aksjer-og-verdipapirer/om/virtuell-valuta/mining/)

### Inferences
- Practical policy for Otium:
  1. Credit users every 10 minutes as **pending** at expected value minus a haircut (e.g. 5–15%, tuned to observed pool luck and 7-day volatility).
  2. Move credits to **available** only after the operator's corresponding pool payout is received (or after N days). Reconcile and true-up (or absorb) the difference.
  3. Keep an operator risk reserve (e.g. 10–20% of outstanding user liabilities) in USDC/EUR.
  4. Cap user-visible credit when the treasury coverage ratio is low (see the monitoring section).
- Liability-matching: outstanding user USD balances are a USD liability. Hold assets that match: stablecoins/EUR for the PayPal share, and XMR only for the expected XMR cash-out share.
- Shorting XMR perps to hedge is not realistic for an EEA business (XMR derivatives are on offshore venues) and is overkill at small scale.

### Gaps
- I found no published case study of a small mining-reward app's crediting and hedging policy.

## Reconciliation, monitoring and alerting stack

### Takeaway
A solo operator can cover monitoring with cheap or free tools:
- Healthchecks.io (free up to 20 checks; open-source) as a dead-man's switch on every cron job: crediting loop, pool fetch, reconciliation, sweep, daily report;
- Uptime Kuma (self-hosted) for endpoint and API uptime;
- push alerts via Telegram, ntfy or email.

The business-logic alerts (balances, coverage, velocity) must be computed by the Otium server itself, with a daily summary.

### Cited Findings
- Healthchecks.io:
  - free plan monitors up to 20 jobs;
  - open source (BSD) and self-hostable;
  - alerts when a cron job misses its expected run time.
  — [Healthchecks.io docs, comparison with Cronitor](https://canary.healthchecks.io/docs/healthchecks_cronitor_comparison/); [Capterra](https://www.capterra.ca/reviews/1028488/healthchecksio)
- Paid Healthchecks.io pricing is reported at $17–$20/mo for the next tier (sources conflict; both are third-party). — [Cronping comparison](https://cronping.com/blog/cronping-vs-healthchecks-vs-cronitor); [Toolradar](https://toolradar.com/tools/healthchecks-io)

### Inferences
- Daily reconciliation job: compare, per source and per day,
  - ledger credits issued (USD) vs. pool/partner-reported earnings (MoneroOcean API, Kryptex stats, Honeygain/Massive/Mysterium dashboards) vs. actual receipts (wallet `get_transfers` "in", exchange deposit history, PayPal transaction search).
  - Flag drift above about 5% over 7 days, and any source with zero receipts past its expected payout cadence.
- Suggested alert thresholds (starting points to tune):

  | Alert | Starting threshold |
  |---|---|
  | Hot wallet low | XMR unlocked < 3 days of average XMR cash-outs (warn), < 1 day (critical) |
  | PayPal low | PayPal balance < pending PayPal cash-outs + 7 days of average (warn); < pending (critical, auto-pause PayPal queue) |
  | Coverage ratio | (hot wallet + PayPal + exchange + receivable pool balances) / total user liabilities < 1.1 (warn), < 1.0 (critical) |
  | Crediting stalled | no credit batch in 30 minutes (Healthchecks ping per 10-minute run with 20-minute grace) |
  | Pool/partner API errors | > 3 consecutive failures, or stats unchanged for 2 hours |
  | Payout failures | any PayPal Payouts item with status FAILED/RETURNED/BLOCKED/UNCLAIMED; any wallet-rpc transfer error |
  | Velocity | cash-outs per hour > 3× trailing-30-day p95; single user cash-out > X USD; new accounts cashing out within 24 hours |

- Reports: a daily summary sent by Telegram bot or email, covering:
  - revenue by source;
  - credits issued;
  - cash-outs by rail;
  - balances per account;
  - coverage ratio;
  - top 5 earners;
  - flagged users.
- Uptime Kuma and ntfy are widely used free, self-hosted tools: Uptime Kuma for HTTP, TCP and keyword monitors with many notification integrations, ntfy for push notifications. Specifics were not sourced in this pass.

### Gaps
- Uptime Kuma, ntfy and Grafana documentation and pricing were not fetched; their claims above are from general knowledge.
- I found no fintech-ops blog sources on reconciliation patterns within budget.

## Risk/fraud automation to replace manual cash-out review

### Takeaway
Rules-based automation at small scale is cheap:
- an IP/VPN reputation API (e.g. proxycheck.io: 1,000 free queries per day, paid plans historically about $30/month for millions of queries);
- a device fingerprint generated by the desktop app;
- velocity and limit rules;
- an internal hold queue for edge cases.

Sanctions screening can be done against free official lists. Sources for that part were not gathered.

### Cited Findings
- proxycheck.io:
  - free tier of 1,000 queries per day with the same data and detection as paid plans;
  - historically, 2.56M daily queries for $29.99/month;
  - prices were raised in January (year not certain from the snippet), with existing subscribers grandfathered.
  — [proxycheck.io pricing](https://proxycheck.io/pricing/); [proxycheck.io Customer Q&A](https://proxycheck.io/blog/post/customer-qa); [proxycheck.io blog](https://proxycheck.io/blog/post/a-decade-of-keeping-the-internet-honest)
- A 2026 comparison lists proxycheck.io among the generous free tiers for VPN and datacenter-proxy detection. — [ipgeolocation.io blog](https://ipgeolocation.io/blog/what-are-the-best-vpn-and-proxy-detection-apis) (written by a competitor)

### Inferences
- Auto-approve a cash-out when all of these hold:
  - the account is older than N days;
  - the earnings pattern is consistent with the device's measured hashrate or bandwidth;
  - the cash-out is under a per-user daily limit and under the global hourly cap;
  - the IP is not a datacenter or VPN, or the user has a stable history;
  - the device fingerprint is not shared with more than K accounts;
  - the PayPal email or XMR address is not shared across accounts;
  - there is no sanctions-list hit on name or country.
- Otherwise put it in a hold queue that auto-releases after 72 hours unless the operator intervenes. This keeps the operator in watch mode.
- Earnings in Otium come from the user's own measured mining and bandwidth, so the main fraud vectors are multi-accounting, stats spoofing and referral abuse, rather than stolen-payment fraud. Velocity and duplicate-identity rules matter more than card-style fraud scoring.
- Sanctions: XMR addresses cannot be screened meaningfully. For PayPal, PayPal performs its own sanctions screening. A geo-block of sanctioned jurisdictions (by IP and country) is the minimal operator-side control.

### Gaps
- I did not fetch pricing for IPQualityScore, ipinfo privacy detection, MaxMind minFraud or Fingerprint.com, or the free OFAC SDN, EU consolidated or UN list download APIs. Whether Norwegian law requires the operator to screen users is a question for a lawyer.

## Norwegian accounting and tax (high level; for an accountant)

### Takeaway
Skatteetaten treats mined crypto as taxable income at its NOK market value when received. Mining can be business activity (virksomhet) if the duration, scale, profit and own-risk criteria are met, which allows running deductions. The resulting coins are assets, so later conversion creates a gain or loss. For Otium, crypto received from pools and partners is business revenue of the company, and user payouts are a cost or liability settlement. Classification, VAT and the user-liability accounting must be confirmed by an accountant.

### Cited Findings
- Skatteetaten on mined crypto:
  - you must report it in the tax return;
  - it becomes taxable as it is received;
  - deductible costs include machines, software and electricity;
  - equipment over NOK 30,000 with at least 3 years of expected life must be depreciated over several years rather than deducted at once.
  — [Skatteetaten, Mining av kryptovaluta](https://www.skatteetaten.no/person/skatt/hjelp-til-riktig-skatt/aksjer-og-verdipapirer/om/virtuell-valuta/mining/)
- Bitcoin and similar are capital assets (formuesobjekt). Mining can be business activity if it has a certain scope, and merely setting up equipment without administrative follow-up rarely qualifies. In a business, crypto is income at NOK market value at the time of extraction, with running deductions for related costs. — [Skatteetaten, Bitcoin og andre virtuelle eiendeler (bedrift)](https://www.skatteetaten.no/bedrift-og-organisasjon/rapportering-og-bransjer/bransjer-med-egne-regler/internett/skatte--og-merverdiavgiftsmessig-behandling-av-bitcoins-og-andre-virtuelle-valutaer/)
- Skatteetaten has announced stepped-up enforcement against hidden crypto values. — [Skatteetaten press](https://www.skatteetaten.no/en/press/nyhetsrommet/skatteetaten-skjerper-innsatsen-mot-skjulte-kryptoverdier/)
- Taxable mining does not automatically mean VAT-liable turnover. A Skatteklagenemnda case on mining pools and a Skattedirektoratet BFU on selling computing power are relevant but were not read. — [Divly guide](https://divly.com/no/guides/mining-av-kryptovaluta-i-norge-skatt-fradrag-og-mva) (commercial source)

### Inferences
- Otium is arguably not "mining" for its own account. It sells or aggregates users' compute and bandwidth and shares the revenue, so the VAT treatment (sale of services to pools and partners; possibly reverse-charge for foreign B2B buyers) and the user-payout treatment (payments to private individuals; possibly reportable) need specific professional advice.
- The treasury system should log, for every inbound coin receipt and every conversion, the timestamp, amount, NOK rate and source. That is needed for income recognition at receipt and gain/loss on conversion.
- The fx/crypto log should cover FIFO lots for crypto held. Norway requires FIFO for crypto to my knowledge; this is unverified here.

### Gaps
- 2025/2026 updates to Skatteetaten crypto guidance were not found. The page dates should be checked.
- Not researched:
  - VAT on revenue from Honeygain, Massive and Mysterium (foreign B2B);
  - reporting obligations for payouts to users;
  - Norway's DAC8/CARF implementation (EU DAC8 applies from 1 Jan 2026; Norway's adoption timing was unverified);
  - whether Otium's ledger of user balances could constitute e-money or require registration with Finanstilsynet as a crypto service provider. Flag all of these for an accountant and a lawyer.
