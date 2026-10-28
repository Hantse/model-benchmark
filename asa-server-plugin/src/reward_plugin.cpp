#include "reward_plugin.hpp"

#include <cctype>
#include <map>
#include <string>

namespace asa_benchmark {

struct RewardPlugin::State {
    Host& host;
    Clock& clock;
    Config config;
    bool loaded = false;
    bool owns_registration = false;
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
    state_->owns_registration = true;
    state_->last_claim.clear();
    return true;
}

void RewardPlugin::Unload() {
    if (state_->owns_registration) {
        state_->host.UnregisterChat("/daily");
        state_->owns_registration = false;
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
    const std::int64_t cooldown = state_->config.cooldown_seconds;
    const auto found = state_->last_claim.find(player->eos_id);
    if (found != state_->last_claim.end()) {
        const std::int64_t last = found->second;
        if (now < last) {
            state_->host.Reply(*player, "Cooldown: " + std::to_string(cooldown) + " seconds");
            return;
        }
        const std::int64_t elapsed = now - last;
        if (elapsed < cooldown) {
            state_->host.Reply(*player, "Cooldown: " + std::to_string(cooldown - elapsed) + " seconds");
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
