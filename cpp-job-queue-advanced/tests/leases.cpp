#include "support.hpp"
#include <cstdint>
#include <limits>

namespace {
constexpr auto MAX = std::numeric_limits<std::uint64_t>::max();
void failed(JobQueue& q, const Job& job) {
    REQUIRE(q.complete(job.spec.id, job.lease_token, false));
}
}

std::vector<Case> lease_cases() {
    return {
    {"lease_initial_clock_and_empty_views", [] {
        JobQueue q(4, 3, 10, 2);
        REQUIRE(q.now() == 0 && q.active_count() == 0 && !q.claim());
        REQUIRE(q.list().empty() && !q.find("missing"));
        for (auto status : {Status::Queued, Status::Running, Status::Succeeded,
                            Status::Failed, Status::Cancelled, Status::Blocked})
            REQUIRE(q.list(status).empty());
    }},
    {"lease_backwards_time_is_atomic", [] {
        JobQueue q(2, 3, 10, 2); add(q, "a"); auto a = take(q);
        REQUIRE(q.advance_to(4)); auto before = q.checkpoint();
        REQUIRE(!q.advance_to(3)); REQUIRE(q.checkpoint() == before);
        REQUIRE(q.complete("a", a.lease_token, true));
    }},
    {"lease_equal_time_does_not_expire_early", [] {
        JobQueue q(2, 3, 10, 2); add(q, "a"); take(q);
        auto before = q.checkpoint(); REQUIRE(q.advance_to(0));
        REQUIRE(q.checkpoint() == before);
        REQUIRE(q.advance_to(10)); before = q.checkpoint();
        REQUIRE(q.advance_to(10)); REQUIRE(q.checkpoint() == before);
    }},
    {"lease_claim_sets_complete_lease_fields", [] {
        JobQueue q(2, 3, 10, 2); REQUIRE(q.advance_to(7)); add(q, "a", 9);
        auto a = take(q);
        REQUIRE(a.status == Status::Running && a.attempts == 1);
        REQUIRE(a.ready_at == 7 && a.lease_deadline == 17 && a.lease_token == 1);
        REQUIRE(a == get(q, "a") && q.now() == 7 && q.active_count() == 1);
    }},
    {"lease_tokens_are_unique_across_jobs_and_attempts", [] {
        JobQueue q(3, 3, 10, 0); add(q, "a"); add(q, "b");
        auto a = take(q), b = take(q); failed(q, a);
        auto again = take(q);
        REQUIRE(a.lease_token == 1 && b.lease_token == 2 && again.lease_token == 3);
        REQUIRE(again.spec.id == "a" && again.attempts == 2);
    }},
    {"lease_failed_claim_consumes_no_token", [] {
        JobQueue q(3, 3, 10, 5);
        for (int i = 0; i < 20; ++i) REQUIRE(!q.claim());
        add(q, "a"); auto a = take(q); REQUIRE(a.lease_token == 1);
        failed(q, a); auto before = q.checkpoint();
        for (int i = 0; i < 20; ++i) REQUIRE(!q.claim());
        REQUIRE(q.checkpoint() == before); REQUIRE(q.advance_to(5));
        REQUIRE(take(q).lease_token == 2);
    }},
    {"lease_wrong_completion_tokens_are_atomic", [] {
        JobQueue q(2, 3, 10, 2); add(q, "a"); auto a = take(q);
        auto before = q.checkpoint();
        for (bool success : {true, false}) {
            REQUIRE(!q.complete("a", 0, success));
            REQUIRE(!q.complete("a", a.lease_token + 1, success));
            REQUIRE(!q.complete("missing", a.lease_token, success));
            REQUIRE(q.checkpoint() == before);
        }
    }},
    {"lease_cross_job_completion_is_rejected", [] {
        JobQueue q(2, 3, 10, 2); add(q, "a"); add(q, "b");
        auto a = take(q), b = take(q); auto before = q.checkpoint();
        REQUIRE(!q.complete("a", b.lease_token, true));
        REQUIRE(!q.complete("b", a.lease_token, false));
        REQUIRE(q.checkpoint() == before);
        succeed(q, a); succeed(q, b); REQUIRE(q.active_count() == 0);
    }},
    {"lease_old_attempt_completion_cannot_finish_retry", [] {
        JobQueue q(1, 3, 10, 0); add(q, "a"); auto first = take(q);
        failed(q, first); auto retry = take(q); auto before = q.checkpoint();
        REQUIRE(!q.complete("a", first.lease_token, true));
        REQUIRE(!q.complete("a", first.lease_token, false));
        REQUIRE(q.checkpoint() == before); succeed(q, retry);
    }},
    {"lease_is_valid_one_tick_before_deadline", [] {
        JobQueue q(1, 2, 10, 4); add(q, "a"); auto a = take(q);
        REQUIRE(q.advance_to(9)); REQUIRE(get(q, "a").status == Status::Running);
        succeed(q, a); REQUIRE(get(q, "a").status == Status::Succeeded);
    }},
    {"lease_expires_exactly_at_deadline", [] {
        JobQueue q(1, 2, 10, 4); add(q, "a"); auto a = take(q);
        REQUIRE(q.advance_to(10)); auto retry = get(q, "a");
        REQUIRE(retry.status == Status::Queued && retry.attempts == 1);
        REQUIRE(retry.ready_at == 14 && retry.lease_token == 0 && retry.lease_deadline == 0);
        REQUIRE(!q.complete("a", a.lease_token, true) && q.active_count() == 1);
    }},
    {"lease_advance_expires_every_running_job", [] {
        JobQueue q(4, 2, 10, 3); add(q, "a"); add(q, "b"); add(q, "c");
        take(q); take(q); REQUIRE(q.advance_to(2)); auto c = take(q);
        REQUIRE(q.advance_to(10));
        REQUIRE(q.list(Status::Queued).size() == 2 && q.list(Status::Running).size() == 1);
        REQUIRE(get(q, "a").ready_at == 13 && get(q, "b").ready_at == 13);
        REQUIRE(q.advance_to(12)); REQUIRE(get(q, "c").ready_at == 15);
        REQUIRE(!q.heartbeat("c", c.lease_token));
    }},
    {"lease_large_time_jump_expires_only_once", [] {
        JobQueue q(1, 8, 3, 5); add(q, "a"); take(q);
        REQUIRE(q.advance_to(1000000)); auto a = get(q, "a");
        REQUIRE(a.status == Status::Queued && a.attempts == 1 && a.ready_at == 1000005);
        REQUIRE(!q.claim()); REQUIRE(q.advance_to(1000005));
        REQUIRE(take(q).attempts == 2);
    }},
    {"lease_explicit_failure_obeys_retry_delay", [] {
        JobQueue q(1, 3, 20, 7); REQUIRE(q.advance_to(5)); add(q, "a");
        auto a = take(q); failed(q, a);
        REQUIRE(q.now() == 5 && get(q, "a").ready_at == 12 && !q.claim());
        REQUIRE(q.advance_to(11)); REQUIRE(!q.claim());
        REQUIRE(q.advance_to(12)); REQUIRE(take(q).attempts == 2);
    }},
    {"lease_expiry_delay_starts_at_requested_tick", [] {
        JobQueue q(1, 3, 10, 7); add(q, "a"); take(q);
        REQUIRE(q.advance_to(50)); REQUIRE(get(q, "a").ready_at == 57);
        REQUIRE(q.advance_to(56)); REQUIRE(!q.claim());
        REQUIRE(q.advance_to(57)); REQUIRE(take(q).lease_deadline == 67);
    }},
    {"lease_zero_delay_retry_is_immediately_eligible", [] {
        JobQueue q(1, 3, 10, 0); add(q, "a"); auto a = take(q); failed(q, a);
        auto second = take(q); REQUIRE(second.attempts == 2 && second.ready_at == 0);
        REQUIRE(q.advance_to(10)); auto third = take(q);
        REQUIRE(third.attempts == 3 && third.ready_at == 10 && third.lease_deadline == 20);
    }},
    {"lease_attempt_budget_counts_first_claim", [] {
        for (std::uint32_t max_attempts : {1u, 2u, 4u}) {
            JobQueue q(1, max_attempts, 10, 0); add(q, "a");
            for (std::uint32_t n = 1; n <= max_attempts; ++n) {
                auto a = take(q); REQUIRE(a.attempts == n); failed(q, a);
                REQUIRE(get(q, "a").status == (n < max_attempts ? Status::Queued : Status::Failed));
            }
            REQUIRE(!q.claim() && q.active_count() == 0);
            REQUIRE(get(q, "a").lease_token == 0 && get(q, "a").lease_deadline == 0);
        }
    }},
    {"lease_success_frees_capacity_and_unblocks_child", [] {
        JobQueue q(2, 2, 10, 4);
        REQUIRE(q.submit_batch({spec("child", 100, {"parent"}), spec("parent", -100)}));
        auto parent = take(q); REQUIRE(parent.spec.id == "parent");
        REQUIRE(!q.claim()); succeed(q, parent); REQUIRE(q.active_count() == 1);
        add(q, "extra", 0); REQUIRE(take(q).spec.id == "child");
    }},
    {"lease_retrying_parent_does_not_block_descendants", [] {
        JobQueue q(3, 2, 10, 4);
        REQUIRE(q.submit_batch({spec("a"), spec("b", 10, {"a"}), spec("c", 20, {"b"})}));
        auto a = take(q); failed(q, a);
        REQUIRE(get(q, "b").status == Status::Queued && get(q, "c").status == Status::Queued);
        REQUIRE(q.active_count() == 3 && !q.claim());
        REQUIRE(q.advance_to(4)); succeed(q, take(q));
        REQUIRE(take(q).spec.id == "b");
    }},
    {"lease_terminal_failure_blocks_shared_transitive_graph", [] {
        JobQueue q(6, 1, 10, 0);
        REQUIRE(q.submit_batch({spec("root"), spec("left", 10, {"root"}),
            spec("right", 20, {"root"}), spec("join", 30, {"left", "right"}), spec("independent")}));
        auto root = take(q); REQUIRE(root.spec.id == "root"); failed(q, root);
        REQUIRE(q.list(Status::Blocked).size() == 3 && q.active_count() == 1);
        for (auto id : {"left", "right", "join"}) {
            auto job = get(q, id); REQUIRE(job.attempts == 0 && job.lease_token == 0);
            REQUIRE(!q.cancel(id) && !q.complete(id, root.lease_token, true));
        }
        REQUIRE(take(q).spec.id == "independent");
    }},
    {"lease_cancel_dependency_waiting_job_cascades", [] {
        JobQueue q(4, 3, 10, 0);
        REQUIRE(q.submit_batch({spec("root"), spec("middle", 0, {"root"}), spec("leaf", 0, {"middle"})}));
        auto root = take(q); REQUIRE(q.cancel("middle"));
        REQUIRE(get(q, "middle").status == Status::Cancelled);
        REQUIRE(get(q, "leaf").status == Status::Blocked && q.active_count() == 1);
        succeed(q, root); REQUIRE(!q.claim());
    }},
    {"lease_cancel_delayed_retry_preserves_attempts", [] {
        JobQueue q(2, 3, 10, 50); add(q, "a"); add(q, "b", 0, {"a"});
        auto a = take(q); failed(q, a); REQUIRE(q.cancel("a"));
        auto cancelled = get(q, "a"); REQUIRE(cancelled.status == Status::Cancelled);
        REQUIRE(cancelled.attempts == 1 && cancelled.ready_at == 50 && cancelled.lease_token == 0);
        REQUIRE(get(q, "b").status == Status::Blocked && q.active_count() == 0);
    }},
    {"lease_cancel_running_and_terminal_is_atomic", [] {
        JobQueue q(3, 1, 10, 0); add(q, "a"); auto a = take(q);
        auto before = q.checkpoint(); REQUIRE(!q.cancel("a") && !q.cancel("missing"));
        REQUIRE(q.checkpoint() == before); succeed(q, a);
        add(q, "b"); failed(q, take(q)); add(q, "c"); REQUIRE(q.cancel("c"));
        before = q.checkpoint();
        for (auto id : {"a", "b", "c"}) REQUIRE(!q.cancel(id));
        REQUIRE(q.checkpoint() == before);
    }},
    {"lease_heartbeat_extends_from_current_logical_time", [] {
        JobQueue q(1, 2, 10, 3); add(q, "a"); auto a = take(q);
        REQUIRE(q.advance_to(8)); REQUIRE(q.heartbeat("a", a.lease_token));
        REQUIRE(get(q, "a").lease_deadline == 18 && q.now() == 8);
        REQUIRE(q.advance_to(10)); REQUIRE(get(q, "a").status == Status::Running);
        REQUIRE(q.advance_to(18)); REQUIRE(get(q, "a").status == Status::Queued);
    }},
    {"lease_invalid_heartbeat_has_no_side_effects", [] {
        JobQueue q(2, 2, 10, 3); add(q, "a"); add(q, "b"); auto a = take(q);
        REQUIRE(q.advance_to(5)); auto before = q.checkpoint();
        REQUIRE(!q.heartbeat("a", 0) && !q.heartbeat("a", a.lease_token + 1));
        REQUIRE(!q.heartbeat("b", a.lease_token) && !q.heartbeat("missing", a.lease_token));
        REQUIRE(q.checkpoint() == before);
    }},
    {"lease_expired_heartbeat_cannot_revive_or_extend_retry", [] {
        JobQueue q(1, 3, 10, 0); add(q, "a"); auto a = take(q);
        REQUIRE(q.advance_to(10)); auto retry = take(q); auto before = q.checkpoint();
        REQUIRE(!q.heartbeat("a", a.lease_token)); REQUIRE(q.checkpoint() == before);
        REQUIRE(q.heartbeat("a", retry.lease_token));
    }},
    {"lease_same_tick_heartbeat_is_idempotent", [] {
        JobQueue q(1, 3, 10, 0); add(q, "a"); auto a = take(q);
        REQUIRE(q.advance_to(7)); REQUIRE(q.heartbeat("a", a.lease_token));
        auto before = q.checkpoint();
        for (int i = 0; i < 20; ++i) REQUIRE(q.heartbeat("a", a.lease_token));
        REQUIRE(q.checkpoint() == before && get(q, "a").lease_deadline == 17);
    }},
    {"lease_deadline_addition_saturates", [] {
        JobQueue q(1, 2, 10, 0); REQUIRE(q.advance_to(MAX - 5)); add(q, "a"); auto a = take(q);
        REQUIRE(a.lease_deadline == MAX);
        REQUIRE(q.advance_to(MAX - 1)); REQUIRE(q.heartbeat("a", a.lease_token));
        REQUIRE(get(q, "a").lease_deadline == MAX);
        REQUIRE(q.advance_to(MAX)); REQUIRE(get(q, "a").status == Status::Queued);
        REQUIRE(!q.claim());
    }},
    {"lease_retry_delay_addition_saturates", [] {
        JobQueue q(1, 3, 2, 20); REQUIRE(q.advance_to(MAX - 10)); add(q, "a");
        auto a = take(q); failed(q, a);
        REQUIRE(get(q, "a").ready_at == MAX && !q.claim());
        REQUIRE(q.advance_to(MAX - 1)); REQUIRE(!q.claim());
        REQUIRE(q.advance_to(MAX)); REQUIRE(!q.claim() && get(q, "a").attempts == 1);
    }},
    {"lease_max_clock_refuses_unrepresentable_future", [] {
        JobQueue q(1, 3, 1, 0); REQUIRE(q.advance_to(MAX)); add(q, "a");
        auto before = q.checkpoint(); REQUIRE(!q.claim());
        REQUIRE(q.checkpoint() == before && q.active_count() == 1);
    }},
    {"lease_retry_keeps_priority_and_original_fifo", [] {
        JobQueue q(4, 3, 10, 3); add(q, "z", 5); add(q, "a", 5);
        auto z = take(q); failed(q, z); add(q, "urgent", 8); add(q, "later", 5);
        REQUIRE(q.advance_to(3)); REQUIRE(take(q).spec.id == "urgent");
        auto retry = take(q); REQUIRE(retry.spec.id == "z" && retry.attempts == 2);
        REQUIRE(take(q).spec.id == "a" && take(q).spec.id == "later");
    }},
    {"lease_invalid_operations_never_increment_attempts", [] {
        JobQueue q(2, 3, 10, 0); add(q, "a"); auto a = take(q);
        for (int n = 0; n < 20; ++n) {
            REQUIRE(!q.claim()); REQUIRE(!q.complete("a", 0, false));
            REQUIRE(!q.cancel("a")); REQUIRE(q.heartbeat("a", a.lease_token));
        }
        REQUIRE(get(q, "a").attempts == 1); failed(q, a);
        REQUIRE(take(q).attempts == 2);
    }},
    {"lease_success_is_terminal_for_all_old_acknowledgements", [] {
        JobQueue q(1, 2, 10, 0); add(q, "a"); auto a = take(q); succeed(q, a);
        auto before = q.checkpoint();
        REQUIRE(!q.complete("a", a.lease_token, true) && !q.complete("a", a.lease_token, false));
        REQUIRE(!q.heartbeat("a", a.lease_token) && !q.cancel("a") && !q.claim());
        REQUIRE(q.checkpoint() == before);
        REQUIRE(q.advance_to(100)); REQUIRE(get(q, "a").status == Status::Succeeded);
    }},
    {"lease_expired_parent_blocks_even_high_priority_child", [] {
        JobQueue q(3, 1, 5, 0);
        REQUIRE(q.submit_batch({spec("root", -100), spec("child", 100, {"root"}), spec("other", -99)}));
        auto other = take(q); REQUIRE(other.spec.id == "other"); succeed(q, other);
        REQUIRE(take(q).spec.id == "root" && !q.claim());
        REQUIRE(q.advance_to(5)); REQUIRE(get(q, "root").status == Status::Failed);
        REQUIRE(get(q, "child").status == Status::Blocked && !q.claim() && q.active_count() == 0);
    }},
    {"lease_generated_single_job_state_machine", [] {
        std::uint32_t random = 0x7351u;
        for (int scenario = 0; scenario < 64; ++scenario) {
            JobQueue q(1, 4, 7, 3); add(q, "job");
            Status expected = Status::Queued; std::uint64_t clock = 0, ready = 0, deadline = 0, token = 0;
            std::uint32_t attempts = 0;
            for (int step = 0; step < 80; ++step) {
                random = random * 1664525u + 1013904223u;
                switch ((random >> 16) % 5) {
                case 0: {
                    auto actual = q.claim(); const bool eligible = expected == Status::Queued && ready <= clock;
                    REQUIRE(actual.has_value() == eligible);
                    if (eligible) { ++attempts; expected = Status::Running; deadline = clock + 7; token = actual->lease_token; }
                    break;
                }
                case 1:
                    clock += (random >> 8) % 9;
                    REQUIRE(q.advance_to(clock));
                    if (expected == Status::Running && deadline <= clock) {
                        expected = attempts < 4 ? Status::Queued : Status::Failed;
                        if (expected == Status::Queued) ready = clock + 3;
                        token = deadline = 0;
                    }
                    break;
                case 2: {
                    const bool success = (random & 1u) != 0;
                    REQUIRE(q.complete("job", token, success) == (expected == Status::Running));
                    if (expected == Status::Running) {
                        expected = success ? Status::Succeeded : attempts < 4 ? Status::Queued : Status::Failed;
                        if (expected == Status::Queued) ready = clock + 3;
                        token = deadline = 0;
                    }
                    break;
                }
                case 3:
                    REQUIRE(q.heartbeat("job", token) == (expected == Status::Running));
                    if (expected == Status::Running) deadline = clock + 7;
                    break;
                default: {
                    auto before = q.checkpoint(); REQUIRE(!q.complete("job", MAX, true));
                    REQUIRE(q.checkpoint() == before); break;
                }
                }
                const auto job = get(q, "job");
                REQUIRE(q.now() == clock && job.status == expected && job.attempts == attempts);
                REQUIRE(job.ready_at == ready && job.lease_deadline == deadline && job.lease_token == token);
                REQUIRE(q.active_count() == (expected == Status::Queued || expected == Status::Running ? 1u : 0u));
            }
        }
    }},
    {"lease_large_failure_chain_and_instance_isolation", [] {
        JobQueue q(256, 1, 3, 0), other(1, 1, 3, 0);
        std::vector<JobSpec> batch;
        for (int n = 0; n < 256; ++n) {
            auto id = "node_" + std::to_string(n);
            batch.push_back(spec(id, 0, n == 0 ? std::vector<std::string>{} :
                                                 std::vector<std::string>{"node_" + std::to_string(n - 1)}));
        }
        REQUIRE(q.submit_batch(batch)); add(other, "node_0"); auto isolated = take(other);
        auto root = take(q); REQUIRE(root.spec.id == "node_0");
        REQUIRE(q.advance_to(3)); REQUIRE(q.list(Status::Blocked).size() == 255);
        REQUIRE(q.active_count() == 0 && !q.claim());
        REQUIRE(other.now() == 0 && get(other, "node_0") == isolated); succeed(other, isolated);
    }}
    };
}
