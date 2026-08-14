# Private Build Assets

The public `grit-gud` repository contains gameplay, project-owned Unity assets,
and build automation. Editable third-party source packages live in a separate
private repository and are installed only on licensed workstations and trusted
build runners.

Compiled Unity players may contain referenced licensed content. Raw FBX files,
textures, source archives, demo projects, and other editable package contents
must not be copied to the `gh-pages` branch or another public artifact.

## Private repository layout

The shared private repository is
`best-coder-open-now-near-me/private-assets`. Grit Gud owns only its namespaced
subdirectory, which mirrors the portion of the Unity project that it owns:

```text
grit-gud/
  .grit-gud-private-assets
  Assets/
    DoubleL.meta
    DoubleL/
    Basic Shooter Pack.meta
    Basic Shooter Pack/
    Input Sprites for TextMesh Pro.meta
    Input Sprites for TextMesh Pro/
    Kevin Iglesias.meta
    Kevin Iglesias/
    Mixamo.meta
    Mixamo/
    Modern GDR - Free icons pack.meta
    Modern GDR - Free icons pack/
    UCLAGameLab.meta
    UCLAGameLab/
    Synty.meta
    Synty/
```

The empty `.grit-gud-private-assets` sentinel prevents the workflow from
overlaying an accidentally selected repository. Every asset must retain its
Unity `.meta` file so GUID references remain stable on developer machines and
build runners.

The overlay holds animation, Synty environment, input-icon, UI-icon, and
wireframe shader source packages. Licensed source assets must be added here
before project-owned catalogs or generated assets reference them.

## GitHub configuration

Configure this value in **Settings > Secrets and variables > Actions**:

| Kind | Name | Value |
| --- | --- | --- |
| Secret | `PRIVATE_ASSETS` | Fine-grained token with read-only Contents access to that repository. |

The tracked `.github/private-assets-ref` file selects the asset branch, tag, or
commit. It is pinned to the reviewed private-asset commit used by this source
branch so the build is reproducible.

The token must not have access to unrelated repositories or write permission.
Because the preview workflow runs on trusted branch pushes rather than fork pull
requests, the secret is not exposed to untrusted fork builds.

During a build, the workflow:

1. checks out public source normally;
2. checks out the configured private repository into a temporary hidden folder;
3. validates its sentinel and `Assets` layout;
4. overlays its `Assets` contents into the Unity project;
5. removes the temporary checkout, including its Git metadata and credentials;
6. keys Unity's import cache with the installed asset revision; and
7. publishes only the generated `Builds/Web` output.

## Local development

Until an installer script is added, clone or copy the private repository's
`grit-gud/Assets` contents over the public project's `Assets` directory. Do not
regenerate or discard its `.meta` files. The public repository ignores the known
private animation package paths, preventing a broad `git add` from publishing
them.

When the private repository becomes the source of truth, updates should be made
there first. The public repository's `.github/private-assets-ref` should then
advance in a reviewable change so a source commit and licensed-asset revision
identify one reproducible build.
