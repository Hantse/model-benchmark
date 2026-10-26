#pragma once

#include <cstdint>
#include <functional>
#include <memory>
#include <string>

// A deliberately narrow, portable benchmark port. These are NOT AsaApi SDK
// declarations, Unreal types, an ABI shim, or a loadable ASA plugin.
namespace asa_benchmark {

struct Player {
    std::string eos_id;
};

struct Config {
    int reward_credits = 250;
    std::int64_t cooldown_seconds = 60;
};

class Host {
public:
    using ChatHandler = std::function<void(const Player*, const std::string&)>;
    virtual ~Host() = default;
    // False means another owner already holds this command. No replacement.
    virtual bool RegisterChat(const std::string& command, ChatHandler handler) = 0;
    virtual void UnregisterChat(const std::string& command) = 0;
    virtual bool HasPermission(const std::string& eos_id, const std::string& permission) = 0;
    // False guarantees no credit was granted; retry is safe in this test port.
    virtual bool AddCredits(const std::string& eos_id, int amount) = 0;
    virtual void Reply(const Player& player, const std::string& message) = 0;
};

class Clock {
public:
    virtual ~Clock() = default;
    // The host supplies nonnegative whole seconds. Tests can move time backwards.
    virtual std::int64_t NowSeconds() const = 0;
};

class RewardPlugin {
public:
    RewardPlugin(Host& host, Clock& clock, Config config = {});
    ~RewardPlugin();
    RewardPlugin(const RewardPlugin&) = delete;
    RewardPlugin& operator=(const RewardPlugin&) = delete;
    RewardPlugin(RewardPlugin&&) = delete;
    RewardPlugin& operator=(RewardPlugin&&) = delete;

    bool Load();
    void Unload();
    void HandleChat(const Player* player, const std::string& text);

private:
    struct State;
    std::unique_ptr<State> state_;
};

} // namespace asa_benchmark
