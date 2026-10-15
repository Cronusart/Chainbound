-- Quest reward item references. 20261014030000_fisher_ape_lucky_crayon declares
-- these as inline REFERENCES, but environments that applied an earlier revision
-- of that migration created chainbound_quest_rewards without them. Add the same
-- default-named constraints only when missing so this is safe everywhere.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conrelid = 'public.chainbound_quest_rewards'::regclass
           AND conname = 'chainbound_quest_rewards_consume_item_id_fkey'
    ) THEN
        ALTER TABLE public.chainbound_quest_rewards
            ADD CONSTRAINT chainbound_quest_rewards_consume_item_id_fkey
            FOREIGN KEY (consume_item_id)
            REFERENCES public.chainbound_item_catalog(item_id);
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conrelid = 'public.chainbound_quest_rewards'::regclass
           AND conname = 'chainbound_quest_rewards_grant_item_id_fkey'
    ) THEN
        ALTER TABLE public.chainbound_quest_rewards
            ADD CONSTRAINT chainbound_quest_rewards_grant_item_id_fkey
            FOREIGN KEY (grant_item_id)
            REFERENCES public.chainbound_item_catalog(item_id);
    END IF;
END;
$$;
