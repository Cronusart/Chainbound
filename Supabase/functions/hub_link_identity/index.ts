import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { handleHub } from "../_shared/hub_edge.ts";

serve((req) => handleHub(req, "link"));
