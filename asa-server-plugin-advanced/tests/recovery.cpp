#include "support.hpp"
#include <algorithm>
#include <iomanip>
#include <limits>
#include <sstream>

namespace {
constexpr auto MAX = std::numeric_limits<std::uint64_t>::max();
Snapshot pending_sample() {
    Fixture f; f.load(); f.host.award_results.push_back(AwardResult::Rejected); f.daily();
    return f.plugin.Save();
}
Snapshot granted_sample() {
    Fixture f; f.load(); f.daily(); return f.plugin.Save();
}
Snapshot unattempted_sample() {
    auto saved = pending_sample(); auto& claim = saved.players[0].claim;
    claim.attempts = 0; claim.last_processed_at.reset(); claim.ready_at = 0; return saved;
}
void reject(const Snapshot& bad) {
    Fixture f;
    auto existing = granted_sample(); existing.observed_time = 123; existing.next_operation = 10;
    REQUIRE(f.plugin.Restore(existing));
    auto before = f.plugin.Save();
    REQUIRE(!f.plugin.Restore(bad)); REQUIRE(f.plugin.Save() == before);
    REQUIRE(f.host.events.empty() && f.host.awards.empty() && f.host.queries.empty());
    REQUIRE(f.clock.calls == 0);
}
std::string padded(std::size_t i) {
    std::ostringstream out; out << 'p' << std::setw(5) << std::setfill('0') << i; return out.str();
}
}

std::vector<Case> recovery_cases() {
    return {
    {"recovery_empty_metadata_without_external_calls", [] {
        Fixture f; auto saved = f.plugin.Save();
        REQUIRE(saved.config == Config{} && saved.observed_time == 0 && saved.next_operation == 1);
        REQUIRE(saved.players.empty() && f.host.events.empty() && f.clock.calls == 0);
    }},
    {"recovery_canonical_eos_order_independent_of_admission", [] {
        Fixture f; f.load(); Player z{"z"}, a{"A"}, low{"a"};
        f.daily(&z); f.daily(&low); f.daily(&a); auto saved = f.plugin.Save();
        REQUIRE(saved.players.size() == 3 && saved.players[0].eos_id == "A");
        REQUIRE(saved.players[1].eos_id == "a" && saved.players[2].eos_id == "z");
        REQUIRE(saved.players[0].claim.operation == 3 && saved.players[2].claim.operation == 1);
    }},
    {"recovery_save_deep_copies_and_earlier_snapshot_stability", [] {
        Fixture f; f.load(); f.daily(); auto original = f.plugin.Save(); auto changed = original;
        changed.players[0].eos_id = "changed"; changed.config.reward_credits = 1;
        changed.players[0].claim.credits = 1; changed.players.clear();
        REQUIRE(f.plugin.Save() == original); f.daily(&f.bob);
        REQUIRE(original.players.size() == 1 && original.next_operation == 2);
        REQUIRE(f.plugin.Save().players.size() == 2);
    }},
    {"recovery_restore_requires_unloaded_and_changes_nothing_when_loaded", [] {
        Fixture f; f.load(); f.daily(); auto before = f.plugin.Save();
        auto events = f.host.events; auto calls = f.clock.calls;
        REQUIRE(!f.plugin.Restore(Snapshot{})); REQUIRE(f.plugin.Save() == before);
        REQUIRE(f.host.events == events && f.clock.calls == calls);
    }},
    {"recovery_rejects_each_configuration_mismatch", [] {
        auto good = pending_sample();
        for (int n = 0; n < 6; ++n) {
            auto bad = good;
            if (n == 0) bad.config.operation_namespace = "other";
            if (n == 1) ++bad.config.reward_credits;
            if (n == 2) ++bad.config.cooldown_seconds;
            if (n == 3) ++bad.config.retry_seconds;
            if (n == 4) ++bad.config.max_attempts;
            if (n == 5) ++bad.config.pending_limit;
            reject(bad);
        }
    }},
    {"recovery_rejects_invalid_config_even_when_matching_receiver", [] {
        for (int n = 0; n < 6; ++n) {
            Config config;
            if (n == 0) config.operation_namespace = "bad/id";
            if (n == 1) config.reward_credits = 0;
            if (n == 2) config.cooldown_seconds = 86401;
            if (n == 3) config.retry_seconds = 0;
            if (n == 4) config.max_attempts = 11;
            if (n == 5) config.pending_limit = 0;
            Fixture f(config); Snapshot bad; bad.config = config;
            auto before = f.plugin.Save(); REQUIRE(!f.plugin.Restore(bad));
            REQUIRE(f.plugin.Save() == before && f.host.events.empty() && f.clock.calls == 0);
        }
    }},
    {"recovery_rejects_duplicate_and_unsorted_eos", [] {
        Fixture f; f.load(); f.daily(); f.daily(&f.bob); auto good = f.plugin.Save();
        auto bad = good; std::swap(bad.players[0], bad.players[1]); reject(bad);
        bad = good; bad.players[1].eos_id = bad.players[0].eos_id; reject(bad);
    }},
    {"recovery_rejects_invalid_eos_identity_bytes", [] {
        for (const auto& id : std::vector<std::string>{"", "a b", "a/b", std::string(65, 'x'),
                                                     std::string("a\0b", 3), "caf\xC3\xA9"}) {
            auto bad = pending_sample(); bad.players[0].eos_id = id; reject(bad);
        }
    }},
    {"recovery_retained_population_limit_includes_terminal_records", [] {
        auto bad = granted_sample(); auto record = bad.players[0]; bad.players.clear();
        for (std::size_t n = 0; n < 10001; ++n) {
            record.eos_id = padded(n); record.claim.operation = n + 1; bad.players.push_back(record);
        }
        bad.next_operation = 10002; reject(bad);
    }},
    {"recovery_pending_limit_counts_pending_and_uncertain_together", [] {
        Config config; config.pending_limit = 2; Fixture f(config);
        auto bad = unattempted_sample(); bad.config = config;
        auto record = bad.players[0]; bad.players.clear();
        for (int n = 0; n < 3; ++n) {
            record.eos_id = padded(n); record.claim.operation = static_cast<std::uint64_t>(n + 1);
            bad.players.push_back(record);
        }
        bad.next_operation = 4; bad.players[1].claim.status = ClaimStatus::Uncertain;
        bad.players[1].claim.attempts = 1; bad.players[1].claim.last_processed_at = 0;
        auto before = f.plugin.Save(); REQUIRE(!f.plugin.Restore(bad)); REQUIRE(f.plugin.Save() == before);
    }},
    {"recovery_rejects_zero_and_duplicate_operation_ids", [] {
        auto bad = pending_sample(); bad.players[0].claim.operation = 0; reject(bad);
        Fixture f; f.load(); f.daily(); f.daily(&f.bob); bad = f.plugin.Save();
        bad.players[1].claim.operation = bad.players[0].claim.operation; reject(bad);
    }},
    {"recovery_rejects_zero_and_nonadvancing_operation_counter", [] {
        auto bad = pending_sample(); bad.next_operation = 0; reject(bad);
        bad = pending_sample(); bad.next_operation = 1; reject(bad);
    }},
    {"recovery_validates_all_captured_economy_parameters", [] {
        for (int n = 0; n < 8; ++n) {
            auto bad = unattempted_sample(); auto& claim = bad.players[0].claim;
            if (n == 0) claim.credits = 0;
            if (n == 1) claim.credits = 10001;
            if (n == 2) claim.cooldown_seconds = 0;
            if (n == 3) claim.cooldown_seconds = 86401;
            if (n == 4) claim.retry_seconds = 0;
            if (n == 5) claim.retry_seconds = 3601;
            if (n == 6) claim.max_attempts = 0;
            if (n == 7) claim.max_attempts = 11;
            reject(bad);
        }
    }},
    {"recovery_rejects_unknown_claim_status", [] {
        auto bad = pending_sample(); bad.players[0].claim.status = static_cast<ClaimStatus>(99); reject(bad);
    }},
    {"recovery_rejects_creation_after_observed_time", [] {
        auto bad = unattempted_sample(); bad.players[0].claim.created_at = 1;
        bad.players[0].claim.ready_at = 1; reject(bad);
    }},
    {"recovery_rejects_ready_before_creation", [] {
        auto bad = unattempted_sample(); bad.observed_time = 10;
        bad.players[0].claim.created_at = 5; bad.players[0].claim.ready_at = 4; reject(bad);
    }},
    {"recovery_validates_processing_timestamp_interval", [] {
        auto bad = pending_sample(); bad.observed_time = 10;
        bad.players[0].claim.created_at = 5; bad.players[0].claim.ready_at = 15;
        bad.players[0].claim.last_processed_at = 4; reject(bad);
        bad.players[0].claim.last_processed_at = 11; reject(bad);
        bad = pending_sample(); bad.players[0].claim.ready_at = 0;
        bad.observed_time = 10; bad.players[0].claim.last_processed_at = 1; reject(bad);
    }},
    {"recovery_unattempted_claim_cannot_have_processed_timestamp", [] {
        auto bad = unattempted_sample(); bad.players[0].claim.last_processed_at = 0; reject(bad);
    }},
    {"recovery_positive_attempts_require_processing_evidence", [] {
        auto bad = pending_sample(); bad.players[0].claim.last_processed_at.reset(); reject(bad);
        bad = granted_sample(); bad.players[0].claim.last_processed_at.reset(); reject(bad);
        bad = pending_sample(); bad.players[0].claim.status = ClaimStatus::Failed;
        bad.players[0].claim.attempts = 3; bad.players[0].claim.ready_at = 0;
        bad.players[0].claim.resolved_at = 0; bad.players[0].claim.last_processed_at.reset(); reject(bad);
        bad = pending_sample(); bad.players[0].claim.status = ClaimStatus::Uncertain;
        bad.players[0].claim.last_processed_at.reset(); // ready=5 != observed=0: not normalized.
        reject(bad);
    }},
    {"recovery_attempt_budgets_match_every_state", [] {
        auto bad = pending_sample(); bad.players[0].claim.attempts = 3; reject(bad);
        bad = pending_sample(); bad.players[0].claim.attempts = 4; reject(bad);
        bad = unattempted_sample(); bad.players[0].claim.status = ClaimStatus::Uncertain; reject(bad);
        bad = granted_sample(); bad.players[0].claim.attempts = 0;
        bad.players[0].claim.last_processed_at.reset(); reject(bad);
        bad = pending_sample(); bad.players[0].claim.status = ClaimStatus::Failed;
        bad.players[0].claim.resolved_at = 0; bad.players[0].claim.ready_at = 0;
        bad.players[0].claim.attempts = 2; reject(bad);
    }},
    {"recovery_unresolved_claim_cannot_be_resolved", [] {
        auto bad = pending_sample(); bad.players[0].claim.resolved_at = 0; reject(bad);
        bad.players[0].claim.status = ClaimStatus::Uncertain; reject(bad);
    }},
    {"recovery_terminal_resolution_has_complete_time_evidence", [] {
        auto good = granted_sample(); auto bad = good;
        bad.players[0].claim.resolved_at.reset(); reject(bad);
        bad = good; bad.players[0].claim.resolved_at = 1; bad.players[0].last_success_at = 1; reject(bad);
        bad = good; bad.players[0].claim.ready_at = 1; reject(bad);
        bad = good; bad.observed_time = 5; bad.players[0].claim.last_processed_at = 2; reject(bad);
        bad = good; bad.observed_time = 10; bad.players[0].claim.created_at = 5;
        bad.players[0].claim.ready_at = 5; bad.players[0].claim.last_processed_at = 5;
        bad.players[0].claim.resolved_at = 4; bad.players[0].last_success_at = 4; reject(bad);
    }},
    {"recovery_last_success_timestamp_and_cooldown_are_consistent", [] {
        auto bad = unattempted_sample(); bad.players[0].last_cooldown_seconds = 1; reject(bad);
        bad = unattempted_sample(); bad.players[0].last_success_at = 0; reject(bad);
        bad.players[0].last_cooldown_seconds = 86401; reject(bad);
        bad = unattempted_sample(); bad.players[0].last_success_at = 1;
        bad.players[0].last_cooldown_seconds = 1; reject(bad);
    }},
    {"recovery_granted_claim_matches_record_success_exactly", [] {
        auto bad = granted_sample(); bad.players[0].last_success_at.reset();
        bad.players[0].last_cooldown_seconds = 0; reject(bad);
        bad = granted_sample(); ++bad.players[0].last_cooldown_seconds; reject(bad);
        bad = granted_sample(); bad.observed_time = 5; bad.players[0].last_success_at = 1; reject(bad);
    }},
    {"recovery_later_claim_requires_prior_cooldown_to_have_elapsed", [] {
        auto bad = unattempted_sample(); bad.observed_time = 100;
        auto& record = bad.players[0]; record.last_success_at = 80; record.last_cooldown_seconds = 60;
        record.claim.created_at = 100; record.claim.ready_at = 100; reject(bad);
        record.claim.created_at = 70; record.claim.ready_at = 100; reject(bad);
    }},
    {"recovery_terminal_roundtrip_is_exact_and_has_no_external_calls", [] {
        auto saved = granted_sample(); Fixture f; REQUIRE(f.plugin.Restore(saved));
        REQUIRE(f.plugin.Save() == saved && f.host.events.empty() && f.clock.calls == 0);
        f.load(); f.clock.now = 5; f.plugin.Tick();
        REQUIRE(f.host.awards.empty() && f.host.queries.empty()); f.daily(); f.reply("Cooldown: 55 seconds");
    }},
    {"recovery_attempted_pending_reconciles_before_any_reaward", [] {
        auto saved = pending_sample(); Fixture f; REQUIRE(f.plugin.Restore(saved));
        auto claim = f.record().claim;
        REQUIRE(claim.status == ClaimStatus::Uncertain && claim.ready_at == 0 && !claim.last_processed_at);
        REQUIRE(f.host.queries.empty() && f.host.awards.empty()); f.load(); f.plugin.Tick();
        REQUIRE(f.host.queries == std::vector<std::string>{"server:1"} && f.host.awards.empty());
        REQUIRE(f.record().claim.status == ClaimStatus::Pending && f.record().claim.ready_at == 5);
        f.clock.now = 5; f.plugin.Tick();
        REQUIRE(f.host.awards.size() == 1 && f.host.awards[0].operation == "server:1");
        REQUIRE(f.record().claim.status == ClaimStatus::Granted);
    }},
    {"recovery_unknown_committed_award_is_queried_without_duplicate", [] {
        FakeHost host; FakeClock clock; Snapshot saved;
        {
            RewardPlugin source(host, clock, {}); REQUIRE(source.Load());
            host.award_results.push_back(AwardResult::Unknown); Player alice{"alice"};
            source.HandleChat(&alice, "/daily"); saved = source.Save();
        }
        REQUIRE(host.ledger.size() == 1 && host.awards.size() == 1);
        RewardPlugin receiver(host, clock, {}); REQUIRE(receiver.Restore(saved)); REQUIRE(receiver.Load());
        clock.now = 3; receiver.Tick();
        REQUIRE(host.awards.size() == 1 && host.queries.size() == 1 && host.ledger.size() == 1);
        auto record = receiver.Save().players[0];
        REQUIRE(record.claim.status == ClaimStatus::Granted && record.last_success_at == 3);
    }},
    {"recovery_failed_load_preserves_restored_claims_and_counters", [] {
        auto saved = pending_sample(); Fixture f; REQUIRE(f.plugin.Restore(saved));
        auto before = f.plugin.Save(); f.host.fail_timer = true;
        REQUIRE(!f.plugin.Load()); REQUIRE(f.plugin.Save() == before && !f.plugin.IsLoaded());
        REQUIRE(f.host.chats.empty() && f.host.timers.empty());
        f.plugin.Unload(); REQUIRE(f.plugin.Save() == before);
        f.host.fail_timer = false; f.load(); f.plugin.Tick();
        REQUIRE(f.host.queries.size() == 1 && f.host.awards.empty());
    }},
    {"recovery_save_after_restore_can_be_restored_again", [] {
        Fixture first; REQUIRE(first.plugin.Restore(pending_sample()));
        auto normalized = first.plugin.Save();
        REQUIRE(normalized.players[0].claim.status == ClaimStatus::Uncertain);
        REQUIRE(!normalized.players[0].claim.last_processed_at);
        Fixture second; REQUIRE(second.plugin.Restore(normalized)); REQUIRE(second.plugin.Save() == normalized);
        REQUIRE(second.plugin.Restore(second.plugin.Save()));
        second.load(); second.plugin.Tick(); REQUIRE(second.host.queries.size() == 1 && second.host.awards.empty());
        // A pending chat observes later time without processing the recovered claim.
        // Saving that immediately-due state must still produce a valid snapshot.
        first.load(); first.clock.now = 10; first.daily();
        auto later = first.plugin.Save(); REQUIRE(later.observed_time == 10);
        REQUIRE(!later.players[0].claim.last_processed_at && later.players[0].claim.ready_at == 0);
        Fixture third; REQUIRE(third.plugin.Restore(later)); third.load(); third.plugin.Tick();
        REQUIRE(third.host.queries.size() == 1 && third.host.awards.empty());
    }},
    {"recovery_old_snapshot_cannot_reuse_issued_operation_id", [] {
        Fixture f; auto empty = f.plugin.Save(); f.load(); f.daily(); f.daily(&f.bob);
        REQUIRE(f.plugin.Save().next_operation == 3); f.plugin.Unload();
        REQUIRE(f.plugin.Restore(empty)); REQUIRE(f.plugin.Save().next_operation == 3);
        f.load(); f.daily(); REQUIRE(f.host.awards.back().operation == "server:3");
        REQUIRE(f.host.ledger.size() == 3);
    }},
    {"recovery_observed_time_highwater_survives_old_snapshot", [] {
        Fixture f; f.load(); f.daily(); auto old = f.plugin.Save();
        f.clock.now = 100; f.plugin.Tick(); f.plugin.Unload(); REQUIRE(f.plugin.Restore(old));
        REQUIRE(f.plugin.Save().observed_time == 100);
        f.clock.now = 0; f.load(); f.daily();
        REQUIRE(f.host.awards.back().operation == "server:2");
        REQUIRE(f.record().last_success_at == 100 && f.plugin.Save().observed_time == 100);
    }},
    {"recovery_operation_counter_exhaustion_never_wraps", [] {
        Fixture f; auto saved = f.plugin.Save(); saved.next_operation = MAX - 1;
        REQUIRE(f.plugin.Restore(saved)); f.load(); f.daily();
        REQUIRE(f.host.awards.back().operation == "server:" + std::to_string(MAX - 1));
        REQUIRE(f.plugin.Save().next_operation == MAX); f.daily(&f.bob);
        f.reply("Operation IDs exhausted", "bob"); REQUIRE(f.host.awards.size() == 1);
        auto before = f.plugin.Save(); f.plugin.Unload(); saved.next_operation = 1;
        REQUIRE(f.plugin.Restore(saved)); REQUIRE(f.plugin.Save().next_operation == before.next_operation);
    }},
    {"recovery_saturated_retry_and_normalized_reconciliation", [] {
        Fixture source; source.load(); source.clock.now = MAX - 2;
        source.host.award_results.push_back(AwardResult::Unknown); source.daily();
        auto saved = source.plugin.Save(); REQUIRE(saved.players[0].claim.ready_at == MAX);
        Fixture target; target.host.unknown_commits = false;
        REQUIRE(target.plugin.Restore(saved)); target.clock.now = MAX; target.load(); target.plugin.Tick();
        REQUIRE(target.host.queries.size() == 1 && target.host.awards.empty());
        REQUIRE(target.record().claim.ready_at == MAX && target.record().claim.status == ClaimStatus::Pending);
        for (int n = 0; n < 5; ++n) target.plugin.Tick();
        REQUIRE(target.host.queries.size() == 1 && target.host.awards.empty());
    }},
    {"recovery_restore_input_remains_detached_from_live_state", [] {
        Fixture f; auto saved = unattempted_sample(); REQUIRE(f.plugin.Restore(saved));
        auto before = f.plugin.Save(); saved.players[0].claim.credits = 999;
        saved.players[0].eos_id = "wrong"; saved.config.operation_namespace = "other";
        saved.players.clear(); REQUIRE(f.plugin.Save() == before);
        f.load(); f.plugin.Tick(); REQUIRE(f.host.awards.size() == 1);
        REQUIRE(f.host.awards[0] == AwardCall{"alice", 100, "server:1"});
        REQUIRE(f.host.queries.empty());
    }},
    {"recovery_generated_restart_workflows_preserve_single_grant_ledger", [] {
        FakeHost host; FakeClock clock; Snapshot saved;
        for (std::size_t wave = 0; wave < 64; ++wave) {
            clock.now = wave * 10;
            RewardPlugin plugin(host, clock, {}); REQUIRE(plugin.Restore(saved)); REQUIRE(plugin.Load());
            plugin.Tick(); // Reconcile the preceding uncertain committed operation.
            Player player{padded(wave)}; host.award_results.push_back(AwardResult::Unknown);
            plugin.HandleChat(&player, "/daily"); saved = plugin.Save();
            REQUIRE(host.awards.size() == wave + 1 && host.ledger.size() == wave + 1);
            REQUIRE(saved.next_operation == wave + 2 && saved.players.size() == wave + 1);
        }
        clock.now = 700; RewardPlugin final(host, clock, {});
        REQUIRE(final.Restore(saved)); REQUIRE(final.Load()); final.Tick();
        REQUIRE(host.awards.size() == 64 && host.ledger.size() == 64 && host.queries.size() == 64);
        for (const auto& record : final.Save().players) REQUIRE(record.claim.status == ClaimStatus::Granted);
    }}
    };
}
