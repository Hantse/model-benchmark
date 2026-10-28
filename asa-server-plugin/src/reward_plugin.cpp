#include "reward_plugin.hpp"

#include <map>
#include <string>

namespace asa_benchmark {

struct RewardPlugin::State {
    Host& host;
    Clock& clock;
    Config config;
    bool loaded = false;
    bool owns_command = false;
    std::map<std::string, std::int64_t> last_claim;
};

RewardPlugin::RewardPlugin(Host& host, Clock& clock, Config config)
    : state_(std::make_unique<State>(State{host, clock, config})) {}

RewardPlugin::~RewardPlugin() { Unload(); }

bool RewardPlugin::Load() {
    if (state_->loaded) return true;
    if (state_->config.reward_credits < 1 || state_->config.reward_credits > 10000) return false;
    if (state_->config.cooldown_seconds < 1 || state_->config.cooldown_seconds > 86400) return false;
    if (!state_->host.RegisterChat("/daily", [this](const Player* player, const std::string& text) {
            HandleChat(player, text);
        })) {
        return false;
    }
    state_->loaded = true;
    state_->owns_command = true;
    state_->last_claim.clear();
    return true;
}

void RewardPlugin::Unload() {
    if (state_->owns_command) {
        state_->host.UnregisterChat("/daily");
        state_->owns_command = false;
    }
    state_->loaded = false;
    state_->last_claim.clear();
}

void RewardPlugin::HandleChat(const Player* player, const std::string& text) {
    if (!state_->loaded) return;
    if (player == nullptr || player->eos_id.empty()) return;

    // Trim leading and trailing ASCII spaces only.
    std::size_t begin = 0;
    std::size_t end = text.size();
    while (begin < end && text[begin] == ' ') ++begin;
    while (end > begin && text[end - 1] == ' ') --end;
    const std::string command = text.substr(begin, end - begin);

    if (command != "/daily") {
        state_->host.Reply(*player, "Usage: /daily");
        return;
    }

    if (!state_->host.HasPermission(player->eos_id, "reward.claim")) {
        state_->host.Reply(*player, "Permission denied");
        return;
    }

    const std::int64_t now = state_->clock.NowSeconds();
    const auto it = state_->last_claim.find(player->eos_id);
    if (it != state_->last_claim.end()) {
        const std::int64_t last = it->second;
        if (now < last) {
            state_->host.Reply(*player, "Cooldown: " + std::to_string(state_->config.cooldown_seconds) + " seconds");
            return;
        }
        const std::int64_t elapsed = now - last;
        if (elapsed < state_->config.cooldown_seconds) {
            const std::int64_t remaining = state_->config.cooldown_seconds - elapsed;
            state_->host.Reply(*player, "Cooldown: " + std::to_string(remaining) + " seconds");
            return;
        }
    }

    if (!state_->host.AddCredits(player->eos_id, state_->config.reward_credits)) {
        state_->host.Reply(*player, "Reward unavailable");
        return;
    }

    state_->last_claim[player->eos_id] = now;
    state_->host.Reply(*player, "Granted " + std::to_string(state_->config.reward_credits) + " credits");
}

} // namespace asa_benchmark
