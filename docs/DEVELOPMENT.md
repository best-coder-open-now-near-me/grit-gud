# Development Setup

## Engine baseline

- Unity `6000.4.10f1`
- Universal Render Pipeline `17.4.0`
- Input System `1.19.0`
- Unity Test Framework `1.6.0`

Open the repository root as the Unity project. The committed
`ProjectSettings/ProjectVersion.txt` keeps local and automated builds on the
same Editor patch.

The project uses three runtime assembly boundaries:

- `GritGud.Domain` contains platform-neutral rules and has Unity engine
  references disabled.
- `GritGud.Application` owns use-case coordination, authoritative sessions,
  commands, and replayable resolution pipelines without Unity references.
- `GritGud.Presentation` owns MonoBehaviours and other Unity-facing adapters.

Editor-only generation tools live in `GritGud.Editor`. Engine-free Domain and
Application tests live in `GritGud.Domain.Tests`; Unity-facing adapter tests live
in `GritGud.Presentation.Tests`. Both run as Edit Mode tests.

## Bootstrap scene

`Assets/GritGud/Scenes/Bootstrap.unity` is the application shell. Regenerate it
from **Grit Gud > Regenerate Bootstrap Scene** in the Editor. Gameplay levels
must remain external data loaded by this shell rather than additional authored
Unity scenes.

At runtime, `GameBootstrap` installs the code-driven start menu. Its committed
level library lists validated JSON documents from `Resources/Levels/Published`.
**Play Selected** and **Edit Selected** route the selected detached document
through the shared runtime and authoring loaders; **New Level** opens a fresh
portable document. **Quit** stops Play Mode in the Editor and exits standalone
players.

Bootstrap is the only enabled build scene and is registered as the Editor's
Play Mode start scene. A guarded Editor initializer also opens it once when a
project session starts. It does not replace a dirty scene and does not run in
batch mode.

## Basic level editor

The first runtime editor slice is documented in [LEVEL_EDITOR.md](LEVEL_EDITOR.md).
Its important assembly boundaries are:

- `GritGud.Domain` owns the portable document and validation rules;
- `GritGud.Application` owns editing sessions, commands, undo/redo, and storage
  interfaces; and
- `GritGud.Presentation` owns Unity construction, input, camera, UI, JSON,
  browser storage, and platform file-transfer adapters.

The Unity Editor-only `GritGud.Editor` assembly must not contain runtime level
editor behavior. The Bootstrap scene remains the only authored application
scene; both gameplay preview and editing construct their worlds from portable
data beneath the application root.

### Publishing an exported level from GitHub

An exported level can be added without a local Unity installation:

1. Open the target source branch on GitHub.
2. Upload the exported JSON directly into
   `Assets/GritGud/Content/Resources/Levels/Published/`.
3. Commit the upload to that branch. No `.meta` file or separate level manifest
   is required for a JSON text asset.
4. Wait for the branch preview workflow. Its EditMode gate deserializes and
   validates every published level before building WebGL.
5. Open the branch preview and choose the level by its authored `displayName`.

Published documents must have unique `levelId` values and complete scenario
instances. A malformed or invalid entry fails the branch validation before a
new preview replaces the last successful build. At runtime, the library also
isolates invalid entries and reports their status without hiding valid levels.

## Local validation

The baseline has been validated locally with Unity `6000.4.10f1` and its
matching Windows and Web build-support modules:

- the full EditMode and PlayMode lifecycle suites pass;
- the Windows player builds successfully; and
- the development Web preview builds successfully.

Generated folders such as `Library`, `Temp`, `Logs`, and `UserSettings` are not
versioned.

Run `tools/validate-repository.py` with Python 3 before invoking Unity. It scans
tracked source for unresolved conflict markers and parses every tracked JSON
document. Run `tools/validate-supabase-contracts.py` to verify ordered migrations,
RPC parameters/return rows, permissions, and the matching C# adapter, then run
`node tools/preview-id.test.mjs` to verify preview identity and workflow routing.
Then run the complete EditMode and PlayMode suites in batch mode; use
`-testResults <path>` and `-logFile <path>` beneath the ignored `Temp` directory
so failures remain inspectable. PlayMode coverage includes both a sustained
default-session smoke and startup/teardown for every committed level whose
library entry is playable.

Runtime-generated terrain, outlines, and brush previews use the committed
`GritGud/RuntimeColor` shader. It is explicitly listed in Graphics Settings so
player shader stripping cannot make `Shader.Find` return null. Runtime material
creation also checks fallbacks and never passes a null shader to Unity's
`Material` constructor.

## Branch coordination

Sequential feature work should stay on one active integration branch and update
one pull request until that feature sequence is merged. Do not create parallel
feature branches for each incremental follow-up when later work depends on the
earlier commits.

Before continuing an active sequence after its target branch advances:

1. Fetch and merge or rebase the target branch into the active integration
   branch locally.
2. Resolve and test that integration once.
3. Push the same branch so the existing pull request updates.
4. Close superseded pull requests instead of merging overlapping versions of
   the same feature independently.

When GitHub labels a conflict as **current**, it means the pull request's head
branch; **incoming** means the branch being merged into it. Choose a whole side
only when one branch is known to contain the other branch's work already.
Otherwise resolve the file manually and retain the distinct changes from both.

The committed build entry points are:

- `GritGud.Editor.BuildCommand.BuildWindows`
- `GritGud.Editor.BuildCommand.BuildWebPreview`

Invoke either with Unity's `-batchmode -projectPath <path> -executeMethod <name>`
arguments. Outputs are written beneath the ignored `Builds` directory. The Web
entry point fails immediately with a clear message when its build-support module
is absent. Web previews use Brotli compression with Unity's JavaScript
decompression fallback, so the same release player works on GitHub Pages and on
plain local HTTP servers that do not attach `Content-Encoding: br` headers.

## Continuous integration

The `CI` workflow runs for every pull request, `main` push, and merge-queue
candidate. Its **Source and contract checks** job needs no repository secrets and
is safe for fork pull requests. It validates repository boundaries, the complete
Supabase migration/RPC contract, and preview identity behavior.

The **Licensed Unity tests** job runs EditMode and PlayMode coverage only for
trusted branches, same-repository pull requests, `main`, and merge-queue refs. It
installs the pinned private asset overlay and consumes Unity/private-repository
secrets, so GitHub skips that job for fork pull requests. Configure both check
names as required branch protections; a skipped licensed job does not expose
secrets to a fork, while trusted merge candidates receive the full test gate.

## GitHub Web previews

The `Branch preview` GitHub Actions workflow builds every push except `main` and
`gh-pages`. It combines a readable ref slug with the first 12 hexadecimal digits
of a stable SHA-256 hash and publishes the WebGL player at
`/preview/<slug>-<hash>/` on an orphan `gh-pages` branch. Publication,
concurrency, reporting, and branch deletion all consume that exact identity, so
refs that normalize to the same slug cannot overwrite each other. The
Pages root is regenerated as an index of all live previews, and `.nojekyll`
keeps the generated Unity files untouched. Re-pushing a branch cancels its older
in-progress build, while publish operations for different branches retry up to
five times when they race to update `gh-pages`.

Deleting a source branch removes its preview automatically. A manual workflow
run can publish an older branch, tag, or commit that did not contain the
workflow. Each successful build writes its finished URL to the Actions job
summary.

For this repository, a branch named `feature/combat-hud` is published at
`https://best-coder-open-now-near-me.github.io/grit-gud/preview/feature-combat-hud-d8d86cc36c0e/`.

One-time repository setup is required before the workflow can run:

1. Obtain a CI license file using the [GameCI activation
   guide](https://game.ci/docs/github/activation): generate and download the
   `.alf` manual activation request for Unity `6000.4.10f1`, upload that request
   at [Unity manual activation](https://license.unity3d.com/manual), select the
   license entitlement associated with the Unity account, and download the
   resulting `.ulf` file. The activation request should come from GameCI rather
   than a developer workstation because Unity licenses the machine that created
   the request.
2. Choose one GameCI activation strategy:
   - For a Unity Personal `.ulf`, create repository secrets named
     `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD`. Paste the complete
     `.ulf` contents, including its XML header and footer, into `UNITY_LICENSE`.
     GameCI needs the account credentials to activate the randomized machine ID
     used by its ephemeral container; the machine-bound `.ulf` is not a
     standalone API key.
   - To avoid storing Unity account credentials, provide a Unity Licensing
     Server and create only the `UNITY_LICENSING_SERVER` repository secret with
     its server URL. This requires the appropriate Unity floating/build-server
     licensing and network access from the GitHub runner.
   - Alternatively, use a trusted self-hosted runner with Unity already
     activated and replace the Docker-based GameCI builder with a direct Unity
     invocation.
3. Add the selected values in **Settings > Secrets and variables > Actions**.
   Treat license files, account credentials, and licensing-server URLs as
   secrets: do not commit them or upload them as public workflow artifacts.
4. In **Settings > Actions > General > Workflow permissions**, enable **Read and
   write permissions** so the workflow can maintain `gh-pages`.
5. After the first preview creates `gh-pages`, configure **Settings > Pages >
   Build and deployment** as **Deploy from a branch**, select `gh-pages`, and
   select `/ (root)`.
6. Push a non-`main` branch or run **Actions > Branch preview > Run workflow** to
   publish a preview.

The preview workflow intentionally runs on branch pushes rather than pull-request
events. Forks receive the separate source/contract CI checks but do not receive
license secrets, private assets, or Pages write access; a maintainer must push a
trusted contribution to a branch in this repository before it can run licensed
Unity coverage or publish a preview.

The Unity Linux image is large enough to exhaust a standard hosted runner before
Docker finishes unpacking it. The workflow removes unused preinstalled Android,
.NET, Haskell, and CodeQL toolchains and prunes stale Docker data before
pulling the builder image. The cleanup step prints disk usage before and after
removal so a future runner-image regression is visible in the job log.

After cleanup, the workflow restores Unity's generated `Library` directory from
the GitHub Actions cache. The cache key changes when the Unity version or package
lock changes, but not for an ordinary script or asset edit; Unity performs its
own incremental invalidation after restoration. The first successful build for
a new key still has to import the project and upload the cache, so the speedup
starts with the following run. If `actions/cache` reports that `Library` exceeds
GitHub's cache-size limit, a persistent self-hosted runner is the next practical
option and also avoids downloading the large Unity Docker image for every job.

Licensed source packages used by a preview are installed from a separate private
repository before Unity imports the project. The pinned ref, read-only
credential, expected layout, and local workflow are documented in
[PRIVATE_ASSETS.md](PRIVATE_ASSETS.md).
