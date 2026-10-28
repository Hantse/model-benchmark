#include "support.hpp"
#include "allocation_probe.hpp"

#include <algorithm>
#include <array>
#include <limits>
#include <set>

namespace {
void rejected(JobQueue& queue, const std::vector<JobSpec>& batch) {
    const auto before = queue.checkpoint();
    REQUIRE(!queue.submit_batch(batch));
    REQUIRE(queue.checkpoint() == before);
}
}

std::vector<Case> admission_cases() {
    return {
        {"admission_invalid_constructor_parameters", [] {
            const std::array<std::array<std::uint64_t, 4>, 4> values{{
                {{0, 2, 10, 0}}, {{10001, 2, 10, 0}},
                {{3, 0, 10, 0}}, {{3, 2, 0, 0}}
            }};
            const auto allocations_before = live_allocations();
            for (int repetition = 0; repetition < 16; ++repetition) {
                for (const auto& v : values) {
                    bool caught = false;
                    try { JobQueue queue(static_cast<std::size_t>(v[0]),
                        static_cast<std::uint32_t>(v[1]), v[2], v[3]); }
                    catch (const std::invalid_argument&) { caught = true; }
                    REQUIRE(caught);
                }
            }
            REQUIRE(live_allocations() == allocations_before);
        }},
        {"admission_constructor_limits_and_initial_state", [] {
            JobQueue queue(10000, std::numeric_limits<std::uint32_t>::max(),
                           std::numeric_limits<std::uint64_t>::max(), 0);
            const auto saved = queue.checkpoint();
            REQUIRE(saved.capacity == 10000);
            REQUIRE(saved.max_attempts == std::numeric_limits<std::uint32_t>::max());
            REQUIRE(saved.lease_ticks == std::numeric_limits<std::uint64_t>::max());
            REQUIRE(saved.retry_delay == 0 && saved.clock == 0);
            REQUIRE(saved.next_order == 1 && saved.next_token == 1 && saved.jobs.empty());
            REQUIRE(queue.now() == 0 && queue.active_count() == 0);
        }},
        {"admission_empty_batch_preserves_nonempty_queue", [] {
            JobQueue queue(2, 3, 20, 5); add(queue, "a"); take(queue);
            REQUIRE(queue.advance_to(4));
            const auto before = queue.checkpoint();
            REQUIRE(queue.submit_batch({}));
            REQUIRE(queue.checkpoint() == before);
        }},
        {"admission_ascii_id_grammar_and_length", [] {
            JobQueue queue(8, 2, 10, 0);
            for (const auto& id : std::vector<std::string>{"", "a b", "a/b", "a.b", "a\t", "caf\xC3\xA9",
                    std::string(65, 'a'), std::string("a\0b", 3), std::string(1, static_cast<char>(0xFF))})
                rejected(queue, {spec(id)});
            add(queue, std::string(64, 'A')); add(queue, "A_9-z"); add(queue, "a_9-z");
            REQUIRE(queue.active_count() == 3);
        }},
        {"admission_payload_boundaries_and_verbatim_bytes", [] {
            JobQueue queue(4, 2, 10, 0);
            for (const auto& value : std::vector<std::string>{"", std::string(257, 'p'), std::string("x\0y", 3)})
                rejected(queue, {{"bad", value, 0, {}}});
            REQUIRE(queue.submit_batch({{"space", " \t\n ", 0, {}},
                                        {"full", std::string(256, 'p'), 0, {}}}));
            REQUIRE(get(queue, "space").spec.payload == " \t\n ");
            REQUIRE(get(queue, "full").spec.payload == std::string(256, 'p'));
        }},
        {"admission_priority_bounds_and_dispatch", [] {
            JobQueue queue(4, 2, 10, 0);
            rejected(queue, {spec("low", -101)}); rejected(queue, {spec("high", 101)});
            REQUIRE(queue.submit_batch({spec("minimum", -100), spec("maximum", 100), spec("middle", 0)}));
            REQUIRE(take(queue).spec.id == "maximum");
            REQUIRE(take(queue).spec.id == "middle");
            REQUIRE(take(queue).spec.id == "minimum");
        }},
        {"admission_dependency_ids_obey_ascii_grammar", [] {
            JobQueue queue(4, 2, 10, 0); add(queue, "root");
            for (const auto& invalid : std::vector<std::string>{"", "bad/id", "a b", std::string(65, 'z'),
                    std::string("r\0t", 3), std::string(1, static_cast<char>(0xE9))})
                rejected(queue, {spec("child", 0, {invalid})});
            add(queue, "child", 0, {"root"});
            REQUIRE(queue.active_count() == 2);
        }},
        {"admission_duplicate_dependencies_reject_atomically", [] {
            JobQueue queue(4, 2, 10, 0); add(queue, "root");
            rejected(queue, {spec("unrelated"), spec("child", 0, {"root", "root"})});
            add(queue, "child", 0, {"root"});
            REQUIRE(get(queue, "child").order == 2);
        }},
        {"admission_self_dependency_rejects_entire_batch", [] {
            JobQueue queue(4, 2, 10, 0);
            rejected(queue, {spec("good"), spec("self", 0, {"self"})});
            REQUIRE(queue.list().empty());
            add(queue, "good"); REQUIRE(take(queue).order == 1);
        }},
        {"admission_sixteen_dependencies_supported_seventeen_rejected", [] {
            JobQueue queue(32, 2, 10, 0);
            std::vector<JobSpec> roots; std::vector<std::string> deps;
            for (int i = 0; i < 17; ++i) { deps.push_back("r" + std::to_string(i)); roots.push_back(spec(deps.back())); }
            REQUIRE(queue.submit_batch(roots));
            rejected(queue, {spec("too_many", 100, deps)});
            deps.pop_back(); add(queue, "join", 100, deps);
            for (int i = 0; i < 16; ++i) { const auto job = take(queue); REQUIRE(job.spec.id == roots[i].id); succeed(queue, job); }
            REQUIRE(take(queue).spec.id == "join");
        }},
        {"admission_duplicate_ids_inside_batch_rollback", [] {
            JobQueue queue(4, 2, 10, 0);
            auto second = spec("same", 99); second.payload = "different";
            rejected(queue, {spec("prefix"), spec("same"), second});
            REQUIRE(queue.submit_batch({spec("same"), spec("prefix")}));
            REQUIRE(take(queue).spec.id == "same");
        }},
        {"admission_retained_ids_never_reusable", [] {
            JobQueue queue(4, 1, 10, 0);
            add(queue, "success"); succeed(queue, take(queue));
            add(queue, "failure"); auto failed = take(queue);
            REQUIRE(queue.complete("failure", failed.lease_token, false));
            REQUIRE(queue.submit_batch({spec("parent"), spec("blocked", 0, {"parent"})}));
            REQUIRE(queue.cancel("parent"));
            REQUIRE(get(queue, "blocked").status == Status::Blocked);
            add(queue, "queued");
            for (const auto& id : {"success", "failure", "parent", "blocked", "queued"})
                rejected(queue, {spec("fresh"), spec(id, 100)});
            REQUIRE(queue.list().size() == 5);
        }},
        {"admission_unknown_forward_reference_rolls_back", [] {
            JobQueue queue(4, 2, 10, 0);
            rejected(queue, {spec("prefix"), spec("child", 100, {"missing"})});
            REQUIRE(queue.submit_batch({spec("missing"), spec("child", 100, {"missing"})}));
            REQUIRE(take(queue).spec.id == "missing");
        }},
        {"admission_capacity_rejects_whole_batch", [] {
            JobQueue queue(3, 2, 10, 0); add(queue, "retained");
            rejected(queue, {spec("a"), spec("b"), spec("c")});
            REQUIRE(queue.submit_batch({spec("b"), spec("a")}));
            REQUIRE(get(queue, "b").order == 2 && get(queue, "a").order == 3);
        }},
        {"admission_running_jobs_consume_capacity", [] {
            JobQueue queue(2, 2, 10, 0); add(queue, "a"); add(queue, "b");
            auto a = take(queue); auto b = take(queue);
            rejected(queue, {spec("c")});
            succeed(queue, a); add(queue, "c");
            REQUIRE(queue.active_count() == 2 && get(queue, "b") == b);
        }},
        {"admission_waiting_and_delayed_jobs_consume_capacity", [] {
            JobQueue queue(3, 3, 10, 20);
            REQUIRE(queue.submit_batch({spec("parent"), spec("child", 100, {"parent"}), spec("retry", 20)}));
            auto retry = take(queue); REQUIRE(retry.spec.id == "retry");
            REQUIRE(queue.complete("retry", retry.lease_token, false));
            rejected(queue, {spec("extra")});
            REQUIRE(queue.active_count() == 3);
            REQUIRE(queue.cancel("child")); add(queue, "extra");
            REQUIRE(get(queue, "retry").ready_at == 20);
        }},
        {"admission_batch_256_accepts_with_input_order", [] {
            JobQueue queue(256, 2, 10, 0); std::vector<JobSpec> batch;
            for (int i = 255; i >= 0; --i) batch.push_back(spec("job_" + std::to_string(i)));
            REQUIRE(queue.submit_batch(batch));
            REQUIRE(queue.active_count() == 256);
            for (std::size_t i = 0; i < batch.size(); ++i) {
                auto job = take(queue); REQUIRE(job.spec.id == batch[i].id);
                REQUIRE(job.order == i + 1); succeed(queue, job);
            }
        }},
        {"admission_batch_257_rejects_without_consuming_ids", [] {
            JobQueue queue(1000, 2, 10, 0); std::vector<JobSpec> batch;
            for (int i = 0; i < 257; ++i) batch.push_back(spec("j" + std::to_string(i)));
            rejected(queue, batch); batch.pop_back(); REQUIRE(queue.submit_batch(batch));
            REQUIRE(get(queue, "j0").order == 1 && get(queue, "j255").order == 256);
        }},
        {"admission_forward_dependency_blocks_high_priority", [] {
            JobQueue queue(3, 2, 10, 0);
            REQUIRE(queue.submit_batch({spec("child", 100, {"root"}), spec("root", -100)}));
            auto root = take(queue); REQUIRE(root.spec.id == "root"); REQUIRE(!queue.claim());
            succeed(queue, root); REQUIRE(take(queue).spec.id == "child");
        }},
        {"admission_multilevel_dependencies_unlock_in_sequence", [] {
            JobQueue queue(4, 2, 10, 0);
            REQUIRE(queue.submit_batch({spec("leaf", 100, {"middle"}), spec("middle", 80, {"root"}), spec("root", -2)}));
            for (const auto& id : {"root", "middle", "leaf"}) {
                auto job = take(queue); REQUIRE(job.spec.id == id); REQUIRE(!queue.claim()); succeed(queue, job);
            }
            REQUIRE(queue.active_count() == 0);
        }},
        {"admission_diamond_requires_every_parent_success", [] {
            JobQueue queue(4, 2, 10, 0);
            REQUIRE(queue.submit_batch({spec("join", 100, {"a", "b"}), spec("a", 10, {"root"}),
                                        spec("b", 5, {"root"}), spec("root")}));
            succeed(queue, take(queue)); auto a = take(queue); auto b = take(queue);
            REQUIRE(a.spec.id == "a" && b.spec.id == "b");
            succeed(queue, a); REQUIRE(!queue.claim());
            succeed(queue, b); REQUIRE(take(queue).spec.id == "join");
        }},
        {"admission_two_job_cycle_rejects_atomically", [] {
            JobQueue queue(3, 2, 10, 0); add(queue, "existing");
            rejected(queue, {spec("a", 0, {"b"}), spec("b", 0, {"a"})});
            REQUIRE(get(queue, "existing").order == 1 && queue.active_count() == 1);
        }},
        {"admission_disconnected_cycle_is_not_overlooked", [] {
            JobQueue queue(8, 2, 10, 0);
            rejected(queue, {spec("root"), spec("good", 0, {"root"}), spec("x", 0, {"y"}),
                             spec("y", 0, {"z"}), spec("z", 0, {"x"})});
            REQUIRE(queue.list().empty());
        }},
        {"admission_long_cycle_rejects_without_partial_prefix", [] {
            JobQueue queue(128, 2, 10, 0); std::vector<JobSpec> batch;
            for (int i = 0; i < 100; ++i)
                batch.push_back(spec("n" + std::to_string(i), i % 10,
                                     {"n" + std::to_string((i + 1) % 100)}));
            rejected(queue, batch);
            batch.back().dependencies.clear(); REQUIRE(queue.submit_batch(batch));
            auto ready = take(queue); REQUIRE(ready.spec.id == "n99");
        }},
        {"admission_failed_cancelled_and_blocked_dependencies_rejected", [] {
            JobQueue queue(8, 1, 10, 0); add(queue, "failed"); auto f = take(queue);
            REQUIRE(queue.complete("failed", f.lease_token, false));
            REQUIRE(queue.submit_batch({spec("cancelled"), spec("blocked", 0, {"cancelled"})}));
            REQUIRE(queue.cancel("cancelled"));
            for (const auto& id : {"failed", "cancelled", "blocked"})
                rejected(queue, {spec("independent"), spec("child", 0, {id})});
        }},
        {"admission_succeeded_dependency_satisfies_later_batch", [] {
            JobQueue queue(2, 2, 10, 0); add(queue, "done"); succeed(queue, take(queue));
            REQUIRE(queue.advance_to(25)); add(queue, "child", 20, {"done"});
            const auto child = take(queue); REQUIRE(child.spec.id == "child");
            REQUIRE(child.ready_at == 25 && child.attempts == 1 && child.lease_deadline == 35);
        }},
        {"admission_priority_and_fifo_ignore_ascii_report_order", [] {
            JobQueue queue(5, 2, 10, 0);
            REQUIRE(queue.submit_batch({spec("z", 5), spec("a", 5), spec("middle", 9), spec("last", -1)}));
            REQUIRE(ids(queue.list()) == std::vector<std::string>({"a", "last", "middle", "z"}));
            for (const auto& id : {"middle", "z", "a", "last"}) REQUIRE(take(queue).spec.id == id);
        }},
        {"admission_retry_preserves_original_fifo_position", [] {
            JobQueue queue(3, 3, 10, 0); add(queue, "z", 4); add(queue, "a", 4);
            auto first = take(queue); REQUIRE(first.spec.id == "z");
            REQUIRE(queue.complete("z", first.lease_token, false)); add(queue, "new", 4);
            auto retry = take(queue); REQUIRE(retry.spec.id == "z" && retry.order == first.order);
            succeed(queue, retry); REQUIRE(take(queue).spec.id == "a"); REQUIRE(take(queue).spec.id == "new");
        }},
        {"admission_dependency_storage_sorted_and_input_detached", [] {
            JobQueue queue(5, 2, 10, 0);
            std::vector<JobSpec> input{spec("child", 30, {"z", "A", "a"}), spec("z"), spec("a"), spec("A")};
            REQUIRE(queue.submit_batch(input)); input[0].dependencies.clear(); input[0].payload = "mutated";
            auto child = get(queue, "child");
            REQUIRE(child.spec.dependencies == std::vector<std::string>({"A", "a", "z"}));
            child.spec.dependencies[0] = "changed";
            REQUIRE(get(queue, "child").spec.dependencies[0] == "A");
            REQUIRE(get(queue, "child").spec.payload == "payload-child");
        }},
        {"admission_report_filters_and_value_copies", [] {
            JobQueue queue(4, 2, 10, 0);
            REQUIRE(queue.submit_batch({spec("z"), spec("A"), spec("a"), spec("_" )}));
            auto original = queue.list(); auto running = take(queue);
            REQUIRE(ids(original) == std::vector<std::string>({"A", "_", "a", "z"}));
            REQUIRE(original.back().status == Status::Queued);
            REQUIRE(ids(queue.list(Status::Running)) == std::vector<std::string>({"z"}));
            REQUIRE(queue.cancel("A"));
            REQUIRE(ids(queue.list(Status::Queued)) == std::vector<std::string>({"_", "a"}));
            original[0].spec.payload = "changed"; running.spec.payload = "changed";
            REQUIRE(get(queue, "A").spec.payload == "payload-A");
            REQUIRE(get(queue, "z").spec.payload == "payload-z");
        }},
        {"admission_instances_have_independent_graph_clock_and_ids", [] {
            JobQueue first(2, 2, 10, 0), second(1, 1, 5, 0);
            add(first, "shared"); add(second, "shared");
            REQUIRE(first.advance_to(3)); auto a = take(first); auto b = take(second);
            REQUIRE(a.order == 1 && b.order == 1 && a.lease_token == 1 && b.lease_token == 1);
            REQUIRE(a.lease_deadline == 13 && b.lease_deadline == 5);
            REQUIRE(second.advance_to(5)); REQUIRE(get(second, "shared").status == Status::Failed);
            REQUIRE(get(first, "shared").status == Status::Running && first.now() == 3);
        }},
        {"admission_rejection_preserves_counters_time_and_future_ids", [] {
            JobQueue queue(5, 2, 10, 0); REQUIRE(queue.advance_to(31)); add(queue, "a");
            rejected(queue, {spec("b"), spec("bad/id")});
            rejected(queue, {spec("b"), spec("x", 0, {"unknown"})});
            REQUIRE(queue.submit_batch({spec("b"), spec("c")}));
            REQUIRE(get(queue, "b").order == 2 && get(queue, "c").order == 3);
            REQUIRE(get(queue, "b").ready_at == 31 && queue.now() == 31);
            REQUIRE(take(queue).lease_token == 1);
        }},
        {"admission_order_counter_never_wraps", [] {
            JobQueue queue(4, 2, 10, 0); auto saved = queue.checkpoint();
            const auto maximum = std::numeric_limits<std::uint64_t>::max();
            saved.next_order = maximum - 1; REQUIRE(queue.restore(saved));
            rejected(queue, {spec("a"), spec("b")}); add(queue, "a");
            REQUIRE(get(queue, "a").order == maximum - 1);
            REQUIRE(queue.checkpoint().next_order == maximum);
            rejected(queue, {spec("b")});
            REQUIRE(queue.submit_batch({}));
        }},
        {"admission_retained_history_limit_is_independent_of_capacity", [] {
            JobQueue queue(256, 1, 10, 0);
            for (int begin = 0; begin < 10000; begin += 250) {
                std::vector<JobSpec> batch;
                for (int i = begin; i < begin + 250; ++i) batch.push_back(spec("j" + std::to_string(i)));
                REQUIRE(queue.submit_batch(batch));
                for (const auto& job : batch) REQUIRE(queue.cancel(job.id));
            }
            REQUIRE(queue.active_count() == 0 && queue.list().size() == 10000);
            rejected(queue, {spec("overflow")});
            REQUIRE(queue.submit_batch({}));
        }},
        {"admission_generated_dag_matches_independent_eligible_order", [] {
            JobQueue queue(128, 2, 10, 0); std::vector<JobSpec> batch;
            constexpr int count = 96;
            for (int i = count - 1; i >= 0; --i) {
                std::vector<std::string> deps;
                if (i >= 1) deps.push_back("job_" + std::to_string((i - 1) / 2));
                if (i >= 7 && i % 3 == 0) deps.push_back("job_" + std::to_string(i - 3));
                std::sort(deps.begin(), deps.end()); deps.erase(std::unique(deps.begin(), deps.end()), deps.end());
                batch.push_back(spec("job_" + std::to_string(i), (i * 37) % 101 - 50, deps));
            }
            REQUIRE(queue.submit_batch(batch)); std::set<std::string> done;
            while (done.size() < batch.size()) {
                const JobSpec* expected = nullptr;
                for (const auto& candidate : batch) {
                    if (done.contains(candidate.id)) continue;
                    if (!std::all_of(candidate.dependencies.begin(), candidate.dependencies.end(),
                                     [&](const auto& id) { return done.contains(id); })) continue;
                    if (!expected || candidate.priority > expected->priority) expected = &candidate;
                }
                REQUIRE(expected != nullptr); auto job = take(queue);
                REQUIRE(job.spec.id == expected->id && job.attempts == 1);
                succeed(queue, job); done.insert(job.spec.id);
            }
            REQUIRE(queue.active_count() == 0 && queue.list(Status::Succeeded).size() == count);
        }},
        {"admission_ineligible_claim_preserves_state_and_token_sequence", [] {
            JobQueue queue(3, 2, 10, 0);
            const auto empty = queue.checkpoint(); REQUIRE(!queue.claim()); REQUIRE(queue.checkpoint() == empty);
            REQUIRE(queue.submit_batch({spec("child", 100, {"root"}), spec("root")}));
            auto root = take(queue); REQUIRE(root.lease_token == 1);
            const auto before = queue.checkpoint(); REQUIRE(!queue.claim()); REQUIRE(queue.checkpoint() == before);
            succeed(queue, root); auto child = take(queue);
            REQUIRE(child.spec.id == "child" && child.lease_token == 2);
        }}
    };
}
