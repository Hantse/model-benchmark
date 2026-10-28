#include "job_queue.hpp"

namespace benchmark {

// Intentionally incomplete starter. Implement the three protected features.
struct JobQueue::Impl {};
JobQueue::JobQueue(std::size_t, std::uint32_t, std::uint64_t, std::uint64_t)
    : impl_(std::make_unique<Impl>()) {}
JobQueue::~JobQueue() = default;
bool JobQueue::submit_batch(const std::vector<JobSpec>&) { return false; }
std::optional<Job> JobQueue::claim() { return std::nullopt; }
bool JobQueue::advance_to(std::uint64_t) { return false; }
bool JobQueue::heartbeat(const std::string&, std::uint64_t) { return false; }
bool JobQueue::complete(const std::string&, std::uint64_t, bool) { return false; }
bool JobQueue::cancel(const std::string&) { return false; }
std::optional<Job> JobQueue::find(const std::string&) const { return std::nullopt; }
std::vector<Job> JobQueue::list(std::optional<Status>) const { return {}; }
std::size_t JobQueue::active_count() const { return 0; }
std::uint64_t JobQueue::now() const { return 0; }
Checkpoint JobQueue::checkpoint() const { return {}; }
bool JobQueue::restore(const Checkpoint&) { return false; }

} // namespace benchmark
