#include "job_queue.hpp"

#include <algorithm>
#include <cctype>
#include <stdexcept>
#include <unordered_set>
#include <vector>

namespace benchmark {

namespace {

bool valid_id(const std::string& id) {
    if (id.empty() || id.size() > 64) return false;
    for (unsigned char c : id) {
        const bool ok = std::isalnum(c) != 0 || c == '_' || c == '-';
        if (!ok) return false;
    }
    return true;
}

bool valid_payload(const std::string& payload) {
    return !payload.empty() && payload.size() <= 256 && payload.find('\0') == std::string::npos;
}

} // namespace

struct JobQueue::Impl {
    std::size_t capacity;
    std::uint32_t max_attempts;
    std::vector<Job> jobs;
    std::unordered_set<std::string> known_ids;

    Impl(std::size_t capacity, std::uint32_t max_attempts)
        : capacity(capacity), max_attempts(max_attempts) {}

    std::size_t active() const {
        std::size_t count = 0;
        for (const auto& job : jobs)
            if (job.status == Status::Queued || job.status == Status::Running) ++count;
        return count;
    }
};

JobQueue::JobQueue(std::size_t capacity, std::uint32_t max_attempts)
    : impl_(new Impl(capacity, max_attempts)) {
    if (capacity == 0 || max_attempts == 0) throw std::invalid_argument("capacity and max_attempts must be positive");
}
JobQueue::~JobQueue() { delete impl_; }

bool JobQueue::submit(std::string id, std::string payload, int priority) {
    if (!valid_id(id) || !valid_payload(payload) || priority < -100 || priority > 100)
        return false;
    if (impl_->known_ids.count(id) != 0) return false;
    if (impl_->active() >= impl_->capacity) return false;
    impl_->jobs.push_back(Job{std::move(id), std::move(payload), priority, 0, Status::Queued});
    impl_->known_ids.insert(impl_->jobs.back().id);
    return true;
}

std::optional<Job> JobQueue::start_next() {
    std::size_t best = 0;
    bool found = false;
    for (std::size_t i = 0; i < impl_->jobs.size(); ++i) {
        if (impl_->jobs[i].status != Status::Queued) continue;
        if (!found || impl_->jobs[i].priority > impl_->jobs[best].priority) { best = i; found = true; }
    }
    if (!found) return std::nullopt;
    impl_->jobs[best].status = Status::Running;
    ++impl_->jobs[best].attempts;
    return impl_->jobs[best];
}

bool JobQueue::complete(const std::string& id, bool success) {
    for (auto& job : impl_->jobs) {
        if (job.id != id) continue;
        if (job.status != Status::Running) return false;
        if (success) {
            job.status = Status::Succeeded;
        } else if (job.attempts < impl_->max_attempts) {
            job.status = Status::Queued;
        } else {
            job.status = Status::Failed;
        }
        return true;
    }
    return false;
}

bool JobQueue::cancel(const std::string& id) {
    for (auto& job : impl_->jobs) {
        if (job.id != id) continue;
        if (job.status != Status::Queued) return false;
        job.status = Status::Cancelled;
        return true;
    }
    return false;
}

std::optional<Job> JobQueue::find(const std::string& id) const {
    for (const auto& job : impl_->jobs)
        if (job.id == id) return job;
    return std::nullopt;
}

std::vector<Job> JobQueue::list(std::optional<Status> status) const {
    std::vector<Job> result;
    for (const auto& job : impl_->jobs)
        if (!status || job.status == *status) result.push_back(job);
    std::sort(result.begin(), result.end(), [](const Job& a, const Job& b) { return a.id < b.id; });
    return result;
}

std::size_t JobQueue::active_count() const { return impl_->active(); }

} // namespace benchmark
