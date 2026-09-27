REVOKE ALL ON FUNCTION public.profile_apply_chainling_passive_recovery_tx(UUID, TIMESTAMPTZ, INTEGER, INTEGER, INTEGER, BOOLEAN) FROM PUBLIC;
REVOKE ALL ON FUNCTION public.profile_apply_chainling_passive_recovery_tx(UUID, TIMESTAMPTZ, INTEGER, INTEGER, INTEGER, BOOLEAN) FROM anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_apply_chainling_passive_recovery_tx(UUID, TIMESTAMPTZ, INTEGER, INTEGER, INTEGER, BOOLEAN) TO service_role;

REVOKE ALL ON TABLE public.chainling_species_vitals FROM PUBLIC;
REVOKE ALL ON TABLE public.chainling_species_vitals FROM anon, authenticated;
GRANT ALL ON TABLE public.chainling_species_vitals TO service_role;;
