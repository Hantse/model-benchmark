#pragma once
#include "job_queue.hpp"
#include <functional>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

using benchmark::Checkpoint;
using benchmark::Job;
using benchmark::JobQueue;
using benchmark::JobSpec;
using benchmark::Status;
using Case = std::pair<std::string, std::function<void()>>;

#define REQUIRE(...) do { if (!(__VA_ARGS__)) throw std::runtime_error( \
    std::string("line ") + std::to_string(__LINE__) + ": " #__VA_ARGS__); } while (false)

inline JobSpec spec(std::string id, int priority = 0,
                    std::vector<std::string> dependencies = {}) {
    return {id, "payload-" + id, priority, std::move(dependencies)};
}
inline void add(JobQueue& queue, std::string id, int priority = 0,
                std::vector<std::string> dependencies = {}) {
    REQUIRE(queue.submit_batch({spec(std::move(id), priority, std::move(dependencies))}));
}
inline Job take(JobQueue& queue) {
    auto job = queue.claim(); REQUIRE(job.has_value()); return *job;
}
inline Job get(const JobQueue& queue, const std::string& id) {
    auto job = queue.find(id); REQUIRE(job.has_value()); return *job;
}
inline std::vector<std::string> ids(const std::vector<Job>& jobs) {
    std::vector<std::string> out;
    for (const auto& job : jobs) out.push_back(job.spec.id);
    return out;
}
inline void succeed(JobQueue& queue, const Job& job) {
    REQUIRE(queue.complete(job.spec.id, job.lease_token, true));
}

std::vector<Case> admission_cases();
std::vector<Case> lease_cases();
std::vector<Case> recovery_cases();
