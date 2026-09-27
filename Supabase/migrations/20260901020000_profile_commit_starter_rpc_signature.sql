-- Keep one PostgREST-visible starter RPC signature. The three-argument
-- implementation already has a default for p_display_name, so a separate
-- two-argument wrapper makes two-argument Edge calls ambiguous (PGRST203).

DROP FUNCTION IF EXISTS public.profile_commit_starter_tx(UUID, TEXT);

REVOKE ALL ON FUNCTION public.profile_commit_starter_tx(UUID, TEXT, TEXT) FROM PUBLIC;
REVOKE ALL ON FUNCTION public.profile_commit_starter_tx(UUID, TEXT, TEXT) FROM anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_commit_starter_tx(UUID, TEXT, TEXT) TO service_role;

COMMENT ON FUNCTION public.profile_commit_starter_tx(UUID, TEXT, TEXT) IS
'Service-role starter commit. p_display_name is optional through its default; keep this as the sole PostgREST signature.';

NOTIFY pgrst, 'reload schema';
