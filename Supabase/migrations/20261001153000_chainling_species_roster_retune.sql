-- Align saved species ids and the recovery HP catalogue with the
-- 2026-10-01 roster rename and point pass.
-- Battle stats still live on SpeciesDefinitionSO. This table is only base HP
-- for server-side passive recovery.

UPDATE public.chainlings SET species_id = 'forkrook' WHERE species_id = 'caw';
UPDATE public.chainlings SET species_id = 'keyper' WHERE species_id = 'ghosty';
UPDATE public.chainlings SET species_id = 'crackett' WHERE species_id = 'lad';
UPDATE public.chainlings SET species_id = 'rattlestake' WHERE species_id = 'snek';
UPDATE public.chainlings SET species_id = 'humpacked' WHERE species_id = 'whale';

UPDATE public.chainling_species_vitals SET species_id = 'forkrook' WHERE species_id = 'caw';
UPDATE public.chainling_species_vitals SET species_id = 'keyper' WHERE species_id = 'ghosty';
UPDATE public.chainling_species_vitals SET species_id = 'crackett' WHERE species_id = 'lad';
UPDATE public.chainling_species_vitals SET species_id = 'rattlestake' WHERE species_id = 'snek';
UPDATE public.chainling_species_vitals SET species_id = 'humpacked' WHERE species_id = 'whale';

INSERT INTO public.chainling_species_vitals (species_id, base_max_hp, catalog_version)
VALUES
    ('keyper', 102, 2),
    ('balloom', 130, 2),
    ('blockwyrm', 168, 2),
    ('cinislug', 134, 2),
    ('crackett', 112, 2),
    ('deghoul', 106, 2),
    ('forkrook', 104, 2),
    ('gilarime', 161, 2),
    ('hopoch', 120, 2),
    ('humpacked', 128, 2),
    ('kitsauto', 139, 2),
    ('kodiax', 124, 2),
    ('kreechank', 124, 2),
    ('marrowhead', 108, 2),
    ('minefolk', 124, 2),
    ('rattlestake', 110, 2),
    ('validape', 156, 2)
ON CONFLICT (species_id) DO UPDATE
SET base_max_hp = EXCLUDED.base_max_hp,
    catalog_version = public.chainling_species_vitals.catalog_version + 1,
    updated_at = now();
