#pragma once

#include <cstddef>
#include <cstdint>
#include <optional>
#include <string>
#include <vector>

namespace benchmark {

enum class Status { Queued, Running, Succeeded, Failed, Cancelled };

struct Job {
    std::string id;
    std::string payload;
    int priority;
    std::uint32_t attempts;
    Status status;
    bool operator==(const Job&) const = default;
};

// This public contract is protected. Add private implementation state in src/.
class JobQueue {
public:
    explicit JobQueue(std::size_t capacity, std::uint32_t max_attempts);
    ~JobQueue();
    JobQueue(const JobQueue&) = delete;
    JobQueue& operator=(const JobQueue&) = delete;

    bool submit(std::string id, std::string payload, int priority);
    std::optional<Job> start_next();
    bool complete(const std::string& id, bool success);
    bool cancel(const std::string& id);
    std::optional<Job> find(const std::string& id) const;
    std::vector<Job> list(std::optional<Status> status = std::nullopt) const;
    std::size_t active_count() const;

private:
    struct Impl;
    Impl* impl_;
};

} // namespace benchmark
