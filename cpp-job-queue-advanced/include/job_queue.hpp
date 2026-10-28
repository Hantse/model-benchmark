#pragma once

#include <cstddef>
#include <cstdint>
#include <memory>
#include <optional>
#include <string>
#include <vector>

namespace benchmark {

enum class Status { Queued, Running, Succeeded, Failed, Cancelled, Blocked };

struct JobSpec {
    std::string id;
    std::string payload;
    int priority = 0;
    std::vector<std::string> dependencies;
    bool operator==(const JobSpec&) const = default;
};

struct Job {
    JobSpec spec;
    Status status = Status::Queued;
    std::uint32_t attempts = 0;
    std::uint64_t order = 0;
    std::uint64_t ready_at = 0;
    std::uint64_t lease_token = 0;
    std::uint64_t lease_deadline = 0;
    bool operator==(const Job&) const = default;
};

struct Checkpoint {
    std::size_t capacity = 0;
    std::uint32_t max_attempts = 0;
    std::uint64_t lease_ticks = 0;
    std::uint64_t retry_delay = 0;
    std::uint64_t clock = 0;
    std::uint64_t next_order = 1;
    std::uint64_t next_token = 1;
    std::vector<Job> jobs;
    bool operator==(const Checkpoint&) const = default;
};

// Protected contract; implement only src/. Time is explicit logical time.
class JobQueue {
public:
    JobQueue(std::size_t capacity, std::uint32_t max_attempts,
             std::uint64_t lease_ticks, std::uint64_t retry_delay);
    ~JobQueue();
    JobQueue(const JobQueue&) = delete;
    JobQueue& operator=(const JobQueue&) = delete;

    bool submit_batch(const std::vector<JobSpec>& specs);
    std::optional<Job> claim();
    bool advance_to(std::uint64_t tick);
    bool heartbeat(const std::string& id, std::uint64_t token);
    bool complete(const std::string& id, std::uint64_t token, bool success);
    bool cancel(const std::string& id);
    std::optional<Job> find(const std::string& id) const;
    std::vector<Job> list(std::optional<Status> status = std::nullopt) const;
    std::size_t active_count() const;
    std::uint64_t now() const;
    Checkpoint checkpoint() const;
    bool restore(const Checkpoint& saved);

private:
    struct Impl;
    std::unique_ptr<Impl> impl_;
};

} // namespace benchmark
