# Supabase cloud saves

Supabase stores private, authenticated level-draft libraries and character
documents. It does not publish community levels or carry multiplayer traffic.

## Project setup

1. In Supabase, enable the Data API and automatic RLS. Keep automatic table
   exposure disabled; explicitly expose only the tables in the `public` schema
   when the migration has been reviewed.
2. In **Authentication > Providers**, enable Anonymous Sign-Ins. This gives a
   first-launch player an authenticated owner ID without showing sign-in UI.
3. Apply every migration in [`supabase/migrations`](../supabase/migrations) in
   filename order. Migration `005` adds stable draft IDs, names, revisions,
   history, and soft deletion. Migration `006` removes the legacy slot mutation
   API and makes the checked RPC functions the only level-draft write boundary.
4. In Unity, create **Assets > Create > Grit Gud > Supabase Configuration** and
   place the asset at `Assets/GritGud/Content/Resources/SupabaseConfiguration.asset`.
   Enter the project HTTPS URL and its **publishable** key from Supabase's
   Connect dialog. Never enter a secret/service-role key in Unity.

The project URL and publishable key identify a public client; access is secured
by the RLS policies and the user's JWT. The configuration asset is ignored so
each developer can choose their own project.

## Ownership and behavior

`LevelDraftLibraryService` owns the Unity-free use cases and contracts.
`SupabaseLevelDraftRepository` is the authenticated Presentation adapter.
`LevelDraftLibraryCoordinator` owns selection, cancellation, busy state, and
mutation sequencing for the menu and editor.

Each cloud draft has an immutable UUID, an editable unique-per-account name,
and an optimistic revision. Saves fail visibly instead of overwriting a newer
revision. Rename changes only the name; duplicate creates a new UUID; delete
soft-archives the row. Immutable revision rows retain saved snapshots.

The level editor's **SAVE LOCAL** and **LOAD LOCAL** buttons use PlayerPrefs as
same-device recovery. **CLOUD SAVE** creates a draft from the level display name
or revision-saves the currently opened cloud draft. **CLOUD LOAD** reloads that
exact UUID. The main menu lists all non-archived drafts for the current account.

Anonymous accounts remain recoverable only on the device holding their refresh
token. A later account-linking surface is required before promising cross-device
recovery. Transient refresh failures preserve the current anonymous identity.

The ignored configuration asset must be supplied to every release build by its
build environment/private asset installation. A clean checkout without that
asset intentionally runs with cloud features unavailable.
