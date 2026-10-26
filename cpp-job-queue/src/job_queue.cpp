#include "job_queue.hpp"

namespace benchmark {

// The starter deliberately leaves all three features incomplete.
struct JobQueue::Impl {};

JobQueue::JobQueue(std::size_t, std::uint32_t) : impl_(new Impl) {}
JobQueue::~JobQueue() { delete impl_; }

bool JobQueue::submit(std::string, std::string, int) { return false; }
std::optional<Job> JobQueue::start_next() { return std::nullopt; }
bool JobQueue::complete(const std::string&, bool) { return false; }
bool JobQueue::cancel(const std::string&) { return false; }
std::optional<Job> JobQueue::find(const std::string&) const { return std::nullopt; }
std::vector<Job> JobQueue::list(std::optional<Status>) const { return {}; }
std::size_t JobQueue::active_count() const { return 0; }

} // namespace benchmark
