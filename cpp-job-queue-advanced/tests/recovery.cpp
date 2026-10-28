#include "support.hpp"

#include <algorithm>
#include <iomanip>
#include <limits>
#include <sstream>

namespace {
constexpr auto MAX = std::numeric_limits<std::uint64_t>::max();

Checkpoint sample() {
    JobQueue queue(8, 3, 10, 4);
    REQUIRE(queue.submit_batch({spec("c"), spec("a"), spec("b", 9, {"c", "a"})}));
    return queue.checkpoint();
}

Checkpoint running_sample() {
    JobQueue queue(8, 3, 10, 4);
    add(queue, "a");
    take(queue);
    add(queue, "b", 9, {"a"});
    return queue.checkpoint();
}

Checkpoint single_sample(bool running = false) {
    JobQueue queue(8, 3, 10, 4);
    add(queue, "a");
    if (running) take(queue);
    return queue.checkpoint();
}

void rejects_unchanged(const Checkpoint& saved) {
    JobQueue receiver(8, 3, 10, 4);
    add(receiver, "live");
    REQUIRE(receiver.advance_to(2));
    take(receiver);
    const auto before = receiver.checkpoint();
    REQUIRE(!receiver.restore(saved));
    REQUIRE(receiver.checkpoint() == before);
}

std::string padded(std::size_t i) {
    std::ostringstream out;
    out << 'j' << std::setw(5) << std::setfill('0') << i;
    return out.str();
}
} // namespace

std::vector<Case> recovery_cases() {
    return {
        {"recovery_empty_checkpoint_metadata", [] {
            JobQueue queue(8, 3, 10, 4);
            const auto saved = queue.checkpoint();
            REQUIRE(saved.capacity == 8 && saved.max_attempts == 3);
            REQUIRE(saved.lease_ticks == 10 && saved.retry_delay == 4);
            REQUIRE(saved.clock == 0 && saved.next_order == 1 && saved.next_token == 1);
            REQUIRE(saved.jobs.empty());
        }},
        {"recovery_checkpoint_canonical_order", [] {
            const auto saved = sample();
            REQUIRE(ids(saved.jobs) == std::vector<std::string>{"a", "b", "c"});
            REQUIRE(saved.jobs[0].order == 2 && saved.jobs[1].order == 3 && saved.jobs[2].order == 1);
            REQUIRE(saved.jobs[1].spec.dependencies == std::vector<std::string>{"a", "c"});
            REQUIRE(saved.next_order == 4 && saved.next_token == 1);
        }},
        {"recovery_checkpoint_nested_copy_isolation", [] {
            JobQueue queue(8, 3, 10, 4);
            REQUIRE(queue.submit_batch({spec("a"), spec("b", 9, {"a"})}));
            auto saved = queue.checkpoint();
            const auto original = saved;
            saved.jobs[1].spec.dependencies[0] = "changed";
            saved.jobs[0].spec.payload[0] = 'X';
            saved.jobs.clear(); saved.clock = 500; saved.next_token = 99;
            REQUIRE(queue.checkpoint() == original);
            const auto first = take(queue); succeed(queue, first);
            REQUIRE(original.jobs[0].status == Status::Queued && original.clock == 0);
        }},
        {"recovery_empty_restore_replaces_live_jobs", [] {
            JobQueue queue(8, 3, 10, 4);
            const auto empty = queue.checkpoint();
            add(queue, "a"); const auto old = take(queue);
            REQUIRE(queue.advance_to(3));
            REQUIRE(queue.restore(empty));
            REQUIRE(queue.list().empty() && queue.active_count() == 0 && queue.now() == 0);
            REQUIRE(queue.checkpoint().next_order == 1 && queue.checkpoint().next_token == 2);
            add(queue, "a"); const auto fresh = take(queue);
            REQUIRE(fresh.lease_token > old.lease_token);
        }},
        {"recovery_roundtrip_delayed_queue_and_clock", [] {
            JobQueue source(8, 3, 10, 4);
            add(source, "a"); const auto first = take(source);
            REQUIRE(source.advance_to(3));
            REQUIRE(source.complete("a", first.lease_token, false));
            add(source, "b", 9, {"a"});
            const auto saved = source.checkpoint();
            JobQueue receiver(8, 3, 10, 4);
            REQUIRE(receiver.restore(saved));
            REQUIRE(receiver.checkpoint() == saved && receiver.now() == 3);
            REQUIRE(!receiver.claim());
            REQUIRE(receiver.advance_to(7));
            const auto retried = take(receiver);
            REQUIRE(retried.spec.id == "a" && retried.attempts == 2);
            succeed(receiver, retried);
            REQUIRE(take(receiver).spec.id == "b");
        }},
        {"recovery_preserves_terminal_jobs_and_blocking", [] {
            JobQueue source(8, 3, 10, 4);
            REQUIRE(source.submit_batch({spec("a", 10), spec("b", 0, {"a"}), spec("c")}));
            succeed(source, take(source));
            REQUIRE(source.cancel("b"));
            add(source, "d", 0, {"c"});
            REQUIRE(source.cancel("c"));
            const auto saved = source.checkpoint();
            JobQueue receiver(8, 3, 10, 4);
            REQUIRE(receiver.restore(saved));
            REQUIRE(receiver.checkpoint() == saved);
            REQUIRE(get(receiver, "a").status == Status::Succeeded);
            REQUIRE(get(receiver, "b").status == Status::Cancelled);
            REQUIRE(get(receiver, "c").status == Status::Cancelled);
            REQUIRE(get(receiver, "d").status == Status::Blocked);
            REQUIRE(receiver.active_count() == 0 && !receiver.claim());
        }},
        {"recovery_rejects_constructor_parameter_mismatch", [] {
            const auto good = sample();
            for (int field = 0; field < 4; ++field) {
                auto bad = good;
                if (field == 0) ++bad.capacity;
                if (field == 1) ++bad.max_attempts;
                if (field == 2) ++bad.lease_ticks;
                if (field == 3) ++bad.retry_delay;
                rejects_unchanged(bad);
            }
        }},
        {"recovery_rejects_retained_job_limit", [] {
            JobQueue receiver(10000, 3, 10, 4);
            auto bad = receiver.checkpoint();
            for (std::size_t i = 0; i < 10001; ++i) {
                Job job; job.spec = spec(padded(i)); job.status = Status::Cancelled; job.order = i + 1;
                bad.jobs.push_back(std::move(job));
            }
            bad.next_order = 10002;
            const auto before = receiver.checkpoint();
            REQUIRE(!receiver.restore(bad));
            REQUIRE(receiver.checkpoint() == before);
        }},
        {"recovery_rejects_active_capacity_overflow", [] {
            JobQueue receiver(2, 3, 10, 4);
            auto bad = receiver.checkpoint();
            for (int i = 0; i < 3; ++i) {
                Job job; job.spec = spec(std::string(1, static_cast<char>('a' + i))); job.order = i + 1;
                bad.jobs.push_back(job);
            }
            bad.next_order = 4;
            const auto before = receiver.checkpoint();
            REQUIRE(!receiver.restore(bad));
            REQUIRE(receiver.checkpoint() == before);
        }},
        {"recovery_rejects_malformed_job_specs", [] {
            // A single independent job isolates spec validation from DAG and
            // canonical sorting checks when the ID itself is corrupted.
            const auto good = single_sample();
            for (int mutation = 0; mutation < 6; ++mutation) {
                auto bad = good;
                auto& item = bad.jobs[0].spec;
                if (mutation == 0) item.id = "bad/id";
                if (mutation == 1) item.id = std::string(65, 'a');
                if (mutation == 2) item.payload.clear();
                if (mutation == 3) item.payload = std::string("x\0y", 3);
                if (mutation == 4) item.payload = std::string(257, 'x');
                if (mutation == 5) item.priority = 101;
                rejects_unchanged(bad);
            }
        }},
        {"recovery_rejects_unsorted_and_duplicate_job_ids", [] {
            JobQueue source(8, 3, 10, 4);
            add(source, "a"); add(source, "b");
            const auto good = source.checkpoint();
            auto bad = good; std::swap(bad.jobs[0], bad.jobs[1]); rejects_unchanged(bad);
            bad = good; bad.jobs[1].spec.id = bad.jobs[0].spec.id; rejects_unchanged(bad);
        }},
        {"recovery_rejects_noncanonical_dependency_lists", [] {
            auto bad = sample(); bad.jobs[1].spec.dependencies = {"c", "a"}; rejects_unchanged(bad);
            bad = sample(); bad.jobs[1].spec.dependencies = {"a", "a"}; rejects_unchanged(bad);
            bad = sample(); bad.jobs[1].spec.dependencies = {"bad/id"}; rejects_unchanged(bad);
            bad = sample(); bad.jobs[1].spec.dependencies.assign(17, "a"); rejects_unchanged(bad);
        }},
        {"recovery_rejects_missing_and_self_dependencies", [] {
            auto bad = sample(); bad.jobs[1].spec.dependencies = {"missing"}; rejects_unchanged(bad);
            bad = sample(); bad.jobs[1].spec.dependencies = {"b"}; rejects_unchanged(bad);
        }},
        {"recovery_rejects_cycles_in_disconnected_graph", [] {
            auto bad = sample();
            bad.jobs[1].spec.dependencies = {"c"};
            bad.jobs[2].spec.dependencies = {"b"};
            rejects_unchanged(bad); // "a" is an unrelated acyclic component.
            bad = sample();
            bad.jobs[0].spec.dependencies = {"b"};
            rejects_unchanged(bad); // a -> b -> a, with c also referenced.
        }},
        {"recovery_rejects_unknown_status_enum", [] {
            auto bad = single_sample(); bad.jobs[0].status = static_cast<Status>(99);
            rejects_unchanged(bad);
        }},
        {"recovery_rejects_zero_and_duplicate_orders", [] {
            auto bad = sample(); bad.jobs[0].order = 0; rejects_unchanged(bad);
            bad = sample(); bad.jobs[1].order = bad.jobs[0].order; rejects_unchanged(bad);
        }},
        {"recovery_rejects_nonadvancing_order_counter", [] {
            auto bad = sample(); bad.next_order = 0; rejects_unchanged(bad);
            bad = sample(); bad.next_order = 3; rejects_unchanged(bad);
        }},
        {"recovery_rejects_nonadvancing_token_counter", [] {
            auto bad = sample(); bad.next_token = 0; rejects_unchanged(bad);
            bad = running_sample(); bad.next_token = bad.jobs[0].lease_token;
            rejects_unchanged(bad);
        }},
        {"recovery_rejects_duplicate_running_tokens", [] {
            JobQueue queue(8, 3, 10, 4);
            add(queue, "a"); add(queue, "b"); take(queue); take(queue);
            auto bad = queue.checkpoint();
            bad.jobs[1].lease_token = bad.jobs[0].lease_token;
            rejects_unchanged(bad);
        }},
        {"recovery_rejects_attempts_beyond_state_budgets", [] {
            for (const auto status : {Status::Queued, Status::Running, Status::Succeeded,
                                      Status::Failed, Status::Cancelled}) {
                // No descendants: a terminal variant must not be rejected
                // merely because a formerly queued child now needs blocking.
                auto bad = single_sample(true); auto& job = bad.jobs[0]; job.status = status;
                job.attempts = status == Status::Queued || status == Status::Cancelled ? 3 : 4;
                if (status != Status::Running) job.lease_token = job.lease_deadline = 0;
                rejects_unchanged(bad);
            }
            // Blocked jobs necessarily have a failed/cancelled/blocked parent,
            // so any positive attempt also violates the executed-parent rule.
            // That logically coupled invariant is tested explicitly below.
        }},
        {"recovery_rejects_missing_execution_attempts", [] {
            for (const auto status : {Status::Running, Status::Succeeded, Status::Failed}) {
                auto bad = single_sample(true); auto& job = bad.jobs[0];
                job.status = status; job.attempts = status == Status::Failed ? 2 : 0;
                if (status != Status::Running) job.lease_token = job.lease_deadline = 0;
                rejects_unchanged(bad);
            }
        }},
        {"recovery_rejects_nonrunning_lease_residue", [] {
            for (const auto status : {Status::Queued, Status::Succeeded, Status::Failed, Status::Cancelled}) {
                auto good = single_sample();
                good.jobs[0].status = status;
                good.jobs[0].attempts = status == Status::Succeeded ? 1 : status == Status::Failed ? 3 : 0;
                auto bad = good; bad.jobs[0].lease_token = 1; bad.next_token = 2;
                rejects_unchanged(bad);
                bad = good; bad.jobs[0].lease_deadline = 10; rejects_unchanged(bad);
            }
            JobQueue source(8, 3, 10, 4);
            REQUIRE(source.submit_batch({spec("a"), spec("b", 0, {"a"})}));
            REQUIRE(source.cancel("a"));
            const auto blocked = source.checkpoint();
            auto bad = blocked; bad.jobs[1].lease_token = 1; bad.next_token = 2;
            rejects_unchanged(bad);
            bad = blocked; bad.jobs[1].lease_deadline = 10; rejects_unchanged(bad);
        }},
        {"recovery_rejects_invalid_running_lease_times", [] {
            const auto good = running_sample();
            for (int mutation = 0; mutation < 4; ++mutation) {
                auto bad = good; bad.clock = 5;
                if (mutation == 0) bad.jobs[0].lease_deadline = 5;
                if (mutation == 1) bad.jobs[0].ready_at = 6;
                if (mutation == 2) bad.jobs[0].lease_token = 0;
                if (mutation == 3) bad.jobs[0].lease_deadline = 0;
                rejects_unchanged(bad);
            }
        }},
        {"recovery_rejects_executed_jobs_with_unsatisfied_dependencies", [] {
            JobQueue source(8, 3, 10, 4);
            REQUIRE(source.submit_batch({spec("a", 0, {"c"}), spec("c")}));
            const auto good = source.checkpoint();
            for (const auto status : {Status::Running, Status::Succeeded, Status::Failed,
                                      Status::Queued, Status::Cancelled}) {
                auto bad = good; auto& job = bad.jobs[0]; job.status = status;
                job.attempts = status == Status::Failed ? 3 : 1;
                if (status == Status::Running) {
                    job.lease_token = 1; job.lease_deadline = 10; bad.next_token = 2;
                }
                rejects_unchanged(bad);
            }
            JobQueue blocked_source(8, 3, 10, 4);
            REQUIRE(blocked_source.submit_batch({spec("a"), spec("b", 0, {"a"})}));
            REQUIRE(blocked_source.cancel("a"));
            auto bad = blocked_source.checkpoint();
            REQUIRE(bad.jobs[1].status == Status::Blocked);
            bad.jobs[1].attempts = 1; // Below max, but its cancelled parent cannot have enabled execution.
            rejects_unchanged(bad);
        }},
        {"recovery_rejects_inconsistent_dependency_failure_states", [] {
            auto bad = sample();
            bad.jobs[2].status = Status::Cancelled; // b is still queued despite cancelled c.
            rejects_unchanged(bad);
            bad = sample(); bad.jobs[0].status = Status::Blocked; // No failed dependency.
            rejects_unchanged(bad);
            bad = sample(); bad.jobs[1].status = Status::Blocked; // All its parents still queued.
            rejects_unchanged(bad);
        }},
        {"recovery_running_jobs_requeue_with_recovery_delay", [] {
            JobQueue source(8, 3, 10, 4);
            add(source, "a"); const auto old = take(source);
            REQUIRE(source.advance_to(3)); add(source, "b", 50, {"a"});
            const auto saved = source.checkpoint();
            JobQueue receiver(8, 3, 10, 4);
            REQUIRE(receiver.restore(saved));
            const auto recovered = get(receiver, "a");
            REQUIRE(recovered.status == Status::Queued && recovered.attempts == 1);
            REQUIRE(recovered.ready_at == 7 && recovered.lease_token == 0 && recovered.lease_deadline == 0);
            REQUIRE(receiver.active_count() == 2 && receiver.now() == 3 && !receiver.claim());
            REQUIRE(!receiver.complete("a", old.lease_token, true));
            REQUIRE(receiver.advance_to(7));
            const auto fresh = take(receiver);
            REQUIRE(fresh.spec.id == "a" && fresh.attempts == 2 && fresh.order == old.order);
            REQUIRE(fresh.lease_token > old.lease_token);
        }},
        {"recovery_last_attempt_fails_and_blocks_descendants", [] {
            JobQueue queue(8, 3, 10, 4);
            REQUIRE(queue.submit_batch({spec("a"), spec("b", 50, {"a"}), spec("d", 90, {"b"})}));
            for (std::uint64_t tick : {0ULL, 4ULL}) {
                REQUIRE(queue.advance_to(tick));
                const auto job = take(queue); REQUIRE(queue.complete("a", job.lease_token, false));
            }
            REQUIRE(queue.advance_to(8)); const auto last = take(queue);
            REQUIRE(last.attempts == 3);
            const auto saved = queue.checkpoint(); REQUIRE(queue.restore(saved));
            REQUIRE(get(queue, "a").status == Status::Failed && get(queue, "a").attempts == 3);
            REQUIRE(get(queue, "a").lease_token == 0 && get(queue, "a").lease_deadline == 0);
            REQUIRE(get(queue, "b").status == Status::Blocked && get(queue, "d").status == Status::Blocked);
            REQUIRE(queue.active_count() == 0 && !queue.claim());
        }},
        {"recovery_mixed_running_attempts_and_shared_descendants", [] {
            JobQueue queue(8, 2, 10, 2);
            REQUIRE(queue.submit_batch({spec("a", 10), spec("b"), spec("c", 90, {"a", "b"}), spec("d", 100, {"c"})}));
            const auto first = take(queue); REQUIRE(queue.complete("a", first.lease_token, false));
            REQUIRE(queue.advance_to(2)); REQUIRE(take(queue).spec.id == "a");
            const auto other = take(queue); REQUIRE(other.spec.id == "b");
            REQUIRE(queue.restore(queue.checkpoint()));
            REQUIRE(get(queue, "a").status == Status::Failed);
            REQUIRE(get(queue, "b").status == Status::Queued && get(queue, "b").ready_at == 4);
            REQUIRE(get(queue, "c").status == Status::Blocked && get(queue, "d").status == Status::Blocked);
            REQUIRE(queue.active_count() == 1);
            REQUIRE(!queue.heartbeat("b", other.lease_token));
            REQUIRE(queue.advance_to(4)); const auto fresh = take(queue);
            REQUIRE(fresh.spec.id == "b" && fresh.attempts == 2); succeed(queue, fresh);
            REQUIRE(!queue.claim() && queue.active_count() == 0);
        }},
        {"recovery_old_checkpoint_preserves_receiver_token_highwater", [] {
            JobQueue queue(8, 3, 10, 4);
            add(queue, "a"); add(queue, "b"); const auto old = queue.checkpoint();
            const auto first = take(queue); succeed(queue, first);
            const auto second = take(queue); succeed(queue, second);
            REQUIRE(queue.restore(old));
            REQUIRE(queue.now() == 0 && queue.checkpoint().next_token == second.lease_token + 1);
            const auto fresh = take(queue);
            REQUIRE(fresh.spec.id == "a" && fresh.lease_token > second.lease_token);
            REQUIRE(!queue.complete("a", first.lease_token, true));
            REQUIRE(!queue.heartbeat("a", first.lease_token));
        }},
        {"recovery_new_instance_uses_saved_token_highwater", [] {
            const auto saved = running_sample(); const auto old = saved.jobs[0];
            JobQueue receiver(8, 3, 10, 4);
            REQUIRE(receiver.restore(saved));
            REQUIRE(receiver.checkpoint().next_token == saved.next_token);
            REQUIRE(receiver.advance_to(4)); const auto fresh = take(receiver);
            REQUIRE(fresh.lease_token == saved.next_token && fresh.lease_token > old.lease_token);
            REQUIRE(!receiver.complete("a", old.lease_token, true));
            succeed(receiver, fresh); REQUIRE(take(receiver).spec.id == "b");
        }},
        {"recovery_invalid_restore_does_not_poison_token_counter", [] {
            JobQueue queue(8, 3, 10, 4);
            add(queue, "live"); const auto first = take(queue); const auto before = queue.checkpoint();
            auto bad = sample(); bad.next_token = MAX; bad.jobs[0].order = 0;
            REQUIRE(!queue.restore(bad) && queue.checkpoint() == before);
            REQUIRE(queue.complete("live", first.lease_token, false));
            REQUIRE(queue.advance_to(4));
            REQUIRE(take(queue).lease_token == before.next_token);
        }},
        {"recovery_repeated_replay_never_reissues_old_tokens", [] {
            JobQueue queue(8, 3, 10, 4);
            add(queue, "a"); const auto original = take(queue); const auto saved = queue.checkpoint();
            auto last_token = original.lease_token;
            for (int replay = 0; replay < 4; ++replay) {
                REQUIRE(queue.restore(saved)); REQUIRE(queue.now() == saved.clock);
                REQUIRE(!queue.complete("a", original.lease_token, true));
                REQUIRE(queue.advance_to(4)); const auto fresh = take(queue);
                REQUIRE(fresh.attempts == 2 && fresh.lease_token > last_token);
                REQUIRE(!queue.heartbeat("a", last_token));
                last_token = fresh.lease_token; succeed(queue, fresh);
            }
        }},
        {"recovery_order_counter_boundary_is_transactional", [] {
            JobQueue queue(8, 3, 10, 4);
            auto saved = queue.checkpoint(); saved.next_order = MAX - 1;
            REQUIRE(queue.restore(saved)); const auto before = queue.checkpoint();
            REQUIRE(!queue.submit_batch({spec("a"), spec("b")}));
            REQUIRE(queue.checkpoint() == before);
            add(queue, "a"); REQUIRE(get(queue, "a").order == MAX - 1);
            REQUIRE(queue.checkpoint().next_order == MAX);
            const auto full = queue.checkpoint(); REQUIRE(!queue.submit_batch({spec("b")}));
            REQUIRE(queue.checkpoint() == full);
        }},
        {"recovery_token_counter_boundary_never_wraps", [] {
            JobQueue queue(8, 3, 10, 4);
            add(queue, "a"); add(queue, "b"); auto saved = queue.checkpoint();
            saved.next_token = MAX - 1; REQUIRE(queue.restore(saved));
            const auto last = take(queue); REQUIRE(last.lease_token == MAX - 1);
            succeed(queue, last); const auto before = queue.checkpoint();
            REQUIRE(!queue.claim() && queue.checkpoint() == before);
            REQUIRE(get(queue, "b").status == Status::Queued && queue.checkpoint().next_token == MAX);
        }},
        {"recovery_delay_saturates_and_max_clock_cannot_claim", [] {
            JobQueue queue(8, 3, 10, 4);
            REQUIRE(queue.advance_to(MAX - 2)); add(queue, "a"); const auto old = take(queue);
            REQUIRE(old.lease_deadline == MAX);
            REQUIRE(queue.restore(queue.checkpoint()));
            REQUIRE(get(queue, "a").ready_at == MAX && get(queue, "a").attempts == 1);
            REQUIRE(!queue.claim()); REQUIRE(queue.advance_to(MAX));
            const auto before = queue.checkpoint();
            REQUIRE(!queue.claim() && queue.checkpoint() == before);
            REQUIRE(!queue.complete("a", old.lease_token, true));
        }},
        {"recovery_generated_dag_retry_replay_workflows", [] {
            JobQueue queue(64, 3, 7, 2);
            std::uint64_t last_token = 0;
            for (int round = 0; round < 64; ++round) {
                const auto prefix = "wave_" + std::to_string(round) + "_";
                const auto a = prefix + "a", b = prefix + "b", c = prefix + "c", d = prefix + "d";
                REQUIRE(queue.submit_batch({spec(a, 2), spec(b, 1), spec(c, 90, {a, b}), spec(d, 100, {c})}));
                const auto root_a = take(queue), root_b = take(queue);
                REQUIRE(root_a.spec.id == a && root_b.spec.id == b);
                REQUIRE(root_a.lease_token > last_token && root_b.lease_token > root_a.lease_token);
                succeed(queue, root_a); REQUIRE(queue.complete(b, root_b.lease_token, false));
                const auto delayed = queue.checkpoint(); REQUIRE(queue.restore(delayed));
                REQUIRE(queue.checkpoint() == delayed && !queue.claim());
                REQUIRE(queue.advance_to(queue.now() + 2)); const auto retry_b = take(queue);
                REQUIRE(retry_b.spec.id == b && retry_b.attempts == 2); succeed(queue, retry_b);
                const auto child = take(queue); REQUIRE(child.spec.id == c); succeed(queue, child);
                const auto interrupted = take(queue); REQUIRE(interrupted.spec.id == d);
                REQUIRE(queue.restore(queue.checkpoint()));
                REQUIRE(!queue.complete(d, interrupted.lease_token, true) && !queue.claim());
                REQUIRE(queue.advance_to(queue.now() + 2)); const auto resumed = take(queue);
                REQUIRE(resumed.spec.id == d && resumed.attempts == 2);
                REQUIRE(resumed.lease_token > interrupted.lease_token); succeed(queue, resumed);
                last_token = resumed.lease_token;
                REQUIRE(queue.active_count() == 0 && !queue.claim());
                REQUIRE(queue.list(Status::Succeeded).size() == static_cast<std::size_t>((round + 1) * 4));
                REQUIRE(get(queue, a).attempts == 1 && get(queue, b).attempts == 2);
                REQUIRE(get(queue, c).attempts == 1 && get(queue, d).attempts == 2);
            }
            REQUIRE(queue.list().size() == 256 && queue.checkpoint().next_token == last_token + 1);
        }}
    };
}
