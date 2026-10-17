# Mysterium: registering a provider node automatically, with payouts to the user's wallet

Researched 2026-10-17 against myst 1.39.7 (tag commit dated 2026-09-23). Sources: node source code, the Registry contract, public transactor GETs, GitHub issues and PRs, MystNodes help, Kryptex. No accounts were created and no funds were sent.

## Verdict

- **Route A: pay the fee (works today, needs no permission from anyone).** Otium sends about 0.13 MYST (Polygon, chain 137) to the identity's `channel_address`. It then calls `POST /identities/{id}/register` with `{"beneficiary":"<user wallet>"}`. The transactor pays the gas and takes the fee from the channel. Cost is about 0.13 MYST, roughly $0.01 at $0.079/MYST, plus Polygon gas for Otium's own ERC-20 transfer. Earnings settle straight to the user's wallet.
- **Route B: partner free registration (free, but needs a deal with MystNodes).** Mysterium's partner guide works like this:
  1. Log in to `sentinel.mysterium.network/api/v1/auth/password` with partner credentials from Mysterium (`"pool":"external"`).
  2. Call `POST affiliator.mysterium.network/api/v1/free-registration/partner {"identity":...}`.
  3. Call `POST /identities/{id}/register {"beneficiary":"<any wallet>","stake":0}`.

  The beneficiary is chosen per node, so each user can get their own wallet. This is probably how Kryptex works, but that is unverified.
- **Not viable:** the global free pool (it currently returns `eligible:false`), referral tokens (campaigns ended in 2022), and paying the fee out of earnings (no such code path exists).

## 1. Eligibility (code: identity/registry/transactor.go, free_registrar.go, tequilapi/endpoints/transactor.go)

- The transactor base URL is `https://transactor.mysterium.network/api/v1` (metadata/network.go). The affiliator is `https://affiliator.mysterium.network/api/v1`.
- `GetFreeRegistrationEligibility(id)` calls `GET identity/register/eligibility/{id}`. This is the per-identity check that `POST /identities/:id/register` uses through `canRegisterForFree`, which returns true if a referral token is present OR the identity is eligible.
- `GetFreeProviderRegistrationEligibility()` calls `GET identity/register/provider/eligibility` with no identity. Its code comment says "determines if there are any free provider registrations available", so it is a global pool. Only `FreeRegistrar` uses it, on node start, and only when `--transactor.provider.try-free-registration=true` is set (default false). It then calls `POST identity/register/provider` with fee 0. A live GET on 2026-10-17 returned `{"eligible":false}`.
- History:
  - PR #5036 (2022-04): free registration was allowed if the registration was "paid in pilvytis", Mysterium's fiat/crypto payment gateway.
  - PR #5778 (2023-04): this check was replaced by the transactor's per-identity eligibility.
  - Issue #5763: "Check free registration availability in affiliator".
- The backend rules are closed source. The known inputs are:
  - partner `free-registration/partner` calls (see section 5)
  - MystNodes SSO onboarding. `VerifyGrant` returns `isEligibleForFreeRegistration` per MystNodes account, and issue #6102 (2025-03) shows the error "mystnodes account ... has reached the limit of free node registrations", which means a per-account quota
  - possibly historical pilvytis orders

  No evidence was found of a per-IP rule.

## 2. Referral tokens

- `POST identity/register/referer` sends `{...registration, token}` with fee forced to 0. The tequilapi treats any non-nil `referral_token` as free, and the transactor validates it server-side.
- PR #4923 (2022-03), "Remove Transactor Referral Program API", says the referral API "is not supported anymore. There are 0 referral campaigns". `GET /identities/{id}/referral` and `/referral-available` were removed.
- What is left is dead code:
  - `GET /transactor/token/:token/reward` routes to the affiliator `register-reward/token/{token}`. A test token returned "not found".
  - The `getReferralTokenRequest` signer is unused.
- The current MystNodes "referral system" is an account bonus (5 MYST per referred friend who reaches 5 MYST), not a registration token. [help article](https://help.mystnodes.com/en/articles/8836815-how-does-the-referral-system-work)
- **Verdict:** no public way to obtain tokens. Whether partners get tokens is unknown. The partner flow uses affiliator whitelisting instead.

## 3. Paying the fee from earnings

- No such path exists. `hermes_promise_settler.loadInitialState` logs "Provider ... not registered, skipping", and `handleHermesPromiseReceived` refuses to settle for unregistered providers. Registry.registerIdentity only takes the fee from the MYST balance at the channel address. Grepping for settle-into-registration found nothing.
- Earnings collected while unregistered stay as unsettled promises at Hermes. It is unverified whether Hermes keeps them for settlement after a later registration.

## 4. Paying the fee (Registry.sol, payments-smart-contracts)

- `registerIdentity(hermesId, stake, transactorFee, beneficiary, sig)` does the following:
  - recovers the identity from a signature over (chainId, registry, hermes, stake, fee, beneficiary)
  - requires `stake+fee <= MYST.balanceOf(getChannelAddress(identity, hermes))`
  - opens the channel with that beneficiary
  - pays the fee to `msg.sender`, the transactor
- So the transactor pays the gas, and the node needs no MATIC/POL.
- Address: `GET /identities/{id}` returns `channel_address`, computed by `GetActiveChannelAddress` with the active Hermes. Polygon (137) values:
  - registry `0x87F0F4b7e0FAb14A565C87BAbbA6c40c92281b51`
  - hermes `0x80ed28d84792d8b153bf2f25f0c4b7a1381de4ab`
  - MYST token `0x1379e8886a944d2d9d440b3d88df536aea08d9f3`
- Amount: `GET /transactor/fees` or the transactor `fee/137/register`. A live check on 2026-10-17 gave 132130846901900000 wei, about 0.1321 MYST. The quote is valid for about 18 minutes and the signed request embeds the fee, so send a margin (for example 0.2 MYST). Any excess stays as consumer balance in the channel.
- Flow: `POST /register` returns 202, and the CLI then says "Registration started. Top up the identities channel to finish it." (cmd/commands/account/command.go). This implies the transactor waits for funds. Funding before or after the POST both look valid, but this is unverified; poll `GET /identities/{id}/registration`.
- Cost at the CoinGecko price of $0.0788: about $0.01 per node.

## 5. Partner integrations

- [node-partner-guide](https://github.com/mysteriumnetwork/node-partner-guide) (src/App.tsx, 2025-05) has these steps:
  1. Run `myst --vendor.id=<partner> service --agreed-terms-and-conditions`.
  2. Run `PUT /identities/current`.
  3. Log in to sentinel with "provided" credentials.
  4. Call `POST affiliator/free-registration/partner` with the Bearer token.
  5. Call `POST /identities/{id}/register {"beneficiary":..., "stake":0}`.

  The guide says the beneficiary is "ERC20 compatible wallet that will receive earned MYST settlements. It is possible to change it after registration but we strongly advise to set it when registering". The beneficiary is set per node, so each user's own wallet works.
- [mystnodes-sdk-client](https://hub.docker.com/r/mysteriumnetwork/mystnodes-sdk-client) is a different binary that runs with `--user.token` "obtained from mystnodes team". `user.token` is not a myst node flag. Its docs say nothing about the beneficiary or payee; it looks like earnings go to the token holder's (partner's) account, which is unverified.
- Kryptex: the beneficiary defaults to Kryptex's wallet `0x33C5D0F470F0aAB2751BC6E3Ad42cA6ecAdCB20f` (custodial, converted to BTC at 5 MYST). A user can change it to their own wallet. No MystNodes account is needed. [Kryptex article](https://www.kryptex.com/articles/exploring-mysterium-en). How Kryptex registers nodes is undocumented, most likely the partner flow.

## 6. Beneficiary rules

- It can be set at registration. If empty, it defaults to the channel address (`fillIdentityRegistrationRequest`), and the settler then refuses auto-settle with "payment channel ... is set as beneficiary, skip settling". **So always pass the user's wallet at registration.**
- It can be changed later. `POST /identities/:id/beneficiary-async` only saves it locally. On the next settlement, `validateBeneficiary` sees the difference and uses transactor `identity/settle_with_beneficiary`. `POST /identities/:id/beneficiary` forces that settlement now.
- `Registry.setBeneficiary` itself has no fee; it only needs the identity's signature. In practice the change rides on a settlement, so the cost is the normal settle fee (about 0.06 MYST) plus the 20% Hermes fee on the settled amount. It probably needs unsettled earnings above the fee (unverified).
- The MystNodes dashboard also offers "Update Wallet"; its help article mentions no fee. [help article](https://help.mystnodes.com/en/articles/8050789-how-to-change-your-wallet-address)

## Unverified

- The transactor and affiliator eligibility rules (closed source).
- How to become a partner and what it costs.
- Whether funding the channel before `POST /register` is accepted.
- What happens to promises earned while unregistered.
- The sdk-client payee model.
- The Kryptex registration mechanism.
