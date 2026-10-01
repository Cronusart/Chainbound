-- Public, immutable-addressed metadata/media for Stamped Core assets.
-- Edge Functions write with the service role; clients only consume public URLs.
INSERT INTO storage.buckets (
    id,
    name,
    public,
    file_size_limit,
    allowed_mime_types
)
VALUES (
    'chainbound-ownership-metadata',
    'chainbound-ownership-metadata',
    true,
    10485760,
    ARRAY['application/json', 'image/png']::text[]
)
ON CONFLICT (id) DO UPDATE
SET name = EXCLUDED.name,
    public = EXCLUDED.public,
    file_size_limit = EXCLUDED.file_size_limit,
    allowed_mime_types = EXCLUDED.allowed_mime_types;

