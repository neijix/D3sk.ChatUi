// AgentCliBridge's chat box (vendor/acb, the /acb scripts D3SKHAND's hub loads), in the hub's load
// order. They are classic scripts that publish globalThis.AgentModels and globalThis.AgentUI; as ES
// modules they run once, in this order, and nothing else of theirs leaks out.
import "../../vendor/acb/model_names.js";
import "../../vendor/acb/model_ranking.js";
import "../../vendor/acb/model_tags.js";
import "../../vendor/acb/model_picker.js";
import "../../vendor/acb/model_catalog.js";
import "../../vendor/acb/model_effort.js";
import "../../vendor/acb/model_agents.js";
import "../../vendor/acb/models.js";
import "../../vendor/acb/composer_menu.js";
import "../../vendor/acb/composer.js";
import "../../vendor/acb/icons.js";
import "../../vendor/acb/permission_modes.js";
import "../../vendor/acb/settings_models.js";
import "../../vendor/acb/settings_agents.js";
import "../../vendor/acb/components.js";

export const AgentUI = globalThis.AgentUI;
export const AgentModels = globalThis.AgentModels;
