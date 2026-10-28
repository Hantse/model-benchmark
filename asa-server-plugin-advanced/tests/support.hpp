#pragma once
#include "reward_plugin.hpp"
#include <deque>
#include <map>
#include <set>
#include <stdexcept>
#include <utility>

using namespace benchmark;
using Case = std::pair<std::string, std::function<void()>>;
#define REQUIRE(...) do { if (!(__VA_ARGS__)) throw std::runtime_error( \
    std::string("line ") + std::to_string(__LINE__) + ": " #__VA_ARGS__); } while (false)

struct FakeClock final : Clock {
    std::uint64_t now = 0;
    mutable std::size_t calls = 0;
    std::uint64_t NowSeconds() const override { ++calls; return now; }
};
struct AwardCall {
    std::string eos;
    std::uint32_t credits;
    std::string operation;
    bool operator==(const AwardCall&) const = default;
};
struct FakeHost final : Host {
    std::uint64_t next_handle = 1;
    std::map<std::string, std::pair<std::uint64_t, ChatHandler>> chats;
    std::map<std::uint64_t, TimerHandler> timers;
    std::vector<ChatHandler> captured_chats;
    std::vector<TimerHandler> captured_timers;
    std::vector<std::string> events;
    std::set<std::string> fail_chat;
    bool fail_timer = false;
    bool unknown_commits = true;
    std::set<std::pair<std::string, std::string>> denied;
    std::deque<AwardResult> award_results;
    std::deque<QueryResult> query_results;
    std::vector<AwardCall> awards;
    std::vector<std::string> queries;
    std::vector<std::pair<std::string, std::string>> permissions;
    std::vector<std::pair<std::string, std::string>> replies;
    std::map<std::string, AwardCall> ledger;

    std::optional<std::uint64_t> RegisterChat(const std::string& command, ChatHandler cb) override {
        events.push_back("register:" + command); captured_chats.push_back(cb);
        if (fail_chat.contains(command) || chats.contains(command)) return std::nullopt;
        auto handle = next_handle++; chats.emplace(command, std::make_pair(handle, std::move(cb)));
        return handle;
    }
    std::optional<std::uint64_t> RegisterTimer(TimerHandler cb) override {
        events.push_back("register:timer"); captured_timers.push_back(cb);
        if (fail_timer) return std::nullopt;
        auto handle = next_handle++; timers.emplace(handle, std::move(cb)); return handle;
    }
    void RemoveChat(std::uint64_t handle) override {
        events.push_back("remove-chat:" + std::to_string(handle));
        for (auto it = chats.begin(); it != chats.end(); ++it)
            if (it->second.first == handle) { chats.erase(it); return; }
    }
    void RemoveTimer(std::uint64_t handle) override {
        events.push_back("remove-timer:" + std::to_string(handle)); timers.erase(handle);
    }
    bool HasPermission(const std::string& eos, const std::string& permission) override {
        permissions.emplace_back(eos, permission); return !denied.contains({eos, permission});
    }
    AwardResult AddCredits(const std::string& eos, std::uint32_t credits,
                           const std::string& operation) override {
        awards.push_back({eos, credits, operation});
        if (ledger.contains(operation)) return AwardResult::Granted;
        auto answer = AwardResult::Granted;
        if (!award_results.empty()) { answer = award_results.front(); award_results.pop_front(); }
        if (answer == AwardResult::Granted || (answer == AwardResult::Unknown && unknown_commits))
            ledger.emplace(operation, awards.back());
        return answer;
    }
    QueryResult QueryAward(const std::string& operation) override {
        queries.push_back(operation);
        if (!query_results.empty()) { auto answer = query_results.front(); query_results.pop_front(); return answer; }
        return ledger.contains(operation) ? QueryResult::Granted : QueryResult::Absent;
    }
    void Reply(const std::string& eos, const std::string& text) override { replies.emplace_back(eos, text); }
};
struct Fixture {
    FakeHost host;
    FakeClock clock;
    Player alice{"alice"}, bob{"bob"};
    RewardPlugin plugin;
    explicit Fixture(Config config = {}) : plugin(host, clock, std::move(config)) {}
    void load() { REQUIRE(plugin.Load()); }
    void daily(const Player* player = nullptr) { plugin.HandleChat(player ? player : &alice, "/daily"); }
    void status(const Player* player = nullptr) { plugin.HandleChat(player ? player : &alice, "/daily-status"); }
    void reply(const std::string& text, const std::string& eos = "alice") const {
        REQUIRE(!host.replies.empty()); REQUIRE(host.replies.back() == std::make_pair(eos, text));
    }
    PlayerRecord record(const std::string& eos = "alice") const {
        auto saved = plugin.Save();
        for (const auto& record : saved.players) if (record.eos_id == eos) return record;
        throw std::runtime_error("record not found: " + eos);
    }
};

std::vector<Case> lifecycle_cases();
std::vector<Case> economy_cases();
std::vector<Case> recovery_cases();
