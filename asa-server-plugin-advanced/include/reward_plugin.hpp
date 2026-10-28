#pragma once
#include <cstddef>
#include <cstdint>
#include <functional>
#include <memory>
#include <optional>
#include <string>
#include <vector>

namespace benchmark {
struct Player { std::string eos_id; };
struct Config {
    std::string operation_namespace = "server";
    std::uint32_t reward_credits = 100;
    std::uint32_t cooldown_seconds = 60;
    std::uint32_t retry_seconds = 5;
    std::uint32_t max_attempts = 3;
    std::size_t pending_limit = 64;
    bool operator==(const Config&) const = default;
};
enum class AwardResult { Granted, Rejected, Unknown };
enum class QueryResult { Granted, Absent, Unknown };
enum class ClaimStatus { Pending, Uncertain, Granted, Failed };
using ChatHandler = std::function<void(const Player*, const std::string&)>;
using TimerHandler = std::function<void()>;
class Clock {
public:
    virtual ~Clock() = default;
    virtual std::uint64_t NowSeconds() const = 0;
};
class Host {
public:
    virtual ~Host() = default;
    virtual std::optional<std::uint64_t> RegisterChat(const std::string&, ChatHandler) = 0;
    virtual std::optional<std::uint64_t> RegisterTimer(TimerHandler) = 0;
    virtual void RemoveChat(std::uint64_t handle) = 0;
    virtual void RemoveTimer(std::uint64_t handle) = 0;
    virtual bool HasPermission(const std::string& eos_id, const std::string& permission) = 0;
    virtual AwardResult AddCredits(const std::string& eos_id, std::uint32_t credits,
                                   const std::string& operation_id) = 0;
    virtual QueryResult QueryAward(const std::string& operation_id) = 0;
    virtual void Reply(const std::string& eos_id, const std::string& text) = 0;
};
struct Claim {
    std::uint64_t operation = 0;
    std::uint32_t credits = 0;
    std::uint32_t cooldown_seconds = 0;
    std::uint32_t retry_seconds = 0;
    std::uint32_t max_attempts = 0;
    std::uint32_t attempts = 0;
    ClaimStatus status = ClaimStatus::Pending;
    std::uint64_t created_at = 0;
    std::uint64_t ready_at = 0;
    std::optional<std::uint64_t> last_processed_at;
    std::optional<std::uint64_t> resolved_at;
    bool operator==(const Claim&) const = default;
};
struct PlayerRecord {
    std::string eos_id;
    std::optional<std::uint64_t> last_success_at;
    std::uint32_t last_cooldown_seconds = 0;
    Claim claim;
    bool operator==(const PlayerRecord&) const = default;
};
struct Snapshot {
    Config config;
    std::uint64_t observed_time = 0;
    std::uint64_t next_operation = 1;
    std::vector<PlayerRecord> players;
    bool operator==(const Snapshot&) const = default;
};
class RewardPlugin {
public:
    RewardPlugin(Host&, Clock&, Config);
    ~RewardPlugin();
    RewardPlugin(const RewardPlugin&) = delete;
    RewardPlugin& operator=(const RewardPlugin&) = delete;
    bool Load();
    void Unload();
    bool Reload(const Config&);
    bool IsLoaded() const;
    void HandleChat(const Player*, const std::string&);
    void Tick();
    Snapshot Save() const;
    bool Restore(const Snapshot&);
private:
    struct State;
    std::shared_ptr<State> state_;
};
} // namespace benchmark
