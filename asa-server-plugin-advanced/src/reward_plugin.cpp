#include "reward_plugin.hpp"
#include <utility>

namespace benchmark {
// Deliberately incomplete, compiling starter: implement the three features.
struct RewardPlugin::State {};
RewardPlugin::RewardPlugin(Host&, Clock&, Config) : state_(std::make_shared<State>()) {}
RewardPlugin::~RewardPlugin() = default;
bool RewardPlugin::Load() { return false; }
void RewardPlugin::Unload() {}
bool RewardPlugin::Reload(const Config&) { return false; }
bool RewardPlugin::IsLoaded() const { return false; }
void RewardPlugin::HandleChat(const Player*, const std::string&) {}
void RewardPlugin::Tick() {}
Snapshot RewardPlugin::Save() const { return {}; }
bool RewardPlugin::Restore(const Snapshot&) { return false; }
} // namespace benchmark
