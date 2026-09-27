-- Align visual identity JSONB keys with current player-facing animation terms.
-- Existing rows may still contain legacy client keys; normalize them in-place.

update public.chainlings
set visual_identity =
  jsonb_strip_nulls(
    jsonb_build_object(
      'aura_id', coalesce(visual_identity->>'aura_id', 'none'),
      'hud_card_variant_id', coalesce(nullif(visual_identity->>'hud_card_variant_id', ''), nullif(visual_identity->>'nameplate_frame_id', ''), 'default'),
      'swap_in_fx_id', coalesce(nullif(visual_identity->>'swap_in_fx_id', ''), nullif(visual_identity->>'spawn_fx_id', ''), 'default'),
      'swap_out_fx_id', coalesce(nullif(visual_identity->>'swap_out_fx_id', ''), nullif(visual_identity->>'ko_swap_fx_id', ''), 'default'),
      'ko_fx_id', coalesce(nullif(visual_identity->>'ko_fx_id', ''), nullif(visual_identity->>'defender_ko_fx_id', ''), 'none'),
      'victory_pose_id', coalesce(visual_identity->>'victory_pose_id', 'default'),
      'idle_style_id', coalesce(visual_identity->>'idle_style_id', 'none'),
      'idle_flourish_id', coalesce(visual_identity->>'idle_flourish_id', 'none'),
      'melee_attack_style_id', coalesce(visual_identity->>'melee_attack_style_id', 'none'),
      'ranged_attack_style_id', coalesce(visual_identity->>'ranged_attack_style_id', 'none'),
      'hit_reaction_style_id', coalesce(visual_identity->>'hit_reaction_style_id', 'none'),
      'attacker_ko_style_id', coalesce(nullif(visual_identity->>'attacker_ko_style_id', ''), nullif(visual_identity->>'enemy_ko_style_id', ''), 'none')
    )
  )
where visual_identity is not null;
