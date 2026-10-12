import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { handlePvp } from "../_shared/pvp_edge.ts";

serve((req) => handlePvp(req, "matchmaker"));
