#include "support.hpp"

#include <iomanip>
#include <limits>
#include <sstream>

namespace {
constexpr auto MAX_TIME = std::numeric_limits<std::uint64_t>::max();

std::string eos(std::size_t number) {
    std::ostringstream out;
    out << 'p' << std::setw(5) << std::setfill('0') << number;
    return out.str();
}

Claim captured(const Config& config, std::uint64_t operation) {
    Claim claim;
    claim.operation = operation;
    claim.credits = config.reward_credits;
    claim.cooldown_seconds = config.cooldown_seconds;
    claim.retry_seconds = config.retry_seconds;
    claim.max_attempts = config.max_attempts;
    return claim;
}

Snapshot retained(const Config& config, std::size_t count) {
    Snapshot saved;
    saved.config = config;
    saved.observed_time = config.cooldown_seconds;
    saved.next_operation = count + 1;
    for (std::size_t i = 0; i < count; ++i) {
        PlayerRecord record;
        record.eos_id = eos(i);
        record.last_success_at = 0;
        record.last_cooldown_seconds = config.cooldown_seconds;
        record.claim = captured(config, i + 1);
        record.claim.attempts = 1;
        record.claim.status = ClaimStatus::Granted;
        record.claim.last_processed_at = 0;
        record.claim.resolved_at = 0;
        saved.players.push_back(std::move(record));
    }
    return saved;
}
} // namespace

std::vector<Case> economy_cases() {
    return {
        {"economy_immediate_grant_captures_claim_at_zero", [] {
            Fixture f; f.load(); f.daily(); f.reply("Granted 100 credits");
            REQUIRE(f.host.awards == std::vector<AwardCall>{{"alice", 100, "server:1"}});
            REQUIRE(f.host.queries.empty() && f.host.ledger.size() == 1);
            const auto record = f.record();
            REQUIRE(record.claim.status == ClaimStatus::Granted && record.claim.attempts == 1);
            REQUIRE(record.claim.operation == 1 && record.claim.created_at == 0 && record.claim.ready_at == 0);
            REQUIRE(record.claim.last_processed_at == 0 && record.claim.resolved_at == 0);
            REQUIRE(record.last_success_at == 0 && record.last_cooldown_seconds == 60);
            REQUIRE(record.claim.credits == 100 && record.claim.retry_seconds == 5);
            REQUIRE(record.claim.max_attempts == 3 && record.claim.cooldown_seconds == 60);
            REQUIRE(f.plugin.Save().next_operation == 2 && f.clock.calls == 1);
        }},
        {"economy_observed_time_never_rewinds", [] {
            Fixture f; f.load(); f.clock.now = 100; f.daily();
            f.clock.now = 20; f.daily(&f.bob);
            REQUIRE(f.record("bob").claim.created_at == 100 && f.record("bob").last_success_at == 100);
            REQUIRE(f.plugin.Save().observed_time == 100);
            f.clock.now = 159; f.daily(); f.reply("Cooldown: 1 seconds");
            f.clock.now = 1; f.daily(); f.reply("Cooldown: 1 seconds");
            REQUIRE(f.host.awards.size() == 2 && f.plugin.Save().observed_time == 159);
        }},
        {"economy_cooldown_exact_boundary", [] {
            Fixture f; f.load(); f.clock.now = 7; f.daily();
            f.clock.now = 66; f.daily(); f.reply("Cooldown: 1 seconds");
            REQUIRE(f.host.awards.size() == 1 && f.plugin.Save().next_operation == 2);
            f.clock.now = 67; f.daily(); f.reply("Granted 100 credits");
            REQUIRE(f.host.awards.size() == 2 && f.host.awards.back().operation == "server:2");
            REQUIRE(f.record().last_success_at == 67);
        }},
        {"economy_cooldown_identity_survives_reconnect_and_is_case_sensitive", [] {
            Fixture f; f.load(); f.daily();
            Player reconnected{"alice"}, different_case{"Alice"};
            f.clock.now = 1; f.daily(&reconnected); f.reply("Cooldown: 59 seconds");
            f.daily(&f.bob); f.reply("Granted 100 credits", "bob");
            f.daily(&different_case); f.reply("Granted 100 credits", "Alice");
            REQUIRE(f.host.awards.size() == 3 && f.plugin.Save().players.size() == 3);
            REQUIRE(f.host.awards[1].eos == "bob" && f.host.awards[2].eos == "Alice");
        }},
        {"economy_granted_claim_keeps_captured_cooldown_after_reload", [] {
            Fixture f; f.load(); f.daily();
            auto config = f.plugin.Save().config; config.cooldown_seconds = 1;
            REQUIRE(f.plugin.Reload(config));
            f.clock.now = 1; f.daily(); f.reply("Cooldown: 59 seconds");
            REQUIRE(f.host.awards.size() == 1 && f.record().last_cooldown_seconds == 60);
            f.clock.now = 60; f.daily();
            REQUIRE(f.record().claim.cooldown_seconds == 1 && f.record().last_cooldown_seconds == 1);
        }},
        {"economy_rejected_award_retries_with_stable_key", [] {
            Config config; config.operation_namespace = "realm_A"; config.reward_credits = 17;
            Fixture f(config); f.host.award_results = {AwardResult::Rejected}; f.load(); f.daily();
            f.reply("Pending realm_A:1");
            auto record = f.record();
            REQUIRE(record.claim.status == ClaimStatus::Pending && record.claim.attempts == 1);
            REQUIRE(record.claim.ready_at == 5 && !record.last_success_at && !record.claim.resolved_at);
            f.clock.now = 5; f.plugin.Tick();
            REQUIRE(f.host.awards == std::vector<AwardCall>{{"alice", 17, "realm_A:1"}, {"alice", 17, "realm_A:1"}});
            REQUIRE(f.record().claim.status == ClaimStatus::Granted && f.record().claim.attempts == 2);
            REQUIRE(f.host.ledger.size() == 1 && f.plugin.Save().next_operation == 2);
        }},
        {"economy_rejected_awards_stop_at_captured_attempt_limit", [] {
            Config config; config.max_attempts = 2;
            Fixture f(config); f.host.award_results = {AwardResult::Rejected, AwardResult::Rejected};
            f.load(); f.daily(); f.clock.now = 5; f.plugin.Tick();
            const auto record = f.record();
            REQUIRE(record.claim.status == ClaimStatus::Failed && record.claim.attempts == 2);
            REQUIRE(record.claim.resolved_at == 5 && record.claim.last_processed_at == 5);
            REQUIRE(!record.last_success_at && record.last_cooldown_seconds == 0 && f.host.ledger.empty());
            f.clock.now = 500; f.plugin.Tick();
            REQUIRE(f.host.awards.size() == 2 && f.host.queries.empty());
        }},
        {"economy_definitive_failure_allows_new_operation_without_cooldown", [] {
            Config config; config.max_attempts = 1;
            Fixture f(config); f.host.award_results = {AwardResult::Rejected}; f.load(); f.daily();
            f.reply("Reward unavailable");
            REQUIRE(f.record().claim.status == ClaimStatus::Failed);
            f.daily(); f.reply("Granted 100 credits");
            REQUIRE(f.host.awards.size() == 2 && f.host.awards.back().operation == "server:2");
            REQUIRE(f.record().claim.operation == 2 && f.record().last_success_at == 0);
        }},
        {"economy_unknown_committed_award_is_queried_without_regrant", [] {
            Fixture f; f.host.award_results = {AwardResult::Unknown}; f.load(); f.daily();
            f.reply("Pending server:1");
            REQUIRE(f.record().claim.status == ClaimStatus::Uncertain && f.host.ledger.size() == 1);
            f.clock.now = 5; f.plugin.Tick();
            REQUIRE(f.host.awards.size() == 1 && f.host.queries == std::vector<std::string>{"server:1"});
            REQUIRE(f.record().claim.status == ClaimStatus::Granted && f.record().claim.attempts == 1);
            REQUIRE(f.record().last_success_at == 5 && f.host.ledger.size() == 1);
        }},
        {"economy_unknown_absent_waits_then_retries_same_operation", [] {
            Fixture f; f.host.unknown_commits = false; f.host.award_results = {AwardResult::Unknown};
            f.load(); f.daily(); f.clock.now = 5; f.plugin.Tick();
            REQUIRE(f.host.queries == std::vector<std::string>{"server:1"} && f.host.awards.size() == 1);
            REQUIRE(f.record().claim.status == ClaimStatus::Pending && f.record().claim.attempts == 1);
            REQUIRE(f.record().claim.ready_at == 10 && f.host.ledger.empty());
            f.clock.now = 9; f.plugin.Tick(); REQUIRE(f.host.awards.size() == 1);
            f.clock.now = 10; f.plugin.Tick();
            REQUIRE(f.host.awards.size() == 2 && f.host.awards[0] == f.host.awards[1]);
            REQUIRE(f.record().claim.status == ClaimStatus::Granted && f.host.ledger.size() == 1);
        }},
        {"economy_unknown_queries_do_not_consume_attempts", [] {
            Fixture f; f.host.award_results = {AwardResult::Unknown};
            f.host.query_results = {QueryResult::Unknown, QueryResult::Unknown, QueryResult::Granted};
            f.load(); f.daily();
            for (std::uint64_t tick : {5ULL, 10ULL}) {
                f.clock.now = tick; f.plugin.Tick();
                const auto claim = f.record().claim;
                REQUIRE(claim.status == ClaimStatus::Uncertain && claim.attempts == 1);
                REQUIRE(claim.ready_at == tick + 5 && claim.last_processed_at == tick && !claim.resolved_at);
            }
            f.clock.now = 15; f.plugin.Tick();
            REQUIRE(f.record().claim.status == ClaimStatus::Granted && f.record().claim.attempts == 1);
            REQUIRE(f.host.awards.size() == 1 && f.host.queries.size() == 3 && f.host.ledger.size() == 1);
        }},
        {"economy_uncertain_last_attempt_can_still_confirm_grant", [] {
            Config config; config.max_attempts = 1;
            Fixture f(config); f.host.award_results = {AwardResult::Unknown};
            f.host.query_results = {QueryResult::Unknown, QueryResult::Granted};
            f.load(); f.daily(); f.clock.now = 5; f.plugin.Tick();
            REQUIRE(f.record().claim.status == ClaimStatus::Uncertain && f.record().claim.attempts == 1);
            f.clock.now = 10; f.plugin.Tick();
            REQUIRE(f.record().claim.status == ClaimStatus::Granted && f.record().claim.attempts == 1);
            REQUIRE(f.host.awards.size() == 1 && f.host.queries.size() == 2);
        }},
        {"economy_authoritative_absent_exhausted_attempt_resolves_failed", [] {
            Config config; config.max_attempts = 1;
            Fixture f(config); f.host.unknown_commits = false;
            f.host.award_results = {AwardResult::Unknown}; f.load(); f.daily();
            f.clock.now = 8; f.plugin.Tick();
            const auto record = f.record();
            REQUIRE(record.claim.status == ClaimStatus::Failed && record.claim.attempts == 1);
            REQUIRE(record.claim.resolved_at == 8 && record.claim.last_processed_at == 8);
            REQUIRE(!record.last_success_at && f.host.awards.size() == 1 && f.host.queries.size() == 1);
            f.clock.now = 13; f.plugin.Tick(); REQUIRE(f.host.queries.size() == 1);
        }},
        {"economy_duplicate_pending_command_only_replies_and_observes_time", [] {
            Fixture f; f.host.award_results = {AwardResult::Rejected}; f.load(); f.daily();
            const auto pending = f.record(); f.clock.now = 100; f.daily(); f.reply("Pending server:1");
            REQUIRE(f.record() == pending && f.plugin.Save().observed_time == 100);
            REQUIRE(f.host.awards.size() == 1 && f.host.queries.empty() && f.plugin.Save().next_operation == 2);
            REQUIRE(f.clock.calls == 2 && f.host.replies.size() == 2);
        }},
        {"economy_duplicate_uncertain_command_does_not_query", [] {
            Fixture f; f.host.award_results = {AwardResult::Unknown}; f.load(); f.daily();
            const auto pending = f.record(); f.clock.now = 5;
            Player reconnect{"alice"}; f.daily(&reconnect); f.reply("Pending server:1");
            REQUIRE(f.record() == pending && f.host.awards.size() == 1 && f.host.queries.empty());
            f.plugin.Tick(); REQUIRE(f.host.queries.size() == 1 && f.record().claim.status == ClaimStatus::Granted);
        }},
        {"economy_timer_respects_not_yet_due_records", [] {
            Fixture f; f.host.award_results = {AwardResult::Rejected}; f.load(); f.daily();
            const auto before = f.record();
            for (std::uint64_t tick : {0ULL, 1ULL, 4ULL}) {
                f.clock.now = tick; f.plugin.Tick(); REQUIRE(f.record() == before);
            }
            REQUIRE(f.host.awards.size() == 1 && f.host.queries.empty());
            f.clock.now = 5; f.plugin.Tick(); REQUIRE(f.host.awards.size() == 2);
        }},
        {"economy_timer_does_not_catch_up_multiple_retry_intervals", [] {
            Fixture f; f.host.award_results = {AwardResult::Rejected, AwardResult::Rejected};
            f.load(); f.daily(); f.clock.now = 1000; f.plugin.Tick();
            REQUIRE(f.host.awards.size() == 2 && f.record().claim.attempts == 2);
            REQUIRE(f.record().claim.status == ClaimStatus::Pending && f.record().claim.ready_at == 1005);
            f.plugin.Tick(); REQUIRE(f.host.awards.size() == 2);
            f.clock.now = 1005; f.plugin.Tick();
            REQUIRE(f.host.awards.size() == 3 && f.record().claim.status == ClaimStatus::Granted);
        }},
        {"economy_timer_once_per_timestamp_at_saturated_deadline", [] {
            Fixture f; f.host.award_results = {AwardResult::Rejected, AwardResult::Rejected, AwardResult::Rejected};
            f.load(); f.clock.now = MAX_TIME; f.daily();
            REQUIRE(f.record().claim.ready_at == MAX_TIME && f.record().claim.attempts == 1);
            const auto before = f.plugin.Save();
            f.plugin.Tick(); f.plugin.Tick(); f.daily(); f.reply("Pending server:1");
            REQUIRE(f.host.awards.size() == 1 && f.host.queries.empty());
            REQUIRE(f.plugin.Save() == before);
        }},
        {"economy_due_records_process_in_ascii_eos_order", [] {
            Fixture f; f.load();
            Player z{"z"}, lower{"a"}, upper{"A"}, underscore{"_"};
            for (const auto* player : {&z, &lower, &upper, &underscore}) {
                f.host.award_results.push_back(AwardResult::Rejected); f.daily(player);
            }
            f.host.awards.clear(); f.clock.now = 5; f.plugin.Tick();
            REQUIRE(f.host.awards == std::vector<AwardCall>{
                {"A", 100, "server:3"}, {"_", 100, "server:4"},
                {"a", 100, "server:2"}, {"z", 100, "server:1"}});
        }},
        {"economy_timer_never_sends_chat_replies", [] {
            Fixture f; f.host.award_results = {AwardResult::Unknown, AwardResult::Rejected};
            f.load(); f.daily(); f.daily(&f.bob);
            const auto replies = f.host.replies;
            f.clock.now = 5; f.plugin.Tick();
            REQUIRE(f.record().claim.status == ClaimStatus::Granted);
            REQUIRE(f.record("bob").claim.status == ClaimStatus::Granted);
            REQUIRE(f.host.replies == replies && replies.size() == 2);
        }},
        {"economy_confirmed_query_starts_cooldown_at_confirmation_time", [] {
            Fixture f; f.host.award_results = {AwardResult::Unknown};
            f.load(); f.clock.now = 4; f.daily();
            REQUIRE(!f.record().last_success_at && !f.record().claim.resolved_at);
            f.clock.now = 11; f.plugin.Tick();
            REQUIRE(f.record().last_success_at == 11 && f.record().claim.resolved_at == 11);
            REQUIRE(f.record().claim.last_processed_at == 11 && f.host.awards.size() == 1);
            f.clock.now = 12; f.daily(); f.reply("Cooldown: 59 seconds");
            f.clock.now = 71; f.daily(); REQUIRE(f.host.awards.size() == 2);
        }},
        {"economy_denial_does_not_observe_time_or_start_cooldown", [] {
            Fixture f; f.load(); f.clock.now = 100;
            f.host.denied.insert({"alice", "reward.claim"}); f.daily(); f.reply("Permission denied");
            REQUIRE(f.clock.calls == 0 && f.plugin.Save().players.empty() && f.plugin.Save().observed_time == 0);
            REQUIRE(f.host.awards.empty() && f.host.queries.empty() && f.plugin.Save().next_operation == 1);
            f.host.denied.clear(); f.daily(); f.reply("Granted 100 credits");
            REQUIRE(f.record().last_success_at == 100 && f.record().claim.operation == 1);
        }},
        {"economy_failed_replacement_keeps_previous_success_without_new_cooldown", [] {
            Config config; config.cooldown_seconds = 2; config.max_attempts = 2;
            Fixture f(config); f.host.award_results = {
                AwardResult::Granted, AwardResult::Rejected, AwardResult::Rejected};
            f.load(); f.clock.now = 10; f.daily();
            f.clock.now = 12; f.daily();
            REQUIRE(f.record().last_success_at == 10 && f.record().last_cooldown_seconds == 2);
            f.clock.now = 17; f.plugin.Tick();
            REQUIRE(f.record().claim.status == ClaimStatus::Failed && f.record().last_success_at == 10);
            f.daily(); f.reply("Granted 100 credits");
            REQUIRE(f.record().claim.operation == 3 && f.record().last_success_at == 17);
            REQUIRE(f.host.ledger.size() == 2);
        }},
        {"economy_pending_retry_delay_is_captured_across_reload", [] {
            Fixture f; f.host.award_results = {AwardResult::Rejected}; f.load(); f.daily();
            auto config = f.plugin.Save().config; config.retry_seconds = 1; config.cooldown_seconds = 1;
            REQUIRE(f.plugin.Reload(config));
            f.clock.now = 1; f.plugin.Tick(); REQUIRE(f.host.awards.size() == 1);
            REQUIRE(f.record().claim.retry_seconds == 5 && f.record().claim.ready_at == 5);
            f.clock.now = 5; f.plugin.Tick(); REQUIRE(f.host.awards.size() == 2);
            REQUIRE(f.record().claim.status == ClaimStatus::Granted);
            REQUIRE(f.record().claim.cooldown_seconds == 60 && f.record().last_cooldown_seconds == 60);
            f.clock.now = 6; f.daily(); f.reply("Cooldown: 59 seconds");

            Fixture uncertain; uncertain.host.award_results = {AwardResult::Unknown};
            uncertain.host.query_results = {QueryResult::Unknown}; uncertain.load(); uncertain.daily();
            auto query_config = uncertain.plugin.Save().config; query_config.retry_seconds = 1;
            REQUIRE(uncertain.plugin.Reload(query_config));
            uncertain.clock.now = 5; uncertain.plugin.Tick();
            REQUIRE(uncertain.record().claim.status == ClaimStatus::Uncertain);
            REQUIRE(uncertain.record().claim.ready_at == 10 && uncertain.record().claim.retry_seconds == 5);
            uncertain.clock.now = 6; uncertain.plugin.Tick();
            REQUIRE(uncertain.host.queries.size() == 1 && uncertain.host.awards.size() == 1);
        }},
        {"economy_pending_credits_and_attempt_budget_survive_reload", [] {
            Config config; config.reward_credits = 17; config.max_attempts = 2;
            Fixture f(config); f.host.award_results = {AwardResult::Rejected, AwardResult::Rejected};
            f.load(); f.daily();
            config.reward_credits = 99; config.max_attempts = 10; config.cooldown_seconds = 3;
            REQUIRE(f.plugin.Reload(config));
            f.clock.now = 5; f.plugin.Tick();
            REQUIRE(f.record().claim.status == ClaimStatus::Failed && f.record().claim.attempts == 2);
            REQUIRE(f.host.awards[0].credits == 17 && f.host.awards[1].credits == 17);
            f.daily(); f.reply("Granted 99 credits");
            REQUIRE(f.record().claim.max_attempts == 10 && f.record().claim.cooldown_seconds == 3);
            REQUIRE(f.host.awards.back().operation == "server:2" && f.host.awards.back().credits == 99);
        }},
        {"economy_unresolved_capacity_rejects_without_record_or_counter", [] {
            Config config; config.pending_limit = 1;
            Fixture f(config); f.host.award_results = {AwardResult::Unknown}; f.load(); f.daily();
            const auto before = f.plugin.Save(); f.daily(&f.bob); f.reply("Reward capacity reached", "bob");
            REQUIRE(f.plugin.Save() == before && f.host.awards.size() == 1 && f.host.queries.empty());
            f.clock.now = 5; f.plugin.Tick(); f.daily(&f.bob);
            REQUIRE(f.record("bob").claim.operation == 2 && f.plugin.Save().players.size() == 2);
        }},
        {"economy_retained_limit_rejects_new_eos_without_counter", [] {
            Config config; Fixture f(config);
            REQUIRE(f.plugin.Restore(retained(config, 10000))); f.load(); f.clock.now = 60;
            const auto before = f.plugin.Save(); f.daily(); f.reply("Reward capacity reached");
            REQUIRE(f.plugin.Save() == before && f.host.awards.empty() && f.host.queries.empty());
            REQUIRE(f.plugin.Save().players.size() == 10000 && f.plugin.Save().next_operation == 10001);
        }},
        {"economy_retained_limit_allows_terminal_eos_reuse", [] {
            Config config; Fixture f(config);
            REQUIRE(f.plugin.Restore(retained(config, 10000))); f.load(); f.clock.now = 60;
            Player existing{eos(0)}; f.daily(&existing); f.reply("Granted 100 credits", existing.eos_id);
            REQUIRE(f.plugin.Save().players.size() == 10000 && f.plugin.Save().next_operation == 10002);
            REQUIRE(f.host.awards == std::vector<AwardCall>{{existing.eos_id, 100, "server:10001"}});
            REQUIRE(f.record(existing.eos_id).last_success_at == 60);
        }},
        {"economy_capacity_rejection_precedes_counter_exhaustion", [] {
            Config config; config.pending_limit = 1;
            Fixture f(config); Snapshot saved; saved.config = config; saved.next_operation = MAX_TIME;
            PlayerRecord pending; pending.eos_id = "alice"; pending.claim = captured(config, 1);
            saved.players.push_back(pending);
            REQUIRE(f.plugin.Restore(saved)); f.load();
            const auto before = f.plugin.Save(); f.daily(&f.bob); f.reply("Reward capacity reached", "bob");
            REQUIRE(f.plugin.Save() == before && f.host.awards.empty());
            Config other; Fixture full(other); auto maximum = retained(other, 10000);
            maximum.next_operation = MAX_TIME;
            REQUIRE(full.plugin.Restore(maximum)); full.load(); full.clock.now = 60;
            const auto full_before = full.plugin.Save(); full.daily(); full.reply("Reward capacity reached");
            REQUIRE(full.plugin.Save() == full_before && full.host.awards.empty());
        }},
        {"economy_exhausted_operation_counter_rejects_atomically", [] {
            Fixture f; auto saved = f.plugin.Save(); saved.next_operation = MAX_TIME;
            REQUIRE(f.plugin.Restore(saved)); f.load();
            const auto before = f.plugin.Save(); f.daily(); f.reply("Operation IDs exhausted");
            REQUIRE(f.plugin.Save() == before && f.host.awards.empty() && f.host.queries.empty());
            REQUIRE(f.plugin.Save().players.empty());
        }},
        {"economy_last_assignable_operation_does_not_wrap", [] {
            Fixture f; auto saved = f.plugin.Save(); saved.next_operation = MAX_TIME - 1;
            REQUIRE(f.plugin.Restore(saved)); f.load(); f.daily();
            REQUIRE(f.host.awards.back().operation == "server:" + std::to_string(MAX_TIME - 1));
            REQUIRE(f.record().claim.operation == MAX_TIME - 1 && f.plugin.Save().next_operation == MAX_TIME);
            const auto before = f.plugin.Save(); f.daily(&f.bob); f.reply("Operation IDs exhausted", "bob");
            REQUIRE(f.plugin.Save() == before && f.host.awards.size() == 1);
            f.clock.now = 60; f.daily(); f.reply("Operation IDs exhausted");
            REQUIRE(f.record() == before.players[0] && f.plugin.Save().next_operation == MAX_TIME);
        }},
        {"economy_retry_time_addition_saturates_without_wrap", [] {
            Fixture f; f.host.award_results = {AwardResult::Rejected, AwardResult::Rejected};
            f.load(); f.clock.now = MAX_TIME - 2; f.daily();
            REQUIRE(f.record().claim.ready_at == MAX_TIME && f.record().claim.last_processed_at == MAX_TIME - 2);
            f.clock.now = MAX_TIME - 1; f.plugin.Tick(); REQUIRE(f.host.awards.size() == 1);
            f.clock.now = MAX_TIME; f.plugin.Tick();
            REQUIRE(f.host.awards.size() == 2 && f.record().claim.attempts == 2);
            REQUIRE(f.record().claim.ready_at == MAX_TIME && f.record().claim.last_processed_at == MAX_TIME);
            f.plugin.Tick(); REQUIRE(f.host.awards.size() == 2);
        }},
        {"economy_cooldown_elapsed_arithmetic_near_uint64_limit", [] {
            Fixture f; f.load(); f.clock.now = MAX_TIME - 1; f.daily();
            f.clock.now = MAX_TIME; f.daily(); f.reply("Cooldown: 59 seconds");
            REQUIRE(f.host.awards.size() == 1 && f.record().last_success_at == MAX_TIME - 1);
            f.clock.now = 0; f.daily(); f.reply("Cooldown: 59 seconds");
            REQUIRE(f.plugin.Save().observed_time == MAX_TIME && f.host.awards.size() == 1);
        }},
        {"economy_resolved_records_never_process_again", [] {
            Config config; config.max_attempts = 1;
            Fixture f(config); f.host.award_results = {AwardResult::Granted, AwardResult::Rejected};
            f.load(); f.daily(); f.daily(&f.bob);
            const auto records = f.plugin.Save().players;
            for (std::uint64_t tick : {1ULL, 5ULL, 1000ULL}) { f.clock.now = tick; f.plugin.Tick(); }
            REQUIRE(f.plugin.Save().players == records && f.host.awards.size() == 2 && f.host.queries.empty());
            REQUIRE(f.record().claim.status == ClaimStatus::Granted && f.record("bob").claim.status == ClaimStatus::Failed);
        }},
        {"economy_reconciliation_uses_monotone_confirmation_time", [] {
            Fixture f; f.host.award_results = {AwardResult::Unknown}; f.load();
            f.clock.now = 10; f.daily(); f.clock.now = 2; f.plugin.Tick();
            REQUIRE(f.host.queries.empty() && f.plugin.Save().observed_time == 10);
            f.clock.now = 15; f.plugin.Tick();
            REQUIRE(f.record().claim.resolved_at == 15 && f.record().last_success_at == 15);
            f.clock.now = 1; f.daily(&f.bob);
            REQUIRE(f.record("bob").claim.created_at == 15 && f.record("bob").claim.resolved_at == 15);
            REQUIRE(f.plugin.Save().observed_time == 15 && f.host.queries.size() == 1);
        }},
        {"economy_generated_client_ledger_reconciliation", [] {
            Config config; config.reward_credits = 7; config.cooldown_seconds = 10; config.retry_seconds = 2;
            Fixture f(config); f.load();
            for (std::size_t i = 0; i < 64; ++i) {
                Player player{eos(i)};
                f.host.unknown_commits = i % 4 != 3;
                f.host.award_results.push_back(i % 4 == 0 ? AwardResult::Granted :
                                               i % 4 == 1 ? AwardResult::Rejected : AwardResult::Unknown);
                f.daily(&player);
            }
            f.host.unknown_commits = true;
            REQUIRE(f.host.awards.size() == 64 && f.host.ledger.size() == 32 && f.host.queries.empty());
            const auto replies = f.host.replies;
            f.clock.now = 2; f.plugin.Tick();
            REQUIRE(f.host.awards.size() == 80 && f.host.queries.size() == 32 && f.host.ledger.size() == 48);
            for (std::size_t i = 0; i < 64; ++i) {
                const auto claim = f.record(eos(i)).claim;
                REQUIRE(claim.operation == i + 1);
                if (i % 4 == 3) REQUIRE(claim.status == ClaimStatus::Pending && claim.attempts == 1 && claim.ready_at == 4);
                else REQUIRE(claim.status == ClaimStatus::Granted);
            }
            f.plugin.Tick(); REQUIRE(f.host.awards.size() == 80 && f.host.queries.size() == 32);
            f.clock.now = 4; f.plugin.Tick();
            REQUIRE(f.host.awards.size() == 96 && f.host.queries.size() == 32 && f.host.ledger.size() == 64);
            REQUIRE(f.host.replies == replies && f.plugin.Save().players.size() == 64);
            for (std::size_t i = 0; i < 64; ++i) {
                const auto record = f.record(eos(i));
                const auto confirmation = i % 4 == 0 ? 0ULL : i % 4 == 3 ? 4ULL : 2ULL;
                REQUIRE(record.claim.status == ClaimStatus::Granted && record.claim.resolved_at == confirmation);
                REQUIRE(record.last_success_at == confirmation && record.last_cooldown_seconds == 10);
                REQUIRE(record.claim.attempts == (i % 4 == 0 || i % 4 == 2 ? 1U : 2U));
                const auto& entry = f.host.ledger.at("server:" + std::to_string(i + 1));
                REQUIRE(entry.eos == eos(i) && entry.credits == 7);
            }
            Player first{eos(0)}; f.clock.now = 9; f.daily(&first); f.reply("Cooldown: 1 seconds", first.eos_id);
            REQUIRE(f.host.ledger.size() == 64);
            f.clock.now = 10; f.daily(&first);
            REQUIRE(f.record(first.eos_id).claim.operation == 65 && f.host.ledger.size() == 65);
        }}
    };
}
