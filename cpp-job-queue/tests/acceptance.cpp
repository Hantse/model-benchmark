#include "job_queue.hpp"

#include <functional>
#include <iostream>
#include <map>
#include <stdexcept>
#include <string>
#include <vector>

using benchmark::Job;
using benchmark::JobQueue;
using benchmark::Status;

#define REQUIRE(expression) do { if (!(expression)) throw std::runtime_error( \
    std::string("line ") + std::to_string(__LINE__) + ": " #expression); } while (false)

void add(JobQueue& queue, const std::string& id, int priority = 0) {
    REQUIRE(queue.submit(id, "payload-" + id, priority));
}

Job next(JobQueue& queue) {
    auto value = queue.start_next();
    REQUIRE(value.has_value());
    return *value;
}

Job get(const JobQueue& queue, const std::string& id) {
    auto value = queue.find(id);
    REQUIRE(value.has_value());
    return *value;
}

std::vector<std::string> ids(const std::vector<Job>& jobs) {
    std::vector<std::string> result;
    for (const auto& job : jobs) result.push_back(job.id);
    return result;
}

const std::map<std::string, std::function<void()>> cases{
    {"empty_queue", [] {
        JobQueue queue(3, 2);
        REQUIRE(queue.active_count() == 0);
        REQUIRE(queue.list().empty());
        for (auto status : {Status::Queued, Status::Running, Status::Succeeded, Status::Failed, Status::Cancelled})
            REQUIRE(queue.list(status).empty());
        REQUIRE(!queue.find("missing"));
        REQUIRE(!queue.start_next());
    }},
    {"reject_zero_capacity", [] {
        bool rejected = false;
        try { JobQueue queue(0, 2); } catch (const std::invalid_argument&) { rejected = true; }
        REQUIRE(rejected);
    }},
    {"reject_zero_attempts", [] {
        bool rejected = false;
        try { JobQueue queue(3, 0); } catch (const std::invalid_argument&) { rejected = true; }
        REQUIRE(rejected);
    }},
    {"submit_job", [] {
        JobQueue queue(3, 2);
        REQUIRE(queue.submit("job-1", "compile", 7));
        auto value = get(queue, "job-1");
        REQUIRE(value.id == "job-1" && value.payload == "compile" && value.priority == 7);
        REQUIRE(value.status == Status::Queued && value.attempts == 0);
        REQUIRE(queue.active_count() == 1);
    }},
    {"reject_invalid_ids", [] {
        JobQueue queue(10, 2);
        for (const auto& id : std::vector<std::string>{"", "bad id", "bad/id", "bad.id", "\t", std::string(65, 'a'), "caf\xC3\xA9", std::string("a\0b", 3)})
            REQUIRE(!queue.submit(id, "payload", 0));
        add(queue, "valid_A-9");
        REQUIRE(queue.list().size() == 1);
    }},
    {"reject_invalid_payloads", [] {
        JobQueue queue(3, 2);
        REQUIRE(!queue.submit("empty", "", 0));
        REQUIRE(!queue.submit("long", std::string(257, 'x'), 0));
        REQUIRE(!queue.submit("nul", std::string("a\0b", 3), 0));
        REQUIRE(queue.submit("ok", " ", 0));
    }},
    {"reject_invalid_priorities", [] {
        JobQueue queue(3, 2);
        REQUIRE(!queue.submit("low", "payload", -101));
        REQUIRE(!queue.submit("high", "payload", 101));
        add(queue, "valid");
        REQUIRE(queue.active_count() == 1);
    }},
    {"accept_boundary_values", [] {
        JobQueue queue(3, 1);
        REQUIRE(queue.submit(std::string(64, 'A'), std::string(256, 'p'), -100));
        REQUIRE(queue.submit("Z_9-", "x", 100));
        REQUIRE(next(queue).id == "Z_9-");
        REQUIRE(next(queue).priority == -100);
    }},
    {"reject_duplicate_id", [] {
        JobQueue queue(3, 2);
        add(queue, "same", 4);
        const auto before = get(queue, "same");
        REQUIRE(!queue.submit("same", "replacement", 99));
        REQUIRE(get(queue, "same") == before);
        REQUIRE(queue.active_count() == 1);
        add(queue, "A"); add(queue, "a");
        REQUIRE(get(queue, "A").id == "A" && get(queue, "a").id == "a");
    }},
    {"capacity_counts_queued", [] {
        JobQueue queue(2, 2);
        add(queue, "one"); add(queue, "two");
        REQUIRE(!queue.submit("three", "payload", 0));
        REQUIRE(queue.active_count() == 2 && !queue.find("three"));
    }},
    {"capacity_counts_running", [] {
        JobQueue queue(1, 2);
        add(queue, "one"); REQUIRE(next(queue).id == "one");
        REQUIRE(!queue.submit("two", "payload", 0));
        REQUIRE(queue.active_count() == 1 && !queue.find("two"));
    }},
    {"invalid_submission_is_atomic", [] {
        JobQueue queue(2, 2);
        add(queue, "one");
        const auto before = queue.list();
        REQUIRE(!queue.submit("bad/id", "payload", 0));
        REQUIRE(!queue.submit("two", "", 0));
        REQUIRE(queue.list() == before && queue.active_count() == 1);
        add(queue, "two");
        REQUIRE(next(queue).id == "one");
        REQUIRE(next(queue).id == "two");
    }},
    {"start_highest_priority", [] {
        JobQueue queue(4, 2);
        add(queue, "low", -2); add(queue, "high", 8); add(queue, "middle", 3);
        REQUIRE(next(queue).id == "high");
        REQUIRE(next(queue).id == "middle");
        REQUIRE(next(queue).id == "low");
    }},
    {"start_equal_priority_fifo", [] {
        JobQueue queue(3, 2);
        add(queue, "z", 5); add(queue, "a", 5); add(queue, "m", 5);
        REQUIRE(next(queue).id == "z");
        REQUIRE(next(queue).id == "a");
        REQUIRE(next(queue).id == "m");
    }},
    {"start_records_attempt", [] {
        JobQueue queue(3, 2); add(queue, "one", 2);
        const auto value = next(queue);
        REQUIRE(value.attempts == 1 && value.status == Status::Running);
        REQUIRE(value.payload == "payload-one" && value.priority == 2);
        REQUIRE(get(queue, "one") == value);
        REQUIRE(queue.active_count() == 1);
    }},
    {"start_skips_running", [] {
        JobQueue queue(2, 2); add(queue, "a", 10); add(queue, "b", 1);
        REQUIRE(next(queue).id == "a"); REQUIRE(next(queue).id == "b");
        REQUIRE(!queue.start_next());
        REQUIRE(get(queue, "a").attempts == 1 && get(queue, "b").attempts == 1);
    }},
    {"complete_success", [] {
        JobQueue queue(2, 3); add(queue, "a"); next(queue);
        REQUIRE(queue.complete("a", true));
        REQUIRE(get(queue, "a").status == Status::Succeeded);
        REQUIRE(get(queue, "a").attempts == 1 && queue.active_count() == 0);
        REQUIRE(!queue.start_next());
    }},
    {"complete_unknown_is_rejected", [] {
        JobQueue queue(2, 2); add(queue, "a");
        const auto before = queue.list();
        REQUIRE(!queue.complete("missing", true)); REQUIRE(!queue.complete("missing", false));
        REQUIRE(queue.list() == before && queue.active_count() == 1);
    }},
    {"complete_queued_is_rejected", [] {
        JobQueue queue(2, 2); add(queue, "a"); const auto before = get(queue, "a");
        REQUIRE(!queue.complete("a", true)); REQUIRE(!queue.complete("a", false));
        REQUIRE(get(queue, "a") == before);
    }},
    {"complete_terminal_is_rejected", [] {
        JobQueue queue(3, 1); add(queue, "success"); add(queue, "failure"); add(queue, "cancelled");
        next(queue); REQUIRE(queue.complete("success", true));
        next(queue); REQUIRE(queue.complete("failure", false)); REQUIRE(queue.cancel("cancelled"));
        const auto before = queue.list();
        for (const auto& id : {"success", "failure", "cancelled"}) {
            REQUIRE(!queue.complete(id, true)); REQUIRE(!queue.complete(id, false));
        }
        REQUIRE(queue.list() == before);
    }},
    {"failed_attempt_is_requeued", [] {
        JobQueue queue(1, 3); add(queue, "a"); next(queue);
        REQUIRE(queue.complete("a", false));
        REQUIRE(get(queue, "a").status == Status::Queued && get(queue, "a").attempts == 1);
        REQUIRE(queue.active_count() == 1);
        REQUIRE(!queue.submit("b", "payload", 0));
        REQUIRE(next(queue).attempts == 2);
    }},
    {"retry_limit_is_terminal", [] {
        JobQueue queue(1, 2); add(queue, "a"); next(queue);
        REQUIRE(queue.complete("a", false)); REQUIRE(next(queue).attempts == 2);
        REQUIRE(queue.complete("a", false));
        REQUIRE(get(queue, "a").status == Status::Failed && get(queue, "a").attempts == 2);
        REQUIRE(queue.active_count() == 0 && !queue.start_next());
    }},
    {"retry_keeps_original_priority", [] {
        JobQueue queue(3, 3); add(queue, "retry", 8); add(queue, "other", 2);
        REQUIRE(next(queue).id == "retry"); REQUIRE(queue.complete("retry", false));
        REQUIRE(next(queue).id == "retry");
        REQUIRE(get(queue, "retry").priority == 8);
        REQUIRE(next(queue).id == "other");
    }},
    {"retry_keeps_fifo_position", [] {
        JobQueue queue(3, 2); add(queue, "first", 5); add(queue, "second", 5);
        REQUIRE(next(queue).id == "first"); REQUIRE(queue.complete("first", false));
        add(queue, "third", 5);
        REQUIRE(next(queue).id == "first"); REQUIRE(next(queue).id == "second");
        REQUIRE(next(queue).id == "third");
    }},
    {"cancel_queued", [] {
        JobQueue queue(2, 2); add(queue, "a");
        REQUIRE(queue.cancel("a"));
        REQUIRE(get(queue, "a").status == Status::Cancelled && get(queue, "a").attempts == 0);
        REQUIRE(queue.active_count() == 0 && !queue.start_next());
        REQUIRE(!queue.cancel("missing"));
        add(queue, "retry"); next(queue); REQUIRE(queue.complete("retry", false));
        REQUIRE(queue.cancel("retry"));
        REQUIRE(get(queue, "retry").status == Status::Cancelled && get(queue, "retry").attempts == 1);
        REQUIRE(queue.active_count() == 0 && !queue.start_next());
    }},
    {"cancel_running_is_rejected", [] {
        JobQueue queue(1, 2); add(queue, "a"); const auto running = next(queue);
        REQUIRE(!queue.cancel("a")); REQUIRE(get(queue, "a") == running);
        REQUIRE(queue.active_count() == 1);
    }},
    {"cancel_terminal_is_rejected", [] {
        JobQueue queue(3, 1); add(queue, "s"); add(queue, "f"); add(queue, "c");
        next(queue); REQUIRE(queue.complete("s", true)); next(queue); REQUIRE(queue.complete("f", false));
        REQUIRE(queue.cancel("c")); const auto before = queue.list();
        REQUIRE(!queue.cancel("s")); REQUIRE(!queue.cancel("f")); REQUIRE(!queue.cancel("c"));
        REQUIRE(queue.list() == before);
    }},
    {"terminal_releases_capacity", [] {
        JobQueue queue(1, 1); add(queue, "a"); next(queue); REQUIRE(queue.complete("a", true));
        add(queue, "b"); next(queue); REQUIRE(queue.complete("b", false));
        add(queue, "c"); REQUIRE(queue.cancel("c")); add(queue, "d");
        REQUIRE(queue.list().size() == 4 && queue.active_count() == 1);
    }},
    {"terminal_id_is_reserved", [] {
        JobQueue queue(1, 1); add(queue, "a"); next(queue); REQUIRE(queue.complete("a", true));
        REQUIRE(!queue.submit("a", "replacement", 0));
        add(queue, "b"); next(queue); REQUIRE(queue.complete("b", false));
        REQUIRE(!queue.submit("b", "replacement", 0)); add(queue, "c"); REQUIRE(queue.cancel("c"));
        REQUIRE(!queue.submit("c", "replacement", 0));
        REQUIRE(queue.active_count() == 0 && queue.list().size() == 3);
    }},
    {"find_returns_snapshot", [] {
        JobQueue queue(2, 2); add(queue, "a", 4);
        auto copy = get(queue, "a"); copy.payload = "changed"; copy.priority = 100; copy.status = Status::Succeeded;
        REQUIRE(get(queue, "a").payload == "payload-a" && get(queue, "a").priority == 4);
        REQUIRE(get(queue, "a").status == Status::Queued);
        const auto queued = get(queue, "a"); next(queue);
        REQUIRE(queued.status == Status::Queued && queued.attempts == 0);
    }},
    {"list_is_sorted_by_id", [] {
        JobQueue queue(4, 2); add(queue, "z", 100); add(queue, "a", -100); add(queue, "M", 0); add(queue, "A", 1);
        REQUIRE(ids(queue.list()) == std::vector<std::string>({"A", "M", "a", "z"}));
        next(queue);
        REQUIRE(ids(queue.list()) == std::vector<std::string>({"A", "M", "a", "z"}));
    }},
    {"list_filters_status", [] {
        JobQueue queue(5, 1); add(queue, "success", 5); add(queue, "failure", 4); add(queue, "running", 3);
        add(queue, "cancelled", 2); add(queue, "queued", 1);
        next(queue); REQUIRE(queue.complete("success", true)); next(queue); REQUIRE(queue.complete("failure", false));
        next(queue); REQUIRE(queue.cancel("cancelled"));
        REQUIRE(ids(queue.list(Status::Succeeded)) == std::vector<std::string>{"success"});
        REQUIRE(ids(queue.list(Status::Failed)) == std::vector<std::string>{"failure"});
        REQUIRE(ids(queue.list(Status::Running)) == std::vector<std::string>{"running"});
        REQUIRE(ids(queue.list(Status::Cancelled)) == std::vector<std::string>{"cancelled"});
        REQUIRE(ids(queue.list(Status::Queued)) == std::vector<std::string>{"queued"});
        REQUIRE(queue.list().size() == 5 && queue.active_count() == 2);
    }},
    {"list_returns_snapshots", [] {
        JobQueue queue(2, 2); add(queue, "a"); add(queue, "b"); auto copy = queue.list();
        copy[0].id = "changed"; copy[1].payload = "changed"; copy.clear();
        REQUIRE(ids(queue.list()) == std::vector<std::string>({"a", "b"}));
        REQUIRE(get(queue, "b").payload == "payload-b");
        auto snapshot = queue.list(); REQUIRE(queue.cancel("a"));
        REQUIRE(snapshot[0].status == Status::Queued);
    }},
    {"independent_queues", [] {
        JobQueue first(1, 1); JobQueue second(2, 3);
        add(first, "same", 2); add(second, "same", 9); next(first);
        REQUIRE(first.complete("same", false));
        REQUIRE(get(first, "same").status == Status::Failed);
        REQUIRE(get(second, "same").status == Status::Queued && get(second, "same").attempts == 0);
        REQUIRE(next(second).priority == 9); REQUIRE(second.complete("same", false));
        REQUIRE(get(second, "same").status == Status::Queued);
    }},
    {"retry_success_keeps_attempt_count", [] {
        JobQueue queue(1, 3); add(queue, "a"); next(queue); REQUIRE(queue.complete("a", false));
        next(queue); REQUIRE(queue.complete("a", true));
        REQUIRE(get(queue, "a").status == Status::Succeeded && get(queue, "a").attempts == 2);
        REQUIRE(!queue.start_next() && queue.active_count() == 0);
    }},
    {"mixed_lifecycle_workflow", [] {
        JobQueue queue(3, 2); add(queue, "batch", -1); add(queue, "urgent", 8); add(queue, "discard", 0);
        REQUIRE(!queue.submit("overflow", "payload", 0)); REQUIRE(next(queue).id == "urgent");
        REQUIRE(queue.cancel("discard")); add(queue, "new", 3); REQUIRE(queue.complete("urgent", false));
        REQUIRE(next(queue).id == "urgent"); REQUIRE(queue.complete("urgent", true));
        REQUIRE(next(queue).id == "new"); REQUIRE(queue.complete("new", false));
        REQUIRE(next(queue).id == "new"); REQUIRE(queue.complete("new", false));
        REQUIRE(next(queue).id == "batch"); REQUIRE(queue.complete("batch", true));
        REQUIRE(queue.active_count() == 0 && !queue.start_next());
        REQUIRE(queue.list(Status::Succeeded).size() == 2 && queue.list(Status::Failed).size() == 1);
        REQUIRE(queue.list(Status::Cancelled).size() == 1 && !queue.find("overflow"));
    }}
};

int main(int argc, char** argv) {
    if (argc != 2 || !cases.contains(argv[1])) {
        std::cerr << "Expected one registered acceptance case name\n";
        return 2;
    }
    try {
        cases.at(argv[1])();
        std::cout << "PASS " << argv[1] << '\n';
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "FAIL " << argv[1] << ": " << error.what() << '\n';
        return 1;
    } catch (...) {
        std::cerr << "FAIL " << argv[1] << ": unexpected exception\n";
        return 1;
    }
}
