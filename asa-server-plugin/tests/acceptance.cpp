#include "reward_plugin.hpp"

#include <iostream>
#include <limits>
#include <map>
#include <set>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

using namespace asa_benchmark;

namespace {

void Check(bool condition, const std::string& message) {
    if (!condition) throw std::runtime_error(message);
}

struct FakeClock final : Clock {
    std::int64_t now = 100;
    std::int64_t NowSeconds() const override { return now; }
};

struct FakeHost final : Host {
    std::map<std::string, ChatHandler> commands;
    std::set<std::string> denied;
    std::vector<std::string> registrations;
    std::vector<std::string> removals;
    std::vector<std::pair<std::string, std::string>> permission_queries;
    std::vector<std::pair<std::string, int>> grants;
    std::vector<std::pair<std::string, std::string>> replies;
    bool grant_succeeds = true;
    int award_calls = 0;

    bool RegisterChat(const std::string& command, ChatHandler handler) override {
        registrations.push_back(command);
        return commands.emplace(command, std::move(handler)).second;
    }
    void UnregisterChat(const std::string& command) override {
        removals.push_back(command);
        commands.erase(command);
    }
    bool HasPermission(const std::string& eos_id, const std::string& permission) override {
        permission_queries.emplace_back(eos_id, permission);
        return denied.count(eos_id) == 0;
    }
    bool AddCredits(const std::string& eos_id, int amount) override {
        ++award_calls;
        if (!grant_succeeds) return false;
        grants.emplace_back(eos_id, amount);
        return true;
    }
    void Reply(const Player& player, const std::string& message) override {
        replies.emplace_back(player.eos_id, message);
    }
    void Dispatch(const Player* player, const std::string& text) {
        const auto found = commands.find("/daily");
        if (found != commands.end()) found->second(player, text);
    }
};

struct Fixture {
    FakeHost host;
    FakeClock clock;
    Player alice{"eos-alice"};
    Player bob{"eos-bob"};
    RewardPlugin plugin{host, clock};

    void Load() { Check(plugin.Load(), "valid configuration must load"); }
    void Claim() { plugin.HandleChat(&alice, "/daily"); }
    void LastReply(const std::string& expected) {
        Check(!host.replies.empty(), "expected a caller reply: " + expected);
        Check(host.replies.back() == std::make_pair(alice.eos_id, expected),
              "expected reply to Alice: " + expected);
    }
};

void Run(const std::string& name) {
    Fixture f;
    if (name == "load_registers_daily") {
        f.Load();
        Check(f.host.registrations == std::vector<std::string>{"/daily"}, "register exactly /daily once");
        Check(f.host.commands.count("/daily") == 1, "registered handler must be callable");
    } else if (name == "load_is_idempotent") {
        f.Load(); f.Load();
        Check(f.host.registrations.size() == 1, "second Load must not register again");
    } else if (name == "unload_removes_owned_command") {
        f.host.commands.emplace("/other", [](const Player*, const std::string&) {});
        f.Load(); f.plugin.Unload();
        Check(f.host.removals == std::vector<std::string>{"/daily"}, "remove the owned registration");
        Check(f.host.commands.count("/other") == 1 && f.host.commands.count("/daily") == 0,
              "unload must preserve unrelated commands");
    } else if (name == "unload_is_idempotent") {
        f.Load(); f.plugin.Unload(); f.plugin.Unload();
        Check(f.host.removals.size() == 1, "second Unload must not remove again");
    } else if (name == "destructor_removes_command") {
        { RewardPlugin scoped{f.host, f.clock}; Check(scoped.Load(), "scoped plugin must load"); }
        Check(f.host.removals == std::vector<std::string>{"/daily"}, "destructor must remove callback");
        Check(f.host.commands.empty(), "no callback may outlive its plugin");
    } else if (name == "failed_registration_preserves_existing_command") {
        int external_calls = 0;
        f.host.commands.emplace("/daily", [&external_calls](const Player*, const std::string&) { ++external_calls; });
        { RewardPlugin conflicting{f.host, f.clock};
          Check(!conflicting.Load(), "existing registration must reject load");
          conflicting.Unload(); }
        f.host.Dispatch(&f.alice, "/daily");
        Check(external_calls == 1 && f.host.removals.empty(), "failed load must not remove another owner's command");
    } else if (name == "invalid_amount_zero_rejected" || name == "invalid_amount_above_limit_rejected"
               || name == "invalid_cooldown_zero_rejected" || name == "invalid_cooldown_above_limit_rejected") {
        Config config;
        if (name == "invalid_amount_zero_rejected") config.reward_credits = 0;
        if (name == "invalid_amount_above_limit_rejected") config.reward_credits = 10001;
        if (name == "invalid_cooldown_zero_rejected") config.cooldown_seconds = 0;
        if (name == "invalid_cooldown_above_limit_rejected") config.cooldown_seconds = 86401;
        RewardPlugin invalid{f.host, f.clock, config};
        Check(!invalid.Load(), "out-of-range configuration must fail");
        Check(f.host.registrations.empty(), "validate config before host registration");
    } else if (name == "failed_load_can_retry_after_conflict") {
        f.host.commands.emplace("/daily", [](const Player*, const std::string&) {});
        Check(!f.plugin.Load(), "load must fail while command is owned elsewhere");
        f.host.commands.erase("/daily");
        f.Load(); f.host.Dispatch(&f.alice, "/daily");
        Check(f.host.grants.size() == 1, "same plugin may load after conflict is removed");
    } else if (name == "callback_dispatches_to_plugin") {
        f.Load(); f.host.Dispatch(&f.alice, "/daily");
        Check(f.host.grants == std::vector<std::pair<std::string, int>>{{"eos-alice", 250}},
              "registered callback must execute the reward handler");
    } else if (name == "valid_claim_grants_configured_amount") {
        RewardPlugin configured{f.host, f.clock, Config{417, 60}};
        Check(configured.Load(), "custom configuration must load");
        configured.HandleChat(&f.alice, "/daily");
        Check(f.host.grants == std::vector<std::pair<std::string, int>>{{"eos-alice", 417}},
              "use configured reward, exactly once");
        f.LastReply("Granted 417 credits");
    } else if (name == "valid_claim_replies_only_to_caller") {
        f.Load(); f.plugin.HandleChat(&f.bob, "/daily");
        Check(f.host.replies == std::vector<std::pair<std::string, std::string>>{{"eos-bob", "Granted 250 credits"}},
              "reply exactly once to the caller");
    } else if (name == "whitespace_trimmed") {
        f.Load(); f.plugin.HandleChat(&f.alice, "   /daily  ");
        Check(f.host.grants.size() == 1, "leading and trailing ASCII spaces are allowed");
        f.LastReply("Granted 250 credits");
    } else if (name == "unknown_command_rejected" || name == "arguments_rejected"
               || name == "command_case_sensitive" || name == "empty_input_rejected") {
        f.Load();
        const auto text = name == "unknown_command_rejected" ? "/dailybonus"
                        : name == "arguments_rejected" ? "/daily 100"
                        : name == "command_case_sensitive" ? "/Daily" : "   ";
        f.plugin.HandleChat(&f.alice, text);
        Check(f.host.grants.empty() && f.host.permission_queries.empty(),
              "invalid command must be rejected before permission and reward calls");
        f.LastReply("Usage: /daily");
        if (name == "empty_input_rejected") {
            f.plugin.HandleChat(&f.alice, "\t/daily");
            f.LastReply("Usage: /daily");
            f.plugin.HandleChat(&f.alice, "/daily\t");
            f.LastReply("Usage: /daily");
            Check(f.host.permission_queries.empty() && f.host.award_calls == 0,
                  "only literal ASCII spaces may be trimmed");
        }
    } else if (name == "null_player_ignored") {
        f.Load(); f.plugin.HandleChat(nullptr, "/daily");
        Check(f.host.permission_queries.empty() && f.host.award_calls == 0 && f.host.replies.empty(),
              "null caller must have no host effects");
    } else if (name == "missing_eos_ignored") {
        f.Load(); Player missing{""}; f.plugin.HandleChat(&missing, "/daily");
        Check(f.host.permission_queries.empty() && f.host.award_calls == 0 && f.host.replies.empty(),
              "missing stable identity must have no host effects");
    } else if (name == "permission_denied_no_award") {
        f.Load(); f.host.denied.insert(f.alice.eos_id); f.Claim();
        Check(f.host.award_calls == 0, "denied callers must never reach award operation");
        f.LastReply("Permission denied");
    } else if (name == "permission_uses_eos_and_required_key") {
        f.Load(); f.Claim();
        Check(f.host.permission_queries == std::vector<std::pair<std::string, std::string>>{{"eos-alice", "reward.claim"}},
              "permission lookup must use caller EOS ID and reward.claim");
    } else if (name == "unloaded_handler_ignored") {
        f.Claim();
        Check(f.host.registrations.empty() && f.host.permission_queries.empty()
              && f.host.award_calls == 0 && f.host.replies.empty(), "unloaded handler must be inert");
    } else if (name == "second_claim_blocked") {
        f.Load(); f.Claim(); f.Claim();
        Check(f.host.award_calls == 1, "same-second retry must not grant twice");
        f.LastReply("Cooldown: 60 seconds");
    } else if (name == "just_before_boundary_blocked") {
        f.Load(); f.Claim(); f.clock.now = 159; f.Claim();
        Check(f.host.award_calls == 1, "one second before boundary is blocked");
        f.LastReply("Cooldown: 1 seconds");
        Fixture large;
        large.Load();
        large.clock.now = std::numeric_limits<std::int64_t>::max() - 30;
        large.Claim();
        large.clock.now = std::numeric_limits<std::int64_t>::max();
        large.Claim();
        Check(large.host.award_calls == 1, "cooldown arithmetic must not overflow near maximum timestamp");
        large.LastReply("Cooldown: 30 seconds");
    } else if (name == "boundary_allows_claim") {
        f.Load(); f.Claim(); f.clock.now = 160; f.Claim();
        Check(f.host.grants.size() == 2, "exact cooldown boundary permits next award");
        f.LastReply("Granted 250 credits");
    } else if (name == "independent_players") {
        f.Load(); f.Claim(); f.plugin.HandleChat(&f.bob, "/daily");
        Check(f.host.grants == std::vector<std::pair<std::string, int>>{{"eos-alice", 250}, {"eos-bob", 250}},
              "one player's claim must not block another");
    } else if (name == "reconnect_same_eos_blocked") {
        f.Load(); f.Claim(); Player reconnected{"eos-alice"};
        f.plugin.HandleChat(&reconnected, "/daily");
        Check(f.host.award_calls == 1, "cooldown identity is EOS ID, not player pointer");
        f.LastReply("Cooldown: 60 seconds");
    } else if (name == "invalid_command_does_not_start_cooldown") {
        f.Load(); f.plugin.HandleChat(&f.alice, "/daily bad"); f.Claim();
        Check(f.host.grants.size() == 1, "malformed input must not consume eligibility");
        f.LastReply("Granted 250 credits");
    } else if (name == "denied_permission_does_not_start_cooldown") {
        f.Load(); f.host.denied.insert(f.alice.eos_id); f.Claim();
        f.host.denied.clear(); f.Claim();
        Check(f.host.grants.size() == 1 && f.host.award_calls == 1, "denial must not consume eligibility");
        f.LastReply("Granted 250 credits");
    } else if (name == "failed_award_does_not_start_cooldown") {
        f.Load(); f.host.grant_succeeds = false; f.Claim();
        f.LastReply("Reward unavailable");
        f.host.grant_succeeds = true; f.Claim();
        Check(f.host.grants.size() == 1 && f.host.award_calls == 2, "failed grant must remain immediately retryable");
        f.LastReply("Granted 250 credits");
    } else if (name == "rollback_does_not_bypass_cooldown") {
        f.Load(); f.Claim(); f.clock.now = 90; f.Claim();
        Check(f.host.award_calls == 1, "backwards time must not grant another reward");
        f.LastReply("Cooldown: 60 seconds");
        f.clock.now = 159; f.Claim(); f.LastReply("Cooldown: 1 seconds");
    } else if (name == "clock_zero_can_start_cooldown") {
        f.Load(); f.clock.now = 0; f.Claim(); f.Claim();
        Check(f.host.award_calls == 1, "zero timestamp must not be mistaken for no previous claim");
        f.LastReply("Cooldown: 60 seconds");
    } else if (name == "success_advances_cooldown") {
        f.Load(); f.Claim(); f.clock.now = 160; f.Claim(); f.clock.now = 170; f.Claim();
        Check(f.host.award_calls == 2, "new success must advance the cooldown start");
        f.LastReply("Cooldown: 50 seconds");
    } else if (name == "unload_then_reload_clears_state") {
        f.Load(); f.Claim(); f.plugin.Unload();
        const auto query_count = f.host.permission_queries.size();
        const auto reply_count = f.host.replies.size();
        f.Claim();
        Check(f.host.award_calls == 1, "handler must be inert after Unload");
        Check(f.host.permission_queries.size() == query_count && f.host.replies.size() == reply_count,
              "unloaded handler must not query permissions or reply");
        f.Load(); f.Claim();
        Check(f.host.grants.size() == 2, "new lifecycle starts with empty cooldown state");
        Check(f.host.registrations.size() == 2, "reload must acquire a new registration");
    } else {
        throw std::runtime_error("unknown protected acceptance case: " + name);
    }
}

} // namespace

int main(int argc, char** argv) {
    if (argc != 2) {
        std::cerr << "Expected one acceptance-case name\n";
        return 2;
    }
    try {
        Run(argv[1]);
        std::cout << "PASS " << argv[1] << '\n';
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "FAIL " << argv[1] << ": " << error.what() << '\n';
        return 1;
    }
}
