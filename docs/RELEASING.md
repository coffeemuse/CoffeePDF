# Releasing CoffeePDF for macOS

A pushed `v*` tag builds CoffeePDF, signs it with the owner's Developer ID, notarises it, and attaches
`CoffeePDF-<version>.dmg` to the tag's GitHub release (spec §10, `.github/workflows/ci.yml`). This page covers the
one-time setup that needs, and each release.

## The secrets

| Secret | Where | What it holds |
|---|---|---|
| `AVALONIA_LICENSE_KEY` | Repository secret (set 2026-10-29) | The Avalonia licence key |
| `APPLE_DEVELOPER_ID_P12` | The `release` environment | The Developer ID Application certificate and its private key, as a `.p12`, base64-encoded |
| `APPLE_DEVELOPER_ID_PASSWORD` | The `release` environment | The `.p12`'s password |
| `APPLE_TEAM_ID` | The `release` environment | The 10-character Apple Team ID |
| `NOTARY_KEY` | The `release` environment | An App Store Connect API team key (`.p8`), base64-encoded |
| `NOTARY_KEY_ID` | The `release` environment | That key's Key ID |
| `NOTARY_ISSUER` | The `release` environment | The Issuer ID of the account's API keys |

CI base64-decodes `APPLE_DEVELOPER_ID_P12` and `NOTARY_KEY`, so they're stored encoded. Only `v*` tags may use the
`release` environment, so a branch or a pull request never sees the signing secrets.

A private repository's environment secrets need GitHub Pro, Team or Enterprise. On GitHub Free, step 1 below fails.
Then either upgrade, or keep the six as repository secrets instead, which every workflow run on a branch of this
repository could read. The second needs a change to `ci.yml`, since `package-mac` names the environment.

## One-time setup

Run these on the Mac that signed the 0.0.1-spike3 build, from the repository's folder, with the `gh` CLI signed in as
the repository's owner. `gh secret set` reads a value from its input, or asks for it when there's none, so no value
lands in the shell's history. Keep the `.p12` and `.p8` files outside the repository.

### 1. The `release` environment, for `v*` tags only

```bash
gh api -X PUT repos/coffeemuse/CoffeePDF/environments/release --input - <<'EOF'
{"deployment_branch_policy": {"protected_branches": false, "custom_branch_policies": true}}
EOF
```

```bash
gh api -X POST repos/coffeemuse/CoffeePDF/environments/release/deployment-branch-policies -f name='v*' -f type=tag
```

On the web, the same steps are Settings › Environments › New environment, named `release`. Under Deployment branches
and tags, choose Selected branches and tags, then add a rule of ref type Tag with the pattern `v*`.

### 2. The Developer ID certificate

1. Find the Team ID with `security find-identity -v -p codesigning`. The identity is named
   `Developer ID Application: <name> (<Team ID>)`, and the 10 characters in the parentheses are the Team ID.
2. Export the certificate:
   1. In Keychain Access, open the login keychain › My Certificates.
   2. Expand that Developer ID Application certificate and check that a private key sits under it.
   3. Right-click the certificate › Export…, and save it as Personal Information Exchange (`.p12`), such as
      `~/Desktop/developer-id.p12`, with a new, strong password.
3. Store all three:

```bash
base64 -i ~/Desktop/developer-id.p12 | gh secret set APPLE_DEVELOPER_ID_P12 --env release --repo coffeemuse/CoffeePDF
```

```bash
gh secret set APPLE_DEVELOPER_ID_PASSWORD --env release --repo coffeemuse/CoffeePDF
```

```bash
gh secret set APPLE_TEAM_ID --env release --repo coffeemuse/CoffeePDF
```

4. Delete the `.p12`, or keep it only in a password manager.

### 3. The notary's key

The Healdan Helper's `helper-release` environment uses the same kind of key. If its `.p8` file was kept, it works here
too, with its Key ID and Issuer ID. Apple lets a `.p8` be downloaded only once, and GitHub never shows a secret again.
Otherwise, make a new key:

1. In App Store Connect, open Users and Access › Integrations › App Store Connect API › Team Keys. The Account Holder
   may have to request access the first time.
2. Generate a key named `CoffeePDF notarisation`, with the Developer role.
3. Download its `AuthKey_<Key ID>.p8`, then note the Key ID in its row and the Issuer ID above the table.
4. Store all three:

```bash
base64 -i ~/Downloads/AuthKey_XXXXXXXXXX.p8 | gh secret set NOTARY_KEY --env release --repo coffeemuse/CoffeePDF
```

```bash
gh secret set NOTARY_KEY_ID --env release --repo coffeemuse/CoffeePDF
```

```bash
gh secret set NOTARY_ISSUER --env release --repo coffeemuse/CoffeePDF
```

### 4. Check

```bash
gh secret list --env release --repo coffeemuse/CoffeePDF
```

It should list the six secrets above. The list shows only their names.

## Each release

1. Once `main`'s CI has passed, tag the commit and push the tag. Tags go through the owner, as pushes do. A version
   with a suffix, such as `v0.1.0-rc1`, becomes a pre-release.

   ```bash
   git tag v0.1.0 origin/main
   ```

   ```bash
   git push origin v0.1.0
   ```

2. CI runs the tests, then `package-mac` builds, signs and notarises the app and its disk image, and staples both.
   `release-mac` makes the GitHub release and attaches the image, but only once `stapler validate` passes on it.
   Without the notary's secrets, the image is signed but not notarised. It's kept as the run's artifact for 30 days
   and isn't attached to the release.
3. **The Gatekeeper check:**
   1. Download the image from the release page in a browser, so that macOS quarantines it as it would for anyone
      else.
   2. Open the image, drag CoffeePDF to Applications, replacing any earlier build there, and open it.
   3. **Expected:** only macOS's usual first-open prompt, saying the app came from the internet and Apple checked it
      for malicious software, then an empty window. There should be no "can't be opened" or "unverified developer"
      block.
   4. To see the verdict itself, run `spctl -a -vv /Applications/CoffeePDF.app`. Expected:
      `accepted` and `source=Notarized Developer ID`.

## When something expires

A Developer ID certificate lasts five years, and an API key lasts until it's revoked. Repeat step 2 or step 3 above
with the new one. `gh secret set` replaces a secret's old value.
