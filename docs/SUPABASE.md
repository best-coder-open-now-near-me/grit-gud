# Supabase cloud saves

This first integration stores private, authenticated level drafts and character
documents. It does not replace the local draft stores, publish community levels,
or carry multiplayer traffic.

## Project setup

1. In Supabase, enable the Data API and automatic RLS. Keep automatic table
   exposure disabled; explicitly expose only the tables in the `public` schema
   when the migration has been reviewed.
2. In **Authentication > Providers**, enable Anonymous Sign-Ins. This gives a
   first-launch player an authenticated owner ID without showing sign-in UI.
3. Run [`202608150001_cloud_documents.sql`](../supabase/migrations/202608150001_cloud_documents.sql)
   in the SQL Editor (or apply it through the Supabase CLI when it is installed).
4. In Unity, create **Assets > Create > Grit Gud > Supabase Configuration** and
   place the asset at `Assets/GritGud/Content/Resources/SupabaseConfiguration.asset`.
   Enter the project HTTPS URL and its **publishable** key from Supabase's
   Connect dialog. Never enter a secret/service-role key in Unity.

The project URL and publishable key identify a public client; access is secured
by the RLS policies and the user's JWT. The configuration asset is ignored so
each developer can choose their own project.

## Current integration boundary

`SupabaseClient.SignInAnonymously` creates a session. Pass that session to
`SupabaseDocumentStore.SaveLevelDraft` or `SaveCharacter` from a MonoBehaviour
coroutine. Existing PlayerPrefs stores remain the fast offline fallback until
the editor and character UI add explicit cloud save/load controls.
