#include "support.hpp"
#include "allocation_probe.hpp"

#include <array>
#include <limits>
#include <memory>

namespace {
struct Observation {
    std::size_t events, chats, timers, permissions, awards, queries, replies, clock;
    bool operator==(const Observation&) const = default;
};
Observation observe(const FakeHost& host, const FakeClock& clock) {
    return {host.events.size(), host.chats.size(), host.timers.size(), host.permissions.size(),
            host.awards.size(), host.queries.size(), host.replies.size(), clock.calls};
}
void fire(const std::vector<ChatHandler>& chats, const std::vector<TimerHandler>& timers,
          const Player& player) {
    for (const auto& callback : chats) {
        callback(&player, "/daily"); callback(&player, "/daily-status");
    }
    for (const auto& callback : timers) callback();
}
std::vector<Config> invalid_configs() {
    std::vector<Config> result;
    for (const auto& ns : std::vector<std::string>{"", std::string(33, 'a'), "bad namespace", "bad/name",
            "caf\xC3\xA9", std::string("a\0b", 3), std::string(1, static_cast<char>(0xFF))}) {
        Config config; config.operation_namespace = ns; result.push_back(config);
    }
    for (int field = 0; field < 5; ++field) {
        for (bool high : {false, true}) {
            Config config;
            if (field == 0) config.reward_credits = high ? 10001 : 0;
            if (field == 1) config.cooldown_seconds = high ? 86401 : 0;
            if (field == 2) config.retry_seconds = high ? 3601 : 0;
            if (field == 3) config.max_attempts = high ? 11 : 0;
            if (field == 4) config.pending_limit = high ? 1025 : 0;
            result.push_back(config);
        }
    }
    return result;
}
}

std::vector<Case> lifecycle_cases() {
    return {
        {"lifecycle_construction_is_host_and_clock_free", [] {
            FakeHost host; FakeClock clock; clock.now = 999;
            Config config; config.operation_namespace = "bad/name";
            {
                RewardPlugin plugin(host, clock, config);
                REQUIRE(!plugin.IsLoaded());
                const auto saved = plugin.Save();
                REQUIRE(saved.config == config && saved.observed_time == 0 && saved.next_operation == 1);
                REQUIRE(saved.players.empty() && host.events.empty() && clock.calls == 0);
            }
            REQUIRE(host.events.empty() && clock.calls == 0);
        }},
        {"lifecycle_invalid_config_rejects_before_any_registration", [] {
            for (const auto& config : invalid_configs()) {
                Fixture f(config); const auto before = f.plugin.Save();
                REQUIRE(!f.plugin.Load() && !f.plugin.IsLoaded());
                REQUIRE(f.host.events.empty() && f.host.captured_chats.empty() && f.host.captured_timers.empty());
                REQUIRE(f.clock.calls == 0 && f.plugin.Save() == before);
            }
        }},
        {"lifecycle_valid_config_numeric_and_ascii_boundaries", [] {
            Config minimum; minimum.operation_namespace = "A"; minimum.reward_credits = 1;
            minimum.cooldown_seconds = minimum.retry_seconds = minimum.max_attempts = 1;
            minimum.pending_limit = 1;
            Config maximum; maximum.operation_namespace = "Az_09-" + std::string(26, 'Z');
            maximum.reward_credits = 10000; maximum.cooldown_seconds = 86400;
            maximum.retry_seconds = 3600; maximum.max_attempts = 10; maximum.pending_limit = 1024;
            for (const auto& config : {minimum, maximum}) {
                Fixture f(config); f.load(); f.daily(); f.reply("Granted " + std::to_string(config.reward_credits) + " credits");
                REQUIRE(f.host.awards.front().operation == config.operation_namespace + ":1");
                REQUIRE(f.record().claim.cooldown_seconds == config.cooldown_seconds);
            }
        }},
        {"lifecycle_load_registers_commands_then_timer", [] {
            Fixture f; const auto before = f.plugin.Save(); f.load();
            REQUIRE(f.plugin.IsLoaded());
            REQUIRE(f.host.events == std::vector<std::string>{"register:/daily", "register:/daily-status", "register:timer"});
            REQUIRE(f.host.chats.size() == 2 && f.host.timers.size() == 1);
            REQUIRE(f.clock.calls == 0 && f.plugin.Save() == before);
        }},
        {"lifecycle_load_is_idempotent_without_state_reset", [] {
            Fixture f; f.load(); f.daily(); const auto saved = f.plugin.Save();
            const auto before = observe(f.host, f.clock);
            for (int n = 0; n < 20; ++n) REQUIRE(f.plugin.Load());
            REQUIRE(f.plugin.IsLoaded() && f.plugin.Save() == saved);
            REQUIRE(observe(f.host, f.clock) == before);
        }},
        {"lifecycle_unloaded_chat_and_tick_ignore_everything", [] {
            Fixture f; f.clock.now = 400;
            const auto saved = f.plugin.Save();
            f.daily(); f.status(); f.plugin.HandleChat(nullptr, "/daily"); f.plugin.Tick(); f.plugin.Unload();
            REQUIRE(!f.plugin.IsLoaded() && f.plugin.Save() == saved);
            REQUIRE(observe(f.host, f.clock) == Observation{0, 0, 0, 0, 0, 0, 0, 0});
        }},
        {"lifecycle_unload_removes_owned_handles_in_reverse_order", [] {
            Fixture f; f.load(); f.clock.now = 10; f.daily(); f.plugin.Unload();
            REQUIRE(!f.plugin.IsLoaded() && f.host.chats.empty() && f.host.timers.empty());
            REQUIRE(f.host.events == std::vector<std::string>{"register:/daily", "register:/daily-status", "register:timer",
                "remove-timer:3", "remove-chat:2", "remove-chat:1"});
            const auto saved = f.plugin.Save();
            REQUIRE(saved.players.empty() && saved.observed_time == 10 && saved.next_operation == 2);
        }},
        {"lifecycle_repeated_unload_has_no_host_or_clock_calls", [] {
            Fixture f; f.load(); f.daily(); const auto restored = f.plugin.Save(); f.plugin.Unload();
            REQUIRE(f.plugin.Restore(restored));
            const auto saved = f.plugin.Save(); const auto before = observe(f.host, f.clock);
            for (int n = 0; n < 20; ++n) { f.plugin.Unload(); f.plugin.Tick(); f.daily(); }
            REQUIRE(f.plugin.Save() == saved && observe(f.host, f.clock) == before);
        }},
        {"lifecycle_first_registration_failure_acquires_nothing", [] {
            Fixture donor; donor.load(); donor.daily();
            Fixture f; REQUIRE(f.plugin.Restore(donor.plugin.Save()));
            f.host.fail_chat.insert("/daily"); const auto saved = f.plugin.Save();
            REQUIRE(!f.plugin.Load() && !f.plugin.IsLoaded());
            REQUIRE(f.host.events == std::vector<std::string>{"register:/daily"});
            REQUIRE(f.host.chats.empty() && f.host.timers.empty());
            REQUIRE(f.plugin.Save() == saved && f.clock.calls == 0);
        }},
        {"lifecycle_second_registration_failure_rolls_back_first", [] {
            Fixture f; f.host.fail_chat.insert("/daily-status"); const auto saved = f.plugin.Save();
            REQUIRE(!f.plugin.Load());
            REQUIRE(f.host.events == std::vector<std::string>{"register:/daily", "register:/daily-status", "remove-chat:1"});
            REQUIRE(f.host.chats.empty() && f.host.timers.empty() && !f.plugin.IsLoaded());
            REQUIRE(f.plugin.Save() == saved && f.clock.calls == 0);
        }},
        {"lifecycle_timer_failure_rolls_back_both_commands", [] {
            Fixture f; f.host.fail_timer = true; const auto saved = f.plugin.Save();
            REQUIRE(!f.plugin.Load());
            REQUIRE(f.host.events == std::vector<std::string>{"register:/daily", "register:/daily-status", "register:timer",
                "remove-chat:2", "remove-chat:1"});
            REQUIRE(f.host.chats.empty() && f.host.timers.empty());
            REQUIRE(!f.plugin.IsLoaded() && f.plugin.Save() == saved && f.clock.calls == 0);
        }},
        {"lifecycle_foreign_daily_registration_is_preserved", [] {
            Fixture f; int called = 0;
            const auto foreign = f.host.RegisterChat("/daily", [&](const Player*, const std::string&) { ++called; });
            REQUIRE(foreign.has_value()); REQUIRE(!f.plugin.Load()); f.plugin.Unload();
            REQUIRE(f.host.chats.size() == 1 && f.host.chats.at("/daily").first == *foreign);
            f.host.chats.at("/daily").second(&f.alice, "/daily"); REQUIRE(called == 1);
            REQUIRE(f.host.events == std::vector<std::string>{"register:/daily", "register:/daily"});
        }},
        {"lifecycle_foreign_status_conflict_removes_only_owned_daily", [] {
            Fixture f; int called = 0;
            const auto foreign = f.host.RegisterChat("/daily-status", [&](const Player*, const std::string&) { ++called; });
            REQUIRE(foreign.has_value()); REQUIRE(!f.plugin.Load());
            REQUIRE(f.host.chats.size() == 1 && f.host.chats.at("/daily-status").first == *foreign);
            REQUIRE(f.host.events == std::vector<std::string>{"register:/daily-status", "register:/daily",
                "register:/daily-status", "remove-chat:2"});
            f.host.chats.at("/daily-status").second(&f.alice, "/daily-status"); REQUIRE(called == 1);
        }},
        {"lifecycle_foreign_timer_and_unrelated_chat_survive_cleanup", [] {
            Fixture f; int timer_calls = 0, chat_calls = 0;
            const auto timer = f.host.RegisterTimer([&] { ++timer_calls; });
            const auto chat = f.host.RegisterChat("/foreign", [&](const Player*, const std::string&) { ++chat_calls; });
            f.load(); f.plugin.Unload();
            REQUIRE(f.host.timers.size() == 1 && f.host.timers.contains(*timer));
            REQUIRE(f.host.chats.size() == 1 && f.host.chats.at("/foreign").first == *chat);
            f.host.timers.at(*timer)(); f.host.chats.at("/foreign").second(&f.alice, "hello");
            REQUIRE(timer_calls == 1 && chat_calls == 1 && f.clock.calls == 0);
        }},
        {"lifecycle_load_can_retry_after_partial_registration_failure", [] {
            Fixture f; f.host.fail_chat.insert("/daily-status"); REQUIRE(!f.plugin.Load());
            f.host.fail_chat.clear(); f.load(); f.daily();
            REQUIRE(f.host.chats.size() == 2 && f.host.timers.size() == 1);
            REQUIRE(f.host.awards.size() == 1 && f.host.awards[0].operation == "server:1");
            REQUIRE(f.plugin.Save().next_operation == 2);
        }},
        {"lifecycle_rollback_callbacks_stay_inert_after_retry", [] {
            Fixture f; f.host.fail_timer = true; REQUIRE(!f.plugin.Load());
            const auto old_chats = f.host.captured_chats; const auto old_timers = f.host.captured_timers;
            auto before = observe(f.host, f.clock); auto saved = f.plugin.Save();
            fire(old_chats, old_timers, f.alice);
            REQUIRE(observe(f.host, f.clock) == before && f.plugin.Save() == saved);
            f.host.fail_timer = false; f.load(); f.clock.now = 40;
            before = observe(f.host, f.clock); saved = f.plugin.Save(); fire(old_chats, old_timers, f.alice);
            REQUIRE(observe(f.host, f.clock) == before && f.plugin.Save() == saved);
            f.host.chats.at("/daily").second(&f.alice, "/daily");
            REQUIRE(f.host.awards.size() == 1 && f.record().last_success_at == 40);
        }},
        {"lifecycle_unloaded_saved_callbacks_are_inert", [] {
            Fixture f; f.load(); const auto chats = f.host.captured_chats; const auto timers = f.host.captured_timers;
            f.plugin.Unload(); f.clock.now = 80;
            const auto before = observe(f.host, f.clock); const auto saved = f.plugin.Save();
            fire(chats, timers, f.alice);
            REQUIRE(observe(f.host, f.clock) == before && f.plugin.Save() == saved);
        }},
        {"lifecycle_new_load_does_not_reactivate_old_callbacks", [] {
            Fixture f; f.load(); const auto old_chats = f.host.captured_chats; const auto old_timers = f.host.captured_timers;
            f.daily(); f.plugin.Unload(); f.load(); f.clock.now = 9;
            const auto before = observe(f.host, f.clock); const auto saved = f.plugin.Save();
            fire(old_chats, old_timers, f.alice);
            REQUIRE(observe(f.host, f.clock) == before && f.plugin.Save() == saved);
            f.host.chats.at("/daily").second(&f.alice, "/daily");
            REQUIRE(f.host.awards.back().operation == "server:2" && f.record().last_success_at == 9);
        }},
        {"lifecycle_saved_callbacks_are_safe_after_destruction", [] {
            FakeHost host; FakeClock clock; Player player{"alice"};
            {
                RewardPlugin plugin(host, clock, {}); REQUIRE(plugin.Load());
                plugin.HandleChat(&player, "/daily");
            }
            REQUIRE(host.chats.empty() && host.timers.empty());
            const auto before = observe(host, clock); clock.now = 100;
            fire(host.captured_chats, host.captured_timers, player);
            REQUIRE(observe(host, clock) == before && host.ledger.size() == 1);
        }},
        {"lifecycle_destructor_uses_reverse_owned_cleanup", [] {
            FakeHost host; FakeClock clock;
            { RewardPlugin plugin(host, clock, {}); REQUIRE(plugin.Load()); }
            REQUIRE(host.events == std::vector<std::string>{"register:/daily", "register:/daily-status", "register:timer",
                "remove-timer:3", "remove-chat:2", "remove-chat:1"});
            REQUIRE(host.chats.empty() && host.timers.empty() && clock.calls == 0);
        }},
        {"lifecycle_resource_ownership_balances_across_success_and_rollback", [] {
            const auto before = live_allocations();
            for (int round = 0; round < 24; ++round) {
                FakeHost host; FakeClock clock; Player player{"alice"};
                {
                    RewardPlugin plugin(host, clock, {});
                    host.fail_timer = true; REQUIRE(!plugin.Load());
                    host.fail_timer = false; REQUIRE(plugin.Load());
                    plugin.HandleChat(&player, "/daily");
                    plugin.Unload(); REQUIRE(plugin.Load());
                }
                REQUIRE(host.chats.empty() && host.timers.empty());
                const auto inert = observe(host, clock);
                fire(host.captured_chats, host.captured_timers, player);
                REQUIRE(observe(host, clock) == inert);
            }
            REQUIRE(live_allocations() == before);
        }},
        {"lifecycle_registered_callbacks_forward_chat_and_timer", [] {
            Fixture f; f.load(); f.host.award_results = {AwardResult::Rejected, AwardResult::Granted};
            f.host.chats.at("/daily").second(&f.alice, "/daily"); f.reply("Pending server:1");
            f.host.chats.at("/daily-status").second(&f.alice, "/daily-status"); f.reply("Pending server:1");
            const auto reply_count = f.host.replies.size(); f.clock.now = 5;
            f.host.timers.begin()->second();
            REQUIRE(f.host.awards.size() == 2 && f.record().claim.status == ClaimStatus::Granted);
            REQUIRE(f.host.replies.size() == reply_count && f.clock.calls == 2);
        }},
        {"lifecycle_reload_requires_loaded_instance", [] {
            Fixture f; Config changed; changed.reward_credits = 777;
            auto saved = f.plugin.Save(); REQUIRE(!f.plugin.Reload(changed));
            REQUIRE(f.plugin.Save() == saved && f.host.events.empty() && f.clock.calls == 0);
            f.load(); f.plugin.Unload(); saved = f.plugin.Save(); const auto before = observe(f.host, f.clock);
            REQUIRE(!f.plugin.Reload(changed)); REQUIRE(f.plugin.Save() == saved && observe(f.host, f.clock) == before);
        }},
        {"lifecycle_reload_invalid_config_is_fully_atomic", [] {
            Fixture f; f.load(); f.daily(); const auto saved = f.plugin.Save();
            const auto before = observe(f.host, f.clock);
            for (const auto& config : invalid_configs()) {
                REQUIRE(!f.plugin.Reload(config)); REQUIRE(f.plugin.Save() == saved);
                REQUIRE(observe(f.host, f.clock) == before && f.plugin.IsLoaded());
            }
        }},
        {"lifecycle_reload_cannot_change_operation_namespace", [] {
            Fixture f; f.load(); f.daily(); auto config = f.plugin.Save().config;
            config.operation_namespace = "different";
            const auto saved = f.plugin.Save(); const auto before = observe(f.host, f.clock);
            REQUIRE(!f.plugin.Reload(config));
            REQUIRE(f.plugin.Save() == saved && observe(f.host, f.clock) == before);
            f.clock.now = 60; f.daily(); REQUIRE(f.host.awards.back().operation == "server:2");
        }},
        {"lifecycle_reload_updates_config_without_host_clock_or_state_loss", [] {
            Fixture f; f.load(); f.host.award_results = {AwardResult::Rejected}; f.daily();
            const auto saved = f.plugin.Save(); const auto before = observe(f.host, f.clock);
            auto changed = saved.config; changed.reward_credits = 999; changed.cooldown_seconds = 120;
            changed.retry_seconds = 11; changed.max_attempts = 7; changed.pending_limit = 2;
            REQUIRE(f.plugin.Reload(changed)); const auto after = f.plugin.Save();
            REQUIRE(after.config == changed && after.players == saved.players);
            REQUIRE(after.observed_time == saved.observed_time && after.next_operation == saved.next_operation);
            REQUIRE(observe(f.host, f.clock) == before && f.plugin.IsLoaded());
        }},
        {"lifecycle_reload_pending_limit_counts_pending_and_uncertain", [] {
            Config config; config.pending_limit = 3; Fixture f(config); f.load();
            f.host.unknown_commits = false;
            f.host.award_results = {AwardResult::Rejected, AwardResult::Unknown}; f.daily(); f.daily(&f.bob);
            REQUIRE(f.record().claim.status == ClaimStatus::Pending && f.record("bob").claim.status == ClaimStatus::Uncertain);
            Player terminal{"carol"}; f.daily(&terminal);
            REQUIRE(f.record("carol").claim.status == ClaimStatus::Granted);
            auto changed = config; changed.pending_limit = 1;
            const auto saved = f.plugin.Save(); const auto before = observe(f.host, f.clock);
            REQUIRE(!f.plugin.Reload(changed)); REQUIRE(f.plugin.Save() == saved && observe(f.host, f.clock) == before);
            changed.pending_limit = 2; REQUIRE(f.plugin.Reload(changed));
            REQUIRE(f.plugin.Save().players == saved.players && observe(f.host, f.clock) == before);
        }},
        {"lifecycle_reload_keeps_all_captured_pending_economy_parameters", [] {
            Fixture f; f.load(); f.host.award_results = {AwardResult::Rejected}; f.daily();
            const auto admitted = f.record().claim; Config changed;
            changed.reward_credits = 200; changed.cooldown_seconds = 90;
            changed.retry_seconds = 7; changed.max_attempts = 5;
            REQUIRE(f.plugin.Reload(changed)); REQUIRE(f.record().claim == admitted);
            f.daily(&f.bob); REQUIRE(f.host.awards.back().credits == 200);
            REQUIRE(f.record("bob").claim.retry_seconds == 7 && f.record("bob").claim.max_attempts == 5);
            f.clock.now = 5; f.plugin.Tick();
            REQUIRE(f.host.awards.back().eos == "alice" && f.host.awards.back().credits == 100);
            REQUIRE(f.record().last_cooldown_seconds == 60 && f.record().claim.max_attempts == 3);
            REQUIRE(f.record().last_success_at == 5 && f.record("bob").last_cooldown_seconds == 90);
        }},
        {"lifecycle_reload_preserves_cooldown_captured_by_previous_success", [] {
            Fixture f; f.load(); f.daily(); Config changed;
            changed.reward_credits = 200; changed.cooldown_seconds = 1;
            REQUIRE(f.plugin.Reload(changed)); f.clock.now = 1; f.daily(); f.reply("Cooldown: 59 seconds");
            REQUIRE(f.host.awards.size() == 1);
            f.clock.now = 60; f.daily(); f.reply("Granted 200 credits");
            REQUIRE(f.record().last_cooldown_seconds == 1 && f.host.awards.back().operation == "server:2");
        }},
        {"lifecycle_invalid_player_identity_has_no_host_or_clock_actions", [] {
            Fixture f; f.load(); const auto before = observe(f.host, f.clock); const auto saved = f.plugin.Save();
            f.plugin.HandleChat(nullptr, "/daily"); f.plugin.HandleChat(nullptr, "/daily-status");
            for (const auto& id : std::vector<std::string>{"", "bad id", "bad/id", "bad.id", std::string(65, 'A'),
                    "caf\xC3\xA9", std::string("a\0b", 3), std::string(1, static_cast<char>(0xE9))}) {
                Player player{id}; f.plugin.HandleChat(&player, "/daily");
                f.plugin.HandleChat(&player, "/daily-status"); f.plugin.HandleChat(&player, "nonsense");
            }
            REQUIRE(observe(f.host, f.clock) == before && f.plugin.Save() == saved);
        }},
        {"lifecycle_parsing_trims_ascii_spaces_only_for_exact_commands", [] {
            Fixture f; f.load(); Player player{std::string(58, 'A') + "_z-09Z"};
            f.plugin.HandleChat(&player, "   /daily   "); f.reply("Granted 100 credits", player.eos_id);
            REQUIRE(f.host.permissions.back() == std::make_pair(player.eos_id, std::string("reward.claim")));
            f.plugin.HandleChat(&player, " /daily-status "); f.reply("Granted server:1", player.eos_id);
            REQUIRE(f.host.permissions.back() == std::make_pair(player.eos_id, std::string("reward.status")));
            REQUIRE(f.clock.calls == 1 && f.host.awards.size() == 1);
        }},
        {"lifecycle_invalid_command_usage_does_not_check_permission_or_time", [] {
            Fixture f; f.load(); const auto saved = f.plugin.Save();
            for (const auto& command : {"", " ", "daily", "/Daily", "/daily extra", "/daily-status extra",
                                       "\t/daily", "/daily\t", "\n/daily", "/daily-status\n", "/daily-statusx"}) {
                const auto before = observe(f.host, f.clock);
                f.plugin.HandleChat(&f.alice, command); f.reply("Usage: /daily | /daily-status");
                const auto after = observe(f.host, f.clock);
                REQUIRE(after.events == before.events && after.permissions == before.permissions);
                REQUIRE(after.clock == before.clock && after.awards == before.awards && after.queries == before.queries);
                REQUIRE(after.replies == before.replies + 1 && f.plugin.Save() == saved);
            }
        }},
        {"lifecycle_permissions_are_command_specific_and_denials_stop", [] {
            Fixture f; f.load(); f.host.denied.insert({"alice", "reward.claim"});
            f.host.denied.insert({"alice", "reward.status"}); const auto saved = f.plugin.Save();
            f.daily(); f.reply("Permission denied"); f.status(); f.reply("Permission denied");
            REQUIRE(f.host.permissions == std::vector<std::pair<std::string, std::string>>{
                {"alice", "reward.claim"}, {"alice", "reward.status"}});
            REQUIRE(f.host.awards.empty() && f.host.queries.empty() && f.clock.calls == 0 && f.plugin.Save() == saved);
            f.host.denied.erase({"alice", "reward.status"}); f.status(); f.reply("No claim");
            REQUIRE(f.clock.calls == 0 && f.plugin.Save() == saved);
        }},
        {"lifecycle_status_reads_all_claim_states_without_sampling_time", [] {
            Fixture f; f.load(); Player carol{"carol"}, stranger{"stranger"};
            f.host.award_results = {AwardResult::Rejected, AwardResult::Unknown, AwardResult::Granted};
            f.daily(); f.daily(&f.bob); f.daily(&carol);
            auto before = observe(f.host, f.clock); const auto saved = f.plugin.Save(); f.clock.now = 999;
            f.status(); f.reply("Pending server:1"); f.status(&f.bob); f.reply("Pending server:2", "bob");
            f.status(&carol); f.reply("Granted server:3", "carol"); f.status(&stranger); f.reply("No claim", "stranger");
            auto after = observe(f.host, f.clock);
            REQUIRE(after.clock == before.clock && after.awards == before.awards && after.queries == before.queries);
            REQUIRE(after.permissions == before.permissions + 4 && f.plugin.Save() == saved);
            Config one; one.max_attempts = 1; Fixture failed(one); failed.load();
            failed.host.award_results = {AwardResult::Rejected}; failed.daily();
            before = observe(failed.host, failed.clock); const auto terminal = failed.plugin.Save();
            failed.status(); failed.reply("Failed server:1"); after = observe(failed.host, failed.clock);
            REQUIRE(after.clock == before.clock && after.awards == before.awards && after.queries == before.queries);
            REQUIRE(failed.plugin.Save() == terminal);
        }},
        {"lifecycle_reconnect_uses_exact_case_sensitive_eos_identity", [] {
            Fixture f; f.load(); f.daily(); Player reconnected{"alice"}, different{"Alice"};
            f.clock.now = 7; f.daily(&reconnected); f.reply("Cooldown: 53 seconds");
            REQUIRE(f.host.awards.size() == 1); f.daily(&different); f.reply("Granted 100 credits", "Alice");
            REQUIRE(f.host.awards.size() == 2 && f.host.awards.back().eos == "Alice");
            REQUIRE(f.plugin.Save().players.size() == 2 && f.record("alice").last_success_at == 0);
            REQUIRE(f.record("Alice").last_success_at == 7);
        }},
        {"lifecycle_instances_isolate_callbacks_configuration_and_history", [] {
            Config config; config.operation_namespace = "second"; config.reward_credits = 500;
            Fixture first, second(config); first.load(); second.load();
            first.clock.now = 4; second.clock.now = 9;
            first.host.chats.at("/daily").second(&first.alice, "/daily");
            second.host.chats.at("/daily").second(&second.alice, "/daily");
            REQUIRE(first.host.awards[0].operation == "server:1" && first.host.awards[0].credits == 100);
            REQUIRE(second.host.awards[0].operation == "second:1" && second.host.awards[0].credits == 500);
            const auto saved = second.plugin.Save(); const auto before = observe(second.host, second.clock);
            first.plugin.Unload(); first.plugin.Tick(); first.daily();
            REQUIRE(second.plugin.IsLoaded() && second.plugin.Save() == saved);
            REQUIRE(observe(second.host, second.clock) == before);
            second.host.chats.at("/daily-status").second(&second.alice, "/daily-status");
            second.reply("Granted second:1");
        }}
    };
}
