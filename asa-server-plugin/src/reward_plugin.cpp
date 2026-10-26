#include "reward_plugin.hpp"

namespace asa_benchmark {

struct RewardPlugin::State {
    Host& host;
    Clock& clock;
    Config config;
};

RewardPlugin::RewardPlugin(Host& host, Clock& clock, Config config)
    : state_(std::make_unique<State>(State{host, clock, config})) {}

RewardPlugin::~RewardPlugin() = default;

bool RewardPlugin::Load() {
    // TODO: validate configuration and acquire the command registration.
    return false;
}

void RewardPlugin::Unload() {
    // TODO: release only this plugin's registration and reset its state.
}

void RewardPlugin::HandleChat(const Player* player, const std::string& text) {
    // TODO: validate the caller and command, check permission, and grant a reward
    // subject to a per-EOS-ID cooldown driven by the injected clock.
    (void)player;
    (void)text;
}

} // namespace asa_benchmark
